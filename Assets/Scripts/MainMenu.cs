using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Menú principal: Jugar (abre "Nueva partida": 2, 3 o 4 jugadores), Opciones (volumen, música, meta) y Salir.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "Catapultas";
    [SerializeField] private AeroSkin skin;

    [Header("Paneles")]
    [SerializeField] private UIFadePanel mainPanel;
    [SerializeField] private UIFadePanel optionsPanel;
    [SerializeField] private UIFadePanel newGamePanel;
    [SerializeField] private CanvasGroup fade;            // pantalla blanca para entrar y salir

    [Header("Botones")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;

    [Header("Nueva partida")]
    [SerializeField] private Button[] playerCountButtons;  // 2, 3 y 4 jugadores
    [SerializeField] private Image[] playerCountPreview;   // orbes con los colores de los jugadores
    [SerializeField] private Button startButton;
    [SerializeField] private Button newGameBackButton;

    [Header("Opciones")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider coinsSlider;          // en decenas: 2 = 20 monedas, 20 = 200
    [SerializeField] private Text volumeValue;
    [SerializeField] private Text musicValue;
    [SerializeField] private Text coinsValue;

    private bool leaving;
    private int selectedPlayers;

    private void Start()
    {
        Time.timeScale = 1f;

        playButton.onClick.AddListener(ShowNewGame);
        optionsButton.onClick.AddListener(ShowOptions);
        quitButton.onClick.AddListener(Quit);
        backButton.onClick.AddListener(ShowMain);
        startButton.onClick.AddListener(Play);
        newGameBackButton.onClick.AddListener(ShowMain);
        for (int i = 0; i < playerCountButtons.Length; i++)
        {
            int count = MatchConfig.MinPlayers + i;
            playerCountButtons[i].onClick.AddListener(() => SelectPlayers(count));
        }

        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        musicSlider.SetValueWithoutNotify(GameSettings.Music);
        coinsSlider.SetValueWithoutNotify(Mathf.Round(GameSettings.WinCoins / 10f));
        volumeSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; RefreshLabels(); });
        musicSlider.onValueChanged.AddListener(v => { GameSettings.Music = v; RefreshLabels(); });
        coinsSlider.onValueChanged.AddListener(v => { GameSettings.WinCoins = Mathf.RoundToInt(v) * 10; RefreshLabels(); });
        RefreshLabels();
        SelectPlayers(GameSettings.LastPlayerCount);

        mainPanel.Show();
        optionsPanel.Hide();
        newGamePanel.Hide();
        Select(playButton);
        if (fade) StartCoroutine(Fade(1f, 0f, 0.6f));
    }

    private void Update()
    {
        if ((optionsPanel.Visible || newGamePanel.Visible) && BackPressed()) ShowMain();
    }

    public void ShowNewGame()
    {
        mainPanel.Hide();
        newGamePanel.Show();
        Select(startButton);
    }

    public void SelectPlayers(int count)
    {
        selectedPlayers = Mathf.Clamp(count, MatchConfig.MinPlayers, MatchConfig.MaxPlayers);
        for (int i = 0; i < playerCountButtons.Length; i++)
        {
            bool on = MatchConfig.MinPlayers + i == selectedPlayers;
            var img = playerCountButtons[i].targetGraphic as Image;
            if (img) img.sprite = on ? skin.buttonGreen : skin.buttonSilver;
            var label = playerCountButtons[i].GetComponentInChildren<Text>();
            if (label) label.color = on ? Color.white : skin.navy;
        }
        // Vista previa: un orbe por jugador, con su color
        for (int i = 0; i < playerCountPreview.Length; i++)
        {
            playerCountPreview[i].gameObject.SetActive(i < selectedPlayers);
            playerCountPreview[i].color = skin.PlayerColor(i);
        }
    }

    // La cantidad de jugadores queda fija al empezar; cambiarla requiere volver al menú
    public void Play()
    {
        if (leaving) return;
        leaving = true;
        MatchConfig.PlayerCount = selectedPlayers;
        GameSettings.LastPlayerCount = selectedPlayers;
        GameSettings.Save();
        StartCoroutine(LoadGame());
    }

    public void ShowOptions()
    {
        mainPanel.Hide();
        optionsPanel.Show();
        Select(volumeSlider);
    }

    public void ShowMain()
    {
        GameSettings.Save();
        optionsPanel.Hide();
        newGamePanel.Hide();
        mainPanel.Show();
        Select(playButton);
    }

    public void Quit()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RefreshLabels()
    {
        volumeValue.text = Mathf.RoundToInt(volumeSlider.value * 100f) + "%";
        musicValue.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        coinsValue.text = (Mathf.RoundToInt(coinsSlider.value) * 10).ToString();
    }

    private IEnumerator LoadGame()
    {
        mainPanel.Hide();
        newGamePanel.Hide();
        if (fade) yield return Fade(0f, 1f, 0.45f);
        SceneManager.LoadScene(gameScene);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        fade.gameObject.SetActive(true);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        fade.alpha = to;
        if (to <= 0f) fade.gameObject.SetActive(false);
    }

    private static void Select(Selectable s)
    {
        if (EventSystem.current && s) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

#if ENABLE_INPUT_SYSTEM
    private static bool BackPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
    private static bool BackPressed() => Input.GetKeyDown(KeyCode.Escape);
#endif
}
