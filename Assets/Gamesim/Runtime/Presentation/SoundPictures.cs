using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>What a sound is for: news the player is told, or the interface answering a hand.</summary>
    public enum SoundKind
    {
        /// <summary>Something happened in the game - a nomination, a ballot, a hit. It must never be the only way the player learns it.</summary>
        Information,
        /// <summary>The interface answering a press, a hover, a panel, a step of the tour: it repeats what the screen already shows.</summary>
        Interface,
    }

    /// <summary>The volume a sound is played under, beside the master (PLAN A, A11).</summary>
    public enum SoundChannel
    {
        Music,
        Effects,
        Interface,
    }

    /// <summary>
    /// Every sound has a picture (PLAN A, A11): one row for each of the house's cues
    /// (<c>HouseAudio.Cue</c>, by name, since this table is Unity-free), saying what the cue is for,
    /// the volume it plays under, and the visible change that carries the same news - so a player
    /// with the sound off, or who cannot hear it, is told everything a hearing player is. The editor
    /// holds the table to the enum, one row a cue; the episode's PlayMode suite holds every
    /// information cue to a change of the text on screen in the frame it sounds.
    ///
    /// <para>The music and the house's air have no row: they are beds, not news. The music plays
    /// under <see cref="SoundChannel.Music"/>; the room tone under <see cref="SoundChannel.Effects"/>.</para>
    /// </summary>
    public static class SoundPictures
    {
        /// <summary>One cue and its picture.</summary>
        public sealed class Row
        {
            public string Cue { get; }
            public SoundKind Kind { get; }
            public SoundChannel Channel { get; }
            /// <summary>The visible change that says what the sound says.</summary>
            public string Picture { get; }

            public Row(string cue, SoundKind kind, SoundChannel channel, string picture)
            {
                Cue = cue; Kind = kind; Channel = channel; Picture = picture;
            }

            public override string ToString() => Cue + " (" + Kind + ", " + Channel + "): " + Picture;
        }

        private static readonly Row[] rows =
        {
            new Row("Button", SoundKind.Interface, SoundChannel.Interface,
                "The control pressed takes its pressed state, and what it does is drawn: the panel it opens, the status line it sets."),
            new Row("Save", SoundKind.Information, SoundChannel.Effects,
                "A key handed over in the key ceremony: the key's card turns over with the houseguest's name on it."),
            new Row("SocialUp", SoundKind.Information, SoundChannel.Effects,
                "A hit, a match or a right answer: the board's count, its verdict line or the house challenge's attempts move."),
            new Row("SocialDown", SoundKind.Information, SoundChannel.Effects,
                "A miss, a wrong flip, a wrong answer or a target let go: the board's count, its verdict line or the attempts move."),
            new Row("CompetitionStart", SoundKind.Information, SoundChannel.Effects,
                "The competition's board or the house challenge's panel comes up with its name and its rules."),
            new Row("CompetitionWin", SoundKind.Information, SoundChannel.Effects,
                "The result card names the winner, and the status line says what was won."),
            new Row("Nomination", SoundKind.Information, SoundChannel.Effects,
                "The ceremony card and the status line name the nominees; in the key ceremony, the last two left without a key."),
            new Row("Veto", SoundKind.Information, SoundChannel.Effects,
                "The veto meeting's card says whether the veto was used and on whom, and the status line says it once."),
            new Row("Vote", SoundKind.Information, SoundChannel.Effects,
                "A ballot is read: the board shows whose vote it is and the tally moves; at the finale, a juror's vote."),
            new Row("Eviction", SoundKind.Information, SoundChannel.Effects,
                "The eviction card and the board's result name who leaves, and the status line says it."),
            new Row("Finale", SoundKind.Information, SoundChannel.Effects,
                "The jury's last vote is read and the winner is named on the card."),
            new Row("PanelOpen", SoundKind.Interface, SoundChannel.Interface, "The panel arriving."),
            new Row("PanelClose", SoundKind.Interface, SoundChannel.Interface, "The panel leaving."),
            new Row("Hover", SoundKind.Interface, SoundChannel.Interface, "The control under the pointer lights."),
            new Row("TutorialStep", SoundKind.Interface, SoundChannel.Interface, "The tour's next step: its title and its words."),
        };

        /// <summary>Every row, in the enum's order.</summary>
        public static IReadOnlyList<Row> Rows => rows;

        /// <summary>The row for a cue's name, or null.</summary>
        public static Row Find(string cue)
        {
            foreach (var row in rows)
                if (string.Equals(row.Cue, cue, StringComparison.Ordinal)) return row;
            return null;
        }

        /// <summary>The channel a cue plays under: its row's, or the effects' for a cue with no row.</summary>
        public static SoundChannel ChannelOf(string cue) => Find(cue)?.Channel ?? SoundChannel.Effects;

        /// <summary>Whether a cue carries news: its row says information, or it has no row (and so is treated as news until it has one).</summary>
        public static bool IsInformation(string cue) => (Find(cue)?.Kind ?? SoundKind.Information) == SoundKind.Information;

        // ---------------------------------------------------------------- the volumes beside the master

        /// <summary>The preferences the three volumes are kept under, as percentages of the master.</summary>
        public const string MusicVolumeKey = "Gamesim.MusicVolume", EffectsVolumeKey = "Gamesim.EffectsVolume", InterfaceVolumeKey = "Gamesim.InterfaceVolume";

        /// <summary>Each volume starts at the whole of the master, so a player who never touches them hears what they always heard.</summary>
        public const int DefaultVolume = 100;

        /// <summary>A step of the volume controls, as the master's.</summary>
        public const int Step = 10;

        /// <summary>The settings' words for a channel's meter and its two controls.</summary>
        public static string MeterCaption(SoundChannel channel) => ChannelName(channel) + " volume";
        public static string DownCaption(SoundChannel channel) => ChannelName(channel) + " volume down";
        public static string UpCaption(SoundChannel channel) => ChannelName(channel) + " volume up";

        /// <summary>The channel's word in the log: "music", "effects", "interface".</summary>
        public static string LogKey(SoundChannel channel) => channel == SoundChannel.Music ? "music-volume" : channel == SoundChannel.Effects ? "effects-volume" : "interface-volume";

        public static string ChannelName(SoundChannel channel) => channel == SoundChannel.Music ? "Music" : channel == SoundChannel.Effects ? "Effects" : "Interface";

        /// <summary>A volume a step down or up, inside 0 to 100.</summary>
        public static int Stepped(int percent, bool up) => Math.Max(0, Math.Min(100, percent + (up ? Step : -Step)));

        /// <summary>A kept volume read back: inside 0 to 100, the whole when it is not a number.</summary>
        public static int Clamp(int percent) => Math.Max(0, Math.Min(100, percent));
    }
}
