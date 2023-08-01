using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAt : MonoBehaviour
{
    public bool lookAtPlayer;
    public bool lockYAxis;
    [Header("If the objet isn't the player.")]
    public Transform objectToLook;
    private Vector3 objetToLookWithoutY;
    
    private bool looking;

    private void OnEnable()
    {
        if (lookAtPlayer)
        {
            objectToLook = HeroMovements.PlayerBody;
        }
        looking = true;
    }

    private void OnDisable()
    {
        looking = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (looking)
        {
            if (lockYAxis)
            {
                objetToLookWithoutY = objectToLook.position;
                objetToLookWithoutY.y = transform.position.y;
                transform.LookAt(objetToLookWithoutY);
            }
            else
            {
                transform.LookAt(objectToLook);
            }
        }
    }

    public static void LookWithoutYAxis(Transform theOneWhoLook, Vector3 lookAtPos)
    {
        Vector3 lookWithoutY = lookAtPos;
        lookWithoutY.y = theOneWhoLook.position.y;
        theOneWhoLook.LookAt(lookWithoutY);
    }
}
