using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void FallDeathEvent(DeathReason deathReason);

public class DeathTrigger : MonoBehaviour
{
    public static FallDeathEvent OnFallDeath;

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            if (OnFallDeath != null) OnFallDeath(DeathReason.Fall);
        }
    }

}
