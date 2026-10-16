using UnityEngine;

// Cámara del menú: se mece despacio alrededor del castillo
public class MenuCameraSway : MonoBehaviour
{
    [SerializeField] private Vector3 lookAt = new Vector3(0f, 4.2f, -19f);
    [SerializeField] private float angle = 9f;       // grados hacia cada lado
    [SerializeField] private float speed = 0.05f;    // ciclos por segundo
    [SerializeField] private float bob = 0.35f;

    private Vector3 offset;

    private void Awake() => offset = transform.position - lookAt;

    private void LateUpdate()
    {
        float t = Time.unscaledTime * speed * 2f * Mathf.PI;
        Vector3 rotated = Quaternion.Euler(0f, Mathf.Sin(t) * angle, 0f) * offset;
        transform.position = lookAt + rotated + Vector3.up * (Mathf.Sin(t * 2.3f) * bob);
        transform.LookAt(lookAt);
    }
}
