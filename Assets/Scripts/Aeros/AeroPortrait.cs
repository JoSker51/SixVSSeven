using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Retrato en vivo de un Aero para la interfaz: el Aero 3D vive en un "estudio" lejos de la arena,
// una cámara propia lo filma a una textura y un RawImage la muestra (dentro de una burbuja o un marco).
public class AeroPortrait : MonoBehaviour
{
    public enum Framing { Body, Bust }

    private static int nextSlot;

    private RawImage image;
    private RenderTexture rt;
    private Transform studio;
    private Camera cam;
    private AeroFigure figure;
    private Framing framing;

    public AeroFigure Figure => figure;

    // Crea el retrato como hijo de "parent", ocupando todo su espacio
    public static AeroPortrait Create(Transform parent, AeroLook look, Color background, Framing framing, int resolution = 256)
    {
        var node = AeroUI.Node("Retrato", parent).Stretch();
        var img = node.gameObject.AddComponent<RawImage>();
        img.raycastTarget = false;
        var p = node.gameObject.AddComponent<AeroPortrait>();
        p.image = img;
        p.framing = framing;
        p.Setup(look, background, resolution);
        return p;
    }

    // Retrato dentro de una burbuja: círculo del color del jugador, el Aero adentro y el brillo encima.
    // "holder" ya viene ubicado y con su tamaño; la burbuja lo llena.
    public static AeroPortrait Bubble(RectTransform holder, AeroSkin skin, AeroLook look, Color color, Framing framing = Framing.Bust, int resolution = 192)
    {
        var maskImg = AeroUI.Img(holder, "Burbuja", skin.orb, Color.white);
        maskImg.rectTransform.Stretch();
        maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var p = Create(maskImg.transform, look, Color.Lerp(color, Color.white, 0.55f), framing, resolution);
        AeroUI.Img(holder, "Brillo", skin.orbGloss, new Color(1f, 1f, 1f, 0.35f)).rectTransform.Stretch();
        return p;
    }

    private void Setup(AeroLook look, Color background, int resolution)
    {
        // Cada retrato tiene su lugar en el estudio, muy por debajo del mapa: ninguna cámara del juego llega ahí
        int slot = nextSlot++;
        studio = new GameObject($"Estudio Aero {slot}").transform;
        studio.position = new Vector3((slot % 16) * 30f, -4000f, (slot / 16) * 30f);

        figure = AeroFigure.Create(look, studio, "Aero");
        figure.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // mirando a la cámara
        foreach (var r in figure.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        cam = new GameObject("Camara").AddComponent<Camera>();
        cam.transform.SetParent(studio, false);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 25f;
        cam.fieldOfView = 26f;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderShadows = false;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        Frame();

        // Luz suave propia: la del juego también llega, esta rellena por delante
        var fill = new GameObject("Luz").AddComponent<Light>();
        fill.transform.SetParent(studio, false);
        fill.type = LightType.Point;
        fill.range = 14f;
        fill.intensity = 3f;
        fill.color = new Color(0.9f, 0.96f, 1f);
        fill.transform.localPosition = new Vector3(-2.5f, 4.5f, -5f);

        rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "Retrato Aero" };
        cam.targetTexture = rt;
        image.texture = rt;
    }

    private void Frame()
    {
        if (framing == Framing.Bust)
        {
            // Cabeza y hombros: lo importante en una burbuja chica (sombrero, gafas, bufanda)
            float headY = 0.94f + 1.4f * Mathf.Clamp(figure.Look.h, AeroLook.MinH, AeroLook.MaxH);
            cam.transform.localPosition = new Vector3(0f, headY - 0.15f, -5.4f);
            cam.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
        }
        else
        {
            cam.transform.localPosition = new Vector3(0f, 2.2f, -10.5f);
            cam.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        }
    }

    public void SetLook(AeroLook look)
    {
        figure.Build(look);
        foreach (var r in figure.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Frame();
    }

    public void SetBackground(Color c) { if (cam) cam.backgroundColor = c; }

    public void Hop() { if (figure) figure.Hop(); }

    private void OnEnable() { if (cam) cam.enabled = true; }
    private void OnDisable() { if (cam) cam.enabled = false; }

    private void OnDestroy()
    {
        if (studio) Destroy(studio.gameObject);
        if (rt) { rt.Release(); Destroy(rt); }
    }
}
