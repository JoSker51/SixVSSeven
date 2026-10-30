using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShopResult { Ok, WrongPhase, NoOffer, Sold, NotEnoughCoins, NoRefreshLeft, Failed }

// Tienda de cada jugador: 3 ofertas al azar de la pool (sin repetir mientras alcancen los objetos distintos).
// Comprar y refrescar solo se puede en la fase de Compra del propio turno. Refrescar tiene un límite por turno
// que lleva esta lógica (no la interfaz): cerrar y abrir la tienda no lo reinicia; el turno siguiente, sí.
[DefaultExecutionOrder(-10)]   // se suscribe a los turnos antes de que empiece el primero
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    private class PlayerShop
    {
        public ItemDefinition[] offers;
        public bool[] sold;
        public int refreshesUsed;
    }

    private readonly Dictionary<int, PlayerShop> shops = new Dictionary<int, PlayerShop>();
    private System.Random rng;

    public event Action<int> Changed;   // jugador cuya tienda cambió

    private static GameBalance Bal => GameBalance.Current;

    private void Awake()
    {
        Instance = this;
        rng = new System.Random(MatchConfig.ResolveSeed() ^ 0x5A17);
    }

    private void Start()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.TurnStarted += OnTurnStarted;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.TurnStarted -= OnTurnStarted;
        if (Instance == this) Instance = null;
    }

    // Empieza el turno: ofertas nuevas y la oportunidad de refrescar vuelve a estar disponible
    private void OnTurnStarted(int player)
    {
        var s = Get(player);
        s.refreshesUsed = 0;
        Roll(s);
        Changed?.Invoke(player);
    }

    public IReadOnlyList<ItemDefinition> Offers(int player) => Get(player).offers;
    public bool IsSold(int player, int slot) { var s = Get(player); return slot >= 0 && slot < s.sold.Length && s.sold[slot]; }
    public int RefreshesLeft(int player) => Mathf.Max(0, Bal.refreshesPerTurn - Get(player).refreshesUsed);
    public int RefreshCost => Bal.refreshCost;

    public ShopResult CanBuy(int player, int slot)
    {
        var tm = TurnManager.Instance;
        if (tm == null || !tm.IsPhase(TurnPhase.Shop, player)) return ShopResult.WrongPhase;
        var s = Get(player);
        if (slot < 0 || slot >= s.offers.Length || s.offers[slot] == null) return ShopResult.NoOffer;
        if (s.sold[slot]) return ShopResult.Sold;
        if (MatchSetup.Instance == null || MatchSetup.Instance.Player(player) == null) return ShopResult.Failed;
        if (GameManager.Instance.GetCoins(player) < s.offers[slot].price) return ShopResult.NotEnoughCoins;
        return ShopResult.Ok;
    }

    // Comprar: se valida todo, se cobra y se guarda en el inventario. Si algo falla, se devuelve el cobro.
    public ShopResult Buy(int player, int slot)
    {
        var result = CanBuy(player, slot);
        if (result != ShopResult.Ok) { AeroAudio.Play(Sfx.Error); return result; }

        var s = Get(player);
        var item = s.offers[slot];
        var state = MatchSetup.Instance.Player(player);
        if (!GameManager.Instance.SpendCoins(player, item.price)) return ShopResult.NotEnoughCoins;
        if (!state.Inventory.Add(item))
        {
            GameManager.Instance.Refund(player, item.price);   // nunca una compra a medias
            return ShopResult.Failed;
        }
        s.sold[slot] = true;
        AeroAudio.Play(Sfx.Buy);
        Changed?.Invoke(player);
        return ShopResult.Ok;
    }

    public ShopResult CanRefresh(int player)
    {
        var tm = TurnManager.Instance;
        if (tm == null || !tm.IsPhase(TurnPhase.Shop, player)) return ShopResult.WrongPhase;
        if (RefreshesLeft(player) <= 0) return ShopResult.NoRefreshLeft;
        if (GameManager.Instance.GetCoins(player) < Bal.refreshCost) return ShopResult.NotEnoughCoins;
        return ShopResult.Ok;
    }

    public ShopResult Refresh(int player)
    {
        var result = CanRefresh(player);
        if (result != ShopResult.Ok) { AeroAudio.Play(Sfx.Error); return result; }
        if (!GameManager.Instance.SpendCoins(player, Bal.refreshCost)) return ShopResult.NotEnoughCoins;
        var s = Get(player);
        s.refreshesUsed++;
        Roll(s);
        AeroAudio.Play(Sfx.Click);
        Changed?.Invoke(player);
        return ShopResult.Ok;
    }

    private PlayerShop Get(int player)
    {
        if (!shops.TryGetValue(player, out var s))
        {
            int n = Mathf.Max(1, Bal.shopOffers);
            s = new PlayerShop { offers = new ItemDefinition[n], sold = new bool[n] };
            shops[player] = s;
        }
        return s;
    }

    // Sorteo sin repetir; si la pool tiene menos objetos distintos que lugares, los que sobran quedan vacíos
    private void Roll(PlayerShop s)
    {
        var pool = new List<ItemDefinition>(Bal.ShopPool);
        for (int i = 0; i < s.offers.Length; i++)
        {
            s.sold[i] = false;
            if (pool.Count == 0) { s.offers[i] = null; continue; }
            int k = rng.Next(pool.Count);
            s.offers[i] = pool[k];
            pool.RemoveAt(k);
        }
    }
}
