using System.Collections.Generic;
using UnityEngine;

// Objeto colocable (cubo, pared, rampa, aro, cilindro, esfera o pirámide; normal, positivo o negativo).
// La misma lógica sirve para todas las variantes: lo que cambia son los datos de su ItemDefinition.
// La pelota avisa cuando lo golpea (Rock) y el aro detecta solo cuándo la pelota pasa por dentro.
public class ArenaObject : MonoBehaviour
{
    public static readonly List<ArenaObject> All = new List<ArenaObject>();

    [SerializeField] private string itemId;
    [SerializeField] private float ringInnerRadius = 1.44f;   // solo aros: radio libre del agujero

    public ItemDefinition Definition { get; private set; }
    public string ItemId => itemId;
    public ItemKind Kind => Definition != null ? Definition.kind : ItemKind.Cube;
    public ItemVariant Variant => Definition != null ? Definition.variant : ItemVariant.Normal;
    public int OwnerId { get; private set; } = -1;
    public bool IsPlaced { get; private set; }
    public float RingInnerRadius => ringInnerRadius;

    private Rock trackedRock;
    private Vector3 prevLocal;
    private bool hasPrev;

    public void Init(ItemDefinition def, int owner)
    {
        Definition = def;
        itemId = def.id;
        OwnerId = owner;
        IsPlaced = true;
        if (!All.Contains(this)) All.Add(this);
    }

    public void RemoveFromArena()
    {
        IsPlaced = false;
        All.Remove(this);
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;   // deja de colisionar en este mismo frame
        Destroy(gameObject);
    }

    private void OnDestroy() => All.Remove(this);

    // Lo llama la pelota en OnCollisionEnter
    public void OnRockHit(ShotRecord shot)
    {
        if (!IsPlaced || shot == null || !shot.RegisterHit(this)) return;
        var b = GameBalance.Current;
        if (Variant == ItemVariant.Positive) GameHUD.Popup(transform.position, $"+{b.positiveBonus}", new Color(0.35f, 0.95f, 0.55f));
        else if (Variant == ItemVariant.Negative) GameHUD.Popup(transform.position, $"−{b.negativePenalty}", new Color(1f, 0.4f, 0.55f));
        AeroAudio.Play(Sfx.Hover, 0.8f);
    }

    // Aro: cuenta solo si el centro de la pelota cruza el plano del aro con toda la pelota dentro del agujero.
    // Se compara la posición entre pasos de física, así una pelota rápida no "salta" el aro sin detectarse.
    private void FixedUpdate()
    {
        if (!IsPlaced || Kind != ItemKind.Ring) return;
        var shot = ShotTracker.Active;
        var rock = shot?.Rock;
        if (rock == null) { hasPrev = false; trackedRock = null; return; }

        Vector3 local = transform.InverseTransformPoint(rock.Body.position);
        if (hasPrev && trackedRock == rock && (prevLocal.y > 0f) != (local.y > 0f))
        {
            float t = prevLocal.y / (prevLocal.y - local.y);
            Vector3 cross = Vector3.Lerp(prevLocal, local, t);
            float radial = new Vector2(cross.x, cross.z).magnitude;
            if (radial + rock.Radius <= ringInnerRadius + 0.02f && shot.RegisterRing(this))
            {
                GameHUD.Popup(transform.position, $"×{GameBalance.Current.ringMultiplier}", new Color(1f, 0.85f, 0.3f));
                AeroAudio.Play(Sfx.Ring);
            }
        }
        prevLocal = local;
        hasPrev = true;
        trackedRock = rock;
    }
}
