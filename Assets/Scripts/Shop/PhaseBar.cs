using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Botones para avanzar de fase: en Compra, "Tienda" y "Continuar"; en Colocación, "¡A lanzar!".
// Enter hace lo mismo que el botón de avanzar; T abre o cierra la tienda.
[DefaultExecutionOrder(-5)]
public class PhaseBar : MonoBehaviour
{
    [SerializeField] private CanvasGroup shopActions;
    [SerializeField] private Button openShopButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private CanvasGroup placeActions;
    [SerializeField] private Button launchButton;
    [SerializeField] private ShopPanel shop;

    private static TurnManager Turns => TurnManager.Instance;
    private static PlacementController Placement => PlacementController.Instance;

    private void Start()
    {
        GameHUD.Wire(openShopButton, () => shop.Toggle());
        GameHUD.Wire(continueButton, Continue);
        GameHUD.Wire(launchButton, Launch);
    }

    private void Update()
    {
        var phase = Turns ? Turns.Phase : TurnPhase.Setup;
        bool paused = Time.timeScale == 0f;
        Show(shopActions, phase == TurnPhase.Shop && !paused);
        Show(placeActions, phase == TurnPhase.Placement && !paused);
        if (paused) return;

        if (phase == TurnPhase.Shop)
        {
            if (GameInput.Pressed(Key.Enter)) Continue();
            else if (GameInput.Pressed(Key.T)) shop.Toggle();
        }
        else if (phase == TurnPhase.Placement && GameInput.Pressed(Key.Enter)
                 && Placement != null && Placement.Holding == null && Placement.RemovalTarget == null)
            Launch();
    }

    private void Continue()
    {
        if (Turns == null || Turns.Phase != TurnPhase.Shop) return;
        shop.Close();
        Turns.Advance();
    }

    private void Launch()
    {
        if (Turns == null || Turns.Phase != TurnPhase.Placement) return;
        if (Placement) { Placement.CancelPlacing(); Placement.CancelRemoval(); }
        Turns.Advance();
    }

    private static void Show(CanvasGroup g, bool on)
    {
        g.alpha = Mathf.MoveTowards(g.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        g.interactable = g.blocksRaycasts = on;
    }
}
