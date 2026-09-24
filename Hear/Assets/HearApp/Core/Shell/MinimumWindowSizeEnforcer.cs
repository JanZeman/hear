using System.Runtime.InteropServices;
using UnityEngine;

namespace HearApp.Core.Shell
{
    // Unity's standalone Player Settings has no native minimum-window-size field. Per
    // docs/05, continuous resizing must remain usable down to a defined minimum practical
    // size - below that, the shell's responsive layout (even the Compact bottom-nav mode) no
    // longer has room for its content.
    //
    // On macOS this calls into a tiny native plugin (Plugins/macOS) that sets the real
    // NSWindow.contentMinSize, so the OS itself refuses to drag past it with no repositioning
    // side effect. Snapping the resolution back up after the fact (the naive
    // Screen.SetResolution approach) visibly moved the window, since Unity does not control which
    // corner/edge the OS anchors a resize to - the native constraint avoids that entirely by
    // never letting the drag go below the limit in the first place.
    //
    // Windows/Linux standalone still use the SetResolution snap-back as a fallback, since no
    // equivalent native plugin exists for them yet.
    public sealed class MinimumWindowSizeEnforcer : MonoBehaviour
    {
        [SerializeField] private int minWidth = 320;
        [SerializeField] private int minHeight = 460;

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("HearAppNativeWindow")]
        private static extern void HearApp_SetMinWindowSize(float width, float height);
#endif

        private void Start()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            HearApp_SetMinWindowSize(minWidth, minHeight);
#endif
        }

        private void Update()
        {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
            if (Screen.fullScreen) return;

            int width = Screen.width;
            int height = Screen.height;
            if (width < minWidth || height < minHeight)
                Screen.SetResolution(Mathf.Max(width, minWidth), Mathf.Max(height, minHeight), FullScreenMode.Windowed);
#endif
        }
    }
}
