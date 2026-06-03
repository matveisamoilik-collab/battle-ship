using UnityEngine;

public class VolcanoEruption : MonoBehaviour
{
    [HideInInspector] public GameObject stonePrefab;

    private static readonly Vector3 VolcanoTop = new Vector3(0.5f, 13.5f, 1.4f);
    private const float Interval  = 5f;
    private const float VertSpeed = 22f;
    private const float Gravity   = 14f;

    private float _timer = 0f;

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.PlayingLevel != 4) return;
        if (stonePrefab == null) return;

        _timer += Time.deltaTime;
        if (_timer >= Interval)
        {
            _timer = 0f;
            LaunchStone();
        }
    }

    void LaunchStone()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null) return;

        Vector3 target = new Vector3(playerGO.transform.position.x, 0f, playerGO.transform.position.z);

        // Solve time of flight: VolcanoTop.y + VertSpeed*t - 0.5*Gravity*t² = 0
        float a    = 0.5f * Gravity;
        float disc = VertSpeed * VertSpeed + 4f * a * VolcanoTop.y;
        float tof  = (VertSpeed + Mathf.Sqrt(disc)) / (2f * a);

        Vector3 horiz  = (target - new Vector3(VolcanoTop.x, 0f, VolcanoTop.z)) / tof;
        Vector3 velocity = new Vector3(horiz.x, VertSpeed, horiz.z);

        var go = Instantiate(stonePrefab, VolcanoTop, Random.rotation);
        var stone = go.GetComponent<VolcanoStone>();
        if (stone != null) stone.Launch(velocity);
    }
}
