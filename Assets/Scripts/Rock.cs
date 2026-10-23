using System;
using System.Collections;
using UnityEngine;

// La pelota de cada tiro. Revienta burbujas (lo detecta cada Bubble), avisa a los objetos que golpea
// y desaparece 2 s después de tocar el suelo, o si se queda quieta, se atasca o sale del mapa.
[RequireComponent(typeof(Rigidbody))]
public class Rock : MonoBehaviour
{
    [Header("Ciclo de vida")]
    [SerializeField] private float destroyDelay = 2f;      // segundos tras tocar el suelo
    [SerializeField] private float maxLifetime = 15f;      // seguro por si se atasca
    [SerializeField] private float killY = -30f;           // si cae fuera del mapa
    [SerializeField] private float settleSpeed = 0.3f;     // quieta encima de un objeto: termina el tiro
    [SerializeField] private float settleTime = 1.5f;
    [SerializeField] private string groundTag = "Ground";

    public int OwnerId { get; private set; }
    public Rigidbody Body => rb;
    public float Radius { get; private set; } = 0.35f;

    // Se dispara justo antes de destruir la pelota (el TurnManager pasa a la fase de resolución)
    public event Action<Rock> Finished;

    private Rigidbody rb;
    private float spawnTime;
    private float slowTime;
    private bool timerStarted;
    private bool finished;
    private float gravityScale = 1f;
    private Vector3 lastPosition;
    private bool hasLastPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnTime = Time.time;
        if (TryGetComponent(out SphereCollider sphere))
            Radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
    }

    // gravityScale > 1: cae más rápido (la catapulta lo compensa con más velocidad, ver GameBalance.projectileSpeed)
    public void Launch(Vector3 direction, float speed, int ownerId, float gravityScale = 1f)
    {
        OwnerId = ownerId;
        this.gravityScale = gravityScale;
        rb.linearVelocity = direction.normalized * speed; // en Unity 2022 o anterior usa rb.velocity
        lastPosition = rb.position;
        hasLastPosition = true;
    }

    private void FixedUpdate()
    {
        if (rb.isKinematic) { hasLastPosition = false; return; }
        if (rb.useGravity && gravityScale != 1f) rb.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);

        // Barrido entre pasos de física: aunque vaya muy rápido, no puede pasar "entre" un paso y otro sin tocar una burbuja
        Vector3 pos = rb.position;
        if (hasLastPosition)
        {
            Vector3 delta = pos - lastPosition;
            float dist = delta.magnitude;
            if (dist > 0.01f)
                foreach (var hit in Physics.SphereCastAll(lastPosition, Radius, delta / dist, dist, ~0, QueryTriggerInteraction.Collide))
                    if (hit.collider.TryGetComponent(out Bubble bubble)) bubble.HitBy(this);
        }
        lastPosition = pos;
        hasLastPosition = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Tocar el suelo inicia la cuenta atrás; mientras tanto puede seguir rodando y reventar burbujas.
        // Los objetos colocados NO tienen el tag "Ground".
        if (collision.collider.CompareTag(groundTag)) StartTimer();

        var shot = ShotTracker.Active;
        var obj = collision.collider.GetComponentInParent<ArenaObject>();
        if (obj != null && shot != null && shot.Rock == this) obj.OnRockHit(shot);
    }

    private void StartTimer()
    {
        if (timerStarted) return;
        timerStarted = true;
        StartCoroutine(DestroyAfterDelay());
    }

    private void Update()
    {
        if (finished) return;
        if (transform.position.y < killY || Time.time - spawnTime > maxLifetime) { Finish(); return; }

        slowTime = rb.linearVelocity.magnitude < settleSpeed ? slowTime + Time.deltaTime : 0f;
        if (slowTime > settleTime) Finish();
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);
        Finish();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        Finished?.Invoke(this);
        Destroy(gameObject);
    }
}
