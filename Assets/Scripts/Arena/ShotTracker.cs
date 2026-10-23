using System.Collections.Generic;
using UnityEngine;

// Registro de un tiro: qué burbujas reventó la pelota y qué objetos tocó o atravesó.
// La detección (Bubble, Rock, aros) solo anota aquí; nadie toca monedas hasta la fase de Resolución.
public class ShotRecord
{
    public int ThrowerId;
    public Rock Rock;

    public readonly List<Bubble> Bubbles = new List<Bubble>();          // burbujas normales reventadas
    public readonly List<int> CastleOwners = new List<int>();           // dueños de burbujas de castillo reventadas
    public readonly HashSet<ArenaObject> Touched = new HashSet<ArenaObject>();
    public readonly HashSet<ArenaObject> Positives = new HashSet<ArenaObject>();
    public readonly HashSet<ArenaObject> Negatives = new HashSet<ArenaObject>();
    public readonly HashSet<ArenaObject> Rings = new HashSet<ArenaObject>();

    public int BaseReward
    {
        get
        {
            int sum = 0;
            foreach (var b in Bubbles) sum += b.Value;
            return sum;
        }
    }

    // Cada burbuja cuenta una sola vez aunque haya varios eventos de colisión
    public bool RegisterBubble(Bubble b)
    {
        if (b.IsCastle)
        {
            if (CastleOwners.Contains(b.OwnerId)) return false;
            CastleOwners.Add(b.OwnerId);
            return true;
        }
        if (Bubbles.Contains(b)) return false;
        Bubbles.Add(b);
        return true;
    }

    // Golpear varias veces el mismo objeto no repite su efecto
    public bool RegisterHit(ArenaObject obj)
    {
        if (obj == null || !Touched.Add(obj)) return false;
        if (obj.Variant == ItemVariant.Positive) Positives.Add(obj);
        else if (obj.Variant == ItemVariant.Negative) Negatives.Add(obj);
        return true;
    }

    public bool RegisterRing(ArenaObject ring) => ring != null && Rings.Add(ring);
}

public static class ShotTracker
{
    public static ShotRecord Active { get; private set; }

    public static ShotRecord Begin(int thrower, Rock rock)
    {
        Active = new ShotRecord { ThrowerId = thrower, Rock = rock };
        return Active;
    }

    public static ShotRecord End()
    {
        var r = Active;
        Active = null;
        return r;
    }
}

// Resultado de un tiro, para aplicarlo y mostrarlo
public struct ShotReward
{
    public int BubbleCount;   // burbujas normales reventadas
    public int B;             // recompensa base de esas burbujas
    public int P;             // bonificaciones positivas activadas
    public int N;             // penalizaciones negativas activadas
    public bool Ring;         // multiplicador activado
    public int E;             // bonificación neta de objetos = 2P - 2N
    public int R;             // antes del límite inferior
    public int Final;         // máx(0, R): lo que se suma al jugador
    public int Stolen;        // monedas robadas con burbujas de castillo
}

// El único lugar donde se calculan las monedas de un tiro (sección 10.5 del documento de diseño):
//   E = 2P - 2N ;  R = B + E  (o  R = 2B + E  con aro) ;  final = máx(0, R)
// Positivos, negativos y aros solo cuentan si en ese tiro se reventó al menos una burbuja.
// El aro duplica solo B, y varios aros en el mismo tiro lo aplican una sola vez.
public static class RewardCalculator
{
    public static ShotReward Compute(int bubbleCount, int baseReward, int positives, int negatives, int rings, GameBalance bal)
    {
        bool any = bubbleCount > 0;
        var r = new ShotReward
        {
            BubbleCount = bubbleCount,
            B = any ? baseReward : 0,
            P = any ? positives : 0,
            N = any ? negatives : 0,
            Ring = any && rings > 0,
        };
        r.E = bal.positiveBonus * r.P - bal.negativePenalty * r.N;
        r.R = (r.Ring ? bal.ringMultiplier : 1) * r.B + r.E;
        r.Final = Mathf.Max(0, r.R);
        return r;
    }

    public static ShotReward Compute(ShotRecord rec, GameBalance bal) =>
        Compute(rec.Bubbles.Count, rec.BaseReward, rec.Positives.Count, rec.Negatives.Count, rec.Rings.Count, bal);
}
