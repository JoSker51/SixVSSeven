using System;
using System.Collections.Generic;
using UnityEngine;

// Dibuja el ícono 2D de un Aero (estilo de los muñequitos de Messenger: cabeza y hombros brillantes con borde oscuro grueso)
// a una textura, píxel por píxel, con figuras suaves (campos de distancia) y degradados. Todo en un lienzo de 256 unidades
// (y hacia abajo); "view" elige qué parte se ve, así el mismo dibujo sirve para el ícono entero o para el zoom a la cabeza.
public static class AeroIconRenderer
{
    public const float HeadR = 46f;
    public const int DrawSize = 256;   // la cara dibujada se guarda a 256 × 256 sobre el cuadrado de la cabeza

    static readonly Color Ink = new Color(0.08f, 0.1f, 0.16f);
    const float Stroke = 5f;

    // Lo que se ve normalmente: un poco más que el lienzo, para que entren los sombreros altos
    public static readonly Rect FullView = new Rect(-14f, -26f, 284f, 284f);

    public struct Layout { public float sx, sy, bodyTop, hc; public Vector2 Head => new Vector2(128f, hc); }

    public static Layout LayoutOf(AeroLook look)
    {
        float sx = Mathf.Clamp(look.w, AeroLook.MinW, AeroLook.MaxW), sy = Mathf.Clamp(look.h, AeroLook.MinH, AeroLook.MaxH);
        float bodyTop = 248f - 96f * sy;
        return new Layout { sx = sx, sy = sy, bodyTop = bodyTop, hc = bodyTop - 36f };
    }

    // Cuadrado de la cabeza (lienzo), donde se pinta la cara
    public static Rect HeadBox(AeroLook look)
    {
        var l = LayoutOf(look);
        return new Rect(128f - HeadR, l.hc - HeadR, HeadR * 2f, HeadR * 2f);
    }

    public static Texture2D Render(AeroLook look, int size, Texture2D reuse = null, Rect? view = null, bool bare = false, bool withDrawing = true)
    {
        var tex = reuse != null && reuse.width == size ? reuse : new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Aero" };
        var c = new Canvas(size, view ?? FullView);
        Paint(c, look, bare, withDrawing);
        tex.SetPixels(c.px);
        tex.Apply(false);
        return tex;
    }

    // ---------------- Lienzo ----------------

    private class Canvas
    {
        public readonly Color[] px;
        public readonly int size;
        public readonly Rect view;
        public readonly float unit;   // unidades del lienzo por píxel

        public Canvas(int size, Rect view)
        {
            this.size = size; this.view = view;
            px = new Color[size * size];
            unit = view.width / size;
        }

        // Rellena una figura: sdf < 0 adentro. Con borde, primero pinta la figura engordada en tinta.
        public void Shape(Rect bounds, Func<Vector2, float> sdf, Func<Vector2, Color> fill, float stroke = Stroke, Func<Vector2, bool> clip = null)
        {
            float grow = stroke + unit * 2f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin - grow - view.x) / unit));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((bounds.xMax + grow - view.x) / unit));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - grow - view.y) / unit));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt((bounds.yMax + grow - view.y) / unit));
            for (int py = y0; py <= y1; py++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(view.x + (x + 0.5f) * unit, view.y + (py + 0.5f) * unit);
                    if (clip != null && !clip(p)) continue;
                    float d = sdf(p);
                    int i = (size - 1 - py) * size + x;   // la textura crece hacia arriba; el lienzo, hacia abajo
                    if (stroke > 0f)
                    {
                        float a = Mathf.Clamp01(0.5f - (d - stroke) / unit);
                        if (a > 0f) Blend(i, Ink, a);
                    }
                    float f = Mathf.Clamp01(0.5f - d / unit);
                    if (f > 0f) { Color col = fill(p); Blend(i, col, f * col.a); }
                }
        }

        public void Blend(int i, Color c, float a)
        {
            Color dst = px[i];
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return;
            px[i] = new Color((c.r * a + dst.r * dst.a * (1f - a)) / outA, (c.g * a + dst.g * dst.a * (1f - a)) / outA, (c.b * a + dst.b * dst.a * (1f - a)) / outA, outA);
        }
    }

    // ---------------- La figura ----------------

    private static void Paint(Canvas c, AeroLook look, bool bare, bool withDrawing)
    {
        var L = LayoutOf(look);
        Color body = AeroLook.ParseColor(look.body, new Color(0.18f, 0.66f, 1f));
        Color head = AeroLook.ParseColor(look.head, body);
        Color hatC = AeroLook.ParseColor(look.hatColor, Color.red);
        Color scarfC = AeroLook.ParseColor(look.scarfColor, Color.cyan);
        float bt = L.bodyTop, hc = L.hc, ht = hc - HeadR;
        var acc = look.acc ?? new List<string>();

        // Detrás: alas y capa
        if (acc.Contains("wings"))
        {
            foreach (float s in new[] { -1f, 1f })
            {
                var cen = new Vector2(128f + s * (70f * L.sx + 20f), bt + 22f);
                float ang = s * 28f;
                c.Shape(Box(cen, 58f), p => Ellipse(Rot(p - cen, ang), 48f, 26f), Iridescent, 3.5f);
            }
        }
        if (acc.Contains("cape"))
        {
            var cen = new Vector2(128f, 262f);
            c.Shape(new Rect(128f - 110f * L.sx, bt - 6f, 220f * L.sx, 270f - bt), p => Mathf.Max(Ellipse(p - cen, 100f * L.sx, 262f - bt + 4f), p.y - 250f),
                    Gloss(new Color(0.83f, 0.17f, 0.19f), cen + new Vector2(-30f, -60f), 140f));
        }

        // Cuerpo: hombros redondos, base recta
        var bc = new Vector2(128f, 248f);
        float rx = 86f * L.sx, ry = 96f * L.sy;
        c.Shape(new Rect(128f - rx, bt, rx * 2f, 248f - bt), p => Mathf.Max(Ellipse(p - bc, rx, ry), p.y - 248f),
                Gloss(body, new Vector2(128f - rx * 0.35f, bt + 18f), rx * 1.5f));
        // Brillo del cuerpo
        var bg = new Vector2(128f - rx * 0.22f, bt + 26f);
        c.Shape(Box(bg, 50f), p => Ellipse(Rot(p - bg, -10f), rx * 0.55f, 16f), p => Shine(p, bg.y - 16f, 32f, 0.55f), 0f);

        if (acc.Contains("pin"))
        {
            var pc = new Vector2(128f - 42f * L.sx, bt + 50f);
            c.Shape(Box(pc, 13f), p => Circle(p - pc, 12f), _ => Color.white, 3f);
            var a = pc + new Vector2(3f, -2f); c.Shape(Box(a, 7f), p => Circle(p - a, 5.5f), _ => new Color(1f, 0.36f, 0.56f), 0f);
            var b = pc + new Vector2(-4f, 3f); c.Shape(Box(b, 6f), p => Circle(p - b, 4.5f), _ => new Color(1f, 0.89f, 0.54f), 1.5f);
        }

        Scarf(c, look.scarf, scarfC, L);

        // Cabeza
        var hcV = L.Head;
        c.Shape(Box(hcV, HeadR), p => Circle(p - hcV, HeadR), Gloss(head, hcV + new Vector2(-16f, -20f), HeadR * 1.7f));
        if (withDrawing && !string.IsNullOrEmpty(look.draw)) Drawing(c, look.draw, hcV);
        // Brillo de la cabeza
        var hg = hcV + new Vector2(-12f, -22f);
        c.Shape(Box(hg, 32f), p => Ellipse(Rot(p - hg, -18f), 28f, 14f), p => Shine(p, hg.y - 14f, 28f, 0.85f), 0f);

        if (bare) return;
        if (acc.Contains("phones")) Phones(c, hatC, hcV);
        Glasses(c, look.glasses, hcV);
        Hat(c, look.hat, hatC, hcV, ht);
        if (acc.Contains("flower"))
        {
            var f = new Vector2(160f, ht + 18f);
            for (int i = 0; i < 5; i++)
            {
                float an = i * Mathf.PI * 2f / 5f;
                var pp = f + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * 9f;
                c.Shape(Box(pp, 8f), p => Circle(p - pp, 7f), _ => new Color(1f, 0.62f, 0.77f), 2.5f);
            }
            c.Shape(Box(f, 6f), p => Circle(p - f, 5.5f), _ => new Color(1f, 0.82f, 0.25f), 2f);
        }
        if (acc.Contains("wand"))
        {
            var a = new Vector2(214f, 236f); var b = new Vector2(232f, 168f);
            c.Shape(Bounds(a, b, 4f), p => Segment(p, a, b) - 3f, _ => new Color(0.79f, 0.54f, 0f), 2.5f);
            var r = new Vector2(236f, 154f);
            c.Shape(Box(r, 13f), p => Mathf.Abs(Circle(p - r, 10f)) - 3f, Gold, 2.5f);
            var b1 = new Vector2(222f, 126f); c.Shape(Box(b1, 10f), p => Circle(p - b1, 9f), Iridescent, 2f);
            var b2 = new Vector2(244f, 112f); c.Shape(Box(b2, 7f), p => Circle(p - b2, 6f), Iridescent, 2f);
        }
    }

    private static void Hat(Canvas c, string id, Color col, Vector2 h, float ht)
    {
        Color dark = Color.Lerp(col, Color.black, 0.22f);
        switch (id)
        {
            case "beanie":
            {
                // Arriba de los ojos: así no tapa la cara dibujada
                var dc = new Vector2(128f, ht + 26f);
                c.Shape(new Rect(76f, ht - 26f, 104f, 54f), p => Mathf.Max(Circle(p - dc, 50f), p.y - dc.y), Gloss(col, dc + new Vector2(-18f, -40f), 70f));
                var band = new Vector2(128f, ht + 26f);
                c.Shape(Box(band, 56f), p => RoundBox(p - band, new Vector2(54f, 9f), 9f), Gloss(dark, band + new Vector2(-20f, -6f), 70f));
                var pom = new Vector2(128f, ht - 26f);
                c.Shape(Box(pom, 13f), p => Circle(p - pom, 12f), Gloss(Color.Lerp(col, Color.white, 0.35f), pom + new Vector2(-4f, -4f), 16f));
                break;
            }
            case "cap":
            {
                var dc = new Vector2(128f, ht + 24f);
                var vis = new Vector2(176f, ht + 23f);
                c.Shape(Box(vis, 36f), p => Ellipse(p - vis, 34f, 8f), Gloss(dark, vis + new Vector2(-10f, -4f), 40f));
                c.Shape(new Rect(78f, ht - 24f, 100f, 50f), p => Mathf.Max(Circle(p - dc, 48f), p.y - dc.y), Gloss(col, dc + new Vector2(-18f, -38f), 70f));
                var btn = new Vector2(128f, ht - 23f);
                c.Shape(Box(btn, 6f), p => Circle(p - btn, 5f), _ => dark, 2f);
                break;
            }
            case "tophat":
            {
                var top = new Vector2(128f, ht - 12f);
                c.Shape(Box(top, 34f), p => RoundBox(p - top, new Vector2(30f, 26f), 4f), Gloss(col, top + new Vector2(-14f, -18f), 50f));
                var band = new Vector2(128f, ht + 6f);
                Color bandC = col.grayscale > 0.85f ? new Color(0.91f, 0.2f, 0.23f) : Color.white;
                c.Shape(Box(band, 32f), p => RoundBox(p - band, new Vector2(30f, 5f), 1f), _ => bandC, 2f);
                var brim = new Vector2(128f, ht + 16f);
                c.Shape(Box(brim, 56f), p => RoundBox(p - brim, new Vector2(52f, 5f), 5f), Gloss(dark, brim + new Vector2(-20f, -4f), 70f));
                break;
            }
            case "crown":
            {
                var band = new Vector2(128f, ht + 10f);
                Func<Vector2, float> crown = p =>
                {
                    float d = RoundBox(p - band, new Vector2(38f, 9f), 2f);
                    foreach (float x in new[] { 98f, 128f, 158f })
                        d = Mathf.Min(d, Triangle(p, new Vector2(x - 14f, ht + 4f), new Vector2(x + 14f, ht + 4f), new Vector2(x, ht - 26f)));
                    return d;
                };
                c.Shape(new Rect(86f, ht - 28f, 84f, 48f), crown, Gold);
                foreach (var (x, gem) in new[] { (128f, new Color(0.91f, 0.2f, 0.23f)), (106f, new Color(0.1f, 0.65f, 0.85f)), (150f, new Color(0.37f, 0.82f, 0.24f)) })
                {
                    var g = new Vector2(x, ht + 10f);
                    c.Shape(Box(g, 6f), p => Circle(p - g, 4.5f), _ => gem, 1.5f);
                }
                break;
            }
            case "party":
            {
                Vector2 a = new Vector2(108f, ht + 12f), b = new Vector2(154f, ht + 12f), apex = new Vector2(134f, ht - 50f);
                c.Shape(new Rect(104f, ht - 52f, 54f, 66f), p => Triangle(p, a, b, apex), PartyFill(col, apex.y, ht + 12f));
                var pom = apex + new Vector2(0f, -2f);
                c.Shape(Box(pom, 10f), p => Circle(p - pom, 9f), _ => new Color(1f, 0.82f, 0.25f), 3f);
                break;
            }
            case "halo":
            {
                var hc = new Vector2(128f, ht - 12f);
                c.Shape(Box(hc, 48f), p => Mathf.Abs(Ellipse(p - hc, 42f, 10f)) - 3.5f, Iridescent, 2.5f);
                var s1 = new Vector2(84f, ht - 22f); c.Shape(Box(s1, 7f), p => Circle(p - s1, 5.5f), Iridescent, 2f);
                var s2 = new Vector2(172f, ht - 26f); c.Shape(Box(s2, 6f), p => Circle(p - s2, 4.5f), Iridescent, 2f);
                break;
            }
        }
    }

    private static void Scarf(Canvas c, string id, Color col, Layout L)
    {
        float y = L.bodyTop + 6f;
        Color dark = Color.Lerp(col, Color.black, 0.18f);
        Func<Vector2, Color> fill = id == "stripes" ? Stripes(col) : Gloss(col, new Vector2(100f, y - 8f), 90f);
        switch (id)
        {
            case "plain":
            case "stripes":
            {
                var tail = new Vector2(150f + 6f * L.sx, y + 34f);
                c.Shape(Box(tail, 36f), p => RoundBox(Rot(p - tail, -10f), new Vector2(12f, 30f), 9f), fill);
                var band = new Vector2(128f, y);
                c.Shape(Box(band, 70f), p => RoundBox(p - band, new Vector2(60f * L.sx, 13f), 12f), fill);
                break;
            }
            case "bow":
            {
                var k = new Vector2(128f, y + 4f);
                Func<Vector2, float> bow = p => Mathf.Min(
                    Triangle(p, k, k + new Vector2(-34f, -18f), k + new Vector2(-34f, 18f)),
                    Triangle(p, k, k + new Vector2(34f, -18f), k + new Vector2(34f, 18f)));
                c.Shape(Box(k, 36f), bow, fill);
                c.Shape(Box(k, 10f), p => Circle(p - k, 9f), _ => dark, 3f);
                break;
            }
            case "tie":
            {
                var k = new Vector2(128f, y + 2f);
                Func<Vector2, float> tie = p => Mathf.Min(
                    Triangle(p, k + new Vector2(-10f, 8f), k + new Vector2(10f, 8f), k + new Vector2(0f, 76f)) - 3f,
                    Triangle(p, k + new Vector2(-14f, 30f), k + new Vector2(14f, 30f), k + new Vector2(0f, 82f)));
                c.Shape(new Rect(110f, y, 36f, 90f), tie, fill);
                c.Shape(Box(k, 12f), p => RoundBox(p - k, new Vector2(10f, 8f), 3f), _ => dark, 3f);
                break;
            }
            case "beads":
            {
                Color alt = Color.Lerp(col, Color.white, 0.45f);
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.PI * (0.12f + 0.76f * i / 8f);
                    var b = new Vector2(128f - Mathf.Cos(a) * 56f * L.sx, y - 4f + Mathf.Sin(a) * 16f);
                    Color bc = i % 2 == 0 ? col : alt;
                    c.Shape(Box(b, 9f), p => Circle(p - b, 8f), Gloss(bc, b + new Vector2(-3f, -3f), 12f), 2.5f);
                }
                break;
            }
        }
    }

    private static void Glasses(Canvas c, string id, Vector2 h)
    {
        float y = h.y - 2f;
        switch (id)
        {
            case "round":
                foreach (float x in new[] { 109f, 147f })
                {
                    var e = new Vector2(x, y);
                    c.Shape(Box(e, 17f), p => Circle(p - e, 15f), _ => new Color(0.82f, 0.94f, 1f, 0.45f), 0f);
                    c.Shape(Box(e, 20f), p => Mathf.Abs(Circle(p - e, 15f)) - 2.5f, _ => Ink, 0f);
                }
                { var a = new Vector2(122f, y - 2f); var b = new Vector2(134f, y - 2f); c.Shape(Bounds(a, b, 3f), p => Segment(p, a, b) - 2f, _ => Ink, 0f); }
                break;
            case "sun":
                foreach (float x in new[] { 108f, 148f })
                {
                    var e = new Vector2(x, y);
                    c.Shape(Box(e, 20f), p => RoundBox(p - e, new Vector2(17f, 11f), 6f), Gloss(new Color(0.1f, 0.13f, 0.19f), e + new Vector2(-6f, -6f), 24f), 2.5f);
                    var hl = e + new Vector2(-5f, -5f); var hr = e + new Vector2(5f, -5f);
                    c.Shape(Bounds(hl, hr, 2f), p => Segment(p, hl, hr) - 1.2f, _ => new Color(1f, 1f, 1f, 0.6f), 0f);
                }
                { var a = new Vector2(124f, y - 4f); var b = new Vector2(132f, y - 4f); c.Shape(Bounds(a, b, 3f), p => Segment(p, a, b) - 2f, _ => Ink, 0f); }
                break;
            case "visor":
            {
                var e = new Vector2(128f, y);
                c.Shape(Box(e, 48f), p => RoundBox(p - e, new Vector2(44f, 11f), 10f), _ => new Color(0.31f, 0.86f, 1f, 0.6f), 3f);
                var a = new Vector2(96f, y - 5f); var b = new Vector2(160f, y - 5f);
                c.Shape(Bounds(a, b, 3f), p => Segment(p, a, b) - 1.6f, _ => new Color(1f, 1f, 1f, 0.85f), 0f);
                break;
            }
            case "3d":
                foreach (var (x, lens) in new[] { (109f, new Color(0.91f, 0.2f, 0.23f, 0.85f)), (147f, new Color(0.1f, 0.65f, 0.85f, 0.85f)) })
                {
                    var e = new Vector2(x, y);
                    c.Shape(Box(e, 20f), p => RoundBox(p - e, new Vector2(16f, 11f), 2f), _ => Color.white, 2.5f);
                    c.Shape(Box(e, 16f), p => RoundBox(p - e, new Vector2(12.5f, 7.5f), 1f), _ => lens, 0f);
                }
                break;
            case "heart":
                foreach (float x in new[] { 109f, 147f })
                {
                    var e = new Vector2(x, y);
                    c.Shape(Box(e, 18f), p => Heart(p - e, 15f), Gloss(new Color(1f, 0.37f, 0.64f), e + new Vector2(-5f, -6f), 20f), 2.5f);
                }
                break;
            case "mono":
            {
                var e = new Vector2(147f, y);
                c.Shape(Box(e, 17f), p => Circle(p - e, 14f), _ => new Color(0.82f, 0.94f, 1f, 0.4f), 0f);
                c.Shape(Box(e, 20f), p => Mathf.Abs(Circle(p - e, 14f)) - 3f, Gold, 1.5f);
                var a = new Vector2(160f, y + 10f); var b = new Vector2(166f, y + 52f);
                c.Shape(Bounds(a, b, 2f), p => Segment(p, a, b) - 1.2f, _ => new Color(0.79f, 0.54f, 0f), 0f);
                break;
            }
        }
    }

    private static void Phones(Canvas c, Color col, Vector2 h)
    {
        var band = h;
        c.Shape(new Rect(h.x - 58f, h.y - 58f, 116f, 60f), p => Mathf.Abs(Circle(p - band, 52f)) - 4f, _ => new Color(0.17f, 0.18f, 0.23f), 2f, p => p.y < h.y);
        foreach (float s in new[] { -1f, 1f })
        {
            var cup = new Vector2(h.x + s * 50f, h.y + 2f);
            c.Shape(Box(cup, 22f), p => RoundBox(p - cup, new Vector2(11f, 18f), 9f), Gloss(col, cup + new Vector2(-4f, -8f), 26f), 3.5f);
        }
    }

    // La cara dibujada a mano, recortada al círculo de la cabeza
    private static readonly Dictionary<string, Color32[]> drawings = new Dictionary<string, Color32[]>();

    public static Color32[] DecodeDrawing(string b64)
    {
        if (string.IsNullOrEmpty(b64)) return null;
        if (drawings.TryGetValue(b64, out var px)) return px;
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(Convert.FromBase64String(b64));
            px = tex.width == DrawSize && tex.height == DrawSize ? tex.GetPixels32() : null;
            UnityEngine.Object.Destroy(tex);
        }
        catch (FormatException) { px = null; }
        if (drawings.Count > 32) drawings.Clear();
        drawings[b64] = px;
        return px;
    }

    private static void Drawing(Canvas c, string b64, Vector2 h)
    {
        var px = DecodeDrawing(b64);
        if (px == null) return;
        float x0 = h.x - HeadR, y0 = h.y - HeadR, k = DrawSize / (HeadR * 2f);
        c.Shape(Box(h, HeadR), p => Circle(p - h, HeadR - 1f), p =>
        {
            int dx = Mathf.Clamp((int)((p.x - x0) * k), 0, DrawSize - 1);
            int dy = Mathf.Clamp((int)((p.y - y0) * k), 0, DrawSize - 1);
            return px[(DrawSize - 1 - dy) * DrawSize + dx];   // la textura de la cara también crece hacia arriba
        }, 0f);
    }

    // ---------------- Rellenos ----------------

    // Degradado brillante como el de los muñequitos: claro arriba a la izquierda, oscuro en el borde
    private static Func<Vector2, Color> Gloss(Color col, Vector2 light, float radius)
    {
        Color hi = Color.Lerp(col, Color.white, 0.55f), lo = Color.Lerp(col, Color.black, 0.32f);
        return p =>
        {
            float t = Mathf.Clamp01(Vector2.Distance(p, light) / radius);
            return t < 0.45f ? Color.Lerp(hi, col, t / 0.45f) : Color.Lerp(col, lo, (t - 0.45f) / 0.55f);
        };
    }

    private static Color Shine(Vector2 p, float top, float height, float strength) =>
        new Color(1f, 1f, 1f, strength * (1f - Mathf.Clamp01((p.y - top) / height)));

    private static Color Gold(Vector2 p)
    {
        float t = Mathf.Repeat(p.y, 40f) / 40f;
        return t < 0.5f ? Color.Lerp(new Color(1f, 0.95f, 0.63f), new Color(1f, 0.76f, 0.1f), t * 2f) : Color.Lerp(new Color(1f, 0.76f, 0.1f), new Color(0.79f, 0.54f, 0f), (t - 0.5f) * 2f);
    }

    private static Color Iridescent(Vector2 p)
    {
        float t = Mathf.Repeat((p.x + p.y) / 90f, 1f);
        Color a = new Color(0.62f, 0.91f, 1f), b = new Color(1f, 0.76f, 0.94f), c = new Color(1f, 0.95f, 0.63f), d = new Color(0.65f, 1f, 0.81f);
        Color col = t < 0.33f ? Color.Lerp(a, b, t / 0.33f) : t < 0.66f ? Color.Lerp(b, c, (t - 0.33f) / 0.33f) : Color.Lerp(c, d, (t - 0.66f) / 0.34f);
        col.a = 0.85f;
        return col;
    }

    private static Func<Vector2, Color> Stripes(Color col)
    {
        Color light = Color.Lerp(col, Color.white, 0.75f);
        return p => Mathf.Repeat(p.x, 16f) < 8f ? col : light;
    }

    private static Func<Vector2, Color> PartyFill(Color col, float top, float bottom)
    {
        var g = Gloss(col, new Vector2(122f, top + 20f), 60f);
        return p => Mathf.Repeat((p.y - top) / (bottom - top) * 3f, 1f) < 0.22f ? new Color(1f, 1f, 1f, 0.85f) : g(p);
    }

    // ---------------- Figuras (distancias con signo, en unidades del lienzo) ----------------

    private static Rect Box(Vector2 c, float r) => new Rect(c.x - r, c.y - r, r * 2f, r * 2f);

    private static Rect Bounds(Vector2 a, Vector2 b, float pad) =>
        Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);

    private static Vector2 Rot(Vector2 p, float deg)
    {
        float r = -deg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
    }

    private static float Circle(Vector2 p, float r) => p.magnitude - r;

    private static float Ellipse(Vector2 p, float rx, float ry) => (new Vector2(p.x / rx, p.y / ry).magnitude - 1f) * Mathf.Min(rx, ry);

    private static float RoundBox(Vector2 p, Vector2 half, float r)
    {
        var q = new Vector2(Mathf.Abs(p.x) - half.x + r, Mathf.Abs(p.y) - half.y + r);
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }

    private static float Triangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2, v0 = p - p0, v1 = p - p1, v2 = p - p2;
        Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
        Vector2 d = Vector2.Min(Vector2.Min(new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                                            new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
        return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
    }

    // Corazón: dos círculos arriba y un cuadrado girado abajo
    private static float Heart(Vector2 p, float s)
    {
        float r = s * 0.42f;
        float a = Circle(p - new Vector2(-s * 0.36f, -s * 0.2f), r);
        float b = Circle(p - new Vector2(s * 0.36f, -s * 0.2f), r);
        float d = RoundBox(Rot(p - new Vector2(0f, s * 0.16f), 45f), new Vector2(s * 0.48f, s * 0.48f), s * 0.08f);
        return Mathf.Min(Mathf.Min(a, b), d);
    }
}
