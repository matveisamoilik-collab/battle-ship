using UnityEngine;
using UnityEngine.UI;

// Shows the procedurally drawn shark picture on the shop card (the texture is generated at runtime, not saved as an asset).
[RequireComponent(typeof(RawImage))]
public class SharkSwatch : MonoBehaviour
{
    void Awake()
    {
        var tex = ModernShipVisual.SharkTexture;
        GetComponent<RawImage>().texture = tex;
        var fit = GetComponent<AspectRatioFitter>();
        if (fit != null) fit.aspectRatio = (float)tex.width / tex.height; // keep the card picture undistorted
    }
}
