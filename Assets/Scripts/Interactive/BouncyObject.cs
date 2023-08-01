using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BouncyObject : MonoBehaviour
{
    [Header("The bouncy object.")]
    [SerializeField] private GameObject bounceBackObject;
    public float bouncePower = 8.0f;
    [SerializeField] private bool isAnEnemy;
    [Header("Do not activate bounce on dash ?")]
    [SerializeField] private bool dontBounceOnDash;
    [HideInInspector] public bool canBounce = true;
    [Header("How to bounce :")]
    [SerializeField] private UnityEvent bounceEvent;

    private void Start()
    {
        canBounce = true;

        if (dontBounceOnDash)
        {
            HeroMovements.OnDash += DisableBounce;

            HeroMovements.OnDashEnd += EnableBounce;
        }
    }

    private void OnDestroy()
    {
        if (dontBounceOnDash)
        {
            HeroMovements.OnDash -= DisableBounce;

            HeroMovements.OnDashEnd -= EnableBounce;
        }
    }

    private void EnableBounce(Transform tr)
    {
        if(enabled) canBounce = true;
    }

    private void DisableBounce(Transform tr)
    {
        if (enabled) canBounce = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player" && canBounce)
        {
            if (isAnEnemy && !HeroMovements.grounded)
            {
                other.SendMessageUpwards("Bounce", bouncePower, SendMessageOptions.DontRequireReceiver);
                bounceBackObject.SendMessage("BounceBack", SendMessageOptions.DontRequireReceiver);
                CameraManager.Instance.ScreenShake(4f, .1f);
                bounceEvent.Invoke();   
            }
            else if(!isAnEnemy)
            {
                other.SendMessageUpwards("Bounce", bouncePower, SendMessageOptions.DontRequireReceiver);
                bounceBackObject.SendMessage("BounceBack", SendMessageOptions.DontRequireReceiver);
                CameraManager.Instance.ScreenShake(4f, .1f);
                bounceEvent.Invoke();
            }
        }
    }
}
