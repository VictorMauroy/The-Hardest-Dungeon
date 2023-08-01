using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanSee : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform eyes; //Transform déterminant la direction dans laquelle regarde l'entité.
    
    [SerializeField] private float halfAngle; //Moitié de l'angle. 60 degrés va dire qu'il y aura 60° vers la droite et la gauche en détection.
    [SerializeField] private float maxDist; //Distance max à laquelle nous pouvons détecter la cible
    [SerializeField] private LayerMask viewObstacleMask; //Masque décrivant ce qu'est un obstacle à la détection de la cible.
    
    [HideInInspector] public bool isSeingTarget; //Vrai si la cible est visible pour l'entité & assez proche
    [HideInInspector] public float distToPlayer;
    [HideInInspector] public float checkFrequency;
    [HideInInspector] public bool look;

    //Suivi d'entité.
    [HideInInspector] public Vector3 lastSeenTargetPosition;
    [HideInInspector] public float targetLostTime;

    // Start is called before the first frame update
    void Start()
    {
        look = true;
        if (checkFrequency <= 0f) checkFrequency = 0.5f;

        StartCoroutine(DistanceToPlayerCheck());
    }

    // Update is called once per frame
    void Update()
    {
        //Débug une ligne : Rouge si la cible n'est pas dans l'angle de vue devant la cible. 
        //Bleu si elle y est mais trop éloignée. Vert si tout est réuni pour la détection.
        Debug.DrawLine(
            eyes.position,
            target.position,
            IsInViewCone() ?
                (isNotCovered() ? Color.green : Color.blue) :
                Color.red
        );

        //Si la cible est visible, assez proche et dans un certain angle de vue devant l'entité, alors elle est détectée.
        if (IsInViewCone() && isNotCovered())
        {
            //Activation uniquement lors du passage en true.
            if (!isSeingTarget) SendMessage("TargetDetected", SendMessageOptions.DontRequireReceiver);
            targetLostTime = 0f;
            lastSeenTargetPosition = target.position;
            isSeingTarget = true;
        }
        else
        {
            isSeingTarget = false;
            targetLostTime += Time.deltaTime;
        }
    }

    /// <summary>
    /// Fonction déterminant si la cible est assez proche et visible par l'entité (aucun obstacle entre eux).
    /// </summary>
    /// <returns>Vrai si le joueur est visible, faux sinon</returns>
    bool isNotCovered()
    {
        if (distToPlayer < maxDist)
        {
            return !Physics.Raycast(
                eyes.position,
                target.position - eyes.position,
                distToPlayer,
                viewObstacleMask
            );
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Fonction déterminant si la cible est dans un certain angle de vue devant l'entité.
    /// </summary>
    /// <returns>Vrai si la cible est à l'intérieur de l'angle de vue, faux sinon.</returns>
    bool IsInViewCone()
    {
        return Vector3.Angle(
            eyes.forward,
            target.position - eyes.position
        ) < halfAngle;
    }

    /// <summary>
    /// Verifier en continu la distance entre l'entite et sa cible. Utilisation d'une frequence afin d'optimiser l'utilisation du calcul.
    /// </summary>
    /// <param name="checkFrequency">A quelle frequence verifier la distance.</param>
    /// <returns></returns>
    IEnumerator DistanceToPlayerCheck()
    {
        do
        {
            distToPlayer = (target.position - transform.position).magnitude;
            
            yield return new WaitForSeconds(checkFrequency);
        } while (look);
    }
}
