using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Lets the PlayMode suite run in <c>-batchmode</c>.
    ///
    /// Several tests drive real input by adding a synthetic Mouse or Keyboard and queueing state
    /// events. The Input System's default <see cref="InputSettings.BackgroundBehavior"/> is
    /// <c>ResetAndDisableNonBackgroundDevices</c>, and a batchmode editor never holds focus, so those
    /// devices are disabled before the queued events are read and the input silently never arrives.
    /// Seven tests — click-to-move, camera orbit and zoom, the Escape key, keyboard diary travel —
    /// failed that way, and the failures look exactly like product bugs.
    ///
    /// This only relaxes the focus rules, which is meaningless for a run that has no window to lose
    /// focus to. It is scoped three ways: it lives in the test assembly, which is never part of a
    /// player build; it is gated on <see cref="Application.isBatchMode"/>, so an interactive editor
    /// session is untouched; and it changes nothing about how the game itself treats input.
    /// </summary>
    internal static class HeadlessInputSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DeliverInputWhileUnfocused()
        {
            if (!Application.isBatchMode) return;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
    }
}
