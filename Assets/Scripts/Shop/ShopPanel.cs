using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Panel de la tienda: 3 tarjetas (icono, nombre, variante, descripción, precio y botón de compra),
// el botón para actualizar las ofertas y el saldo. Se abre solo al empezar la fase de Compra.
// Toda la validación la hace ShopManager; este panel solo muestra y pide.
[DefaultExecutionOrder(-5)]
public class ShopPanel : MonoBehaviour
{
    [Serializable]
    public class Card
    {
        public CanvasGroup group;
        public Image icon;
        public Text title;
        public Image badge;
        public Text badgeText;
        public Text description;
        public Text price;
        public Button buy;
        public Text buyText;
    }

    [SerializeField] private AeroSkin skin;
    [SerializeField] private UIFadePanel panel;
    [SerializeField] private Card[] cards;
    [SerializeField] private Text coinsText;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Text refreshText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text messageText;

    public bool IsOpen => panel && panel.Visible;

    private static TurnManager Turns => TurnManager.Instance;
    private static ShopManager Shop => ShopManager.Instance;

    private void Start()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int slot = i;
            GameHUD.Wire(cards[i].buy, () => OnBuy(slot));
        }
        GameHUD.Wire(refreshButton, OnRefresh);
        GameHUD.Wire(closeButton, Close);
        if (Shop != null) Shop.Changed += _ => Refresh();
        if (Turns != null) Turns.PhaseChanged += OnPhaseChanged;
    }

    private void OnDestroy()
    {
        if (Turns != null) Turns.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(TurnPhase phase)
    {
        if (phase == TurnPhase.Shop) Open();
        else Close();
    }

    public void Open()
    {
        if (Turns == null || Turns.Phase != TurnPhase.Shop) return;
        messageText.text = "";
        panel.Show();
        Refresh();
    }

    public void Close() { if (panel) panel.Hide(); }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    private void Update()
    {
        if (!IsOpen) return;
        if (GameInput.Pressed(Key.Escape)) { GameInput.ConsumeEscape(); Close(); return; }
        Refresh();   // el saldo puede cambiar (compras, refrescos); es barato
    }

    private void Refresh()
    {
        if (Turns == null || Shop == null || GameManager.Instance == null) return;
        int p = Turns.CurrentPlayer;
        if (p < 0) return;
        int coins = GameManager.Instance.GetCoins(p);
        var offers = Shop.Offers(p);

        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            var item = i < offers.Count ? offers[i] : null;
            if (item == null)
            {
                card.group.alpha = 0.45f;
                card.icon.enabled = false;
                card.title.text = "Sin oferta";
                card.badge.gameObject.SetActive(false);
                card.description.text = "No hay más objetos distintos en la tienda.";
                card.price.text = "";
                card.buy.interactable = false;
                card.buyText.text = "—";
                continue;
            }
            var state = Shop.CanBuy(p, i);
            card.group.alpha = 1f;   // el botón ya dice si alcanza; el ícono se ve siempre nítido
            card.icon.enabled = item.icon != null;
            card.icon.sprite = item.icon;
            card.title.text = item.displayName;
            card.badge.gameObject.SetActive(true);
            StyleBadge(card, item.variant);
            card.description.text = item.description;
            card.price.text = $"<b>{item.price}</b> monedas";
            card.buy.interactable = state == ShopResult.Ok;
            card.buyText.text = state switch
            {
                ShopResult.Ok => "Comprar",
                ShopResult.Sold => "Comprado",
                ShopResult.NotEnoughCoins => $"Te faltan {item.price - coins}",
                _ => "No disponible",
            };
        }

        coinsText.text = $"Tus monedas: <b>{coins}</b>";
        var refresh = Shop.CanRefresh(p);
        refreshButton.interactable = refresh == ShopResult.Ok;
        refreshText.text = Shop.RefreshesLeft(p) <= 0
            ? "Ya actualizaste este turno"
            : refresh == ShopResult.NotEnoughCoins
                ? $"Actualizar: cuesta {Shop.RefreshCost} moneda"
                : $"Actualizar ofertas  ({Shop.RefreshCost} moneda)";
    }

    private void StyleBadge(Card card, ItemVariant variant)
    {
        var bal = GameBalance.Current;
        switch (variant)
        {
            case ItemVariant.Positive:
                card.badge.sprite = skin.buttonGreen; card.badge.color = Color.white;
                card.badgeText.text = $"Positivo  +{bal.positiveBonus}"; card.badgeText.color = Color.white; break;
            case ItemVariant.Negative:
                card.badge.sprite = skin.buttonSilver; card.badge.color = new Color(0.33f, 0.36f, 0.5f);
                card.badgeText.text = $"Negativo  −{bal.negativePenalty}"; card.badgeText.color = Color.white; break;
            case ItemVariant.Special:
                card.badge.sprite = skin.buttonGold; card.badge.color = Color.white;
                card.badgeText.text = $"Especial  ×{bal.ringMultiplier}"; card.badgeText.color = skin.navy; break;
            default:
                card.badge.sprite = skin.buttonSilver; card.badge.color = Color.white;
                card.badgeText.text = "Normal"; card.badgeText.color = skin.navy; break;
        }
    }

    private void OnBuy(int slot)
    {
        int p = Turns.CurrentPlayer;
        var item = Shop.Offers(p)[slot];
        var result = Shop.Buy(p, slot);
        messageText.text = result switch
        {
            ShopResult.Ok => $"¡Compraste {item.FullName}! Ya está en tu inventario.",
            ShopResult.NotEnoughCoins => "No te alcanzan las monedas.",
            ShopResult.Sold => "Ya compraste esa oferta.",
            ShopResult.WrongPhase => "Solo se compra en la fase de Compra.",
            _ => "No se pudo comprar.",
        };
        Refresh();
    }

    private void OnRefresh()
    {
        var result = Shop.Refresh(Turns.CurrentPlayer);
        messageText.text = result switch
        {
            ShopResult.Ok => "Ofertas nuevas.",
            ShopResult.NoRefreshLeft => "Solo se puede actualizar una vez por turno.",
            ShopResult.NotEnoughCoins => "No te alcanza para actualizar.",
            _ => "",
        };
        Refresh();
    }
}
