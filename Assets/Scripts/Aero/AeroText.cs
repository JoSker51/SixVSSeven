using UnityEngine;
using UnityEngine.UI;

public enum AeroWeight { Regular, Bold, Light }

// Tipografía del estilo Aero: Segoe UI (la de Windows Vista/7), tomada del sistema al jugar.
// En el editor se ve con la fuente integrada de Unity; al darle Play cambia a Segoe UI.
public static class AeroFont
{
    private static Font regular, light, builtin;

    public static Font Builtin => builtin ? builtin : (builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static Font Get(AeroWeight weight)
    {
        if (weight == AeroWeight.Light) return light ? light : (light = FromOS("Segoe UI Light", "Segoe UI", "Arial"));
        return regular ? regular : (regular = FromOS("Segoe UI", "Arial"));
    }

    public static void Apply(Text text, AeroWeight weight)
    {
        text.font = Application.isPlaying ? Get(weight) : Builtin;
        text.fontStyle = weight == AeroWeight.Bold ? FontStyle.Bold : FontStyle.Normal;
    }

    private static Font FromOS(params string[] names)
    {
        try
        {
            var f = Font.CreateDynamicFontFromOSFont(names, 32);
            if (f != null) return f;
        }
        catch { /* sin fuentes del sistema: usamos la integrada */ }
        return Builtin;
    }
}

[RequireComponent(typeof(Text))]
public class AeroText : MonoBehaviour
{
    public AeroWeight weight = AeroWeight.Regular;

    private void Awake() => AeroFont.Apply(GetComponent<Text>(), weight);
}
