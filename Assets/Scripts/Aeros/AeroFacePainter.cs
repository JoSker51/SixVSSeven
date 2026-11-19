using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Lienzo para dibujar la cara del Aero a mano alzada (como en el canal Aeros): va encima del zoom a la cabeza
// y solo pinta dentro del círculo. La cara se guarda como PNG de 256 × 256 (AeroLook.draw).
public class AeroFacePainter : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    const int N = AeroIconRenderer.DrawSize;

    public Color32 color = new Color32(10, 61, 102, 255);
    public int brush = 7;          // radio en píxeles del lienzo de 256
    public bool erase;
    public event Action StrokeEnded;

    private RawImage image;
    private Texture2D tex;
    private Color32[] px;
    private readonly List<Color32[]> undo = new List<Color32[]>();
    private Vector2 last;
    private bool painting;

    public static AeroFacePainter Create(RectTransform holder)
    {
        var node = AeroUI.Node("Lienzo", holder).Stretch();
        var img = node.gameObject.AddComponent<RawImage>();
        img.raycastTarget = true;
        var p = node.gameObject.AddComponent<AeroFacePainter>();
        p.image = img;
        p.tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Cara" };
        p.px = new Color32[N * N];
        p.tex.SetPixels32(p.px);
        p.tex.Apply();
        img.texture = p.tex;
        return p;
    }

    public void Load(string b64)
    {
        var src = AeroIconRenderer.DecodeDrawing(b64);
        if (src != null) Array.Copy(src, px, px.Length); else Array.Clear(px, 0, px.Length);
        undo.Clear();
        Push();
    }

    // null si está en blanco
    public string Save()
    {
        bool blank = true;
        for (int i = 3; i < px.Length; i += 7) if (px[i].a > 0) { blank = false; break; }
        if (blank) return null;
        return Convert.ToBase64String(tex.EncodeToPNG());
    }

    public void Undo()
    {
        if (undo.Count == 0) return;
        Array.Copy(undo[undo.Count - 1], px, px.Length);
        undo.RemoveAt(undo.Count - 1);
        Push();
        StrokeEnded?.Invoke();
    }

    public void Clear()
    {
        SaveUndo();
        Array.Clear(px, 0, px.Length);
        Push();
        StrokeEnded?.Invoke();
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!ToPixel(e, out var p)) return;
        SaveUndo();
        painting = true;
        last = p;
        Stamp(p, p);
        Push();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!painting || !ToPixel(e, out var p)) return;
        Stamp(last, p);
        last = p;
        Push();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!painting) return;
        painting = false;
        StrokeEnded?.Invoke();
    }

    private bool ToPixel(PointerEventData e, out Vector2 p)
    {
        p = default;
        var rt = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return false;
        var r = rt.rect;
        p = new Vector2((local.x - r.x) / r.width * N, (local.y - r.y) / r.height * N);   // y hacia arriba, igual que la textura
        return true;
    }

    // Pinta un trazo redondo de a hasta b, solo dentro del círculo de la cabeza
    private void Stamp(Vector2 a, Vector2 b)
    {
        float r = brush * (erase ? 2.2f : 1f);
        float half = N * 0.5f, rr = (half - 1f) * (half - 1f);
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 1f)), x1 = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 1f)), y1 = Mathf.Min(N - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 1f));
        Vector2 ab = b - a;
        float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float cx = x + 0.5f - half, cy = y + 0.5f - half;
                if (cx * cx + cy * cy > rr) continue;
                var q = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
                float d = (q - (a + ab * t)).magnitude;
                float cover = Mathf.Clamp01(r - d + 0.5f);
                if (cover <= 0f) continue;
                int i = y * N + x;
                var dst = px[i];
                if (erase) { dst.a = (byte)Mathf.Min(dst.a, (1f - cover) * 255f); px[i] = dst; continue; }
                float srcA = cover;
                float outA = srcA + dst.a / 255f * (1f - srcA);
                byte Mix(byte s, byte dd) => (byte)Mathf.Clamp(Mathf.RoundToInt((s * srcA + dd * (dst.a / 255f) * (1f - srcA)) / Mathf.Max(outA, 1e-4f)), 0, 255);
                px[i] = new Color32(Mix(color.r, dst.r), Mix(color.g, dst.g), Mix(color.b, dst.b), (byte)Mathf.RoundToInt(outA * 255f));
            }
    }

    private void SaveUndo()
    {
        undo.Add((Color32[])px.Clone());
        if (undo.Count > 25) undo.RemoveAt(0);
    }

    private void Push()
    {
        tex.SetPixels32(px);
        tex.Apply(false);
    }

    private void OnDestroy()
    {
        if (tex) Destroy(tex);
    }
}
