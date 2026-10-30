using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Cámara del juego:
//  - Compra y Colocación: vista libre. Empieza desde arriba, girada para que el jugador de turno quede abajo.
//    Clic derecho + arrastrar gira, WASD (o arrastrar con la ruedita apretada) mueve, la ruedita acerca o aleja
//    y C vuelve a la vista inicial. Cada turno empieza otra vez desde la vista inicial.
//  - Lanzamiento: detrás de la catapulta activa, siguiendo a la pelota en vuelo. C alterna con la vista general.
public class CameraRig : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 11f, -7.5f);  // relativo a la catapulta
    [SerializeField] private float lookAhead = 12f;
    [SerializeField, Range(0f, 1f)] private float followRock = 0.35f;              // cuánto avanza la cámara con la pelota
    [SerializeField] private float planDistance = 26f;                             // vista inicial de planificación
    [SerializeField] private float planHeight = 46f;
    [SerializeField] private Vector3 overviewPosition = new Vector3(0f, 52f, -36f);
    [SerializeField] private Vector3 overviewLookAt = Vector3.zero;
    [SerializeField] private float smoothing = 3.5f;

    [Header("Vista libre (Compra y Colocación)")]
    [SerializeField] private float orbitSpeed = 0.25f;                             // grados por píxel
    [SerializeField] private float panSpeed = 0.9f;                                // por segundo, según lo lejos que esté
    [SerializeField] private Vector2 pitchLimits = new Vector2(12f, 88f);
    [SerializeField] private Vector2 distanceLimits = new Vector2(8f, 80f);
    [SerializeField] private float freeSmoothing = 12f;

    private bool overview;
    private Vector3 lookPoint;

    private Vector3 focus;
    private float freeYaw, freePitch, freeDistance;
    private bool freeReady;
    private bool orbiting;
    private float planningTime;

    public bool FreeView { get; private set; }
    public Vector3 Focus => focus;
    public float Yaw => freeYaw;
    public float Pitch => freePitch;
    public float Distance => freeDistance;

    private void Start()
    {
        lookPoint = transform.position + transform.forward * 20f;
        if (turnManager) turnManager.TurnStarted += _ => freeReady = false;
    }

    private void LateUpdate()
    {
        var phase = turnManager ? turnManager.Phase : TurnPhase.Aiming;
        if (TogglePressed() && phase == TurnPhase.Aiming) overview = !overview;

        Vector3 targetPos = overviewPosition;
        Vector3 targetLook = overviewLookAt;

        var gm = GameManager.Instance;
        bool gameOver = gm != null && gm.GameOver;
        var cat = turnManager ? turnManager.Current : null;
        FreeView = false;

        if (!gameOver && cat != null)
        {
            Transform t = cat.transform;
            bool planning = phase == TurnPhase.Shop || phase == TurnPhase.Placement || phase == TurnPhase.Setup;
            if (planning)
            {
                FreeView = true;
                if (!freeReady || TogglePressed()) ResetFreeView(t);
                HandleFreeInput();
                targetPos = FreePosition();
                targetLook = focus;
            }
            else if (!overview)
            {
                Vector3 basePos = t.position + t.rotation * followOffset;
                Rock rock = turnManager.CurrentRock;
                if (rock != null)
                {
                    Vector3 delta = rock.transform.position - t.position;
                    delta.y = 0f;
                    targetPos = basePos + delta * followRock;
                    targetLook = rock.transform.position;
                }
                else
                {
                    targetPos = basePos;
                    targetLook = t.position + t.forward * lookAhead;
                }
            }
        }

        // Al entrar a la vista libre la cámara llega suave; después responde rápido a lo que hace el jugador
        planningTime = FreeView ? planningTime + Time.deltaTime : 0f;
        float rate = FreeView ? Mathf.Lerp(smoothing, freeSmoothing, Mathf.Clamp01(planningTime / 0.8f)) : smoothing;
        float k = 1f - Mathf.Exp(-rate * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, k);
        lookPoint = Vector3.Lerp(lookPoint, targetLook, Mathf.Clamp01(k * 1.5f));
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
    }

    // ---------------- Vista libre ----------------

    // La misma vista de siempre: desde arriba, del lado del jugador de turno
    private void ResetFreeView(Transform cat)
    {
        Vector3 outward = -cat.forward;
        outward.y = 0f;
        outward.Normalize();
        Vector3 center = Arena.Instance ? Arena.Instance.Center : Vector3.zero;
        focus = center - outward * 2f;
        freeYaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
        freePitch = Mathf.Atan2(planHeight, planDistance) * Mathf.Rad2Deg;
        freeDistance = new Vector2(planDistance, planHeight).magnitude;
        freeReady = true;
        planningTime = 0f;
    }

    private void HandleFreeInput()
    {
        if (Time.timeScale == 0f) { orbiting = false; return; }   // en pausa
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Girar: clic derecho + arrastrar (si el clic empezó sobre la interfaz, no gira)
        if (GameInput.RightClick) orbiting = !overUI;
        if (!GameInput.RightHeld) orbiting = false;
        Vector2 d = GameInput.MouseDelta;
        if (orbiting && GameInput.RightDragging) Orbit(d.x * orbitSpeed, -d.y * orbitSpeed);

        // Mover: WASD, o arrastrar con la ruedita apretada
        Vector2 move = new Vector2((GameInput.Held(Key.D) ? 1f : 0f) - (GameInput.Held(Key.A) ? 1f : 0f),
                                   (GameInput.Held(Key.W) ? 1f : 0f) - (GameInput.Held(Key.S) ? 1f : 0f));
        if (move != Vector2.zero)
        {
            Pan(move * panSpeed * freeDistance * Time.deltaTime);
            // WASD también mueve la selección de la interfaz: se suelta para que Enter no apriete otro botón
            if (EventSystem.current && (GameInput.Pressed(Key.W) || GameInput.Pressed(Key.A) || GameInput.Pressed(Key.S) || GameInput.Pressed(Key.D)))
                EventSystem.current.SetSelectedGameObject(null);
        }
        if (GameInput.MiddleHeld && !overUI) Pan(-d * freeDistance * 0.0016f);

        // Acercar y alejar: la ruedita (con Shift la ruedita sube o baja el objeto que estás colocando)
        if (!overUI && !GameInput.ShiftHeld) Zoom(GameInput.ScrollStep);
    }

    // Gira alrededor del punto que mira: "agarrar el mundo" (arrastrar a la derecha gira la arena a la derecha)
    public void Orbit(float yawDegrees, float pitchDegrees)
    {
        freeYaw += yawDegrees;
        freePitch = Mathf.Clamp(freePitch + pitchDegrees, pitchLimits.x, pitchLimits.y);
    }

    // Mueve el punto que mira sobre el suelo, en el plano de la pantalla (x = derecha, y = adelante)
    public void Pan(Vector2 amount)
    {
        float yaw = freeYaw * Mathf.Deg2Rad;
        Vector3 right = new Vector3(-Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
        Vector3 forward = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
        focus += right * amount.x + forward * amount.y;
        float limit = (Arena.Instance ? Arena.Instance.Radius : 27f) + 4f;
        Vector3 center = Arena.Instance ? Arena.Instance.Center : Vector3.zero;
        Vector3 off = focus - center;
        off.y = 0f;
        if (off.magnitude > limit) focus = center + off.normalized * limit;
    }

    public void Zoom(int steps)
    {
        if (steps != 0) freeDistance = Mathf.Clamp(freeDistance * Mathf.Pow(0.87f, steps), distanceLimits.x, distanceLimits.y);
    }

    private Vector3 FreePosition()
    {
        float yaw = freeYaw * Mathf.Deg2Rad, pitch = freePitch * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
        Vector3 pos = focus + dir * freeDistance;
        pos.y = Mathf.Max(pos.y, 1.5f);   // nunca debajo del suelo
        return pos;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TogglePressed() => Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
    private static bool TogglePressed() => Input.GetKeyDown(KeyCode.C);
#endif
}
