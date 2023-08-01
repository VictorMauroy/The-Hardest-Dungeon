using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MonsterExpressions
{
    Surprise,
    Question
}

public class ExpressionsScript : MonoBehaviour
{
    [SerializeField] private ParticleSystem expressionsParticlesPrefab;
    [SerializeField] private float expressionOffset;
    [SerializeField] private Material exclamMat;
    [SerializeField] private Material interogMat;

    public void MakeExpression(MonsterExpressions expressionToShow)
    {
        ParticleSystem newExpressionParticles = Instantiate(
            expressionsParticlesPrefab, 
            transform.position + Vector3.up * expressionOffset, 
            Quaternion.identity,
            transform
            );

        switch (expressionToShow)
        {
            case MonsterExpressions.Surprise:
                newExpressionParticles.GetComponent<Renderer>().material = exclamMat;
                break;
            
            case MonsterExpressions.Question:
                newExpressionParticles.GetComponent<Renderer>().material = interogMat;
                break;
        }

        Destroy(newExpressionParticles, 1.1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.up * expressionOffset, Vector3.one * 0.1f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * expressionOffset);
    }
}
