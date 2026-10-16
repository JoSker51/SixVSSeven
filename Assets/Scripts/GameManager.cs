using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int playerCount = 2;   // pasa a 4 cuando amplíes
    [SerializeField] private int winCoins = 100;

    private int[] coins;

    public bool GameOver { get; private set; }
    public int PlayerCount => playerCount;
    public int WinCoins => winCoins;

    public event Action<int, int> CoinsChanged;   // (jugador, total nuevo)
    public event Action<int> PlayerWon;           // jugador ganador

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (GameSettings.HasWinCoins) winCoins = GameSettings.WinCoins;   // elegido en Opciones
        coins = new int[playerCount];
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;   // permite reiniciar la escena
    }

    public int GetCoins(int player) => coins[player];

    public void AddCoins(int player, int amount)
    {
        if (GameOver || amount <= 0) return;
        coins[player] += amount;
        CoinsChanged?.Invoke(player, coins[player]);
        CheckWin(player);
    }

    // Para la tienda y para quitar obstáculos. Devuelve false si no alcanza.
    public bool SpendCoins(int player, int amount)
    {
        if (amount < 0 || coins[player] < amount) return false;
        coins[player] -= amount;
        CoinsChanged?.Invoke(player, coins[player]);
        return true;
    }

    // Quita monedas de 'from' y las suma a 'to'. Nunca deja a 'from' por debajo de 0.
    // Devuelve la cantidad realmente transferida.
    public int Transfer(int from, int to, int amount)
    {
        if (GameOver || from == to || amount <= 0) return 0;

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
