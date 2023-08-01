using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OutlineEffect;

public class FlyingEnemy : MonoBehaviour
{
    private GrabbableTarget grabbableBehavior;

    //Si l'entité peut se déplacer selon son propre désir ou subit un déplacement forcé.
    [HideInInspector] public bool canFreelyMove;
    
    public static EnemyImpact OnEnemyImpact;
    public static Pouf OnPouf;
    public static EnemyDamageReceived OnDamagesReceived;

    [Header("Movements")]
    public bool falling; //Chute de l'entité : Incapacité durant cette période.
    private float verticalVelocity;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float gravityScale;
    [SerializeField] private float heightOffset;
    private RaycastHit groundHit;
    private float fallTime;
    private const float TimeBeforeDyingByFall = 6f;

    [Header("Passive behavior")]
    [SerializeField, Tooltip("Select the behaviors for this enemy. Remember to make the probability sum equal to 100%")]
    private List<EnemyStateCharacteristics> thisEnemyStates;
    [SerializeField] private EnemyAIState currentState;
    [SerializeField] private float minChangeTime, maxChangeTime;
    public float changeTime;
    private Vector3 initialPosition;
    private Vector3 currentDestination;
    [SerializeField] private float flySpeed;
    [SerializeField] private float minWanderingDistance;
    [SerializeField] private float maxWanderingDistance;
    [SerializeField] private Transform searchingPointsParent;
    private List<Vector3> searchingPoints = new List<Vector3>();
    int patrolIndex;
    private float heightBeforeFall;
    Coroutine distanceCheckCoroutine;
    Coroutine movementCoroutine;
    bool destinationReached;

    private bool downForceActivated;
    private const float downForceDuration = .7f;
    private float downForceStartTime;
    [SerializeField] private AnimationCurve jumpOnMeCurve;

    [Header("Agressive behavior")]
    public bool attackMode; 
    private Transform playerTr;
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
    [SerializeField] private bool throwingState;
    private bool grabbed;
    private Vector3 throwDirection;
    [SerializeField] private float projectionTime = 1.5f;
    [SerializeField] private float projectionDistance = 5f;
    [SerializeField] private AnimationCurve projectionCurve;
    [SerializeField] private AnimationCurve projectionRotCurve;
    [SerializeField] private float entityRadius;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask obstacleMaskWithoutPlayer;

    // Start is called before the first frame update
    void Start()
    {
        enemyAnimator.SetBool("NormalState", true);
        playerTr = HeroMovements.PlayerBody;
        heightBeforeFall = transform.position.y;
        canFreelyMove = true;
        enemyBody = enemyAnimator.transform;

        initialPosition = transform.position;
        if (searchingPointsParent)
        {
            foreach (Transform tr in searchingPointsParent)
            {
                searchingPoints.Add(tr.position);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (confusedTime > 0f || !canFreelyMove || grabbed) goto SkipIA;

        if (!attackMode)
        {
            changeTime -= Time.deltaTime;
            if (changeTime < 0)
            {
                int nextStateValue = Random.Range(0, 100);
                float currentCheckValue = 0;
                foreach (EnemyStateCharacteristics enemyState in thisEnemyStates)
                {
                    if (currentCheckValue + enemyState.probability >= nextStateValue)
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
                    currentDestination = transform.position;

                    //Le booléen est true uniquement quand on transitionne à partir d'un autre état non fini.
                    if (destinationReached)
                    {
                        if (distanceCheckCoroutine != null) StopCoroutine(distanceCheckCoroutine);
                        enemyAnimator.SetBool("Flying", false);
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
                        currentDestination = searchingPoints[patrolIndex];
                        destinationReached = false;
                        transform.LookAt(currentDestination);
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

        if (falling)
        {
            Ray toGroundRay = new Ray(transform.position, -transform.up);
            
            //Si on rentre en collision avec un objet en-dessous de nous.
            if(Physics.SphereCast(toGroundRay, 0.3f, out groundHit, heightOffset - 0.2f, obstacleMask))
            {
                verticalVelocity = 0f;
                falling = false;
                fallTime = 0f;

                //Lancer une animation d'impact au sol puis redonner après quelques instants la possibilité de bouger
                // à l'entité.

                if (OnEnemyImpact != null) OnEnemyImpact(groundHit.point, true);
                confusedTime = 5f;
            }
            else //Si rien n'est touché, on continue de tomber
            {
                verticalVelocity += gravity * gravityScale * Time.deltaTime;
                fallTime += Time.deltaTime;
                /*
                 * Compteur de temps de chute : au bout d'un certain temps, l'entité meurt.
                 */
            }

            transform.Translate(verticalVelocity * Vector3.up * Time.deltaTime);
        }

        if(downForceActivated)
        {
            float downForceProgression = (Time.time - downForceStartTime) / downForceDuration;
            transform.position = new Vector3(
                transform.position.x,
                heightBeforeFall + jumpOnMeCurve.Evaluate(downForceProgression),
                transform.position.z
            );
            if (downForceProgression >= 1f) downForceActivated = false;
        }

        if(confusedTime > 0f && !throwingState)
        {
            confusedTime -= Time.deltaTime;
            if(confusedTime <= 0f)
            {
                canFreelyMove = true;
                enemyAnimator.SetBool("Confusing", false);
                enemyAnimator.SetBool("NormalState", true);

                if (OnPouf != null) OnPouf(transform.position);

                //Possibilité de rajouter un check plus propre pour savoir s'il peut retourner à sa position initiale,
                //  en utilisant notamment des raycasts. Au besoin.
                if (Vector3.Distance(transform.position, initialPosition) > 16f)
                {
                    transform.position = initialPosition;
                    if (OnPouf != null) OnPouf(transform.position);
                }
                else
                {
                    if(Physics.Raycast(transform.position, -transform.up, 1f , obstacleMask))
                    {
                        enemyAnimator.SetBool("Flying", true);
                        enemyAnimator.SetBool("NormalState", false);

                        if(Physics.Raycast(transform.position, transform.up, out RaycastHit heightHit, 3f, obstacleMask))
                        {
                            transform.position = transform.position + Vector3.up * (heightHit.distance - 0.8f);
                        }
                        else
                        {
                            transform.position = transform.position + Vector3.up * 3f;
                        }
                        if (OnPouf != null) OnPouf(transform.position);
                    }
                }
                
                if (confusedParticles.isPlaying) 
                {
                    confusedParticles.Stop();
                    confusedParticles.Clear();
                }
                if (grabbableBehavior)
                {
                    grabbableBehavior.enabled = true;
                    grabbableBehavior.grabVisibility.enabled = true;
                }
                
            }
        }

        //Désactiver l'outline quelques secondes après avoir subit des dégâts.
        if (damageOutlineVisibleTime > 0f)
        {
            damageOutlineVisibleTime -= Time.deltaTime;
            if (damageOutlineVisibleTime <= 0f)
            {
                enemyOutline.enabled = false;
            }
        }

        if (fallTime >= TimeBeforeDyingByFall)
        {
            Death();
        }
    }

    #region Passive Behavior

    private void EnemyStateChange()
    {
        ResetAllStates();

        switch (currentState)
        {
            case EnemyAIState.Idle:
                enemyAnimator.SetBool("Flying", false);
                enemyAnimator.SetBool("NormalState", true);
                break;

            case EnemyAIState.Wandering:
                currentDestination = initialPosition +
                    Quaternion.AngleAxis(
                        Random.Range(0, 360),
                        Vector3.up
                    ) * Vector3.forward * Random.Range(minWanderingDistance, maxWanderingDistance);
                enemyAnimator.SetBool("Flying", true);
                enemyAnimator.SetBool("NormalState", false);

                movementCoroutine = StartCoroutine(MoveToPoint());
                
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

                currentDestination = searchingPoints[patrolIndex];
                movementCoroutine = StartCoroutine(MoveToPoint());
                enemyAnimator.SetBool("Flying", true);
                enemyAnimator.SetBool("NormalState", false);
                break;

            case EnemyAIState.StepBackToInitialPos:
                currentDestination = initialPosition;
                enemyAnimator.SetBool("Flying", true);
                enemyAnimator.SetBool("NormalState", false);
                movementCoroutine = StartCoroutine(MoveToPoint());
                break;
        }
    }

    /// <summary>
    /// Ici sont reset toutes les valeurs spécifiques aux états précédents.
    /// </summary>
    private void ResetAllStates()
    {
        destinationReached = false;
        StopToMoveAtWill();
        transform.rotation *= Quaternion.AngleAxis(-transform.rotation.eulerAngles.x, Vector3.right);
    }

    IEnumerator MoveToPoint()
    {
        float distCheckFreq = 1f;
        switch (currentState)
        {
            case EnemyAIState.Wandering:
                distCheckFreq = 1f;
                break;
            case EnemyAIState.Searching:
                distCheckFreq = 0.5f;
                break;
            case EnemyAIState.StepBackToInitialPos:
                distCheckFreq = 0.3f;
                break;
        }
        distanceCheckCoroutine = StartCoroutine(DistanceCheck(distCheckFreq));
        
        Vector3 look = currentDestination;
        look.y = transform.position.y;
        transform.LookAt(look);
        
        Vector3 nextPosition;

        do
        {
            if (!destinationReached)
            {
                nextPosition = (currentDestination - transform.position).normalized * Time.deltaTime * flySpeed;
                Ray toDestRay = new Ray(transform.position, nextPosition);
                if(!Physics.SphereCast(toDestRay, entityRadius, out RaycastHit hit, entityRadius+0.2f, obstacleMask))
                {
                    transform.position += nextPosition;
                }
                else
                {
                    
                }
            }

            yield return null;
        } while (canFreelyMove && currentState != EnemyAIState.Idle);

        //Reset la rotation en X à 0.
        transform.rotation *= Quaternion.AngleAxis(-transform.rotation.eulerAngles.x, Vector3.right);
    }

    /// <summary>
    /// Fonction à appeler lorsque l'entité ne peut plus bouger selon son propre désir.
    /// </summary>
    void StopToMoveAtWill()
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);
        if (distanceCheckCoroutine != null) StopCoroutine(distanceCheckCoroutine);
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

    #endregion

    public void Grabbed(bool grabState)
    {
        grabbed = grabState;
    }

    public void BounceBack()
    {
        if (bounceWhileCrushedCoroutine != null) StopCoroutine(bounceWhileCrushedCoroutine);
        if (OnEnemyImpact != null) OnEnemyImpact(transform.position + Vector3.up * heightOffset, false);
        DamagesReceived(transform.position + Vector3.up * heightOffset);
        bounceWhileCrushedCoroutine = StartCoroutine(BounceBounce());

        downForceActivated = true;
        downForceStartTime = Time.time;
        heightBeforeFall = transform.position.y;
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
        } while (fractionOfJourney < 1f);
    }

    public void Death()
    {

    }

    #region Projection by player spell or dash

    private List<Transform> directionnalArrows = new List<Transform>();
    private bool obstacleMeet;
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

        /*string animNameForProjection = "";
        switch (axisThrowDirection)
        {
            case ThrowAxis.Right:
                animNameForProjection = "projectedH";
                break;

            case ThrowAxis.Left:
                animNameForProjection = "projectedH";
                break;

            case ThrowAxis.Backward:
                animNameForProjection = "projectedV";
                break;

            case ThrowAxis.Bottom:
                animNameForProjection = "projectedV";
                break;
        }


        projectionCoroutine = StartCoroutine(ThrowToDirection(
            throwDirection.normalized,
            true,
            animNameForProjection,
            projectionTime - .2f,
            15.3f
            )); */

        projectionCoroutine = StartCoroutine(ThrowByPower(axisThrowDirection));
        StopToMoveAtWill();

        return true;
    }

    IEnumerator ThrowByPower(ThrowAxis axisThrowDirection)
    {
        throwingState = true;
        obstacleMeet = false;
        canFreelyMove = false;
        grabbableBehavior.enabled = false;
        grabbableBehavior.grabVisibility.enabled = false;

        heightBeforeFall = transform.position.y;

        //Déclenchement des animations et effets de projection.
        enemyAnimator.SetBool("NormalState", false);
        enemyAnimator.SetBool("Flying", false);
        switch (axisThrowDirection)
        {
            case ThrowAxis.Right:
                enemyAnimator.SetBool("projectedH", true);
                break;

            case ThrowAxis.Left:
                enemyAnimator.SetBool("projectedH", true);
                break;

            case ThrowAxis.Backward:
                enemyAnimator.SetBool("projectedV", true);
                break;

            case ThrowAxis.Bottom:
                enemyAnimator.SetBool("projectedV", true);
                break;
        }

        //Orienter les particules pour qu'elles laissent une traînée en direction inversion de notre destination.
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

        /* Ci-dessous : déplacement graduel de l'entité vers sa destination de projection.
         * Réalisation de Raycast à chaque déplacement afin de stopper prématurément si l'entité rencontre un mur. */
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
                if(Physics.SphereCast(ray, entityRadius, out RaycastHit hit , rayDist, obstacleMask))
                {
                    obstaclePosition = hit.point;
                }
                
                obstacleMeet = true;
            }

            yield return null;
        } while (!obstacleMeet && fractionOfJourney < 1f);

        //Effets et animations de fin de projection 
        switch (axisThrowDirection)
        {
            case ThrowAxis.Right:
                enemyAnimator.SetBool("projectedH", false);
                break;

            case ThrowAxis.Left:
                enemyAnimator.SetBool("projectedH", false);
                break;

            case ThrowAxis.Backward:
                enemyAnimator.SetBool("projectedV", false);
                break;

            case ThrowAxis.Bottom:
                enemyAnimator.SetBool("projectedV", false);
                break;
        }
        if (obstacleMeet && obstaclePosition != Vector3.zero) //(AVEC impact)
        {
            //Particules d'impact, un effet de tourni + une chute de l'entité
            if (OnEnemyImpact != null) OnEnemyImpact(obstaclePosition, true);
            falling = true;
        }
        else // (SANS impact)
        {
            //Effet de tourni pendant quelques secondes puis l'entité peut de nouveau bouger.
            confusedTime = 5f;
        }

        enemyAnimator.SetBool("Confusing", true);
        if (!confusedParticles.isPlaying) confusedParticles.Play();

        throwingState = false;
    }
    
    public void HitByPlayerDash(Vector3 playerDashDirection)
    {
        if (!throwingState)
        {
            bouncyObject.enabled = false;

            //StartCoroutine(ThrownByDash(playerDashDirection));
            projectionCoroutine = StartCoroutine(ThrowToDirection(playerDashDirection, false, "projectedV", throwByDashTime, projectionByDashSpeed));
            if (OnEnemyImpact != null) OnEnemyImpact(transform.position, false);
            DamagesReceived(transform.position);
            if (playerTr.parent.TryGetComponent(out HeroMovements heroMovements))
            {
                heroMovements.StopDash();
                Debug.Log("STOP DASH");
            }

            StopToMoveAtWill();
        }
    }

    IEnumerator ThrowToDirection(
        Vector3 direction, bool withThrowAxis, string throwAnimationName, float throwTime, float speed
        )
    {
        throwingState = true;
        falling = false;
        obstacleMeet = false;

        /* Animations & movements settings */
        if (grabbableBehavior) grabbableBehavior.enabled = false;
        if (grabbableBehavior) grabbableBehavior.grabVisibility.enabled = false;
        canFreelyMove = false;
        enemyAnimator.SetBool("NormalState", false);
        enemyAnimator.SetBool("Confusing", false);
        enemyAnimator.SetBool("Flying", false);
        enemyAnimator.SetBool(throwAnimationName, true); //Faire attention au nom renseigné. (Vertical ou Horizontal)
        //Orienter les particules pour qu'elles laissent une traînée en direction inverse de notre destination.
        if (!projectionParticles.isPlaying) projectionParticles.Play();
        projectionParticles.transform.LookAt(transform.position + direction);
        projectionParticles.transform.localRotation *= Quaternion.Euler(0, 180f, 0);

        bouncyObject.enabled = false;
        gameObject.layer = 2;
        heightBeforeFall = transform.position.y;

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
            fractionOfJourney = (Time.time - startTime) / throwTime;
            //Si fractionOfJourney atteint 1, cela veut dire que la vitesse de projection est minimale.
            if (fractionOfJourney > 1f)
            {
                fractionOfJourney = 1f;
                falling = true;
                throwingState = false;
                /*Debug : 
                Vector3 movedDist = transform.position - startPosition;
                Debug.Log("Distance parcourue : " + movedDist.magnitude);*/
            }

            nextPositionDir =
                (direction * decelerationCurve.Evaluate(fractionOfJourney)
                -
                (withThrowAxis ?
                    Vector3.zero :
                (Vector3.up * (1 - decelerationCurve.Evaluate(fractionOfJourney)))))
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
                obstacleMeet = true;

                falling = true;

                throwingState = false;
                /*Debug : 
                Vector3 movedDist = transform.position - startPosition;
                Debug.Log("Distance parcourue : " + movedDist.magnitude);*/
            }
            yield return null;
        } while (throwingState);

        /* Une fois que l'entité est retombée au sol (si morte de sa chute : faire autre chose.) */

        //Si on est assez proche du sol, on y colle l'entité directement
        Ray ray = new Ray(transform.position, -transform.up);
        if (Physics.Raycast(ray, out RaycastHit hit, 1f, obstacleMask))
        {
            transform.position = hit.point + Vector3.up * heightOffset;
            obstacleMeet = true;
        }

        enemyAnimator.SetBool(throwAnimationName, false);

        confusedTime = 5f;
        enemyAnimator.SetBool("Confusing", true);
        if (!confusedParticles.isPlaying) confusedParticles.Play();
    
        bouncyObject.enabled = true;
        bouncyObject.canBounce = true;
        gameObject.layer = LayerMask.NameToLayer("Enemy");
    }
    #endregion

    IEnumerator DistanceCheck(float checkFrequency)
    {
        do
        {
            float distance = (currentDestination - transform.position).sqrMagnitude;
            if (distance < 1f) //should be : "dist < distRequired*distRequired", but it's one.
            {
                destinationReached = true;
            }
            yield return new WaitForSeconds(checkFrequency);
        } while (canFreelyMove);

    }

    #region Life, Damage & Attacks
    public void DamagesReceived(Vector3 damagesPosition)
    {
        if (OnDamagesReceived != null) OnDamagesReceived(damagesPosition);
        enemyOutline.enabled = true;
        damageOutlineVisibleTime = 0.5f;
    }
    #endregion
}
