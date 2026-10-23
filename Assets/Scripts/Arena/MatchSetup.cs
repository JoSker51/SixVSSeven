using System.Collections.Generic;
using UnityEngine;

// Arma la partida al cargar la escena: solo los jugadores elegidos (2 a 4), repartidos alrededor de la arena
// (a 180°, 120° o 90°), cada uno con su castillo, catapulta, burbuja de castillo, color y sección del mapa.
[DefaultExecutionOrder(-50)]
public class MatchSetup : MonoBehaviour
{
    public static MatchSetup Instance { get; private set; }

    [SerializeField] private Arena arena;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BubbleField bubbleField;
    [SerializeField] private CatapultController catapultPrefab;
    [SerializeField] private PlayerPaint castlePrefab;
    [SerializeField] private Bubble bubblePrefab;
    [SerializeField] private AeroSkin skin;
    [SerializeField] private Material[] playerMaterials;
    [SerializeField] private Material[] markerMaterials;
    [SerializeField] private Material dividerMaterial;
    [SerializeField] private Material sectionMaterial;   // transparente; se tiñe con el color de cada jugador

    private readonly List<PlayerState> players = new List<PlayerState>();

    public IReadOnlyList<PlayerState> Players => players;
    public PlayerState Player(int id) => id >= 0 && id < players.Count ? players[id] : null;

    private void Awake()
    {
        Instance = this;
        int n = MatchConfig.PlayerCount;
        var bal = GameBalance.Current;
        arena.Configure(n);
        var spawned = new GameObject("Jugadores").transform;

        for (int i = 0; i < n; i++)
        {
            float angle = Arena.PlayerAngle(i, n);
            Vector3 dir = Arena.Direction(angle);
            Quaternion facing = Quaternion.LookRotation(-dir);   // mirando al centro
            Color color = skin.PlayerColor(i);

            var castle = Instantiate(castlePrefab, arena.Center + dir * bal.castleRingRadius, facing, spawned);
            castle.name = $"Castillo J{i + 1}";
            castle.Apply(Pick(playerMaterials, i), null);

            var catapult = Instantiate(catapultPrefab, arena.Center + dir * bal.catapultRingRadius, facing, spawned);
            catapult.name = $"Catapulta J{i + 1}";
            catapult.Setup(i, color);
            if (catapult.TryGetComponent(out PlayerPaint paint)) paint.Apply(Pick(playerMaterials, i), Pick(markerMaterials, i));
            turnManager.Register(catapult);

            arena.AddKeepOut(castle.transform.position, bal.castleKeepOut);
            arena.AddKeepOut(catapult.transform.position, bal.catapultKeepOut);

            var castleBubble = Instantiate(bubblePrefab, bubbleField.transform);
            castleBubble.name = $"Burbuja castillo J{i + 1}";
            bubbleField.AddCastleBubble(castleBubble, castle.transform.position + Vector3.up * bal.castleBubbleHeight, bal.castleBubbleRadius, i, color);

            players.Add(new PlayerState { Id = i, Color = color, Angle = angle, Catapult = catapult, Castle = castle.transform, CastleBubble = castleBubble });
        }

        BuildSections(n);
        Physics.SyncTransforms();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // Cada jugador tiene su sección (porción del círculo frente a su castillo), con una franja tenue de su color
    private void BuildSections(int n)
    {
        var root = new GameObject("Secciones").transform;
        root.SetParent(arena.transform, false);
        float r = arena.Radius, inner = 2.2f;
        for (int i = 0; i < n; i++)
        {
            float center = Arena.PlayerAngle(i, n), half = 180f / n;

            var wedge = new GameObject($"Sección J{i + 1}", typeof(MeshFilter), typeof(MeshRenderer));
            wedge.transform.SetParent(root, false);
            wedge.transform.localPosition = Vector3.up * 0.015f;
            wedge.GetComponent<MeshFilter>().sharedMesh = ProceduralMesh.Wedge(center - half, center + half, inner, r - 0.05f, 48);
            var mr = wedge.GetComponent<MeshRenderer>();
            var tint = new Material(sectionMaterial);
            Color c = players[i].Color;
            tint.color = new Color(c.r, c.g, c.b, sectionMaterial.color.a);
            if (tint.HasProperty("_BaseColor")) tint.SetColor("_BaseColor", tint.color);
            mr.sharedMaterial = tint;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Divisor brillante en el borde de la sección
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(line.GetComponent<Collider>());
            line.name = "Divisor";
            line.transform.SetParent(root, false);
            Vector3 d = Arena.Direction(center - half);
            float len = r - inner;
            line.transform.SetPositionAndRotation(arena.Center + d * (inner + len / 2f) + Vector3.up * 0.03f, Quaternion.LookRotation(d));
            line.transform.localScale = new Vector3(0.14f, 0.04f, len);
            line.GetComponent<Renderer>().sharedMaterial = dividerMaterial;
        }
    }

    private static Material Pick(Material[] list, int i) => list != null && list.Length > 0 ? list[i % list.Length] : null;
}
