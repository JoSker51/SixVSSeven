using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Rock : MonoBehaviour
{
    [Header("Ciclo de vida")]
    [SerializeField] private float destroyDelay = 2f;      // segundos tras puntuar o tocar el suelo
    [SerializeField] private float maxLifetime = 15f;      // seguro por si se atasca
    [SerializeField] private float killY = -30f;           // si cae fuera del mapa
    [SerializeField] private string groundTag = "Ground";

    public int OwnerId { get; private set; }
    public bool Consumed { get; private set; }   // ya sumó o restó puntos
    public float Multiplier { get; set; } = 1f;  // lo modifican los obstáculos (paso 4)

    // Se dispara justo antes de destruir la roca (el TurnManager lo usa para pasar de turno)
    public event Action<Rock> Finished;

    private Rigidbody rb;
    private float spawnTime;
    private bool timerStarted;
    private bool finished;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnTime = Time.time;
    }

    public void Launch(Vector3 direction, float speed, int ownerId)
    {
        OwnerId = ownerId;
        rb.linearVelocity = direction.normalized * speed; // en Unity 2022 o anterior usa rb.velocity
    }

    // Los huecos llaman a esto antes de dar puntos: devuelve true solo una vez por roca
    public bool TryConsume()
    {
        if (Consumed) return false;
        Consumed = true;
        StartTimer();
        return true;
    }

    // Efecto visual: la roca cae dentro del hueco en el que puntuó
    public void SinkInto(Vector3 holeCenter)
    {
        rb.isKinematic = true;
        StartCoroutine(SinkRoutine(holeCenter));
    }

    private IEnumerator SinkRoutine(Vector3 holeCenter)
    {
        Vector3 from = transform.position;
        Vector3 to = new Vector3(holeCenter.x, holeCenter.y - 1.5f, holeCenter.z);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            transform.position = Vector3.Lerp(from, to, t * t);
            yield return null;
        }
        transform.position = to;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Tocar el suelo inicia la cuenta atrás; aun así puede entrar a un hueco durante esos 2 s.
        // Los obstáculos NO deben tener el tag "Ground".
        if (collision.collider.CompareTag(groundTag)) StartTimer();
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
        if (transform.position.y < killY || Time.time - spawnTime > maxLifetime) Finish();
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
