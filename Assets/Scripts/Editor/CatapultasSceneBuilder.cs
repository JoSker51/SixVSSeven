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
//  - MenuPrincipal: la isla con la arena de fondo, y Jugar (2 a 4 jugadores) / Opciones / Salir.
//  - Catapultas: arena circular, burbujas, castillos y catapultas (se crean al empezar según los jugadores), HUD.
// También genera los prefabs (castillo, catapulta, burbuja, pelota) y las mallas de la arena.
// Menú: Catapultas > Construir escenas (menú + juego)
// La tienda y los objetos colocables se agregan en CatapultasSceneBuilder.Tienda.cs (métodos parciales).
public static partial class CatapultasSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/MenuPrincipal.unity";
    private const string GameScenePath = "Assets/Scenes/Catapultas.unity";
    private const string MatFolder = "Assets/Materials";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MeshFolder = "Assets/Arte/Mallas";
    private const string RockPrefabPath = PrefabFolder + "/Rock.prefab";
    private const string BubblePrefabPath = PrefabFolder + "/Burbuja.prefab";
    private const string PopFxPrefabPath = PrefabFolder + "/BurbujaRevienta.prefab";
    private const string CastlePrefabPath = PrefabFolder + "/Castillo.prefab";
    private const string CatapultPrefabPath = PrefabFolder + "/Catapulta.prefab";
    private const string VolumePath = "Assets/Settings/AeroVolume.asset";
    private const string BalancePath = "Assets/Resources/GameBalance.asset";

    private const float IslandRadius = 36f;
    private static readonly Vector3 SunEuler = new Vector3(45f, 150f, 0f);
    private static readonly Color Navy = new Color(0.04f, 0.24f, 0.4f);

    private static AeroSkin skin;
    private static GameBalance balance;
    private static VolumeProfile volumeProfile;
    private static Material groundMat, outerGrassMat, sandMat, waterMat, marbleMat, railMat, aquaMat, woodMat, darkWoodMat,
                            rockMat, trunkMat, lineMat, bubbleMat, skyMat, dividerMat, sectionMat;
    private static Material[] leafMats, playerMats, markerMats;
    private static Mesh floorMesh, wallMesh, wallColliderMesh, railMesh, hubMesh, islandMesh;

    // Ganchos para la parte de tienda y objetos (si ese archivo no está, el compilador los ignora)
    static partial void BuildShopAssets();
    static partial void BuildPreparationUI(Transform hudRoot, GameHUD hud, TurnManager turnManager);

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
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets", "Arte");
        EnsureFolder("Assets/Arte", "Mallas");
        foreach (var old in new[] { MatFolder + "/Ground.mat", MatFolder + "/Piedra.mat", MatFolder + "/PiedraOscura.mat", MatFolder + "/Cuadricula.asset",
                                    MatFolder + "/Hueco.mat", MatFolder + "/BordeHueco.mat", "Assets/Arte/UI/BotonNaranja.png" })
            AssetDatabase.DeleteAsset(old);   // restos de versiones anteriores (huecos, campo cuadrado)

        // Cada escena nueva descarga los assets sin usar, así que todo se recarga por ruta para cada una
        var game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AeroArtGenerator.GenerateAll(SunDirection);
        CreateVolumeProfile();
        balance = EnsureBalance();
        SetupMaterials();
        CreateMeshes();
        CreatePrefabs();
        BuildShopAssets();
        EditorUtility.SetDirty(balance);
        AssetDatabase.SaveAssets();
        BuildGameScene();
        EditorSceneManager.SaveScene(game, GameScenePath);

        var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        balance = EnsureBalance();
        SetupMaterials();
        LoadMeshes();
        BuildMenuScene();
        EditorSceneManager.SaveScene(menu, MenuScenePath);

        SetBuildScenes(MenuScenePath, GameScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Catapultas] Escenas creadas: " + MenuScenePath + " y " + GameScenePath);
    }

    private static Vector3 SunDirection => -(Quaternion.Euler(SunEuler) * Vector3.forward);

    // Los números del juego: si el asset ya existe se respeta (el grupo lo puede ajustar a mano)
    private static GameBalance EnsureBalance()
    {
        var b = AssetDatabase.LoadAssetAtPath<GameBalance>(BalancePath);
        if (b == null)
        {
            b = ScriptableObject.CreateInstance<GameBalance>();
            AssetDatabase.CreateAsset(b, BalancePath);
        }
        return b;
    }

    // =====================================================================
    // Escena de juego
    // =====================================================================

    private static void BuildGameScene()
    {
        CreateEnvironment(CreateSun());
        var cam = CreateCamera(new Vector3(0f, 52f, -36f), Vector3.zero);

        new GameObject("GameManager").AddComponent<GameManager>();
        var arena = CreateArena();
        var turnManager = new GameObject("TurnManager").AddComponent<TurnManager>();
        Set(turnManager, ("preparationPhases", false));
        var field = new GameObject("Burbujas").AddComponent<BubbleField>();
        Set(field, ("bubblePrefab", Load<GameObject>(BubblePrefabPath).GetComponent<Bubble>()));

        var setup = new GameObject("MatchSetup").AddComponent<MatchSetup>();
        Set(setup,
            ("arena", arena), ("turnManager", turnManager), ("bubbleField", field),
            ("catapultPrefab", Load<GameObject>(CatapultPrefabPath).GetComponent<CatapultController>()),
            ("castlePrefab", Load<GameObject>(CastlePrefabPath).GetComponent<PlayerPaint>()),
            ("bubblePrefab", Load<GameObject>(BubblePrefabPath).GetComponent<Bubble>()),
            ("skin", skin), ("playerMaterials", playerMats), ("markerMaterials", markerMats),
            ("dividerMaterial", dividerMat), ("sectionMaterial", sectionMat));

        Set(cam.gameObject.AddComponent<CameraRig>(), ("turnManager", turnManager), ("overviewPosition", new Vector3(0f, 52f, -36f)));

        CreateEventSystem();
        BuildHUD(turnManager);
    }

    private static void BuildHUD(TurnManager turnManager)
    {
        var canvas = CreateCanvas("HUD");
        Transform root = canvas.transform;
        var hud = canvas.gameObject.AddComponent<GameHUD>();

        var world = AeroUI.Node("Etiquetas", root).Stretch();

        // Marcador (las filas las crea GameHUD según cuántos jueguen)
        var board = AeroUI.Img(root, "Marcador", skin.glassPanel, Color.white, 1.4f).rectTransform
            .At(new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(460f, 78f + 74f * 2));
        var goal = AeroUI.Label(board, "Meta", "Ronda 1 · Meta: 100 monedas", 24, Navy, AeroWeight.Regular, TextAnchor.MiddleLeft);
        goal.rectTransform.At(new Vector2(0f, 1f), new Vector2(26f, -12f), new Vector2(410f, 40f));

        // Turno y fase
        var banner = AeroUI.Img(root, "Turno", skin.PlayerButton(0), Color.white, skin.buttonBlue.rect.height / 84f);
        banner.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(660f, 84f));
        AeroUI.Effect<Shadow>(banner, new Color(0.02f, 0.15f, 0.3f, 0.3f), new Vector2(0f, -5f));
        var turnText = AeroUI.Label(banner.transform, "Texto", "Jugador 1 · Lanzamiento", 36, Color.white, AeroWeight.Bold);
        turnText.rectTransform.Stretch();
        AeroUI.Effect<Shadow>(turnText, new Color(0.02f, 0.18f, 0.32f, 0.45f), new Vector2(0f, -2f));
        var strip = AeroUI.Img(root, "Fases", skin.glassPanel, Color.white, 2.4f).rectTransform
            .At(new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(800f, 46f));

        // Botón de pausa
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

        // Potencia
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

        // Ayuda (cambia según la fase)
        var help = AeroUI.Img(root, "Controles", skin.glassPanel, Color.white, 1.4f);
        help.rectTransform.At(new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(400f, 330f));
        var helpGroup = help.gameObject.AddComponent<CanvasGroup>();
        helpGroup.blocksRaycasts = false;
        var helpText = AeroUI.Label(help.transform, "Texto", "", 22, Navy, AeroWeight.Regular, TextAnchor.MiddleLeft);
        helpText.lineSpacing = 1.15f;
        helpText.rectTransform.Stretch(22f);

        // Resumen del tiro
        var summaryRoot = AeroUI.Node("Resumen", root).At(new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(560f, 230f));
        summaryRoot.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        var summaryPanel = summaryRoot.gameObject.AddComponent<UIFadePanel>();
        Set(summaryPanel, ("startVisible", false), ("duration", 0.25f));
        AeroUI.Img(summaryRoot, "Vidrio", skin.glassPanel, Color.white).rectTransform.Stretch();
        var summaryTitle = AeroUI.Label(summaryRoot, "Titulo", "+0 monedas", 48, Color.white, AeroWeight.Bold);
        summaryTitle.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(520f, 64f));
        AeroUI.Gradient(summaryTitle, Color.white, new Color(1f, 0.8f, 0.25f));
        AeroUI.Effect<Outline>(summaryTitle, Navy, new Vector2(2.5f, -2.5f));
        var summaryBody = AeroUI.Label(summaryRoot, "Detalle", "", 26, Navy, AeroWeight.Regular, TextAnchor.UpperCenter);
        summaryBody.lineSpacing = 1.1f;
        summaryBody.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(520f, 140f));

        // Pausa
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

        // Victoria
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
        AeroUI.Gradient(winTitle, Color.white, skin.PlayerColor(0));
        AeroUI.Effect<Outline>(winTitle, Navy, new Vector2(3f, -3f));
        AeroUI.Effect<Shadow>(winTitle, new Color(0.02f, 0.15f, 0.3f, 0.35f), new Vector2(0f, -7f));
        var winSub = AeroUI.Label(winBox, "Subtitulo", "Llegó a 100 monedas", 36, Navy, AeroWeight.Light);
        winSub.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(860f, 50f));
        var again = AeroUI.Button(winBox, "OtraVez", "Jugar otra vez", skin.buttonGreen, new Vector2(370f, 96f), Color.white, 40);
        ((RectTransform)again.transform).At(new Vector2(0.5f, 0f), new Vector2(-200f, 70f), new Vector2(370f, 96f));
        var winMenu = AeroUI.Button(winBox, "Menu", "Menú principal", skin.buttonSilver, new Vector2(370f, 96f), Navy, 40);
        ((RectTransform)winMenu.transform).At(new Vector2(0.5f, 0f), new Vector2(200f, 70f), new Vector2(370f, 96f));

        Set(hud,
            ("turnManager", turnManager), ("skin", skin),
            ("board", board), ("goalText", goal),
            ("turnBanner", banner), ("turnText", turnText), ("phaseStrip", strip),
            ("powerPanel", powerGroup), ("powerFill", powerFill), ("powerText", powerText),
            ("helpPanel", helpGroup), ("helpText", helpText),
            ("summaryPanel", summaryPanel), ("summaryTitle", summaryTitle), ("summaryBody", summaryBody),
            ("pauseButton", pauseButton), ("pausePanel", pausePanel),
            ("resumeButton", resume), ("restartButton", restart), ("menuButton", menuBtn),
            ("victoryPanel", winPanel), ("victoryTitle", winTitle), ("victorySubtitle", winSub),
            ("againButton", again), ("victoryMenuButton", winMenu),
            ("worldLayer", world));

        // Tienda, inventario y retirada (parte B): van antes de los paneles de pausa y victoria
        int pauseIndex = pauseRoot.GetSiblingIndex();
        int before = root.childCount;
        BuildPreparationUI(root, hud, turnManager);
        var added = new List<Transform>();
        for (int i = before; i < root.childCount; i++) added.Add(root.GetChild(i));
        for (int k = 0; k < added.Count; k++) added[k].SetSiblingIndex(pauseIndex + k);

        // Fundido blanco al entrar (continúa el del menú), siempre encima de todo
        var fade = AeroUI.Img(root, "Fundido", null, Color.white);
        fade.rectTransform.Stretch();
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;
        Set(hud, ("fade", fadeGroup));
    }

    // =====================================================================
    // Menú principal
    // =====================================================================

    private static void BuildMenuScene()
    {
        CreateEnvironment(CreateSun());
        CreateArenaVisuals(null);

        // De adorno: los cuatro castillos y catapultas con sus colores, y algunas burbujas flotando
        int n = MatchConfig.MaxPlayers;
        var deco = new GameObject("Decoracion").transform;
        for (int i = 0; i < n; i++)
        {
            Vector3 dir = Arena.Direction(Arena.PlayerAngle(i, n));
            Quaternion facing = Quaternion.LookRotation(-dir);
            var castle = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(CastlePrefabPath), deco);
            castle.transform.SetPositionAndRotation(dir * balance.castleRingRadius, facing);
            castle.GetComponent<PlayerPaint>().Apply(playerMats[i], null);
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(CatapultPrefabPath), deco);
            cat.transform.SetPositionAndRotation(dir * balance.catapultRingRadius, facing);
            cat.GetComponent<PlayerPaint>().Apply(playerMats[i], markerMats[i]);
            var cb = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BubblePrefabPath), deco);
            cb.transform.position = dir * balance.castleRingRadius + Vector3.up * balance.castleBubbleHeight;
            cb.transform.localScale = Vector3.one * balance.castleBubbleRadius * 2f;
            cb.GetComponentInChildren<SpriteRenderer>().color = Color.Lerp(skin.PlayerColor(i), Color.white, 0.45f);
        }
        var rng = new System.Random(5);
        for (int k = 0; k < 14; k++)
        {
            float a = (float)rng.NextDouble() * 360f, d = 4f + (float)rng.NextDouble() * 12f;
            var b = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BubblePrefabPath), deco);
            b.transform.position = Arena.Direction(a) * d + Vector3.up * (1.5f + (float)rng.NextDouble() * 4f);
            b.transform.localScale = Vector3.one * (1.1f + (float)rng.NextDouble() * 1.1f);
        }

        // Encuadre: las cúpulas del castillo del Jugador 1 asoman entre el título y los botones
        var lookAt = new Vector3(0f, 2.6f, -balance.castleRingRadius);
        var cam = CreateCamera(new Vector3(0f, 7f, -balance.castleRingRadius + 20f), lookAt);
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

        // Nueva partida: cuántos jugadores y a cuántas monedas
        var ng = AeroUI.Node("PanelNuevaPartida", root).At(new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(880f, 640f));
        ng.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        var ngPanel = ng.gameObject.AddComponent<UIFadePanel>();
        Set(ngPanel, ("startVisible", false));
        AeroUI.Img(ng, "Vidrio", skin.glassPanel, Color.white).rectTransform.Stretch();
        Title(ng, "Nueva partida", 64, -28f);
        AeroUI.Label(ng, "Pregunta", "¿Cuántos juegan?", 32, Navy).rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(800f, 44f));
        var countButtons = new Button[MatchConfig.MaxPlayers - MatchConfig.MinPlayers + 1];
        for (int i = 0; i < countButtons.Length; i++)
        {
            int count = MatchConfig.MinPlayers + i;
            countButtons[i] = AeroUI.Button(ng, $"{count} jugadores", count.ToString(), skin.buttonSilver, new Vector2(200f, 100f), Navy, 54);
            ((RectTransform)countButtons[i].transform).At(new Vector2(0.5f, 1f), new Vector2((i - 1) * 240f, -162f), new Vector2(200f, 100f));
        }
        var preview = new Image[MatchConfig.MaxPlayers];
        for (int i = 0; i < preview.Length; i++)
        {
            preview[i] = AeroUI.Orb(ng, $"Color J{i + 1}", skin, skin.PlayerColor(i), 52f);
            preview[i].rectTransform.At(new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 76f, -284f), new Vector2(52f, 52f));
        }
        // Meta: el slider recorre GameBalance.winCoinChoices (el MainMenu pone el rango al arrancar)
        var goal = OptionRow(ng, "Monedas para ganar", -372f, 0f, 1f, true, out var goalValue);
        AeroUI.Label(ng, "Nota", "Un tiro normal da unas 3 monedas; cada pasada por un aro las duplica", 24, new Color(Navy.r, Navy.g, Navy.b, 0.8f), AeroWeight.Light)
            .rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, -436f), new Vector2(820f, 34f));
        var ngBack = AeroUI.Button(ng, "Volver", "Volver", skin.buttonSilver, new Vector2(280f, 90f), Navy, 40);
        ((RectTransform)ngBack.transform).At(new Vector2(0.5f, 0f), new Vector2(-170f, 36f), new Vector2(280f, 90f));
        var start = AeroUI.Button(ng, "Empezar", "¡Empezar!", skin.buttonGreen, new Vector2(320f, 90f), Color.white, 42);
        ((RectTransform)start.transform).At(new Vector2(0.5f, 0f), new Vector2(170f, 36f), new Vector2(320f, 90f));

        // Opciones
        var opt = AeroUI.Node("PanelOpciones", root).At(new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(880f, 440f));
        opt.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        var optPanel = opt.gameObject.AddComponent<UIFadePanel>();
        Set(optPanel, ("startVisible", false));
        AeroUI.Img(opt, "Vidrio", skin.glassPanel, Color.white).rectTransform.Stretch();
        Title(opt, "Opciones", 64, -28f);
        var volume = OptionRow(opt, "Volumen general", -150f, 0f, 1f, false, out var volumeValue);
        var music = OptionRow(opt, "Música", -250f, 0f, 1f, false, out var musicValue);
        var back = AeroUI.Button(opt, "Volver", "Volver", skin.buttonBlue, new Vector2(320f, 90f), Color.white, 40);
        ((RectTransform)back.transform).At(new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(320f, 90f));

        // Pie de página
        var version = AeroUI.Label(root, "Version", "Demo · v0.3", 24, Color.white, AeroWeight.Light, TextAnchor.LowerRight);
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
            ("skin", skin), ("mainPanel", mainPanel), ("optionsPanel", optPanel), ("newGamePanel", ngPanel), ("fade", fadeGroup),
            ("playButton", play), ("optionsButton", options), ("quitButton", quit), ("backButton", back),
            ("playerCountButtons", countButtons), ("playerCountPreview", preview), ("startButton", start), ("newGameBackButton", ngBack),
            ("goalSlider", goal), ("goalValue", goalValue),
            ("volumeSlider", volume), ("musicSlider", music),
            ("volumeValue", volumeValue), ("musicValue", musicValue));
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

    private static Text Title(RectTransform parent, string text, int size, float y)
    {
        var t = AeroUI.Label(parent, "Titulo", text, size, Navy, AeroWeight.Bold);
        t.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(parent.sizeDelta.x - 40f, size * 1.3f));
        AeroUI.Gradient(t, new Color(0.15f, 0.55f, 0.9f), Navy);
        AeroUI.Effect<Shadow>(t, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -2f));
        return t;
    }

    // Coloca un botón centrado horizontalmente a cierta altura desde el borde de arriba del padre
    private static Button Place(Button b, float y)
    {
        var rt = (RectTransform)b.transform;
        rt.At(new Vector2(0.5f, 1f), new Vector2(0f, y), rt.sizeDelta);
        return b;
    }

    // =====================================================================
    // Arena circular
    // =====================================================================

    private static Arena CreateArena()
    {
        var arena = new GameObject("Arena").AddComponent<Arena>();
        CreateArenaVisuals(arena.transform);
        return arena;
    }

    // Suelo en disco (malla + MeshCollider real), pared curva cerrada, barrera invisible alta y baranda brillante
    private static void CreateArenaVisuals(Transform parent)
    {
        if (parent == null) parent = new GameObject("Arena").transform;

        var floor = MeshObject("Suelo (cuadrícula)", parent, floorMesh, groundMat, true);
        floor.tag = "Ground";
        MeshObject("Pared", parent, wallMesh, marbleMat, true);
        var barrier = new GameObject("Barrera invisible", typeof(MeshCollider));
        barrier.transform.SetParent(parent, false);
        barrier.GetComponent<MeshCollider>().sharedMesh = wallColliderMesh;   // la pelota no se escapa por arriba de la pared
        var rail = MeshObject("Baranda", parent, railMesh, aquaMat, false);
        rail.transform.localPosition = Vector3.up * 1.1f;
        var hub = MeshObject("Centro", parent, hubMesh, marbleMat, false);
        hub.transform.localPosition = Vector3.up * 0.02f;
    }

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool collider)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    private static void CreateMeshes()
    {
        float r = balance.arenaRadius;
        floorMesh = SaveMesh(ProceduralMesh.Disc(r, 128, 4f, "Suelo"), "Suelo");
        wallMesh = SaveMesh(ProceduralMesh.WallBand(r, r + 0.7f, 1.1f, 128, "Pared"), "Pared");
        wallColliderMesh = SaveMesh(ProceduralMesh.WallBand(r, r + 0.7f, 14f, 128, "Barrera"), "Barrera");
        railMesh = SaveMesh(ProceduralMesh.Torus(r + 0.35f, 0.42f, 128, 16, "Baranda"), "Baranda");
        hubMesh = SaveMesh(ProceduralMesh.Torus(2.2f, 0.1f, 64, 8, "Centro"), "Centro");
        islandMesh = SaveMesh(ProceduralMesh.Disc(IslandRadius, 128, 4f, "Isla"), "Isla");
    }

    private static void LoadMeshes()
    {
        floorMesh = Load<Mesh>(MeshFolder + "/Suelo.asset");
        wallMesh = Load<Mesh>(MeshFolder + "/Pared.asset");
        wallColliderMesh = Load<Mesh>(MeshFolder + "/Barrera.asset");
        railMesh = Load<Mesh>(MeshFolder + "/Baranda.asset");
        hubMesh = Load<Mesh>(MeshFolder + "/Centro.asset");
        islandMesh = Load<Mesh>(MeshFolder + "/Isla.asset");
    }

    // Guarda la malla como asset (reutiliza el archivo si ya existe, así no cambia su GUID)
    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{MeshFolder}/{name}.asset";
        var existing = Load<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
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
        Prim(PrimitiveType.Cylinder, "Isla (arena)", env, new Vector3(0f, -1.7f, 0f), new Vector3(IslandRadius * 2f + 2f, 1.55f, IslandRadius * 2f + 2f), sandMat, false);
        var outer = MeshObject("Cesped exterior", env, islandMesh, outerGrassMat, true);
        outer.transform.localPosition = Vector3.up * -0.04f;
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

    // Árboles redondos en el anillo de césped entre la pared y el agua
    private static void CreateTrees(Transform parent)
    {
        var root = new GameObject("Arboles").transform;
        root.SetParent(parent);
        var rng = new System.Random(11);
        var spots = new List<Vector3>();
        float inner = balance.arenaRadius + 2.6f, outerR = IslandRadius - 2f;
        for (int tries = 0; spots.Count < 30 && tries < 900; tries++)
        {
            float a = (float)rng.NextDouble() * 360f;
            float d = Mathf.Lerp(inner, outerR, (float)rng.NextDouble());
            var p = Arena.Direction(a) * d + Vector3.up * -0.04f;
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
        var go = new GameObject("Burbujas del agua");
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
        shape.radius = 46f;
        shape.radiusThickness = 0.2f;

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
        col.color = new ParticleSystem.MinMaxGradient(FadeGradient());

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.2f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = bubbleMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private static Gradient FadeGradient()
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        return g;
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
    // Prefabs: pelota, burbuja (y su efecto al reventar), castillo y catapulta
    // =====================================================================

    private static void CreatePrefabs()
    {
        CreateRockPrefab();
        CreatePopEffectPrefab();
        CreateBubblePrefab();
        CreateCastlePrefab();
        CreateCatapultPrefab();
    }

    private static void CreateRockPrefab()
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
        SavePrefab(go, RockPrefabPath);
    }

    // Estallido: burbujitas que salen disparadas y se desvanecen
    private static void CreatePopEffectPrefab()
    {
        var go = new GameObject("BurbujaRevienta");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startColor = Color.white;
        main.gravityModifier = -0.05f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.95f, 1f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = bubbleMat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        SavePrefab(go, PopFxPrefabPath);
    }

    // Burbuja objetivo: trigger esférico + sprite de burbuja que siempre mira a la cámara
    private static void CreateBubblePrefab()
    {
        var go = new GameObject("Burbuja");
        go.AddComponent<SphereCollider>().isTrigger = true;
        var bubble = go.AddComponent<Bubble>();
        var visual = new GameObject("Visual", typeof(SpriteRenderer));
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * (1f / 2.44f);   // el círculo del sprite mide 2.44 unidades
        var sr = visual.GetComponent<SpriteRenderer>();
        sr.sprite = skin.bubble;
        sr.shadowCastingMode = ShadowCastingMode.Off;
        Set(bubble, ("sprite", sr), ("popEffect", Load<GameObject>(PopFxPrefabPath)));
        SavePrefab(go, BubblePrefabPath);
    }

    private static void CreateCastlePrefab()
    {
        var root = new GameObject("Castillo").transform;
        var colored = new List<Renderer>();
        Material mat = playerMats[0];

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
        colored.Add(Prim(PrimitiveType.Cube, "Dintel", root, new Vector3(0f, wallH - 0.35f, h + t / 2f), new Vector3(gate, 0.7f, t), mat).GetComponent<Renderer>());
        colored.Add(Prim(PrimitiveType.Sphere, "Gema", root, new Vector3(0f, wallH + 0.35f, h + t / 2f), Vector3.one * 0.7f, mat, false).GetComponent<Renderer>());

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
            colored.Add(Prim(PrimitiveType.Sphere, "Cúpula", root, corner + Vector3.up * 3.5f, Vector3.one * 1.95f, mat, false).GetComponent<Renderer>());
            Prim(PrimitiveType.Sphere, "Remate", root, corner + Vector3.up * 4.55f, Vector3.one * 0.35f, marbleMat, false);
            Prim(PrimitiveType.Cylinder, "Mástil", root, corner + Vector3.up * 5.05f, new Vector3(0.07f, 0.5f, 0.07f), marbleMat, false);
            colored.Add(Prim(PrimitiveType.Cube, "Bandera", root, corner + new Vector3(0.4f, 5.3f, 0f), new Vector3(0.75f, 0.45f, 0.05f), mat, false).GetComponent<Renderer>());
        }

        // Pedestal en el patio: de ahí "sale" la burbuja de castillo que flota encima
        Prim(PrimitiveType.Cylinder, "Pedestal", root, new Vector3(0f, 2.3f, 0f), new Vector3(0.5f, 2.3f, 0.5f), marbleMat, false);
        colored.Add(Prim(PrimitiveType.Sphere, "Orbe pedestal", root, new Vector3(0f, 4.75f, 0f), Vector3.one * 0.7f, mat, false).GetComponent<Renderer>());

        var paint = root.gameObject.AddComponent<PlayerPaint>();
        Set(paint, ("colorParts", colored.ToArray()));
        SavePrefab(root.gameObject, CastlePrefabPath);
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

    private static void CreateCatapultPrefab()
    {
        var root = new GameObject("Catapulta").transform;
        var baseR = Prim(PrimitiveType.Cube, "Base", root, new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 0.35f, 2.4f), playerMats[0]).GetComponent<Renderer>();
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

        var launch = new GameObject("LaunchPoint").transform;
        launch.SetParent(root, false);
        launch.localPosition = new Vector3(0f, 3.8f, 1f);

        var marker = Prim(PrimitiveType.Sphere, "Indicador turno", root, new Vector3(0f, 5.2f, 0f), Vector3.one * 0.45f, markerMats[0], false);
        marker.AddComponent<MarkerSpin>();

        var line = new GameObject("Trayectoria").AddComponent<LineRenderer>();
        line.transform.SetParent(root, false);
        line.sharedMaterial = lineMat;
        line.useWorldSpace = true;
        line.widthCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 0.05f);
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.startColor = new Color(1f, 1f, 1f, 0.95f);
        line.endColor = new Color(1f, 1f, 1f, 0f);

        var ctrl = root.gameObject.AddComponent<CatapultController>();
        Set(ctrl, ("launchPoint", launch), ("rockPrefab", Load<GameObject>(RockPrefabPath).GetComponent<Rock>()), ("armPivot", pivot),
                  ("activeMarker", marker), ("loadedRock", loaded), ("trajectory", line));
        var paint = root.gameObject.AddComponent<PlayerPaint>();
        Set(paint, ("colorParts", new[] { baseR }), ("glowParts", new[] { marker.GetComponent<Renderer>() }));
        SavePrefab(root.gameObject, CatapultPrefabPath);
    }

    private static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    // =====================================================================
    // Materiales y postproceso
    // =====================================================================

    private static void SetupMaterials()
    {
        skin = Load<AeroSkin>(AeroArtGenerator.SkinPath);
        volumeProfile = Load<VolumeProfile>(VolumePath);

        groundMat = Lit("Cesped", Color.white, 0.32f);
        groundMat.mainTexture = Load<Texture2D>(AeroArtGenerator.GrassGridPath);
        groundMat.mainTextureScale = Vector2.one;   // la malla del disco ya trae la escala de la cuadrícula
        outerGrassMat = Lit("CespedExterior", Color.white, 0.28f);
        outerGrassMat.mainTexture = Load<Texture2D>(AeroArtGenerator.GrassPath);
        outerGrassMat.mainTextureScale = Vector2.one;

        sandMat = Lit("Arena", new Color(0.97f, 0.9f, 0.7f), 0.35f);
        waterMat = Lit("Agua", new Color(0.13f, 0.66f, 0.88f), 0.96f, 0.1f);
        marbleMat = Lit("Marmol", new Color(0.95f, 0.97f, 0.99f), 0.78f);
        railMat = Lit("Baranda", new Color(0.86f, 0.96f, 1f), 0.9f);
        aquaMat = Lit("Aqua", new Color(0.35f, 0.8f, 1f), 0.95f);
        woodMat = Lit("Madera", new Color(0.86f, 0.58f, 0.33f), 0.6f);
        darkWoodMat = Lit("MaderaOscura", new Color(0.5f, 0.3f, 0.17f), 0.55f);
        rockMat = Lit("Roca", new Color(0.92f, 0.95f, 0.97f), 0.88f);
        trunkMat = Lit("Tronco", new Color(0.62f, 0.4f, 0.22f), 0.45f);
        dividerMat = Lit("Divisor", new Color(0.96f, 0.99f, 1f), 0.9f, 0f, new Color(0.25f, 0.3f, 0.35f));
        leafMats = new[]
        {
            Lit("Hojas1", new Color(0.36f, 0.8f, 0.27f), 0.6f),
            Lit("Hojas2", new Color(0.52f, 0.88f, 0.33f), 0.6f),
            Lit("Hojas3", new Color(0.27f, 0.7f, 0.24f), 0.6f),
        };

        int n = skin.playerColors.Length;
        playerMats = new Material[n];
        markerMats = new Material[n];
        for (int i = 0; i < n; i++)
        {
            Color c = skin.playerColors[i];
            playerMats[i] = Lit($"Jugador{i + 1}", c, 0.88f);
            markerMats[i] = Lit($"Indicador{i + 1}", Color.Lerp(c, Color.white, 0.3f), 0.95f, 0f, c * 1.6f);
        }

        sectionMat = Transparent(Unlit("Seccion", new Color(1f, 1f, 1f, 0.2f)));
        lineMat = SaveMat("Linea", new Material(Shader.Find("Sprites/Default")));
        bubbleMat = SaveMat("Burbuja", new Material(Shader.Find("Sprites/Default")));
        bubbleMat.mainTexture = skin.bubble.texture;
        skyMat = SaveMat("Cielo", new Material(Shader.Find("Skybox/Panoramic")));
        skyMat.SetTexture("_MainTex", Load<Texture2D>(AeroArtGenerator.SkyPath));
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

    private static Material Unlit(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var m = SaveMat(name, new Material(shader));
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Material URP transparente (vidrio, fantasmas de colocación, tinte de las secciones)
    private static Material Transparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
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

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

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
