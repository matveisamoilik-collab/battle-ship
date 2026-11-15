using UnityEngine;

// Pooled ballistic projectile for the kraken ink and the thrown rocks. The launch velocity is
// solved analytically so it lands exactly on the target after flightTime (same model as
// VolcanoStone / Cannonball). When it reaches the target the owner resolves the hit.
public interface IArcOwner
{
    void OnArcLanded(ArcProjectile projectile, Vector3 landPos);
}

public class ArcProjectile : MonoBehaviour
{
    public enum Kind { Ink, Rock }

    public Kind kind;
    public float spinSpeed = 0f;     // degrees per second (rocks tumble)

    [HideInInspector] public DangerZone marker;   // landing marker shown while in flight

    Vector3   velocity;
    float     gravity;
    float     timeLeft;
    Vector3   target;
    IArcOwner owner;
    Vector3   spinAxis = Vector3.right;

    public void Launch(Vector3 from, Vector3 target, float flightTime, float gravity, IArcOwner owner)
    {
        this.target  = target;
        this.gravity = gravity;
        this.owner   = owner;
        timeLeft     = Mathf.Max(0.1f, flightTime);
        transform.position = from;
        Vector3 d = target - from;
        velocity = new Vector3(d.x / timeLeft, (d.y + 0.5f * gravity * timeLeft * timeLeft) / timeLeft, d.z / timeLeft);
        spinAxis = Random.onUnitSphere;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, timeLeft);
        velocity.y -= gravity * dt;
        transform.position += velocity * dt;
        if (spinSpeed != 0f)
            transform.Rotate(spinAxis, spinSpeed * dt, Space.World);
        else if (velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(velocity.normalized); // ink drips (model -Z) trail behind
        timeLeft -= dt;
        if (timeLeft <= 0f)
        {
            var o = owner;
            owner = null;
            o?.OnArcLanded(this, new Vector3(target.x, 0f, target.z));
        }
    }

    public void Cancel() => owner = null;
}
