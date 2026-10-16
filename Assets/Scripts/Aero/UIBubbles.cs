using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Burbujas que suben flotando por la pantalla (detrás de los paneles)
public class UIBubbles : MonoBehaviour
{
    [SerializeField] private Sprite bubble;
    [SerializeField] private int count = 16;
    [SerializeField] private Vector2 sizeRange = new Vector2(24f, 110f);
    [SerializeField] private Vector2 speedRange = new Vector2(30f, 80f);
    [SerializeField, Range(0f, 1f)] private float alpha = 0.85f;

    private class Bubble { public RectTransform rt; public float x, y, speed, size, phase, wobble; }

    private readonly List<Bubble> bubbles = new List<Bubble>();
    private RectTransform area;

    private void Start()
    {
        area = (RectTransform)transform;
        for (int i = 0; i < count; i++)
        {
            var img = AeroUI.Img(transform, "Burbuja", bubble, new Color(1f, 1f, 1f, alpha));
            var b = new Bubble { rt = img.rectTransform };
            Respawn(b, true);
            bubbles.Add(b);
        }
    }

    private void Respawn(Bubble b, bool anywhere)
    {
        Rect r = area.rect;
        b.size = Random.Range(sizeRange.x, sizeRange.y);
        float big = Mathf.InverseLerp(sizeRange.x, sizeRange.y, b.size);
        b.speed = Random.Range(speedRange.x, speedRange.y) * Mathf.Lerp(1.2f, 0.7f, big);   // las grandes suben más lento
        b.x = Random.Range(r.xMin, r.xMax);
        b.y = anywhere ? Random.Range(r.yMin, r.yMax) : r.yMin - b.size;
        b.phase = Random.value * 10f;
        b.wobble = Random.Range(8f, 26f);
        b.rt.sizeDelta = new Vector2(b.size, b.size);
    }

    private void Update()
    {
        if (area == null) return;
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        Rect r = area.rect;
        foreach (var b in bubbles)
        {
            b.y += b.speed * dt;
            if (b.y > r.yMax + b.size) Respawn(b, false);
            b.rt.anchoredPosition = new Vector2(b.x + Mathf.Sin(t * 0.8f + b.phase) * b.wobble, b.y);
        }
    }
}
