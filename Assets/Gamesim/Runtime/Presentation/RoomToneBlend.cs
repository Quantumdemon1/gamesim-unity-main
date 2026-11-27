using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The house's two bed voices, as numbers: which bed each carries, how loud it is now and where
    /// it is going. A room crossed is a crossfade rather than a cut - the old bed falls and the new
    /// one rises over <see cref="CrossfadeSeconds"/> at a constant rate - and the generic air is a bed
    /// like any room's, so leaving a room for a corridor fades too.
    ///
    /// <para>A voice keeps its bed after it has faded out, so a quick return to the room it just
    /// left fades the same voice back in, and a turn back in the middle of a fade carries on from the
    /// level it had reached. A third bed asked for while two are still sounding takes the quieter
    /// voice. Where a bed is in its loop belongs to <see cref="HouseAudio"/>, which keeps each bed's
    /// place for its return.</para>
    ///
    /// <para>Arithmetic only, with nothing of Unity's in it, so Tools/SimulationTests pins it.
    /// Presentation: nothing here reads or writes the season.</para>
    /// </summary>
    public sealed class RoomToneBlend
    {
        /// <summary>Seconds a bed takes from silence to full, and from full to silence.</summary>
        public const float CrossfadeSeconds = 0.75f;

        /// <summary>
        /// Seconds the director keeps the last room's bed when the camera's focus is in no room -
        /// a doorway, a gap between two rooms' floors - before it hands over to the generic air.
        /// Long enough that walking through a doorway is one crossfade and not two.
        /// </summary>
        public const float FallbackHoldSeconds = 0.5f;

        /// <summary>The generic air's key: the bed between rooms and in a room without a bed of its own.</summary>
        public const string Air = "";

        /// <summary>How many beds can sound at once: the one going and the one coming.</summary>
        public const int Voices = 2;

        /// <summary>A room's bed at full level, as a share of the effects volume.</summary>
        public const float RoomPeak = 0.7f;

        /// <summary>The generic air at full level, as a share of the effects volume.</summary>
        public const float AirPeak = 1f;

        private readonly string[] keys = new string[Voices];
        private readonly float[] levels = new float[Voices];
        private readonly float[] targets = new float[Voices];

        /// <summary>The voice that carries the bed asked for last, or -1 before any.</summary>
        public int Current { get; private set; } = -1;

        /// <summary>The bed a voice carries: a room's name, <see cref="Air"/>, or null for none yet.</summary>
        public string KeyOf(int voice) => keys[voice];

        /// <summary>How loud a voice is now, 0 to 1 of its bed's full level.</summary>
        public float LevelOf(int voice) => levels[voice];

        /// <summary>Where a voice is going: 1 for the bed asked for, 0 for the others.</summary>
        public float TargetOf(int voice) => targets[voice];

        /// <summary>Whether a voice should be sounding: it carries a bed that is audible or on its way up.</summary>
        public bool Wanted(int voice) => keys[voice] != null && (levels[voice] > 0f || targets[voice] > 0f);

        /// <summary>How loud the bed with <paramref name="key"/> (null is the air) is now; 0 when no voice carries it.</summary>
        public float LevelOf(string key)
        {
            int voice = VoiceOf(key ?? Air);
            return voice < 0 ? 0f : levels[voice];
        }

        /// <summary>A bed's gain at full level: <see cref="AirPeak"/> for the air, <see cref="RoomPeak"/> for a room.</summary>
        public static float PeakOf(string key) => string.IsNullOrEmpty(key) ? AirPeak : RoomPeak;

        /// <summary>
        /// The bed with <paramref name="key"/> asked for (null is the air): it rises to full and every
        /// other voice falls to silence. Returns the voice that carries it. <paramref name="given"/>
        /// is true when that voice was handed the bed now - its clip is to be loaded, and whatever it
        /// carried before is gone - and false when it carried the bed already: asked again, turned
        /// back to mid-fade, or faded out and kept for a return.
        /// </summary>
        public int Enter(string key, out bool given)
        {
            key = key ?? Air;
            int voice = VoiceOf(key);
            given = voice < 0;
            if (given)
            {
                voice = Quieter();
                keys[voice] = key;
                levels[voice] = 0f;
            }
            Current = voice;
            for (int i = 0; i < Voices; i++) targets[i] = i == voice ? 1f : 0f;
            return voice;
        }

        /// <summary>Every voice moved toward its target for <paramref name="seconds"/>, at the crossfade's constant rate.</summary>
        public void Tick(float seconds)
        {
            for (int i = 0; i < Voices; i++) levels[i] = MusicFade.Step(levels[i], targets[i], CrossfadeSeconds, seconds);
        }

        /// <summary>
        /// Every voice silent at once: the sound muted, reduced or switched off. The bed asked for is
        /// still the one asked for, and comes back in from silence; the one that was going is let go.
        /// </summary>
        public void Silence()
        {
            for (int i = 0; i < Voices; i++)
            {
                levels[i] = 0f;
                if (i != Current) targets[i] = 0f;
            }
        }

        /// <summary>
        /// The room the director gives the audio: the room its query <paramref name="found"/>, or -
        /// when it found none - the <paramref name="last"/> room until the miss has lasted
        /// <see cref="FallbackHoldSeconds"/>, and then none, which is the generic air.
        /// <paramref name="missedSince"/> is the clock reading the miss began at, NaN while the
        /// query finds a room; the caller keeps it between calls.
        /// </summary>
        public static string HeldRoom(string found, string last, float now, ref float missedSince)
        {
            if (found != null || last == null)
            {
                missedSince = float.NaN;
                return found;
            }
            if (float.IsNaN(missedSince)) missedSince = now;
            return now - missedSince >= FallbackHoldSeconds ? null : last;
        }

        private int VoiceOf(string key)
        {
            if (key == null) return -1;
            for (int i = 0; i < Voices; i++)
                if (string.Equals(keys[i], key, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>The voice a new bed takes: the quieter, and at a tie the one not asked for last.</summary>
        private int Quieter()
        {
            if (Current < 0) return 0;
            int other = Current == 0 ? 1 : 0;
            return levels[Current] < levels[other] ? Current : other;
        }
    }
}
