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

#if ENABLE_INPUT_SYSTEM
    public static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
    public static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    public static bool LeftClick => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static bool RightClick => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

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
    public static int ScrollStep => Input.mouseScrollDelta.y > 0.01f ? 1 : Input.mouseScrollDelta.y < -0.01f ? -1 : 0;
    private static KeyCode ToKeyCode(Key k) => k == Key.Enter ? KeyCode.Return : (KeyCode)System.Enum.Parse(typeof(KeyCode), k.ToString());
#endif
}
