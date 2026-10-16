using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Menú principal: Jugar, Opciones (volumen, música, meta de monedas) y Salir.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "Catapultas";

    [Header("Paneles")]
    [SerializeField] private UIFadePanel mainPanel;
    [SerializeField] private UIFadePanel optionsPanel;
    [SerializeField] private CanvasGroup fade;            // pantalla blanca para entrar y salir

    [Header("Botones")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;

    [Header("Opciones")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider coinsSlider;          // en decenas: 2 = 20 monedas, 20 = 200
    [SerializeField] private Text volumeValue;
    [SerializeField] private Text musicValue;
    [SerializeField] private Text coinsValue;

    private bool leaving;

    private void Start()
    {
        Time.timeScale = 1f;

        playButton.onClick.AddListener(Play);
        optionsButton.onClick.AddListener(ShowOptions);
        quitButton.onClick.AddListener(Quit);
        backButton.onClick.AddListener(ShowMain);

        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        musicSlider.SetValueWithoutNotify(GameSettings.Music);
        coinsSlider.SetValueWithoutNotify(Mathf.Round(GameSettings.WinCoins / 10f));
        volumeSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; RefreshLabels(); });
        musicSlider.onValueChanged.AddListener(v => { GameSettings.Music = v; RefreshLabels(); });
        coinsSlider.onValueChanged.AddListener(v => { GameSettings.WinCoins = Mathf.RoundToInt(v) * 10; RefreshLabels(); });
        RefreshLabels();

        mainPanel.Show();
        optionsPanel.Hide();
        Select(playButton);
        if (fade) StartCoroutine(Fade(1f, 0f, 0.6f));
    }

    private void Update()
    {
        if (optionsPanel.Visible && BackPressed()) ShowMain();
    }

    public void Play()
    {
        if (leaving) return;
        leaving = true;
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
        mainPanel.Show();
        Select(optionsButton);
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
