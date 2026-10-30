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
    public readonly List<ArenaObject> RingPasses = new List<ArenaObject>();   // una entrada por pasada (el mismo aro puede repetirse)

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

    // Cada pasada cuenta, aunque sea por el mismo aro. Devuelve cuántas lleva el tiro.
    public int RegisterRingPass(ArenaObject ring)
    {
        if (ring != null) RingPasses.Add(ring);
        return RingPasses.Count;
    }
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
    public int RingPasses;    // pasadas por aros que cuentan (0 si no hubo burbujas)
    public long Multiplier;   // 2 elevado a las pasadas: 1, 2, 4, 8...
    public int E;             // bonificación neta de objetos = 2P - 2N
    public int R;             // antes del límite inferior
    public int Final;         // máx(0, R): lo que se suma al jugador
    public int Stolen;        // monedas robadas con burbujas de castillo
}

// El único lugar donde se calculan las monedas de un tiro (sección 10.5 del documento de diseño):
//   E = 2P - 2N ;  M = 2^k (k = pasadas por aros) ;  R = M·B + E ;  final = máx(0, R)
// Positivos, negativos y aros solo cuentan si en ese tiro se reventó al menos una burbuja.
// Los aros se apilan: cada pasada vuelve a duplicar B (1 pasada ×2, 2 ×4, 3 ×8...), aunque sea el mismo aro.
public static class RewardCalculator
{
    public const long MaxReward = 1_000_000_000;   // tope técnico para que los números no se desborden

    // Multiplicador de k pasadas (base ×2 de GameBalance). Se corta al llegar al tope técnico.
    public static long Multiplier(int passes, GameBalance bal)
    {
        long m = 1;
        for (int i = 0; i < passes && m < MaxReward; i++) m *= Mathf.Max(1, bal.ringMultiplier);
        return System.Math.Min(m, MaxReward);
    }

    public static ShotReward Compute(int bubbleCount, int baseReward, int positives, int negatives, int ringPasses, GameBalance bal)
    {
        bool any = bubbleCount > 0;
        var r = new ShotReward
        {
            BubbleCount = bubbleCount,
            B = any ? baseReward : 0,
            P = any ? positives : 0,
            N = any ? negatives : 0,
            RingPasses = any ? ringPasses : 0,
        };
        r.E = bal.positiveBonus * r.P - bal.negativePenalty * r.N;
        r.Multiplier = Multiplier(r.RingPasses, bal);
        long raw = System.Math.Min(r.Multiplier * r.B, MaxReward) + r.E;
        r.R = (int)System.Math.Max(-MaxReward, System.Math.Min(raw, MaxReward));
        r.Final = Mathf.Max(0, r.R);
        return r;
    }

    public static ShotReward Compute(ShotRecord rec, GameBalance bal) =>
        Compute(rec.Bubbles.Count, rec.BaseReward, rec.Positives.Count, rec.Negatives.Count, rec.RingPasses.Count, bal);
}
