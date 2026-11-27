using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A screen whose controls sound as the house's HUD does (AU-10): the director hands it the
    /// house's control sounds once it is attached, the way the tour is handed its step sound.
    /// </summary>
    public interface ISoundedScreen
    {
        /// <summary>The house's control sounds, or null while none has been handed over (a bare test).</summary>
        ControlSounds Sounds { get; }
    }

    /// <summary>
    /// The sounds of a screen's controls (AU-10): the hover tick when the pointer arrives over a
    /// control that can be pressed, and the button's click for a press that asked for no sound of
    /// its own (<see cref="PressAnswer"/>). The main menu, the cast screen, the creator and the
    /// season report had neither: every press there was silent, while the house's HUD ticked.
    ///
    /// <para>Both are the interface's cues, on the Interface volume, halved under reduced sound and
    /// silent when the sound is off, as the HUD's are. A greyed-out control makes no sound. The
    /// screen is found from the control when the pointer arrives or the press runs, so a control
    /// made by a static helper needs no reference to its screen.</para>
    /// </summary>
    public sealed class ControlSounds
    {
        private readonly Action<HouseAudio.Cue> play;
        private readonly Func<int> asked;

        /// <param name="play">Asks the house for a cue.</param>
        /// <param name="asked">How many cues the house has asked for so far (<see cref="HouseAudio.CuesAsked"/>).</param>
        public ControlSounds(Action<HouseAudio.Cue> play, Func<int> asked)
        {
            this.play = play;
            this.asked = asked;
        }

        /// <summary>The pointer arrived over a control that can be pressed.</summary>
        public void Hover() => play?.Invoke(HouseAudio.Cue.Hover);

        /// <summary>Runs a press, and clicks when it asked for no sound of its own.</summary>
        public void Press(Action action) => PressAnswer.Run(action, asked, () => play?.Invoke(HouseAudio.Cue.Button));

        /// <summary>The sounds of the screen <paramref name="control"/> is drawn on, or null.</summary>
        public static ControlSounds Of(Component control)
        {
            if (control == null) return null;
            var screen = control.GetComponentInParent<ISoundedScreen>(true);
            return screen != null ? screen.Sounds : null;
        }

        /// <summary>Whether <paramref name="control"/> can be pressed now: shown, and not greyed out, nor its group.</summary>
        public static bool CanPress(GameObject control)
        {
            if (control == null) return false;
            var selectable = control.GetComponent<Selectable>();
            return selectable == null || (selectable.IsActive() && selectable.IsInteractable());
        }

        /// <summary>Gives <paramref name="control"/> its screen's hover tick, heard only while it can be pressed.</summary>
        public static void Tick(Selectable control)
        {
            if (control == null) return;
            if (control.GetComponent<ControlTick>() == null) control.gameObject.AddComponent<ControlTick>();
        }

        /// <summary>
        /// Runs a press of <paramref name="control"/>: answered as its screen's presses are. On a screen
        /// with no sounds, or on a control that cannot be pressed now, it runs as it always did, silent.
        /// </summary>
        public static void Press(Component control, Action action)
        {
            var sounds = control != null && CanPress(control.gameObject) ? Of(control) : null;
            if (sounds != null) sounds.Press(action);
            else action?.Invoke();
        }

        /// <summary>
        /// A way out drawn on a sounded screen (<see cref="IWayOutHost.Dress"/>): its tick, and a press
        /// answered as the screen's other controls are. No action, no press.
        /// </summary>
        public static Action Dress(Button control, Action action)
        {
            Tick(control);
            if (action == null) return null;
            return () => Press(control, action);
        }
    }

    /// <summary>The hover tick of a control on a sounded screen (<see cref="ControlSounds.Tick"/>).</summary>
    [DisallowMultipleComponent]
    public sealed class ControlTick : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!ControlSounds.CanPress(gameObject)) return;
            var sounds = ControlSounds.Of(this);
            if (sounds != null) sounds.Hover();
        }
    }
}
