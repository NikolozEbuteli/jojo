using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour
{
    [Header("Loadout")]
    [SerializeField] private CharacterDefinition characterData;
    [SerializeField] private GameObject standPrefab;

    [Header("Movement")]
    [SerializeField] private float fallbackSpeed = 5f;
    [SerializeField] private Rigidbody2D rb;

    [Header("Rotation")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float rotationSpeed = 1080f;

    [Header("Animation")]
    [SerializeField] private Animator playerAnimator;

    [Header("Local camera rig prefab")]
    [SerializeField] private GameObject cameraRigPrefab;

    private GameObject cameraRigInstance;
    private PlayerCameraRig cameraRig;

    private StandController spawnedStand;

    private NetworkVariable<bool> standOut = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<Vector2> netAimDir = new NetworkVariable<Vector2>(
        Vector2.right,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private Vector2 moveInput;
    private Vector2 knockbackVelocity;

    private float actionSlowMultiplier = 1f;
    private float actionSlowTimer = 0f;
    private bool barrageHeld;

    public bool HasStandOut => standOut.Value;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (visualRoot == null)
            visualRoot = transform;

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (playerAnimator != null && characterData != null && characterData.animator != null)
            playerAnimator.runtimeAnimatorController = characterData.animator;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        if (cameraRigPrefab == null)
        {
            Debug.LogError("Camera rig prefab is missing on PlayerController.");
            return;
        }

        cameraRigInstance = Instantiate(cameraRigPrefab);
        cameraRig = cameraRigInstance.GetComponent<PlayerCameraRig>();

        if (cameraRig != null)
            cameraRig.SetTarget(transform);

        // Now it's safe to use cameraRig
        Vector2 aimDir = cameraRig.GetAimDirectionFromPlayer(transform.position);

        if (aimDir.sqrMagnitude > 0.0001f)
        {
            aimDir.Normalize();
            netAimDir.Value = aimDir;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (cameraRigInstance != null)
            Destroy(cameraRigInstance);
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (Input.GetKeyDown(KeyCode.Q))
            ToggleStandServerRpc();

        ReadMovementInput();
        RotateTowardAim();
        HandleCombatInput();
        UpdateWalkingAnimation();
        UpdateActionSlow();
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        float speed = characterData != null ? characterData.speed : fallbackSpeed;
        float moveMultiplier = GetCurrentMoveMultiplier();

        Vector2 velocity = moveInput * speed * moveMultiplier;
        velocity += knockbackVelocity;

        rb.linearVelocity = velocity;

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            25f * Time.fixedDeltaTime
        );
    }

    private void ReadMovementInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(h, v);
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();
    }

    private void RotateTowardAim()
    {
        if (cameraRig == null)
            return;

        Vector2 aimDir = cameraRig.GetAimDirectionFromPlayer(transform.position);
        if (aimDir.sqrMagnitude < 0.0001f)
            return;

        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);

        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    private void HandleCombatInput()
    {
        if (!standOut.Value)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            ApplyActionSlow(characterData != null ? characterData.animator != null ? 0.72f : 0.72f : 0.72f, 0.20f);
            LightAttackServerRpc();
        }

        if (Input.GetMouseButtonDown(1))
        {
            ApplyActionSlow(0.55f, 0.35f);
            HeavyAttackServerRpc();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            barrageHeld = true;
            ApplyBarrageMoveSlow(true);
            BarrageStateServerRpc(true);
        }

        if (Input.GetKeyUp(KeyCode.B))
        {
            barrageHeld = false;
            ApplyBarrageMoveSlow(false);
            BarrageStateServerRpc(false);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            ApplyActionSlow(0.45f, 0.35f);
            Ability1ServerRpc();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            ApplyActionSlow(0.45f, 0.35f);
            Ability2ServerRpc();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ApplyActionSlow(0.45f, 0.35f);
            Ability3ServerRpc();
        }
    }

    private void UpdateWalkingAnimation()
    {
        if (playerAnimator == null)
            return;

        bool isWalking = moveInput.sqrMagnitude > 0.01f;
        playerAnimator.SetBool("isWalking", isWalking);
    }

    private void UpdateActionSlow()
    {
        if (actionSlowTimer <= 0f)
            return;

        actionSlowTimer -= Time.deltaTime;
        if (actionSlowTimer <= 0f)
        {
            actionSlowTimer = 0f;
            actionSlowMultiplier = 1f;
        }
    }

    private float GetCurrentMoveMultiplier()
    {
        float barrageMultiplier = barrageHeld ? 0.35f : 1f;
        return Mathf.Min(actionSlowMultiplier, barrageMultiplier);
    }

    private void ApplyBarrageMoveSlow(bool active)
    {
        if (active)
            actionSlowMultiplier = Mathf.Min(actionSlowMultiplier, 0.35f);
        else if (actionSlowTimer <= 0f)
            actionSlowMultiplier = 1f;
    }

    private void ApplyActionSlow(float multiplier, float duration)
    {
        actionSlowMultiplier = Mathf.Min(actionSlowMultiplier, multiplier);
        actionSlowTimer = Mathf.Max(actionSlowTimer, duration);
    }

    public void AddKnockback(Vector2 force)
    {
        knockbackVelocity += force;
    }

    [ServerRpc]
    private void LightAttackServerRpc()
    {
        if (spawnedStand is IStandCombat combat)
            combat.LightAttack();
    }

    [ServerRpc]
    private void HeavyAttackServerRpc()
    {
        if (spawnedStand is IStandCombat combat)
            combat.HeavyAttack();
    }

    [ServerRpc]
    private void BarrageStateServerRpc(bool active)
    {
        if (spawnedStand is IStandCombat combat)
            combat.SetBarrage(active);
    }

    [ServerRpc]
    private void Ability1ServerRpc()
    {
        if (spawnedStand is IStandCombat combat)
            combat.Ability1();
    }

    [ServerRpc]
    private void Ability2ServerRpc()
    {
        if (spawnedStand is IStandCombat combat)
            combat.Ability2();
    }

    [ServerRpc]
    private void Ability3ServerRpc()
    {
        if (spawnedStand is IStandCombat combat)
            combat.Ability3();
    }

    [ServerRpc]
    private void ToggleStandServerRpc()
    {
        if (spawnedStand != null)
        {
            if (spawnedStand.NetworkObject != null && spawnedStand.NetworkObject.IsSpawned)
                spawnedStand.NetworkObject.Despawn(true);

            spawnedStand = null;
            standOut.Value = false;
            return;
        }

        if (standPrefab == null)
        {
            Debug.LogError("Stand prefab is missing on PlayerController.");
            return;
        }

        Vector2 aimDir = GetAimDirectionForStand();
        Vector2 right = new Vector2(aimDir.y, -aimDir.x);

        Vector2 baseOffset = right * 0.9f + aimDir * -0.45f;
        Vector2 spawnPos = (Vector2)transform.position + baseOffset + aimDir * 0.6f;

        GameObject standGO = Instantiate(standPrefab, spawnPos, Quaternion.identity);

        NetworkObject netObj = standGO.GetComponent<NetworkObject>();
        StandController stand = standGO.GetComponent<StandController>();

        if (netObj == null || stand == null)
        {
            Debug.LogError("Stand prefab must have both NetworkObject and StandController.");
            Destroy(standGO);
            return;
        }

        stand.InitializeOwner(OwnerClientId);
        netObj.Spawn();

        spawnedStand = stand;
        standOut.Value = true;
    }

    private Vector2 GetAimDirectionForStand()
    {
        if (cameraRig == null)
            return Vector2.right;

        Vector2 aimDir = cameraRig.GetAimDirectionFromPlayer(transform.position);
        if (aimDir.sqrMagnitude < 0.0001f)
            return Vector2.right;

        return aimDir.normalized;
    }

    public Vector2 GetCurrentAimDirection()
    {
        return GetAimDirectionForStand();
    }

    private void OnDestroy()
    {
        if (IsServer && spawnedStand != null && spawnedStand.NetworkObject != null && spawnedStand.NetworkObject.IsSpawned)
            spawnedStand.NetworkObject.Despawn(true);
    }
}