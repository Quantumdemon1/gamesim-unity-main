using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The sounds of what the player is doing in the house: a knife on a board at the counter, a
    /// sizzle at the hob, strokes in the pool, the hot tub's bubbles, a soft snore in bed and a beat
    /// to dance to. One row for each activity that makes a sound, by the activity's name
    /// (<c>HouseFurnitureActivity</c>, since this table is Unity-free); sitting at the table and
    /// resting on a lounger have none and stay quiet. Each loop lives in
    /// <c>Resources/Audio/Activities/&lt;Activity&gt;</c>, rendered by <c>ArtSource/audio/bb_foley.py</c>.
    ///
    /// <para>The loops play under <see cref="SoundChannel.Effects"/>, like the rooms' beds, and are
    /// beds rather than news: the body doing the activity is the picture. Reduce sound and mute take
    /// them away; reduced motion does not, because a sound is not motion.</para>
    ///
    /// <para>Presentation only: nothing here reads or writes the season, and nothing draws from its
    /// random stream.</para>
    /// </summary>
    public static class ActivitySounds
    {
        /// <summary>Where an activity's loop lives: <c>Resources/Audio/Activities/&lt;Activity&gt;</c>.</summary>
        public const string ResourceFolder = "Audio/Activities/";

        /// <summary>Seconds a loop takes from silence to full as the activity gets under way.</summary>
        public const float FadeIn = 0.4f;

        /// <summary>
        /// Seconds a loop takes from full to silence as the player starts to get up: the time the
        /// music's duck takes to come back (<see cref="MusicFade.DuckOut"/>), so the dance's beat
        /// hands back to the season's music without a gap or a bump.
        /// </summary>
        public const float FadeOut = 0.6f;

        /// <summary>One activity's loop.</summary>
        public sealed class Row
        {
            /// <summary>The activity's name, which is also its loop's file name.</summary>
            public string Activity { get; }
            /// <summary>The loop's place in the mix, in decibels against its recording: 0 plays it as rendered.</summary>
            public float GainDb { get; }
            /// <summary>Whether the loop is music, and the season's music steps back under it (<see cref="MusicFade.ActivityMusicDuck"/>).</summary>
            public bool DucksMusic { get; }

            public Row(string activity, float gainDb = 0f, bool ducksMusic = false)
            {
                Activity = activity; GainDb = gainDb; DucksMusic = ducksMusic;
            }

            public override string ToString() => Activity + (DucksMusic ? " (music)" : "");
        }

        private static readonly Row[] rows =
        {
            new Row("PrepareSnack"),
            new Row("Sleep"),
            new Row("Swim"),
            new Row("Soak"),
            new Row("Cook"),
            new Row("Dance", ducksMusic: true),
        };

        /// <summary>Every row, in the activities' order.</summary>
        public static IReadOnlyList<Row> Rows => rows;

        /// <summary>The row for an activity's name, or null for one that makes no sound (and for none).</summary>
        public static Row Find(string activity)
        {
            if (activity == null) return null;
            foreach (var row in rows)
                if (string.Equals(row.Activity, activity, StringComparison.Ordinal)) return row;
            return null;
        }

        /// <summary>The loop an activity plays, by name: the activity's own name, or null for one that makes no sound.</summary>
        public static string LoopOf(string activity) => Find(activity)?.Activity;

        /// <summary>The factor a loop is played at for its place in the mix: its row's gain as a share of the whole, 1 for none.</summary>
        public static float GainOf(string activity) => (float)Math.Pow(10.0, (Find(activity)?.GainDb ?? 0f) / 20.0);

        /// <summary>Whether an activity's loop is music the season's music steps back for.</summary>
        public static bool DucksMusic(string activity) => Find(activity)?.DucksMusic ?? false;
    }

    /// <summary>
    /// The house's one activity voice, as numbers: the loop it carries, how loud it is now and the
    /// loop asked for. One loop at a time: a loop asked for rises from silence over
    /// <see cref="ActivitySounds.FadeIn"/>; asked for nothing it falls over
    /// <see cref="ActivitySounds.FadeOut"/> and is let go; asked for another while one still sounds,
    /// the one sounding falls all the way first and the next comes in after it. Asked for the loop it
    /// is letting go, it turns round where it is. Arithmetic only, on <see cref="MusicFade.Step"/>, so
    /// Tools/SimulationTests pins it; where the loop is in its file belongs to <c>HouseAudio</c>.
    /// </summary>
    public sealed class ActivityLoop
    {
        /// <summary>The loop asked for, by its activity's name, or null for none.</summary>
        public string Asked { get; private set; }

        /// <summary>The loop the voice carries now, or null for none.</summary>
        public string Key { get; private set; }

        /// <summary>How loud the voice is now, 0 to 1 of its loop's full level.</summary>
        public float Level { get; private set; }

        /// <summary>Whether the voice should be sounding: it carries a loop that is audible or on its way up.</summary>
        public bool Sounding => Key != null && (Level > 0f || Key == Asked);

        /// <summary>Asks for <paramref name="loop"/>, or for silence with null. A silent voice takes the loop at once; asking again changes nothing.</summary>
        public void Ask(string loop)
        {
            Asked = loop;
            if (Level <= 0f) Key = loop;
        }

        /// <summary>Moves the voice on by <paramref name="seconds"/>: up toward the loop asked for, or down and out of the one it carries.</summary>
        public void Tick(float seconds)
        {
            if (Key == Asked)
            {
                if (Key != null) Level = MusicFade.Step(Level, 1f, ActivitySounds.FadeIn, seconds);
                return;
            }
            Level = MusicFade.Step(Level, 0f, ActivitySounds.FadeOut, seconds);
            // The old loop is gone: the voice takes the one asked for, which comes in from silence.
            if (Level <= 0f) Key = Asked;
        }

        /// <summary>Silence at once, the loop asked for kept: what the preferences do when they take the sound away. It comes back in from silence.</summary>
        public void Silence()
        {
            Level = 0f;
            Key = Asked;
        }
    }
}
