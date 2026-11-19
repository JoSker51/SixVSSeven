using System.Collections;
using UnityEngine;

// Sensación de los golpes: temblor de cámara y un instante de cámara lenta en los momentos grandes.
// Se crea solo la primera vez que se usa. Respeta la pausa: si el juego está en pausa (timeScale 0) no toca nada.
public class Juice : MonoBehaviour
{
    private static Juice instance;

    // Lo suma CameraRig a su posición en cada frame (se apaga solo)
    public static Vector3 ShakeOffset { get; private set; }

    private float trauma;
    private float slowUntil;
    private float baseFixedDelta;

    private static Juice Ensure()
    {
        if (instance) return instance;
        instance = new GameObject("Juice").AddComponent<Juice>();
        return instance;
    }

    private void Awake()
    {
        instance = this;
        baseFixedDelta = Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        ShakeOffset = Vector3.zero;
        Time.fixedDeltaTime = baseFixedDelta;
    }

    // amount ~0.05 (toque suave) a ~0.4 (golpe fuerte). Los temblores seguidos se suman hasta un tope.
    public static void Shake(float amount)
    {
        if (amount <= 0f) return;
        var j = Ensure();
        j.trauma = Mathf.Min(0.6f, j.trauma + amount);
    }

    public static void SlowMotion()
    {
        var bal = GameBalance.Current;
        var j = Ensure();
        if (Time.timeScale == 0f) return;   // en pausa
        j.slowUntil = Time.unscaledTime + bal.slowMotionSeconds;
        j.StopAllCoroutines();
        j.StartCoroutine(j.SlowRoutine(bal.slowMotionScale));
    }

    private IEnumerator SlowRoutine(float scale)
    {
        Time.timeScale = scale;
        while (Time.unscaledTime < slowUntil)
        {
            if (Time.timeScale == 0f) yield break;   // se pausó: al reanudar, GameHUD vuelve a 1
            yield return null;
        }
        // Vuelve a velocidad normal suave en un instante
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            if (Time.timeScale == 0f) yield break;
            Time.timeScale = Mathf.Lerp(scale, 1f, t / 0.15f);
            yield return null;
        }
        if (Time.timeScale != 0f) Time.timeScale = 1f;
    }

    private void LateUpdate()
    {
        // La física sigue igual de suave en cámara lenta: pasos más cortos cuando el tiempo va lento
        if (Time.timeScale > 0f) Time.fixedDeltaTime = baseFixedDelta * Mathf.Min(1f, Time.timeScale);

        if (trauma <= 0f || Time.timeScale == 0f) { ShakeOffset = Vector3.zero; return; }
        float t = Time.unscaledTime * 28f;
        float s = trauma * trauma;   // el temblor crece rápido con golpes fuertes y se apaga suave
        ShakeOffset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * (2.4f * s);
        trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 1.6f);
    }
}
