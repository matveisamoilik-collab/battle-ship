using UnityEngine;

// Victory dolphin (Dolphin.fbx, modelled upright: tail at y = 0, head up, belly +Z). Leaps out of
// the sea in front of the player's ship, lands tail-walking facing the camera and claps its
// flippers (Animator states Jump -> Applaud loop).
public class DolphinApplause : MonoBehaviour
{
    public Transform pivot;
    public Animator  animator;
    public float     modelScale = 2.2f;

    static readonly int JumpHash    = Animator.StringToHash("Jump");
    static readonly int ApplaudHash = Animator.StringToHash("Applaud");

    BossEncounterController host;
    bool    playing;
    bool    playSounds = true;
    float   t;
    Vector3 start, end;
    bool    applauding;
    float   nextClap;

    const float LeapTime   = 1.1f;
    const float LeapHeight = 9f;
    const float StandY     = -1.2f;   // tail under water while tail-walking

    public void Init(BossEncounterController host, bool playSounds = true)
    {
        this.host = host;
        this.playSounds = playSounds;
        gameObject.SetActive(false);
    }

    // delay: seconds before the leap (staggers a ring of dolphins)
    public void Play(Vector3 standPos, Vector3 faceToward, float delay = 0f)
    {
        gameObject.SetActive(true);
        playing = true;
        Vector3 d = KrakenUtil.Flat(faceToward - standPos);
        transform.rotation = Quaternion.LookRotation(d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back);
        end   = KrakenUtil.Flat(standPos);
        start = end - transform.forward * 7f;
        transform.position = start;
        pivot.localScale = Vector3.one * modelScale;
        pivot.localPosition = new Vector3(0f, -12f, 0f);   // hidden under water until the leap
        t = -delay;
        applauding = false;
        jumped = false;
    }

    bool jumped;

    public void HideImmediate()
    {
        playing = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!playing) return;
        t += Time.deltaTime;
        if (t < 0f) return;
        if (!jumped)
        {
            jumped = true;
            if (animator != null) animator.Play(JumpHash, 0, 0f);
            host.Splash(start, 1.5f);
            if (playSounds) SoundManager.PlayAt("splash", start, 1f, 0.05f, 1.3f);
        }
        if (t < LeapTime)
        {
            float k = t / LeapTime;
            transform.position = Vector3.Lerp(start, end, k);
            float y = Mathf.Lerp(-4f, StandY, k) + Mathf.Sin(k * Mathf.PI) * LeapHeight;
            pivot.localPosition = new Vector3(0f, y, 0f);
            // body pitches from diving-forward through the arc to upright
            pivot.localRotation = Quaternion.Euler(Mathf.Lerp(55f, 0f, KrakenUtil.Smooth01(k)), 0f, 0f);
            return;
        }
        if (!applauding)
        {
            applauding = true;
            transform.position = end;
            pivot.localRotation = Quaternion.identity;
            if (animator != null) animator.CrossFadeInFixedTime(ApplaudHash, 0.2f);
            host.Splash(end, 1.2f);
            if (playSounds) SoundManager.PlayAt("splash", end, 0.8f, 0.05f, 1.4f);
            nextClap = t + 0.25f;
        }
        // tail-walking bob, claps twice per second (matches the Applaud clip)
        float tt = t - LeapTime;
        pivot.localPosition = new Vector3(0f, StandY + Mathf.Sin(tt * 4f) * 0.25f, Mathf.Sin(tt * 2f) * 0.3f);
        if (t >= nextClap)
        {
            nextClap += 0.5f;
            if (playSounds) SoundManager.PlayAt("click", end, 1f, 0.15f, 0.75f);
        }
    }
}
