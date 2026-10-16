using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón "gel": crece un poco al pasar el mouse (o al seleccionarlo con teclado), se hunde al presionar y suena.
public class AeroButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
                            IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressScale = 0.96f;
    [SerializeField] private float speed = 14f;

    private bool hover, selected, pressed;
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (selectable is Button b) b.onClick.AddListener(() => AeroAudio.Play(Sfx.Click));
    }

    private void OnDisable()
    {
        hover = selected = pressed = false;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        bool interactable = selectable == null || selectable.IsInteractable();
        float target = !interactable ? 1f : pressed ? pressScale : (hover || selected) ? hoverScale : 1f;
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hover = true;
        if (selectable == null || selectable.IsInteractable()) AeroAudio.Play(Sfx.Hover, 0.6f);
    }

    public void OnPointerExit(PointerEventData e) { hover = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;
}
