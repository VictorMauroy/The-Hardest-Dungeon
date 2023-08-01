using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GrabbableTarget : MonoBehaviour
{
    public Vector3 slimyPositionOffset;
    private OutlineEffect.Outline outline;
    public Animator markerAnimator;
    public GrabVisibility grabVisibility;

    public Transform directionnalArrowParent; //Right then left, back+top, bottom.

    [Header("Are directionnal arrows fixed ?")]
    public bool fixedDirectionnalArrows;

    [Header("Events")]
    public UnityEvent AimingModeEnterEvent;
    public UnityEvent AimingModeExitEvent;
    public UnityEvent SelectedEvent;
    public UnityEvent UnselectedEvent;
    public UnityEvent StartTargetEvent;
    public UnityEvent EndTargetEvent;

    private void Awake()
    {
        if (!grabVisibility)
        {
            Debug.LogError("Assignation manquante d'un GrabVisibility sur cet objet.", gameObject);
        }

        if (fixedDirectionnalArrows)
        {
            if(directionnalArrowParent.TryGetComponent(out LookAt arrowLookAt))
            {
                arrowLookAt.enabled = false;
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        outline = grabVisibility.gameObject.GetComponent<OutlineEffect.Outline>();
        outline.enabled = false;
    }

    public ThrowAxis ChangeAxisVisualisation(ThrowAxis newThrowAxis)
    {
        if (!directionnalArrowParent) Debug.LogError("There isn't any directionnalArrowParent on GameObject", gameObject);

        int childCount = directionnalArrowParent.childCount;

        if (childCount > 0)
        {
            foreach (Transform childArrow in directionnalArrowParent)
            {
                childArrow.gameObject.SetActive(false);
            }

            switch (newThrowAxis)
            {
                case ThrowAxis.Right:
                    directionnalArrowParent.GetChild(0).gameObject.SetActive(true);
                    return ThrowAxis.Right;

                case ThrowAxis.Left:
                    directionnalArrowParent.GetChild(1).gameObject.SetActive(true);
                    return ThrowAxis.Left;

                case ThrowAxis.Backward:
                    if(childCount > 2)
                    {
                        directionnalArrowParent.GetChild(2).gameObject.SetActive(true);
                        return ThrowAxis.Backward;
                    }
                    else
                    {
                        goto case ThrowAxis.Right;
                    }

                case ThrowAxis.Bottom:
                    if(childCount > 3)
                    {
                        directionnalArrowParent.GetChild(3).gameObject.SetActive(true);
                        return ThrowAxis.Bottom;
                    }
                    else
                    {
                        goto case ThrowAxis.Right;
                    }
            }
        }

        return ThrowAxis.Right;
    }

    public void EnterPossibleTargetMode()
    {
        StartTargetEvent.Invoke();
    }

    public void ExitPossibleTargetMode()
    {
        EndTargetEvent.Invoke();
    }

    public void Selected()
    {
        markerAnimator.SetBool("Selected", true);
        SelectedEvent.Invoke();
    }

    public void Unselected()
    {
        markerAnimator.SetBool("Selected", false);
        UnselectedEvent.Invoke();
    }

    public bool GrabbedAndThrown(ThrowAxis throwDirection)
    {
        //Réaliser des projections (raycast) pour savoir si l'entité peut être lancée dans une telle direction.
        //Si oui, on retourne vrai, faux sinon.

        Transform parent = transform.parent;

        if (parent.TryGetComponent(out Slimoeil slimoeil))
        {
            return slimoeil.ThrownByPlayer(this, throwDirection);
        } 
        else if(parent.TryGetComponent(out Seeker seeker))
        {
            return seeker.ThrownByPlayer(this, throwDirection);
        }
        else if (parent.TryGetComponent(out FlyingEnemy flyingEnemy))
        {
            return flyingEnemy.ThrownByPlayer(this, throwDirection);
        } 
        else if (parent.TryGetComponent(out BasicEnemy basicEnemy))
        {
            return basicEnemy.ThrownByPlayer(this, throwDirection);
        }

        return true;
    }

    public void EnterTargetSelectionMode()
    {
        AimingModeEnterEvent.Invoke();
        SendMessage("Grabbed", true, SendMessageOptions.DontRequireReceiver);
        transform.parent.SendMessage("Grabbed", true, SendMessageOptions.DontRequireReceiver);
        outline.enabled = true;
        grabVisibility.playerIsAiming = true;
        grabVisibility.isVisible = true;
    }

    public void ExitTargetSelectionMode()
    {
        outline.enabled = false;
        grabVisibility.playerIsAiming = true;
        grabVisibility.isInViewCone = false;
        AimingModeExitEvent.Invoke();
        SendMessage("Grabbed", false, SendMessageOptions.DontRequireReceiver);
        transform.parent.SendMessage("Grabbed", false, SendMessageOptions.DontRequireReceiver);
        grabVisibility.isVisible = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position + slimyPositionOffset, 0.2f);
    }
}
