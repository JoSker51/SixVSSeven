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
    public bool Resting { get; set; }   // apoyado en el suelo o en otro objeto (no flotando a propósito)

    private Rock trackedRock;
    private Vector3 prevLocal;
    private bool hasPrev;
    private int ringSide;              // lado del aro donde está la pelota: +1, −1 o 0 (todavía en el plano)
    private bool lastCrossInside;      // el último cruce del plano fue por dentro del agujero

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

    // Cae (con aceleración) cuando se retira lo que tenía debajo. La física ya está en 'to': esto es solo lo que se ve.
    public void AnimateFall(Vector3 from, Vector3 to)
    {
        StopAllCoroutines();
        StartCoroutine(Fall(from, to));
    }

    private System.Collections.IEnumerator Fall(Vector3 from, Vector3 to)
    {
        float duration = Mathf.Clamp(Mathf.Sqrt(2f * Vector3.Distance(from, to) / 9.81f), 0.12f, 0.6f);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            transform.position = Vector3.LerpUnclamped(from, to, k * k);
            yield return null;
        }
        transform.position = to;
        Physics.SyncTransforms();
    }

    // Lo llama la pelota en OnCollisionEnter
    public void OnRockHit(ShotRecord shot)
    {
        if (!IsPlaced || shot == null || !shot.RegisterHit(this)) return;
        var b = GameBalance.Current;
        if (Variant == ItemVariant.Positive) GameHUD.Popup(transform.position, $"+{b.positiveBonus}", new Color(0.35f, 0.95f, 0.55f));
        else if (Variant == ItemVariant.Negative) GameHUD.Popup(transform.position, $"−{b.negativePenalty}", new Color(1f, 0.4f, 0.55f));
        AeroAudio.Play(Sfx.Hover, 0.8f);
    }

    // Aro: cada pasada cuenta (también si la pelota vuelve a cruzar el mismo aro) y vuelve a duplicar.
    // Una pasada es cruzar el plano por dentro del agujero, con toda la pelota adentro, y salir del otro lado
    // al menos un radio de pelota: así una pelota que tiembla justo en el plano no suma pasadas falsas.
    // Se compara la posición entre pasos de física, así una pelota rápida no "salta" el aro sin detectarse.
    private void FixedUpdate()
    {
        if (!IsPlaced || Kind != ItemKind.Ring) return;
        var shot = ShotTracker.Active;
        var rock = shot?.Rock;
        if (rock == null) { hasPrev = false; trackedRock = null; return; }
        if (trackedRock != rock) { hasPrev = false; ringSide = 0; lastCrossInside = false; }

        Vector3 local = transform.InverseTransformPoint(rock.Body.position);
        if (hasPrev && (prevLocal.y > 0f) != (local.y > 0f))
        {
            float t = prevLocal.y / (prevLocal.y - local.y);
            Vector3 cross = Vector3.Lerp(prevLocal, local, t);
            float radial = new Vector2(cross.x, cross.z).magnitude;
            lastCrossInside = radial + rock.Radius <= ringInnerRadius + 0.02f;
        }
        int side = local.y > rock.Radius ? 1 : local.y < -rock.Radius ? -1 : 0;
        if (side != 0)
        {
            if (ringSide != 0 && side != ringSide && lastCrossInside)
            {
                int passes = shot.RegisterRingPass(this);
                GameHUD.Popup(transform.position, $"×{RewardCalculator.Multiplier(passes, GameBalance.Current)}", new Color(1f, 0.85f, 0.3f));
                AeroAudio.Play(Sfx.Ring);
            }
            ringSide = side;
            lastCrossInside = false;
        }
        prevLocal = local;
        hasPrev = true;
        trackedRock = rock;
    }
}
