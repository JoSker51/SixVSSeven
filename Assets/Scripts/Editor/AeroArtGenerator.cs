using System.IO;
using UnityEditor;
using UnityEngine;

// Genera por código el arte del estilo Frutiger Aero: botones de gel, vidrio, burbujas, orbes,
// barras, rayos de aurora, el cielo con nubes y las texturas de césped. Todo queda en Assets/Arte.
public static class AeroArtGenerator
{
    public const string Folder = "Assets/Arte";
    public const string SkinPath = Folder + "/AeroSkin.asset";
    public const string SkyPath = Folder + "/Texturas/Cielo.png";
    public const string GrassGridPath = Folder + "/Texturas/CespedCuadricula.png";
    public const string GrassPath = Folder + "/Texturas/Cesped.png";

    public static AeroSkin GenerateAll(Vector3 sunDirection)
    {
        EnsureFolder("Assets", "Arte");
        EnsureFolder(Folder, "UI");
        EnsureFolder(Folder, "Texturas");

        var skin = AssetDatabase.LoadAssetAtPath<AeroSkin>(SkinPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<AeroSkin>();
            AssetDatabase.CreateAsset(skin, SkinPath);
        }

        var pill = new Vector4(80, 80, 80, 80);
        skin.buttonGreen = SaveSprite("BotonVerde", GelButton(512, 160, Hex(0xC4F77E), Hex(0x2FA51F), Hex(0x1C6E12)), 512, 160, pill);
        skin.buttonBlue = SaveSprite("BotonAzul", GelButton(512, 160, Hex(0xA8E8FF), Hex(0x1679DA), Hex(0x0B4E93)), 512, 160, pill);
        skin.buttonOrange = SaveSprite("BotonNaranja", GelButton(512, 160, Hex(0xFFD79A), Hex(0xF17C0C), Hex(0xA34C00)), 512, 160, pill);
        skin.buttonSilver = SaveSprite("BotonPlata", GelButton(512, 160, Hex(0xFFFFFF), Hex(0xB4C6D4), Hex(0x6E8597)), 512, 160, pill);
        skin.glassPanel = SaveSprite("Vidrio", GlassPanel(256, 256, 44f), 256, 256, new Vector4(56, 56, 56, 56));
        skin.bubble = SaveSprite("Burbuja", Bubble(256), 256, 256, Vector4.zero);
        skin.orb = SaveSprite("Orbe", Orb(128), 128, 128, Vector4.zero);
        skin.orbGloss = SaveSprite("OrbeBrillo", OrbGloss(128), 128, 128, Vector4.zero);
        skin.glow = SaveSprite("Resplandor", Glow(128), 128, 128, Vector4.zero);
        skin.swoosh = SaveSprite("Aurora", Swoosh(1024, 256), 1024, 256, Vector4.zero);
        skin.track = SaveSprite("Riel", Track(256, 56), 256, 56, new Vector4(28, 28, 28, 28));
        skin.fill = SaveSprite("Relleno", Fill(256, 56), 256, 56, new Vector4(28, 28, 28, 28));
        skin.navy = new Color(0.04f, 0.24f, 0.4f);
        skin.playerColors = new[] { new Color(0.17f, 0.55f, 1f), new Color(1f, 0.55f, 0.1f) };
        EditorUtility.SetDirty(skin);

        SaveTexture(SkyPath, Sky(2048, 1024, sunDirection.normalized), 2048, 1024, sky: true);
        SaveTexture(GrassGridPath, Grass(256, true), 256, 256, sky: false);
        SaveTexture(GrassPath, Grass(256, false), 256, 256, sky: false);

        AssetDatabase.SaveAssets();
        return skin;
    }

    // ---------------- Interfaz ----------------

    // Píldora de gel: degradado, brillo arriba con borde marcado, resplandor abajo y contorno
    private static Color[] GelButton(int w, int h, Color top, Color bottom, Color border)
    {
        var px = new Color[w * h];
        float r = h / 2f - 2f;
        Color glowCol = Color.Lerp(top, Color.white, 0.5f);
        float gTop = h - 7f, gBot = h * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f, v = fy / h;
                float d = RoundRect(fx, fy, w / 2f, h / 2f, w / 2f - 2f, h / 2f - 2f, r);
                float cover = Sat(0.5f - d);
                if (cover <= 0f) { px[y * w + x] = Color.clear; continue; }

                Color c = Color.Lerp(bottom, top, Mathf.Pow(v, 0.9f));
                float gx = (fx - w / 2f) / (w * 0.38f), gy = v / 0.3f;
                c = Color.Lerp(c, glowCol, Mathf.Exp(-gx * gx - gy * gy) * 0.6f);

                float dg = RoundRect(fx, fy, w / 2f, (gTop + gBot) / 2f, w / 2f - 12f, (gTop - gBot) / 2f, Mathf.Min(r - 10f, (gTop - gBot) / 2f));
                c = Color.Lerp(c, Color.white, Sat(0.5f - dg) * Mathf.Lerp(0.12f, 0.8f, Smooth(0.5f, 1f, v)));

                c = Color.Lerp(c, Color.white, Sat(1f - Mathf.Abs(d + 4.5f) / 1.5f) * 0.45f);
                c = Color.Lerp(c, border, Smooth(-3f, -0.5f, d));
                c.a = cover;
                px[y * w + x] = c;
            }
        return px;
    }

    // Vidrio esmerilado: blanco translúcido, brillo arriba, línea celeste interior y borde blanco
    private static Color[] GlassPanel(int w, int h, float r)
    {
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f, v = fy / h;
                float d = RoundRect(fx, fy, w / 2f, h / 2f, w / 2f - 2f, h / 2f - 2f, r);
                float cover = Sat(0.5f - d);
                if (cover <= 0f) { px[y * w + x] = Color.clear; continue; }

                Color c = Color.Lerp(new Color(0.8f, 0.93f, 1f), Color.white, v);
                float a = Mathf.Lerp(0.74f, 0.86f, v);
                float gloss = Smooth(0.8f, 1f, v) * 0.5f;
                c = Color.Lerp(c, Color.white, gloss);
                a = Mathf.Lerp(a, 0.95f, gloss);
                float inner = Sat(1f - Mathf.Abs(d + 5f) / 1.2f) * 0.6f;
                c = Color.Lerp(c, new Color(0.55f, 0.85f, 1f), inner);
                a = Mathf.Lerp(a, 0.95f, inner);
                float edge = Smooth(-3f, -0.5f, d);
                c = Color.Lerp(c, Color.white, edge);
                a = Mathf.Lerp(a, 1f, edge);
                c.a = a * cover;
                px[y * w + x] = c;
            }
        return px;
    }

    // Burbuja de jabón: casi transparente al centro, borde tornasolado y reflejos
    private static Color[] Bubble(int s)
    {
        var px = new Color[s * s];
        float c0 = s / 2f, R = s / 2f - 6f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f - c0) / R, dy = (y + 0.5f - c0) / R;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Sat((1f - r) * R + 0.5f);
                if (cover <= 0f) { px[y * s + x] = Color.clear; continue; }

                float rim = Smooth(0.7f, 1f, r);
                float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + r * 0.35f, 1f);
                Color c = Color.Lerp(Color.white, Color.HSVToRGB(hue, 0.35f, 1f), rim * 0.6f);
                float a = 0.08f + 0.6f * rim * rim;

                float edge = Smooth(0.94f, 0.985f, r);
                c = Color.Lerp(c, new Color(0.75f, 0.92f, 1f), edge * 0.5f);
                a = Mathf.Max(a, edge * 0.85f);

                float sx = dx + 0.38f, sy = dy - 0.38f;
                float ut = (sx + sy) * 0.7071f, ur = (-sx + sy) * 0.7071f;
                float spec = 1f - Smooth(0.5f, 1f, (ut / 0.3f) * (ut / 0.3f) + (ur / 0.14f) * (ur / 0.14f));
                // Reflejo en arco abajo a la derecha (un punto se confundía con un "." al lado de los números)
                float ang = Mathf.Atan2(dy, dx) + 0.785f;
                float arc = Smooth(0.8f, 0.88f, r) * (1f - Smooth(0.9f, 0.96f, r)) * Mathf.Exp(-ang * ang / 0.15f) * 0.6f;
                float s2 = Mathf.Max(spec * 0.95f, arc);
                c = Color.Lerp(c, Color.white, s2);
                a = Mathf.Max(a, s2);

                c.a = a * cover;
                px[y * s + x] = c;
            }
        return px;
    }

    // Esfera en escala de grises (se tiñe con Image.color)
    private static Color[] Orb(int s)
    {
        var px = new Color[s * s];
        float c0 = s / 2f, R = s / 2f - 2f;
        Vector3 L = new Vector3(-0.35f, 0.55f, 0.75f).normalized;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f - c0) / R, dy = (y + 0.5f - c0) / R;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Sat((1f - r) * R + 0.5f);
                if (cover <= 0f) { px[y * s + x] = Color.clear; continue; }

                var n = new Vector3(dx, dy, Mathf.Sqrt(Mathf.Max(0f, 1f - r * r)));
                float shade = Mathf.Lerp(0.55f, 1f, Sat(Vector3.Dot(n, L)));
                shade += 0.3f * Mathf.Exp(-(dy + 0.72f) * (dy + 0.72f) / 0.04f) * Smooth(0f, 0.9f, 1f - Mathf.Abs(dx));
                shade *= Mathf.Lerp(1f, 0.78f, Smooth(0.85f, 1f, r));
                shade = Mathf.Min(1f, shade);
                px[y * s + x] = new Color(shade, shade, shade, cover);
            }
        return px;
    }

    private static Color[] OrbGloss(int s)
    {
        var px = new Color[s * s];
        float c0 = s / 2f, R = s / 2f - 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f - c0) / R, dy = (y + 0.5f - c0) / R;
                float e = (dx / 0.68f) * (dx / 0.68f) + ((dy - 0.42f) / 0.45f) * ((dy - 0.42f) / 0.45f);
                float a = (1f - Smooth(0.75f, 1f, e)) * Mathf.Lerp(0.05f, 0.85f, Smooth(-0.1f, 0.85f, dy));
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        return px;
    }

    private static Color[] Glow(int s)
    {
        var px = new Color[s * s];
        float c0 = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f - c0) / c0, dy = (y + 0.5f - c0) / c0;
                float r2 = dx * dx + dy * dy;
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Exp(-r2 * 3.5f) * (1f - Smooth(0.8f, 1f, Mathf.Sqrt(r2))));
            }
        return px;
    }

    // Cintas de luz curvas (las "auroras" típicas del estilo)
    private static Color[] Swoosh(int w, int h)
    {
        var px = new Color[w * h];
        float[,] bands =
        {   // amplitud, frecuencia, fase, ancho, intensidad, desplazamiento
            { 46f, 1.0f, 0.0f, 22f, 0.55f, 0f },
            { 38f, 1.3f, 1.2f, 9f, 0.8f, 10f },
            { 60f, 0.8f, 2.4f, 34f, 0.3f, -10f },
            { 30f, 1.6f, 0.6f, 4f, 0.9f, 20f },
        };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, fy = y + 0.5f;
                float fade = Smooth(0f, 0.18f, u) * Smooth(1f, 0.82f, u);
                float a = 0f;
                for (int b = 0; b < bands.GetLength(0); b++)
                {
                    float yc = h / 2f + bands[b, 5] + bands[b, 0] * Mathf.Sin(2f * Mathf.PI * u * bands[b, 1] + bands[b, 2]);
                    float dy = (fy - yc) / bands[b, 3];
                    a += bands[b, 4] * Mathf.Exp(-dy * dy);
                }
                a = Sat(a) * fade;
                Color c = Color.Lerp(new Color(0.8f, 0.96f, 1f), Color.white, a);
                c.a = a;
                px[y * w + x] = c;
            }
        return px;
    }

    // Riel hundido para barras y sliders
    private static Color[] Track(int w, int h)
    {
        var px = new Color[w * h];
        Color navy = new Color(0.04f, 0.24f, 0.4f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f, v = fy / h;
                float d = RoundRect(fx, fy, w / 2f, h / 2f, w / 2f - 1f, h / 2f - 1f, h / 2f - 1f);
                float cover = Sat(0.5f - d);
                if (cover <= 0f) { px[y * w + x] = Color.clear; continue; }

                Color c = navy;
                float a = Mathf.Lerp(0.2f, 0.42f, v);
                float hl = Sat(1f - Mathf.Abs(d + 2.5f) / 1.5f) * Smooth(0.55f, 0f, v);
                c = Color.Lerp(c, Color.white, hl);
                a = Mathf.Lerp(a, 0.8f, hl);
                float edge = Smooth(-2f, -0.3f, d) * 0.6f;
                c = Color.Lerp(c, Color.white, edge);
                a = Mathf.Lerp(a, 0.7f, edge);
                c.a = a * cover;
                px[y * w + x] = c;
            }
        return px;
    }

    // Relleno brillante en gris (se tiñe con Image.color)
    private static Color[] Fill(int w, int h)
    {
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f, v = fy / h;
                float d = RoundRect(fx, fy, w / 2f, h / 2f, w / 2f - 1f, h / 2f - 1f, h / 2f - 1f);
                float cover = Sat(0.5f - d);
                if (cover <= 0f) { px[y * w + x] = Color.clear; continue; }

                float g = Mathf.Lerp(0.7f, 1f, v);
                Color c = new Color(g, g, g);
                float dg = RoundRect(fx, fy, w / 2f, h * 0.74f, w / 2f - 8f, h * 0.2f, h * 0.2f);
                c = Color.Lerp(c, Color.white, Sat(0.5f - dg) * 0.65f);
                c = Color.Lerp(c, new Color(0.55f, 0.55f, 0.55f), Smooth(-2f, -0.3f, d));
                c.a = cover;
                px[y * w + x] = c;
            }
        return px;
    }

    // ---------------- Texturas del mundo ----------------

    // Cielo 360° (latitud-longitud) con degradado azul, nubes esponjosas y el sol en la dirección de la luz
    private static Color[] Sky(int w, int h, Vector3 sun)
    {
        var px = new Color[w * h];
        Color zenith = new Color(0.1f, 0.42f, 0.88f), mid = new Color(0.36f, 0.7f, 0.98f), horizon = new Color(0.86f, 0.97f, 1f);
        Color sea = new Color(0.16f, 0.62f, 0.85f), seaDeep = new Color(0.06f, 0.36f, 0.62f);
        Color sunCol = new Color(1f, 0.97f, 0.88f);

        for (int y = 0; y < h; y++)
        {
            float lat = (1f - (y + 0.5f) / h) * Mathf.PI;
            float sinLat = Mathf.Sin(lat), el = Mathf.Cos(lat);
            for (int x = 0; x < w; x++)
            {
                float lon = (0.5f - (x + 0.5f) / w) * 2f * Mathf.PI;
                var dir = new Vector3(sinLat * Mathf.Cos(lon), el, sinLat * Mathf.Sin(lon));
                Color c;
                if (el >= 0f)
                {
                    float t = Mathf.Pow(el, 0.45f);
                    c = t < 0.5f ? Color.Lerp(horizon, mid, t / 0.5f) : Color.Lerp(mid, zenith, (t - 0.5f) / 0.5f);

                    // Nubes proyectadas sobre un plano (se ven más chicas cerca del horizonte, sin costuras)
                    float k = 1.1f / (el + 0.12f);
                    float n = Fbm(dir.x * k + 10f, dir.z * k + 20f, 5);
                    float cloud = Smooth(0.5f, 0.7f, n) * Smooth(0f, 0.1f, el) * (1f - 0.55f * Smooth(0.45f, 1f, el));
                    Color cloudCol = Color.Lerp(new Color(0.84f, 0.91f, 0.98f), Color.white, Smooth(0.55f, 0.78f, n));
                    c = Color.Lerp(c, cloudCol, cloud);
                }
                else
                {
                    float t = Mathf.Sqrt(-el);
                    c = Color.Lerp(horizon * 0.95f, sea, Smooth(0f, 0.15f, t));
                    c = Color.Lerp(c, seaDeep, Smooth(0.15f, 1f, t));
                }

                float sd = Mathf.Max(0f, Vector3.Dot(dir, sun));
                c += sunCol * (Mathf.Pow(sd, 900f) * 3f + Mathf.Pow(sd, 60f) * 0.35f + Mathf.Pow(sd, 8f) * 0.08f);
                c.r = Sat(c.r); c.g = Sat(c.g); c.b = Sat(c.b); c.a = 1f;
                px[y * w + x] = c;
            }
        }
        return px;
    }

    // Césped brillante que se repite sin costuras; con o sin cuadrícula
    private static Color[] Grass(int s, bool grid)
    {
        var px = new Color[s * s];
        int cell = s / 2;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float n = TileNoise(x / 16f, y / 16f, s / 16, 1) * 0.6f + TileNoise(x / 4f, y / 4f, s / 4, 2) * 0.4f;
                Color c;
                if (grid)
                {
                    bool checker = ((x / cell) + (y / cell)) % 2 == 0;
                    c = checker ? new Color(0.5f, 0.83f, 0.32f) : new Color(0.43f, 0.76f, 0.27f);
                }
                else c = new Color(0.45f, 0.78f, 0.29f);
                c *= Mathf.Lerp(0.92f, 1.08f, n);

                if (grid)
                {
                    int lx = x % cell, ly = y % cell;
                    float line = (lx < 3 || ly < 3) ? 1f : (lx < 5 || ly < 5) ? 0.4f : 0f;
                    c = Color.Lerp(c, new Color(0.78f, 0.96f, 0.6f), line * 0.7f);
                }
                c.a = 1f;
                px[y * s + x] = c;
            }
        return px;
    }

    // ---------------- Guardado ----------------

    private static Sprite SaveSprite(string name, Color[] px, int w, int h, Vector4 border)
    {
        string path = $"{Folder}/UI/{name}.png";
        WritePng(path, px, w, h);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = border;
        imp.spritePixelsPerUnit = 100f;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SaveTexture(string path, Color[] px, int w, int h, bool sky)
    {
        WritePng(path, px, w, h);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.maxTextureSize = 2048;
        if (sky)
        {
            imp.mipmapEnabled = false;   // evita una costura en el panorama
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;   // sin bandas en el degradado
        }
        else
        {
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 8;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
        }
        imp.filterMode = FilterMode.Trilinear;
        imp.SaveAndReimport();
    }

    private static void WritePng(string path, Color[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    // ---------------- Matemática ----------------

    private static float Sat(float x) => Mathf.Clamp01(x);

    private static float Smooth(float a, float b, float x)
    {
        float t = Sat((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // Distancia con signo a un rectángulo redondeado (negativa adentro)
    private static float RoundRect(float px, float py, float cx, float cy, float hw, float hh, float r)
    {
        float qx = Mathf.Abs(px - cx) - (hw - r), qy = Mathf.Abs(py - cy) - (hh - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x, y) * amp;
            norm += amp;
            x *= 2.03f; y *= 2.03f;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    // Ruido de valor que se repite cada 'period' celdas (para texturas sin costuras)
    private static float TileNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
        float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Hash(int i, int j, int period, int seed)
    {
        i = ((i % period) + period) % period;
        j = ((j % period) + period) % period;
        unchecked
        {
            uint h = (uint)(i * 374761393 + j * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
}
