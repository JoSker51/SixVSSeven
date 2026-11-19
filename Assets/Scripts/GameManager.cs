using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int winCoins = 100;

    private int[] coins;
    private int playerCount;

    public bool GameOver { get; private set; }
    public int PlayerCount => playerCount;
    public int WinCoins => winCoins;

    public event Action<int, int> CoinsChanged;   // (jugador, total nuevo)
    public event Action<int> PlayerWon;           // jugador ganador

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        playerCount = MatchConfig.PlayerCount;                              // elegido en "Nueva partida" (2 a 4)
        winCoins = MatchConfig.WinCoins > 0 ? MatchConfig.WinCoins                // elegida en "Nueva partida"
                 : GameSettings.HasWinCoins ? GameSettings.WinCoins : GameBalance.Current.defaultWinCoins;
        coins = new int[playerCount];
        for (int i = 0; i < playerCount; i++) coins[i] = GameBalance.Current.startingCoins;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;   // permite reiniciar la escena
    }

    public bool IsValidPlayer(int player) => player >= 0 && player < playerCount;

    public int GetCoins(int player) => IsValidPlayer(player) ? coins[player] : 0;

    public void AddCoins(int player, int amount)
    {
        if (GameOver || amount <= 0 || !IsValidPlayer(player)) return;
        coins[player] += amount;
        CoinsChanged?.Invoke(player, coins[player]);
        CheckWin(player);
    }

    // Para la tienda y para retirar objetos. Devuelve false si no alcanza (y no cobra nada).
    public bool SpendCoins(int player, int amount)
    {
        if (GameOver || amount < 0 || !IsValidPlayer(player) || coins[player] < amount) return false;
        coins[player] -= amount;
        CoinsChanged?.Invoke(player, coins[player]);
        return true;
    }

    // Devuelve un cobro que no se pudo completar (no cuenta como ganar monedas)
    public void Refund(int player, int amount)
    {
        if (amount <= 0 || !IsValidPlayer(player)) return;
        coins[player] += amount;
        CoinsChanged?.Invoke(player, coins[player]);
    }

    // Quita monedas de 'from' y las suma a 'to'. Nunca deja a 'from' por debajo de 0.
    // Devuelve la cantidad realmente transferida.
    public int Transfer(int from, int to, int amount)
    {
        if (GameOver || from == to || amount <= 0 || !IsValidPlayer(from) || !IsValidPlayer(to)) return 0;

        int real = Mathf.Min(amount, coins[from]);   // límite: no baja de 0
        if (real <= 0) return 0;

        coins[from] -= real;
        coins[to] += real;
        CoinsChanged?.Invoke(from, coins[from]);
        CoinsChanged?.Invoke(to, coins[to]);
        CheckWin(to);
        return real;
    }

    private void CheckWin(int player)
    {
        if (GameOver || coins[player] < winCoins) return;
        GameOver = true;
        Debug.Log($"¡Gana el jugador {player}!");
        PlayerWon?.Invoke(player);
    }
}
