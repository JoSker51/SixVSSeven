using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Catapulta de un jugador. Solo responde a los controles cuando el TurnManager la activa.
// A/D mueve, Q/E gira, W/S ajusta el ángulo, Espacio carga y al soltar dispara.
public class CatapultController : MonoBehaviour
{
    public int playerId;
    public Color playerColor = Color.white;

    [Header("Referencias")]
    [SerializeField] private Transform launchPoint;
    [SerializeField] private Rock rockPrefab;

    [Header("Movimiento lateral")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float moveLimit = 6f;          // distancia máxima a cada lado de la posición inicial

    [Header("Giro (Q/E)")]
    [SerializeField] private bool allowYaw = true;
    [SerializeField] private float yawSpeed = 40f;
    [SerializeField] private float maxYaw = 35f;

    [Header("Ángulo de tiro (W/S)")]
    [SerializeField] private float minAngle = 15f;
    [SerializeField] private float maxAngle = 75f;
    [SerializeField] private float angleSpeed = 35f;
    [SerializeField] private float startAngle = 45f;

    [Header("Potencia (Espacio)")]
    [SerializeField] private float minPower = 6f;
    [SerializeField] private float maxPower = 22f;
    [SerializeField] private float chargeCycle = 1.6f;      // segundos para ir de mínimo a máximo y volver

    [Header("Visual (opcional)")]
    [SerializeField] private Transform armPivot;
    [SerializeField] private GameObject activeMarker;
    [SerializeField] private GameObject loadedRock;
    [SerializeField] private LineRenderer trajectory;
    [SerializeField, Range(0f, 1f)] private float trajectoryFraction = 0.5f;   // parte del arco que se muestra
    [SerializeField] private float armRestAngle = -110f;
    [SerializeField] private float armFireAngle = 20f;

    public event Action<CatapultController, Rock> Fired;

    public bool IsActive { get; private set; }
    public bool IsCharging { get; private set; }
    public float Angle { get; private set; }
    public float Power01 { get; private set; }

    private Vector3 startPos;
    private Quaternion startRot;
    private float offset;
    private float yaw;
    private float chargeTime;
    private bool firing;
    private bool spaceArmed;

    private const int TrajectoryPoints = 40;

    private void Awake()
    {
        // Todo se inicializa aquí: el TurnManager llama a SetActive() en su Start
        startPos = transform.position;
        startRot = transform.rotation;
        Angle = startAngle;
        Power01 = 0.5f;
        if (armPivot) armPivot.localRotation = Quaternion.Euler(armRestAngle, 0f, 0f);
        RefreshVisuals();
    }

    // La llama MatchSetup al crear la catapulta de cada jugador
    public void Setup(int player, Color color)
    {
        playerId = player;
        playerColor = color;
        if (trajectory)
        {
            trajectory.startColor = new Color(1f, 1f, 1f, 0.95f);
            trajectory.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        IsCharging = false;
        spaceArmed = false;
        RefreshVisuals();
    }

    private void Update()
    {
        if (!IsActive || firing) return;
        if (Time.timeScale == 0f)   // juego en pausa
        {
            if (!IsCharging) spaceArmed = false;
            return;
        }

        float dt = Time.deltaTime;

        float move = (Held(Key.D) ? 1f : 0f) - (Held(Key.A) ? 1f : 0f);
        offset = Mathf.Clamp(offset + move * moveSpeed * dt, -moveLimit, moveLimit);

        if (allowYaw)
        {
            float turn = (Held(Key.E) ? 1f : 0f) - (Held(Key.Q) ? 1f : 0f);
            yaw = Mathf.Clamp(yaw + turn * yawSpeed * dt, -maxYaw, maxYaw);
        }

        float aim = (Held(Key.W) ? 1f : 0f) - (Held(Key.S) ? 1f : 0f);
        Angle = Mathf.Clamp(Angle + aim * angleSpeed * dt, minAngle, maxAngle);

        transform.SetPositionAndRotation(
            startPos + startRot * Vector3.right * offset,
            startRot * Quaternion.Euler(0f, yaw, 0f));

        // Hay que soltar Espacio antes de cargar (por si se usó para un botón del menú de pausa)
        bool space = Held(Key.Space);
        if (!space) spaceArmed = true;
        if (space && (IsCharging || spaceArmed))
        {
            if (!IsCharging) { IsCharging = true; chargeTime = 0f; }
            chargeTime += dt;
            Power01 = Mathf.PingPong(chargeTime * 2f / chargeCycle, 1f);
            if (armPivot) armPivot.localRotation = Quaternion.Euler(armRestAngle - 12f * Power01, 0f, 0f);
        }
        else if (IsCharging && !space)
        {
            IsCharging = false;
            StartCoroutine(FireRoutine());
        }

        UpdateTrajectory();
    }

    public Vector3 LaunchDirection => transform.rotation * Quaternion.Euler(-Angle, 0f, 0f) * Vector3.forward;
    // La velocidad sube con projectileSpeed y la gravedad del tiro con su cuadrado: mismas distancias, vuelo más rápido
    public float LaunchSpeed => Mathf.Lerp(minPower, maxPower, Power01) * GameBalance.Current.projectileSpeed;
    public static Vector3 ShotGravity => Physics.gravity * GameBalance.Current.ProjectileGravityScale;
    private Vector3 LaunchOrigin => launchPoint ? launchPoint.position : transform.position + Vector3.up * 2f;

    private IEnumerator FireRoutine()
    {
        firing = true;
        IsActive = false;          // un tiro por turno: el TurnManager la reactiva en su próximo turno
        RefreshVisuals();

        yield return SwingArm(armRestAngle - 12f * Power01, armFireAngle, 0.12f);

        Rock rock = Instantiate(rockPrefab, LaunchOrigin, Quaternion.identity);
        var rockCol = rock.GetComponent<Collider>();
        if (rockCol)
            foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(rockCol, c);

        var rend = rock.GetComponent<Renderer>();
        if (rend) rend.material.color = Color.Lerp(playerColor, Color.white, 0.2f);
        var trail = rock.GetComponent<TrailRenderer>();
        if (trail) { trail.startColor = Color.Lerp(playerColor, Color.white, 0.5f); trail.endColor = new Color(playerColor.r, playerColor.g, playerColor.b, 0f); }

        rock.Launch(LaunchDirection, LaunchSpeed, playerId, GameBalance.Current.ProjectileGravityScale);
        AeroAudio.Play(Sfx.Launch);
        Fired?.Invoke(this, rock);

        yield return SwingArm(armFireAngle, armRestAngle, 0.6f);
        firing = false;
    }

    private IEnumerator SwingArm(float from, float to, float duration)
    {
        if (!armPivot) yield break;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            armPivot.localRotation = Quaternion.Euler(Mathf.Lerp(from, to, t / duration), 0f, 0f);
            yield return null;
        }
        armPivot.localRotation = Quaternion.Euler(to, 0f, 0f);
    }

    private void UpdateTrajectory()
    {
        if (!trajectory) return;

        // Arco balístico simple (sin rozamiento) para ayudar a apuntar
        Vector3 p0 = LaunchOrigin;
        Vector3 v0 = LaunchDirection * LaunchSpeed;
        Vector3 g = ShotGravity;   // la misma gravedad que usa la pelota

        // Tiempo hasta tocar y = 0
        float a = 0.5f * g.y, b = v0.y, c = p0.y;
        float disc = b * b - 4f * a * c;
        float tGround = disc > 0f ? (-b - Mathf.Sqrt(disc)) / (2f * a) : 3f;
        float tMax = Mathf.Max(0.1f, tGround * trajectoryFraction);

        trajectory.positionCount = TrajectoryPoints;
        for (int i = 0; i < TrajectoryPoints; i++)
        {
            float t = tMax * i / (TrajectoryPoints - 1);
            trajectory.SetPosition(i, p0 + v0 * t + 0.5f * g * t * t);
        }
    }

    private void RefreshVisuals()
    {
        if (activeMarker) activeMarker.SetActive(IsActive);
        if (loadedRock) loadedRock.SetActive(IsActive);
        if (trajectory) trajectory.enabled = IsActive;
        if (IsActive) UpdateTrajectory();
    }

#if ENABLE_INPUT_SYSTEM
    private static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
#else
    private enum Key { A, D, Q, E, W, S, Space }
    private static bool Held(Key key)
    {
        switch (key)
        {
            case Key.A: return Input.GetKey(KeyCode.A);
            case Key.D: return Input.GetKey(KeyCode.D);
            case Key.Q: return Input.GetKey(KeyCode.Q);
            case Key.E: return Input.GetKey(KeyCode.E);
            case Key.W: return Input.GetKey(KeyCode.W);
            case Key.S: return Input.GetKey(KeyCode.S);
            default: return Input.GetKey(KeyCode.Space);
        }
    }
#endif
}
