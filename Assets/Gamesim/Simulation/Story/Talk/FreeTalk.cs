using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>Why no free talk is on offer with a housemate right now, or <see cref="None"/> when one is.</summary>
    public enum TalkRefusal
    {
        /// <summary>A talk is on offer.</summary>
        None,
        /// <summary>The season plays no free talk (talk rules 0, or the story's lore is off): no tile at all.</summary>
        Off,
        /// <summary>The player has left the house.</summary>
        PlayerOut,
        /// <summary>The housemate has left the house, or is not a housemate.</summary>
        NotInHouse,
        /// <summary>No window is open (a competition, a ceremony, eviction night).</summary>
        NoWindow,
        /// <summary>The player has had this window's talk with them (one counted talk per housemate per window, ruling R5).</summary>
        TalkedThisWindow,
        /// <summary>No talk in the tables is due for them (only before the tables land: "the usual" never runs dry, ruling R9).</summary>
        NothingToSay,
    }

    /// <summary>What the "Just talk" tile offers for one housemate now: the talk, or why there is none.</summary>
    public sealed class TalkOffer
    {
        public TalkRefusal Refusal;

        /// <summary>The talk on offer, or null.</summary>
        public TalkRow Talk;

        /// <summary>The talk is a spent "the usual" coming round again: it gains the small gain (ruling R9).</summary>
        public bool Repeat;

        /// <summary>The player-facing reason when refused (a subtitle for the tile), or "" when on offer.</summary>
        public string Reason = "";

        public bool Available => Refusal == TalkRefusal.None && Talk != null;
    }

    /// <summary>
    /// What one free talk does: the second and third beats the screen shows after the player's reply. Computed by
    /// <see cref="FreeTalk.Result"/> on the state the command is submitted against; the engine applies exactly this.
    /// </summary>
    public sealed class TalkResult
    {
        public string NpcId, TalkId, Tone;

        /// <summary>How the reply landed: +1, 0 or -1.</summary>
        public int Landing;

        /// <summary>The player's line, as said.</summary>
        public string Reply;

        /// <summary>Their reaction at that landing, tokens filled.</summary>
        public string Reaction;

        /// <summary>The outcome line (<see cref="FreeTalk.Landed"/>, <see cref="FreeTalk.NothingMuchMoved"/>, <see cref="FreeTalk.WrongNote"/>).</summary>
        public string Outcome;

        /// <summary>The fact learned, its sentence and its layer (1 to 3), or null, null and 0 when nothing was learned.</summary>
        public string FactId, Fact;
        public int FactLayer;

        /// <summary>A standing talk's line, filled with this week's names, or null; and the candour it was said in.</summary>
        public string StandingLine, Stance;

        /// <summary>The free warmth applied from the player to them: +1, 0 or -1 (their side moves 0.8 to 1.2 of it).</summary>
        public int Warmth;

        /// <summary>Rapport tenths gained, and the rapport before and after (integer and tenths).</summary>
        public int Tenths, RapportBefore, TenthsBefore, RapportAfter, TenthsAfter;

        /// <summary>A spent "the usual" said again, with the small gain (ruling R9).</summary>
        public bool Repeat;
    }

    /// <summary>
    /// The free "Just talk" (slice S1, talk rules 1): the queries the screen and the notebook read, all pure. The command is
    /// <see cref="EpisodeCommandKind.JustTalk"/> (<see cref="CommandText"/>), resolved by the engine exactly as <see cref="Result"/>
    /// says. Nothing here draws from the season's stream; at talk rules 0 nothing is read beyond the level itself.
    /// </summary>
    public static class FreeTalk
    {
        // ------------------------------------------------------------------ the outcome lines (spec 3.2)

        public const string Landed = "That landed.", NothingMuchMoved = "Fine. Nothing much moved.", WrongNote = "Wrong note. They'll remember that.";

        // ------------------------------------------------------------------ refusals (the tile's subtitle; the command's reason)

        public const string OffReason = "Free talk is not part of this season.";
        public const string PlayerOutReason = "Evicted players can follow the season but cannot influence it.";
        public const string NotInHouseReason = "Choose a housemate who is still in the house.";
        public const string NoWindowReason = "Nobody is free to talk right now.";
        public const string TalkedReason = "Free. You talked this window.";
        public const string NothingReason = "Nothing to talk about right now.";
        public const string NotOnOfferReason = "That talk is not on offer.";
        public const string ToneReason = "Pick one of the replies on offer.";

        // ------------------------------------------------------------------ the numbers (ruling R5, spec 3.4 and 3.7)

        /// <summary>A positive free gain is cut to 0 when either side's score is this or more (R5).</summary>
        public const double WarmthStop = 25;

        /// <summary>Rapport tenths by landing (+1, 0, -1), before the listener's class (spec 3.4).</summary>
        public const int TenthsLands = 8, TenthsEven = 5, TenthsGrates = 2;

        /// <summary>The layer gates (spec 3.7, D13): background at rapport 3; what drives them at 6 with their view of you 15.</summary>
        public const int BackgroundRapport = 3, DrivesRapport = 6, DrivesView = 15;

        /// <summary>Where they stand (layer 4, the Friend numbers): rapport 6 (Guarded 8), their view of you 35, from week 2.</summary>
        public const int StandingRapport = 6, StandingRapportGuarded = 8, StandingView = 35, StandingWeek = 2;

        /// <summary>The kinds' default priorities (spec 3.13): layer and standing 70, a windowed rhythm talk 30, rhythm 20.</summary>
        public const int LayerPriority = 70, WindowPriority = 30, RhythmPriority = 20;

        // ------------------------------------------------------------------ the queries

        /// <summary>Whether the season plays free talk: talk rules 1 or later and the story's lore on.</summary>
        public static bool On(EpisodeState s) =>
            TalkRules.At(s, TalkRules.Talk) && EpisodeEngine.StoryAt(s, StoryRules.Lore);

        /// <summary>
        /// The window key the cap is counted in (ruling R5): 1 on move-in night, otherwise week * 8 + window + 2; 0 while no window
        /// is open.
        /// </summary>
        public static int WindowKey(EpisodeState s)
        {
            if (s == null) return 0;
            if (EpisodeEngine.IsFirstNight(s)) return 1;
            int window = EpisodeEngine.Window(s);
            return window == Windows.None ? 0 : s.week * 8 + window + 2;
        }

        /// <summary>What the tile offers for a housemate now, or why nothing.</summary>
        public static TalkOffer Offer(EpisodeState s, string npcId) =>
            new TalkOffer { Refusal = TalkRefusal.Off, Reason = OffReason };

        /// <summary>The command's text: <c>talkId|tone</c>, the target the housemate.</summary>
        public static string CommandText(string talkId, string tone) => (talkId ?? "") + "|" + (tone ?? "");

        /// <summary>Reads a command's text back into its talk id and tone.</summary>
        public static bool TryParse(string text, out string talkId, out string tone)
        {
            talkId = null; tone = null;
            if (string.IsNullOrEmpty(text)) return false;
            int bar = text.IndexOf('|');
            if (bar <= 0 || bar != text.LastIndexOf('|') || bar == text.Length - 1) return false;
            talkId = text.Substring(0, bar);
            tone = text.Substring(bar + 1);
            return true;
        }

        /// <summary>
        /// What answering the talk on offer in <paramref name="tone"/> does, on the state the command is submitted against: null when
        /// the command would be refused. The engine applies exactly this result.
        /// </summary>
        public static TalkResult Result(EpisodeState s, string npcId, string talkId, string tone) => null;

        /// <summary>The deepest of layers 1 to 3 open with a housemate (spec 3.7); an All-Star's stops at 2 (ruling R7). 0 when off.</summary>
        public static int OpenLayer(EpisodeState s, string npcId) => 0;

        /// <summary>Whether layer 4, where they stand, is open with a housemate (spec 3.7).</summary>
        public static bool StandingOpen(EpisodeState s, string npcId) => false;

        /// <summary>The pool a housemate's talks come from: <see cref="TalkPools.Star"/> for any All-Star template, else their class.</summary>
        public static string PoolOf(ContestantState who) => TalkPools.Steady;

        /// <summary>A housemate's temperament class from their six axes (spec 3.5): Spark, Steady or Guarded.</summary>
        public static string ClassOf(ContestantState who) => TalkPools.Steady;

        /// <summary>Whether a card was built from an All-Stars template, whatever it is now named (ruling R7).</summary>
        public static bool IsAllStar(ContestantState who) =>
            StoryPeople.TemplateOf(who)?.Roster == CastTemplates.Roster.AllStars;

        /// <summary>A talk's priority: its own, or its kind's default.</summary>
        public static int PriorityOf(TalkRow talk) =>
            talk == null ? 0 : talk.Priority > 0 ? talk.Priority
                : talk.Kind != TalkKind.Rhythm ? LayerPriority : talk.When?.Window != null ? WindowPriority : RhythmPriority;
    }
}
