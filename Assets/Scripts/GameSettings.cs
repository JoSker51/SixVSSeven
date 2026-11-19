using UnityEngine;

// Preferencias guardadas en PlayerPrefs: volumen general, música, y los últimos jugadores y meta elegidos en "Nueva partida".
public static class GameSettings
{
    private const string VolumeKey = "opciones.volumen";
    private const string MusicKey = "opciones.musica";
    private const string WinCoinsKey = "opciones.metaMonedas";

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); Apply(); }
    }

    public static float Music
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.5f);
        set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Apply(); }
    }

    // Cantidad de jugadores elegida la última vez en "Nueva partida"
    public static int LastPlayerCount
    {
        get => PlayerPrefs.GetInt("partida.jugadores", 2);
        set => PlayerPrefs.SetInt("partida.jugadores", value);
    }

    // Si nunca se empezó una partida, "Nueva partida" propone la meta de GameBalance
    public static bool HasWinCoins => PlayerPrefs.HasKey(WinCoinsKey);

    public static int WinCoins
    {
        get => PlayerPrefs.GetInt(WinCoinsKey, 100);
        set => PlayerPrefs.SetInt(WinCoinsKey, Mathf.Max(1, value));
    }

    public static void Save() => PlayerPrefs.Save();

    public static void Apply()
    {
        AudioListener.volume = Volume;
        AeroAudio.SetMusicVolume(Music);
    }
}
