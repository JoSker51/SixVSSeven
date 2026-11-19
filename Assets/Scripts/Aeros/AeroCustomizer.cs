using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// "Personaliza tu Aero": después de Nueva partida, cada jugador arma su Aero (ícono 2D estilo Messenger, como el canal
// Aeros de la presentación). Pestañas Cuerpo, Sombrero, Bufanda, Gafas, Extras y Dibujar (zoom a la cabeza para pintarle
// la cara a mano alzada); nombre; Sorpréndeme. Se arma solo con AeroUI. MainMenu lo abre con Open(); al terminar guarda
// los Aeros (AeroRoster) y avisa con onDone.
public class AeroCustomizer : MonoBehaviour
{
    [SerializeField] private AeroSkin skin;
    [SerializeField] private UIFadePanel panel;
    [SerializeField] private CanvasGroup hideWhileOpen;   // el título del menú: se esconde para dar espacio

    private enum Tab { Body, Hat, Scarf, Glasses, Extras, Draw }

    static readonly string[] InkColors = { "#0a3d66", "#ffffff", "#e8333a", "#ff8a3d", "#ffd23f", "#5fd23c", "#19a6d8", "#9c6bff", "#f047a0", "#2b2f3a" };
    const float Zoom = 1.12f;   // cuánto más grande que la cabeza se ve el zoom de Dibujar

    private readonly List<AeroLook> looks = new List<AeroLook>();
    private readonly List<(Button button, Image bg, Text title, Text name, AeroIconView icon)> chips = new List<(Button, Image, Text, Text, AeroIconView)>();
    private readonly List<(Image img, Text label, Tab tab)> tabButtons = new List<(Image, Text, Tab)>();
    private readonly List<GameObject> tabContent = new List<GameObject>();
    private readonly List<Action> refreshers = new List<Action>();   // repintan qué opción está elegida

    private RectTransform content;
    private Image stageBg;
    private AeroIconView big;
    private GameObject drawStage;
    private RawImage zoomImage;
    private Texture2D zoomTex;
    private AeroFacePainter painter;
    private InputField nameField;
    private Text header, nextText;
    private Image headerPill;
    private int count, current;
    private Tab tab = Tab.Body;
    private Action onDone, onBack;
    private bool built;

    public bool Visible => panel && panel.Visible;

    private Color Navy => skin.navy;
    private AeroLook Cur => looks[current];

    public void Open(int players, Action done, Action back)
    {
        if (!built) Build();
        count = Mathf.Clamp(players, MatchConfig.MinPlayers, MatchConfig.MaxPlayers);
        onDone = done; onBack = back;
        looks.Clear();
        for (int i = 0; i < MatchConfig.MaxPlayers; i++) looks.Add(AeroRoster.Get(i).Clone());
        for (int i = 0; i < chips.Count; i++)
        {
            chips[i].button.gameObject.SetActive(i < count);
            chips[i].icon.SetLook(looks[i]);
        }
        SetTab(Tab.Body);
        Select(0);
        if (hideWhileOpen) hideWhileOpen.alpha = 0f;
        panel.Show();
    }

    private void Close()
    {
        panel.Hide();
        if (hideWhileOpen) hideWhileOpen.alpha = 1f;
    }

    private void Update()
    {
        if (!Visible) return;
#if ENABLE_INPUT_SYSTEM
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        bool esc = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (esc && !nameField.isFocused) GoBack();
    }

    private void Select(int player)
    {
        current = Mathf.Clamp(player, 0, count - 1);
        nameField.SetTextWithoutNotify(Cur.name ?? "");
        Color pc = skin.PlayerColor(current);
        string colorName = skin.playerColorNames != null && current < skin.playerColorNames.Length ? skin.playerColorNames[current] : "";
        header.text = $"Jugador {current + 1}  ·  {colorName}";
        header.color = skin.TextOn(current);
        headerPill.sprite = skin.PlayerButton(current);
        stageBg.color = Color.Lerp(pc, Color.white, 0.72f);
        nextText.text = current < count - 1 ? $"Jugador {current + 2}  ▸" : "¡A jugar!  ▸";
        for (int i = 0; i < chips.Count; i++)
        {
            bool on = i == current;
            chips[i].bg.sprite = on ? skin.PlayerButton(i) : skin.glassPanel;
            chips[i].title.color = on ? skin.TextOn(i) : Navy;
            chips[i].name.color = on ? skin.TextOn(i) : new Color(Navy.r, Navy.g, Navy.b, 0.75f);
        }
        if (tab == Tab.Draw) EnterDraw();
        Changed(false);
    }

    // Algo del Aero cambió: se ve en el ícono grande y en su ficha
    private void Changed(bool hop = true)
    {
        big.SetLook(Cur);
        chips[current].icon.SetLook(Cur);
        chips[current].name.text = string.IsNullOrWhiteSpace(Cur.name) ? "Sin nombre" : Cur.name;
        foreach (var r in refreshers) r();
        if (tab == Tab.Draw) RenderZoom();
        if (hop) big.Hop();
    }

    private void Next()
    {
        AeroAudio.Play(Sfx.Click);
        if (current < count - 1) { Select(current + 1); big.Hop(); return; }
        for (int i = 0; i < count; i++) AeroRoster.Set(i, looks[i]);
        AeroRoster.Save();
        Close();
        onDone?.Invoke();
    }

    private void GoBack()
    {
        AeroAudio.Play(Sfx.Click);
        if (current > 0) { Select(current - 1); return; }
        for (int i = 0; i < count; i++) AeroRoster.Set(i, looks[i]);   // lo que ya armaron no se pierde
        Close();
        onBack?.Invoke();
    }

    private void Surprise()
    {
        AeroAudio.Play(Sfx.Buy);
        var keep = Cur;
        looks[current] = AeroLook.Random();
        looks[current].name = keep.name;
        looks[current].draw = keep.draw;   // la cara dibujada no se pierde
        Changed();
    }

    private void SetTab(Tab t)
    {
        tab = t;
        for (int i = 0; i < tabContent.Count; i++) tabContent[i].SetActive(i == (int)t);
        foreach (var (img, label, tt) in tabButtons)
        {
            bool on = tt == t;
            img.sprite = on ? skin.buttonBlue : skin.buttonSilver;
            label.color = on ? Color.white : Navy;
        }
        bool draw = t == Tab.Draw;
        big.gameObject.SetActive(!draw);
        drawStage.SetActive(draw);
        if (draw) EnterDraw();
    }

    // Dibujar: zoom a la cabeza sin sombrero ni gafas, y el lienzo justo encima
    private void EnterDraw()
    {
        painter.Load(Cur.draw);
        RenderZoom();
    }

    private void RenderZoom()
    {
        Rect head = AeroIconRenderer.HeadBox(Cur);
        Rect view = new Rect(head.center.x - head.width * Zoom / 2f, head.center.y - head.height * Zoom / 2f, head.width * Zoom, head.height * Zoom);
        zoomTex = AeroIconRenderer.Render(Cur, 512, zoomTex, view, bare: true, withDrawing: false);
        zoomImage.texture = zoomTex;
    }

    private void OnStroke()
    {
        Cur.draw = painter.Save();
        chips[current].icon.SetLook(Cur);
        big.SetLook(Cur);
    }

    // ---------------- Construcción de la interfaz ----------------

    private void Build()
    {
        built = true;
        var root = (RectTransform)transform;
        var glass = AeroUI.Img(root, "Vidrio", skin.glassPanel, Color.white);
        glass.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1820f, 990f));
        var frame = glass.rectTransform;

        var title = AeroUI.Label(frame, "Titulo", "Personaliza tu Aero", 60, Navy, AeroWeight.Bold, TextAnchor.MiddleLeft);
        title.rectTransform.At(new Vector2(0f, 1f), new Vector2(44f, -20f), new Vector2(800f, 80f));
        AeroUI.Gradient(title, new Color(0.15f, 0.55f, 0.9f), Navy);
        AeroUI.Effect<Shadow>(title, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -2f));
        headerPill = AeroUI.Img(frame, "Jugador", skin.PlayerButton(0), Color.white, 160f / 64f);
        headerPill.rectTransform.At(new Vector2(1f, 1f), new Vector2(-44f, -30f), new Vector2(420f, 64f));
        header = AeroUI.Label(headerPill.transform, "Texto", "Jugador 1", 32, Color.white, AeroWeight.Bold);
        header.rectTransform.Stretch();

        // Fichas de los jugadores (izquierda)
        for (int i = 0; i < MatchConfig.MaxPlayers; i++)
        {
            int p = i;
            var bg = AeroUI.Img(frame, $"Ficha J{i + 1}", skin.glassPanel, Color.white, 2f);
            bg.raycastTarget = true;
            bg.rectTransform.At(new Vector2(0f, 1f), new Vector2(40f, -124f - i * 150f), new Vector2(330f, 136f));
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { AeroAudio.Play(Sfx.Click); Select(p); });
            bg.gameObject.AddComponent<AeroButtonFX>();
            var holder = AeroUI.Node("Icono", bg.transform).At(new Vector2(0f, 0.5f), new Vector2(10f, 4f), new Vector2(120f, 120f));
            var icon = AeroIconView.Create(holder, AeroRoster.Get(i), 160);
            var t = AeroUI.Label(bg.transform, "Titulo", $"Jugador {i + 1}", 30, Navy, AeroWeight.Bold, TextAnchor.MiddleLeft);
            t.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(136f, 20f), new Vector2(190f, 40f));
            var n = AeroUI.Label(bg.transform, "Nombre", "", 24, Navy, AeroWeight.Regular, TextAnchor.MiddleLeft);
            n.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(136f, -22f), new Vector2(190f, 34f));
            chips.Add((b, bg, t, n, icon));
        }

        // Ícono grande (centro), zoom para dibujar y nombre
        var stage = AeroUI.Img(frame, "Escenario", skin.glassPanel, Color.white, 1.4f);
        stage.rectTransform.At(new Vector2(0f, 1f), new Vector2(400f, -124f), new Vector2(540f, 640f));
        stageBg = AeroUI.Img(stage.transform, "Fondo", skin.glassPanel, new Color(0.85f, 0.95f, 1f), 1.6f);
        stageBg.rectTransform.Stretch(14f);
        AeroUI.Img(stage.transform, "Resplandor", skin.glow, new Color(1f, 1f, 1f, 0.8f)).rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(520f, 520f));
        var bigHolder = AeroUI.Node("Aero", stage.transform).At(new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 500f));
        big = AeroIconView.Create(bigHolder, AeroRoster.Get(0), 512);

        var drawRoot = AeroUI.Node("Dibujar", stage.transform).At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 500f));
        drawStage = drawRoot.gameObject;
        zoomImage = AeroUI.Node("Zoom", drawRoot).Stretch().gameObject.AddComponent<RawImage>();
        zoomImage.raycastTarget = false;
        var canvasArea = AeroUI.Node("Cara", drawRoot).At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f / Zoom, 500f / Zoom));
        painter = AeroFacePainter.Create(canvasArea);
        painter.StrokeEnded += OnStroke;
        drawStage.SetActive(false);

        AeroUI.Label(frame, "Etiqueta nombre", "Nombre", 28, Navy, AeroWeight.Bold, TextAnchor.MiddleLeft)
            .rectTransform.At(new Vector2(0f, 1f), new Vector2(404f, -778f), new Vector2(160f, 44f));
        nameField = NameField(frame);

        // Pestañas y opciones (derecha)
        var side = AeroUI.Img(frame, "Opciones", skin.glassPanel, new Color(1f, 1f, 1f, 0.8f), 1.4f);
        side.rectTransform.At(new Vector2(0f, 1f), new Vector2(980f, -124f), new Vector2(800f, 720f));
        var tabsDef = new[] { (Tab.Body, "Cuerpo"), (Tab.Hat, "Sombrero"), (Tab.Scarf, "Bufanda"), (Tab.Glasses, "Gafas"), (Tab.Extras, "Extras"), (Tab.Draw, "Dibujar") };
        for (int i = 0; i < tabsDef.Length; i++)
        {
            var (tt, label) = tabsDef[i];
            var b = AeroUI.Button(side.transform, label, label, skin.buttonSilver, new Vector2(124f, 60f), Navy, 23);
            ((RectTransform)b.transform).At(new Vector2(0f, 1f), new Vector2(18f + i * 128f, -20f), new Vector2(124f, 60f));
            b.onClick.AddListener(() => { AeroAudio.Play(Sfx.Click); SetTab(tt); });
            tabButtons.Add(((Image)b.targetGraphic, b.GetComponentInChildren<Text>(), tt));
        }
        content = AeroUI.Node("Contenido", side.transform);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(24f, 20f); content.offsetMax = new Vector2(-24f, -100f);

        tabContent.Add(BuildBodyTab());
        tabContent.Add(BuildChoiceTab("Sombreros", AeroLook.Hats, l => l.hat, (l, v) => l.hat = v, AeroLook.ClothColors, l => l.hatColor, (l, v) => l.hatColor = v));
        tabContent.Add(BuildChoiceTab("Bufandas", AeroLook.Scarves, l => l.scarf, (l, v) => l.scarf = v, AeroLook.ClothColors, l => l.scarfColor, (l, v) => l.scarfColor = v));
        tabContent.Add(BuildChoiceTab("Gafas", AeroLook.Glasses, l => l.glasses, (l, v) => l.glasses = v, null, null, null));
        tabContent.Add(BuildExtrasTab());
        tabContent.Add(BuildDrawTab());

        // Botones de abajo
        var back = AeroUI.Button(frame, "Volver", "◂  Volver", skin.buttonSilver, new Vector2(300f, 90f), Navy, 38);
        ((RectTransform)back.transform).At(new Vector2(0f, 0f), new Vector2(40f, 34f), new Vector2(300f, 90f));
        back.onClick.AddListener(GoBack);
        var random = AeroUI.Button(frame, "Sorprendeme", "Sorpréndeme", skin.buttonBlue, new Vector2(360f, 90f), Color.white, 38);
        ((RectTransform)random.transform).At(new Vector2(0.5f, 0f), new Vector2(160f, 34f), new Vector2(360f, 90f));
        random.onClick.AddListener(Surprise);
        var next = AeroUI.Button(frame, "Siguiente", "Jugador 2  ▸", skin.buttonGreen, new Vector2(400f, 90f), Color.white, 38);
        ((RectTransform)next.transform).At(new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(400f, 90f));
        next.onClick.AddListener(Next);
        nextText = next.GetComponentInChildren<Text>();
    }

    private InputField NameField(RectTransform frame)
    {
        var bg = AeroUI.Img(frame, "Campo nombre", skin.track, Color.white, skin.track.rect.height / 70f);
        bg.raycastTarget = true;
        bg.rectTransform.At(new Vector2(0f, 1f), new Vector2(560f, -766f), new Vector2(380f, 70f));
        var text = AeroUI.Label(bg.transform, "Texto", "", 32, Navy, AeroWeight.Bold, TextAnchor.MiddleLeft);
        text.rectTransform.Stretch(14f);
        text.supportRichText = false;
        var ph = AeroUI.Label(bg.transform, "Placeholder", "Tu Aero", 32, new Color(Navy.r, Navy.g, Navy.b, 0.4f), AeroWeight.Light, TextAnchor.MiddleLeft);
        ph.rectTransform.Stretch(14f);
        var field = bg.gameObject.AddComponent<InputField>();
        field.textComponent = text;
        field.placeholder = ph;
        field.characterLimit = 14;
        field.targetGraphic = bg;
        field.onValueChanged.AddListener(v => { Cur.name = v; chips[current].name.text = string.IsNullOrWhiteSpace(v) ? "Sin nombre" : v; });
        return field;
    }

    private RectTransform Page(string name)
    {
        var page = AeroUI.Node(name, content);
        page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one;
        page.offsetMin = page.offsetMax = Vector2.zero;
        return page;
    }

    private void Heading(RectTransform page, string text, float y) =>
        AeroUI.Label(page, text, text, 28, Navy, AeroWeight.Bold, TextAnchor.MiddleLeft).rectTransform
            .At(new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(700f, 40f));

    private Text Note(RectTransform page, string text, float y)
    {
        var t = AeroUI.Label(page, "Nota", text, 22, new Color(Navy.r, Navy.g, Navy.b, 0.75f), AeroWeight.Light, TextAnchor.UpperLeft);
        t.rectTransform.At(new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(740f, 80f));
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }

    private GameObject BuildBodyTab()
    {
        var page = Page("Cuerpo");
        Heading(page, "Color de la cabeza", -6f);
        Swatches(page, -52f, AeroLook.BodyColors, l => l.head, (l, v) => l.head = v);
        Heading(page, "Color del cuerpo", -142f);
        Swatches(page, -188f, AeroLook.BodyColors, l => l.body, (l, v) => l.body = v);
        Heading(page, "Forma", -278f);
        ShapeSlider(page, "Altura", -330f, AeroLook.MinH, AeroLook.MaxH, l => l.h, (l, v) => l.h = v);
        ShapeSlider(page, "Ancho", -400f, AeroLook.MinW, AeroLook.MaxW, l => l.w, (l, v) => l.w = v);
        Note(page, "Los Aeros no tienen cara: su personalidad se la pones tú con los colores, la ropa y lo que le dibujes en la pestaña Dibujar.", -470f);
        return page.gameObject;
    }

    private GameObject BuildChoiceTab(string name, (string id, string label)[] options, Func<AeroLook, string> get, Action<AeroLook, string> set,
                                      string[] colors, Func<AeroLook, string> getColor, Action<AeroLook, string> setColor)
    {
        var page = Page(name);
        for (int i = 0; i < options.Length; i++)
        {
            var (id, label) = options[i];
            var b = AeroUI.Button(page, label, label, skin.buttonSilver, new Vector2(236f, 72f), Navy, 28);
            ((RectTransform)b.transform).At(new Vector2(0f, 1f), new Vector2((i % 3) * 248f, -(i / 3) * 86f), new Vector2(236f, 72f));
            b.onClick.AddListener(() => { AeroAudio.Play(Sfx.Place); set(Cur, id); Changed(); });
            var img = (Image)b.targetGraphic; var txt = b.GetComponentInChildren<Text>();
            refreshers.Add(() => { bool on = get(Cur) == id; img.sprite = on ? skin.buttonGreen : skin.buttonSilver; txt.color = on ? Color.white : Navy; });
        }
        if (colors != null)
        {
            float y = -((options.Length + 2) / 3) * 86f - 20f;
            Heading(page, "Color", y);
            Swatches(page, y - 46f, colors, getColor, setColor);
        }
        return page.gameObject;
    }

    private GameObject BuildExtrasTab()
    {
        var page = Page("Extras");
        Heading(page, "Puedes ponerle varios", -6f);
        var opts = AeroLook.Accessories;
        for (int i = 0; i < opts.Length; i++)
        {
            var (id, label) = opts[i];
            var b = AeroUI.Button(page, label, label, skin.buttonSilver, new Vector2(236f, 72f), Navy, 28);
            ((RectTransform)b.transform).At(new Vector2(0f, 1f), new Vector2((i % 3) * 248f, -56f - (i / 3) * 86f), new Vector2(236f, 72f));
            b.onClick.AddListener(() =>
            {
                AeroAudio.Play(Sfx.Place);
                if (Cur.acc.Contains(id)) Cur.acc.Remove(id); else Cur.acc.Add(id);
                Changed();
            });
            var img = (Image)b.targetGraphic; var txt = b.GetComponentInChildren<Text>();
            refreshers.Add(() => { bool on = Cur.Has(id); img.sprite = on ? skin.buttonGreen : skin.buttonSilver; txt.color = on ? Color.white : Navy; });
        }
        return page.gameObject;
    }

    private GameObject BuildDrawTab()
    {
        var page = Page("Dibujar");
        Note(page, "Dibuja con el mouse dentro del círculo: una cara, unas mejillas, un antifaz, lo que quieras. Los sombreros y gafas vuelven al salir de aquí.", -4f);
        Heading(page, "Lápiz", -96f);
        var inkRings = new List<(Image ring, string hex)>();
        for (int i = 0; i < InkColors.Length; i++)
        {
            string hex = InkColors[i];
            var orb = AeroUI.Orb(page, hex, skin, AeroLook.ParseColor(hex, Color.white), 58f);
            orb.rectTransform.At(new Vector2(0f, 1f), new Vector2(4f + i * 72f, -142f), new Vector2(58f, 58f));
            orb.raycastTarget = true;
            var b = orb.gameObject.AddComponent<Button>();
            b.targetGraphic = orb;
            b.onClick.AddListener(() => { AeroAudio.Play(Sfx.Click); painter.color = AeroLook.ParseColor(hex, Color.black); painter.erase = false; foreach (var r in refreshers) r(); });
            orb.gameObject.AddComponent<AeroButtonFX>();
            var ring = AeroUI.Img(orb.transform, "Elegido", skin.bubble, Navy);
            ring.rectTransform.Stretch(-8f);
            refreshers.Add(() => ring.enabled = !painter.erase && (Color)painter.color == AeroLook.ParseColor(hex, Color.black));
        }
        Heading(page, "Grosor", -232f);
        var sizes = new[] { (4, "Fino"), (7, "Normal"), (12, "Grueso"), (20, "Brocha") };
        for (int i = 0; i < sizes.Length; i++)
        {
            var (size, label) = sizes[i];
            var b = AeroUI.Button(page, label, label, skin.buttonSilver, new Vector2(176f, 64f), Navy, 26);
            ((RectTransform)b.transform).At(new Vector2(0f, 1f), new Vector2(i * 186f, -278f), new Vector2(176f, 64f));
            b.onClick.AddListener(() => { AeroAudio.Play(Sfx.Click); painter.brush = size; painter.erase = false; foreach (var r in refreshers) r(); });
            var img = (Image)b.targetGraphic; var txt = b.GetComponentInChildren<Text>();
            refreshers.Add(() => { bool on = !painter.erase && painter.brush == size; img.sprite = on ? skin.buttonBlue : skin.buttonSilver; txt.color = on ? Color.white : Navy; });
        }
        var eraser = AeroUI.Button(page, "Borrador", "Borrador", skin.buttonSilver, new Vector2(236f, 72f), Navy, 28);
        ((RectTransform)eraser.transform).At(new Vector2(0f, 1f), new Vector2(0f, -376f), new Vector2(236f, 72f));
        eraser.onClick.AddListener(() => { AeroAudio.Play(Sfx.Click); painter.erase = !painter.erase; foreach (var r in refreshers) r(); });
        var eImg = (Image)eraser.targetGraphic; var eTxt = eraser.GetComponentInChildren<Text>();
        refreshers.Add(() => { eImg.sprite = painter.erase ? skin.buttonBlue : skin.buttonSilver; eTxt.color = painter.erase ? Color.white : Navy; });
        var undo = AeroUI.Button(page, "Deshacer", "Deshacer", skin.buttonSilver, new Vector2(236f, 72f), Navy, 28);
        ((RectTransform)undo.transform).At(new Vector2(0f, 1f), new Vector2(248f, -376f), new Vector2(236f, 72f));
        undo.onClick.AddListener(() => { AeroAudio.Play(Sfx.Click); painter.Undo(); });
        var clear = AeroUI.Button(page, "Borrar todo", "Borrar todo", skin.buttonSilver, new Vector2(236f, 72f), Navy, 28);
        ((RectTransform)clear.transform).At(new Vector2(0f, 1f), new Vector2(496f, -376f), new Vector2(236f, 72f));
        clear.onClick.AddListener(() => { AeroAudio.Play(Sfx.Error); painter.Clear(); });
        return page.gameObject;
    }

    private void Swatches(RectTransform page, float y, string[] colors, Func<AeroLook, string> get, Action<AeroLook, string> set)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            string hex = colors[i];
            var orb = AeroUI.Orb(page, hex, skin, AeroLook.ParseColor(hex, Color.white), 58f);
            orb.rectTransform.At(new Vector2(0f, 1f), new Vector2(4f + i * 72f, y), new Vector2(58f, 58f));
            orb.raycastTarget = true;
            var b = orb.gameObject.AddComponent<Button>();
            b.targetGraphic = orb;
            b.onClick.AddListener(() => { AeroAudio.Play(Sfx.Click); set(Cur, hex); Changed(); });
            orb.gameObject.AddComponent<AeroButtonFX>();
            var ring = AeroUI.Img(orb.transform, "Elegido", skin.bubble, Navy);
            ring.rectTransform.Stretch(-8f);
            refreshers.Add(() => ring.enabled = string.Equals(get(Cur), hex, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void ShapeSlider(RectTransform page, string label, float y, float min, float max, Func<AeroLook, float> get, Action<AeroLook, float> set)
    {
        AeroUI.Label(page, label, label, 28, Navy, AeroWeight.Regular, TextAnchor.MiddleLeft)
            .rectTransform.At(new Vector2(0f, 1f), new Vector2(4f, y), new Vector2(140f, 50f));
        var s = AeroUI.Slider(page, label + " (barra)", new Vector2(560f, 46f), skin, new Color(0.35f, 0.75f, 1f), Color.white);
        ((RectTransform)s.transform).At(new Vector2(0f, 1f), new Vector2(150f, y - 2f), new Vector2(560f, 46f));
        s.minValue = min; s.maxValue = max;
        s.onValueChanged.AddListener(v => { set(Cur, v); big.SetLook(Cur); chips[current].icon.SetLook(Cur); });
        refreshers.Add(() => s.SetValueWithoutNotify(get(Cur)));
    }

    private void Awake()
    {
        if (!panel) panel = GetComponent<UIFadePanel>();
    }

    private void OnDestroy()
    {
        if (zoomTex) Destroy(zoomTex);
    }
}
