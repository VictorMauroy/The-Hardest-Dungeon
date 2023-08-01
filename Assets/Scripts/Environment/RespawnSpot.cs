using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnSpot : MonoBehaviour
{
    [SerializeField] private Transform respawnLocation;

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            LevelManager.respawnPosition = respawnLocation.position;
            LevelManager.respawnRotation = respawnLocation.rotation;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(respawnLocation.position, Vector3.one * 2f);
        
        Gizmos.DrawLine(respawnLocation.position, respawnLocation.position + respawnLocation.forward);
        Gizmos.DrawSphere(respawnLocation.position + respawnLocation.forward, 0.3f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(respawnLocation.position, respawnLocation.position + respawnLocation.right);
        Gizmos.DrawSphere(respawnLocation.position + respawnLocation.right, 0.3f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(respawnLocation.position, respawnLocation.position + respawnLocation.up);
        Gizmos.DrawSphere(respawnLocation.position + respawnLocation.up, 0.3f);
    }
}
