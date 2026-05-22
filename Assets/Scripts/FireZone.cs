using UnityEngine;
using UnityEngine.InputSystem;

public class FireZone : MonoBehaviour
{
    public bool IsPressed { get; private set; }

    void Update()
    {
        IsPressed = false;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        var ts = Touchscreen.current;
        if (ts == null) return;

        foreach (var touch in ts.touches)
        {
            if (touch.isInProgress && touch.position.ReadValue().x > Screen.width * 0.45f)
            {
                IsPressed = true;
                return;
            }
        }
    }
}
