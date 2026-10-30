using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Burbuja objetivo: la pelota la revienta al tocarla y sigue de largo.
// Solo avisa al registro del tiro (ShotTracker); las monedas se calculan al final, en un solo lugar.
// Una "burbuja de castillo" flota sobre un castillo: si la revienta un rival, le roba monedas al dueño.
[DefaultExecutionOrder(110)]   // después de la cámara, para mirar siempre hacia ella
[RequireComponent(typeof(SphereCollider))]
public class Bubble : MonoBehaviour
{
    public static readonly List<Bubble> All = new List<Bubble>();

    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private GameObject popEffect;

    public int Value { get; private set; }
    public float Radius { get; private set; }
    public int Section { get; private set; }
    public bool IsCastle { get; private set; }
    public int OwnerId { get; private set; } = -1;
    public bool Popped { get; private set; }

    private SphereCollider col;
    private Color tint = Color.white;
    private float phase;
    private Camera cam;

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;   // la escala del objeto es el diámetro
        phase = Random.value * 10f;
        All.Add(this);
    }

    private void OnDestroy() => All.Remove(this);

    public void Place(Vector3 position, float radius, int value, int section)
    {
        IsCastle = false;
        OwnerId = -1;
        Section = section;
        Value = value;
        SetShape(position, radius);
        tint = value >= 5 ? new Color(1f, 0.93f, 0.65f) : value >= 3 ? new Color(0.8f, 1f, 0.92f) : Color.white;
        Show();
    }

    public void PlaceAsCastle(Vector3 position, float radius, int owner, Color ownerColor)
    {
        IsCastle = true;
        OwnerId = owner;
        Section = -1;
        Value = 0;
        SetShape(position, radius);
        tint = Color.Lerp(ownerColor, Color.white, 0.45f);
        Show();
    }

    private void SetShape(Vector3 position, float radius)
    {
        Radius = radius;
        transform.position = position;
        transform.localScale = Vector3.one * (radius * 2f);
    }

    private void Show()
    {
        Popped = false;
        col.enabled = true;
        if (sprite)
        {
            sprite.enabled = true;
            sprite.color = tint;
        }
    }

    // Sin lugar libre: queda oculta (como reventada) hasta el próximo intento de reaparecer
    public void HideUntilRespawn(int section)
    {
        Section = section;
        Popped = true;
        col.enabled = false;
        if (sprite) sprite.enabled = false;
    }

    private void OnTriggerEnter(Collider other) => TryPop(other);
    private void OnTriggerStay(Collider other) => TryPop(other);

    private void TryPop(Collider other)
    {
        if (Popped) return;
        var rock = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<Rock>() : null;
        if (rock != null) HitBy(rock);
    }

    // También la llama el barrido de la pelota (Rock.FixedUpdate) por si va tan rápido que el trigger no la ve
    public void HitBy(Rock rock)
    {
        if (Popped || rock == null) return;
        if (IsCastle && rock.OwnerId == OwnerId) return;   // el dueño no revienta la burbuja de su castillo

        var shot = ShotTracker.Active;
        if (shot == null || shot.Rock != rock) return;
        if (!shot.RegisterBubble(this)) return;           // cada burbuja cuenta una sola vez

        Pop();
        if (IsCastle) GameHUD.Popup(transform.position, "¡Robo!", new Color(1f, 0.45f, 0.6f));
        else GameHUD.Popup(transform.position, "+" + Value, new Color(1f, 0.82f, 0.2f));
    }

    private void Pop()
    {
        Popped = true;
        col.enabled = false;
        AeroAudio.Play(Sfx.Pop);
        if (popEffect) Instantiate(popEffect, transform.position, Quaternion.identity).transform.localScale = Vector3.one * Mathf.Max(1f, Radius);
        StartCoroutine(PopAnimation());
    }

    private IEnumerator PopAnimation()
    {
        if (!sprite) yield break;
        Vector3 baseScale = sprite.transform.localScale;
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            float k = t / 0.15f;
            sprite.transform.localScale = baseScale * (1f + 0.4f * k);
            sprite.color = new Color(tint.r, tint.g, tint.b, 1f - k);
            yield return null;
        }
        sprite.transform.localScale = baseScale;
        sprite.enabled = false;
    }

    private void LateUpdate()
    {
        if (!sprite || !sprite.enabled) return;
        if (!cam) cam = Camera.main;
        if (cam) sprite.transform.rotation = cam.transform.rotation;
    }
}
