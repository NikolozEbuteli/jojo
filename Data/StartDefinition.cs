using UnityEngine;

[CreateAssetMenu(menuName = "JoJo/Stand Definition", fileName = "StandDefinition")]
public class StandDefinition : ScriptableObject
{
    [Header("Identity")]
    public string standName = "Stand";

    [Header("Stats")]
    [Min(1f)] public float strength = 10f;
    [Min(1f)] public float durability = 10f;
    [Min(1f)] public float range = 3f;

    [Header("Visuals")]
    public RuntimeAnimatorController animator;

    [Header("Follow")]
    public Vector2 followOffset = new Vector2(0.9f, -0.45f);
    [Min(0.1f)] public float followSpeed = 7f;
    [Min(0.1f)] public float rotationSpeed = 720f;

    [Header("Summon Effect")]
    public Vector3 summonStartLocalOffset = new Vector3(0f, 0.25f, 0f);
    [Range(0f, 1f)] public float summonStartAlpha = 0.15f;
    [Min(0.05f)] public float summonBlendTime = 0.25f;

    [Header("Attack Damage")]
    [Min(0f)] public float lightDamage = 8f;
    [Min(0f)] public float heavyDamage = 14f;
    [Min(0f)] public float barrageDamage = 2f;

    [Header("Attack Knockback")]
    [Min(0f)] public float lightKnockback = 2.5f;
    [Min(0f)] public float heavyKnockback = 4f;
    [Min(0f)] public float barrageKnockback = 1f;

    [Header("Light Attack Timing")]
    [Min(0.01f)] public float lightWindup = 0.10f;
    [Min(0.01f)] public float lightActiveTime = 0.12f;
    [Min(0.01f)] public float lightCooldown = 0.25f;

    [Header("Heavy Attack Timing")]
    [Min(0.01f)] public float heavyWindup = 0.16f;
    [Min(0.01f)] public float heavyActiveTime = 0.16f;
    [Min(0.01f)] public float heavyCooldown = 0.42f;

    [Header("Barrage Timing")]
    [Min(0.01f)] public float barrageTickInterval = 0.08f;

    [Header("Move Slowdown")]
    [Range(0.05f, 1f)] public float lightMoveMultiplier = 0.72f;
    [Range(0.05f, 1f)] public float heavyMoveMultiplier = 0.55f;
    [Range(0.05f, 1f)] public float barrageMoveMultiplier = 0.35f;

    [Header("Barrage VFX")]
    public GameObject barrageHandPrefab;
    [Min(0.01f)] public float barrageHandSpawnInterval = 0.04f;
    [Min(0.05f)] public float barrageHandLifetime = 0.22f;
    [Min(0f)] public float barrageHandMinSpeed = 5f;
    [Min(0f)] public float barrageHandMaxSpeed = 9f;
    [Range(0f, 1f)] public float barrageHandMinAlpha = 0.35f;
    [Range(0f, 1f)] public float barrageHandMaxAlpha = 0.85f;
    public Vector2 barrageHandSpread = new Vector2(0.12f, 0.08f);
}