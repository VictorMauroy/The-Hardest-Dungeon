using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using OutlineEffect;
public enum EnemyAIState
{
    Idle, //Rester sur place.
    Wandering,
    Searching,
    StepBackToInitialPos
}

[System.Serializable]
public class EnemyStateCharacteristics
{
    public EnemyAIState aIState;
    public float probability;
}

public class BasicEnemy : MonoBehaviour
{
    private GrabbableTarget grabbableBehavior;

    //Si l'entité peut se déplacer selon son propre désir ou subit un déplacement forcé.
    [HideInInspector] public bool canFreelyMove;

    public static EnemyImpact OnEnemyImpact;
    public static Pouf OnPouf;
    public static EnemyDamageReceived OnDamagesReceived;

    [Header("Movements")]
    public bool falling; //Chute de l'entité : Incapacité durant cette période.
    public bool grounded;
    /*private float verticalVelocity;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float gravityScale;
    private RaycastHit groundHit;*/
    [SerializeField] private float heightOffset;
    private float fallTime;
    private const float TimeBeforeDyingByFall = 6f;

    [Header("IA")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField, Tooltip("Select the behaviors for this enemy. Remember to make the probability sum equal to 100%")] 
    private List<EnemyStateCharacteristics> thisEnemyStates;
    [SerializeField] private EnemyAIState currentState;
    [SerializeField] private float minChangeTime, maxChangeTime;
    public float changeTime;
    [SerializeField] private float minWanderingDistance;
    [SerializeField] private float maxWanderingDistance;
    private Vector3 initialPosition;
    [SerializeField] private Transform searchingPointsParent;
    private List<Vector3> searchingPoints = new List<Vector3>();
    int patrolIndex;
    Coroutine distanceCheckCoroutine;
    bool destinationReached;

    [Header("Attack & Pursuit")]
    public bool attackMode;
    private Transform playerTr;
    private Vector3 lastSeenPlayerPosition;
    [HideInInspector] public int currentLife;
    [SerializeField] private int maxLife;
    [SerializeField] private Outline enemyOutline;
    private float damageOutlineVisibleTime;

    [Header("Animations & Effects")]
    public Animator enemyAnimator;
    [SerializeField] private ParticleSystem projectionParticles;
    [SerializeField] private ParticleSystem confusedParticles;
    [SerializeField] private ParticleSystem nervousParticles;
    private float confusedTime;

    [Header("Bounce")]
    [SerializeField] private AnimationCurve bounceCurve;
    Coroutine bounceWhileCrushedCoroutine;
    private float bounceTime = .7f;
    private Transform enemyBody;
    [SerializeField] private BouncyObject bouncyObject;

    [Header("Grabbred & Thrown")]
    [SerializeField] private bool throwing;
    private bool grabbed;
    private Vector3 throwDirection;
    [SerializeField] private float projectionTime = 1.5f;
    [SerializeField] private float projectionDistance = 5f;
    [SerializeField] private float fallSpeed = 3f;
    [SerializeField] private AnimationCurve projectionCurve;
    [SerializeField] private AnimationCurve projectionRotCurve;
    [SerializeField] private AnimationCurve fallCurve;
    [SerializeField] private float entityRadius;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask obstacleMaskWithoutPlayer;

    // Start is called before the first frame update
    void Start()
    {
        enemyAnimator.SetBool("NormalState", true);
        initialPosition = transform.position;
        playerTr = HeroMovements.PlayerBody;
        canFreelyMove = true;

        if (searchingPointsParent)
        {
            foreach (Transform tr in searchingPointsParent)
            {
                searchingPoints.Add(tr.position);
            }
        }
    }

    /*
     * ------ Quand le joueur n'est pas détecté ------
     * 
     *  Créer des booléens afin de déterminer certaines "personnalités" des ennemis ?
     *  Par exemple, ils ne vont pas tous avoir le même comportement (qui serait embêtant pour certaines 
     *  intégrations). Certains ennemis vont donc rester fixe jusqu'à détecter le joueur. D'autres pourraient
     *  réaliser des rondes + tourner sur eux-mêmes afin de chercher d'éventuels intrus.
     * 
     * ------ Détection du joueur & mode attaque ------
     * 
     *  On pourrait imaginer différents modes de détection (selon envie de gameplay) : le basique serait un 
     *  système de vue pour l'entité, si le joueur passe devant lui (+ distance proche) : l'ennemi passe
     *  en mode attaque. Un autre système pourrait être une entrée dans une zone spécifique qui déclenche
     *  l'agressivité des entités. Des groupes d'ennemis pourraient aussi être formés : quand un ennemi détecte,
     *  il en informe les autres qui vont aussi attaquer le joueur.
     *  
     * ------ Système d'attaque et de poursuite ------
     *
     *  Quand le joueur est détecté, une poursuite s'engage. L'entité cherche à atteindre une position proche de
     *  celle du joueur afin de pouvoir l'attaquer : quand l'entité atteint une distance suffisante du joueur, elle
     *  va lancer une attaque (dépend de l'entité actuelle). Certains ennemis peuvent aussi attaquer à distance (le
     *  système de retour en mode passif en est affecté).
     *  
     *  Les attaques pourraient être assignées à un script présent sur l'entité actuelle. On pourrait donc avoir
     *  ce script global pour les ennemis terrestres + les scripts d'attaque dépendant des entités ?
     *  Mieux vaut peut-être dupliquer ce script pour mieux correspondre à chaque ennemi.
     *  
     * ------ Retour en mode passif et défaite ------
     *  
     *  L'entité abandonne sa poursuite si le joueur est impossible à suivre (n'est plus sur la plateforme, 
     *  distance trop éloignée, cinématique), si le joueur n'est plus visible pendant un certain temps ? 
     *  
     */

    // Update is called once per frame
    void Update()
    {
        if (confusedTime > 0f || !canFreelyMove || grabbed || !agent.enabled) goto SkipIA;

        if (!attackMode)
        {
            changeTime -= Time.deltaTime;
            if (changeTime < 0)
            {
                /*currentState = (EnemyAIState)Random.Range(0, thisEnemyStates.Count);
                EnemyStateChange();
                changeTime = Random.Range(minChangeTime, maxChangeTime);*/

                int nextStateValue = Random.Range(0, 100);
                float currentCheckValue = 0;
                foreach (EnemyStateCharacteristics enemyState in thisEnemyStates)
                {
                    if (currentCheckValue+enemyState.probability >= nextStateValue)
                    {
                        currentState = enemyState.aIState;
                        //Debug.Log("Next value is : " + nextStateValue + ", State : " + enemyState.aIState.ToString());
                        changeTime = Random.Range(minChangeTime, maxChangeTime);
                        EnemyStateChange();
                        break;
                    }
                    else
                    {
                        currentCheckValue += enemyState.probability;
                    }
                }
            }

            switch (currentState)
            {
                case EnemyAIState.Idle:
                    agent.destination = transform.position;

                    //Le booléen est true uniquement quand on transitionne à partir d'un autre état non fini.
                    if (destinationReached)
                    {
                        if(distanceCheckCoroutine != null) StopCoroutine(distanceCheckCoroutine);
                        enemyAnimator.SetBool("Walking", false);
                        enemyAnimator.SetBool("NormalState", true);
                        destinationReached = false;
                    }
                    break;

                case EnemyAIState.Wandering:
                    if (destinationReached)
                    {
                        StopCoroutine(distanceCheckCoroutine);
                        currentState = EnemyAIState.Idle;
                    }
                    break;

                case EnemyAIState.Searching:
                    if (destinationReached)
                    {
                        patrolIndex++;
                        if (patrolIndex >= searchingPoints.Count) patrolIndex = 0;
                        agent.destination = searchingPoints[patrolIndex];
                        destinationReached = false;
                    }
                    break;

                case EnemyAIState.StepBackToInitialPos:
                    if (destinationReached)
                    {
                        StopCoroutine(distanceCheckCoroutine);
                        currentState = EnemyAIState.Idle;
                    }
                    break;
            }
        }
        else //Si le joueur est assez proche pour être attaqué
        {

        }

    SkipIA:

        if (confusedTime > 0f && !throwing && !falling)
        {
            confusedTime -= Time.deltaTime;
            if (confusedTime <= 0f)
            {
                canFreelyMove = true;
                agent.enabled = true;

                enemyAnimator.SetBool("Confusing", false);
                enemyAnimator.SetBool("NormalState", true);
                if (confusedParticles.isPlaying)
                {
                    confusedParticles.Stop();
                    confusedParticles.Clear();
                }

                if(grabbableBehavior) grabbableBehavior.enabled = true;
                if(grabbableBehavior) grabbableBehavior.grabVisibility.enabled = true;
            }
        }

        //Désactiver l'outline quelques secondes après avoir subit des dégâts.
        if (damageOutlineVisibleTime > 0f)
        {
            damageOutlineVisibleTime -= Time.deltaTime;
            if(damageOutlineVisibleTime <= 0f)
            {
                enemyOutline.enabled = false;
            }
        }

        if (fallTime >= TimeBeforeDyingByFall) //Ajouter un booléen ou paramètre pour que ça ne le fasse qu'une fois.
        {
            Death();
        }
    }

    private void EnemyStateChange()
    {
        ResetAllStates();

        switch (currentState)
        {
            case EnemyAIState.Idle:
                enemyAnimator.SetBool("Walking", false);
                enemyAnimator.SetBool("NormalState", true);
                break;

            case EnemyAIState.Wandering:
                agent.destination = transform.position +
                    Quaternion.AngleAxis(
                        Random.Range(0, 360),
                        Vector3.up
                    ) * Vector3.forward * Random.Range(minWanderingDistance, maxWanderingDistance);
                enemyAnimator.SetBool("Walking", true);
                enemyAnimator.SetBool("NormalState", false);
                distanceCheckCoroutine = StartCoroutine(DistanceCheck(1f));
                break;

            case EnemyAIState.Searching:
                if (searchingPoints.Count < 1)
                {
                    Debug.LogError("There isn't any searching points.", gameObject);
                    break;
                }

                changeTime += 10f;

                /*int searchDestination = Random.Range(0, searchingPoints.Count);
                agent.destination = searchingPoints[searchDestination];*/

                agent.destination = searchingPoints[patrolIndex];
                distanceCheckCoroutine = StartCoroutine(DistanceCheck(0.5f));
                enemyAnimator.SetBool("Walking", true);
                enemyAnimator.SetBool("NormalState", false);
                break;

            case EnemyAIState.StepBackToInitialPos:
                agent.destination = initialPosition;
                enemyAnimator.SetBool("Walking", true);
                enemyAnimator.SetBool("NormalState", false);
                distanceCheckCoroutine = StartCoroutine(DistanceCheck(1f));
                break;
        }
    }

    /// <summary>
    /// Ici sont reset toutes les valeurs spécifiques aux états précédents.
    /// </summary>
    private void ResetAllStates()
    {
        destinationReached = false;
        if (distanceCheckCoroutine != null) StopCoroutine(distanceCheckCoroutine);
    }

    public void BounceBack()
    {
        if (bounceWhileCrushedCoroutine != null) StopCoroutine(bounceWhileCrushedCoroutine);
        if (OnEnemyImpact != null) OnEnemyImpact(transform.position + Vector3.up * heightOffset, false);
        DamagesReceived(transform.position + Vector3.up * heightOffset);
        bounceWhileCrushedCoroutine = StartCoroutine(BounceBounce());
    }

    IEnumerator BounceBounce()
    {
        if (!enemyBody) enemyBody = enemyAnimator.transform;

        float startTime = Time.time;
        float fractionOfJourney;
        Vector3 baseScale = Vector3.one;

        do
        {
            fractionOfJourney = (Time.time - startTime) / bounceTime;
            enemyBody.localScale = baseScale 
                + Vector3.up * bounceCurve.Evaluate(fractionOfJourney)
                + Vector3.right * bounceCurve.Evaluate(fractionOfJourney) * 0.2f
                + Vector3.forward * bounceCurve.Evaluate(fractionOfJourney) * 0.2f;

            yield return null;
        } while (fractionOfJourney <1f);
    }

    public void Death()
    {

    }

    IEnumerator DistanceCheck(float checkFrequency)
    {
        do
        {
            float distance = (agent.destination - transform.position).sqrMagnitude;
            if(distance < 1f) //should be : "dist < distRequired*distRequired", but it's one.
            {
                destinationReached = true;
            }
            yield return new WaitForSeconds(checkFrequency);
        } while (canFreelyMove && !attackMode);
        
    }

    private void OnDrawGizmos()
    {
        switch (currentState)
        {
            case EnemyAIState.Idle:
                Gizmos.color = Color.yellow;
                break;

            case EnemyAIState.Wandering:
                Gizmos.color = Color.green;
                break;

            case EnemyAIState.Searching:
                Gizmos.color = Color.blue;
                break;

            case EnemyAIState.StepBackToInitialPos:
                Gizmos.color = Color.black;
                break;
        }

        if (attackMode) Gizmos.color = Color.red;

        Gizmos.DrawCube(transform.position + Vector3.up, Vector3.one / 2f);
    }

    #region Projection & Fall

    bool agentWasActive = true;
    public void Grabbed(bool grabState)
    {
        grabbed = grabState;
        if (grabbed)
        {
            agentWasActive = agent.enabled;
            agent.enabled = false;
        }
        else if(!throwing && !falling)
        {
            agent.enabled = agentWasActive;
        }
    }

    private List<Transform> directionnalArrows = new List<Transform>();
    private float throwByDashTime = 1.1f;
    private float projectionByDashSpeed = 16f;
    public AnimationCurve decelerationCurve /*= AnimationCurve.Linear(0f, 1f, 1f, 0f)*/;
    Coroutine projectionCoroutine;

    public bool ThrownByPlayer(GrabbableTarget myGrabbableBehavior, ThrowAxis axisThrowDirection)
    {
        //Réaliser des projections (raycast) pour savoir si l'entité peut être lancée dans une telle direction.
        //Si oui, on retourne vrai, faux sinon.

        grabbableBehavior = myGrabbableBehavior;

        if (directionnalArrows.Count < 1)
        {
            foreach (Transform tr in myGrabbableBehavior.directionnalArrowParent)
            {
                directionnalArrows.Add(tr);
            }
        }

        switch (axisThrowDirection)
        {
            case ThrowAxis.Right:
                throwDirection = directionnalArrows[0].GetChild(0).position - transform.position;
                GrabAndProjectSkill.LastThrowDir = throwDirection;
                break;

            case ThrowAxis.Left:
                throwDirection = directionnalArrows[1].GetChild(0).position - transform.position;
                GrabAndProjectSkill.LastThrowDir = throwDirection;
                break;

            case ThrowAxis.Backward:
                if (directionnalArrows.Count > 2)
                {
                    throwDirection = directionnalArrows[2].GetChild(0).position - transform.position;
                    GrabAndProjectSkill.LastThrowDir = throwDirection;
                }
                else return false; //Si aucune flèche n'oriente dans cette direction, on ne projette pas l'entité.
                break;

            case ThrowAxis.Bottom:
                if (directionnalArrows.Count > 3)
                {
                    throwDirection = directionnalArrows[3].GetChild(0).position - transform.position;
                    GrabAndProjectSkill.LastThrowDir = throwDirection;
                }
                else return false; //Si aucune flèche n'oriente dans cette direction, on ne projette pas l'entité.
                break;
        }

        //Faire en sorte que l'ennemi regarde le joueur lorsqu'il est projeté.
        Vector3 cameraPosition = Camera.main.transform.position;
        cameraPosition.y = transform.position.y;
        transform.LookAt(cameraPosition);

        Debug.DrawLine(transform.position, transform.position + throwDirection, Color.green, 5f);

        //StartCoroutine(Throw(axisThrowDirection));

        string animNameForProjection = "";
        switch (axisThrowDirection)
        {
            case ThrowAxis.Right:
                animNameForProjection = "ProjectedH";
                break;

            case ThrowAxis.Left:
                animNameForProjection = "ProjectedH";
                break;

            case ThrowAxis.Backward:
                animNameForProjection = "ProjectedV";
                break;

            case ThrowAxis.Bottom:
                animNameForProjection = "ProjectedV";
                break;
        }
        projectionCoroutine = StartCoroutine(ThrowAndFall(
            throwDirection.normalized, 
            true, 
            animNameForProjection, 
            projectionTime-.2f, 
            15.3f
            ));

        return true;
    }

    public void HitByPlayerDash(Vector3 playerDashDirection)
    {
        if (!falling && !throwing)
        {
            //StartCoroutine(ThrownByDash(playerDashDirection));
            projectionCoroutine = StartCoroutine(ThrowAndFall(playerDashDirection, false, "ProjectedV", throwByDashTime, projectionByDashSpeed));
            if (OnEnemyImpact != null) OnEnemyImpact(transform.position, false);
            DamagesReceived(transform.position);
            if (playerTr.parent.TryGetComponent(out HeroMovements heroMovements))
            {
                heroMovements.StopDash();
                Debug.Log("STOP DASH");
            }
        }
    }

    IEnumerator ThrowAndFall(
        Vector3 direction, bool withThrowAxis, string throwAnimationName, float throwTime, float speed
        )
    {
        throwing = true;
        falling = false;

        /* Animations & movements settings */
        if (grabbableBehavior) grabbableBehavior.enabled = false;
        if(grabbableBehavior) grabbableBehavior.grabVisibility.enabled = false;
        canFreelyMove = false;
        agent.enabled = false;
        enemyAnimator.SetBool("NormalState", false);
        enemyAnimator.SetBool("Walking", false);
        enemyAnimator.SetBool("Confusing", false);
        enemyAnimator.SetBool(throwAnimationName, true); //Faire attention au nom renseigné. (Vertical ou Horizontal)
        //Orienter les particules pour qu'elles laissent une traînée en direction inverse de notre destination.
        if (!projectionParticles.isPlaying) projectionParticles.Play();
        projectionParticles.transform.LookAt(transform.position + direction);
        projectionParticles.transform.localRotation *= Quaternion.Euler(0, 180f, 0);

        bouncyObject.enabled = false;
        gameObject.layer = 2;

        /* Paramètres de la projection. */
        if (!withThrowAxis)
        {
            direction += Vector3.up; //Ajout d'un mouvement vers le haut pour réaliser une courbe montante au début.
        }
        float startTime = Time.time;
        float fractionOfJourney;
        Vector3 nextPositionDir;

        /*Debug : */
        Vector3 startPosition = transform.position;
        
        do
        {
            if (!falling) //Lorsqu'on vient d'être projeté.
            {
                fractionOfJourney = (Time.time - startTime) / throwTime;
                //Si fractionOfJourney atteint 1, cela veut dire que la vitesse de projection est minimale.
                if (fractionOfJourney > 1f)
                {
                    fractionOfJourney = 1f;
                    falling = true;
                    throwing = false;
                    /*Debug : */
                    Vector3 movedDist = transform.position - startPosition;
                    Debug.Log("Distance parcourue : " + movedDist.magnitude);
                }

                nextPositionDir = 
                    (direction * decelerationCurve.Evaluate(fractionOfJourney)
                    - 
                    (withThrowAxis ? 
                     Vector3.zero :
                    (Vector3.up * (1 - decelerationCurve.Evaluate(fractionOfJourney))) ))
                    * Time.deltaTime * speed;

                Ray thrownRay = new Ray(transform.position, nextPositionDir);
                if (!Physics.SphereCast(
                    thrownRay, 0.3f, out RaycastHit throwHit, nextPositionDir.magnitude, obstacleMaskWithoutPlayer))
                {
                    transform.position += nextPositionDir;
                }
                else
                {
                    if (OnEnemyImpact != null) OnEnemyImpact(throwHit.point, true);

                    //Faire un autre raycast pour savoir si l'entité peut tomber en ligne droite.
                    //Si oui, déclencher le falling prématurément.
                    //Si non, quitter la boucle.

                    //Si l'entité ne touche pas le sol et peut encore tomber.
                    if (!Physics.CheckSphere(transform.position - transform.up, .3f, obstacleMaskWithoutPlayer))
                    {
                        falling = true;
                    }
                    else //l'entité est collé à un obstacle sous elle. On quitte la boucle.
                    {
                        falling = false;
                    }

                    throwing = false;
                    /*Debug : */
                    Vector3 movedDist = transform.position - startPosition;
                    Debug.Log("Distance parcourue : " + movedDist.magnitude);
                }
            }
            else //une fois que l'entité a touché un mur, le sol ou si la direction est devenue verticale
            {
                fallTime += Time.deltaTime;

                nextPositionDir = Vector3.down * Time.deltaTime * speed;

                Ray fallingRay = new Ray(transform.position, nextPositionDir);
                if (!Physics.SphereCast(fallingRay, 0.3f, out RaycastHit fallingHit, nextPositionDir.magnitude, obstacleMask))
                {
                    transform.position += nextPositionDir;
                }
                else
                {
                    falling = false;
                    fallTime = 0f;
                    if (OnEnemyImpact != null) OnEnemyImpact(fallingHit.point, true);
               }
            }
            yield return null;
        } while (throwing || falling);

        /* Une fois que l'entité est retombée au sol (si morte de sa chute : faire autre chose.) */

        confusedTime = 3f;
        enemyAnimator.SetBool(throwAnimationName, false);
        enemyAnimator.SetBool("Confusing", true);
        if (!confusedParticles.isPlaying) confusedParticles.Play();

        //Si on est assez proche du sol, on y colle l'entité directement
        Ray ray = new Ray(transform.position, -transform.up);
        if (Physics.Raycast(ray, out RaycastHit hit, 1f, obstacleMask))
        {
            transform.position = hit.point + Vector3.up * heightOffset;
        }

        bouncyObject.enabled = true;
        bouncyObject.canBounce = true;
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }

    /* Ancienne méthode de projection.
    IEnumerator Throw(ThrowAxis axisThrowDirection)
    {
        throwing = true;
        obstacleMeet = false;
        canFreelyMove = false;
        agent.enabled = false;
        grabbableBehavior.enabled = false;
        grabbableBehavior.grabVisibility.enabled = false;

        //Déclenchement des animations et effets de projection.
        enemyAnimator.SetBool("NormalState", false);
        enemyAnimator.SetBool("Walking", false);
        enemyAnimator.SetBool("Confusing", false);
        switch (axisThrowDirection)
        {
            case ThrowAxis.Right:
                enemyAnimator.SetBool("ProjectedH", true);
                break;

            case ThrowAxis.Left:
                enemyAnimator.SetBool("ProjectedH", true);
                break;

            case ThrowAxis.Backward:
                enemyAnimator.SetBool("ProjectedV", true);
                break;

            case ThrowAxis.Bottom:
                enemyAnimator.SetBool("ProjectedV", true);
                break;
        }

        //Orienter les particules pour qu'elles laissent une traînée en direction inverse de notre destination.
        if (!projectionParticles.isPlaying) projectionParticles.Play();
        projectionParticles.transform.LookAt(transform.position + throwDirection);
        projectionParticles.transform.localRotation *= Quaternion.Euler(0, 180f, 0);

        //Paramètres de la projection.
        float startTime = Time.time;
        float fractionOfJourney;
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + throwDirection * projectionDistance;
        Vector3 obstaclePosition = Vector3.zero;
        enemyAnimator.SetFloat("RotationSpeed", 1f);
        gameObject.layer = 2;

        // Ci-dessous : déplacement graduel de l'entité vers sa destination de projection.
        // Réalisation de Raycast à chaque déplacement afin de stopper prématurément si l'entité rencontre un mur.
        do
        {
            fractionOfJourney = (Time.time - startTime) / projectionTime;

            Vector3 nextPosition = Vector3.Lerp(
                startPosition,
                endPosition,
                projectionCurve.Evaluate(fractionOfJourney)
                );
            enemyAnimator.SetFloat("RotationSpeed", projectionRotCurve.Evaluate(fractionOfJourney));

            if (!Physics.CheckSphere(nextPosition, entityRadius, obstacleMask))
            {
                transform.position = nextPosition;
            }
            else
            {
                Ray ray = new Ray(transform.position, throwDirection);
                float rayDist = (transform.position - nextPosition).magnitude;
                if (Physics.SphereCast(ray, entityRadius, out RaycastHit hit, rayDist, obstacleMask))
                {
                    obstaclePosition = hit.point;
                }

                obstacleMeet = true;
            }

            yield return null;
        } while (!obstacleMeet && fractionOfJourney < 1f);

        if (obstacleMeet && obstaclePosition != Vector3.zero) //(AVEC impact)
        {
            //Particules d'impact, un effet de tourni + une chute de l'entité
            if (OnEnemyImpact != null) OnEnemyImpact(obstaclePosition, true);
        }

        //Si l'entité ne touche pas le sol 
        if(!Physics.CheckSphere(transform.position-transform.up, entityRadius, obstacleMask))
        {
            StartCoroutine(FallWithProjectionForce(axisThrowDirection));
        }
        else //Si elle touche pratiquement le sol
        {
            //Effets et animations de fin de projection 
            switch (axisThrowDirection)
            {
                case ThrowAxis.Right:
                    enemyAnimator.SetBool("ProjectedH", false);
                    break;

                case ThrowAxis.Left:
                    enemyAnimator.SetBool("ProjectedH", false);
                    break;

                case ThrowAxis.Backward:
                    enemyAnimator.SetBool("ProjectedV", false);
                    break;

                case ThrowAxis.Bottom:
                    enemyAnimator.SetBool("ProjectedV", false);
                    break;
            }

            confusedTime = 3f;
            enemyAnimator.SetBool("Confusing", true);
            if (!confusedParticles.isPlaying) confusedParticles.Play();

            //Si on est assez proche du sol, on y colle l'entité directement
            Ray ray = new Ray(transform.position, -transform.up);
            if(Physics.Raycast(ray, out RaycastHit hit, 1f, obstacleMask))
            {
                transform.position = hit.point + Vector3.up * heightOffset;
            }

        }
        gameObject.layer = LayerMask.NameToLayer("Enemy");
        throwing = false;
    }

    IEnumerator ThrownByDash(Vector3 playerDashDirection)
    {
        throwing = true;
        float startTime = Time.time;
        Vector3 throwDir = playerDashDirection + Vector3.up;
        float fractionOfJourney;
        Vector3 obstaclePosition = Vector3.zero;
        obstacleMeet = false;
        canFreelyMove = false;
        gameObject.layer = 2;
        enemyAnimator.SetBool("ProjectedV", true);
        enemyAnimator.SetBool("NormalState", false);
        enemyAnimator.SetBool("Walking", false);
        enemyAnimator.SetBool("Confusing", false);

        //Orienter les particules pour qu'elles laissent une traînée en direction inverse de notre destination.
        if (!projectionParticles.isPlaying) projectionParticles.Play();
        projectionParticles.transform.LookAt(transform.position + throwDirection);
        projectionParticles.transform.localRotation *= Quaternion.Euler(0, 180f, 0);

        agent.enabled = false;

        do
        {
            fractionOfJourney = (Time.time - startTime) / throwByDashTime;
            if (fractionOfJourney > 1f) fractionOfJourney = 1f;
            Vector3 nextPositionDir =
                (throwDir * decelerationCurve.Evaluate(fractionOfJourney) 
                - Vector3.up * (1- decelerationCurve.Evaluate(fractionOfJourney)))
                * Time.deltaTime * projectionByDashSpeed;

            Ray thrownByDashray = new Ray(transform.position, nextPositionDir);
            if (!Physics.SphereCast(thrownByDashray, 0.3f, out RaycastHit hit, nextPositionDir.magnitude, obstacleMaskWithoutPlayer))
            {
                transform.position += nextPositionDir;
                fallTime += Time.deltaTime;
            }
            else
            {
                //Si on entre en collision avec un objet
                obstacleMeet = true;
                obstaclePosition = hit.point;
                fallTime = 0f;
            }
            yield return null;
        } while (!obstacleMeet);

        //Effets et animations de fin de projection 
        if (obstaclePosition != Vector3.zero)
        {
            if (OnEnemyImpact != null) OnEnemyImpact(obstaclePosition, false);
            fallTime = 0f;
        }
        enemyAnimator.SetBool("ProjectedV", false);
        confusedTime = 3f;
        enemyAnimator.SetBool("Confusing", true);
        if (!confusedParticles.isPlaying) confusedParticles.Play();

        gameObject.layer = LayerMask.NameToLayer("Enemy");
        throwing = false;
    }
    
    IEnumerator FallWithProjectionForce(ThrowAxis axisThrowDirection)
    {
        falling = true;

        //Paramètres de la projection.
        float startTime = Time.time;
        float fractionOfJourney;
        Vector3 startPosition = transform.position;
        //Vector3 endPosition = startPosition + throwDirection * projectionDistance;
        Vector3 obstaclePosition = Vector3.zero;
        float undoProjectionCurveTime = 2f;
        enemyAnimator.SetFloat("RotationSpeed", .5f);
        gameObject.layer = 2;
        obstacleMeet = false;

        do
        {
            fractionOfJourney = (Time.time - startTime) / undoProjectionCurveTime;
            if (fractionOfJourney >= 1f) fractionOfJourney = 1f;
            float curvedSpeed = fallSpeed * projectionCurve.Evaluate(fractionOfJourney);

            Vector3 nextPositionDir = 
                (-transform.up  
                + (1-fractionOfJourney) * throwDirection) 
                * Time.deltaTime * curvedSpeed;

            Ray fallingRay = new Ray(transform.position, nextPositionDir); 
            if(!Physics.SphereCast(fallingRay, 0.3f, out RaycastHit hit, nextPositionDir.magnitude, obstacleMask))
            {
                transform.position += nextPositionDir;
            }
            else
            {
                //Si on entre en collision avec un objet
                obstacleMeet = true;
                obstaclePosition = hit.point;
                falling = false;
            }

            fallTime += Time.deltaTime;

            yield return null;
        } while (falling);

        //Effets et animations de fin de projection 
        if (obstacleMeet && obstaclePosition != Vector3.zero)
        {
            if (OnEnemyImpact != null) OnEnemyImpact(obstaclePosition, false);
            fallTime = 0f;
        }
        
        switch (axisThrowDirection)
        {
            case ThrowAxis.Right:
                enemyAnimator.SetBool("ProjectedH", false);
                break;

            case ThrowAxis.Left:
                enemyAnimator.SetBool("ProjectedH", false);
                break;

            case ThrowAxis.Backward:
                enemyAnimator.SetBool("ProjectedV", false);
                break;

            case ThrowAxis.Bottom:
                enemyAnimator.SetBool("ProjectedV", false);
                break;
        }
        confusedTime = 3f;
        enemyAnimator.SetBool("Confusing", true);
        if (!confusedParticles.isPlaying) confusedParticles.Play();

        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }
    */
    #endregion

    #region Life, Damage & Attacks
    public void DamagesReceived(Vector3 damagesPosition)
    {
        if (OnDamagesReceived != null) OnDamagesReceived(damagesPosition);
        enemyOutline.enabled = true;
        damageOutlineVisibleTime = 0.5f;
    }
    #endregion
}
