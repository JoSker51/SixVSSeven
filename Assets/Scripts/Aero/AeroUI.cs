using UnityEngine;
using UnityEngine.UI;

// Atajos para armar interfaz uGUI con el estilo Aero. Los usa el constructor de escenas (editor)
// y el HUD en tiempo de juego para las etiquetas flotantes.
public static class AeroUI
{
    public static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };   // capa UI
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    // Ancla, pivote y posición en el mismo punto (0..1 dentro del padre)
    public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    // ppu > 1 achica los bordes del 9-slice (sirve para que las píldoras queden redondas a cualquier alto)
    public static Image Img(Transform parent, string name, Sprite sprite, Color color, float ppu = 1f)
    {
        var img = Node(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppu;
        }
        return img;
    }

    public static Text Label(Transform parent, string name, string text, int size, Color color,
                             AeroWeight weight = AeroWeight.Regular, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var rt = Node(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        rt.gameObject.AddComponent<AeroText>().weight = weight;
        AeroFont.Apply(t, weight);
        return t;
    }

    public static T Effect<T>(Graphic g, Color color, Vector2 distance) where T : Shadow
    {
        var e = g.gameObject.AddComponent<T>();
        e.effectColor = color;
        e.effectDistance = distance;
        return e;
    }

    public static UIGradient Gradient(Graphic g, Color top, Color bottom)
    {
        var grad = g.gameObject.AddComponent<UIGradient>();
        grad.top = top;
        grad.bottom = bottom;
        return grad;
    }

    // Botón tipo gel con texto, sombra, brillo al pasar el mouse y sonido
    public static Button Button(Transform parent, string name, string text, Sprite sprite, Vector2 size,
                                Color textColor, int fontSize = 42)
    {
        float ppu = sprite ? sprite.rect.height / size.y : 1f;
        var img = Img(parent, name, sprite, Color.white, ppu);
        img.raycastTarget = true;
        img.rectTransform.sizeDelta = size;
        Effect<Shadow>(img, new Color(0.02f, 0.15f, 0.3f, 0.3f), new Vector2(0f, -5f));

        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor = new Color(0.93f, 0.93f, 0.93f);
        cb.highlightedColor = Color.white;
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.8f, 0.85f, 0.9f);
        cb.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        var label = Label(img.transform, "Texto", text, fontSize, textColor, AeroWeight.Bold);
        label.rectTransform.Stretch();
        bool lightText = textColor.grayscale > 0.6f;
        Effect<Shadow>(label, lightText ? new Color(0.02f, 0.18f, 0.32f, 0.55f) : new Color(1f, 1f, 1f, 0.7f),
                       lightText ? new Vector2(0f, -2f) : new Vector2(0f, -1.5f));

        img.gameObject.AddComponent<AeroButtonFX>();
        return b;
    }

    // Slider con riel hundido, relleno brillante y un orbe como manija
    public static Slider Slider(Transform parent, string name, Vector2 size, AeroSkin skin, Color fillColor, Color handleColor)
    {
        var root = Node(name, parent);
        root.sizeDelta = size;
        float handle = size.y;
        float barH = size.y * 0.5f;

        var bg = Img(root, "Riel", skin.track, Color.white, skin.track.rect.height / barH);
        bg.raycastTarget = true;
        bg.rectTransform.anchorMin = new Vector2(0f, 0.25f);
        bg.rectTransform.anchorMax = new Vector2(1f, 0.75f);
        bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

        var fillArea = Node("Area relleno", root);
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.offsetMin = new Vector2(handle * 0.5f, 0f);
        fillArea.offsetMax = new Vector2(-handle * 0.5f, 0f);
        var fill = Img(fillArea, "Relleno", skin.fill, fillColor, skin.fill.rect.height / barH);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        fill.rectTransform.offsetMin = new Vector2(-handle * 0.5f, 0f);
        fill.rectTransform.offsetMax = Vector2.zero;

        var slideArea = Node("Area manija", root);
        slideArea.anchorMin = Vector2.zero;
        slideArea.anchorMax = Vector2.one;
        slideArea.offsetMin = new Vector2(handle * 0.5f, 0f);
        slideArea.offsetMax = new Vector2(-handle * 0.5f, 0f);
        var knob = Img(slideArea, "Manija", skin.orb, handleColor);
        knob.raycastTarget = true;
        knob.preserveAspect = true;
        knob.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        knob.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        knob.rectTransform.sizeDelta = new Vector2(handle, 4f);
        Effect<Shadow>(knob, new Color(0.02f, 0.15f, 0.3f, 0.35f), new Vector2(0f, -3f));
        var gloss = Img(knob.transform, "Brillo", skin.orbGloss, Color.white);
        gloss.preserveAspect = true;
        gloss.rectTransform.Stretch();

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = knob.rectTransform;
        slider.targetGraphic = knob;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        return slider;
    }

    // Orbe brillante (esfera teñida + reflejo blanco)
    public static Image Orb(Transform parent, string name, AeroSkin skin, Color color, float size)
    {
        var orb = Img(parent, name, skin.orb, color);
        orb.rectTransform.sizeDelta = new Vector2(size, size);
        Img(orb.transform, "Brillo", skin.orbGloss, Color.white).rectTransform.Stretch();
        return orb;
    }
}
