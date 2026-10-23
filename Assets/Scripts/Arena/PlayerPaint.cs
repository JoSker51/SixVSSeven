using UnityEngine;

// Marca qué partes de un castillo o catapulta llevan el color del jugador.
public class PlayerPaint : MonoBehaviour
{
    [SerializeField] private Renderer[] colorParts;
    [SerializeField] private Renderer[] glowParts;   // el indicador de turno (material con brillo)

    public void Apply(Material color, Material glow)
    {
        foreach (var r in colorParts) if (r) r.sharedMaterial = color;
        if (glow) foreach (var r in glowParts) if (r) r.sharedMaterial = glow;
    }
}
