using UnityEngine;

public class BarrageHandVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Vector2 moveDirection;
    private float speed;
    private float lifetime;
    private float age;
    private Color baseColor;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Initialize(Vector2 direction, float moveSpeed, float lifeTime, float startAlpha)
    {
        moveDirection = direction.sqrMagnitude < 0.0001f ? Vector2.up : direction.normalized;
        speed = moveSpeed;
        lifetime = Mathf.Max(0.05f, lifeTime);
        age = 0f;

        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            baseColor.a = startAlpha;
            spriteRenderer.color = baseColor;
        }
    }

    private void Update()
    {
        transform.position += (Vector3)(moveDirection * speed * Time.deltaTime);
        age += Time.deltaTime;

        float t = Mathf.Clamp01(age / lifetime);
        float alpha = Mathf.Lerp(baseColor.a, 0f, t);

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }

        if (age >= lifetime)
            Destroy(gameObject);
    }
}