using UnityEngine;

// Todas las imágenes de la interfaz Aero (las genera Catapultas > Construir escenas).
[CreateAssetMenu(menuName = "Catapultas/Aero Skin")]
public class AeroSkin : ScriptableObject
{
    [Header("Botones tipo gel")]
    public Sprite buttonGreen;
    public Sprite buttonBlue;
    public Sprite buttonOrange;
    public Sprite buttonSilver;

    [Header("Vidrio y adornos")]
    public Sprite glassPanel;
    public Sprite bubble;
    public Sprite orb;        // esfera en gris para teñir
    public Sprite orbGloss;   // brillo blanco que va encima del orbe
    public Sprite glow;
    public Sprite swoosh;

    [Header("Barras")]
    public Sprite track;
    public Sprite fill;       // en gris para teñir

    [Header("Colores")]
    public Color navy = new Color(0.04f, 0.24f, 0.4f);
    public Color[] playerColors = { new Color(0.17f, 0.55f, 1f), new Color(1f, 0.55f, 0.1f) };

    public Sprite PlayerButton(int player) => player % 2 == 0 ? buttonBlue : buttonOrange;

    public Color PlayerColor(int player) =>
        playerColors != null && playerColors.Length > 0 ? playerColors[Mathf.Abs(player) % playerColors.Length] : Color.white;
}
