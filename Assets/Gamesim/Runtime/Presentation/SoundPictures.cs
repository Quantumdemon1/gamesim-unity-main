using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>What a sound is for: news the player is told, the interface answering a hand, or a motion the player sees.</summary>
    public enum SoundKind
    {
        /// <summary>Something happened in the game - a nomination, a ballot, a hit. It must never be the only way the player learns it.</summary>
        Information,
        /// <summary>The interface answering a press, a hover, a panel, a step of the tour: it repeats what the screen already shows.</summary>
        Interface,
        /// <summary>
        /// A motion the player sees in the house - the front door's leaves giving and landing shut. It
        /// is the sound of what is on screen, so it carries no news of its own, and it plays under the
        /// effects' volume as the house's air does.
        /// </summary>
        World,
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
    /// the volume it plays under, its place in the mix (<see cref="Row.GainDb"/>), and the visible change that carries the same news - so a player
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
            /// <summary>
            /// The cue's place in the mix, in decibels against its recording: 0 for the game's news,
            /// which plays as recorded, and below it for the interface's answers, so a menu never
            /// sounds as big as a nomination; a motion the player sees (<see cref="SoundKind.World"/>)
            /// sits below the news too. Every recording peaks at the same level, which put the
            /// panels' swish level with the eviction and the hover's tick over the ballot's.
            /// </summary>
            public float GainDb { get; }
            /// <summary>The least time between two soundings of the cue, in seconds: 0 for none. A sweep across a list asks once a row.</summary>
            public float Spacing { get; }

            public Row(string cue, SoundKind kind, SoundChannel channel, string picture, float gainDb = 0f, float spacing = 0f)
            {
                Cue = cue; Kind = kind; Channel = channel; Picture = picture; GainDb = gainDb; Spacing = spacing;
            }

            public override string ToString() => Cue + " (" + Kind + ", " + Channel + "): " + Picture;
        }

        private static readonly Row[] rows =
        {
            new Row("Button", SoundKind.Interface, SoundChannel.Interface,
                "The control pressed takes its pressed state, and what it does is drawn: the panel it opens, the status line it sets.", gainDb: -4f),
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
                "The eviction card and the board's result name who leaves, and the status line says it; at the final eviction, the final two's card names who joins the jury."),
            new Row("Finale", SoundKind.Information, SoundChannel.Effects,
                "The jury's last vote is read and the winner is named on the card."),
            new Row("PanelOpen", SoundKind.Interface, SoundChannel.Interface, "The panel arriving.", gainDb: -11f),
            new Row("PanelClose", SoundKind.Interface, SoundChannel.Interface, "The panel leaving.", gainDb: -11f),
            new Row("Hover", SoundKind.Interface, SoundChannel.Interface, "The control under the pointer lights.", gainDb: -12f, spacing: HoverSpacing),
            new Row("TutorialStep", SoundKind.Interface, SoundChannel.Interface, "The tour's next step: its title and its words.", gainDb: -9f),
            // The front door (AU-06): the opening's entrances and the evicted's walk out. Sounds of a
            // motion on screen, under the news; appended, as the cues are.
            new Row("DoorOpen", SoundKind.World, SoundChannel.Effects,
                "The front door's leaves give after their rattle and swing open, and the light beyond comes up.", gainDb: DoorGainDb),
            new Row("DoorShut", SoundKind.World, SoundChannel.Effects,
                "The front door's leaves land shut, and the light beyond dies away.", gainDb: DoorGainDb),
            // The house called to a staged ceremony (AU-07).
            new Row("Summons", SoundKind.Information, SoundChannel.Effects,
                "The strip comes up with the ceremony's headline and the summons line: the house gathers at the table, or takes its seats."),
        };

        /// <summary>
        /// The front door's place in the mix, in decibels: under the news, so a door is never as big
        /// as the eviction it follows, and over the interface's answers, because it is the house.
        /// </summary>
        public const float DoorGainDb = -5f;

        /// <summary>
        /// The least time between two hover ticks, in seconds: a pointer swept down a list enters a
        /// row a frame, and a tick a row chattered. Every hover is still asked for and counted; the
        /// ones inside this time of the last that sounded are not heard.
        /// </summary>
        public const float HoverSpacing = 0.06f;

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

        /// <summary>The factor a cue's recording is played at for its place in the mix: its row's gain as a share of the whole, 1 for a cue with no row.</summary>
        public static float GainOf(string cue) => (float)Math.Pow(10.0, (Find(cue)?.GainDb ?? 0f) / 20.0);

        /// <summary>The least time between two soundings of a cue, in seconds: its row's, 0 for a cue with no row.</summary>
        public static float SpacingOf(string cue) => Find(cue)?.Spacing ?? 0f;

        /// <summary>
        /// Whether a cue asked for at <paramref name="now"/> sounds, when it last sounded at
        /// <paramref name="last"/> (NaN for never) and keeps <paramref name="spacing"/> seconds
        /// between soundings. A clock that went back - a new session's - sounds too.
        /// </summary>
        public static bool Sounds(float now, float last, float spacing) =>
            spacing <= 0f || float.IsNaN(last) || now < last || now - last >= spacing;

        // ---------------------------------------------------------------- the volumes beside the master

        /// <summary>The preferences the three volumes are kept under, as percentages of the master.</summary>
        public const string MusicVolumeKey = "Gamesim.MusicVolume", EffectsVolumeKey = "Gamesim.EffectsVolume", InterfaceVolumeKey = "Gamesim.InterfaceVolume";

        /// <summary>Each volume starts at the whole of the master, so a player who never touches them hears what they always heard.</summary>
        public const int DefaultVolume = 100;

        /// <summary>A step of the volume controls, as the master's.</summary>
        public const int Step = 10;

        /// <summary>The name of a channel's slider in Settings: "Music volume" (its value reads "70%").</summary>
        public static string MeterCaption(SoundChannel channel) => ChannelName(channel) + " volume";

        /// <summary>The channel's word in the log: "music", "effects", "interface".</summary>
        public static string LogKey(SoundChannel channel) => channel == SoundChannel.Music ? "music-volume" : channel == SoundChannel.Effects ? "effects-volume" : "interface-volume";

        public static string ChannelName(SoundChannel channel) => channel == SoundChannel.Music ? "Music" : channel == SoundChannel.Effects ? "Effects" : "Interface";

        /// <summary>A volume a step down or up, inside 0 to 100.</summary>
        public static int Stepped(int percent, bool up) => Math.Max(0, Math.Min(100, percent + (up ? Step : -Step)));

        /// <summary>A kept volume read back: inside 0 to 100, the whole when it is not a number.</summary>
        public static int Clamp(int percent) => Math.Max(0, Math.Min(100, percent));
    }
}
