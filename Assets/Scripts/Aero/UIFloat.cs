using UnityEngine;

// Movimiento suave de vaivén para títulos y adornos de la interfaz
public class UIFloat : MonoBehaviour
{
    [SerializeField] private Vector2 amplitude = new Vector2(0f, 8f);
    [SerializeField] private float speed = 0.5f;      // ciclos por segundo
    [SerializeField] private float phase;

    private RectTransform rt;
    private Vector2 basePos;

    private void Awake()
    {
        rt = (RectTransform)transform;
        basePos = rt.anchoredPosition;
    }

    private void Update()
    {
        float t = Time.unscaledTime * speed * 2f * Mathf.PI + phase;
        rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 0.7f) * amplitude.x, Mathf.Sin(t) * amplitude.y);
    }
}
