using UnityEngine;

// Scripted (Rigidbody-free) falling debris used by Level 6: pillar chunks and collapsing drums.
// Falls with gravity and spins; on reaching the sea it splashes, slows down, sinks and self-destructs.
public class FallingPiece : MonoBehaviour
{
    public Vector3    velocity;
    public Vector3    spinAxis = Vector3.right;
    public float      spinSpeed;          // deg/s
    public float      delay;              // seconds before the piece starts to fall
    public GameObject splashPrefab;       // optional — big pieces only
    public bool       playSound;

    private const float Gravity   = 14f;
    private const float SinkSpeed = 1.6f;
    private const float SinkTime  = 3f;

    private bool  inWater;
    private float sinkTimer;

    void Update()
    {
        float dt = Time.deltaTime;
        if (delay > 0f) { delay -= dt; return; }

        if (!inWater)
        {
            velocity.y -= Gravity * dt;
            transform.position += velocity * dt;
            transform.Rotate(spinAxis, spinSpeed * dt, Space.World);

            if (transform.position.y <= 0f && velocity.y < 0f)
            {
                inWater = true;
                Vector3 p = new Vector3(transform.position.x, 0f, transform.position.z);
                if (splashPrefab != null) Instantiate(splashPrefab, p, Quaternion.identity);
                if (playSound) SoundManager.PlayAt("splash", p, 0.8f);
                velocity  *= 0.15f;
                spinSpeed *= 0.2f;
            }
        }
        else
        {
            sinkTimer += dt;
            transform.position += (velocity + Vector3.down * SinkSpeed) * dt;
            transform.Rotate(spinAxis, spinSpeed * dt, Space.World);
            if (sinkTimer >= SinkTime) Destroy(gameObject);
        }
    }
}
