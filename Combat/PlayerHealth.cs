using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private CharacterDefinition characterData;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerController controller;

    public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (controller == null)
            controller = GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            float maxHealth = characterData != null ? characterData.maxHealth : 100f;
            CurrentHealth.Value = maxHealth;
        }
    }

    public void ApplyDamage(float amount, Vector2 hitDirection, float knockbackForce)
    {
        if (!IsServer)
            return;

        amount = Mathf.Max(0f, amount);
        CurrentHealth.Value = Mathf.Max(0f, CurrentHealth.Value - amount);

        if (knockbackForce > 0f)
        {
            Vector2 force = hitDirection.sqrMagnitude < 0.0001f
                ? Vector2.zero
                : hitDirection.normalized * knockbackForce;

            KnockbackClientRpc(force);
        }

        if (CurrentHealth.Value <= 0f)
        {
            Debug.Log($"{name} is down.");
        }
    }

    [ClientRpc]
    private void KnockbackClientRpc(Vector2 force)
    {
        if (!IsOwner || controller == null)
            return;

        controller.AddKnockback(force);
    }
}