using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Player rights")]
    public bool canRotateCamera;

    [Header("Input Settings")]
    public MainCharControls inputActions;
    [SerializeField] private PlayerInput playerInput;
    private float rotationPower;
    public float gamepadRotSensitivity;
    public float mouseRotSensitivity;
    private string previousControlScheme = "";
    private const string gamepadScheme = "Gamepad";
    private const string mouseScheme = "MouseAndKeyboard";
    private Vector3 moveDir;
    public bool invertYAxis;

    [Header("Camera properties")]
    public CinemachineVirtualCamera baseCamera;
    Cinemachine3rdPersonFollow baseCamFollow;
    public Transform baseCamFollowTarget;
    public Transform playerBody;
    private Vector2 mouseDelta;
    private float baseCameraDistance;
    private float baseAimingCameraDistance;
    [SerializeField] private float undoZoomSpeed;
    private RaycastHit lastCameraRayHit;
    [SerializeField] private float minCameraDistance = 0.7f;
    [SerializeField] private float sphereCastRadius = 0.2f;
    [SerializeField] private LayerMask obstaclesMask;

    [Header("Aiming Mode")]
    public CinemachineVirtualCamera aimingCamera;
    Cinemachine3rdPersonFollow aimingCamFollow;

    Coroutine screenShakeCoroutine;
    CinemachineBasicMultiChannelPerlin baseChannelPerlin;
    CinemachineBasicMultiChannelPerlin aimingChannelPerlin;

    private void Awake()
    {
        Instance = this;
        inputActions = new MainCharControls();
        baseCamFollow = baseCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        aimingCamFollow = aimingCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        baseChannelPerlin = baseCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        aimingChannelPerlin = aimingCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        rotationPower = mouseRotSensitivity;
    }

    private void OnEnable()
    {
        inputActions.Enable();

        playerInput.onControlsChanged += OnControlsChanged;

        //Get the move direction from left stick or WASD/ZQSD inputs
        inputActions.Player.Movement.performed += context =>
        {
            Vector2 dir = context.ReadValue<Vector2>();
            moveDir = new Vector3(dir.x, moveDir.y, dir.y);

        };
        inputActions.Player.Movement.canceled += _ =>
        {
            moveDir = Vector3.zero;
        };

        //Get the look direction from right stick or mouse delta.
        inputActions.Player.Look.performed += contextLook =>
        {
            mouseDelta = contextLook.ReadValue<Vector2>();
            if (invertYAxis) mouseDelta = new Vector2(mouseDelta.x, -mouseDelta.y);
            //if (mouseDelta.sqrMagnitude < 0.05f) mouseDelta = Vector2.zero;
        };
        inputActions.Player.Look.canceled += _ => { mouseDelta = Vector2.zero; };
    }

    private void OnDisable()
    {
        playerInput.onControlsChanged -= OnControlsChanged;
        inputActions.Enable();
    }

    // Start is called before the first frame update
    void Start()
    {
        baseCameraDistance = baseCamFollow.CameraDistance;
        baseAimingCameraDistance = aimingCamFollow.CameraDistance;
    }

    // Update is called once per frame
    void Update()
    {
        if (!canRotateCamera)
        {
            return; //Solution temporaire 
        }

        //Appliquer une rotation à la caméra en fonction du déplacement de la souris ou du joystick droit.
        baseCamFollowTarget.transform.rotation *= Quaternion.AngleAxis(mouseDelta.x * rotationPower, Vector3.up);
        baseCamFollowTarget.transform.rotation *= Quaternion.AngleAxis(mouseDelta.y * rotationPower, Vector3.right);

        var angles = baseCamFollowTarget.transform.localEulerAngles;
        angles.z = 0;

        var angle = baseCamFollowTarget.transform.localEulerAngles.x;

        //Limiter la rotation vers le haut/bas.
        if (angle > 180 && angle < 340)
        {
            angles.x = 340;
        }
        else if (angle < 180 && angle > 40)
        {
            angles.x = 40;
        }

        baseCamFollowTarget.transform.localEulerAngles = angles;

        if (!aimingCamera.gameObject.activeInHierarchy)
        {
            //Rotation du personnage en fonction de la caméra et/ou des déplacements appliqués à ce dernier.
            if (moveDir != Vector3.zero)
            {
                //Lorsqu'on se déplace, le personnage est toujours orienté dans la même direction que la caméra.
                transform.rotation = Quaternion.Euler(0, baseCamFollowTarget.transform.rotation.eulerAngles.y, 0);

                // Orienter le corps du personnage de manière fluide vers la direction vers laquelle le joueur le déplace.
                //Chercher s'il est possible de simplifier. Cela semble lourd.
                Vector3 moveDirAdapted = Quaternion.AngleAxis(transform.eulerAngles.y, Vector3.up) * moveDir;
                float singleStep = 15f * Time.deltaTime;
                Vector3 newDirection = Vector3.RotateTowards(playerBody.forward, moveDirAdapted, singleStep, 0.0f);
                playerBody.rotation = Quaternion.LookRotation(newDirection);

                baseCamFollowTarget.transform.localEulerAngles = new Vector3(angles.x, 0, 0);
            }
            else
            {
                //On ne rotate pas le personnage entier mais seulement l'objet à suivre.
                //Permet de regarder le personnage sous différents angles lorsque nous ne nous déplaçons pas.
                baseCamFollowTarget.transform.localEulerAngles = new Vector3(angles.x, angles.y, 0);
            }
        }
        else
        {
            playerBody.rotation = Quaternion.Euler(0, baseCamFollowTarget.transform.rotation.eulerAngles.y, 0);
        }
    }

    private void LateUpdate()
    {
        /*
         * Ci-dessous : S'éloigner ou se rapprocher du joueur en fonction des obstacles visuels.
         */

        if (baseCamera.gameObject.activeInHierarchy)
        {
            RaycastHit hit;
            Vector3 rayDir = baseCamera.transform.position - baseCamFollowTarget.position;
            if (Physics.SphereCast(baseCamFollowTarget.position, sphereCastRadius, rayDir, out hit, baseCameraDistance, obstaclesMask))
            {
                //Debug.DrawLine(baseCamFollowTarget.position, hit.point, Color.red, 0.3f);

                //if (lastCameraRayHit.distance == 0f) lastCameraRayHit = hit;

                float camDist = baseCamFollow.CameraDistance;
                float nextDist = camDist;

                //Si un objet se trouve entre la caméra et le joueur
                if (hit.distance != lastCameraRayHit.distance && hit.distance - sphereCastRadius < camDist)
                {
                    if (hit.distance < camDist)
                    {
                        nextDist = (hit.distance - sphereCastRadius < minCameraDistance) ? minCameraDistance : hit.distance - sphereCastRadius;
                    }
                }
                //Si l'objet touché se trouve plus loin, derrière la caméra.
                else if (hit.distance - sphereCastRadius > camDist)
                {
                    nextDist = camDist + Time.deltaTime * undoZoomSpeed;
                }

                baseCamFollow.CameraDistance = nextDist;
                lastCameraRayHit = hit;
            }
            //Si aucun objet n'entre en collision et que nous sommes rapprochés, on peut éloigner la caméra.
            else if (baseCamFollow.CameraDistance < baseCameraDistance)
            {
                baseCamFollow.CameraDistance += Time.deltaTime * undoZoomSpeed;
                if (baseCamFollow.CameraDistance > baseCameraDistance) baseCamFollow.CameraDistance = baseCameraDistance;
            }

            /*       Multi-preventive Raycasts       */

            //En pause, à continuer plus tard.

            //Debug.DrawRay(baseCamFollowTarget.position, rayDir, hit.distance > 0 ? Color.red : Color.blue, 0.2f);

            //Left Rays
            Vector3[] leftVectors = new Vector3[4];
            for (int i = 0; i < leftVectors.Length; i++)
            {
                leftVectors[i] = rayDir;
                leftVectors[i] = Quaternion.AngleAxis(15f * i + 1, Vector3.up) * leftVectors[i];

                //Debug.DrawLine(baseCamFollowTarget.position, baseCamFollowTarget.position + leftVectors[i], Color.blue);
            }



            //Right Rays
        }

        if (aimingCamera.gameObject.activeInHierarchy)
        {
            RaycastHit hit;
            Vector3 rayDir = aimingCamera.transform.position - baseCamFollowTarget.position;

            if (Physics.SphereCast(baseCamFollowTarget.position, sphereCastRadius, rayDir, out hit, baseAimingCameraDistance, obstaclesMask))
            {
                //Debug.DrawLine(baseCamFollowTarget.position, hit.point, Color.red, 0.3f);

                //if (lastCameraRayHit.distance == 0f) lastCameraRayHit = hit;

                float camDist = aimingCamFollow.CameraDistance;
                float nextDist = camDist;

                //Si un objet se trouve entre la caméra et le joueur
                if (hit.distance != lastCameraRayHit.distance && hit.distance - sphereCastRadius < camDist)
                {
                    if (hit.distance < camDist)
                    {
                        nextDist = (hit.distance - sphereCastRadius < minCameraDistance) ? minCameraDistance : hit.distance - sphereCastRadius;
                    }
                }
                //Si l'objet touché se trouve plus loin, derrière la caméra.
                else if (hit.distance - sphereCastRadius > camDist)
                {
                    nextDist = camDist + Time.deltaTime * undoZoomSpeed;
                }

                aimingCamFollow.CameraDistance = nextDist;
                lastCameraRayHit = hit;
            }
            //Si aucun objet n'entre en collision et que nous sommes rapprochés, on peut éloigner la caméra.
            else if (aimingCamFollow.CameraDistance < baseAimingCameraDistance)
            {
                aimingCamFollow.CameraDistance += Time.deltaTime * undoZoomSpeed;
                if (aimingCamFollow.CameraDistance > baseAimingCameraDistance) aimingCamFollow.CameraDistance = baseAimingCameraDistance;
            }
        }
    }

    public void AimingMode(bool activeState)
    {   
        if (activeState)
        {
            baseCamera.gameObject.SetActive(false);
            aimingCamera.gameObject.SetActive(true);

            //Diminution de la vitesse de rotation de l'écran.
            rotationPower = rotationPower / 2f;
            playerBody.rotation = Quaternion.Euler(0, baseCamFollowTarget.transform.rotation.eulerAngles.y, 0);
        }
        else
        {
            aimingCamera.gameObject.SetActive(false);
            baseCamera.gameObject.SetActive(true);

            //Retour à la normale de la vitesse de rotation de l'écran.
            if (playerInput.currentControlScheme == mouseScheme)
            {
                rotationPower = mouseRotSensitivity;
            }
            else if(playerInput.currentControlScheme == gamepadScheme)
            {
                rotationPower = gamepadRotSensitivity;
            }
            else
            {
                rotationPower = gamepadRotSensitivity;
            }
        }
    }

    public void ScreenShake(float intensity, float duration)
    {
        if (screenShakeCoroutine != null) StopCoroutine(screenShakeCoroutine);
        screenShakeCoroutine = StartCoroutine(ScreenShakeCoroutine(intensity, duration));
    }

    IEnumerator ScreenShakeCoroutine(float intensity, float duration)
    {
        CinemachineBasicMultiChannelPerlin currentChannelPerlin = baseCamera.gameObject.activeInHierarchy ?
            baseChannelPerlin : aimingChannelPerlin;
        float shakeTimer = duration;

        do
        {
            currentChannelPerlin.m_AmplitudeGain = Mathf.Lerp(intensity, 0f, 1 - (shakeTimer/duration));
            shakeTimer -= Time.deltaTime;
            yield return null;
        } while (shakeTimer >= 0f);

        currentChannelPerlin.m_AmplitudeGain = 0f;
    }

    private void OnControlsChanged(PlayerInput playerInput)
    {
        if (playerInput.currentControlScheme == mouseScheme && previousControlScheme != mouseScheme)
        {
            //Passage en mode clavier et souris.
            previousControlScheme = mouseScheme;
            rotationPower = mouseRotSensitivity;
        }
        else if (playerInput.currentControlScheme == gamepadScheme && previousControlScheme != gamepadScheme)
        {
            //Passage en mode manette.
            previousControlScheme = gamepadScheme;
            rotationPower = gamepadRotSensitivity;
        }
    }
}
