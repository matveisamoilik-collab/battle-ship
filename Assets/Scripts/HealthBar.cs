using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image fillImage;
    private RectTransform _fillRect;

    void Awake()
    {
        if (fillImage == null) return;
        _fillRect = fillImage.GetComponent<RectTransform>();
        _fillRect.pivot = new Vector2(0f, 0.5f);
    }

    public void SetHealth(float current, float max)
    {
        if (_fillRect == null && fillImage != null)
        {
            _fillRect = fillImage.GetComponent<RectTransform>();
            _fillRect.pivot = new Vector2(0f, 0.5f);
        }
        if (_fillRect == null) return;
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        _fillRect.localScale = new Vector3(ratio, 1f, 1f);
    }
}
