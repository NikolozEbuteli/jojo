using System.Collections.Generic;
using UnityEngine;

public class CombatHitbox : MonoBehaviour
{
    public enum HitboxMode
    {
        Single,
        Continuous
    }

    [SerializeField] private Collider2D hitCollider;

    private HitboxMode mode;
    private float damage;
    private float knockbackForce;
    private float tickInterval;
    private Transform sourceRoot;
    private bool active;
    private float nextTickTime;
    private readonly HashSet<int> hitTargets = new HashSet<int>();
    private readonly HashSet<Transform> ignoredRoots = new HashSet<Transform>();

    private void Awake()
    {
        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();

        if (hitCollider != null)
            hitCollider.enabled = false;
    }

    public void SetContext(Transform sourceRoot, params Transform[] rootsToIgnore)
    {
        this.sourceRoot = sourceRoot;
        ignoredRoots.Clear();

        if (rootsToIgnore == null)
            return;

        for (int i = 0; i < rootsToIgnore.Length; i++)
        {
            if (rootsToIgnore[i] != null)
                ignoredRoots.Add(rootsToIgnore[i]);
        }

        if (hitCollider != null && active)
            hitCollider.enabled = true;
    }

    public void BeginSingle(
        float damage,
        float knockbackForce,
        Transform sourceRoot,
        params Transform[] rootsToIgnore)
    {
        this.damage = damage;
        this.knockbackForce = knockbackForce;
        mode = HitboxMode.Single;
        active = true;
        nextTickTime = 0f;
        hitTargets.Clear();

        SetContext(sourceRoot, rootsToIgnore);

        if (hitCollider != null)
            hitCollider.enabled = true;
    }

    public void BeginContinuous(
        float damage,
        float knockbackForce,
        float tickInterval,
        Transform sourceRoot,
        params Transform[] rootsToIgnore)
    {
        this.damage = damage;
        this.knockbackForce = knockbackForce;
        this.tickInterval = Mathf.Max(0.01f, tickInterval);
        mode = HitboxMode.Continuous;
        active = true;
        nextTickTime = 0f;
        hitTargets.Clear();

        SetContext(sourceRoot, rootsToIgnore);

        if (hitCollider != null)
            hitCollider.enabled = true;
    }

    public void End()
    {
        active = false;
        hitTargets.Clear();

        if (hitCollider != null)
            hitCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!active || other == null)
            return;

        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!active || other == null || mode != HitboxMode.Continuous)
            return;

        if (Time.time < nextTickTime)
            return;

        nextTickTime = Time.time + tickInterval;
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        Transform otherRoot = other.transform.root;

        if (sourceRoot != null && otherRoot == sourceRoot)
            return;

        if (ignoredRoots.Contains(otherRoot))
            return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        if (mode == HitboxMode.Single)
        {
            int id = ((Component)damageable).GetInstanceID();
            if (hitTargets.Contains(id))
                return;

            hitTargets.Add(id);
        }

        Vector2 from = transform.position;
        Vector2 to = other.transform.position;
        Vector2 hitDir = (to - from).normalized;

        damageable.ApplyDamage(damage, hitDir, knockbackForce);
    }
}