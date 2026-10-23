using UnityEngine;

// Todas las imágenes de la interfaz Aero (las genera Catapultas > Construir escenas).
// Paleta de la interfaz: azules acuáticos, cian, verdes, blancos y vidrio.
// Paleta de los jugadores: coral, sol, violeta y magenta, para que nunca se confundan con botones o paneles.
[CreateAssetMenu(menuName = "Catapultas/Aero Skin")]
public class AeroSkin : ScriptableObject
{
    [Header("Botones tipo gel (interfaz)")]
    public Sprite buttonGreen;
    public Sprite buttonBlue;
    public Sprite buttonSilver;
    public Sprite buttonGold;     // aro y variantes especiales en la tienda

    [Header("Píldoras de jugador (mismo gel, colores de jugador)")]
    public Sprite[] playerButtons;

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
    public Color[] playerColors =
    {
        new Color(1f, 0.37f, 0.34f),    // Coral
        new Color(1f, 0.76f, 0.1f),     // Sol
        new Color(0.61f, 0.42f, 1f),    // Violeta
        new Color(0.94f, 0.28f, 0.62f), // Magenta
    };
    public string[] playerColorNames = { "Coral", "Sol", "Violeta", "Magenta" };

    public Sprite PlayerButton(int player) =>
        playerButtons != null && playerButtons.Length > 0 ? playerButtons[Mathf.Abs(player) % playerButtons.Length] : buttonBlue;

    public Color PlayerColor(int player) =>
        playerColors != null && playerColors.Length > 0 ? playerColors[Mathf.Abs(player) % playerColors.Length] : Color.white;

    // Texto que se lee sobre el color del jugador (el amarillo necesita texto oscuro)
    public Color TextOn(int player)
    {
        Color c = PlayerColor(player);
        return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b > 0.68f ? navy : Color.white;
    }
}
