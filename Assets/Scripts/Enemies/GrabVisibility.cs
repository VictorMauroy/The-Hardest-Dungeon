using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void GrabbableVisibleEvent(GrabbableTarget grabbableTarget);
public delegate void GrabbableInvisibleEvent(GrabbableTarget grabbableTarget);
public delegate void EnterInViewConeEvent(GrabbableTarget grabbableTarget);
public delegate void ExitViewConeEvent(GrabbableTarget grabbableTarget);

public class GrabVisibility : MonoBehaviour
{
    public static GrabbableVisibleEvent OnGrabbableVisible; //Quand l'objet/entité peut être ciblé(e).
    public static GrabbableInvisibleEvent OnGrabbableInvisible; //Quand l'objet/entité ne peut plus être ciblé(e).
    public static EnterInViewConeEvent OnGrabbableViewConeEnter;
    public static ExitViewConeEvent OnGrabbableViewConeExit;

    [HideInInspector] public bool isVisible;
    private Transform mainCameraTr;
    [HideInInspector] public bool isInViewCone;
    private const float viewConeAngle = 30f;
    public GrabbableTarget grabbableTarget;
    [HideInInspector] public bool playerIsAiming;

    private void OnBecameVisible() //Require a Renderer Component.
    {
        if (OnGrabbableVisible != null) OnGrabbableVisible(grabbableTarget);
    }

    private void OnBecameInvisible()
    {
        if (OnGrabbableInvisible != null) OnGrabbableInvisible(grabbableTarget);
        isVisible = false;
    }

    private void Awake()
    {
        if (!grabbableTarget)
        {
            Debug.LogError("Assignation manquante d'un GrabbableTarget sur cet objet.", gameObject);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        mainCameraTr = Camera.main.transform;
    }

    void Update()
    {
        if (isVisible && playerIsAiming)
        {
            bool viewConeResult = IsInViewCone();

            //Repérer la frame lorsque la cible entre dans le champ de vision.
            if (!isInViewCone && viewConeResult)
            {
                if (OnGrabbableViewConeEnter != null) OnGrabbableViewConeEnter(grabbableTarget);
            }

            //Déterminer la frame lorsque la cible quitte le champ de vision
            if (isInViewCone && !viewConeResult)
            {
                if (OnGrabbableViewConeExit != null) OnGrabbableViewConeExit(grabbableTarget);
            }
            isInViewCone = viewConeResult;
        }
    }

    public bool IsInViewCone()
    {
        return Vector3.Angle(
            mainCameraTr.transform.forward,
            transform.position - mainCameraTr.transform.position
        ) < viewConeAngle;
    }

    private void OnDrawGizmos()
    {
        if (playerIsAiming)
        {
            Gizmos.color = isInViewCone ? Color.green : Color.red;
            Gizmos.DrawCube(transform.position + Vector3.up, Vector3.one * 0.3f);
        }
    }
}
