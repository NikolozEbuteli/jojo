using System.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public abstract class StandController : NetworkBehaviour, IDamageable
{
    private static readonly List<StandController> allStands = new List<StandController>();

    [Header("Data")]
    [SerializeField] protected StandDefinition standData;

    [Header("Visuals")]
    [SerializeField] protected Transform visualRoot;
    [SerializeField] protected SpriteRenderer[] spriteRenderers;
    [SerializeField] protected Animator standAnimator;

    [Header("Offsets")]
    [SerializeField] protected Transform leftHandOffset;
    [SerializeField] protected Transform rightHandOffset;

    private NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool hasPendingOwner;
    private ulong pendingOwnerClientId;
    private bool appearancePlayed;

    public ulong OwnerClientIdValue => ownerClientId.Value;

    public virtual bool IsAttacking => false;
    public virtual bool IsBarrageActive => false;

    public void InitializeOwner(ulong clientId)
    {
        pendingOwnerClientId = clientId;
        hasPendingOwner = true;
    }

    protected virtual void Reset()
    {
        standAnimator = GetComponentInChildren<Animator>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        allStands.Add(this);

        if (IsServer && hasPendingOwner)
            ownerClientId.Value = pendingOwnerClientId;

        if (standAnimator == null)
            standAnimator = GetComponentInChildren<Animator>();

        if (visualRoot == null)
            visualRoot = transform;

        if (standData != null && standAnimator != null && standData.animator != null)
            standAnimator.runtimeAnimatorController = standData.animator;

        // NEW: Position the stand at the owner before appearance animation
        if (IsServer)
        {
            Transform ownerTransform = GetOwnerTransform();
            if (ownerTransform != null && standData != null)
            {
                Vector2 aimDir = GetOwnerAimDirection();
                if (aimDir.sqrMagnitude < 0.0001f)
                    aimDir = Vector2.right;
                else
                    aimDir.Normalize();

                Vector2 right = new Vector2(aimDir.y, -aimDir.x);
                Vector2 targetPos =
                    (Vector2)ownerTransform.position +
                    right * standData.followOffset.x +
                    aimDir * standData.followOffset.y;

                transform.position = targetPos;
            }
        }

        if (!appearancePlayed)
        {
            appearancePlayed = true;
            StartCoroutine(SummonAppearanceRoutine());
        }
    }
    public override void OnNetworkDespawn()
    {
        allStands.Remove(this);
        StopAllCoroutines();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ServerTick();
    }

    protected virtual void ServerTick()
    {
        Transform ownerTransform = GetOwnerTransformPublic();
        if (ownerTransform == null)
            return;

        FollowOwner(ownerTransform);
    }

    public Transform GetOwnerTransformPublic()
    {
        return GetOwnerTransform();
    }

    protected Transform GetOwnerTransform()
    {
        if (NetworkManager.Singleton == null)
            return null;

        ulong clientId = ownerClientId.Value;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return null;

        if (client.PlayerObject == null)
            return null;

        return client.PlayerObject.transform;
    }

    protected PlayerController GetOwnerPlayerController()
    {
        Transform ownerTransform = GetOwnerTransform();
        if (ownerTransform == null)
            return null;

        return ownerTransform.GetComponent<PlayerController>();
    }

    protected PlayerHealth GetOwnerPlayerHealth()
    {
        Transform ownerTransform = GetOwnerTransform();
        if (ownerTransform == null)
            return null;

        return ownerTransform.GetComponent<PlayerHealth>();
    }

    protected StandController GetClosestEnemyStand(float range)
    {
        StandController closest = null;
        float bestDist = range;

        for (int i = 0; i < allStands.Count; i++)
        {
            StandController other = allStands[i];

            if (other == null || other == this)
                continue;

            if (!other.IsSpawned)
                continue;

            if (other.OwnerClientIdValue == OwnerClientIdValue)
                continue;

            float dist = Vector2.Distance(transform.position, other.transform.position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                closest = other;
            }
        }

        return closest;
    }

    protected virtual void FollowOwner(Transform ownerTransform)
    {
        if (standData == null)
            return;

        PlayerController ownerPlayer = ownerTransform.GetComponent<PlayerController>();

        // 🔥 IMPORTANT: This must now be NETWORKED aim direction
        Vector2 aimDir = ownerPlayer != null ? ownerPlayer.GetCurrentAimDirection() : Vector2.right;

        if (aimDir.sqrMagnitude < 0.0001f)
            aimDir = Vector2.right;
        else
            aimDir.Normalize();

        Vector2 right = new Vector2(aimDir.y, -aimDir.x);

        Vector2 targetPos =
            (Vector2)ownerTransform.position +
            right * standData.followOffset.x +
            aimDir * standData.followOffset.y;

        // ✅ POSITION on ROOT (networked)
        transform.position = Vector2.MoveTowards(
            transform.position,
            targetPos,
            standData.followSpeed * Time.deltaTime
        );

        // ✅ ROTATION on VISUAL ROOT
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);

        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            targetRot,
            standData.rotationSpeed * Time.deltaTime
        );
    }

    protected Vector2 GetOwnerAimDirection()
    {
        PlayerController ownerPlayer = GetOwnerPlayerController();
        if (ownerPlayer == null)
            return Vector2.right;

        Vector2 aimDir = ownerPlayer.GetCurrentAimDirection();
        if (aimDir.sqrMagnitude < 0.0001f)
            return Vector2.right;

        return aimDir.normalized;
    }

    public virtual float GetClashPower()
    {
        if (standData == null)
            return 1f;

        float strength = standData.strength;
        float durability = standData.durability;
        float speed = standData.followSpeed;

        return (strength * 0.45f) + (durability * 0.35f) + (speed * 0.20f);
    }

    public virtual void BreakBarrage()
    {
    }

    public virtual void ApplyDamage(float amount, Vector2 hitDirection, float knockbackForce)
    {
        if (!IsServer)
            return;

        PlayerHealth health = GetOwnerPlayerHealth();
        if (health != null)
            health.ApplyDamage(amount, hitDirection, knockbackForce);
    }

    protected IEnumerator SummonAppearanceRoutine()
    {
        if (standData == null || visualRoot == null)
            yield break;

        Vector3 startLocalPos = standData.summonStartLocalOffset;
        Vector3 endLocalPos = Vector3.zero;

        SetAlpha(standData.summonStartAlpha);
        visualRoot.localPosition = startLocalPos;

        float elapsed = 0f;
        float duration = Mathf.Max(standData.summonBlendTime, 0.05f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            visualRoot.localPosition = Vector3.Lerp(startLocalPos, endLocalPos, eased);
            SetAlpha(Mathf.Lerp(standData.summonStartAlpha, 1f, eased));

            yield return null;
        }

        visualRoot.localPosition = endLocalPos;
        SetAlpha(1f);
    }

    protected void SetAlpha(float alpha)
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            Color c = spriteRenderers[i].color;
            c.a = alpha;
            spriteRenderers[i].color = c;
        }
    }
}