using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SlimeAnimState
{
    Idle,
    Fly,
    Walk,
    Fall,
    Swirl
}

public class SlimyBehavior : MonoBehaviour
{
    private GrabAndProjectSkill grabAndProjectSkill;
    private HeroMovements player; // => Physical Body, not the script wearer object
    [SerializeField] private Transform slimeBody;

    [Header("Follow")]
    [SerializeField] private Transform lowPosition; //Position à suivre au mieux en étant au sol
    [SerializeField] private Transform highPosition; //Position à suivre en étant en l'air
    [SerializeField] private Transform behindPosition; //Position à suivre en étant bloqué à droite.
    private Vector3 currentFollowPosition;
    [HideInInspector] public bool followPlayer;
    [SerializeField] private float baseFollowSpeed;
    [SerializeField] private float maxFollowSpeed;
    [SerializeField] private float followCheckFreq;
    private float distToFollowPoint;
    [SerializeField] private float minFollowDist;
    private bool isFollowing;
    private bool grounded;
    private bool falling;
    private RaycastHit groundHit;
    private const float landMinDist = 2.5f;
    private bool playerIsNearGround;
    private int positionIndex; // 0 = walk at right ; 1 = fly at right ; 2 = fly at back
    private bool[] availablePositions;
    [HideInInspector] public bool playerIsAiming;
    [SerializeField] private float entityRadius = 0.5f;
    [SerializeField] private LayerMask obstaclesMask;

    [Header("Player Aim")]
    [SerializeField] private Transform aimPositionTr;
    [SerializeField] private ParticleSystem waitForTargetParticles;
    private bool waitingForTarget;
    [SerializeField] private float aimFlySpeed = 2.0f;
    [SerializeField] private ParticleSystem instantMoveParticlesPrefab;
    Coroutine ammoSlimeCoroutine;
    bool ammoSlimeFlying;
    [SerializeField] private AnimationCurve projectionCurve;

    [Header("Animations")]
    [SerializeField] private Animator slimyAnimator;
    private int idleAnimIndex;
    private int flyAnimIndex;
    private int fallingAnimIndex;
    private int walkAnimIndex;
    private int swirlingAnimIndex;
    Quaternion lookRot;
    private float undoSwirlDelay;

    //[SerializeField] private Animator eyesAnimator;

    private Coroutine distCheckCoroutine;
    private Coroutine positionsCheckCoroutine;

    /*  -------------------- COMPORTEMENT DU SLIME --------------------
     *  
     *  ===> La majeure partie du temps : Suit le joueur à une position fixe et réagit à ses actions.
     *      Suivre avec un certain délai : le joueur est plus rapide et le slime n'est pas collé à lui.
     *      Start : Créer un transform à la position que l'on désire suivre, avec comme parent le Transform joueur.
     *      Le Slime essaie de rester un maximum au sol mais si le joueur est au-dessus du vide ou s'il est trop haut
     *      par rapport au slime, celui-ci s'envole. => Mode terrestre (stick to ground) et mode volant.
     *      Le mode volant le fait se positionner vers l'épaule du joueur ? Terrestre le pied.
     *      Si les deux positions (haut et basse) sont dans un mur, alors le slime se tient à une troisième position, 
     *      derrière le joueur en vol.
     *  
     *  ===> Mode visée : 
     *      Slime se déplace très rapidement à la cible et se pose sur elle ? + une aura particulière sur le
     *      slime et sur nous (notre bras ?) afin de renforcer l'effet.
     *      Quand on change de cible, le slime essaie d'adapter sa position.
     *      Lorsque nous projetons l'entité, le slime tourne sur lui-même et retombe 1-2s avant de revenir vers nous.
     *      Comme une cartouche de fusil après un tir.
     *      Si aucune cible n'est encore sélectionnée, Slimy vole à côté de nous, aura activée.
     *  
     *  ===> Mort du joueur :
     *      Animation d'échec sur le slime : s'applatit ? + Perd ses pièces d'or / trésors ?
     *  
     *  ===> Obtention trésor :
     *  
     *  
     *  ===> Autres idées <===
     *      - Particules de sueur quand le slime met trop longtemps à rejoindre le joueur
     *      - Animation d'écrasement quand le slime peut se poser au sol après avoir volé.
     *      - Fumée lorsqu'il décole pour s'envoler
     *      - Trail derrière les ailes du slime
     *      - Particules de glue/slime lors de certaines interactions ? impact, bond
     *      - Animation d'idle long
     *  
     *  ---------------------------------------------------------------
     */

    // Start is called before the first frame update
    void Start()
    {
        player = HeroMovements.PlayerBody.parent.GetComponent<HeroMovements>();
        grabAndProjectSkill = player.GetComponent<GrabAndProjectSkill>();

        idleAnimIndex = Animator.StringToHash("IdleSlime");
        walkAnimIndex = Animator.StringToHash("WalkSlime");
        fallingAnimIndex = Animator.StringToHash("FallingSlime");
        flyAnimIndex = Animator.StringToHash("FlySlime");
        swirlingAnimIndex = Animator.StringToHash("SwirlingSlime");
        
        HeroMovements.OnDoubleJump += DoDoubleJumpEvent;

        followPlayer = true;
        if (followCheckFreq == 0f) followCheckFreq = 0.3f; //Eviter les boucles infinies sur une même frame.
        distCheckCoroutine = StartCoroutine(CheckDistanceToPlayer());

        availablePositions = new bool[2];
        positionsCheckCoroutine = StartCoroutine(AvailablePositionsCheck());

        transform.position = lowPosition.position;
    }

    private void OnDisable()
    {
        HeroMovements.OnDoubleJump -= DoDoubleJumpEvent;
    }

    // Update is called once per frame
    void Update()
    {
        #region Player Follow

        if (!followPlayer || ammoSlimeFlying) goto SkipFollow;

        Ray playerToGroundRay = new Ray(player.transform.position, -player.transform.up);
        playerIsNearGround = Physics.SphereCast(playerToGroundRay, 0.3f, 1.5f, obstaclesMask);
        //Si playerIsNearGround faux -> on passe en mode volant.
        Ray toGroundRay = new Ray(transform.position, -transform.up);

        if (distToFollowPoint > minFollowDist)
        {
            isFollowing = true;
            /* Si les positions à droite sont toutes impossibles, on se déplace en mode volant vers l'arrière.
             * Sinon on check 
             */

            //Faire avancer le slime vers le joueur puis le diriger vers le sol s'il en est assez proche.
            Vector3 followDirection;

            //if (Physics.SphereCast(toGroundRay, entityRadius, out groundHit, landMinDist, obstaclesMask) && playerIsNearGround)
            if (Physics.Raycast(toGroundRay, out groundHit, landMinDist, obstaclesMask) 
                && playerIsNearGround
                && availablePositions[0])
            {
                positionIndex = 0;
                
                if (groundHit.distance >= .35f)
                {
                    //Si sol est < landMinDist et que le joueur est au sol (playerIsNearGround true), on tombe
                    if (!falling)
                    {
                        falling = true;
                        grounded = false;
                        ChangeSlimeAnimations(SlimeAnimState.Fall);
                    }

                    followDirection = lowPosition.transform.position - transform.position;
                    transform.Translate(
                        (followDirection.normalized - Vector3.up) * baseFollowSpeed * Time.deltaTime);
                }
                else
                {
                    //Si sol est assez proche, on s'y colle et on marche ou idle.
                    grounded = true;
                    falling = false;
                    ChangeSlimeAnimations(SlimeAnimState.Walk);

                    followDirection = lowPosition.transform.position - transform.position;
                    followDirection -= Vector3.up * followDirection.y;
                    transform.Translate(followDirection.normalized * baseFollowSpeed * Time.deltaTime);

                    /*Faire en sorte que le slime reste au sol de manière propre.
                     * + condition afin qu'il ne rentre pas dans des obstacles, détection en partant du haut.*/

                    //Ici le problème est que le slime se positionne sur une prédiction de position. Pas l'actuelle.
                    Vector3 highestSlimePosition = new Vector3(transform.position.x, highPosition.position.y, transform.position.z);

                    Ray obstacleBtwPointsRay = new Ray(highestSlimePosition, -transform.up);
                    float distToObstacle = (highestSlimePosition - groundHit.point).magnitude;
                    if (Physics.Raycast(obstacleBtwPointsRay,out RaycastHit fromHighToPointHit, distToObstacle, obstaclesMask))
                    {
                        Vector3 closestPoint = fromHighToPointHit.point;
                        Vector3 snappedPosition = new Vector3(transform.position.x, closestPoint.y + entityRadius / 3f, transform.position.z);
                        transform.position = snappedPosition;
                    }
                    else
                    {
                        Vector3 closestPoint = groundHit.point;
                        Vector3 snappedPosition = new Vector3(transform.position.x, closestPoint.y + entityRadius / 3f, transform.position.z);
                        transform.position = snappedPosition;
                    }
                }

            }
            else
            {
                //Si sol est > landMinDist on vole.
                if (grounded || falling)
                {
                    //lancer anim vol si on ne touche plus.
                    grounded = false;
                    falling = false;
                    ChangeSlimeAnimations(SlimeAnimState.Fly);
                }

                if (availablePositions[1])
                {
                    positionIndex = 1;
                    followDirection = highPosition.transform.position - transform.position;
                }
                else
                {
                    positionIndex = 2;
                    followDirection = behindPosition.transform.position - transform.position;
                }

                transform.Translate(followDirection.normalized * baseFollowSpeed * Time.deltaTime);
            }

            if (distToFollowPoint > 1f)
            {
                //slimeBody.LookAt(transform.position + followDirection.normalized);
                lookRot = Quaternion.LookRotation(followDirection, transform.up);
                slimeBody.rotation = lookRot;
            }
        }
        else if(isFollowing)
        {
            isFollowing = false;

            if (Physics.Raycast(toGroundRay, out groundHit, landMinDist, obstaclesMask) 
                && playerIsNearGround
                && availablePositions[0])
            {
                ChangeSlimeAnimations(SlimeAnimState.Idle);
            }
            else
            {
                ChangeSlimeAnimations(SlimeAnimState.Fly);
            }

            //slimeBody.LookAt(transform.position + player.playerBody.forward);
            lookRot = Quaternion.LookRotation(player.playerBody.forward, transform.up);

            slimeBody.rotation = lookRot;//Quaternion.Slerp(transform.rotation, lookRot, 150f * Time.deltaTime);
        }

        switch (positionIndex)
        {
            case 0:
                currentFollowPosition = lowPosition.position;
                break;

            case 1:
                currentFollowPosition = highPosition.position;
                break;

            case 2:
                currentFollowPosition = behindPosition.position;
                break;
        }

        SkipFollow:
        #endregion

        if (playerIsAiming)
        {
            if (waitingForTarget)
            {
                transform.position = aimPositionTr.position; //Possibilité de faire un suivi progressif, + beau.
                slimeBody.rotation = HeroMovements.PlayerBody.rotation;
            }
        }

        if (undoSwirlDelay > 0f)
        {
            undoSwirlDelay -= Time.deltaTime;
            if(undoSwirlDelay <= 0f)
            {
                if(slimyAnimator.GetCurrentAnimatorStateInfo(0).IsName("swirling_slime"))
                {
                    ChangeSlimeAnimations(SlimeAnimState.Fly);
                }
            }
        }
    }

    private void ChangeSlimeAnimations(SlimeAnimState newAnimState)
    {
        slimyAnimator.SetBool(flyAnimIndex, false);
        slimyAnimator.SetBool(fallingAnimIndex, false);
        slimyAnimator.SetBool(walkAnimIndex, false);
        slimyAnimator.SetBool(idleAnimIndex, false);
        slimyAnimator.SetBool(swirlingAnimIndex, false);
        
        switch (newAnimState)
        {
            case SlimeAnimState.Idle:
                slimyAnimator.SetBool(idleAnimIndex, true);
                break;

            case SlimeAnimState.Fly:
                slimyAnimator.SetBool(flyAnimIndex, true);
                
                break;

            case SlimeAnimState.Walk:
                slimyAnimator.SetBool(walkAnimIndex, true);
                break;

            case SlimeAnimState.Fall:
                slimyAnimator.SetBool(fallingAnimIndex, true);
                break;

            case SlimeAnimState.Swirl:
                slimyAnimator.SetBool(swirlingAnimIndex, true);
                break;
        }
    }

    #region Follow Coroutines
    IEnumerator CheckDistanceToPlayer()
    {
        do
        {
            distToFollowPoint = (currentFollowPosition - transform.position).magnitude;

            yield return new WaitForSeconds(followCheckFreq);
        } while (followPlayer);
    }

    IEnumerator AvailablePositionsCheck()
    {
        do
        {
            Vector3 toLowPosDir = lowPosition.position + Vector3.up * entityRadius - player.transform.position;
            Ray toSlimeRay = new Ray(player.transform.position, toLowPosDir);

            Ray groundRay = new Ray(transform.position, -transform.up);
            Physics.Raycast(groundRay, out RaycastHit groundHit, landMinDist, obstaclesMask);

            //Si aucun obstacle n'est présent autour de la position basse.
            if (!Physics.SphereCast(toSlimeRay, entityRadius / 2f, toLowPosDir.magnitude, obstaclesMask))
            {
                Ray obstacleBtwPointsRay = new Ray(highPosition.position, -transform.up);
                float distToObstacle = (highPosition.position - lowPosition.position).magnitude;

                //Check si la position basse n'est pas à l'intérieur d'un bloc avec un raycast en partant du haut vers le bas.
                if(!Physics.Raycast(obstacleBtwPointsRay, distToObstacle, obstaclesMask))
                {
                    availablePositions[0] = true;
                }
                else
                {
                    availablePositions[0] = false;
                }
            }
            else //Si un obstacle est détecté, il est peut-être possible de se positionner au-dessus
            {
                //Déterminer si l'obstacle est entre le point bas et haut.
                Ray obstacleBtwPointsRay = new Ray(highPosition.position - Vector3.up * entityRadius, -transform.up);
                float distToObstacle = (highPosition.position - Vector3.up * entityRadius - lowPosition.position).magnitude;

                //S'il est assez proche pour se positionner au-dessus.
                if (Physics.SphereCast(obstacleBtwPointsRay, entityRadius/2f, distToObstacle, obstaclesMask)){
                    availablePositions[0] = true;
                }
                else //S'il est trop éloigné pour se positionner dessus
                {
                    availablePositions[0] = false;
                }
            }
            
            availablePositions[1] = !Physics.CheckSphere(
            highPosition.position,
            entityRadius,
            obstaclesMask
            );

            yield return new WaitForSeconds(followCheckFreq);
        } while (followPlayer);
    }
    #endregion

    #region Aim Mode

    /// <summary>
    /// Appelé lorsque le joueur entre en mode visée. Le Slime arrête le suivi du joueur et entre en mode visée.
    /// </summary>
    /// <param name="aimingState">Etat actuel du mode de visée.</param>
    public void AimingMode(bool aimingState)
    {
        playerIsAiming = aimingState;
        followPlayer = !aimingState;
        
        //Arrêter ou réactiver les coroutines de suivi.
        if (aimingState)
        {
            StopCoroutine(distCheckCoroutine);
            StopCoroutine(positionsCheckCoroutine);

            transform.position = aimPositionTr.position;
            waitForTargetParticles.Play();
            Destroy(Instantiate(instantMoveParticlesPrefab, transform.position, Quaternion.identity), 1.5f);
        }
        else if(!ammoSlimeFlying)
        {
            distCheckCoroutine = StartCoroutine(CheckDistanceToPlayer());
            positionsCheckCoroutine = StartCoroutine(AvailablePositionsCheck());
            waitForTargetParticles.Stop();
        }

        //LeavePlayerTarget(); //À voir selon la suite

        if(!ammoSlimeFlying) ChangeSlimeAnimations(SlimeAnimState.Fly);
        waitingForTarget = true;

    }

    /// <summary>
    /// Quand une nouvelle cible est active en mode visée. Et changement de cible ?
    /// </summary>
    public void StickToPlayerTarget()
    {
        waitingForTarget = false;
        //waitForTargetParticles.Stop();

        //Téléportation avec particules aux deux positions. Déplacement vers la position de la cible + offset.
        Destroy(Instantiate(instantMoveParticlesPrefab, transform.position, Quaternion.identity), 1.5f);
        transform.position = grabAndProjectSkill.currentTarget.transform.position + grabAndProjectSkill.currentTarget.slimyPositionOffset;
        Destroy(Instantiate(instantMoveParticlesPrefab, transform.position, Quaternion.identity), 1.5f);
        LookAt.LookWithoutYAxis(slimeBody, player.transform.position);

        //Regard du slime vers le bas.
    }

    /// <summary>
    /// Lorsqu'une cible est projetée en mode visée.
    /// </summary>
    public void PlayerTargetThrown(Vector3 throwDirection)
    {
        AimingMode(false);
        ammoSlimeCoroutine = StartCoroutine(AmmoSlimeThrown(throwDirection));
    }

    /// <summary>
    /// Quand la cible n'est plus sélectionnée.
    /// </summary>
    public void LeavePlayerTarget()
    {
        waitingForTarget = !(grabAndProjectSkill.currentTarget != null);
        
        Destroy(Instantiate(instantMoveParticlesPrefab, transform.position, Quaternion.identity), 1.5f);

        //Retour position de visée
    }

    IEnumerator AmmoSlimeThrown(Vector3 throwDirection)
    {
        ammoSlimeFlying = true;

        //Paramètres de la projection.
        float startTime = Time.time;
        float fractionOfJourney;
        Vector3 startPosition = transform.position;

        //Inverser la throwDirection pour faire en sorte que le slime parte dans le sens inverse de celui de l'entité.
        //Faire en sorte qu'il parte toujours à l'opposé mais essentiellement horizontalement.
        Vector3 slimeThrowDirection = new Vector3(
            -throwDirection.x,
            throwDirection.y > 0 ? throwDirection.y : -throwDirection.y,
            -throwDirection.z
            );
        Vector3 endPosition = startPosition + Vector3.up + slimeThrowDirection * 1.5f;
        ChangeSlimeAnimations(SlimeAnimState.Swirl);
        LookAt.LookWithoutYAxis(slimeBody, grabAndProjectSkill.currentTarget.transform.position);

        do
        {
            fractionOfJourney = (Time.time - startTime) / 1f;

            if (fractionOfJourney < 1f)
            {
                Vector3 nextPosition = Vector3.Lerp(
                startPosition,
                endPosition,
                projectionCurve.Evaluate(fractionOfJourney)
                );

                if (!Physics.CheckSphere(nextPosition, entityRadius, obstaclesMask))
                {
                    transform.position = nextPosition;
                }
                else
                {
                    //à voir.
                }
            }
            else
            {
                ammoSlimeFlying = false;
            }

            yield return null;
        } while (ammoSlimeFlying);

        ChangeSlimeAnimations(SlimeAnimState.Fly);
        distCheckCoroutine = StartCoroutine(CheckDistanceToPlayer());
        positionsCheckCoroutine = StartCoroutine(AvailablePositionsCheck());
        waitForTargetParticles.Stop();
    }

    #endregion

    private void DoDoubleJumpEvent(Transform heroPosition)
    {
        ChangeSlimeAnimations(SlimeAnimState.Swirl);
        undoSwirlDelay = 1f;
    }
}
