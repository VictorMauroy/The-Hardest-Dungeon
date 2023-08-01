using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using DG.Tweening;
using OutlineEffect;

public delegate void DashEvent(Transform heroPosition);
public delegate void DashEndEvent(Transform heroPosition);
public delegate void JumpEvent(Transform heroPosition);
public delegate void DoubleJumpEvent(Transform heroPosition);
public delegate void LandingEvent(Transform heroPosition);

public class HeroMovements : MonoBehaviour
{
    public MainCharControls inputActions;
    [SerializeField] private GrabAndProjectSkill projectSkill;

    private static Transform _playerBody;
    public static Transform PlayerBody
    {
        get => _playerBody;
    }

    public static DashEvent OnDash;
    public static DashEndEvent OnDashEnd;
    public static JumpEvent OnJump;
    public static DoubleJumpEvent OnDoubleJump;
    public static LandingEvent OnLanding;

    [Header("Player rights")]
    public bool canMove;

    [Header("Movement properties")]
    [SerializeField] private float walkSpeed;
    private float baseWalkSpeed;
    //Rigidbody rb;
    private Vector3 moveDir;
    public Transform playerBody;
    [SerializeField] private ParticleSystem walkParticles;
    int velocityHash; //Used to modify the walk speed animation with a gamepad.
    float walkDirMagnitude;

    [Header("Jump")]
    [SerializeField] private float jumpPower;
    public static bool grounded;
    bool jumpRequest; //Transmission info controller -> update
    int jumpCount = 0; //0 = on ground, 1 = first jump, 2 = second jump.
    [SerializeField] private float fallJumpDelay; //Error delay while falling from a platform.
    private float fallTime;
    private float verticalVelocity;
    [SerializeField] private float fallingSpeedModifier;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float gravityScale;
    [SerializeField] private float heightOffset;
    private RaycastHit groundHit;
    private RaycastHit headHit;

    [Header("Dash properties")]
    public bool dashing;
    private bool dashRequest; //Memorize dash request from player (one frame only)
    [SerializeField] private float dashSpeed;
    private float dashStartTime;
    private float journeyLength;
    private Vector3 dashDirSaved;
    [SerializeField] private float maxDashDistance;
    [SerializeField] private float minDashDistance;
    [SerializeField] private LayerMask dashObstaclesMask;
    private RaycastHit dashHitInfo;
    private Coroutine dashCoroutine;
    [SerializeField] private float delayBtwDash;
    private float dashDelay;
    private bool airDashUsed; //Cannot make multiple air dash.
    [SerializeField] private Outline characterOutline;

    [Header("Camera settings")]
    public CameraManager cameraManager;
    [SerializeField, Tooltip("Currently used for raycasts in : Jump, Fall, Move")] 
    private LayerMask obstaclesMask;

    [Header("Animations")]
    [SerializeField] private Animator characterAnimator;

    private void Awake()
    {
        inputActions = new MainCharControls();
        _playerBody = playerBody;
        velocityHash = Animator.StringToHash("Velocity");
    }

    #region PlayerControlConnexion
    private void OnEnable()
    {
        inputActions.Enable();

        //Get the move direction from left stick or WASD/ZQSD inputs
        inputActions.Player.Movement.performed += context =>
        {
            if (canMove)
            {
                Vector2 dir = context.ReadValue<Vector2>();
                moveDir = new Vector3(dir.x, moveDir.y, dir.y);
            }
        };
        inputActions.Player.Movement.canceled += _ => 
        { 
            moveDir = Vector3.zero;
            characterAnimator.SetBool("Walking", false);
        };

        //Get the jump trigger from the south button (gamepad) or space button.
        //Can also be used for soaring/hovering and double jump.
        inputActions.Player.Jump.performed += _ => {
            if(canMove) jumpRequest = true;
        };

        inputActions.Player.Dash.performed += _ =>
        {
            if(!dashing) dashRequest = true;
        };
    }

    private void OnDisable()
    {
        inputActions.Disable();
        
    }
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        baseWalkSpeed = walkSpeed;
        characterOutline.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!canMove)
        {
            goto SkipAllMovements;
        }

        if (dashing)
        {
            //Ignore during the period of a dash or cinematics the other movements of the character.
            goto SkipBasicMovements;
        }

        #region JUMP & Ceiling
        Ray toGroundRay = new Ray(transform.position, -transform.up);
        Ray toCeilingRay = new Ray(transform.position, transform.up);
        //grounded = Physics.Raycast(toGroundRay, out groundHit, heightOffset + 0.1f, obstaclesMask);

        //Raise the search area of the ground. Allow to stay on the edges of platforms.
        grounded = Physics.SphereCast(toGroundRay, 0.3f, out groundHit, heightOffset - 0.2f, obstaclesMask);

        verticalVelocity += gravity * gravityScale * Time.deltaTime;
        if (grounded && verticalVelocity < 0)
        {
            verticalVelocity = 0f;
            jumpCount = 0;
            airDashUsed = false;
            if (!walkParticles.isPlaying) walkParticles.Play();

            if (grounded && fallTime > 1.6f)
            {
                if (OnLanding != null) OnLanding(transform);
            }

            /*Above : Keeps the player on the "ground". Allows to climb slopes and not fall into the void in certain situations.*/
            Vector3 closestPoint = groundHit.point;
            Vector3 snappedPosition = new Vector3(transform.position.x, closestPoint.y + heightOffset, transform.position.z);
            transform.position = snappedPosition;
        }
        //If aloft : blocks the movements and keeps from jumping higher when a ceiling is encountered.
        else if (Physics.SphereCast(toCeilingRay, 0.3f, out headHit, heightOffset - 0.4f, obstaclesMask))
        {
            verticalVelocity = 0f;
            Vector3 closestPoint = headHit.point;
            Vector3 snappedPosition = new Vector3(transform.position.x, closestPoint.y - heightOffset, transform.position.z);
            transform.position = snappedPosition;
        }

        fallTime = grounded ? 0f : fallTime + Time.deltaTime; //Temps de chute

        //Connected to the jump input
        if (jumpRequest)
        {
            if ((grounded || fallTime <= fallJumpDelay) && jumpCount == 0) //Si au sol + délai d'erreur pour le saut (confort joueur)
            {
                verticalVelocity = jumpPower;
                jumpCount = 1;
                if (OnJump != null) OnJump(transform);
            } else if (jumpCount == 1 || (jumpCount == 0 && !grounded))
            {
                verticalVelocity = jumpPower;
                jumpCount = 2;
                characterAnimator.SetTrigger("Flip");
                if (OnDoubleJump != null) OnDoubleJump(transform);
            }
            jumpRequest = false;
        }

        Physics.Raycast(toGroundRay, out RaycastHit toGroundhit, Mathf.Infinity, obstaclesMask);
        if (!grounded && (toGroundhit.distance > 1.5f || jumpCount > 0))
        {
            characterAnimator.SetBool("Falling", true);
            if (walkParticles.isPlaying) walkParticles.Stop();
        }
        else
        {
            characterAnimator.SetBool("Falling", false);
        }

        /* Isn't working, saved for later
        if (fallTime >= 1.6f && groundHit.distance < 1f && groundHit.distance > 0f)
        {
            characterAnimator.SetTrigger("FallEnd");
            fallTime = 0.5f; 
        }*/

        /*
         * When climbing a slope, check the angle and the distance between the ground and our feet 
         * and then raise the character by that amount. However, if the angle is too high, 
         * the character will have to fall along the surface until the angle is correct again. 
         * The player is pushed slightly away from the slope while falling.
         */
        #endregion

        #region Walk & Horizontal Move 

        moveDir = Vector3.ClampMagnitude(moveDir, 1f);
        walkDirMagnitude = moveDir.magnitude;
        characterAnimator.SetFloat(velocityHash, walkDirMagnitude);
        characterAnimator.SetBool("Walking", walkDirMagnitude > .04f);
        walkSpeed = baseWalkSpeed;
        Vector3 nextMove = transform.position + transform.TransformDirection(moveDir * walkSpeed * Time.deltaTime);

        if (Physics.CheckCapsule(nextMove + transform.up * 0.5f, nextMove - transform.up * 0.55f, 0.3f, obstaclesMask))
        {
            walkSpeed = 0f;
        }
        //Debug.DrawLine(transform.position, nextMove, Color.magenta, 0.5f);

        #endregion
        if (!grounded) walkSpeed *= fallingSpeedModifier;

        transform.Translate((moveDir * walkSpeed + verticalVelocity * Vector3.up) * Time.deltaTime);

    SkipBasicMovements:

        #region Dash

        if (dashDelay > 0f)
        {
            dashDelay -= Time.deltaTime;
        }

        if (dashRequest)
        {
            if (airDashUsed || dashDelay > 0f) goto IgnoreDash;

            //Default action : dash forward of the characterBody.
            Vector3 dashDir = playerBody.forward;
            Vector3 dashDirection = dashDir * maxDashDistance;

            //Dash + directionnal key -> dash to new movement direction
            if (moveDir != Vector3.zero)
            {
                dashDir = moveDir;
                dashDir = Vector3.Normalize(dashDir);
                dashDirection = transform.TransformDirection(dashDir * maxDashDistance);
            }

            dashDirSaved = dashDirection / maxDashDistance;

            //Debug.DrawRay(transform.position, dashDestination - transform.position, Color.red, 10f);

            if (!Physics.CapsuleCast(
                transform.position + transform.up * 0.5f,
                transform.position - transform.up * 0.55f,
                0.3f,
                dashDirection,
                out dashHitInfo,
                maxDashDistance,
                dashObstaclesMask))
            {
                dashing = true;

                //Vector3 dashDestination = transform.position + transform.forward * maxDashDistance;
                dashCoroutine = StartCoroutine(Dash(transform.position, transform.position + dashDirection));
                Debug.DrawRay(transform.position, dashDirection, Color.blue, 0.5f);
            }
            else if (dashHitInfo.distance > minDashDistance)
            {
                dashing = true;
                Vector3 shortenedDashDestination =
                    transform.position + (moveDir != Vector3.zero ?
                        transform.TransformDirection(dashDir * (dashHitInfo.distance - 0.3f)) :
                        dashDir * (dashHitInfo.distance - 0.3f));
                //Vector3 dashDestination = transform.position + transform.forward * maxDashDistance;
                dashCoroutine = StartCoroutine(Dash(transform.position, shortenedDashDestination));
                Debug.DrawRay(
                    transform.position,
                    playerBody.forward * (dashHitInfo.distance - 0.3f),
                    moveDir != Vector3.zero ? Color.cyan : Color.red,
                    0.5f
                    );
            }

            // à voir après tests : 
            //Raycast sphérique partie basse du personnage. Sert à aider à déterminer si le personnage pourrait
            // atterir sur une plateforme si elle est ratée de peu. -> Correction de la position pour légèrement
            // élever le personnage et le faire se positionner sur la plateforme.
            // /!\ Check s'il ne touchera pas de plafond ?

    IgnoreDash:

            dashRequest = false;
        }
    #endregion

    SkipAllMovements:
        return;
    }

    IEnumerator Dash(Vector3 startPosition, Vector3 endPosition)
    {
        characterAnimator.SetBool("Dashing", true);
        airDashUsed = true; 
        if (grounded) 
        {
            dashDelay = delayBtwDash;
        }
        if (OnDash != null) OnDash(transform);
        CameraManager.Instance.ScreenShake(5f, .1f);
        characterOutline.enabled = true;

        //While some ennemies are in front of us or on us, make them fly.
        LayerMask enemyLayerMask = LayerMask.NameToLayer("Enemy");
        Vector3 checkPosition = transform.position + dashDirSaved;
        if(Physics.CheckCapsule(checkPosition + transform.up * 0.5f, checkPosition - transform.up * 0.55f, 0.3f, enemyLayerMask) )
        {
            Collider[] enemyFounded = Physics.OverlapCapsule(checkPosition + transform.up * 0.5f, checkPosition - transform.up * 0.55f, 0.3f, enemyLayerMask);
            foreach (Collider collider in enemyFounded)
            {
                collider.SendMessageUpwards("HitByPlayerDash", dashDirSaved, SendMessageOptions.DontRequireReceiver);
            }
            CameraManager.Instance.ScreenShake(10f, .2f);
        }

        dashStartTime = Time.time;
        journeyLength = Vector3.Distance(startPosition, endPosition);
        float fractionOfJourney;
        do
        {
            // Distance moved equals elapsed time times speed.
            float distCovered = (Time.time - dashStartTime) * dashSpeed;

            // Fraction of journey completed equals current distance divided by total distance.
            fractionOfJourney = distCovered / journeyLength;

            transform.position = Vector3.Lerp(startPosition, endPosition, fractionOfJourney);

            yield return null;
        } while (fractionOfJourney < 1);

        dashing = false;
        characterAnimator.SetBool("Dashing", false);

        if (OnDashEnd != null) OnDashEnd(transform);
        characterOutline.enabled = false;
    }

    public void StopDash()
    {
        if (dashCoroutine == null) return;

        StopCoroutine(dashCoroutine);
        dashing = false;
        characterAnimator.SetBool("Dashing", false);

        if (OnDashEnd != null) OnDashEnd(transform);
        characterOutline.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(dashing && other.tag == "Enemy")
        {
            other.SendMessageUpwards("HitByPlayerDash", dashDirSaved, SendMessageOptions.DontRequireReceiver);
            CameraManager.Instance.ScreenShake(10f, .2f);
        }
    }

    #region Other Interactions

    public void Bounce(float bouncePower)
    {
        verticalVelocity = bouncePower;
        if (OnDoubleJump != null) OnDoubleJump(transform);

        /* ------ IMPORTANT : Re-allow the player to make a double jump when jumping on an entity or a special block  ------ */
        if (jumpCount == 2) jumpCount = 1;
    }
    #endregion

    /// <summary>
    /// Donner ou retirer le contrôle complet du personnage au joueur.
    /// </summary>
    /// <param name="isAllowed">Vrai : contrôle total ; faux : aucun contrôle.</param>
    public void LockOrUnlockAllControls(bool isAllowed)
    {
        if(isAllowed == false) //Player cannot control the character
        {
            canMove = false;
            cameraManager.canRotateCamera = false;
        }
        else //Give all controls to the player
        {
            canMove = true;
            cameraManager.canRotateCamera = true;
        }
    }

    #region Death And Respawn
    public void Respawn(Vector3 respawnPosition, Quaternion respawnRotation, float respawnDelay)
    {
        transform.position = respawnPosition;
        transform.rotation = respawnRotation;
        playerBody.localRotation = Quaternion.identity;
        cameraManager.baseCamFollowTarget.localRotation = Quaternion.identity;
        
        //At the end of the fade : player respawn animation
        DOVirtual.DelayedCall(respawnDelay, () =>
        {
            characterAnimator.SetTrigger("Revive");

            //Animation complete : re-unlock controls for the player
            DOVirtual.DelayedCall(4f, () =>
            {
                LockOrUnlockAllControls(true);
            });
        });
    }

    public void Death()
    {
        LockOrUnlockAllControls(false);
        characterAnimator.SetTrigger("Death");
    }
    #endregion

}