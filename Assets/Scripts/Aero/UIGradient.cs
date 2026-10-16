using UnityEngine;
using UnityEngine.UI;

// Degradado vertical para textos e imágenes de uGUI (los títulos brillantes del estilo Aero).
// Va antes que Outline/Shadow en el GameObject para que el contorno no herede el degradado.
[AddComponentMenu("UI/Effects/Degradado Aero")]
public class UIGradient : BaseMeshEffect
{
    public Color top = Color.white;
    public Color bottom = new Color(0.5f, 0.85f, 1f);

    public void SetColors(Color topColor, Color bottomColor)
    {
        top = topColor;
        bottom = bottomColor;
        if (graphic) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        var v = new UIVertex();
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            min = Mathf.Min(min, v.position.y);
            max = Mathf.Max(max, v.position.y);
        }

        float h = max - min;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            float t = h > 0f ? (v.position.y - min) / h : 1f;
            v.color = Color.Lerp(bottom, top, t) * (Color)v.color;
            vh.SetUIVertex(v, i);
        }
    }
}
