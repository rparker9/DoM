using UnityEngine;

public enum K { W, A, S, D, Q, E, Z, C, Up, Down, Left, Right, Space, R, Shift, Ctrl, F, H, T, M, Alpha1, Alpha2, Alpha3, Alpha4, F1, F2, F3, F4, Tab }

/// <summary>Works with either the legacy Input Manager or the new Input System package.</summary>
public static class KeyInput
{
#if ENABLE_LEGACY_INPUT_MANAGER
    static KeyCode Map(K k)
    {
        switch (k)
        {
            case K.W: return KeyCode.W; case K.A: return KeyCode.A; case K.S: return KeyCode.S; case K.D: return KeyCode.D;
            case K.Q: return KeyCode.Q; case K.E: return KeyCode.E; case K.Z: return KeyCode.Z; case K.C: return KeyCode.C;
            case K.Up: return KeyCode.UpArrow; case K.Down: return KeyCode.DownArrow;
            case K.Left: return KeyCode.LeftArrow; case K.Right: return KeyCode.RightArrow;
            case K.Space: return KeyCode.Space; case K.R: return KeyCode.R;
            case K.Shift: return KeyCode.LeftShift; case K.Ctrl: return KeyCode.LeftControl;
            case K.F: return KeyCode.F; case K.H: return KeyCode.H; case K.T: return KeyCode.T; case K.M: return KeyCode.M;
            case K.Alpha1: return KeyCode.Alpha1; case K.Alpha2: return KeyCode.Alpha2;
            case K.Alpha3: return KeyCode.Alpha3; case K.Alpha4: return KeyCode.Alpha4;
            case K.F1: return KeyCode.F1; case K.F2: return KeyCode.F2;
            case K.F3: return KeyCode.F3; case K.F4: return KeyCode.F4; default: return KeyCode.Tab;
        }
    }
    public static bool Held(K k) => Input.GetKey(Map(k));
    public static bool Down(K k) => Input.GetKeyDown(Map(k));
#elif ENABLE_INPUT_SYSTEM
    static UnityEngine.InputSystem.Key Map(K k)
    {
        switch (k)
        {
            case K.W: return UnityEngine.InputSystem.Key.W; case K.A: return UnityEngine.InputSystem.Key.A;
            case K.S: return UnityEngine.InputSystem.Key.S; case K.D: return UnityEngine.InputSystem.Key.D;
            case K.Q: return UnityEngine.InputSystem.Key.Q; case K.E: return UnityEngine.InputSystem.Key.E;
            case K.Z: return UnityEngine.InputSystem.Key.Z; case K.C: return UnityEngine.InputSystem.Key.C;
            case K.Up: return UnityEngine.InputSystem.Key.UpArrow; case K.Down: return UnityEngine.InputSystem.Key.DownArrow;
            case K.Left: return UnityEngine.InputSystem.Key.LeftArrow; case K.Right: return UnityEngine.InputSystem.Key.RightArrow;
            case K.Space: return UnityEngine.InputSystem.Key.Space; case K.R: return UnityEngine.InputSystem.Key.R;
            case K.Shift: return UnityEngine.InputSystem.Key.LeftShift; case K.Ctrl: return UnityEngine.InputSystem.Key.LeftCtrl;
            case K.F: return UnityEngine.InputSystem.Key.F; case K.H: return UnityEngine.InputSystem.Key.H;
            case K.T: return UnityEngine.InputSystem.Key.T; case K.M: return UnityEngine.InputSystem.Key.M;
            case K.Alpha1: return UnityEngine.InputSystem.Key.Digit1; case K.Alpha2: return UnityEngine.InputSystem.Key.Digit2;
            case K.Alpha3: return UnityEngine.InputSystem.Key.Digit3; case K.Alpha4: return UnityEngine.InputSystem.Key.Digit4;
            case K.F1: return UnityEngine.InputSystem.Key.F1; case K.F2: return UnityEngine.InputSystem.Key.F2;
            case K.F3: return UnityEngine.InputSystem.Key.F3; case K.F4: return UnityEngine.InputSystem.Key.F4;
            default: return UnityEngine.InputSystem.Key.Tab;
        }
    }
    public static bool Held(K k)
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb[Map(k)].isPressed;
    }
    public static bool Down(K k)
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb[Map(k)].wasPressedThisFrame;
    }
#else
    public static bool Held(K k) => false;
    public static bool Down(K k) => false;
#endif
}
