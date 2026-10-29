using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Crea el juego para Windows: una carpeta con "Fresh Kingdom Showdown.exe" que se abre con doble clic.
// Menú: Catapultas → Crear ejecutable (Windows). Queda en Builds/Windows (git no lo sube).
// Desde la línea de comandos: Unity.exe -batchmode -projectPath <proyecto> -executeMethod BuildGame.BuildWindows
public static class BuildGame
{
    public const string Folder = "Builds/Windows";
    public const string ExeName = "Fresh Kingdom Showdown.exe";
    private const string IconPath = "Assets/Arte/Icono.png";
    private static readonly string[] Scenes = { "Assets/Scenes/MenuPrincipal.unity", "Assets/Scenes/Catapultas.unity" };

    [MenuItem("Catapultas/Crear ejecutable (Windows)")]
    public static void BuildFromMenu()
    {
        var report = Build();
        if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(Path.Combine(Folder, ExeName));
    }

    public static void BuildWindows()
    {
        var report = Build();
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static BuildReport Build()
    {
        ApplyPlayerSettings();
        if (Directory.Exists(Folder)) Directory.Delete(Folder, true);   // sin restos de builds anteriores
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,   // solo el menú y el juego (SampleScene no va)
            locationPathName = Path.Combine(Folder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4HC,
        });
        var s = report.summary;
        // Unity deja esta carpeta solo para depurar; no hace falta para jugar
        string backup = Path.Combine(Folder, Path.GetFileNameWithoutExtension(ExeName) + "_BackUpThisFolder_ButDontShipItWithYourGame");
        if (s.result == BuildResult.Succeeded && Directory.Exists(backup)) Directory.Delete(backup, true);
        Debug.Log($"[Build] {s.result}: {s.totalErrors} errores, {s.totalTime.TotalSeconds:0} s → {Path.GetFullPath(Folder)}");
        return report;
    }

    private static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = "Fresh Kingdom Showdown";
        PlayerSettings.companyName = "SixVSSeven";
        PlayerSettings.bundleVersion = "0.4";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;   // pantalla completa; Alt+Enter cambia a ventana
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.SplashScreen.show = false;   // sin el logo de Unity al abrir
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);   // quita el código .NET que no se usa
        var icon = EnsureIcon();
        if (icon) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
    }

    // Ícono del .exe: una burbuja Aero azul y brillante (se dibuja una vez y queda en Assets/Arte)
    private static Texture2D EnsureIcon()
    {
        if (!File.Exists(IconPath))
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            float r = size * 0.46f;
            Color top = new Color(0.55f, 0.88f, 1f), bottom = new Color(0.04f, 0.32f, 0.72f), rim = new Color(0.02f, 0.2f, 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Vector2.Distance(p, c) / r;
                    if (d > 1.02f) { px[y * size + x] = Color.clear; continue; }
                    float v = (p.y - (c.y - r)) / (2f * r);                    // 0 abajo, 1 arriba
                    Color col = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    col = Color.Lerp(col, new Color(0.3f, 0.95f, 1f), Mathf.Clamp01(1f - v * 2.2f) * 0.55f * d);   // brillo cian abajo
                    col = Color.Lerp(col, rim, Mathf.SmoothStep(0.86f, 1f, d) * 0.8f);
                    // Reflejo de arriba: una elipse blanca que se desvanece
                    Vector2 h = new Vector2((p.x - c.x) / (r * 0.68f), (p.y - (c.y + r * 0.45f)) / (r * 0.36f));
                    float hl = Mathf.Clamp01(1f - h.sqrMagnitude);
                    col = Color.Lerp(col, Color.white, hl * hl * 0.85f);
                    col.a = Mathf.Clamp01((1.02f - d) / 0.03f);
                    px[y * size + x] = col;
                }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
    }
}
