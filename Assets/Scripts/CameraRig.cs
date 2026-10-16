using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Cámara de la demo: se coloca detrás de la catapulta activa y sigue a la roca en vuelo.
// C alterna con una vista general de todo el mapa.
public class CameraRig : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 11f, -7.5f);  // relativo a la catapulta
    [SerializeField] private float lookAhead = 12f;
    [SerializeField, Range(0f, 1f)] private float followRock = 0.35f;              // cuánto avanza la cámara con la roca
    [SerializeField] private Vector3 overviewPosition = new Vector3(34f, 30f, 0f);
    [SerializeField] private Vector3 overviewLookAt = Vector3.zero;
    [SerializeField] private float smoothing = 3.5f;

    private bool overview;
    private Vector3 lookPoint;

    private void Start()
    {
        lookPoint = transform.position + transform.forward * 20f;
    }

    private void LateUpdate()
    {
        if (TogglePressed()) overview = !overview;

        Vector3 targetPos = overviewPosition;
        Vector3 targetLook = overviewLookAt;

        var gm = GameManager.Instance;
        bool gameOver = gm != null && gm.GameOver;
        var cat = turnManager ? turnManager.Current : null;

        if (!overview && !gameOver && cat != null)
        {
            Transform t = cat.transform;
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

        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, k);
        lookPoint = Vector3.Lerp(lookPoint, targetLook, Mathf.Clamp01(k * 1.5f));
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TogglePressed() => Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
    private static bool TogglePressed() => Input.GetKeyDown(KeyCode.C);
#endif
}
