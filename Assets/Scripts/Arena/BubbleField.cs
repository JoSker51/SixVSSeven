using System.Collections.Generic;
using UnityEngine;

// Genera las burbujas en posiciones aleatorias válidas: dentro del círculo, repartidas en partes iguales
// por sección (una por jugador), sin encimarse entre sí ni con castillos, catapultas, paredes u objetos.
// Las burbujas reventadas reaparecen al final del turno en un lugar libre de su misma sección.
public class BubbleField : MonoBehaviour
{
    public static BubbleField Instance { get; private set; }

    [SerializeField] private Bubble bubblePrefab;

    private readonly List<Bubble> bubbles = new List<Bubble>();
    private readonly List<(Bubble b, Vector3 pos, float radius, int owner, Color color)> castleBubbles = new List<(Bubble, Vector3, float, int, Color)>();
    private System.Random rng;

    public IReadOnlyList<Bubble> Bubbles => bubbles;
    public int Seed { get; private set; }
    public int Missing { get; private set; }   // burbujas que no encontraron lugar (se reintenta al terminar cada turno)

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Generate(int seed)
    {
        Seed = seed;
        rng = new System.Random(seed);
        Physics.SyncTransforms();
        foreach (var b in bubbles) if (b) Destroy(b.gameObject);
        bubbles.Clear();

        var arena = Arena.Instance;
        int perSection = GameBalance.Current.bubblesPerSection;
        Missing = 0;
        for (int s = 0; s < arena.Sections; s++)
            for (int k = 0; k < perSection; k++)
            {
                var b = Instantiate(bubblePrefab, transform);
                b.name = $"Burbuja S{s + 1}-{k + 1}";
                bubbles.Add(b);
                if (!TryPlace(b, s)) Hide(b, s);
            }
        Physics.SyncTransforms();
        if (Missing > 0) Debug.LogWarning($"[Catapultas] {Missing} burbujas no encontraron lugar libre; se reintentará al final del turno.");
    }

    // La burbuja de cada castillo siempre vuelve al mismo lugar, encima de su castillo
    public void AddCastleBubble(Bubble b, Vector3 position, float radius, int owner, Color color)
    {
        b.PlaceAsCastle(position, radius, owner, color);
        castleBubbles.Add((b, position, radius, owner, color));
    }

    // Final de turno: las burbujas reventadas (o sin lugar) vuelven a aparecer en su sección
    public int RespawnPopped()
    {
        foreach (var (b, pos, r, owner, color) in castleBubbles)
            if (b && b.Popped) b.PlaceAsCastle(pos, r, owner, color);

        int placed = 0;
        Missing = 0;
        foreach (var b in bubbles)
        {
            if (!b.Popped) continue;
            if (TryPlace(b, b.Section)) placed++;
            else Hide(b, b.Section);
        }
        Physics.SyncTransforms();
        return placed;
    }

    private void Hide(Bubble b, int section)
    {
        Missing++;
        b.HideUntilRespawn(section);
    }

    private bool TryPlace(Bubble b, int section)
    {
        var bal = GameBalance.Current;
        var arena = Arena.Instance;
        float half = arena.SectionWidth / 2f;
        for (int attempt = 0; attempt < bal.bubbleMaxAttempts; attempt++)
        {
            // Plan B en los últimos intentos: se achica la separación pedida (nunca se permite encimar)
            float gapScale = attempt < bal.bubbleMaxAttempts * 3 / 4 ? 1f : 0.6f;
            float radius = Mathf.Lerp(bal.bubbleRadius.x, bal.bubbleRadius.y, Next());
            float angle = arena.SectionCenter(section) + (Next() * 2f - 1f) * half;
            float rMin = bal.bubbleCenterMargin + radius;
            float rMax = arena.Radius - bal.bubbleWallMargin - radius;
            if (rMax <= rMin) return false;
            float dist = Mathf.Sqrt(Mathf.Lerp(rMin * rMin, rMax * rMax, Next()));   // reparto parejo en el área
            float height = Mathf.Lerp(bal.bubbleHeight.x, bal.bubbleHeight.y, Next());
            Vector3 pos = arena.Center + Arena.Direction(angle) * dist + Vector3.up * height;

            if (arena.SectionOf(pos) != section) continue;
            if (!IsFree(pos, radius, b, gapScale)) continue;

            b.Place(pos, radius, ValueFor(pos, radius), section);
            return true;
        }
        return false;
    }

    public bool IsFree(Vector3 pos, float radius, Bubble self, float gapScale = 1f)
    {
        var bal = GameBalance.Current;
        var arena = Arena.Instance;
        if (!arena.InsideHorizontal(pos, radius + bal.bubbleWallMargin)) return false;
        if (pos.y - radius < 0.3f) return false;                          // nada de burbujas enterradas
        if (arena.InKeepOut(pos, radius)) return false;

        foreach (var o in Bubble.All)
        {
            if (o == self || o.Popped || !o.isActiveAndEnabled) continue;
            if (Vector3.Distance(o.transform.position, pos) < radius + o.Radius + bal.bubbleMinGap * gapScale) return false;
        }

        // Física real: no tocar objetos, castillos, catapultas ni paredes (el suelo no cuenta)
        foreach (var c in Physics.OverlapSphere(pos, radius + bal.bubbleObjectClearance, ~0, QueryTriggerInteraction.Ignore))
            if (!c.CompareTag("Ground")) return false;
        return true;
    }

    // Más chica = más monedas; más cerca del centro = más monedas (la fórmula de los huecos, adaptada al círculo)
    public static int ValueFor(Vector3 pos, float radius)
    {
        var bal = GameBalance.Current;
        var arena = Arena.Instance;
        float size = Mathf.Clamp(bal.bubbleReferenceRadius / radius, 0.5f, 3f);
        float span = Mathf.Max(0.01f, arena.Radius - bal.bubbleWallMargin - bal.bubbleCenterMargin);
        float centerFactor = 1f - Mathf.Clamp01((arena.HorizontalDistance(pos) - bal.bubbleCenterMargin) / span);
        return Mathf.Max(1, Mathf.RoundToInt(bal.bubbleBaseValue * size * (1f + centerFactor)));
    }

    private float Next() => (float)rng.NextDouble();
}
