using UnityEngine;

public class VolcanoStone : MonoBehaviour
{
    [HideInInspector] public GameObject splashPrefab;

    private Vector3 _velocity;
    private const float Gravity      = 14f;
    private const float DamageRadius = 8f;
    private const float StoneDamage  = 3f;

    public void Launch(Vector3 initialVelocity)
    {
        _velocity = initialVelocity;
    }

    void Update()
    {
        _velocity.y -= Gravity * Time.deltaTime;
        transform.position += _velocity * Time.deltaTime;

        float hSpeed = new Vector2(_velocity.x, _velocity.z).magnitude;
        if (hSpeed > 0.01f)
            transform.Rotate(Vector3.right, hSpeed * 15f * Time.deltaTime, Space.Self);

        if (transform.position.y <= 0.1f)
            Impact();
    }

    void Impact()
    {
        Vector3 pos = new Vector3(transform.position.x, 0f, transform.position.z);

        if (splashPrefab != null)
            Instantiate(splashPrefab, pos, Quaternion.identity);

        GameManager.Instance?.ShakeCamera(0.35f, 0.3f);

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            float dist = Vector3.Distance(
                new Vector3(playerGO.transform.position.x, 0f, playerGO.transform.position.z), pos);
            if (dist <= DamageRadius)
            {
                var ship = playerGO.GetComponent<PlayerShip>()
                           ?? playerGO.GetComponentInParent<PlayerShip>();
                ship?.TakeDamage(StoneDamage);
            }
        }

        Destroy(gameObject);
    }
}
