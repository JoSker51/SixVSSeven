using UnityEngine;

// Un solo objeto en la escena con los datos del mapa que usan los huecos para calcular puntos.
public class MapSettings : MonoBehaviour
{
    public static MapSettings Instance { get; private set; }

    [Tooltip("Las 4 esquinas de la cuadrícula (objetos vacíos)")]
    public Transform[] corners;

    [Tooltip("Radio de un hueco 'normal': da la base de puntos")]
    public float referenceRadius = 1.5f;

    [Tooltip("Puntos base de un hueco normal")]
    public int basePoints = 1;

    [Tooltip("Distancia a la esquina que cuenta como 'lo más lejos posible'")]
    public float maxCornerDistance = 20f;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    // Distancia horizontal (XZ) a la esquina más cercana
    public float DistanceToNearestCorner(Vector3 position)
    {
        float best = float.MaxValue;
        foreach (var c in corners)
        {
            Vector3 d = c.position - position;
            d.y = 0f;
            best = Mathf.Min(best, d.magnitude);
        }
        return best;
    }
}
