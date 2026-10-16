using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CastleHole : MonoBehaviour
{
    [SerializeField] private int ownerId;          // dueño del castillo
    [SerializeField] private int stealAmount = 3;  // monedas que se roban por acierto
    [SerializeField] private int maxStealPerShot = 10;  // tope por si hay multiplicadores

    public int OwnerId => ownerId;
    public int StealAmount => stealAmount;

    private float radius;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
    }

    private void OnTriggerEnter(Collider other) { TryHit(other); }
    private void OnTriggerStay(Collider other) { TryHit(other); }

    private void TryHit(Collider other)
    {
        var rock = other.GetComponentInParent<Rock>();
        if (rock == null || rock.Consumed) return;

        Vector3 d = rock.transform.position - transform.position;
        d.y = 0f;
        if (d.magnitude > radius) return;

        if (!rock.TryConsume()) return;
        rock.SinkInto(transform.position);

        // Si el dueño encesta en su propio hueco, no pasa nada (la roca igual se elimina)
        if (rock.OwnerId == ownerId) return;

        int amount = Mathf.Clamp(Mathf.RoundToInt(stealAmount * rock.Multiplier), 1, maxStealPerShot);
        int stolen = GameManager.Instance.Transfer(ownerId, rock.OwnerId, amount);
        AeroAudio.Play(Sfx.Steal);
        GameHUD.Popup(transform.position, stolen > 0 ? $"¡Robo! +{stolen}" : "Robo: 0", new Color(1f, 0.4f, 0.55f));
        Debug.Log($"Jugador {rock.OwnerId} le quita {stolen} monedas al jugador {ownerId}");
    }
}
