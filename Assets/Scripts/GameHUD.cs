using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// HUD del juego con estilo Aero (uGUI): marcador, turno, potencia, valor de cada hueco,
// textos flotantes, pausa (Esc) y pantalla de victoria. La escena la arma CatapultasSceneBuilder.
[DefaultExecutionOrder(100)]   // después de CameraRig, para que las etiquetas no vayan un cuadro atrasadas
public class GameHUD : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private AeroSkin skin;
    [SerializeField] private ScoreHole[] scoreHoles;
    [SerializeField] private CastleHole[] castleHoles;
    [SerializeField] private string menuScene = "MenuPrincipal";

    [Header("Marcador")]
    [SerializeField] private Text goalText;
    [SerializeField] private Text[] coinTexts;
    [SerializeField] private RectTransform[] progressFills;
    [SerializeField] private Image[] rowHighlights;

    [Header("Turno y potencia")]
    [SerializeField] private Image turnBanner;
    [SerializeField] private Text turnText;
    [SerializeField] private CanvasGroup powerPanel;
    [SerializeField] private Image powerFill;
    [SerializeField] private Text powerText;
    [SerializeField] private CanvasGroup helpPanel;

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

    private class WorldLabel { public RectTransform rt; public Transform target; public Vector3 offset; public Text text; public ScoreHole hole; public int shown = -1; }
    private class FloatingText { public RectTransform rt; public CanvasGroup group; public Vector3 world; public float start; }

    private readonly List<WorldLabel> labels = new List<WorldLabel>();
    private readonly List<FloatingText> floating = new List<FloatingText>();
    private const float PopupDuration = 1.6f;

    private Canvas canvas;
    private Camera cam;
    private CatapultController lastTurn;
    private float turnPulse;
    private int[] shownCoins;
    private float[] coinPulse;
    private bool paused;

    // Texto flotante sobre un punto del mapa (lo usan los huecos al puntuar)
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

        var gm = GameManager.Instance;
        int n = gm != null ? gm.PlayerCount : coinTexts.Length;
        shownCoins = new int[n];
        coinPulse = new float[n];
        if (gm != null)
        {
            gm.PlayerWon += OnPlayerWon;
            goalText.text = $"Meta: <b>{gm.WinCoins}</b> monedas";
        }

        pauseButton.onClick.AddListener(TogglePause);
        resumeButton.onClick.AddListener(TogglePause);
        restartButton.onClick.AddListener(Restart);
        menuButton.onClick.AddListener(GoToMenu);
        againButton.onClick.AddListener(Restart);
        victoryMenuButton.onClick.AddListener(GoToMenu);

        foreach (var h in scoreHoles) if (h) CreateHoleLabel(h);
        foreach (var h in castleHoles) if (h) CreateCastleLabel(h);
        if (fade) StartCoroutine(FadeIn());
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

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.PlayerWon -= OnPlayerWon;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        bool over = gm != null && gm.GameOver;
        if (!over && PausePressed()) TogglePause();
        if (over && RestartPressed()) Restart();
    }

    private void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        var gm = GameManager.Instance;
        UpdateScores(gm);
        UpdateTurn(gm);
        UpdateLabels();
        UpdatePopups();
    }

    // ---------- Marcador, turno y potencia ----------

    private void UpdateScores(GameManager gm)
    {
        if (gm == null) return;
        var current = turnManager ? turnManager.Current : null;
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < coinTexts.Length && i < shownCoins.Length; i++)
        {
            int c = gm.GetCoins(i);
            if (c != shownCoins[i]) { shownCoins[i] = c; coinTexts[i].text = c.ToString(); coinPulse[i] = 1f; }
            coinPulse[i] = Mathf.MoveTowards(coinPulse[i], 0f, dt * 2.5f);
            coinTexts[i].rectTransform.localScale = Vector3.one * (1f + 0.35f * coinPulse[i] * coinPulse[i]);

            float target = Mathf.Clamp01(c / (float)gm.WinCoins);
            float x = Mathf.Lerp(progressFills[i].anchorMax.x, target, 1f - Mathf.Exp(-8f * dt));
            progressFills[i].anchorMax = new Vector2(x, 1f);

            bool active = current != null && current.playerId == i && !gm.GameOver;
            var hc = rowHighlights[i].color;
            hc.a = Mathf.MoveTowards(hc.a, active ? 0.5f : 0f, dt * 3f);
            rowHighlights[i].color = hc;
        }
    }

    private void UpdateTurn(GameManager gm)
    {
        var cat = turnManager ? turnManager.Current : null;
        bool over = gm != null && gm.GameOver;
        float dt = Time.unscaledDeltaTime;

        if (cat != null && cat != lastTurn)
        {
            lastTurn = cat;
            turnPulse = 1f;
            turnBanner.sprite = skin.PlayerButton(cat.playerId);
        }
        turnPulse = Mathf.MoveTowards(turnPulse, 0f, dt * 2.5f);
        turnBanner.rectTransform.localScale = Vector3.one * (1f + 0.12f * turnPulse * turnPulse);
        turnBanner.gameObject.SetActive(cat != null && !over);
        if (cat != null)
            turnText.text = turnManager.CurrentRock != null ? "¡Roca en el aire!" : $"Turno del Jugador {cat.playerId + 1}";

        bool showPower = cat != null && cat.IsActive && !over && !paused;
        powerPanel.alpha = Mathf.MoveTowards(powerPanel.alpha, showPower ? 1f : 0f, dt * 6f);
        if (cat != null && cat.IsActive)
        {
            powerFill.rectTransform.anchorMax = new Vector2(cat.Power01, 1f);
            powerFill.color = PowerColor(cat.Power01);
            powerText.text = cat.IsCharging
                ? $"Potencia <b>{Mathf.RoundToInt(cat.Power01 * 100)}%</b>   ·   suelta ESPACIO para disparar"
                : $"Ángulo <b>{cat.Angle:0}°</b>   ·   mantén ESPACIO para cargar";
        }

        helpPanel.alpha = Mathf.MoveTowards(helpPanel.alpha, !over && !paused ? 1f : 0f, dt * 6f);
        pauseButton.gameObject.SetActive(!over);
    }

    private static Color PowerColor(float p)
    {
        Color low = new Color(0.35f, 0.85f, 0.3f), mid = new Color(1f, 0.85f, 0.2f), high = new Color(1f, 0.38f, 0.22f);
        return p < 0.5f ? Color.Lerp(low, mid, p * 2f) : Color.Lerp(mid, high, (p - 0.5f) * 2f);
    }

    // ---------- Etiquetas y textos flotantes ----------

    private void CreateHoleLabel(ScoreHole hole)
    {
        var root = AeroUI.Node("Etiqueta " + hole.name, worldLayer);
        root.sizeDelta = new Vector2(64f, 64f);
        AeroUI.Img(root, "Burbuja", skin.bubble, Color.white).rectTransform.Stretch(-6f);
        var t = AeroUI.Label(root, "Valor", "+", 30, Color.white, AeroWeight.Bold);
        t.rectTransform.Stretch();
        AeroUI.Effect<Outline>(t, skin.navy, new Vector2(2f, -2f));
        labels.Add(new WorldLabel { rt = root, target = hole.transform, offset = Vector3.up * 0.7f, text = t, hole = hole });
    }

    private void CreateCastleLabel(CastleHole hole)
    {
        var root = AeroUI.Node("Etiqueta castillo", worldLayer);
        root.sizeDelta = new Vector2(150f, 44f);
        var sprite = skin.PlayerButton(hole.OwnerId);
        AeroUI.Img(root, "Fondo", sprite, Color.white, sprite.rect.height / 44f).rectTransform.Stretch();
        var t = AeroUI.Label(root, "Texto", $"ROBA {hole.StealAmount}", 22, Color.white, AeroWeight.Bold);
        t.rectTransform.Stretch();
        AeroUI.Effect<Shadow>(t, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -1.5f));
        labels.Add(new WorldLabel { rt = root, target = hole.transform, offset = Vector3.up * 1.2f, text = t });
    }

    private void UpdateLabels()
    {
        foreach (var l in labels)
        {
            if (l.hole && l.hole.Points != l.shown) { l.shown = l.hole.Points; l.text.text = "+" + l.shown; }
            Vector2 p = default;
            float depth = 0f;
            bool ok = l.target && ToLocal(l.target.position + l.offset, out p, out depth);
            l.rt.gameObject.SetActive(ok && !paused);
            if (!ok) continue;
            l.rt.anchoredPosition = p;
            l.rt.localScale = Vector3.one * Mathf.Clamp(22f / depth, 0.55f, 1.15f);   // más chico a lo lejos
        }
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
        var t = AeroUI.Label(root, "Texto", p.text, 64, Color.white, AeroWeight.Bold);
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
        yield return new WaitForSecondsRealtime(0.9f);
        if (paused) TogglePause();
        Color c = PlayerColor(player);
        victoryTitle.text = $"¡Gana el Jugador {player + 1}!";
        var grad = victoryTitle.GetComponent<UIGradient>();
        if (grad) grad.SetColors(Color.white, c);
        victorySubtitle.text = $"Llegó a {GameManager.Instance.GetCoins(player)} monedas";
        victoryPanel.Show();
        AeroAudio.Play(Sfx.Win);
        Select(againButton);
    }

    private Color PlayerColor(int player)
    {
        if (turnManager)
            foreach (var c in turnManager.Catapults)
                if (c && c.playerId == player) return c.playerColor;
        return skin.PlayerColor(player);
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
