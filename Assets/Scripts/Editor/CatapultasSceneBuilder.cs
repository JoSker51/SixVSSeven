using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Arma las dos escenas con estilo Frutiger Aero:
//  - MenuPrincipal: isla con el castillo de fondo y los botones Jugar / Opciones / Salir.
//  - Catapultas: el juego (pasos 1 a 3 del documento) con HUD, pausa y pantalla de victoria.
// Menú: Catapultas > Construir escenas (menú + juego)
public static class CatapultasSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/MenuPrincipal.unity";
    private const string GameScenePath = "Assets/Scenes/Catapultas.unity";
    private const string MatFolder = "Assets/Materials";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string RockPrefabPath = PrefabFolder + "/Rock.prefab";
    private const string VolumePath = "Assets/Settings/AeroVolume.asset";

    private const float HalfMap = 24f;           // la cuadrícula de juego mide 48 x 48
    private const float IslandHalf = 33f;        // la isla (con árboles) mide 66 x 66
    private const float CastleDistance = 19f;
    private const float CatapultDistance = 12.5f;
    private static readonly Vector3 SunEuler = new Vector3(45f, 150f, 0f);

    private static readonly Color[] PlayerColors = { new Color(0.17f, 0.55f, 1f), new Color(1f, 0.55f, 0.1f) };
    private static readonly Color Navy = new Color(0.04f, 0.24f, 0.4f);

    // Huecos simétricos (giro de 180°) para que el mapa sea justo: (x, z, radio)
    private static readonly Vector3[] Holes =
    {
        new Vector3(0f, 0f, 0.8f),
        new Vector3(-6f, 3f, 1.4f),   new Vector3(6f, -3f, 1.4f),
        new Vector3(-4f, -6f, 1.0f),  new Vector3(4f, 6f, 1.0f),
        new Vector3(-11f, -5f, 1.6f), new Vector3(11f, 5f, 1.6f),
        new Vector3(-14f, 8f, 2.0f),  new Vector3(14f, -8f, 2.0f),
    };

    private static AeroSkin skin;
    private static VolumeProfile volumeProfile;
    private static Material groundMat, outerGrassMat, sandMat, waterMat, marbleMat, railMat, aquaMat, woodMat, darkWoodMat,
                            holeMat, rimMat, rockMat, trunkMat, lineMat, bubbleMat, skyMat;
    private static Material[] leafMats, playerMats, markerMats;

    [MenuItem("Catapultas/Construir escenas (menú + juego)")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAll();
        EditorSceneManager.OpenScene(MenuScenePath);
        EditorUtility.DisplayDialog("Catapultas", "Escenas creadas:\n• " + MenuScenePath + "\n• " + GameScenePath + "\n\nAbre MenuPrincipal y dale a Play.", "OK");
    }

    // Nombre anterior, por si alguien lo llama desde línea de comandos
    public static void Build() => BuildAll();

    public static void BuildAll()
    {
        EnsureTag("Ground");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Scenes");
        foreach (var old in new[] { "Ground.mat", "Piedra.mat", "PiedraOscura.mat", "Cuadricula.asset" })
            AssetDatabase.DeleteAsset(MatFolder + "/" + old);   // restos de la versión anterior

        // Cada escena nueva descarga los assets sin usar, así que los materiales se recargan para cada una
        var game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AeroArtGenerator.GenerateAll(SunDirection);
        CreateVolumeProfile();
        SetupMaterials();
        BuildGameScene(CreateRockPrefab());
        EditorSceneManager.SaveScene(game, GameScenePath);

        var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupMaterials();
        BuildMenuScene();
        EditorSceneManager.SaveScene(menu, MenuScenePath);

        SetBuildScenes(MenuScenePath, GameScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Catapultas] Escenas creadas: " + MenuScenePath + " y " + GameScenePath);
    }

    private static Vector3 SunDirection => -(Quaternion.Euler(SunEuler) * Vector3.forward);

    // =====================================================================
    // Escena de juego
    // =====================================================================

    private static void BuildGameScene(Rock rockPrefab)
    {
        CreateEnvironment(CreateSun());
        var cam = CreateCamera(new Vector3(34f, 30f, 0f), Vector3.zero);

        new GameObject("GameManager").AddComponent<GameManager>();
        CreatePlayArea(gameplay: true);
        var turnManager = new GameObject("TurnManager").AddComponent<TurnManager>();

        var holesRoot = new GameObject("Huecos").transform;
        var scoreHoles = new List<ScoreHole>();
        foreach (var h in Holes)
            scoreHoles.Add((ScoreHole)CreateScoreHole(holesRoot, new Vector3(h.x, 0f, h.y), h.z, true));

        var castleHoles = new List<CastleHole>();
        var catapults = new List<CatapultController>();
        for (int p = 0; p < 2; p++)
        {
            float side = p == 0 ? -1f : 1f;   // Jugador 1 abajo mirando a +z; Jugador 2 arriba mirando a -z
            Quaternion facing = Quaternion.LookRotation(new Vector3(0f, 0f, -side));
            castleHoles.Add(CreateCastle(p, new Vector3(0f, 0f, side * CastleDistance), facing, true));
            catapults.Add(CreateCatapult(p, new Vector3(0f, 0f, side * CatapultDistance), facing, rockPrefab));
        }
        Set(turnManager, ("catapults", catapults.ToArray()));

        Set(cam.gameObject.AddComponent<CameraRig>(), ("turnManager", turnManager));

        CreateEventSystem();
        BuildHUD(turnManager, scoreHoles, castleHoles, catapults.Count);
    }

    private static void BuildHUD(TurnManager turnManager, List<ScoreHole> scoreHoles, List<CastleHole> castleHoles, int players)
    {
        var canvas = CreateCanvas("HUD");
        Transform root = canvas.transform;
        var hud = canvas.gameObject.AddComponent<GameHUD>();

        var world = AeroUI.Node("Etiquetas", root).Stretch();

        // --- Marcador ---
        const float rowH = 74f;
        var board = AeroUI.Img(root, "Marcador", skin.glassPanel, Color.white, 1.4f).rectTransform
            .At(new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(440f, 76f + rowH * players));
        var goal = AeroUI.Label(board, "Meta", "Meta: 100 monedas", 26, Navy, AeroWeight.Regular, TextAnchor.MiddleLeft);
        goal.rectTransform.At(new Vector2(0f, 1f), new Vector2(26f, -12f), new Vector2(390f, 40f));

        var coinTexts = new Text[players];
        var fills = new RectTransform[players];
        var highlights = new Image[players];
        for (int i = 0; i < players; i++)
        {
            Color c = PlayerColors[i];
            var row = AeroUI.Node($"Jugador {i + 1}", board).At(new Vector2(0.5f, 1f), new Vector2(0f, -58f - rowH * i), new Vector2(414f, rowH - 8f));
            highlights[i] = AeroUI.Img(row, "Resaltado", skin.fill, new Color(c.r, c.g, c.b, 0f), skin.fill.rect.height / (rowH - 8f));
            highlights[i].rectTransform.Stretch();
            AeroUI.Orb(row, "Color", skin, c, 44f).rectTransform.At(new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 44f));
            AeroUI.Label(row, "Nombre", $"Jugador {i + 1}", 28, Navy, AeroWeight.Bold, TextAnchor.MiddleLeft)
                .rectTransform.At(new Vector2(0f, 1f), new Vector2(68f, -4f), new Vector2(220f, 34f));
            var track = AeroUI.Img(row, "Barra", skin.track, Color.white, skin.track.rect.height / 14f);
            track.rectTransform.At(new Vector2(0f, 0f), new Vector2(68f, 11f), new Vector2(230f, 14f));
            var fill = AeroUI.Img(track.transform, "Progreso", skin.fill, c, skin.fill.rect.height / 14f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fills[i] = fill.rectTransform;
            coinTexts[i] = AeroUI.Label(row, "Monedas", "0", 40, Navy, AeroWeight.Bold, TextAnchor.MiddleRight);
            coinTexts[i].rectTransform.At(new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(110f, 50f));
            AeroUI.Effect<Shadow>(coinTexts[i], new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, -2f));
        }

        // --- Turno ---
        var banner = AeroUI.Img(root, "Turno", skin.buttonBlue, Color.white, skin.buttonBlue.rect.height / 84f);
        banner.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(620f, 84f));
        AeroUI.Effect<Shadow>(banner, new Color(0.02f, 0.15f, 0.3f, 0.3f), new Vector2(0f, -5f));
        var turnText = AeroUI.Label(banner.transform, "Texto", "Turno del Jugador 1", 38, Color.white, AeroWeight.Bold);
        turnText.rectTransform.Stretch();
        AeroUI.Effect<Shadow>(turnText, new Color(0.02f, 0.18f, 0.32f, 0.55f), new Vector2(0f, -2f));

        // --- Botón de pausa ---
        var pauseOrb = AeroUI.Orb(root, "Pausa", skin, new Color(0.35f, 0.7f, 1f), 84f);
        pauseOrb.rectTransform.At(new Vector2(1f, 1f), new Vector2(-28f, -26f), new Vector2(84f, 84f));
        pauseOrb.raycastTarget = true;
        AeroUI.Effect<Shadow>(pauseOrb, new Color(0.02f, 0.15f, 0.3f, 0.3f), new Vector2(0f, -4f));
        var pauseButton = pauseOrb.gameObject.AddComponent<Button>();
        pauseButton.targetGraphic = pauseOrb;
        pauseButton.navigation = new Navigation { mode = Navigation.Mode.None };
        pauseOrb.gameObject.AddComponent<AeroButtonFX>();
        var pauseIcon = AeroUI.Label(pauseOrb.transform, "Icono", "II", 34, Color.white, AeroWeight.Bold);
        pauseIcon.rectTransform.Stretch();
        AeroUI.Effect<Shadow>(pauseIcon, new Color(0.02f, 0.18f, 0.32f, 0.6f), new Vector2(0f, -2f));

        // --- Potencia ---
        var power = AeroUI.Img(root, "Potencia", skin.glassPanel, Color.white, 1.6f);
        power.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(720f, 128f));
        var powerGroup = power.gameObject.AddComponent<CanvasGroup>();
        powerGroup.alpha = 0f;
        powerGroup.blocksRaycasts = false;
        var powerText = AeroUI.Label(power.transform, "Info", "Ángulo 45°", 28, Navy);
        powerText.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(680f, 40f));
        var powerTrack = AeroUI.Img(power.transform, "Barra", skin.track, Color.white, skin.track.rect.height / 36f);
        powerTrack.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(640f, 36f));
        var powerFill = AeroUI.Img(powerTrack.transform, "Relleno", skin.fill, new Color(0.35f, 0.85f, 0.3f), skin.fill.rect.height / 36f);
        powerFill.rectTransform.anchorMin = Vector2.zero;
        powerFill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        powerFill.rectTransform.offsetMin = powerFill.rectTransform.offsetMax = Vector2.zero;

        // --- Controles ---
        var help = AeroUI.Img(root, "Controles", skin.glassPanel, Color.white, 1.4f);
        help.rectTransform.At(new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(340f, 222f));
        var helpGroup = help.gameObject.AddComponent<CanvasGroup>();
        helpGroup.blocksRaycasts = false;
        var helpText = AeroUI.Label(help.transform, "Texto",
            "<b>A / D</b>   mover\n<b>Q / E</b>   girar\n<b>W / S</b>   ángulo\n<b>Espacio</b>   cargar y soltar\n<b>C</b>  cámara   ·   <b>Esc</b>  pausa",
            23, Navy, AeroWeight.Regular, TextAnchor.MiddleLeft);
        helpText.lineSpacing = 1.15f;
        helpText.rectTransform.Stretch(24f);

        // --- Pausa ---
        var pauseRoot = AeroUI.Node("PanelPausa", root).Stretch();
        pauseRoot.gameObject.AddComponent<CanvasGroup>().alpha = 0f;   // oculto también en el editor
        var pausePanel = pauseRoot.gameObject.AddComponent<UIFadePanel>();
        Set(pausePanel, ("startVisible", false));
        var dim = AeroUI.Img(pauseRoot, "Oscurecer", null, new Color(0.02f, 0.12f, 0.25f, 0.45f));
        dim.raycastTarget = true;
        dim.rectTransform.Stretch();
        var pauseBox = AeroUI.Img(pauseRoot, "Caja", skin.glassPanel, Color.white).rectTransform
            .At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 560f));
        Title(pauseBox, "Pausa", 72, -36f);
        var resume = Place(AeroUI.Button(pauseBox, "Continuar", "Continuar", skin.buttonGreen, new Vector2(420f, 96f), Color.white), -170f);
        var restart = Place(AeroUI.Button(pauseBox, "Reiniciar", "Reiniciar", skin.buttonBlue, new Vector2(420f, 96f), Color.white), -290f);
        var menuBtn = Place(AeroUI.Button(pauseBox, "Menu", "Menú principal", skin.buttonSilver, new Vector2(420f, 96f), Navy), -410f);

        // --- Victoria ---
        var winRoot = AeroUI.Node("PanelVictoria", root).Stretch();
        winRoot.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        var winPanel = winRoot.gameObject.AddComponent<UIFadePanel>();
        Set(winPanel, ("startVisible", false), ("duration", 0.35f), ("hiddenScale", 0.85f));
        var winDim = AeroUI.Img(winRoot, "Oscurecer", null, new Color(0.02f, 0.12f, 0.25f, 0.4f));
        winDim.raycastTarget = true;
        winDim.rectTransform.Stretch();
        var winBubbles = AeroUI.Node("Burbujas", winRoot).Stretch();
        Set(winBubbles.gameObject.AddComponent<UIBubbles>(), ("bubble", skin.bubble), ("count", 26), ("speedRange", new Vector2(60f, 140f)));
        var winBox = AeroUI.Img(winRoot, "Caja", skin.glassPanel, Color.white).rectTransform
            .At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 520f));
        var winTitle = AeroUI.Label(winBox, "Titulo", "¡Gana el Jugador 1!", 84, Color.white, AeroWeight.Bold);
        winTitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(860f, 110f));
        AeroUI.Gradient(winTitle, Color.white, PlayerColors[0]);
        AeroUI.Effect<Outline>(winTitle, Navy, new Vector2(3f, -3f));
        AeroUI.Effect<Shadow>(winTitle, new Color(0.02f, 0.15f, 0.3f, 0.35f), new Vector2(0f, -7f));
        var winSub = AeroUI.Label(winBox, "Subtitulo", "Llegó a 100 monedas", 36, Navy, AeroWeight.Light);
        winSub.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(860f, 50f));
        var again = AeroUI.Button(winBox, "OtraVez", "Jugar otra vez", skin.buttonGreen, new Vector2(370f, 96f), Color.white, 40);
        ((RectTransform)again.transform).At(new Vector2(0.5f, 0f), new Vector2(-200f, 70f), new Vector2(370f, 96f));
        var winMenu = AeroUI.Button(winBox, "Menu", "Menú principal", skin.buttonSilver, new Vector2(370f, 96f), Navy, 40);
        ((RectTransform)winMenu.transform).At(new Vector2(0.5f, 0f), new Vector2(200f, 70f), new Vector2(370f, 96f));

        // Fundido blanco al entrar (continúa el del menú)
        var fade = AeroUI.Img(root, "Fundido", null, Color.white);
        fade.rectTransform.Stretch();
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        Set(hud, ("fade", fadeGroup),
            ("turnManager", turnManager), ("skin", skin),
            ("scoreHoles", scoreHoles.ToArray()), ("castleHoles", castleHoles.ToArray()),
            ("goalText", goal), ("coinTexts", coinTexts), ("progressFills", fills), ("rowHighlights", highlights),
            ("turnBanner", banner), ("turnText", turnText),
            ("powerPanel", powerGroup), ("powerFill", powerFill), ("powerText", powerText), ("helpPanel", helpGroup),
            ("pauseButton", pauseButton), ("pausePanel", pausePanel),
            ("resumeButton", resume), ("restartButton", restart), ("menuButton", menuBtn),
            ("victoryPanel", winPanel), ("victoryTitle", winTitle), ("victorySubtitle", winSub),
            ("againButton", again), ("victoryMenuButton", winMenu),
            ("worldLayer", world));
    }

    // =====================================================================
    // Menú principal
    // =====================================================================

    private static void BuildMenuScene()
    {
        CreateEnvironment(CreateSun());
        CreatePlayArea(gameplay: false);
        var holesRoot = new GameObject("Huecos").transform;
        foreach (var h in Holes) CreateScoreHole(holesRoot, new Vector3(h.x, 0f, h.y), h.z, false);
        for (int p = 0; p < 2; p++)
        {
            float side = p == 0 ? -1f : 1f;
            Quaternion facing = Quaternion.LookRotation(new Vector3(0f, 0f, -side));
            CreateCastle(p, new Vector3(0f, 0f, side * CastleDistance), facing, false);
            CreateCatapult(p, new Vector3(0f, 0f, side * CatapultDistance), facing, null);
        }

        // Encuadre: las cúpulas del castillo asoman entre el título y los botones
        var lookAt = new Vector3(0f, 2.6f, -19f);
        var cam = CreateCamera(new Vector3(0f, 7f, 1f), lookAt);
        Set(cam.gameObject.AddComponent<MenuCameraSway>(), ("lookAt", lookAt));

        CreateEventSystem();
        BuildMenuUI();
    }

    private static void BuildMenuUI()
    {
        var canvas = CreateCanvas("Menu");
        Transform root = canvas.transform;
        var menu = canvas.gameObject.AddComponent<MainMenu>();

        // Adornos de fondo: auroras y burbujas
        var aurora = AeroUI.Img(root, "Aurora", skin.swoosh, new Color(1f, 1f, 1f, 0.55f));
        aurora.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(2400f, 560f));
        Set(aurora.gameObject.AddComponent<UIFloat>(), ("amplitude", new Vector2(70f, 14f)), ("speed", 0.06f));
        var aurora2 = AeroUI.Img(root, "Aurora 2", skin.swoosh, new Color(0.85f, 0.97f, 1f, 0.35f));
        aurora2.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(2600f, 420f));
        aurora2.rectTransform.localScale = new Vector3(1f, -1f, 1f);
        Set(aurora2.gameObject.AddComponent<UIFloat>(), ("amplitude", new Vector2(-90f, 10f)), ("speed", 0.045f), ("phase", 1.5f));
        var bubbles = AeroUI.Node("Burbujas", root).Stretch();
        Set(bubbles.gameObject.AddComponent<UIBubbles>(), ("bubble", skin.bubble), ("count", 18));

        // Título
        var title = AeroUI.Node("Titulo", root).At(new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1400f, 300f));
        Set(title.gameObject.AddComponent<UIFloat>(), ("amplitude", new Vector2(0f, 7f)), ("speed", 0.25f));
        AeroUI.Img(title, "Resplandor", skin.glow, new Color(1f, 1f, 1f, 0.75f)).rectTransform
            .At(new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1300f, 420f));
        var titleText = AeroUI.Label(title, "Texto", "Catapultas", 168, Color.white, AeroWeight.Bold);
        titleText.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(1400f, 200f));
        AeroUI.Gradient(titleText, Color.white, new Color(0.42f, 0.8f, 1f));
        AeroUI.Effect<Outline>(titleText, Navy, new Vector2(3.5f, -3.5f));
        AeroUI.Effect<Shadow>(titleText, new Color(0.02f, 0.15f, 0.3f, 0.35f), new Vector2(0f, -8f));
        var sub = AeroUI.Label(title, "Subtitulo", "y castillos", 54, new Color(0.1f, 0.4f, 0.66f), AeroWeight.Regular);
        sub.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 70f));
        AeroUI.Effect<Outline>(sub, new Color(1f, 1f, 1f, 0.85f), new Vector2(1.5f, -1.5f));

        // Panel principal (sobre el castillo)
        var main = AeroUI.Node("PanelPrincipal", root).At(new Vector2(0.5f, 0.5f), new Vector2(0f, -235f), new Vector2(560f, 410f));
        main.gameObject.AddComponent<CanvasGroup>();
        var mainPanel = main.gameObject.AddComponent<UIFadePanel>();
        AeroUI.Img(main, "Vidrio", skin.glassPanel, new Color(1f, 1f, 1f, 0.9f)).rectTransform.Stretch();
        var play = Place(AeroUI.Button(main, "Jugar", "Jugar", skin.buttonGreen, new Vector2(440f, 100f), Color.white, 46), -36f);
        var options = Place(AeroUI.Button(main, "Opciones", "Opciones", skin.buttonBlue, new Vector2(440f, 100f), Color.white, 46), -154f);
        var quit = Place(AeroUI.Button(main, "Salir", "Salir", skin.buttonSilver, new Vector2(440f, 100f), Navy, 46), -272f);

        // Panel de opciones
        var opt = AeroUI.Node("PanelOpciones", root).At(new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(880f, 600f));
        opt.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        var optPanel = opt.gameObject.AddComponent<UIFadePanel>();
        Set(optPanel, ("startVisible", false));
        AeroUI.Img(opt, "Vidrio", skin.glassPanel, Color.white).rectTransform.Stretch();
        Title(opt, "Opciones", 64, -28f);
        var volume = OptionRow(opt, "Volumen general", -150f, 0f, 1f, false, out var volumeValue);
        var music = OptionRow(opt, "Música", -250f, 0f, 1f, false, out var musicValue);
        var coins = OptionRow(opt, "Monedas para ganar", -350f, 2f, 20f, true, out var coinsValue);
        AeroUI.Label(opt, "Nota", "La meta de monedas se aplica al empezar una partida", 24, new Color(Navy.r, Navy.g, Navy.b, 0.75f), AeroWeight.Light)
            .rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -425f), new Vector2(820f, 34f));
        var back = AeroUI.Button(opt, "Volver", "Volver", skin.buttonBlue, new Vector2(320f, 90f), Color.white, 40);
        ((RectTransform)back.transform).At(new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(320f, 90f));

        // Pie de página
        var version = AeroUI.Label(root, "Version", "Demo · v0.2", 24, Color.white, AeroWeight.Light, TextAnchor.LowerRight);
        version.rectTransform.At(new Vector2(1f, 0f), new Vector2(-30f, 22f), new Vector2(400f, 40f));
        AeroUI.Effect<Outline>(version, new Color(0.02f, 0.18f, 0.32f, 0.55f), new Vector2(1.2f, -1.2f));
        var hint = AeroUI.Label(root, "Ayuda", "Mouse, o flechas + Enter", 24, Color.white, AeroWeight.Regular, TextAnchor.LowerLeft);
        hint.rectTransform.At(new Vector2(0f, 0f), new Vector2(30f, 22f), new Vector2(600f, 40f));
        AeroUI.Effect<Outline>(hint, new Color(0.02f, 0.18f, 0.32f, 0.55f), new Vector2(1.2f, -1.2f));

        // Fundido a blanco al entrar y al empezar a jugar
        var fade = AeroUI.Img(root, "Fundido", null, Color.white);
        fade.rectTransform.Stretch();
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;   // MainMenu lo pone en blanco al arrancar y lo desvanece
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        Set(menu,
            ("mainPanel", mainPanel), ("optionsPanel", optPanel), ("fade", fadeGroup),
            ("playButton", play), ("optionsButton", options), ("quitButton", quit), ("backButton", back),
            ("volumeSlider", volume), ("musicSlider", music), ("coinsSlider", coins),
            ("volumeValue", volumeValue), ("musicValue", musicValue), ("coinsValue", coinsValue));
    }

    private static Slider OptionRow(RectTransform parent, string label, float y, float min, float max, bool whole, out Text value)
    {
        AeroUI.Label(parent, label, label, 32, Navy, AeroWeight.Regular, TextAnchor.MiddleLeft)
            .rectTransform.At(new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(330f, 50f));
        var slider = AeroUI.Slider(parent, "Slider " + label, new Vector2(330f, 44f), skin, new Color(0.3f, 0.75f, 1f), new Color(0.35f, 0.7f, 1f));
        ((RectTransform)slider.transform).At(new Vector2(0f, 1f), new Vector2(390f, y - 3f), new Vector2(330f, 44f));
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.value = max;
        value = AeroUI.Label(parent, "Valor " + label, "100", 32, Navy, AeroWeight.Bold, TextAnchor.MiddleRight);
        value.rectTransform.At(new Vector2(1f, 1f), new Vector2(-50f, y), new Vector2(110f, 50f));
        return slider;
    }

    private static void Title(RectTransform parent, string text, int size, float y)
    {
        var t = AeroUI.Label(parent, "Titulo", text, size, Navy, AeroWeight.Bold);
        t.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(parent.sizeDelta.x - 40f, size * 1.3f));
        AeroUI.Gradient(t, new Color(0.15f, 0.55f, 0.9f), Navy);
        AeroUI.Effect<Shadow>(t, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -2f));
    }

    // Coloca un botón centrado horizontalmente a cierta altura desde el borde de arriba del padre
    private static Button Place(Button b, float y)
    {
        var rt = (RectTransform)b.transform;
        rt.At(new Vector2(0.5f, 1f), new Vector2(0f, y), rt.sizeDelta);
        return b;
    }

    // =====================================================================
    // Mundo: cielo, agua, isla, árboles, burbujas y postproceso
    // =====================================================================

    private static void CreateEnvironment(Light sun)
    {
        RenderSettings.skybox = skyMat;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.82f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.92f, 0.98f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.55f, 0.38f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.82f, 0.94f, 1f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 260f;

        var env = new GameObject("Entorno").transform;
        Prim(PrimitiveType.Plane, "Agua", env, new Vector3(0f, -0.8f, 0f), new Vector3(60f, 1f, 60f), waterMat, false);
        Prim(PrimitiveType.Cube, "Isla (arena)", env, new Vector3(0f, -1.7f, 0f), new Vector3(IslandHalf * 2f, 3.1f, IslandHalf * 2f), sandMat, false);
        var outer = Prim(PrimitiveType.Plane, "Cesped exterior", env, new Vector3(0f, -0.04f, 0f), new Vector3(IslandHalf / 5f, 1f, IslandHalf / 5f), outerGrassMat);
        outer.tag = "Ground";
        CreateTrees(env);
        CreateBubbleParticles(env);

        var probe = new GameObject("Reflejos").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(env);
        probe.transform.position = new Vector3(0f, 4f, 0f);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(300f, 100f, 300f);
        probe.resolution = 128;
        probe.hdr = true;
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.cullingMask = ~(1 << 5);

        var volume = new GameObject("Postproceso").AddComponent<Volume>();
        volume.transform.SetParent(env);
        volume.isGlobal = true;
        volume.sharedProfile = volumeProfile;
    }

    private static void CreateTrees(Transform parent)
    {
        var root = new GameObject("Arboles").transform;
        root.SetParent(parent);
        var rng = new System.Random(11);
        var spots = new List<Vector3>();
        for (int tries = 0; spots.Count < 28 && tries < 800; tries++)
        {
            var p = new Vector3((float)(rng.NextDouble() * 2 - 1) * 31.5f, -0.04f, (float)(rng.NextDouble() * 2 - 1) * 31.5f);
            float m = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            if (m < 26.5f) continue;
            bool near = false;
            foreach (var s in spots) if ((s - p).sqrMagnitude < 18f) { near = true; break; }
            if (near) continue;
            spots.Add(p);

            var t = new GameObject("Arbol").transform;
            t.SetParent(root);
            t.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            t.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.6f);
            Prim(PrimitiveType.Cylinder, "Tronco", t, new Vector3(0f, 1f, 0f), new Vector3(0.45f, 1f, 0.45f), trunkMat, false);
            Prim(PrimitiveType.Sphere, "Copa", t, new Vector3(0f, 2.6f, 0f), Vector3.one * 2.6f, leafMats[0], false);
            Prim(PrimitiveType.Sphere, "Copa", t, new Vector3(0.8f, 2.2f, 0.3f), Vector3.one * 1.8f, leafMats[1], false);
            Prim(PrimitiveType.Sphere, "Copa", t, new Vector3(-0.6f, 3.3f, -0.2f), Vector3.one * 1.7f, leafMats[2], false);
        }
    }

    // Burbujas que suben desde el agua alrededor de la isla
    private static void CreateBubbleParticles(Transform parent)
    {
        var go = new GameObject("Burbujas");
        go.transform.SetParent(parent);
        go.transform.SetPositionAndRotation(new Vector3(0f, -0.8f, 0f), Quaternion.Euler(-90f, 0f, 0f));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 16f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 40f;
        shape.radiusThickness = 0.3f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.2f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = bubbleMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private static Light CreateSun()
    {
        var light = new GameObject("Sol").AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.65f;
        light.transform.rotation = Quaternion.Euler(SunEuler);
        return light;
    }

    private static Camera CreateCamera(Vector3 pos, Vector3 lookAt)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        go.transform.position = pos;
        go.transform.LookAt(lookAt);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        return cam;
    }

    // =====================================================================
    // Cancha, huecos, castillos y catapultas
    // =====================================================================

    private static MapSettings CreatePlayArea(bool gameplay)
    {
        var ground = Prim(PrimitiveType.Plane, "Ground (cuadrícula)", null, Vector3.zero, new Vector3(HalfMap / 5f, 1f, HalfMap / 5f), groundMat);
        ground.tag = "Ground";

        // Barandas brillantes alrededor, con orbes en las esquinas
        var rails = new GameObject("Barandas").transform;
        float e = HalfMap + 0.3f, len = HalfMap * 2f + 0.6f;
        Prim(PrimitiveType.Capsule, "Baranda N", rails, new Vector3(0f, 0.3f, e), new Vector3(0.55f, len / 2f, 0.55f), railMat, true, Quaternion.Euler(0f, 0f, 90f));
        Prim(PrimitiveType.Capsule, "Baranda S", rails, new Vector3(0f, 0.3f, -e), new Vector3(0.55f, len / 2f, 0.55f), railMat, true, Quaternion.Euler(0f, 0f, 90f));
        Prim(PrimitiveType.Capsule, "Baranda E", rails, new Vector3(e, 0.3f, 0f), new Vector3(0.55f, len / 2f, 0.55f), railMat, true, Quaternion.Euler(90f, 0f, 0f));
        Prim(PrimitiveType.Capsule, "Baranda O", rails, new Vector3(-e, 0.3f, 0f), new Vector3(0.55f, len / 2f, 0.55f), railMat, true, Quaternion.Euler(90f, 0f, 0f));
        foreach (var c in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) })
            Prim(PrimitiveType.Sphere, "Orbe esquina", rails, new Vector3(c.x * e, 0.55f, c.z * e), Vector3.one * 1.1f, aquaMat, false);

        if (!gameplay) return null;

        var map = new GameObject("MapSettings").AddComponent<MapSettings>();
        map.corners = new Transform[4];
        int i = 0;
        foreach (var c in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) })
        {
            var corner = new GameObject("Esquina " + (i + 1)).transform;
            corner.SetParent(map.transform);
            corner.position = c * HalfMap;
            map.corners[i++] = corner;
        }
        map.referenceRadius = 1.5f;
        map.basePoints = 2;
        map.maxCornerDistance = HalfMap * 1.4142f;   // el centro del mapa es lo más lejos posible de una esquina
        return map;
    }

    private static Component CreateScoreHole(Transform parent, Vector3 pos, float radius, bool gameplay)
    {
        var go = new GameObject($"ScoreHole (r={radius})");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        AddHoleVisual(go.transform, radius, rimMat);
        if (!gameplay) return null;
        AddHoleTrigger(go, radius);
        return go.AddComponent<ScoreHole>();
    }

    private static void AddHoleTrigger(GameObject go, float radius)
    {
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(radius * 2f, 1.2f, radius * 2f);
        box.center = new Vector3(0f, 0.6f, 0f);
    }

    private static void AddHoleVisual(Transform parent, float radius, Material rim)
    {
        Prim(PrimitiveType.Cylinder, "Borde", parent, new Vector3(0f, 0.01f, 0f), new Vector3(radius * 2f + 0.4f, 0.02f, radius * 2f + 0.4f), rim, false);
        Prim(PrimitiveType.Cylinder, "Agujero", parent, new Vector3(0f, 0.025f, 0f), new Vector3(radius * 2f, 0.01f, radius * 2f), holeMat, false);
    }

    private static CastleHole CreateCastle(int player, Vector3 center, Quaternion facing, bool gameplay)
    {
        var root = new GameObject($"Castillo J{player + 1}").transform;
        root.SetPositionAndRotation(center, facing);
        var mat = playerMats[player];

        const float h = 3f;      // mitad del patio interior
        const float t = 0.6f;    // grosor del muro
        const float wallH = 2.2f;
        const float gate = 2.2f; // ancho de la puerta
        float outer = h + t;

        Prim(PrimitiveType.Cube, "Muro trasero", root, new Vector3(0f, wallH / 2f, -h - t / 2f), new Vector3(outer * 2f, wallH, t), marbleMat);
        Prim(PrimitiveType.Cube, "Muro izq", root, new Vector3(-h - t / 2f, wallH / 2f, 0f), new Vector3(t, wallH, h * 2f), marbleMat);
        Prim(PrimitiveType.Cube, "Muro der", root, new Vector3(h + t / 2f, wallH / 2f, 0f), new Vector3(t, wallH, h * 2f), marbleMat);
        float segLen = outer - gate / 2f, segX = gate / 2f + segLen / 2f;
        Prim(PrimitiveType.Cube, "Muro frontal izq", root, new Vector3(-segX, wallH / 2f, h + t / 2f), new Vector3(segLen, wallH, t), marbleMat);
        Prim(PrimitiveType.Cube, "Muro frontal der", root, new Vector3(segX, wallH / 2f, h + t / 2f), new Vector3(segLen, wallH, t), marbleMat);
        Prim(PrimitiveType.Cube, "Dintel", root, new Vector3(0f, wallH - 0.35f, h + t / 2f), new Vector3(gate, 0.7f, t), mat);
        Prim(PrimitiveType.Sphere, "Gema", root, new Vector3(0f, wallH + 0.35f, h + t / 2f), Vector3.one * 0.7f, mat, false);

        AddMerlons(root, new Vector3(-outer, wallH, -h - t / 2f), new Vector3(outer, wallH, -h - t / 2f));
        AddMerlons(root, new Vector3(-h - t / 2f, wallH, -h), new Vector3(-h - t / 2f, wallH, h));
        AddMerlons(root, new Vector3(h + t / 2f, wallH, -h), new Vector3(h + t / 2f, wallH, h));
        AddMerlons(root, new Vector3(-outer, wallH, h + t / 2f), new Vector3(-gate / 2f - 0.6f, wallH, h + t / 2f));
        AddMerlons(root, new Vector3(gate / 2f + 0.6f, wallH, h + t / 2f), new Vector3(outer, wallH, h + t / 2f));

        // Torres con cúpula brillante y bandera
        float c = h + t / 2f;
        foreach (var corner in new[] { new Vector3(-c, 0, -c), new Vector3(c, 0, -c), new Vector3(-c, 0, c), new Vector3(c, 0, c) })
        {
            Prim(PrimitiveType.Cylinder, "Torre", root, corner + Vector3.up * 1.75f, new Vector3(1.8f, 1.75f, 1.8f), marbleMat);
            Prim(PrimitiveType.Cylinder, "Anillo", root, corner + Vector3.up * 3.5f, new Vector3(2.05f, 0.08f, 2.05f), marbleMat, false);
            Prim(PrimitiveType.Sphere, "Cúpula", root, corner + Vector3.up * 3.5f, Vector3.one * 1.95f, mat, false);
            Prim(PrimitiveType.Sphere, "Remate", root, corner + Vector3.up * 4.55f, Vector3.one * 0.35f, marbleMat, false);
            Prim(PrimitiveType.Cylinder, "Mástil", root, corner + Vector3.up * 5.05f, new Vector3(0.07f, 0.5f, 0.07f), marbleMat, false);
            Prim(PrimitiveType.Cube, "Bandera", root, corner + new Vector3(0.4f, 5.3f, 0f), new Vector3(0.75f, 0.45f, 0.05f), mat, false);
        }

        // Hueco de castigo en el patio (bajo el castillo)
        var holeGo = new GameObject($"CastleHole J{player + 1}");
        holeGo.transform.SetParent(root, false);
        AddHoleVisual(holeGo.transform, 1.5f, mat);
        if (!gameplay) return null;
        AddHoleTrigger(holeGo, 1.5f);
        var hole = holeGo.AddComponent<CastleHole>();
        Set(hole, ("ownerId", player));
        return hole;
    }

    private static void AddMerlons(Transform root, Vector3 from, Vector3 to)
    {
        float len = Vector3.Distance(from, to);
        int count = Mathf.Max(2, Mathf.RoundToInt(len / 0.9f));
        bool alongX = Mathf.Abs((to - from).normalized.x) > 0.5f;
        for (int i = 0; i < count; i += 2)
        {
            Vector3 p = Vector3.Lerp(from, to, (i + 0.5f) / count) + Vector3.up * 0.2f;
            Prim(PrimitiveType.Cube, "Almena", root, p, alongX ? new Vector3(0.45f, 0.4f, 0.6f) : new Vector3(0.6f, 0.4f, 0.45f), marbleMat, false);
        }
    }

    // rockPrefab null = catapulta de adorno (para el menú)
    private static CatapultController CreateCatapult(int player, Vector3 pos, Quaternion facing, Rock rockPrefab)
    {
        bool gameplay = rockPrefab != null;
        var root = new GameObject($"Catapulta J{player + 1}").transform;
        root.SetPositionAndRotation(pos, facing);

        Prim(PrimitiveType.Cube, "Base", root, new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 0.35f, 2.4f), playerMats[player]);
        foreach (var w in new[] { new Vector3(-0.88f, 0.375f, -0.8f), new Vector3(0.88f, 0.375f, -0.8f), new Vector3(-0.88f, 0.375f, 0.8f), new Vector3(0.88f, 0.375f, 0.8f) })
            Prim(PrimitiveType.Cylinder, "Rueda", root, w, new Vector3(0.75f, 0.08f, 0.75f), darkWoodMat, false, Quaternion.Euler(0f, 0f, 90f));
        Prim(PrimitiveType.Cube, "Poste izq", root, new Vector3(-0.45f, 1.35f, 0.2f), new Vector3(0.15f, 1.3f, 0.15f), woodMat, false);
        Prim(PrimitiveType.Cube, "Poste der", root, new Vector3(0.45f, 1.35f, 0.2f), new Vector3(0.15f, 1.3f, 0.15f), woodMat, false);
        Prim(PrimitiveType.Cylinder, "Eje", root, new Vector3(0f, 1.75f, 0.2f), new Vector3(0.12f, 0.5f, 0.12f), darkWoodMat, false, Quaternion.Euler(0f, 0f, 90f));

        var pivot = new GameObject("ArmPivot").transform;
        pivot.SetParent(root, false);
        pivot.localPosition = new Vector3(0f, 1.75f, 0.2f);
        pivot.localRotation = Quaternion.Euler(-110f, 0f, 0f);   // brazo cargado hacia atrás
        Prim(PrimitiveType.Cube, "Brazo", pivot, new Vector3(0f, 1f, 0f), new Vector3(0.16f, 2.4f, 0.16f), woodMat, false);
        Prim(PrimitiveType.Cube, "Cuchara", pivot, new Vector3(0f, 2.1f, 0.1f), new Vector3(0.6f, 0.6f, 0.12f), darkWoodMat, false);
        var loaded = Prim(PrimitiveType.Sphere, "Roca cargada", pivot, new Vector3(0f, 2.1f, 0.45f), Vector3.one * 0.6f, rockMat, false);
        if (!gameplay) return null;

        var launch = new GameObject("LaunchPoint").transform;
        launch.SetParent(root, false);
        launch.localPosition = new Vector3(0f, 3.8f, 1f);

        var marker = Prim(PrimitiveType.Sphere, "Indicador turno", root, new Vector3(0f, 5.2f, 0f), Vector3.one * 0.45f, markerMats[player], false);
        marker.AddComponent<MarkerSpin>();

        var line = new GameObject("Trayectoria").AddComponent<LineRenderer>();
        line.transform.SetParent(root, false);
        line.sharedMaterial = lineMat;
        line.useWorldSpace = true;
        line.widthCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 0.05f);
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        Color pc = PlayerColors[player];
        line.startColor = new Color(1f, 1f, 1f, 0.95f);
        line.endColor = new Color(pc.r, pc.g, pc.b, 0f);

        var ctrl = root.gameObject.AddComponent<CatapultController>();
        ctrl.playerId = player;
        ctrl.playerColor = PlayerColors[player];
        Set(ctrl, ("launchPoint", launch), ("rockPrefab", rockPrefab), ("armPivot", pivot),
                  ("activeMarker", marker), ("loadedRock", loaded), ("trajectory", line));
        return ctrl;
    }

    private static Rock CreateRockPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Rock";
        go.transform.localScale = Vector3.one * 0.7f;
        go.GetComponent<Renderer>().sharedMaterial = rockMat;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.angularDamping = 0.8f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        go.AddComponent<Rock>();

        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = lineMat;
        trail.time = 0.45f;
        trail.widthCurve = AnimationCurve.Linear(0f, 0.45f, 1f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;

        PrefabUtility.SaveAsPrefabAsset(go, RockPrefabPath);
        Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabPath).GetComponent<Rock>();
    }

    // =====================================================================
    // Materiales y postproceso
    // =====================================================================

    private static void SetupMaterials()
    {
        skin = AssetDatabase.LoadAssetAtPath<AeroSkin>(AeroArtGenerator.SkinPath);
        volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);

        groundMat = Lit("Cesped", Color.white, 0.32f);
        groundMat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AeroArtGenerator.GrassGridPath);
        groundMat.mainTextureScale = Vector2.one * (HalfMap / 2f);   // celdas de 2 x 2 unidades
        outerGrassMat = Lit("CespedExterior", Color.white, 0.28f);
        outerGrassMat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AeroArtGenerator.GrassPath);
        outerGrassMat.mainTextureScale = Vector2.one * (IslandHalf / 2f);

        sandMat = Lit("Arena", new Color(0.97f, 0.9f, 0.7f), 0.35f);
        waterMat = Lit("Agua", new Color(0.13f, 0.66f, 0.88f), 0.96f, 0.1f);
        marbleMat = Lit("Marmol", new Color(0.95f, 0.97f, 0.99f), 0.78f);
        railMat = Lit("Baranda", new Color(0.86f, 0.96f, 1f), 0.9f);
        aquaMat = Lit("Aqua", new Color(0.35f, 0.8f, 1f), 0.95f);
        woodMat = Lit("Madera", new Color(0.86f, 0.58f, 0.33f), 0.6f);
        darkWoodMat = Lit("MaderaOscura", new Color(0.5f, 0.3f, 0.17f), 0.55f);
        holeMat = Lit("Hueco", new Color(0.03f, 0.17f, 0.32f), 0.85f);
        rimMat = Lit("BordeHueco", new Color(0.88f, 0.97f, 1f), 0.92f);
        rockMat = Lit("Roca", new Color(0.92f, 0.95f, 0.97f), 0.88f);
        trunkMat = Lit("Tronco", new Color(0.62f, 0.4f, 0.22f), 0.45f);
        leafMats = new[]
        {
            Lit("Hojas1", new Color(0.36f, 0.8f, 0.27f), 0.6f),
            Lit("Hojas2", new Color(0.52f, 0.88f, 0.33f), 0.6f),
            Lit("Hojas3", new Color(0.27f, 0.7f, 0.24f), 0.6f),
        };

        playerMats = new Material[PlayerColors.Length];
        markerMats = new Material[PlayerColors.Length];
        for (int i = 0; i < PlayerColors.Length; i++)
        {
            playerMats[i] = Lit($"Jugador{i + 1}", PlayerColors[i], 0.88f);
            markerMats[i] = Lit($"Indicador{i + 1}", Color.Lerp(PlayerColors[i], Color.white, 0.3f), 0.95f, 0f, PlayerColors[i] * 1.6f);
        }

        lineMat = SaveMat("Linea", new Material(Shader.Find("Sprites/Default")));
        bubbleMat = SaveMat("Burbuja", new Material(Shader.Find("Sprites/Default")));
        bubbleMat.mainTexture = skin.bubble.texture;
        skyMat = SaveMat("Cielo", new Material(Shader.Find("Skybox/Panoramic")));
        skyMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AeroArtGenerator.SkyPath));
        skyMat.SetFloat("_Exposure", 1f);
        skyMat.SetFloat("_Rotation", 0f);
    }

    private static void CreateVolumeProfile()
    {
        AssetDatabase.DeleteAsset(VolumePath);
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, VolumePath);

        var bloom = p.Add<Bloom>(true);
        bloom.intensity.Override(0.55f);
        bloom.threshold.Override(0.95f);
        bloom.scatter.Override(0.72f);
        var color = p.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.12f);
        color.saturation.Override(16f);
        color.contrast.Override(8f);
        var vignette = p.Add<Vignette>(true);
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.03f, 0.18f, 0.32f));
        var tone = p.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        foreach (var c in p.components)
        {
            c.name = c.GetType().Name;
            AssetDatabase.AddObjectToAsset(c, p);
        }
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
    }

    private static Material Lit(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var m = SaveMat(name, new Material(shader));
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Reutiliza el asset si ya existe (así reconstruir no rompe referencias)
    private static Material SaveMat(string name, Material fresh)
    {
        string path = $"{MatFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }
        existing.shader = fresh.shader;
        Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
                                   Material mat, bool keepCollider = true, Quaternion? localRot = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // Asigna campos privados [SerializeField] desde el editor
    private static void Set(Object target, params (string name, object value)[] values)
    {
        var so = new SerializedObject(target);
        foreach (var (name, value) in values)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogError($"[Catapultas] {target.GetType().Name} no tiene el campo '{name}'"); continue; }
            switch (value)
            {
                case Object[] arr:
                    p.arraySize = arr.Length;
                    for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
                    break;
                case Object o: p.objectReferenceValue = o; break;
                case bool b: p.boolValue = b; break;
                case int n: p.intValue = n; break;
                case float f: p.floatValue = f; break;
                case string s: p.stringValue = s; break;
                case Vector2 v: p.vector2Value = v; break;
                case Vector3 v3: p.vector3Value = v3; break;
                case Color c: p.colorValue = c; break;
                default: Debug.LogError($"[Catapultas] Tipo no soportado para '{name}'"); break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    private static void EnsureTag(string tag)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    // El menú queda primero: es la escena que abre el juego compilado
    private static void SetBuildScenes(params string[] paths)
    {
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var p in paths) scenes.Add(new EditorBuildSettingsScene(p, true));
        foreach (var s in EditorBuildSettings.scenes)
            if (System.Array.IndexOf(paths, s.path) < 0) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
