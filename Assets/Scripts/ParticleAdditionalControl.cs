using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleAdditionalControl : MonoBehaviour
{
    public void RotateLikeTransform(Transform targetTransform)
    {
        transform.rotation = targetTransform.rotation;   
    }

    public void RotateLikeTransformLocalRot(Transform targetTransform)
    {
        transform.localRotation = targetTransform.localRotation;
    }
}
