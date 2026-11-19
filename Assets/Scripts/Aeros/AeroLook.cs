using System;
using System.Collections.Generic;
using UnityEngine;

// Cómo se ve el Aero de un jugador: un ícono 2D al estilo de los muñequitos de Messenger (cabeza y hombros brillantes),
// sin cara: se viste con sombrero, bufanda, gafas y accesorios, y se le puede dibujar la cara a mano alzada.
// Las opciones y paletas son las mismas que en la presentación (mii.js), para que los Aeros se reconozcan.
[Serializable]
public class AeroLook
{
    public string name = "";
    public string body = "#2ea8ff";   // color del cuerpo (hombros)
    public string head = "#2ea8ff";
    public float w = 1f;              // ancho (0,78 a 1,3)
    public float h = 1f;              // altura (0,8 a 1,25)
    public string hat = "none";
    public string hatColor = "#e8333a";
    public string scarf = "none";
    public string scarfColor = "#19a6d8";
    public string glasses = "none";
    public List<string> acc = new List<string>();
    public string draw;               // la cara dibujada a mano: PNG de 256 × 256 en base64 (vacío = sin dibujo)

    public static readonly string[] BodyColors = { "#2ea8ff", "#33cbe0", "#5fd23c", "#ffc21a", "#ff8a3d", "#ff5e57", "#f047a0", "#9c6bff", "#b8c6d3", "#3a4a5c" };
    public static readonly string[] ClothColors = { "#e8333a", "#ff8a3d", "#ffd23f", "#5fd23c", "#19a6d8", "#2f5fd8", "#9c6bff", "#f047a0", "#ffffff", "#2b2f3a" };

    // (id, nombre en pantalla)
    public static readonly (string id, string label)[] Hats =
        { ("none", "Nada"), ("beanie", "Gorro"), ("cap", "Gorra"), ("tophat", "Copa"), ("crown", "Corona"), ("party", "Fiesta"), ("halo", "Aro burbuja") };
    public static readonly (string id, string label)[] Scarves =
        { ("none", "Nada"), ("plain", "Bufanda"), ("stripes", "A rayas"), ("bow", "Moño"), ("tie", "Corbata"), ("beads", "Collar") };
    public static readonly (string id, string label)[] Glasses =
        { ("none", "Nada"), ("round", "Redondas"), ("sun", "De sol"), ("visor", "Visor aero"), ("3d", "3D"), ("heart", "Corazón"), ("mono", "Monóculo") };
    public static readonly (string id, string label)[] Accessories =
        { ("phones", "Audífonos"), ("flower", "Flor"), ("pin", "Pin SvS"), ("wand", "Burbujas"), ("wings", "Alas"), ("cape", "Capa") };

    public const float MinW = 0.78f, MaxW = 1.3f, MinH = 0.8f, MaxH = 1.25f;

    public bool Has(string accessory) => acc != null && acc.Contains(accessory);

    public AeroLook Clone() => JsonUtility.FromJson<AeroLook>(JsonUtility.ToJson(this));

    public static Color ParseColor(string hex, Color fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }

    // Igual que «Sorpréndeme» de la presentación
    public static AeroLook Random(System.Random rng = null)
    {
        rng ??= new System.Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
        string Pick(string[] a) => a[rng.Next(a.Length)];
        string PickId((string id, string label)[] a) => a[rng.Next(a.Length)].id;
        string bc = Pick(BodyColors);
        var look = new AeroLook
        {
            body = bc,
            head = rng.NextDouble() < 0.6 ? bc : Pick(BodyColors),
            w = (float)Math.Round(0.85 + rng.NextDouble() * 0.35, 2),
            h = (float)Math.Round(0.88 + rng.NextDouble() * 0.3, 2),
            hat = PickId(Hats), hatColor = Pick(ClothColors),
            scarf = PickId(Scarves), scarfColor = Pick(ClothColors),
            glasses = PickId(Glasses),
        };
        foreach (var a in Accessories) if (rng.NextDouble() < 0.18) look.acc.Add(a.id);
        return look;
    }
}

// Los Aeros de la partida: se eligen en el menú y se recuerdan (PlayerPrefs) para la próxima vez
public static class AeroRoster
{
    private static readonly AeroLook[] looks = new AeroLook[MatchConfig.MaxPlayers];

    private static string Key(int player) => $"aeros.jugador{player + 1}";

    public static AeroLook Get(int player)
    {
        player = Mathf.Clamp(player, 0, looks.Length - 1);
        if (looks[player] != null) return looks[player];
        AeroLook loaded = null;
        string json = PlayerPrefs.GetString(Key(player), "");
        if (!string.IsNullOrEmpty(json))
        {
            try { loaded = JsonUtility.FromJson<AeroLook>(json); } catch (ArgumentException) { loaded = null; }
        }
        if (loaded == null)
        {
            loaded = AeroLook.Random(new System.Random(Environment.TickCount + player * 7919));
            loaded.acc.Clear();
        }
        loaded.acc ??= new List<string>();
        return looks[player] = loaded;
    }

    public static void Set(int player, AeroLook look)
    {
        player = Mathf.Clamp(player, 0, looks.Length - 1);
        looks[player] = look;
    }

    public static void Save()
    {
        for (int i = 0; i < looks.Length; i++)
            if (looks[i] != null) PlayerPrefs.SetString(Key(i), JsonUtility.ToJson(looks[i]));
        PlayerPrefs.Save();
    }

    // El nombre que se muestra: el que le puso el jugador, o "Jugador N" si lo dejó vacío
    public static string DisplayName(int player)
    {
        string n = Get(player).name;
        return string.IsNullOrWhiteSpace(n) ? $"Jugador {player + 1}" : n.Trim();
    }
}
