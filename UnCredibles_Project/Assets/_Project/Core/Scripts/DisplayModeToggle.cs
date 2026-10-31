using UnityEngine;
using UnityEngine.InputSystem;

namespace UnCredibles.Core
{
    // F11 switches between full screen and a window. The game is made for 16:9, so both keep it:
    // full screen on a monitor with another shape shows black bars, and the window is a 16:9 share of the monitor.
    public sealed class DisplayModeToggle : MonoBehaviour
    {
        private const float Aspect = 16f / 9f;
        // Size of the window against the monitor.
        private const float WindowShare = .75f;

        private void Start() => Apply(Screen.fullScreenMode != FullScreenMode.Windowed);

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
                Apply(Screen.fullScreenMode == FullScreenMode.Windowed);
        }

        private static void Apply(bool fullScreen)
        {
            // The editor's Game view has its own size.
            if (Application.isEditor) return;
            var display = Screen.mainWindowDisplayInfo;
            float share = fullScreen ? 1f : WindowShare;
            float width = display.width * share;
            float height = display.height * share;
            if (width / height > Aspect) width = height * Aspect;
            else height = width / Aspect;
            Screen.SetResolution(Mathf.RoundToInt(width), Mathf.RoundToInt(height),
                fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
