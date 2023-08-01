using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ThrowAxis
{
    Right,
    Left,
    Backward,
    Bottom
}

[RequireComponent(typeof(HeroMovements), typeof(CameraManager))]
public class GrabAndProjectSkill : MonoBehaviour
{
    public bool canGrab;
    public MainCharControls inputActions;
    public SlimyBehavior slimyBehavior;
    private HeroMovements heroMovements;
    private CameraManager cameraManager;

    [SerializeField] private float delayBtwTargetSwitch;
    private float targetSwitchTime;

    public bool aiming;
    [SerializeField] private float targetMaxDist;
    [SerializeField] private LayerMask targetVisibilityLayerMask;
    public GrabbableTarget currentTarget;
    private Transform mainCameraTr;
    private ThrowAxis currentThrowAxis;
    [SerializeField] private List<GrabbableTarget> visibleTargets;
    [SerializeField] private List<GrabbableTarget> possibleTargets;

    public static Vector3 LastThrowDir;

    [SerializeField] private GameObject thrownTargetParticlesPrefab;

    [Header("Animations")]
    [SerializeField] private Animator characterAnimator;
    private int aimingVerticalLayerIndex;
    private int aimingHorizontalLayerIndex;
    int horizontalBlendIndex;
    int verticalBlendIndex;

    private void Awake()
    {
        inputActions = new MainCharControls();
    }

    // Start is called before the first frame update
    void Start()
    {
        heroMovements = GetComponent<HeroMovements>();
        cameraManager = GetComponent<CameraManager>();
        canGrab = true;

        visibleTargets = new List<GrabbableTarget>();

        mainCameraTr = Camera.main.transform;

        GrabVisibility.OnGrabbableVisible += AddGrabbableTarget;
        GrabVisibility.OnGrabbableInvisible += RemoveGrabbableTarget;
        GrabVisibility.OnGrabbableViewConeEnter += AddInVisionConeTarget;
        GrabVisibility.OnGrabbableViewConeExit += RemoveInVisionConeTarget;

        currentThrowAxis = ThrowAxis.Right;

        aimingVerticalLayerIndex = characterAnimator.GetLayerIndex("AimingVertical");
        aimingHorizontalLayerIndex = characterAnimator.GetLayerIndex("AimingHorizontal");
        horizontalBlendIndex = Animator.StringToHash("ArmHorizontalValue");
        verticalBlendIndex = Animator.StringToHash("ArmVerticalValue");
    }

    private void OnEnable()
    {
        inputActions.Enable();

        //Contrôles liés au skill "Attraper et Projeter"
        inputActions.Player.Aim.performed += _ =>
        {
            EnterAimMode();
        };
        inputActions.Player.Aim.canceled += _ =>
        {
            ExitAimMode();
        };

        //à voir pour remplacer cette touche en fonction de l'évolution du controller.
        inputActions.Player.Attack.started += _ => ThrowTarget();

        inputActions.Player.Selection.performed += context =>
        {
            Vector2 dir = context.ReadValue<Vector2>();
            if (dir == Vector2.right) currentThrowAxis = ThrowAxis.Right;
            if (dir == Vector2.left) currentThrowAxis = ThrowAxis.Left;
            if (dir == Vector2.up) currentThrowAxis = ThrowAxis.Backward;
            if (dir == Vector2.down) currentThrowAxis = ThrowAxis.Bottom;
            if (currentTarget) ChangeTargetAxisVisualisation();
        };

        inputActions.Player.Movement.started += context => SwitchTarget(context.ReadValue<Vector2>());
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        GrabVisibility.OnGrabbableVisible -= AddGrabbableTarget;
        GrabVisibility.OnGrabbableInvisible -= RemoveGrabbableTarget;
        GrabVisibility.OnGrabbableViewConeEnter -= AddInVisionConeTarget;
        GrabVisibility.OnGrabbableViewConeExit -= RemoveInVisionConeTarget;
    }

    // Update is called once per frame
    void Update()
    {
        /*
          * Faire un effet visuel pour différentes choses :
         * - Cibles possibles (shader ? Surbrillance rouge ? Outline rouge ?)
         * - Cible sélectionnée (Slimy sur sa tête, main qui apparaît + autres indications)
         * - Cible sélectionnée quittée (Slimy s'en va, main disparaît, petit effet de dissipation)
         */

        if(possibleTargets.Count == 1 && aiming && !currentTarget)
        {
            currentTarget = possibleTargets[0];
            currentTarget.Selected();
            ChangeTargetAxisVisualisation();
        }
        else if(possibleTargets.Count > 1 && aiming && !currentTarget)
        {
            currentTarget = possibleTargets[possibleTargets.Count -1];
            currentTarget.Selected();
            ChangeTargetAxisVisualisation();
        }

        if(possibleTargets.Count < 1)
        {
            currentTargetNumber = 0;
        }

        if (targetSwitchTime >= 0f) targetSwitchTime -= Time.deltaTime;

    }

    private int currentTargetNumber;
    public void SwitchTarget(Vector2 moveDir)
    {
        /*
         * Amélioration : permettre de sélectionner les cibles de manière précise 
         * en utilisant la direction du moveDir.
         */

        if(possibleTargets.Count > 1 && aiming && targetSwitchTime <= 0f)
        {
            if(currentTargetNumber+1 < possibleTargets.Count)
            {
                currentTargetNumber++;
            }
            else
            {
                currentTargetNumber = 0;
            }
            if(currentTarget) currentTarget.Unselected();
            currentTarget = possibleTargets[currentTargetNumber];
            currentTarget.Selected();
            ChangeTargetAxisVisualisation();

            targetSwitchTime = delayBtwTargetSwitch;
        }
    }

    public void EnterAimMode()
    {
        if (!canGrab || heroMovements.dashing || !HeroMovements.grounded || aiming || !heroMovements.canMove) return;

        aiming = true;
        cameraManager.AimingMode(true);

        heroMovements.canMove = false;
        slimyBehavior.AimingMode(true);

        foreach (GrabbableTarget target in visibleTargets)
        {
            Vector3 targetDirection = target.transform.position - mainCameraTr.position;
            Ray ray = new Ray(mainCameraTr.position, targetDirection);
            if (!Physics.Raycast(ray, targetDirection.magnitude, targetVisibilityLayerMask))
            {
                target.EnterTargetSelectionMode();
            }
        }

        characterAnimator.SetBool("Walking", false);
        characterAnimator.SetTrigger("CastMagic");
        characterAnimator.SetBool("CastingMagic", true);

        characterAnimator.SetLayerWeight(aimingHorizontalLayerIndex, 1f);
        characterAnimator.SetLayerWeight(aimingVerticalLayerIndex, 1f);

        /* Pour regarder correctement la cible : 
        * Faire une transition du CamFollowTarget afin qu'il s'oriente dans la direction de l'entité
        *      Et ce sur tous les axes
        * Changer le LookAt de la caméra de visée à chaque fois

        * Faire un mode recherche de cible avec le CamFollowTarget qui permet de regarder autour de soi
        * Le pouvoir ne peut être utilisé sans cible.
        */
    }

    public void ExitAimMode()
    {
        aiming = false;
        cameraManager.AimingMode(false);
        heroMovements.canMove = true;
        slimyBehavior.AimingMode(false);

        characterAnimator.SetBool("CastingMagic", false);
        characterAnimator.SetLayerWeight(aimingHorizontalLayerIndex, 0f);
        characterAnimator.SetLayerWeight(aimingVerticalLayerIndex, 0f);
        characterAnimator.SetFloat(verticalBlendIndex, 0f);
        characterAnimator.SetFloat(horizontalBlendIndex, 0f);

        foreach (GrabbableTarget target in visibleTargets)
        {
            target.ExitTargetSelectionMode();
        }
        possibleTargets.Clear();
        if (currentTarget) currentTarget.Unselected();
        currentTarget = null;
    }

    public void ThrowTarget()
    {
        if (!currentTarget) return;

        if (currentTarget.GrabbedAndThrown(currentThrowAxis))
        {
            CameraManager.Instance.ScreenShake(3f, .2f);
            slimyBehavior.PlayerTargetThrown(LastThrowDir);
            Destroy(Instantiate(thrownTargetParticlesPrefab, currentTarget.transform.position, Quaternion.identity), 1f);
            ExitAimMode();
        }
    }

    private void ChangeTargetAxisVisualisation()
    {
        currentThrowAxis = currentTarget.ChangeAxisVisualisation(currentThrowAxis);
        
        /* Ci-dessous, les calculs ne sont pas tous corrects, ce n'est pas bon du tout en Update(). 
         *  La seconde partie est la plus problématique, la première est plus sûre.
         */
        if (currentTarget)
        {
            slimyBehavior.StickToPlayerTarget();

            float sideA = Vector3.Distance(
                transform.position, 
                new Vector3(
                    currentTarget.transform.position.x,
                    transform.position.y,
                    currentTarget.transform.position.z)
                );

            //Horizontal distance to targetHead
            float sideB = currentTarget.transform.position.y - transform.position.y;
            //Vertical difference
            float angleRad = Mathf.Atan(sideB / sideA);
            //Or if degrees are needed
            float angleDeg = angleRad * Mathf.Rad2Deg;

            float verticalValue = Mathf.Clamp(angleDeg, -30f, 30f) / 30f;
            characterAnimator.SetFloat(verticalBlendIndex, verticalValue);

            //angle horizontal , renvoie environ si la cible est à gauche ou à droite.
            float horizontalSignedAngle = Vector3.SignedAngle(
                characterAnimator.transform.forward,
                (currentTarget.transform.position - characterAnimator.transform.position).normalized,
                Vector3.up);

            float horizontalValue = Mathf.Clamp(horizontalSignedAngle, -50f, 50f) / 50f;
            characterAnimator.SetFloat(horizontalBlendIndex, horizontalValue);
        }
    }

    /// <summary>
    /// Ajoute une entrée à la liste des éléments visibles pouvant être attrapés.
    /// </summary>
    /// <param name="target">Cible attrapable</param>
    private void AddGrabbableTarget(GrabbableTarget target)
    {
        if (aiming)
        {
            Vector3 targetDirection = target.transform.position - mainCameraTr.position;
            Ray ray = new Ray(mainCameraTr.position, targetDirection);
            if (!Physics.Raycast(ray, targetDirection.magnitude, targetVisibilityLayerMask))
            {
                target.EnterTargetSelectionMode();
            }
        } 
        visibleTargets.Add(target);
    }

    /// <summary>
    /// Supprime une entrée à la liste des éléments visibles pouvant être attrapés.
    /// </summary>
    /// <param name="target">Cible attrapable</param>
    private void RemoveGrabbableTarget(GrabbableTarget target)
    {
        if (aiming) target.ExitTargetSelectionMode();
        visibleTargets.Remove(target);
        possibleTargets.Remove(target);
    }

    private void AddInVisionConeTarget(GrabbableTarget target)
    {
        float distToTarget = Vector3.Distance(transform.position, target.transform.position);
        Debug.Log("Dist to target is : " + distToTarget, target.gameObject);
        if (distToTarget < targetMaxDist)
        {
            target.EnterPossibleTargetMode();
            possibleTargets.Add(target);
        }
    }

    private void RemoveInVisionConeTarget(GrabbableTarget target)
    {
        if(target == currentTarget)
        {
            target.Unselected();
            currentTarget = null;
            slimyBehavior.LeavePlayerTarget();
        }
        target.ExitPossibleTargetMode();
        possibleTargets.Remove(target);
    }
}
