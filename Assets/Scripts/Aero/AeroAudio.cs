using System.Collections.Generic;
using UnityEngine;

public enum Sfx { Hover, Click, Launch, Score, Steal, Win }

// Efectos y música ambiente generados por código (estilo burbujeante), así no hay que importar audios.
// Se crea solo al arrancar cualquier escena y sobrevive a los cambios de escena.
public class AeroAudio : MonoBehaviour
{
    private const int Rate = 44100;
    private static AeroAudio instance;

    private AudioSource sfxSource;
    private AudioSource musicSource;
    private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    private static AeroAudio EnsureInstance()
    {
        if (instance) return instance;
        var go = new GameObject("AeroAudio");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AeroAudio>();
        return instance;
    }

    public static void Play(Sfx sfx, float volume = 1f)
    {
        var a = EnsureInstance();
        if (a.clips.TryGetValue(sfx, out var clip)) a.sfxSource.PlayOneShot(clip, volume);
    }

    public static void SetMusicVolume(float volume)
    {
        if (instance) instance.musicSource.volume = volume;
    }

    private void Awake()
    {
        instance = this;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;

        clips[Sfx.Hover] = Clip("hover", Hover());
        clips[Sfx.Click] = Clip("click", Bloop());
        clips[Sfx.Launch] = Clip("launch", Whoosh());
        clips[Sfx.Score] = Clip("score", Chime(new[] { 1318.5f, 1975.5f }, 0.09f, 1.0f));
        clips[Sfx.Steal] = Clip("steal", Wah());
        clips[Sfx.Win] = Clip("win", Chime(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.11f, 2.0f));

        musicSource.clip = Clip("musica", Music());
        GameSettings.Apply();
        musicSource.volume = GameSettings.Music;
        musicSource.Play();
    }

    // ---------- Generadores ----------

    private static AudioClip Clip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

    private static float[] Hover()
    {
        var d = Buffer(0.05f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 1900f * t) * Mathf.Exp(-t * 90f) * 0.18f;
        }
        return d;
    }

    // "Bloop" de burbuja: tono que sube rápido y se apaga
    private static float[] Bloop()
    {
        var d = Buffer(0.2f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = 380f + 900f * (1f - Mathf.Exp(-t * 30f));
            phase += 2f * Mathf.PI * f / Rate;
            float env = (1f - Mathf.Exp(-t * 800f)) * Mathf.Exp(-t * 22f);
            d[i] = (Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase)) * env * 0.45f;
        }
        return d;
    }

    // Ruido filtrado (zumbido del brazo) + golpe de madera
    private static float[] Whoosh()
    {
        var d = Buffer(0.6f);
        var rng = new System.Random(3);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float u = t / 0.6f;
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-Mathf.Max(0f, t - 0.05f) * 6f);
            float cutoff = Mathf.Lerp(0.02f, 0.22f, Mathf.Sin(Mathf.PI * u));
            lp += cutoff * ((float)rng.NextDouble() * 2f - 1f - lp);
            float thunk = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 25f);
            d[i] = lp * env * 1.4f + thunk * 0.5f;
        }
        return d;
    }

    // Campanitas: cada nota entra un poco después de la anterior
    private static float[] Chime(float[] notes, float spacing, float seconds)
    {
        var d = Buffer(seconds);
        for (int n = 0; n < notes.Length; n++)
        {
            int start = Mathf.RoundToInt(n * spacing * Rate);
            float f = notes[n];
            for (int i = start; i < d.Length; i++)
            {
                float t = (i - start) / (float)Rate;
                float env = (1f - Mathf.Exp(-t * 400f)) * Mathf.Exp(-t * 4.5f);
                float w = 2f * Mathf.PI * f * t;
                d[i] += (Mathf.Sin(w) + 0.35f * Mathf.Sin(2f * w) + 0.12f * Mathf.Sin(3.01f * w)) * env * 0.22f;
            }
        }
        return d;
    }

    // "Uuuh" descendente para el robo
    private static float[] Wah()
    {
        var d = Buffer(0.5f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(560f, 180f, t / 0.5f) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 8f * t));
            phase += 2f * Mathf.PI * f / Rate;
            float env = (1f - Mathf.Exp(-t * 200f)) * Mathf.Exp(-t * 3f);
            d[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase)) * env * 0.4f;
        }
        return d;
    }

    // Música ambiente de 16 s que se repite sin cortes: acordes suaves, bajo y campanitas
    private static float[] Music()
    {
        const float loop = 16f;
        var d = Buffer(loop);

        float[][] pads =
        {
            new[] { 329.63f, 392.00f, 493.88f, 587.33f },   // Cmaj9
            new[] { 261.63f, 329.63f, 392.00f, 493.88f },   // Am9
            new[] { 220.00f, 261.63f, 329.63f, 392.00f },   // Fmaj9
            new[] { 246.94f, 293.66f, 329.63f, 440.00f },   // G6/9
        };
        float[] bass = { 130.81f, 110.00f, 87.31f, 98.00f };
        float[] bells =
        {
            659.3f, 0, 784.0f, 0, 987.8f, 784.0f, 0, 587.3f,
            523.3f, 0, 659.3f, 0, 880.0f, 0, 784.0f, 0,
            440.0f, 0, 523.3f, 0, 659.3f, 784.0f, 0, 659.3f,
            587.3f, 0, 493.9f, 0, 587.3f, 0, 659.3f, 784.0f,
        };

        for (int c = 0; c < 4; c++)
        {
            foreach (var f in pads[c]) AddNote(d, c * 4f, 4.8f, f, 0.05f, Pad);
            AddNote(d, c * 4f, 4.4f, bass[c], 0.11f, Bass);
        }
        for (int s = 0; s < bells.Length; s++)
            if (bells[s] > 0f) AddNote(d, s * 0.5f, 2.5f, bells[s], 0.07f, Bell);

        float peak = 0f;
        foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > 0f) for (int i = 0; i < d.Length; i++) d[i] *= 0.7f / peak;
        return d;
    }

    private delegate float Voice(float t, float duration, float f);

    // Suma la nota dando la vuelta al final del buffer, para que el bucle no tenga corte
    private static void AddNote(float[] d, float start, float duration, float f, float amp, Voice voice)
    {
        int s0 = Mathf.RoundToInt(start * Rate);
        int n = Mathf.RoundToInt(duration * Rate);
        for (int i = 0; i < n; i++)
            d[(s0 + i) % d.Length] += voice(i / (float)Rate, duration, f) * amp;
    }

    private static float Pad(float t, float duration, float f)
    {
        float env = Mathf.SmoothStep(0f, 1f, t / 1.2f) * Mathf.SmoothStep(0f, 1f, (duration - t) / 1.4f);
        float w = 2f * Mathf.PI * f * t;
        return (Mathf.Sin(w) + 0.5f * Mathf.Sin(w * 1.004f) + 0.15f * Mathf.Sin(2f * w)) * env;
    }

    private static float Bass(float t, float duration, float f)
    {
        float env = Mathf.Clamp01(t / 0.05f) * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(t / 1f)) * Mathf.SmoothStep(0f, 1f, (duration - t) / 0.6f);
        float w = 2f * Mathf.PI * f * t;
        return (Mathf.Sin(w) + 0.2f * Mathf.Sin(2f * w)) * env;
    }

    private static float Bell(float t, float duration, float f)
    {
        float env = (1f - Mathf.Exp(-t * 300f)) * Mathf.Exp(-t * 2.5f);
        float w = 2f * Mathf.PI * f * t;
        return (Mathf.Sin(w) + 0.3f * Mathf.Sin(2f * w) * Mathf.Exp(-t * 4f)) * env;
    }
}
