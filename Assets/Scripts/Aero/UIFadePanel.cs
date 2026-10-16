using UnityEngine;

// Panel que aparece/desaparece con fundido y un pequeño zoom. Funciona aunque el juego esté en pausa.
[RequireComponent(typeof(CanvasGroup))]
public class UIFadePanel : MonoBehaviour
{
    [SerializeField] private bool startVisible = true;
    [SerializeField] private float duration = 0.18f;
    [SerializeField] private float hiddenScale = 0.94f;

    private CanvasGroup group;
    private bool visible;

    public bool Visible => visible;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        visible = startVisible;
        group.alpha = visible ? 1f : 0f;
        transform.localScale = Vector3.one * (visible ? 1f : hiddenScale);
        ApplyInteraction();
    }

    public void Show() => SetVisible(true);
    public void Hide() => SetVisible(false);

    public void SetVisible(bool value)
    {
        if (group == null) Awake();
        visible = value;
        ApplyInteraction();
    }

    private void ApplyInteraction()
    {
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, step);
        float s = Mathf.Lerp(hiddenScale, 1f, Mathf.SmoothStep(0f, 1f, group.alpha));
        transform.localScale = new Vector3(s, s, 1f);
    }
}
