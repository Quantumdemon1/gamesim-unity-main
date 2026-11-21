using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What a free talk is (the free "Just talk" feature, slice S1, ruling R1): its kind decides what the engine does with it.
    /// A <see cref="Layer"/> talk frames a fact the player learns, a <see cref="Standing"/> talk is the weekly "where they stand"
    /// (layer 4), and a <see cref="Rhythm"/> talk is house life that reveals nothing.
    /// </summary>
    public enum TalkKind
    {
        Layer,
        Rhythm,
        Standing,

        /// <summary>
        /// A friendship stage talk (slice S2, talk rules 2): said once, the first time its <see cref="TalkRow.Stage"/> holds with
        /// the housemate, ahead of every other talk (<see cref="TalkFriendship"/>). Never offered below talk rules 2, and never as
        /// an ordinary talk. The Close friend talk asks a favour: its replies carry <see cref="TalkReply.Favour"/>.
        /// </summary>
        Stage,
    }

    /// <summary>
    /// One free talk, as a row of a C# table (ruling R1: content lives in code, no text format, no loader). The tables supply text
    /// and gates only: what a talk gains, reveals or costs is the engine's (<see cref="FreeTalk"/>), from the tone, the housemate and
    /// the state. A spent id the tables no longer hold is ignored.
    ///
    /// <para>Text is the source's, verbatim: a stage direction in parentheses, the spoken words in double quotes
    /// (<c>(folding towels) "Ask me something."</c>). <see cref="TalkText"/> fills its tokens and splits it for the screen.</para>
    /// </summary>
    public sealed class TalkRow
    {
        /// <summary>Unique across every table, 1 to 64 characters, no colon (it is saved in <c>contact.spentTalks</c>).</summary>
        public string Id;

        /// <summary>The class pool that says it (<see cref="TalkPools"/>), or null for a person's own talk (<see cref="Owner"/>).</summary>
        public string Pool;

        /// <summary>The cast template id whose own talk this is (a later slice's person tables), or null for a pool talk.</summary>
        public string Owner;

        public TalkKind Kind;

        /// <summary>The layer it serves: 1 to 3 for a layer frame, 4 for a standing talk, 0 for rhythm.</summary>
        public int Layer;

        /// <summary>When it may be said: every gate must hold. Never null.</summary>
        public TalkGate When = new TalkGate();

        /// <summary>The source's <c>priority:</c>, or 0 for the kind's default (<see cref="FreeTalk.PriorityOf"/>).</summary>
        public int Priority;

        /// <summary>
        /// A "the usual" talk (the source's <c># usual</c> line): once every talk the housemate has is spent, it may come round again
        /// with the small gain (ruling R9). Repeating never adds a second entry to <c>contact.spentTalks</c>.
        /// </summary>
        public bool Usual;

        /// <summary>The source's <c>reveal: next</c>: a layer frame that leads to the next fact within its layer.</summary>
        public bool Reveals;

        /// <summary>Their opening line (the source's <c>open:</c>).</summary>
        public string Open;

        /// <summary>The player's replies (the source's <c>a</c> lines), each in its own tone; three in slice 1.</summary>
        public TalkReply[] Replies = Array.Empty<TalkReply>();

        /// <summary>A standing talk's three forms (the source's <c>standing:</c> lines); null on every other kind.</summary>
        public TalkStandingLines Standing;

        /// <summary>
        /// A <see cref="TalkKind.Stage"/> talk's stage: <see cref="FriendStage.Acquainted"/>, <see cref="FriendStage.Friendly"/>,
        /// <see cref="FriendStage.Friend"/> or <see cref="FriendStage.CloseFriend"/>. <see cref="FriendStage.Stranger"/> (the default)
        /// on every other kind; a stage talk holding any other value is never offered.
        /// </summary>
        public FriendStage Stage;
    }

    /// <summary>
    /// A talk's gates, the slice-1 subset of the source's <c>when:</c> vocabulary (spec 5.3). The later slices' gates (stage, flirt,
    /// romance, event, callback, flag, room) are added here by their slices; nothing in slice 1 reads them.
    /// </summary>
    public sealed class TalkGate
    {
        /// <summary><c>first</c>: no counted talk with them yet.</summary>
        public bool First;

        /// <summary><c>layer&gt;=N</c>: the layer open with them is N or more (<see cref="FreeTalk.OpenLayer"/>); 0 for no gate.</summary>
        public int Layer;

        /// <summary><c>week&gt;=N</c>: the season's week is N or later; 0 for no gate.</summary>
        public int Week;

        /// <summary><c>window=...</c>: one of <see cref="TalkWindows"/>, or null for any window.</summary>
        public string Window;
    }

    /// <summary>One reply the player can give, and their reaction to it at each landing.</summary>
    public sealed class TalkReply
    {
        /// <summary>The tone, one of <see cref="TalkRules.Tones"/> (never <see cref="TalkRules.Flirt"/> in slice 1). <see cref="TalkTone.FromSource"/> maps the source's word.</summary>
        public string Tone;

        /// <summary>What the player says (the source's <c>a &lt;tone&gt; | ...</c> text).</summary>
        public string Line;

        /// <summary>Their reaction when it lands (+1, the source's <c>+</c>), when it is neutral (0, <c>=</c>) and when it grates (-1, <c>-</c>).</summary>
        public string Lands, Even, Grates;

        /// <summary>The reply keeps the fact back: whatever the landing, a layer talk answered this way teaches nothing.</summary>
        public bool HoldsBack;

        /// <summary>
        /// On the Close friend stage talk only (slice S2): the reply answers their favour, <see cref="TalkFavour.Keep"/> ("I've got
        /// you", +2 warmth both ways as one act) or <see cref="TalkFavour.Refuse"/> ("Can't promise that", -2). <see cref="TalkFavour.None"/>
        /// (the default) everywhere else, and ignored on every other talk.
        /// </summary>
        public TalkFavour Favour;
    }

    /// <summary>What a reply to the Close friend stage talk does with their favour (slice S2, ruling T6).</summary>
    public enum TalkFavour
    {
        /// <summary>Not a favour reply.</summary>
        None,
        /// <summary>"I've got you": +2 warmth both ways, one act, not stopped at 25.</summary>
        Keep,
        /// <summary>"Can't promise that": -2 warmth both ways.</summary>
        Refuse,
    }

    /// <summary>
    /// A standing talk's three forms (layer 4): the engine picks one by the housemate's candour this week and fills
    /// <c>{close}</c>, <c>{worry}</c> and <c>{safe}</c>; in the dishonest form <c>{close}</c> and <c>{worry}</c> are its decoys.
    /// </summary>
    public sealed class TalkStandingLines
    {
        public string Honest, Neutral, Dishonest;
    }

    /// <summary>The class pools a talk can belong to (spec 3.5): three temperaments, and the All-Stars' own game-register pool.</summary>
    public static class TalkPools
    {
        public const string Spark = "spark", Steady = "steady", Guarded = "guarded", Star = "star";
        public static readonly string[] All = { Spark, Steady, Guarded, Star };
        public static bool IsKnown(string pool) => pool != null && Array.IndexOf(All, pool) >= 0;
    }

    /// <summary>The window names a gate may hold (the source's <c>window=</c>).</summary>
    public static class TalkWindows
    {
        public const string AfterHoH = "after-hoh", AfterNoms = "after-noms", AfterVeto = "after-veto", AfterEviction = "after-eviction", MoveIn = "move-in";
        public static readonly string[] All = { AfterHoH, AfterNoms, AfterVeto, AfterEviction, MoveIn };
        public static bool IsKnown(string window) => window != null && Array.IndexOf(All, window) >= 0;
    }

    /// <summary>
    /// The tones on screen (<see cref="TalkRules.Tones"/>, the saved ids) and the approach each one is judged as
    /// (<see cref="Personality.Approach"/>, spec 3.2). The source files name tones by approach; <see cref="FromSource"/> maps them.
    /// </summary>
    public static class TalkTone
    {
        /// <summary>The approach a tone is received as: Warm warm, Straight candid, Tease playful, Push bold, Careful calculated, Let it go yield, Call it out hardball.</summary>
        public static string Approach(string tone)
        {
            switch (tone)
            {
                case TalkRules.Warm: return Personality.Approach.Warm;
                case TalkRules.Straight: return Personality.Approach.Candid;
                case TalkRules.Tease: return Personality.Approach.Playful;
                case TalkRules.Push: return Personality.Approach.Bold;
                case TalkRules.Careful: return Personality.Approach.Calculated;
                case TalkRules.LetGo: return Personality.Approach.Yield;
                case TalkRules.CallOut: return Personality.Approach.Hardball;
                case TalkRules.Flirt: return Personality.Approach.Playful;
                default: return null;
            }
        }

        /// <summary>The saved tone for a source file's tone word (warm, candid, playful, bold, calculated, yield, hardball, flirt), or null.</summary>
        public static string FromSource(string word)
        {
            switch (word)
            {
                case "warm": return TalkRules.Warm;
                case "candid": return TalkRules.Straight;
                case "playful": return TalkRules.Tease;
                case "bold": return TalkRules.Push;
                case "calculated": return TalkRules.Careful;
                case "yield": return TalkRules.LetGo;
                case "hardball": return TalkRules.CallOut;
                case "flirt": return TalkRules.Flirt;
                default: return null;
            }
        }

        /// <summary>The tone's caption on a reply button (spec 3.2), American spelling.</summary>
        public static string Caption(string tone)
        {
            switch (tone)
            {
                case TalkRules.Warm: return "Warm";
                case TalkRules.Straight: return "Straight";
                case TalkRules.Tease: return "Tease";
                case TalkRules.Push: return "Push";
                case TalkRules.Careful: return "Careful";
                case TalkRules.LetGo: return "Let it go";
                case TalkRules.CallOut: return "Call it out";
                case TalkRules.Flirt: return "Flirt";
                default: return "";
            }
        }
    }
}
