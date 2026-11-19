using UnityEngine;
using UnityEngine.UI;

// El ícono 2D de un Aero en la interfaz (marcador, menú, victoria): flota suave y salta de alegría con Hop().
public class AeroIconView : MonoBehaviour
{
    private RawImage image;
    private Texture2D tex;
    private int resolution;
    private RectTransform rt;
    private float phase, hopTime = -1f;

    // Crea el ícono como hijo de "holder", llenándolo
    public static AeroIconView Create(RectTransform holder, AeroLook look, int resolution = 192)
    {
        var node = AeroUI.Node("Aero", holder).Stretch();
        var view = node.gameObject.AddComponent<AeroIconView>();
        view.image = node.gameObject.AddComponent<RawImage>();
        view.image.raycastTarget = false;
        view.rt = node;
        view.resolution = resolution;
        view.phase = Random.value * 6f;
        view.SetLook(look);
        return view;
    }

    public void SetLook(AeroLook look)
    {
        tex = AeroIconRenderer.Render(look, resolution, tex);
        image.texture = tex;
    }

    public void Hop() => hopTime = 0f;

    private void Update()
    {
        float h = rt.rect.height;
        float y = Mathf.Sin(Time.unscaledTime * 2.2f + phase) * h * 0.015f;
        float squash = 0f;
        if (hopTime >= 0f)
        {
            hopTime += Time.unscaledDeltaTime;
            const float duration = 0.5f;
            float k = hopTime / duration;
            if (k >= 1f) hopTime = -1f;
            else { y += Mathf.Sin(k * Mathf.PI) * h * 0.16f; squash = Mathf.Sin(k * Mathf.PI * 2f) * 0.06f; }
        }
        rt.anchoredPosition = new Vector2(0f, y);
        rt.localScale = new Vector3(1f + squash, 1f - squash, 1f);
    }

    private void OnDestroy()
    {
        if (tex) Destroy(tex);
    }
}
