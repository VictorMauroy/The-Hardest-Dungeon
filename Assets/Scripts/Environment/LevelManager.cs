using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DeathReason
{
    Fall,
    EnemyAttack,
    Trap
    //Possibilité de rajouter des raisons plus précises plus tard.
}

public class LevelManager : MonoBehaviour
{
    public UIEffectsManager effectsManagerUI;
    [SerializeField] private HeroMovements heroMovements;
    
    public static Vector3 respawnPosition;
    public static Quaternion respawnRotation;
    private bool waitingForDeathRespawn;
    private float respawnDelay;

    // Start is called before the first frame update
    void Start()
    {
        UIEffectsManager.OnMaskedScene += DoMaskedSceneReaction;
        DeathTrigger.OnFallDeath += Death;

        //Valeurs par défaut : lancement de la scène.
        respawnPosition = heroMovements.transform.position;
        respawnRotation = heroMovements.transform.rotation;

        heroMovements.LockOrUnlockAllControls(true);
    }

    private void OnDestroy()
    {
        UIEffectsManager.OnMaskedScene -= DoMaskedSceneReaction;
        DeathTrigger.OnFallDeath -= Death;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Death(DeathReason deathReason)
    {
        if (waitingForDeathRespawn) return; //Sécurité : ne pas appeler si déjà en cours de respawn.
        
        waitingForDeathRespawn = true;
        heroMovements.Death();

        switch (deathReason)
        {
            case DeathReason.Fall:
                respawnDelay = 1.9f;
                effectsManagerUI.ClassicBlackFade(3f, 2f);
                break;

            case DeathReason.EnemyAttack:
                respawnDelay = 1.9f;
                effectsManagerUI.ClassicBlackFade(3f, 2f);
                break;

            case DeathReason.Trap:
                respawnDelay = 1.9f;
                effectsManagerUI.ClassicBlackFade(3f, 2f);
                break;
        }

    }

    public void DoMaskedSceneReaction()
    {
        if (waitingForDeathRespawn)
        {
            heroMovements.Respawn(respawnPosition, respawnRotation, respawnDelay);
            waitingForDeathRespawn = false;
        }
    }
}
