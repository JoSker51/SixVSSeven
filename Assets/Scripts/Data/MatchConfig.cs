using UnityEngine;

// Lo que se elige en "Nueva partida" y pasa del menú a la escena de juego.
// Queda fijo al empezar: cambiarlo requiere volver al menú y empezar otra partida.
public static class MatchConfig
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;

    private static int playerCount = MinPlayers;

    public static int PlayerCount
    {
        get => playerCount;
        set => playerCount = Mathf.Clamp(value, MinPlayers, MaxPlayers);
    }

    // 0 = usar la semilla de GameBalance (y si también es 0, una al azar)
    public static int Seed { get; set; }

    public static int ResolveSeed()
    {
        int seed = Seed != 0 ? Seed : GameBalance.Current.randomSeed;
        return seed != 0 ? seed : System.Environment.TickCount;
    }
}
