using UnityEngine;

// Opciones del jugador guardadas en PlayerPrefs: volumen general, música y meta de monedas.
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

    // Si nunca se tocó en Opciones, el GameManager usa su valor del Inspector
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
