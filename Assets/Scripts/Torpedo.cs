using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Torpedo : MonoBehaviour
{
    public float speed = 50f;
    public float lifetime = 5f;
    public float damage = 1f;
    public GameObject explosionPrefab;
    public bool isPlayerTorpedo = false;

    private Rigidbody rb;
    // Prevents self-collision with the firing ship on the spawn frame
    private bool collisionEnabled;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
        // 0.1 s gives the torpedo 5 units of travel before any collision is checked
        Invoke(nameof(EnableCollision), 0.1f);
    }

    void EnableCollision() { collisionEnabled = true; }

    void OnCollisionEnter(Collision collision)
    {
        if (!collisionEnabled) return;

        // Reach the ship script on the root GO — ships are multi-object hierarchies
        Transform root = collision.transform.root;

        if (isPlayerTorpedo && root.CompareTag("Enemy"))
        {
            BotShip bot = root.GetComponent<BotShip>();
            if (bot != null) bot.TakeDamage(damage);
            HitEffect();
        }
        else if (!isPlayerTorpedo && root.CompareTag("Player"))
        {
            PlayerShip ps = root.GetComponent<PlayerShip>();
            if (ps != null) ps.TakeDamage(damage);
            HitEffect();
        }
        else if (!root.CompareTag("Player") && !root.CompareTag("Enemy"))
        {
            // Hit a wall, the water, or any other object — just explode
            SpawnExplosion();
            Destroy(gameObject);
        }
        // Hitting the wrong ship (own fire bouncing back) does nothing
    }

    void HitEffect()
    {
        SpawnExplosion();
        if (GameManager.Instance != null)
            GameManager.Instance.ShakeCamera(0.3f, 0.3f);
        Destroy(gameObject);
    }

    void SpawnExplosion()
    {
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
    }
}
