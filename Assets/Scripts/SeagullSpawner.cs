using UnityEngine;

// Level 6: sends a seagull at the camera every Interval seconds (component on Level6_Dreadhold,
// so it only runs when that level's root is active). No gulls once the game is over or during
// the island-sinking finale; a gull still in the air at game over is removed.
public class SeagullSpawner : MonoBehaviour
{
    public const float Interval = 15f;

    public GameObject  seagullPrefab;
    public CameraSplat splat;

    float   timer;
    Seagull active;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || seagullPrefab == null) return;
        if (gm.IsGameOver || gm.Level6Finale)
        {
            if (active != null && gm.IsGameOver) Destroy(active.gameObject);
            return;
        }

        timer += Time.deltaTime;
        if (timer < Interval || active != null) return;
        timer = 0f;

        var cam = Camera.main;
        if (cam == null) return;
        var go = Instantiate(seagullPrefab, cam.transform.position + cam.transform.forward * 50f, Quaternion.identity);
        active = go.GetComponent<Seagull>();
        if (active == null) active = go.AddComponent<Seagull>();
        active.Init(cam, splat);
    }
}
