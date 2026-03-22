using UnityEngine;

public interface IDamageable
{
    void ApplyDamage(float amount, Vector2 hitDirection, float knockbackForce);
}