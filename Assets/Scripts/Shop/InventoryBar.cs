using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Barra con los objetos comprados del jugador de turno (agrupados, con cantidad). Se ve en la fase de Colocación.
// Clic en un objeto: empieza a colocarlo. Arriba muestra qué se está colocando y si el lugar es válido.
[DefaultExecutionOrder(-5)]
public class InventoryBar : MonoBehaviour
{
    [SerializeField] private AeroSkin skin;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform slotsRoot;
    [SerializeField] private Text emptyText;
    [SerializeField] private CanvasGroup infoGroup;
    [SerializeField] private Text infoText;

    private class Slot { public ItemDefinition item; public Image frame; public Text count; }

    private readonly List<Slot> slots = new List<Slot>();
    private int shownPlayer = -1;
    private bool dirty = true;

    private static TurnManager Turns => TurnManager.Instance;
    private static PlacementController Placement => PlacementController.Instance;

    private void Start()
    {
        if (MatchSetup.Instance != null)
            foreach (var p in MatchSetup.Instance.Players) p.Inventory.Changed += () => dirty = true;
        if (Placement != null) Placement.Changed += () => dirty = true;
    }

    private void Update()
    {
        bool show = Turns != null && Turns.Phase == TurnPhase.Placement;
        float dt = Time.unscaledDeltaTime;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * 6f);
        group.interactable = group.blocksRaycasts = show;
        infoGroup.alpha = group.alpha;
        if (!show) return;

        int p = Turns.CurrentPlayer;
        if (p != shownPlayer) { shownPlayer = p; dirty = true; }
        if (dirty) { dirty = false; Rebuild(); }
        UpdateInfo();
    }

    private void Rebuild()
    {
        foreach (Transform child in slotsRoot) Destroy(child.gameObject);
        slots.Clear();
        var state = MatchSetup.Instance ? MatchSetup.Instance.Player(shownPlayer) : null;
        if (state == null) return;

        var distinct = new List<ItemDefinition>();
        foreach (var item in state.Inventory.Items) if (!distinct.Contains(item)) distinct.Add(item);
        emptyText.gameObject.SetActive(distinct.Count == 0);

        const float size = 104f, gap = 14f;
        float x0 = -(distinct.Count - 1) * (size + gap) / 2f;
        for (int i = 0; i < distinct.Count; i++)
        {
            var item = distinct[i];
            var frame = AeroUI.Img(slotsRoot, item.id, skin.glassPanel, Color.white, 2.2f);
            frame.raycastTarget = true;
            frame.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (size + gap), 0f), new Vector2(size, size));
            AeroUI.Img(frame.transform, "Resplandor", skin.glow, VariantGlow(item.variant)).rectTransform.Stretch(-8f);
            var icon = AeroUI.Img(frame.transform, "Icono", item.icon, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.Stretch(10f);
            var badge = AeroUI.Orb(frame.transform, "Cantidad", skin, new Color(0.35f, 0.7f, 1f), 38f);
            badge.rectTransform.At(new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(38f, 38f));
            var count = AeroUI.Label(badge.transform, "Texto", "x" + state.Inventory.CountOf(item), 18, Color.white, AeroWeight.Bold);
            count.rectTransform.Stretch();
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            frame.gameObject.AddComponent<AeroButtonFX>();
            GameHUD.Wire(button, () => { if (Placement) Placement.BeginPlacing(item); });
            slots.Add(new Slot { item = item, frame = frame, count = count });
        }

        foreach (var s in slots)
            s.frame.color = Placement && Placement.Holding == s.item ? new Color(0.7f, 0.95f, 1f) : Color.white;
    }

    private void UpdateInfo()
    {
        var pc = Placement;
        if (pc == null) return;
        if (pc.Holding != null)
        {
            infoText.text = pc.IsValid
                ? $"<b>{pc.Holding.FullName}</b>  ·  clic para colocar  ·  clic derecho para cancelar"
                : $"<b>{pc.Holding.FullName}</b>  ·  <color=#C2185B>{pc.Reason}</color>";
        }
        else if (pc.RemovalTarget != null) infoText.text = "Confirma o cancela la retirada";
        else if (!string.IsNullOrEmpty(pc.LastMessage)) infoText.text = pc.LastMessage;
        else infoText.text = slots.Count > 0 ? "Elige un objeto para colocarlo, o haz clic en uno de la arena para retirarlo" : "Haz clic en un objeto de la arena para retirarlo, o lanza";
    }

    private static Color VariantGlow(ItemVariant v) => v switch
    {
        ItemVariant.Positive => new Color(0.4f, 1f, 0.6f, 0.7f),
        ItemVariant.Negative => new Color(1f, 0.4f, 0.6f, 0.6f),
        ItemVariant.Special => new Color(1f, 0.85f, 0.35f, 0.8f),
        _ => new Color(0.75f, 0.92f, 1f, 0.6f),
    };
}
