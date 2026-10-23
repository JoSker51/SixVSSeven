using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// HUD del juego con estilo Aero (uGUI): marcador de 2 a 4 jugadores, turno y fase, potencia, valor de cada
// burbuja, textos flotantes, resumen de cada tiro, pausa (Esc) y victoria. La escena la arma CatapultasSceneBuilder.
[DefaultExecutionOrder(100)]   // después de CameraRig, para que las etiquetas no vayan un cuadro atrasadas
public class GameHUD : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private AeroSkin skin;
    [SerializeField] private string menuScene = "MenuPrincipal";

    [Header("Marcador")]
    [SerializeField] private RectTransform board;
    [SerializeField] private Text goalText;

    [Header("Turno, fase y potencia")]
    [SerializeField] private Image turnBanner;
    [SerializeField] private Text turnText;
    [SerializeField] private RectTransform phaseStrip;
    [SerializeField] private CanvasGroup powerPanel;
    [SerializeField] private Image powerFill;
    [SerializeField] private Text powerText;
    [SerializeField] private CanvasGroup helpPanel;
    [SerializeField] private Text helpText;

    [Header("Resumen del tiro")]
    [SerializeField] private UIFadePanel summaryPanel;
    [SerializeField] private Text summaryTitle;
    [SerializeField] private Text summaryBody;

    [Header("Pausa y victoria")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private UIFadePanel pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private UIFadePanel victoryPanel;
    [SerializeField] private Text victoryTitle;
    [SerializeField] private Text victorySubtitle;
    [SerializeField] private Button againButton;
    [SerializeField] private Button victoryMenuButton;

    [Header("Etiquetas sobre el mapa")]
    [SerializeField] private RectTransform worldLayer;
    [SerializeField] private CanvasGroup fade;   // blanco que se desvanece al entrar (sigue al fundido del menú)

    private struct PendingPopup { public Vector3 pos; public string text; public Color color; }
    private static readonly List<PendingPopup> pending = new List<PendingPopup>();

    private class Row { public Text coins; public RectTransform fill; public Image highlight; public int shown = int.MinValue; public float pulse; }
    private class BubbleLabel { public RectTransform rt; public Text text; public int shown = -1; }
    private class FloatingText { public RectTransform rt; public CanvasGroup group; public Vector3 world; public float start; }
    private class Step { public TurnPhase phase; public Text text; public Image dot; }

    private readonly List<Row> rows = new List<Row>();
    private readonly Dictionary<Bubble, BubbleLabel> bubbleLabels = new Dictionary<Bubble, BubbleLabel>();
    private readonly List<FloatingText> floating = new List<FloatingText>();
    private readonly List<Step> steps = new List<Step>();
    private const float PopupDuration = 1.6f;

    private Canvas canvas;
    private Camera cam;
    private int lastTurnPlayer = -1;
    private TurnPhase lastPhase = TurnPhase.Setup;
    private float turnPulse;
    private bool paused;
    private string lastGoal;

    public bool Paused => paused;

    // Texto flotante sobre un punto del mapa (burbujas, objetos y aros lo usan al activarse)
    public static void Popup(Vector3 worldPos, string text, Color color)
    {
        pending.Add(new PendingPopup { pos = worldPos, text = text, color = color });
    }

    private void Awake()
    {
        pending.Clear();
        canvas = GetComponentInParent<Canvas>().rootCanvas;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        cam = Camera.main;

        BuildScoreboard(GameManager.Instance != null ? GameManager.Instance.PlayerCount : MatchConfig.PlayerCount);
        BuildPhaseStrip();

        if (GameManager.Instance != null) GameManager.Instance.PlayerWon += OnPlayerWon;
        if (turnManager != null) turnManager.ShotResolved += OnShotResolved;

        Wire(pauseButton, TogglePause);
        Wire(resumeButton, TogglePause);
        Wire(restartButton, Restart);
        Wire(menuButton, GoToMenu);
        Wire(againButton, Restart);
        Wire(victoryMenuButton, GoToMenu);

        if (fade) StartCoroutine(FadeIn());
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.PlayerWon -= OnPlayerWon;
        if (turnManager != null) turnManager.ShotResolved -= OnShotResolved;
        Time.timeScale = 1f;
    }

    // Los botones del HUD se deseleccionan al usarlos, así Espacio no los vuelve a "presionar" mientras apuntas
    public static void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (!b) return;
        b.onClick.AddListener(action);
        b.onClick.AddListener(() => { if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null); });
    }

    private IEnumerator FadeIn()
    {
        fade.gameObject.SetActive(true);
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            fade.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / 0.6f);
            yield return null;
        }
        fade.gameObject.SetActive(false);
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool over = gm != null && gm.GameOver;
        if (!over && PausePressed() && !GameInput.EscapeConsumed) TogglePause();
        if (over && RestartPressed()) Restart();
    }

    private void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        var gm = GameManager.Instance;
        UpdateScores(gm);
        UpdateTurn(gm);
        UpdateBubbleLabels();
        UpdatePopups();
    }

    // ---------- Marcador ----------

    private void BuildScoreboard(int players)
    {
        const float rowH = 74f;
        board.sizeDelta = new Vector2(board.sizeDelta.x, 78f + rowH * players);
        for (int i = 0; i < players; i++)
        {
            Color c = skin.PlayerColor(i);
            var row = AeroUI.Node($"Jugador {i + 1}", board).At(new Vector2(0.5f, 1f), new Vector2(0f, -58f - rowH * i), new Vector2(board.sizeDelta.x - 26f, rowH - 8f));
            var hl = AeroUI.Img(row, "Resaltado", skin.fill, new Color(c.r, c.g, c.b, 0f), skin.fill.rect.height / (rowH - 8f));
            hl.rectTransform.Stretch();
            AeroUI.Orb(row, "Color", skin, c, 44f).rectTransform.At(new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 44f));
            string colorName = skin.playerColorNames != null && i < skin.playerColorNames.Length ? skin.playerColorNames[i] : "";
            AeroUI.Label(row, "Nombre", $"Jugador {i + 1}  <size=20>{colorName}</size>", 28, skin.navy, AeroWeight.Bold, TextAnchor.MiddleLeft)
                .rectTransform.At(new Vector2(0f, 1f), new Vector2(68f, -4f), new Vector2(240f, 34f));
            var track = AeroUI.Img(row, "Barra", skin.track, Color.white, skin.track.rect.height / 14f);
            track.rectTransform.At(new Vector2(0f, 0f), new Vector2(68f, 11f), new Vector2(230f, 14f));
            var fill = AeroUI.Img(track.transform, "Progreso", skin.fill, c, skin.fill.rect.height / 14f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var coins = AeroUI.Label(row, "Monedas", "0", 40, skin.navy, AeroWeight.Bold, TextAnchor.MiddleRight);
            coins.rectTransform.At(new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(110f, 50f));
            AeroUI.Effect<Shadow>(coins, new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, -2f));
            rows.Add(new Row { coins = coins, fill = fill.rectTransform, highlight = hl });
        }
    }

    private void UpdateScores(GameManager gm)
    {
        if (gm == null) return;
        string goal = $"Ronda <b>{(turnManager ? turnManager.Round : 1)}</b>   ·   Meta: <b>{gm.WinCoins}</b> monedas";
        if (goal != lastGoal) { lastGoal = goal; goalText.text = goal; }

        int currentPlayer = turnManager ? turnManager.CurrentPlayer : -1;
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int c = gm.GetCoins(i);
            if (c != r.shown) { r.pulse = r.shown == int.MinValue ? 0f : 1f; r.shown = c; r.coins.text = c.ToString(); r.coins.fontSize = c >= 100000 ? 26 : c >= 10000 ? 32 : 40; }   // con aros apilados caben números grandes
            r.pulse = Mathf.MoveTowards(r.pulse, 0f, dt * 2.5f);
            r.coins.rectTransform.localScale = Vector3.one * (1f + 0.35f * r.pulse * r.pulse);

            float x = Mathf.Lerp(r.fill.anchorMax.x, Mathf.Clamp01(c / (float)gm.WinCoins), 1f - Mathf.Exp(-8f * dt));
            r.fill.anchorMax = new Vector2(x, 1f);

            var hc = r.highlight.color;
            hc.a = Mathf.MoveTowards(hc.a, currentPlayer == i && !gm.GameOver ? 0.5f : 0f, dt * 3f);
            r.highlight.color = hc;
        }
    }

    // ---------- Turno y fase ----------

    private void BuildPhaseStrip()
    {
        if (!phaseStrip || !turnManager) return;
        var phases = turnManager.PreparationPhases
            ? new[] { TurnPhase.Shop, TurnPhase.Placement, TurnPhase.Aiming, TurnPhase.Resolving }
            : new[] { TurnPhase.Aiming, TurnPhase.Resolving };
        float w = phaseStrip.sizeDelta.x / phases.Length;
        for (int i = 0; i < phases.Length; i++)
        {
            var cell = AeroUI.Node(PhaseName(phases[i]), phaseStrip).At(new Vector2(0f, 0.5f), new Vector2(w * i, 0f), new Vector2(w, phaseStrip.sizeDelta.y));
            var dot = AeroUI.Orb(cell, "Punto", skin, Color.white, 18f);
            dot.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(18f, 18f));
            var t = AeroUI.Label(cell, "Texto", $"{i + 1}. {PhaseName(phases[i])}", 22, skin.navy, AeroWeight.Regular, TextAnchor.MiddleLeft);
            t.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(w - 40f, phaseStrip.sizeDelta.y));
            steps.Add(new Step { phase = phases[i], text = t, dot = dot });
        }
    }

    public static string PhaseName(TurnPhase p) => p switch
    {
        TurnPhase.Shop => "Compra",
        TurnPhase.Placement => "Colocación",
        TurnPhase.Aiming => "Lanzamiento",
        TurnPhase.Resolving => "Resolución",
        TurnPhase.Finishing => "Fin del turno",
        TurnPhase.GameOver => "Fin de la partida",
        _ => "",
    };

    private void UpdateTurn(GameManager gm)
    {
        if (!turnManager) return;
        var cat = turnManager.Current;
        int player = turnManager.CurrentPlayer;
        TurnPhase phase = turnManager.Phase;
        bool over = gm != null && gm.GameOver;
        float dt = Time.unscaledDeltaTime;

        if (player >= 0 && player != lastTurnPlayer)
        {
            lastTurnPlayer = player;
            turnPulse = 1f;
            turnBanner.sprite = skin.PlayerButton(player);
            turnText.color = skin.TextOn(player);
        }
        turnPulse = Mathf.MoveTowards(turnPulse, 0f, dt * 2.5f);
        turnBanner.rectTransform.localScale = Vector3.one * (1f + 0.12f * turnPulse * turnPulse);
        turnBanner.gameObject.SetActive(player >= 0 && !over);
        if (phaseStrip) phaseStrip.gameObject.SetActive(player >= 0 && !over);

        if (player >= 0)
        {
            string what = phase == TurnPhase.Aiming && turnManager.CurrentRock != null ? "¡Pelota en el aire!" : PhaseName(phase);
            turnText.text = $"Jugador {player + 1}  ·  {what}";
        }

        foreach (var s in steps)
        {
            bool active = s.phase == phase || (phase == TurnPhase.Finishing && s.phase == TurnPhase.Resolving);
            s.text.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            s.text.color = active ? skin.navy : new Color(skin.navy.r, skin.navy.g, skin.navy.b, 0.45f);
            s.dot.color = active && player >= 0 ? skin.PlayerColor(player) : new Color(1f, 1f, 1f, 0.6f);
        }

        if (phase != lastPhase)
        {
            lastPhase = phase;
            if (phase != TurnPhase.Resolving && summaryPanel) summaryPanel.Hide();
            helpText.text = HelpFor(phase);
        }

        bool showPower = cat != null && cat.IsActive && phase == TurnPhase.Aiming && !over && !paused;
        powerPanel.alpha = Mathf.MoveTowards(powerPanel.alpha, showPower ? 1f : 0f, dt * 6f);
        if (cat != null && cat.IsActive)
        {
            powerFill.rectTransform.anchorMax = new Vector2(cat.Power01, 1f);
            powerFill.color = PowerColor(cat.Power01);
            powerText.text = cat.IsCharging
                ? $"Potencia <b>{Mathf.RoundToInt(cat.Power01 * 100)}%</b>   ·   suelta ESPACIO para disparar"
                : $"Ángulo <b>{cat.Angle:0}°</b>   ·   mantén ESPACIO para cargar";
        }

        bool showHelp = !over && !paused && helpText.text.Length > 0;
        helpPanel.alpha = Mathf.MoveTowards(helpPanel.alpha, showHelp ? 1f : 0f, dt * 6f);
        pauseButton.gameObject.SetActive(!over);
    }

    private static string HelpFor(TurnPhase phase) => phase switch
    {
        TurnPhase.Shop => "<b>Compra</b>: abre la tienda y\ngasta tus monedas.\n\n<b>Enter</b>  pasar a colocación",
        TurnPhase.Placement => "<b>Colocación</b>: elige un objeto abajo\n<b>Clic</b> colocar  ·  <b>Clic der.</b> cancelar\n<b>Q / E</b> girar  ·  <b>R / F</b> inclinar\n<b>Z / X</b> ladear  ·  <b>Rueda</b> altura\nClic en un objeto puesto: retirarlo\n<b>Enter</b>  ¡a lanzar!",
        TurnPhase.Aiming => "<b>A / D</b>   mover\n<b>Q / E</b>   girar\n<b>W / S</b>   ángulo\n<b>Espacio</b>   cargar y soltar\n<b>C</b>  cámara   ·   <b>Esc</b>  pausa",
        _ => "",
    };

    private static Color PowerColor(float p)
    {
        Color low = new Color(0.35f, 0.85f, 0.3f), mid = new Color(1f, 0.85f, 0.2f), high = new Color(1f, 0.38f, 0.22f);
        return p < 0.5f ? Color.Lerp(low, mid, p * 2f) : Color.Lerp(mid, high, (p - 0.5f) * 2f);
    }

    // ---------- Resumen del tiro ----------

    private void OnShotResolved(ShotReward r, ShotRecord rec)
    {
        if (!summaryPanel) return;
        var bal = GameBalance.Current;
        var sb = new StringBuilder();
        if (r.BubbleCount > 0)
        {
            sb.Append($"Burbujas: <b>{r.BubbleCount}</b>  →  +{r.B}");
            long ringed = System.Math.Min(r.Multiplier * r.B, RewardCalculator.MaxReward);
            if (r.RingPasses == 1) sb.Append($"\nAro ×{r.Multiplier}  →  +{ringed}");
            else if (r.RingPasses > 1) sb.Append($"\nAros: {r.RingPasses} pasadas ×{r.Multiplier}  →  +{ringed}");
            if (r.P > 0) sb.Append($"\nPositivos: {r.P}  →  +{bal.positiveBonus * r.P}");
            if (r.N > 0) sb.Append($"\nNegativos: {r.N}  →  −{bal.negativePenalty * r.N}");
            if (r.R < 0) sb.Append("\n(la recompensa nunca baja de 0)");
        }
        else
        {
            sb.Append("No reventaste ninguna burbuja");
            if (rec.Touched.Count > 0 || rec.RingPasses.Count > 0) sb.Append("\nSin burbujas, los objetos no dan ni quitan");
        }
        if (r.Stolen > 0) sb.Append($"\n¡Robo de castillo!  →  +{r.Stolen}");

        int total = r.Final + r.Stolen;
        summaryTitle.text = total > 0 ? $"+{total} monedas" : "Sin monedas";
        summaryBody.text = sb.ToString();
        summaryPanel.Show();
        if (total > 0) AeroAudio.Play(r.Stolen > 0 ? Sfx.Steal : Sfx.Score);
    }

    // ---------- Etiquetas de burbujas y textos flotantes ----------

    private void UpdateBubbleLabels()
    {
        foreach (var b in Bubble.All)
        {
            if (!bubbleLabels.TryGetValue(b, out var label))
            {
                label = b.IsCastle ? CreateCastleLabel(b) : CreateValueLabel(b);
                bubbleLabels[b] = label;
            }
            bool visible = b && b.isActiveAndEnabled && !b.Popped && !paused;
            if (visible && !b.IsCastle && b.Value != label.shown) { label.shown = b.Value; label.text.text = "+" + b.Value; }
            Vector2 p = default;
            float depth = 0f;
            if (visible) visible = ToLocal(b.transform.position + (b.IsCastle ? Vector3.up * (b.Radius + 0.6f) : Vector3.zero), out p, out depth);
            label.rt.gameObject.SetActive(visible);
            if (!visible) continue;
            label.rt.anchoredPosition = p;
            label.rt.localScale = Vector3.one * Mathf.Clamp(24f / depth, 0.55f, 1.2f);   // más chica a lo lejos
        }

        // Limpia etiquetas de burbujas destruidas
        List<Bubble> gone = null;
        foreach (var kv in bubbleLabels) if (!kv.Key) (gone ??= new List<Bubble>()).Add(kv.Key);
        if (gone != null) foreach (var b in gone) { Destroy(bubbleLabels[b].rt.gameObject); bubbleLabels.Remove(b); }
    }

    private BubbleLabel CreateValueLabel(Bubble b)
    {
        var root = AeroUI.Node("Valor burbuja", worldLayer);
        root.sizeDelta = new Vector2(80f, 50f);
        var t = AeroUI.Label(root, "Valor", "+", 30, Color.white, AeroWeight.Bold);
        t.rectTransform.Stretch();
        AeroUI.Effect<Outline>(t, skin.navy, new Vector2(2f, -2f));
        return new BubbleLabel { rt = root, text = t };
    }

    private BubbleLabel CreateCastleLabel(Bubble b)
    {
        var root = AeroUI.Node("Etiqueta castillo", worldLayer);
        root.sizeDelta = new Vector2(150f, 44f);
        var sprite = skin.PlayerButton(b.OwnerId);
        AeroUI.Img(root, "Fondo", sprite, Color.white, sprite.rect.height / 44f).rectTransform.Stretch();
        var t = AeroUI.Label(root, "Texto", $"ROBA {GameBalance.Current.stealAmount}", 22, skin.TextOn(b.OwnerId), AeroWeight.Bold);
        t.rectTransform.Stretch();
        return new BubbleLabel { rt = root, text = t };
    }

    private void UpdatePopups()
    {
        foreach (var p in pending) SpawnPopup(p);
        pending.Clear();

        for (int i = floating.Count - 1; i >= 0; i--)
        {
            var f = floating[i];
            float t = (Time.time - f.start) / PopupDuration;
            if (t >= 1f) { Destroy(f.rt.gameObject); floating.RemoveAt(i); continue; }

            bool ok = ToLocal(f.world + Vector3.up * (1.2f + 2.5f * t), out Vector2 pos, out _);
            f.rt.gameObject.SetActive(ok);
            if (ok) f.rt.anchoredPosition = pos;
            float pop = t < 0.1f ? Mathf.Lerp(0.4f, 1.25f, t / 0.1f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.1f) / 0.2f));
            f.rt.localScale = Vector3.one * pop;
            f.group.alpha = 1f - t * t;
        }
    }

    private void SpawnPopup(PendingPopup p)
    {
        var root = AeroUI.Node("Texto flotante", worldLayer);
        root.sizeDelta = new Vector2(420f, 100f);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var t = AeroUI.Label(root, "Texto", p.text, 60, Color.white, AeroWeight.Bold);
        t.rectTransform.Stretch();
        AeroUI.Gradient(t, Color.white, p.color);
        AeroUI.Effect<Outline>(t, skin.navy, new Vector2(2.5f, -2.5f));
        AeroUI.Effect<Shadow>(t, new Color(0f, 0f, 0f, 0.3f), new Vector2(0f, -5f));
        floating.Add(new FloatingText { rt = root, group = group, world = p.pos, start = Time.time });
    }

    private bool ToLocal(Vector3 world, out Vector2 local, out float depth)
    {
        local = default;
        depth = 0f;
        if (!cam) return false;
        Vector3 sp = cam.WorldToScreenPoint(world);
        depth = sp.z;
        if (sp.z <= 0.1f) return false;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(worldLayer, sp, uiCam, out local);
    }

    // ---------- Pausa, victoria y escenas ----------

    public void TogglePause()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.GameOver) return;
        paused = !paused;
        Time.timeScale = paused ? 0f : 1f;
        pausePanel.SetVisible(paused);
        Select(paused ? resumeButton : null);   // sin selección, Espacio no "presiona" botones mientras juegas
    }

    private void OnPlayerWon(int player) => StartCoroutine(ShowVictory(player));

    private IEnumerator ShowVictory(int player)
    {
        yield return new WaitForSecondsRealtime(1.2f);
        if (paused) TogglePause();
        if (summaryPanel) summaryPanel.Hide();
        victoryTitle.text = $"¡Gana el Jugador {player + 1}!";
        var grad = victoryTitle.GetComponent<UIGradient>();
        if (grad) grad.SetColors(Color.white, skin.PlayerColor(player));
        victorySubtitle.text = $"Llegó a {GameManager.Instance.GetCoins(player)} monedas";
        victoryPanel.Show();
        AeroAudio.Play(Sfx.Win);
        Select(againButton);
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }

    private static void Select(Selectable s)
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(s ? s.gameObject : null);
    }

#if ENABLE_INPUT_SYSTEM
    private static bool PausePressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    private static bool RestartPressed() => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
    private static bool PausePressed() => Input.GetKeyDown(KeyCode.Escape);
    private static bool RestartPressed() => Input.GetKeyDown(KeyCode.R);
#endif
}
