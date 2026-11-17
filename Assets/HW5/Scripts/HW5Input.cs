using UnityEngine;

/// <summary>
/// 同時支援舊版 Input Manager 與新版 Input System 的按鍵判斷。
/// Unity 6 新專案預設可能只開新版 Input System，直接呼叫 Input.GetKeyDown 會報錯。
/// </summary>
public static class HW5Input
{
    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            case KeyCode.F12: return kb.f12Key.wasPressedThisFrame;
            case KeyCode.P: return kb.pKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(key);
#endif
    }
}
