using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public interface IStandCombat
{
    void LightAttack();
    void HeavyAttack();
    void SetBarrage(bool active);
    void Ability1();
    void Ability2();
    void Ability3();
}

public class StarPlatinumStand : StandController, IStandCombat
{
    [Header("Hitboxes")]
    [SerializeField] private CombatHitbox leftPunchHitbox;
    [SerializeField] private CombatHitbox rightPunchHitbox;
    [SerializeField] private CombatHitbox barrageHitbox;

    private NetworkVariable<bool> barrageActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Coroutine barrageVfxRoutine;
    private readonly List<GameObject> spawnedBarrageHands = new List<GameObject>();

    private bool nextLeftPunch = true;
    private bool isAttackLocked;
    private float nextClashResolveTime;

    public override bool IsAttacking => isAttackLocked || barrageActive.Value;
    public override bool IsBarrageActive => barrageActive.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        barrageActive.OnValueChanged += OnBarrageChanged;
    }

    public override void OnNetworkDespawn()
    {
        barrageActive.OnValueChanged -= OnBarrageChanged;
        StopBarrageVfx();
        base.OnNetworkDespawn();
    }

    protected override void ServerTick()
    {
        Transform ownerTransform = GetOwnerTransformPublic();
        if (ownerTransform == null)
            return;

        float range = standData != null ? standData.range : 3f;
        StandController enemyStand = GetClosestEnemyStand(range);

        if (barrageActive.Value)
        {
            if (enemyStand != null)
            {
                bool enemyBarrage = enemyStand.IsBarrageActive;
                bool enemyAttacking = enemyStand.IsAttacking;

                if (enemyBarrage)
                {
                    HandleBarrageClash(enemyStand, ownerTransform);
                    return;
                }

                if (enemyAttacking || IsAttacking)
                {
                    FaceAndApproachCombatTarget(ownerTransform, enemyStand.transform);
                    UpdateBarrageDamageContext(enemyStand, clashActive: false);
                    return;
                }
            }

            FaceAndApproachOwnerForward(ownerTransform);
            UpdateBarrageDamageContext(null, clashActive: false);
            return;
        }

        if (IsAttacking)
        {
            if (enemyStand != null && Vector2.Distance(transform.position, enemyStand.transform.position) <= range)
            {
                FaceAndApproachCombatTarget(ownerTransform, enemyStand.transform);
                return;
            }

            FaceAndApproachOwnerForward(ownerTransform);
            return;
        }

        FollowOwner(ownerTransform);
    }

    public override float GetClashPower()
    {
        if (standData == null)
            return 1f;

        float strength = standData.strength;
        float durability = standData.durability;
        float combatSpeed = standData.followSpeed;
        float randomness = Random.Range(-0.35f, 0.35f);

        return (strength * 0.45f) + (durability * 0.35f) + (combatSpeed * 0.20f) + randomness;
    }

    public override void BreakBarrage()
    {
        if (!IsServer)
            return;

        barrageActive.Value = false;

        if (standAnimator != null)
            standAnimator.SetBool("barrage", false);

        if (barrageHitbox != null)
            barrageHitbox.End();

        StopBarrageVfx();
    }

    public void LightAttack()
    {
        if (!IsServer || isAttackLocked || barrageActive.Value)
            return;

        StartCoroutine(LightPunchRoutine());
    }

    public void HeavyAttack()
    {
        if (!IsServer || isAttackLocked || barrageActive.Value)
            return;

        StartCoroutine(HeavyPunchRoutine());
    }

    public void SetBarrage(bool active)
    {
        if (!IsServer)
            return;

        if (active == barrageActive.Value)
        {
            if (active)
                UpdateBarrageDamageContext(null, clashActive: false);

            return;
        }

        barrageActive.Value = active;

        if (standAnimator != null)
            standAnimator.SetBool("barrage", active);

        if (active)
        {
            Transform ownerTransform = GetOwnerTransformPublic();
            Transform ownerRoot = ownerTransform != null ? ownerTransform.root : null;

            if (barrageHitbox != null)
            {
                barrageHitbox.BeginContinuous(
                    standData != null ? standData.barrageDamage : 2f,
                    standData != null ? standData.barrageKnockback : 1f,
                    standData != null ? standData.barrageTickInterval : 0.08f,
                    transform.root,
                    ownerRoot
                );
            }

            StartBarrageVfx();
        }
        else
        {
            if (barrageHitbox != null)
                barrageHitbox.End();

            StopBarrageVfx();
        }
    }

    public void Ability1() { }
    public void Ability2() { }
    public void Ability3() { }

    private IEnumerator LightPunchRoutine()
    {
        isAttackLocked = true;

        bool useLeft = nextLeftPunch;
        nextLeftPunch = !nextLeftPunch;

        if (standAnimator != null)
            standAnimator.SetTrigger(useLeft ? "leftPunch" : "rightPunch");

        yield return new WaitForSeconds(standData != null ? standData.lightWindup : 0.10f);

        CombatHitbox hitbox = useLeft ? leftPunchHitbox : rightPunchHitbox;
        Transform ownerTransform = GetOwnerTransformPublic();
        Transform ignoreRoot = ownerTransform != null ? ownerTransform.root : null;

        if (hitbox != null)
        {
            hitbox.BeginSingle(
                standData != null ? standData.lightDamage : 8f,
                standData != null ? standData.lightKnockback : 2.5f,
                transform.root,
                ignoreRoot
            );
        }

        yield return new WaitForSeconds(standData != null ? standData.lightActiveTime : 0.12f);

        if (hitbox != null)
            hitbox.End();

        yield return new WaitForSeconds(standData != null ? standData.lightCooldown : 0.25f);

        isAttackLocked = false;
    }

    private IEnumerator HeavyPunchRoutine()
    {
        isAttackLocked = true;

        if (standAnimator != null)
            standAnimator.SetTrigger("rightPunch");

        yield return new WaitForSeconds(standData != null ? standData.heavyWindup : 0.16f);

        Transform ownerTransform = GetOwnerTransformPublic();
        Transform ignoreRoot = ownerTransform != null ? ownerTransform.root : null;

        if (rightPunchHitbox != null)
        {
            rightPunchHitbox.BeginSingle(
                standData != null ? standData.heavyDamage : 14f,
                standData != null ? standData.heavyKnockback : 4f,
                transform.root,
                ignoreRoot
            );
        }

        yield return new WaitForSeconds(standData != null ? standData.heavyActiveTime : 0.16f);

        if (rightPunchHitbox != null)
            rightPunchHitbox.End();

        yield return new WaitForSeconds(standData != null ? standData.heavyCooldown : 0.42f);

        isAttackLocked = false;
    }

    private void HandleBarrageClash(StandController enemyStand, Transform ownerTransform)
    {
        if (enemyStand == null)
            return;

        FaceAndApproachCombatTarget(ownerTransform, enemyStand.transform);
        UpdateBarrageDamageContext(enemyStand, clashActive: true);

        if (Time.time < nextClashResolveTime)
            return;

        nextClashResolveTime = Time.time + 0.18f;

        float myPower = GetClashPower();
        float enemyPower = enemyStand.GetClashPower();

        if (myPower > enemyPower + 0.25f)
        {
            enemyStand.BreakBarrage();
        }
        else if (enemyPower > myPower + 0.25f)
        {
            BreakBarrage();
        }
    }

    private void UpdateBarrageDamageContext(StandController enemyStand, bool clashActive)
    {
        if (barrageHitbox == null)
            return;

        Transform ownerTransform = GetOwnerTransformPublic();
        Transform ownerRoot = ownerTransform != null ? ownerTransform.root : null;

        if (clashActive && enemyStand != null)
        {
            Transform enemyOwnerTransform = enemyStand.GetOwnerTransformPublic();
            Transform enemyOwnerRoot = enemyOwnerTransform != null ? enemyOwnerTransform.root : null;

            barrageHitbox.SetContext(
                transform.root,
                ownerRoot,
                enemyStand.transform.root,
                enemyOwnerRoot
            );
        }
        else
        {
            barrageHitbox.SetContext(transform.root, ownerRoot);
        }
    }

    private void FaceAndApproachCombatTarget(Transform ownerTransform, Transform target)
    {
        if (target == null)
            return;

        Vector2 dir = (target.position - transform.position);
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        else
            dir.Normalize();

        float holdDistance = standData != null ? Mathf.Max(0.55f, standData.range * 0.45f) : 0.8f;
        Vector2 targetPos = (Vector2)target.position - dir * holdDistance;

        transform.position = Vector2.MoveTowards(
            transform.position,
            targetPos,
            (standData != null ? standData.followSpeed : 7f) * 1.2f * Time.deltaTime
        );

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);

        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            targetRot,
            (standData != null ? standData.rotationSpeed : 720f) * 1.2f * Time.deltaTime
        );
    }

    private void FaceAndApproachOwnerForward(Transform ownerTransform)
    {
        Vector2 aimDir = GetOwnerAimDirection();
        if (aimDir.sqrMagnitude < 0.0001f)
            aimDir = Vector2.right;
        else
            aimDir.Normalize();

        Vector2 targetPos = (Vector2)ownerTransform.position + aimDir * 1.0f;

        transform.position = Vector2.MoveTowards(
            transform.position,
            targetPos,
            (standData != null ? standData.followSpeed : 7f) * Time.deltaTime
        );

        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);

        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            targetRot,
            (standData != null ? standData.rotationSpeed : 720f) * Time.deltaTime
        );
    }

    private void OnBarrageChanged(bool previous, bool current)
    {
        if (current)
            StartBarrageVfx();
        else
            StopBarrageVfx();
    }

    private void StartBarrageVfx()
    {
        if (standData == null || standData.barrageHandPrefab == null)
            return;

        if (barrageVfxRoutine != null)
            StopCoroutine(barrageVfxRoutine);

        barrageVfxRoutine = StartCoroutine(BarrageVfxRoutine());
    }

    private void StopBarrageVfx()
    {
        if (barrageVfxRoutine != null)
        {
            StopCoroutine(barrageVfxRoutine);
            barrageVfxRoutine = null;
        }

        for (int i = 0; i < spawnedBarrageHands.Count; i++)
        {
            if (spawnedBarrageHands[i] != null)
                Destroy(spawnedBarrageHands[i]);
        }

        spawnedBarrageHands.Clear();
    }

    private IEnumerator BarrageVfxRoutine()
    {
        float interval = standData != null ? standData.barrageHandSpawnInterval : 0.04f;

        while (barrageActive.Value)
        {
            SpawnBarrageHand();
            yield return new WaitForSeconds(interval);
        }

        barrageVfxRoutine = null;
    }

    private void SpawnBarrageHand()
    {
        if (standData == null || standData.barrageHandPrefab == null)
            return;

        Transform source = Random.value < 0.5f ? leftHandOffset : rightHandOffset;
        if (source == null)
            source = visualRoot != null ? visualRoot : transform;

        Vector2 spread = standData.barrageHandSpread;
        Vector3 offset = new Vector3(
            Random.Range(-spread.x, spread.x),
            Random.Range(-spread.y, spread.y),
            0f
        );

        Vector3 spawnPos = source.position + offset;

        Vector2 forward = GetOwnerAimDirection();
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;

        float randomTilt = Random.Range(-10f, 10f);
        Quaternion rot = Quaternion.Euler(0f, 0f, angle + randomTilt);

        GameObject hand = Instantiate(standData.barrageHandPrefab, spawnPos, rot);
        spawnedBarrageHands.Add(hand);

        BarrageHandVfx vfx = hand.GetComponent<BarrageHandVfx>();
        if (vfx != null)
        {
            float speed = Random.Range(standData.barrageHandMinSpeed, standData.barrageHandMaxSpeed);
            float alpha = Random.Range(standData.barrageHandMinAlpha, standData.barrageHandMaxAlpha);

            vfx.Initialize(forward, speed, standData.barrageHandLifetime, alpha);
        }
    }
}