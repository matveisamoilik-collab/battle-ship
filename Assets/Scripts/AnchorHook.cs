using UnityEngine;

// Level 5 hazard: a giant anchor rests on the big island. Every 10 s (counted while idle)
// it flies to the player's ship, hooks it and drags it into the island. The crash deals damage.
public class AnchorHook : MonoBehaviour
{
    private enum State { Idle, Flying, Pulling, Returning }

    public Transform    anchor;   // pivot at the anchor's bottom-centre
    public LineRenderer chain;
    public IslandData   island;

    private const float Interval      = 10f;
    private const float FlySpeed      = 60f;
    private const float PullSpeed     = 30f;
    private const float ReturnSpeed   = 45f;
    private const float PullTimeout   = 6f;
    private const float SpinSpeed     = 540f;   // deg/s while flying / returning
    private const float HookHeight    = 1.5f;   // attach point above ship origin
    private const float ChainTopRatio = 0.8f;   // chain attaches near the anchor's ring

    private State      state = State.Idle;
    private float      timer;
    private float      pullTime;
    private Vector3    restPos;
    private Quaternion restRot;
    private Vector3    chainOrigin;
    private float      anchorHeight = 1f;
    private PlayerShip player;

    void Start()
    {
        if (anchor == null) { enabled = false; return; }

        restPos     = anchor.position;
        restRot     = anchor.rotation;
        chainOrigin = restPos + Vector3.up * 0.5f;

        // Measure anchor height for the chain attach point
        bool has = false;
        Bounds b = new Bounds();
        foreach (var r in anchor.GetComponentsInChildren<Renderer>())
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (has) anchorHeight = Mathf.Max(0.5f, b.max.y - anchor.position.y);

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null) player = playerGO.GetComponent<PlayerShip>();

        if (chain != null) chain.enabled = false;
    }

    bool Active =>
        GameManager.Instance != null &&
        !GameManager.Instance.IsGameOver &&
        GameManager.Instance.PlayingLevel == 5 &&
        player != null;

    void Update()
    {
        if (!Active) return;

        switch (state)
        {
            case State.Idle:
                timer += Time.deltaTime;
                if (timer >= Interval)
                {
                    state = State.Flying;
                    SoundManager.PlayAt("anchor_throw", anchor.position, 1f);
                }
                break;

            case State.Flying:
            {
                Vector3 target = HookPoint();
                anchor.position = Vector3.MoveTowards(anchor.position, target, FlySpeed * Time.deltaTime);
                anchor.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
                if ((anchor.position - target).sqrMagnitude < 1f)
                {
                    Vector3 c = island != null ? island.transform.position : transform.position;
                    player.BeginPull(new Vector3(c.x, 0f, c.z), PullSpeed);
                    pullTime = 0f;
                    state    = State.Pulling;
                    SoundManager.Play("anchor_hook", 1f);
                    GameManager.Instance.ShakeCamera(0.2f, 0.4f);
                }
                break;
            }

            case State.Pulling:
                pullTime += Time.deltaTime;
                if (player.PullCrashed || pullTime >= PullTimeout)
                {
                    // Crash damage is applied by PlayerShip's island-collision rule (Level 5+)
                    player.EndPull();
                    GameManager.Instance.ShakeCamera(0.4f, 1.0f);
                    state = State.Returning;
                    SoundManager.PlayAt("anchor_reel", anchor.position, 0.9f);
                }
                break;

            case State.Returning:
                anchor.position = Vector3.MoveTowards(anchor.position, restPos, ReturnSpeed * Time.deltaTime);
                anchor.Rotate(Vector3.up, -SpinSpeed * Time.deltaTime, Space.World);
                if ((anchor.position - restPos).sqrMagnitude < 0.01f)
                {
                    anchor.position = restPos;
                    anchor.rotation = restRot;
                    timer = 0f;
                    state = State.Idle;
                }
                break;
        }
    }

    // Runs after PlayerShip.Update has moved the ship, so the anchor never lags behind it
    void LateUpdate()
    {
        if (state == State.Pulling && player != null)
            anchor.position = HookPoint();

        if (chain == null) return;
        bool show = state != State.Idle;
        chain.enabled = show;
        if (show)
        {
            chain.SetPosition(0, chainOrigin);
            chain.SetPosition(1, anchor.position + Vector3.up * anchorHeight * ChainTopRatio);
        }
    }

    Vector3 HookPoint() => player.transform.position + Vector3.up * HookHeight;
}
