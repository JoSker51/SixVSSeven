using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if !ENABLE_INPUT_SYSTEM
// Sin el Input System nuevo usamos este enum con los mismos nombres, así los scripts no cambian
public enum Key { A, C, D, E, F, Q, R, S, T, W, X, Z, Space, Enter, Escape, Tab, PageUp, PageDown }
#endif

// Lectura de teclado y mouse en un solo lugar (el proyecto usa el Input System nuevo).
// Los scripts que la usan agregan: #if ENABLE_INPUT_SYSTEM using UnityEngine.InputSystem; #endif
public static class GameInput
{
    // Esc cancela primero lo más cercano (colocar un objeto, la tienda); si alguien lo usó, el HUD no pausa
    private static int escapeConsumedFrame = -1;
    public static void ConsumeEscape() => escapeConsumedFrame = Time.frameCount;
    public static bool EscapeConsumed => escapeConsumedFrame == Time.frameCount;

    // Clic derecho: arrastrar gira la cámara; soltarlo sin arrastrar es un clic "corto" (cancelar)
    private const float DragThreshold = 8f;   // píxeles
    private static int trackedFrame = -1;
    private static Vector2 rightDown;
    private static bool rightDragged;

    private static void TrackRight()
    {
        if (trackedFrame == Time.frameCount) return;
        trackedFrame = Time.frameCount;
        if (RightClick) { rightDown = MousePosition; rightDragged = false; }
        else if (RightHeld && (MousePosition - rightDown).sqrMagnitude > DragThreshold * DragThreshold) rightDragged = true;
    }

    public static bool RightDragging { get { TrackRight(); return RightHeld && rightDragged; } }
    public static bool RightTap { get { TrackRight(); return RightReleased && !rightDragged; } }

#if ENABLE_INPUT_SYSTEM
    public static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
    public static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    public static bool LeftClick => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static bool RightClick => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    public static bool RightHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;
    public static bool RightReleased => Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
    public static bool MiddleHeld => Mouse.current != null && Mouse.current.middleButton.isPressed;
    public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    public static bool ShiftHeld => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

    // -1, 0 o +1 por cada "clic" de la ruedita
    public static int ScrollStep
    {
        get
        {
            if (Mouse.current == null) return 0;
            float y = Mouse.current.scroll.ReadValue().y;
            return y > 0.01f ? 1 : y < -0.01f ? -1 : 0;
        }
    }
#else
    public static bool Held(Key key) => Input.GetKey(ToKeyCode(key));
    public static bool Pressed(Key key) => Input.GetKeyDown(ToKeyCode(key));
    public static Vector2 MousePosition => Input.mousePosition;
    public static bool LeftClick => Input.GetMouseButtonDown(0);
    public static bool RightClick => Input.GetMouseButtonDown(1);
    public static bool RightHeld => Input.GetMouseButton(1);
    public static bool RightReleased => Input.GetMouseButtonUp(1);
    public static bool MiddleHeld => Input.GetMouseButton(2);
    public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 20f;
    public static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    public static int ScrollStep => Input.mouseScrollDelta.y > 0.01f ? 1 : Input.mouseScrollDelta.y < -0.01f ? -1 : 0;
    private static KeyCode ToKeyCode(Key k) => k == Key.Enter ? KeyCode.Return : (KeyCode)System.Enum.Parse(typeof(KeyCode), k.ToString());
#endif
}
