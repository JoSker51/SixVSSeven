using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ScoreHole : MonoBehaviour
{
    [SerializeField] private TMP_Text label;   // opcional: texto "+2" sobre el hueco
    [SerializeField] private int manualOverride = 0;   // si es > 0, ignora la fórmula

    public int Points { get; private set; }
    private float radius;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
    }

    private void Start()
    {
        Points = manualOverride > 0 ? manualOverride : ComputePoints();
        if (label) label.text = "+" + Points;
    }

    // Más pequeño = más puntos; más lejos de las esquinas = más puntos
    private int ComputePoints()
    {
        var map = MapSettings.Instance;
        if (map == null) return 1;

        float sizeFactor = Mathf.Clamp(map.referenceRadius / radius, 0.5f, 3f);
        float distFactor = Mathf.Clamp01(map.DistanceToNearestCorner(transform.position) / map.maxCornerDistance);
        return Mathf.Max(1, Mathf.RoundToInt(map.basePoints * sizeFactor * (1f + distFactor)));
    }

    private void OnTriggerEnter(Collider other) { TryScore(other); }
    private void OnTriggerStay(Collider other) { TryScore(other); }

    private void TryScore(Collider other)
    {
        var rock = other.GetComponentInParent<Rock>();
        if (rock == null || rock.Consumed) return;

        // La roca solo cuenta si su centro está dentro del hueco (no basta rozar el borde)
        Vector3 d = rock.transform.position - transform.position;
        d.y = 0f;
        if (d.magnitude > radius) return;

        if (!rock.TryConsume()) return;

        int pts = Mathf.Max(1, Mathf.RoundToInt(Points * rock.Multiplier));
        GameManager.Instance.AddCoins(rock.OwnerId, pts);
        rock.SinkInto(transform.position);
        AeroAudio.Play(Sfx.Score);
        GameHUD.Popup(transform.position, "+" + pts, new Color(1f, 0.82f, 0.2f));
        Debug.Log($"Jugador {rock.OwnerId} encesta: +{pts}");
    }
}
