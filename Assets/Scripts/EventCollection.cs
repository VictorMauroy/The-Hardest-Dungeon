using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EventCollection : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent OnDashEvent;
    public UnityEvent OnJumpEvent;
    public UnityEvent OnDoubleJumpEvent;
    public UnityEvent OnLandingEvent;

    [Header("Prefabs")]
    [SerializeField] private ParticleSystem enemyImpactParticles;
    [SerializeField] private ParticleSystem enemyDamageParticles;
    [SerializeField] private ParticleSystem enemySmokeParticles;
    [SerializeField] private ParticleSystem poufParticles;

    // Start is called before the first frame update
    void Start()
    {
        HeroMovements.OnDash += DoDashEvent;
        HeroMovements.OnJump += DoJumpEvent;
        HeroMovements.OnDoubleJump += DoDoubleJumpEvent;
        HeroMovements.OnLanding += DoLandingEvent;

        BasicEnemy.OnEnemyImpact += DoEnemyImpactEvent;
        BasicEnemy.OnPouf += DoPoufEvent;
        BasicEnemy.OnDamagesReceived += DoEnemyDamageReceivedEvent;

        FlyingEnemy.OnEnemyImpact += DoEnemyImpactEvent;
        FlyingEnemy.OnPouf += DoPoufEvent;
        FlyingEnemy.OnDamagesReceived += DoEnemyDamageReceivedEvent;
        
        Slimoeil.OnEnemyImpact += DoEnemyImpactEvent;
        Slimoeil.OnPouf += DoPoufEvent;
        Slimoeil.OnDamagesReceived += DoEnemyDamageReceivedEvent;

        Seeker.OnEnemyImpact += DoEnemyImpactEvent;
        Seeker.OnPouf += DoPoufEvent;
        Seeker.OnDamagesReceived += DoEnemyDamageReceivedEvent;
    }

    void OnDestroy()
    {
        HeroMovements.OnDash -= DoDashEvent;
        HeroMovements.OnJump -= DoJumpEvent;
        HeroMovements.OnDoubleJump -= DoDoubleJumpEvent;
        HeroMovements.OnLanding -= DoLandingEvent;

        BasicEnemy.OnEnemyImpact -= DoEnemyImpactEvent;
        BasicEnemy.OnPouf -= DoPoufEvent;
        BasicEnemy.OnDamagesReceived -= DoEnemyDamageReceivedEvent;

        FlyingEnemy.OnEnemyImpact -= DoEnemyImpactEvent;
        FlyingEnemy.OnPouf -= DoPoufEvent;
        FlyingEnemy.OnDamagesReceived -= DoEnemyDamageReceivedEvent;

        Slimoeil.OnEnemyImpact -= DoEnemyImpactEvent;
        Slimoeil.OnPouf -= DoPoufEvent;
        Slimoeil.OnDamagesReceived -= DoEnemyDamageReceivedEvent;

        Seeker.OnEnemyImpact -= DoEnemyImpactEvent;
        Seeker.OnPouf -= DoPoufEvent;
        Seeker.OnDamagesReceived -= DoEnemyDamageReceivedEvent;
    }

    private void DoDashEvent(Transform heroPosition)
    {
        OnDashEvent.Invoke();
        //ParticleSystem dashParticles = Instantiate(dashStartParticles, heroPosition.position, Quaternion.identity);
        //dashParticles.Play();
        //Destroy(dashParticles.gameObject, 0.8f);
    }

    private void DoJumpEvent(Transform heroPosition)
    {
        OnJumpEvent.Invoke();
    }

    private void DoDoubleJumpEvent(Transform heroPosition)
    {
        OnDoubleJumpEvent.Invoke();
    }

    private void DoLandingEvent(Transform heroPosition)
    {
        OnLandingEvent.Invoke();
        //Possibilité de faire un effet différent selon le sol sur lequel on atterit.
    }

    private void DoEnemyImpactEvent(Vector3 impactPoint, bool isWallImpact)
    {
        ParticleSystem impactParticles = Instantiate(enemyImpactParticles);
        impactParticles.transform.position = impactPoint;
        if (isWallImpact)
        {
            ParticleSystem smokeParticles = Instantiate(enemySmokeParticles);
            smokeParticles.transform.position = impactPoint;
            Destroy(smokeParticles.gameObject, 2f);
        }
        Destroy(impactParticles.gameObject, 2f);
    }
    private void DoPoufEvent(Vector3 poufPosition)
    {
        ParticleSystem poufEventParticles = Instantiate(poufParticles, poufPosition, Quaternion.identity);
        Destroy(poufEventParticles, 2f);
    }

    private void DoEnemyDamageReceivedEvent(Vector3 damagesPosition)
    {
        Destroy(Instantiate(enemyDamageParticles, damagesPosition, Quaternion.identity) , 1f);
    }
}
