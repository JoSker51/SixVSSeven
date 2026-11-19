using System.Collections.Generic;
using UnityEngine;

// Arena circular: radio real, secciones (una por jugador) y zonas reservadas alrededor de castillos y catapultas.
// La geometría y las colisiones (suelo en disco, pared curva) las genera CatapultasSceneBuilder con el mismo radio.
public class Arena : MonoBehaviour
{
    public static Arena Instance { get; private set; }

    private readonly List<(Vector3 pos, float radius)> keepOut = new List<(Vector3, float)>();

    public float Radius => GameBalance.Current.arenaRadius;
    public Vector3 Center => transform.position;
    public int Sections { get; private set; } = MatchConfig.MinPlayers;
    public float SectionWidth => 360f / Sections;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Configure(int sections)
    {
        Sections = Mathf.Max(1, sections);
        keepOut.Clear();
    }

    // El jugador 0 queda abajo (z negativa) y el turno avanza hacia su derecha
    public static float PlayerAngle(int player, int players) => 270f + player * 360f / players;

    public static Vector3 Direction(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r));
    }

    public float SectionCenter(int section) => PlayerAngle(section, Sections);

    public int SectionOf(Vector3 worldPos)
    {
        Vector3 d = worldPos - Center;
        float a = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        float start = PlayerAngle(0, Sections) - SectionWidth / 2f;
        return Mathf.FloorToInt(Mathf.Repeat(a - start, 360f) / SectionWidth) % Sections;
    }

    public float HorizontalDistance(Vector3 worldPos)
    {
        Vector3 d = worldPos - Center;
        d.y = 0f;
        return d.magnitude;
    }

    // ¿El punto queda dentro del círculo dejando 'margin' hasta la pared?
    public bool InsideHorizontal(Vector3 worldPos, float margin) => HorizontalDistance(worldPos) + margin <= Radius;

    public void AddKeepOut(Vector3 pos, float radius) => keepOut.Add((pos, radius));

    public bool InKeepOut(Vector3 pos, float extra)
    {
        foreach (var (p, r) in keepOut)
        {
            Vector3 d = pos - p;
            d.y = 0f;
            if (d.magnitude < r + extra) return true;
        }
        return false;
    }
}
