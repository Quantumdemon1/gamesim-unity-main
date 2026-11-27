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
        /// A week-event talk (slice S5, talk rules 5): what just happened in the house this week, said while its
        /// <see cref="TalkGate.Event"/> holds (<see cref="TalkEvents"/>), at priority 100 (<see cref="FreeTalk.EventPriority"/>), and
        /// spent once. Otherwise an ordinary talk: tones, layer 0, nothing revealed. Never offered below talk rules 5
        /// (<see cref="TalkRow.FromRules"/>). Placed before <see cref="Stage"/> so every check that reads "<c>Kind &lt;= Stage</c>"
        /// as "not a romance kind" reads it so; no kind's number is saved anywhere.
        /// </summary>
        Event,

        /// <summary>
        /// A friendship stage talk (slice S2, talk rules 2): said once, the first time its <see cref="TalkRow.Stage"/> holds with
        /// the housemate, ahead of every other talk (<see cref="TalkFriendship"/>). Never offered below talk rules 2, and never as
        /// an ordinary talk. The Close friend talk asks a favour: its replies carry <see cref="TalkReply.Favour"/>.
        /// </summary>
        Stage,

        // ------------------------------------------------------------------ talk rules 3 (slice S3): romance. Never offered below
        // talk rules 3, never as an ordinary talk (FreeTalk.Offer leaves every kind below out), and only from the romance tables:
        // a class pool's (Pool spark, steady or guarded) or an All-Star template's own (Owner). Never spent in contact.spentTalks.

        /// <summary>
        /// The player's Flirt answer: <see cref="TalkRow.Replies"/> holds ONE reply in the tone <see cref="TalkRules.Flirt"/>, its
        /// <c>Line</c> what the player says and its <c>Lands</c>/<c>Even</c>/<c>Grates</c> their answer in kind when interested. The
        /// engine adds it, as a fourth reply, to the talk on offer when Flirt is open (<see cref="TalkRomance.FlirtOpen"/>). No
        /// <see cref="TalkRow.Open"/> is read.
        /// </summary>
        FlirtLine,

        /// <summary>They flirt first (priority 60): a Spark housemate, interested, only after the player's own first Flirt (R8).
        /// Replies: tones (a Flirt reply among them counts as the player's Flirt), and the choice <see cref="TalkChoice.JustFriends"/>.</summary>
        Opening,

        /// <summary>Their kind no to a Flirt (scene): "I like you. Not like that." Only <see cref="TalkRow.Open"/>; no replies.</summary>
        SoftNo,

        /// <summary>An undecided housemate's first answer to a Flirt (scene): they hold back. Only <see cref="TalkRow.Open"/>.</summary>
        HoldBack,

        /// <summary>A closed regular's answer to a Flirt (scene): they draw the line, kindly. Only <see cref="TalkRow.Open"/>.</summary>
        LineDrawn,

        /// <summary>
        /// A here-to-play housemate's answer to a Flirt (scene), by <see cref="TalkRow.Attempt"/>: 1 the full scene
        /// (<see cref="TalkRow.Open"/> their reaction, <see cref="TalkRow.Second"/> their "I'm here to play", and three choice replies
        /// <see cref="TalkChoice.LaughItOff"/>, <see cref="TalkChoice.PushAgain"/>, <see cref="TalkChoice.Apologize"/>, each with its
        /// player line in <c>Line</c> and their answer in <c>Lands</c>); 2 the short line; 3 the final short line. Attempts 2 and 3
        /// hold only <see cref="TalkRow.Open"/>.
        /// </summary>
        TurnAside,

        /// <summary>
        /// They declare (priority 95): <see cref="TalkRow.Open"/>, then the choice replies <see cref="TalkChoice.Accept"/>,
        /// <see cref="TalkChoice.NotYet"/>, <see cref="TalkChoice.NotLikeThat"/> and <see cref="TalkChoice.JustFriends"/>, each with
        /// the player's line and their answer in <c>Lands</c>.
        /// </summary>
        Declaration,

        /// <summary>Their yes when the player makes it official (scene): only <see cref="TalkRow.Open"/>.</summary>
        Accepts,

        /// <summary>Their "not like that" when the player makes it official and they are not interested (scene): only <see cref="TalkRow.Open"/>.</summary>
        Refuses,

        /// <summary>A couple's talk (priority 60, while the two are a couple): tones, and the choice <see cref="TalkChoice.EndItKindly"/>.</summary>
        Couple,

        /// <summary>The ex talk (priority 60, for two weeks after it ends): tones only.</summary>
        Ex,

        /// <summary>The jealousy talk (priority 100): the one who heard the player flirted elsewhere. Tones, and
        /// <see cref="TalkChoice.EndItKindly"/> when the two are a couple.</summary>
        Jealousy,

        /// <summary>A third housemate's reaction to the player's couple (priority 40): <see cref="TalkRow.Reaction"/> 1 ("You two
        /// aren't subtle.") or 2 ("Careful. A pair is a target."). Tones only. About the pair in the game, nothing else.</summary>
        Reaction,
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
        /// The talk level a season must hold for the row to be offered at all (<see cref="TalkRules.At"/>), or 0 for every level
        /// free talk plays. A row written for a later level carries that level, so the talk on offer at every earlier level stays
        /// exactly as it was (slice S5's rows carry <see cref="TalkRules.ComesToTalk"/>).
        /// </summary>
        public int FromRules;

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

        // ------------------------------------------------------------------ talk rules 3 (slice S3); defaults on every other kind

        /// <summary>A <see cref="TalkKind.TurnAside"/> row's attempt: 1, 2 or 3. 0 on every other kind.</summary>
        public int Attempt;

        /// <summary>A <see cref="TalkKind.Reaction"/> row's reaction: 1 ("You two aren't subtle.") or 2 ("Careful. A pair is a target."). 0 otherwise.</summary>
        public int Reaction;

        /// <summary>The second beat of a <see cref="TalkKind.TurnAside"/> attempt 1: their "I'm here to play", in their own voice. Null otherwise.</summary>
        public string Second;
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

        /// <summary>
        /// <c>spark&gt;=N</c> (talk rules 3, romance rows only): the player's spark with them is N or more; 0 for no gate. A romance
        /// row may also use <see cref="Week"/> and <see cref="Window"/>; <see cref="First"/> and <see cref="Layer"/> are never read on one.
        /// </summary>
        public int Spark;

        /// <summary>
        /// <c>event=&lt;kind&gt;</c> (talk rules 5): one of <see cref="TalkEvents.All"/>, which must hold this week for the talk with
        /// this housemate (<see cref="TalkEvents.Holds"/>, read from the season's committed state); null for no gate. Never holds
        /// below talk rules 5.
        /// </summary>
        public string Event;
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

        /// <summary>
        /// On a romance row only (talk rules 3): the reply is a CHOICE, not a tone (<see cref="Tone"/> null): its <c>Line</c> the
        /// player's words and its <c>Lands</c> their answer. <see cref="TalkChoice.None"/> (the default) on a tone reply. What a
        /// choice does is the engine's (<see cref="TalkRomance"/>), never the row's.
        /// </summary>
        public TalkChoice Choice;
    }

    /// <summary>
    /// The continuation choices of a romance talk or scene (slice S3, talk rules 3). Which are on offer is the engine's
    /// (<see cref="TalkRomance.ChoicesNow"/>); a row only gives their words. Sent as <c>talkId|choice:&lt;id&gt;</c>
    /// (<see cref="TalkChoices.Answer"/>).
    /// </summary>
    public enum TalkChoice
    {
        None = 0,
        /// <summary>The player's own declaration, once ready (in any romance talk or scene).</summary>
        MakeItOfficial,
        /// <summary>Yes to their declaration: a couple.</summary>
        Accept,
        /// <summary>Not yet to their declaration: nothing lost; asked again a week later, at most twice (R8).</summary>
        NotYet,
        /// <summary>No to their declaration: romance with them closed three weeks.</summary>
        NotLikeThat,
        /// <summary>Free, in every romance talk: romance with them closed for the season at no cost, until the player's own later Flirt (R8).</summary>
        JustFriends,
        /// <summary>In a couple's talk: it ends kindly.</summary>
        EndItKindly,
        /// <summary>A here-to-play turn-aside, attempt 1: laugh it off.</summary>
        LaughItOff,
        /// <summary>A here-to-play turn-aside, attempt 1: push again.</summary>
        PushAgain,
        /// <summary>A here-to-play turn-aside, attempt 1: apologize.</summary>
        Apologize,
    }

    /// <summary>The choices' command ids and captions (American spelling).</summary>
    public static class TalkChoices
    {
        /// <summary>The answer prefix of a choice in a command's text: <c>talkId|choice:accept</c>.</summary>
        public const string Prefix = "choice:";

        public static string Id(TalkChoice choice)
        {
            switch (choice)
            {
                case TalkChoice.MakeItOfficial: return "official";
                case TalkChoice.Accept: return "accept";
                case TalkChoice.NotYet: return "not-yet";
                case TalkChoice.NotLikeThat: return "not-like-that";
                case TalkChoice.JustFriends: return "just-friends";
                case TalkChoice.EndItKindly: return "end-it";
                case TalkChoice.LaughItOff: return "laugh";
                case TalkChoice.PushAgain: return "push-again";
                case TalkChoice.Apologize: return "apologize";
                default: return null;
            }
        }

        /// <summary>The choice an id names, or <see cref="TalkChoice.None"/>.</summary>
        public static TalkChoice FromId(string id)
        {
            foreach (TalkChoice choice in Enum.GetValues(typeof(TalkChoice)))
                if (choice != TalkChoice.None && string.Equals(Id(choice), id, StringComparison.Ordinal)) return choice;
            return TalkChoice.None;
        }

        /// <summary>The answer part of a command's text for a choice: <c>choice:&lt;id&gt;</c>.</summary>
        public static string Answer(TalkChoice choice) => choice == TalkChoice.None ? "" : Prefix + Id(choice);

        /// <summary>The choice an answer part names (<c>choice:&lt;id&gt;</c>), or <see cref="TalkChoice.None"/> for a tone or anything else.</summary>
        public static TalkChoice FromAnswer(string answer) =>
            answer != null && answer.StartsWith(Prefix, StringComparison.Ordinal) ? FromId(answer.Substring(Prefix.Length)) : TalkChoice.None;

        /// <summary>The choice's pill on its button (the reply's own words stay its caption, as a tone's do).</summary>
        public static string Caption(TalkChoice choice)
        {
            switch (choice)
            {
                case TalkChoice.MakeItOfficial: return "Make it official";
                case TalkChoice.Accept: return "Accept";
                case TalkChoice.NotYet: return "Not yet";
                case TalkChoice.NotLikeThat: return "Not like that";
                case TalkChoice.JustFriends: return "Just friends";
                case TalkChoice.EndItKindly: return "End it kindly";
                case TalkChoice.LaughItOff: return "Laugh it off";
                case TalkChoice.PushAgain: return "Push again";
                case TalkChoice.Apologize: return "Apologize";
                default: return "";
            }
        }
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
