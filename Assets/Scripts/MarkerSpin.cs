using UnityEngine;

// Indicador que gira y flota sobre la catapulta activa
public class MarkerSpin : MonoBehaviour
{
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2.5f;

    private Vector3 basePos;

    private void Awake() { basePos = transform.localPosition; }

    private void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }
}
