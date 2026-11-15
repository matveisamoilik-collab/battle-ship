using UnityEngine;
using UnityEngine.UI;

// Level 7 boss bar: a wide HealthBar under the top-centre objective text, with the boss name +
// phase on the left and a shield / submerged state. The phase sentence itself goes to
// GameManager.SetObjectiveText (the L6 objective slot).
public class BossHUD : MonoBehaviour
{
    public HealthBar   bar;
    public Image       fill;
    public Text        nameText;
    public Text        phaseText;
    public CanvasGroup group;

    public static readonly Color KrakenColor = new Color(0.85f, 0.15f, 0.35f);
    public static readonly Color ShieldColor = new Color(0.35f, 0.8f, 1f);
    public static readonly Color FishColor   = new Color(0.95f, 0.75f, 0.2f);

    Color baseColor = KrakenColor;
    bool  shielded;

    public void Show(string bossName, string phase, float hp, float max, Color color)
    {
        gameObject.SetActive(true);
        if (nameText  != null) nameText.text  = bossName;
        if (phaseText != null) phaseText.text = phase;
        baseColor = color;
        shielded  = false;
        SetHP(hp, max);
        SetDim(false);
        ApplyColor();
    }

    public void SetHP(float hp, float max)
    {
        if (bar != null) bar.SetHealth(hp, max);
    }

    public void SetDim(bool dim)
    {
        if (group != null) group.alpha = dim ? 0.45f : 1f;
    }

    public void SetShield(bool on)
    {
        shielded = on;
        ApplyColor();
    }

    public void Hide() => gameObject.SetActive(false);

    void ApplyColor()
    {
        if (fill != null) fill.color = shielded ? ShieldColor : baseColor;
    }
}
