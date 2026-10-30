using UnityEngine;
using UnityEngine.UI;

// Confirmación para retirar un objeto colocado: muestra qué es, quién lo puso y cuánto cuesta antes de pagar.
public class RemovalPrompt : MonoBehaviour
{
    [SerializeField] private UIFadePanel panel;
    [SerializeField] private Text title;
    [SerializeField] private Text detail;
    [SerializeField] private Button confirm;
    [SerializeField] private Text confirmText;
    [SerializeField] private Button cancel;

    private static PlacementController Placement => PlacementController.Instance;

    private void Start()
    {
        GameHUD.Wire(confirm, () => { if (Placement) Placement.ConfirmRemoval(); });
        GameHUD.Wire(cancel, () => { if (Placement) Placement.CancelRemoval(); });
    }

    private void Update()
    {
        var target = Placement ? Placement.RemovalTarget : null;
        panel.SetVisible(target != null);
        if (target == null || target.Definition == null) return;

        int p = TurnManager.Instance.CurrentPlayer;
        int cost = Placement.RemovalCost(target);
        int coins = GameManager.Instance.GetCoins(p);
        title.text = $"¿Retirar {target.Definition.FullName}?";
        string owner = target.OwnerId == p ? "Lo colocaste tú" : $"Lo colocó el Jugador {target.OwnerId + 1}";
        detail.text = $"{owner}.\nCuesta <b>{cost}</b> monedas (tienes {coins}).\nNo vuelve a tu inventario.";
        bool can = Placement.CanAffordRemoval;
        confirm.interactable = can;
        confirmText.text = can ? $"Retirar  (−{cost})" : "Te faltan monedas";
    }
}
