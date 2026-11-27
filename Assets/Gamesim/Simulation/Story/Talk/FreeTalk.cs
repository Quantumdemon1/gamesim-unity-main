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

        /// <summary>The standing line as the talk screen shows it, in first names (<see cref="TalkText.FirstNames"/>), whatever the saved line holds; or null.</summary>
        public string StandingSaid;

        /// <summary>The free warmth applied from the player to them: +1, 0 or -1 (their side moves 0.8 to 1.2 of it); the favour's +2 or -2 (talk rules 2).</summary>
        public int Warmth;

        /// <summary>Rapport tenths gained, and the rapport before and after (integer and tenths).</summary>
        public int Tenths, RapportBefore, TenthsBefore, RapportAfter, TenthsAfter;

        /// <summary>A spent "the usual" said again, with the small gain (ruling R9).</summary>
        public bool Repeat;

        // ------------------------------------------------------------------ talk rules 2 (slice S2); at their defaults below it

        /// <summary>The stage this talk marks when it is a stage talk (T5): its event row is <see cref="StageEvent"/>. Stranger otherwise.</summary>
        public FriendStage StageReached;

        /// <summary>The stage's event row as written to the season's log (<see cref="TalkFriendship.EventLine"/>), or null.</summary>
        public string StageEvent;

        /// <summary>The Close friend talk's favour as answered (T6): Keep (+2 warmth both ways) or Refuse (-2); None otherwise. <see cref="Warmth"/> holds its ±2.</summary>
        public TalkFavour Favour;

        /// <summary>The chemistry's word when this talk is the one that first shows it (the third counted talk, T1), else null.</summary>
        public string ChemistryShown;

        /// <summary>The player's lean before and after (T4): equal below talk rules 2.</summary>
        public int LeanBefore, LeanAfter;

        /// <summary>The agreement with them after the talk (-10..10): the saved agreement plus <see cref="Landing"/>, clamped.</summary>
        public int AgreementAfter;

        // ------------------------------------------------------------------ talk rules 3 (slice S3); at their defaults below it

        /// <summary>The choice answered (<c>talkId|choice:&lt;id&gt;</c>), or None for a tone.</summary>
        public TalkChoice Choice;

        /// <summary>A continuation: a choice answered on the scene left open this window. Not counted: no cap, no gain, no landing.</summary>
        public bool Continuation;

        /// <summary>The scene a Flirt (or the player's Make it official) played, and its row; None and null otherwise.</summary>
        public RomanceScene Scene;
        public string SceneTalkId;

        /// <summary>Their words in the scene, tokens filled (a turn-aside's beat 1 and beat 2), or the answer a choice row gives; null when none.</summary>
        public string SceneLine, SceneSecond;

        /// <summary>A turn-aside's attempt, 1 to 3; 0 otherwise.</summary>
        public int Attempt;

        /// <summary>The spark with them before and after (0..100).</summary>
        public int SparkBefore, SparkAfter;

        /// <summary>contact.romance before and after ("" for none; <see cref="TalkRules.Romances"/>).</summary>
        public string RomanceBefore = "", RomanceAfter = "";

        /// <summary>The headline of what changed in the romance.</summary>
        public RomanceChange Change;

        /// <summary>The act's number on their view of the player (a choice's, an ending's), beside <see cref="Warmth"/>; 0 when none.</summary>
        public int ActWarmth;

        /// <summary>The choices now open on <see cref="SceneTalkId"/> (contact.talkPending), answered by a continuation command; empty when none.</summary>
        public List<TalkChoice> Choices = new List<TalkChoice>();

        /// <summary>The couple's fact went public with this answer (a witness in the room, U8).</summary>
        public bool WentPublic;

        /// <summary>The event row the answer writes to the season's log (<see cref="TalkRomance.EventKind"/>), or null.</summary>
        public string RomanceEvent;
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

        /// <summary>A week-event talk's priority (spec 3.13, talk rules 5): ahead of every ordinary talk, and of a due stage talk.</summary>
        public const int EventPriority = 100;

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

        /// <summary>
        /// What the tile offers for a housemate now, or why nothing. The talk is the highest-priority one whose gates hold and that
        /// they have not yet spent (ties on the talk's own keyed coin, so adding a talk reshuffles no other); a spent standing talk
        /// comes round again once all of theirs are spent; and once nothing unspent is due, a spent "the usual" comes round again
        /// (ruling R9), its coin keyed to the window so the usuals take turns.
        /// </summary>
        public static TalkOffer Offer(EpisodeState s, string npcId)
        {
            if (!On(s)) return Refused(TalkRefusal.Off, OffReason);
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return Refused(TalkRefusal.PlayerOut, PlayerOutReason);
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active) return Refused(TalkRefusal.NotInHouse, NotInHouseReason);
            int key = WindowKey(s);
            if (key == 0) return Refused(TalkRefusal.NoWindow, NoWindowReason);
            var contact = Contact(s, npcId);
            if (contact != null && contact.talkWindow == key) return Refused(TalkRefusal.TalkedThisWindow, TalkedReason);

            // Talk rules 3 (slice S3): the romance talk due with them, if any; jealousy and their declaration come first of all.
            var romance = TalkRomance.LevelOn(s) ? TalkRomance.DueTalk(s, npcId) : null;
            if (romance != null && PriorityOf(romance) >= TalkRomance.DeclarationPriority)
                return new TalkOffer { Refusal = TalkRefusal.None, Talk = romance };

            // Talk rules 2 (T5): a stage talk that has come due is the talk on offer, ahead of every other; from talk rules 5 a
            // week-event talk due with them goes first (priority 100 against a stage's 60, spec 3.13), and the stage talk stays due.
            var stageTalk = TalkFriendship.DueTalk(s, npcId);
            if (stageTalk != null && !EventDue(s, npc, contact)) return new TalkOffer { Refusal = TalkRefusal.None, Talk = stageTalk };

            var spent = new HashSet<string>((contact?.spentTalks ?? new List<string>()).Select(TalkRules.SpentId), StringComparer.Ordinal);
            bool full = spent.Count >= TalkRules.MostSpentTalks;
            // A stage talk is only ever offered as one (above), never as an ordinary talk, at any level; nor is a romance talk
            // (slice S3), which only TalkRomance.DueTalk offers.
            var rows = TalkBank.Rows.Where(t => t != null && t.Kind != TalkKind.Stage && !TalkRomance.IsRomanceKind(t.Kind)
                && Holds(s, npc, contact, t)).ToList();
            bool standingLeft = rows.Any(t => t.Kind == TalkKind.Standing && !spent.Contains(t.Id));
            TalkRow best = null; bool bestRepeat = false; int bestPriority = int.MinValue; double bestCoin = 2;
            // From talk rules 5 (spec 3.13, "the person file, else the class pool"): at the same priority, a regular's own unspent
            // talk goes ahead of their pool's. Below level 5 no own talk holds, so every offer there is decided as before.
            bool five = TalkRules.At(s, TalkRules.ComesToTalk); bool bestOwn = false;
            void Consider(TalkRow t, bool repeat)
            {
                int priority = PriorityOf(t);
                double coin = StoryRandom.Unit(s, repeat ? "talk:v1:r:" + npcId + ":" + key + ":" + t.Id : "talk:v1:q:" + npcId + ":" + t.Id);
                bool own = five && !repeat && t.Owner != null && !TalkRomance.IsRomanceKind(t.Kind);
                if (priority < bestPriority || priority == bestPriority && (own == bestOwn ? coin >= bestCoin : !own)) return;
                best = t; bestRepeat = repeat; bestOwn = own; bestPriority = priority; bestCoin = coin;
            }
            foreach (var t in rows)
            {
                if (!spent.Contains(t.Id)) { if (!full) Consider(t, false); }
                else if (t.Kind == TalkKind.Standing && !standingLeft) Consider(t, true);
            }
            // A romance talk (talk rules 3) competes by its priority; it is never spent here (TalkRomance.DueTalk keeps its own).
            if (romance != null) Consider(romance, false);
            if (best == null)
                foreach (var t in rows)
                    if (t.Usual && spent.Contains(t.Id)) Consider(t, true);
            if (best == null) return Refused(TalkRefusal.NothingToSay, NothingReason);
            return new TalkOffer { Refusal = TalkRefusal.None, Talk = best, Repeat = bestRepeat };
        }

        private static TalkOffer Refused(TalkRefusal refusal, string reason) => new TalkOffer { Refusal = refusal, Reason = reason };

        /// <summary>
        /// Whether a week-event talk is due with them (talk rules 5): an unspent one whose gates hold, while the spent list has room.
        /// False below level 5, where nothing else is read.
        /// </summary>
        private static bool EventDue(EpisodeState s, ContestantState npc, ContactState contact)
        {
            if (!TalkEvents.On(s)) return false;
            var spent = new HashSet<string>((contact?.spentTalks ?? new List<string>()).Select(TalkRules.SpentId), StringComparer.Ordinal);
            if (spent.Count >= TalkRules.MostSpentTalks) return false;
            return TalkBank.Rows.Any(t => t != null && t.Kind == TalkKind.Event && !spent.Contains(t.Id) && Holds(s, npc, contact, t));
        }

        /// <summary>The player's contact row with a housemate, or null before any talk of either kind.</summary>
        public static ContactState Contact(EpisodeState s, string npcId) =>
            s?.story?.contacts?.FirstOrDefault(c => c != null && c.npcId == npcId);

        /// <summary>
        /// Whether a talk may be said by this housemate now: their pool, a well-formed row, its gates, and a fact or a stance to give.
        /// Its week gate reads through <see cref="TalkChemistry.WeekGate"/> (unchanged below talk rules 2).
        /// </summary>
        internal static bool Holds(EpisodeState s, ContestantState npc, ContactState contact, TalkRow t)
        {
            if (t == null || string.IsNullOrEmpty(t.Id) || t.Id.Length > TalkRules.MostIdLength || t.Id.IndexOf(TalkRules.ToneMark) >= 0
                || string.IsNullOrEmpty(t.Open) || t.Replies == null || t.Replies.Length == 0
                || t.Replies.Any(r => r == null || !TalkRules.IsTone(r.Tone) || r.Tone == TalkRules.Flirt)) return false;
            // A row's own floor (slice S5): never offered below the talk level it was written for, so a row added for a later level
            // leaves the talk on offer at every earlier level as it was.
            if (t.FromRules > 0 && !TalkRules.At(s, t.FromRules)) return false;
            // The pool (ruling R7): an All-Star template only ever draws the star pool or its own table, and never layer 3.
            bool star = IsAllStar(npc);
            string templateId = StoryPeople.TemplateOf(npc)?.Id;
            bool mine = t.Owner != null ? templateId != null && t.Owner == templateId : t.Pool == (star ? TalkPools.Star : ClassOf(npc));
            if (!mine || star && (t.Layer == 3 || t.When?.Layer == 3)) return false;
            // The gates (spec 5.3, the slice-1 subset).
            var when = t.When ?? new TalkGate();
            int talks = contact?.talks ?? 0;
            if (when.First && talks != 0) return false;
            if (when.Week > 0 && s.week < TalkChemistry.WeekGate(s, npc.id, when.Week)) return false;
            if (when.Window != null && !InWindow(s, when.Window)) return false;
            if (when.Layer > 0 && !AtLayer(s, npc.id, talks, when.Layer)) return false;
            // event=<kind> (talk rules 5): this week's event with them, read from the committed state; never below level 5.
            if (when.Event != null && !TalkEvents.Holds(s, npc.id, when.Event)) return false;
            // What it gives: a standing talk needs layer 4 and no snapshot yet this week; a layer frame needs a fact within its
            // layer to lead to, unless it is a first talk, which is said whatever it can give.
            switch (t.Kind)
            {
                case TalkKind.Standing:
                    return t.Standing != null && StandingOpen(s, npc.id) && (contact == null || contact.standingWeek != s.week);
                case TalkKind.Layer:
                    return !t.Reveals || when.First || NextFact(s, npc.id, RevealDepth(s, npc.id, t)) != null;
                case TalkKind.Event:
                    // An event talk is about its event: one without an event gate is never said.
                    return when.Event != null;
                default:
                    return true;
            }
        }

        internal static bool InWindow(EpisodeState s, string window)
        {
            bool firstNight = EpisodeEngine.IsFirstNight(s);
            int open = EpisodeEngine.Window(s);
            switch (window)
            {
                case TalkWindows.MoveIn: return firstNight;
                case TalkWindows.AfterHoH: return open == Windows.AfterHoH;
                case TalkWindows.AfterNoms: return open == Windows.AfterNominations;
                case TalkWindows.AfterVeto: return open == Windows.AfterVeto;
                case TalkWindows.AfterEviction: return open == Windows.AfterEviction && !firstNight;
                default: return false;
            }
        }

        /// <summary><c>layer&gt;=N</c>: 1 after the first counted talk, 2 and 3 by <see cref="OpenLayer"/>, 4 by <see cref="StandingOpen"/>.</summary>
        private static bool AtLayer(EpisodeState s, string npcId, int talks, int layer)
        {
            if (layer <= 0) return true;
            if (layer == 1) return talks >= 1;
            if (layer <= 3) return OpenLayer(s, npcId) >= layer;
            if (layer == 4) return StandingOpen(s, npcId);
            return false;
        }

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
        public static TalkResult Result(EpisodeState s, string npcId, string talkId, string tone)
        {
            var offer = Offer(s, npcId);
            if (!offer.Available || !string.Equals(offer.Talk.Id, talkId, StringComparison.Ordinal)) return null;
            var talk = offer.Talk;
            var reply = talk.Replies.FirstOrDefault(r => r.Tone == tone);
            if (reply == null) return null;
            return Answer(s, npcId, offer, reply);
        }

        /// <summary>
        /// What answering the talk on offer with one reply in its tone does (the body of <see cref="Result"/>): the reply is the
        /// talk's own, or at talk rules 3 the Flirt line's (<see cref="TalkKind.FlirtLine"/>) or a romance row's tone reply.
        /// </summary>
        internal static TalkResult Answer(EpisodeState s, string npcId, TalkOffer offer, TalkReply reply)
        {
            var talk = offer.Talk;
            string tone = reply.Tone;
            var npc = s.Find(npcId);
            var contact = Contact(s, npcId);
            // Talk rules 2 (slice S2): nothing below reads a slice-2 rule unless this holds.
            bool two = TalkChemistry.On(s);
            int landing = two ? LandingNow(s, npcId, tone) : Landing(s, npc, tone);

            // Warmth (R5): +1, 0, -1; a repeat never gains (R9's small gain); a positive gain stops when either side is at 25.
            int warmth = landing;
            if (offer.Repeat) warmth = Math.Min(0, warmth);
            if (warmth > 0 && (s.Score(npcId, s.playerId) >= WarmthStop || s.Score(s.playerId, npcId) >= WarmthStop)) warmth = 0;
            // The Close friend's favour (T6): the reply is the act, +2 or -2, in place of the free warmth and past the stop.
            var favour = TalkFavour.None;
            if (two && talk.Kind == TalkKind.Stage && talk.Stage == FriendStage.CloseFriend && reply.Favour != TalkFavour.None)
            {
                favour = reply.Favour;
                warmth = favour == TalkFavour.Keep ? TalkFriendship.FavourWarmth : -TalkFriendship.FavourWarmth;
            }

            // Rapport in tenths beside the integer (spec 3.4, D1), by the listener's class; a repeat at most a neutral talk's; from
            // talk rules 2 by the chemistry too (T2), rounded once.
            int tenths = two ? Tenths(landing, offer.Repeat, ClassOf(npc), TalkChemistry.Band(s, npcId)) : Tenths(landing, offer.Repeat, ClassOf(npc));
            int rapportBefore = contact?.rapport ?? 0, tenthsBefore = contact?.talkTenths ?? 0;
            int total = tenthsBefore + tenths;
            int agreementAfter = Math.Max(-TalkRules.MostAgreement, Math.Min(TalkRules.MostAgreement, (contact?.agreement ?? 0) + landing));
            int leanBefore = two ? s.story.talkLean : 0;

            var result = new TalkResult
            {
                // The player's line as said: its tokens filled as the reply's button showed them (TalkScreenLines.Replies).
                NpcId = npcId, TalkId = talk.Id, Tone = tone, Landing = landing, Reply = TalkText.Fill(s, npcId, reply.Line ?? ""),
                Reaction = TalkText.Fill(s, npcId, ReactionAt(reply, landing)),
                Outcome = landing > 0 ? Landed : landing < 0 ? WrongNote : NothingMuchMoved,
                Warmth = warmth, Tenths = tenths, RapportBefore = rapportBefore, TenthsBefore = tenthsBefore,
                RapportAfter = Math.Min(100, rapportBefore + total / 10), TenthsAfter = total % 10, Repeat = offer.Repeat,
                Favour = favour, AgreementAfter = agreementAfter, LeanBefore = leanBefore,
                LeanAfter = two ? TalkLean.After(leanBefore, tone) : leanBefore,
            };

            if (two)
            {
                // A stage talk marks its stage with one event row (T5).
                if (talk.Kind == TalkKind.Stage)
                {
                    result.StageReached = talk.Stage;
                    result.StageEvent = TalkFriendship.EventLine(talk.Stage, TalkText.SavedName(s, npc.name));
                }
                // The third counted talk shows the chemistry for the first time (T1), as it stands after this talk.
                if ((contact?.talks ?? 0) == TalkChemistry.ShownAfterTalks - 1)
                    result.ChemistryShown = TalkChemistry.Word(TalkChemistry.BandOf(TalkChemistry.ValueOf(agreementAfter, TalkChemistry.Affinity(s, npcId))));
            }

            // A layer frame leads to the next fact within its layer; a grating landing or a reply that holds back learns nothing.
            if (talk.Kind == TalkKind.Layer && talk.Reveals && landing > -1 && !reply.HoldsBack)
            {
                var fact = NextFact(s, npcId, RevealDepth(s, npcId, talk));
                if (fact != null) { result.FactId = fact.id; result.Fact = fact.text; result.FactLayer = fact.depth; }
            }

            // Where they stand (R6): in Candour's stance for the week; after a grating landing, the short version (neutral, no
            // names); a reply that holds back gets nothing.
            if (talk.Kind == TalkKind.Standing && !reply.HoldsBack)
            {
                string stance = landing < 0 ? Candour.Neutral : TalkStanding.Stance(s, npcId);
                result.Stance = stance;
                result.StandingLine = TalkStanding.Line(s, npcId, talk, stance);
                result.StandingSaid = TalkText.FirstNames(s, result.StandingLine);
            }
            return result;
        }

        private static string ReactionAt(TalkReply reply, int landing)
        {
            string line = landing > 0 ? reply.Lands : landing < 0 ? reply.Grates : reply.Even;
            return string.IsNullOrEmpty(line) ? reply.Even ?? "" : line;
        }

        /// <summary>
        /// How a tone lands on a housemate (spec 3.3): their reception of its approach plus the approach weights on their own lore
        /// sheet (every fact, known or not), clamped to -1..+1.
        /// </summary>
        public static int Landing(EpisodeState s, ContestantState npc, string tone)
        {
            string approach = TalkTone.Approach(tone);
            if (npc == null || approach == null) return 0;
            int value = Personality.Reception(npc, approach)
                + Lore.FactsOf(s, npc.id).Where(f => f.approach == approach).Sum(f => f.sign);
            return Math.Max(-1, Math.Min(1, value));
        }

        /// <summary>
        /// How a tone lands on a housemate now: <see cref="Landing"/>, and from talk rules 2 (ruling R10) one step lower, not below -1,
        /// when the player answered them in the same tone in either of the last two counted talks (<c>lastTone</c>, <c>priorTone</c>);
        /// from talk rules 3 a Flirt is exempt (courting repeats it by design), and still counts in those two talks for the others.
        /// Below talk rules 2 exactly <see cref="Landing"/>.
        /// </summary>
        public static int LandingNow(EpisodeState s, string npcId, string tone)
        {
            var npc = s?.Find(npcId);
            int landing = Landing(s, npc, tone);
            if (!TalkChemistry.On(s) || string.IsNullOrEmpty(tone)) return landing;
            var contact = Contact(s, npcId);
            // Talk rules 3: courting is a Flirt every chance you get, so a Flirt does not wear thin by repetition; every other
            // tone still lands one step lower on a repeat.
            bool flirtAtThree = tone == TalkRules.Flirt && TalkRomance.LevelOn(s);
            if (!flirtAtThree && contact != null && (contact.lastTone == tone || contact.priorTone == tone)) landing = Math.Max(-1, landing - 1);
            // Talk rules 3 (spec 3.3): a pushed turn-aside's cool lowers the landing one step for that many counted talks.
            if (contact != null && contact.cool > 0 && TalkRomance.LevelOn(s)) landing = Math.Max(-1, landing - 1);
            return landing;
        }

        /// <summary>
        /// Rapport tenths at talk rules 2 (T2): <see cref="Tenths(int, bool, string)"/>'s base by landing, times the listener's class
        /// (1.2, 1.0, 0.8) and the chemistry (<see cref="TalkChemistry.TenthsTimes"/>), rounded half up once, at least 1.
        /// </summary>
        public static int Tenths(int landing, bool repeat, string listenerClass, ChemistryBand chemistry)
        {
            int baseTenths = landing > 0 ? TenthsLands : landing < 0 ? TenthsGrates : TenthsEven;
            if (repeat) baseTenths = Math.Min(baseTenths, TenthsEven);
            int times = listenerClass == TalkPools.Spark ? 12 : listenerClass == TalkPools.Guarded ? 8 : 10;
            return Math.Max(1, (baseTenths * times * TalkChemistry.TenthsTimes(chemistry) + 500) / 1000);
        }

        /// <summary>Rapport tenths a counted talk gains (spec 3.4): 8, 5 or 2 by landing, at most 5 on a repeat; x1.2 Spark, x0.8 Guarded, rounded half up, at least 1.</summary>
        public static int Tenths(int landing, bool repeat, string listenerClass)
        {
            int baseTenths = landing > 0 ? TenthsLands : landing < 0 ? TenthsGrates : TenthsEven;
            if (repeat) baseTenths = Math.Min(baseTenths, TenthsEven);
            int times = listenerClass == TalkPools.Spark ? 12 : listenerClass == TalkPools.Guarded ? 8 : 10;
            return Math.Max(1, (baseTenths * times + 5) / 10);
        }

        /// <summary>The deepest fact a talk may lead to: its own layer within what is open with them (a first talk: layer 1).</summary>
        private static int RevealDepth(EpisodeState s, string npcId, TalkRow talk)
        {
            int open = Math.Max(1, OpenLayer(s, npcId));
            return talk.Layer >= 1 && talk.Layer <= 3 ? Math.Min(talk.Layer, open) : open;
        }

        /// <summary>
        /// The next fact a free talk leads to: the shallowest hidden one on their snapshotted sheet no deeper than
        /// <paramref name="depth"/>, never a secret (depth 4 or the secret facet), in the profile's facet order; null when none, or
        /// when the player already knows the 200 they can.
        /// </summary>
        public static Lore.Fact NextFact(EpisodeState s, string npcId, int depth)
        {
            if (s?.story?.knownFacts == null || s.story.knownFacts.Count >= 200) return null;
            return Lore.FactsOf(s, npcId)
                .Where(f => f.depth >= 1 && f.depth <= depth && f.depth < 4 && f.facet != Lore.Facets.Secret && !Lore.Knows(s, f.id))
                .OrderBy(f => f.depth).ThenBy(f => Array.IndexOf(Lore.Facets.All, f.facet)).ThenBy(f => f.id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// The deepest of layers 1 to 3 open with a housemate (spec 3.7, D13): 1 from the first talk, 2 at rapport 3, 3 at rapport 6
        /// with their view of you 15 or a finished personal story. An All-Star's stops at 2 (ruling R7). 0 when off or not a housemate.
        /// </summary>
        public static int OpenLayer(EpisodeState s, string npcId)
        {
            if (!On(s)) return 0;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer) return 0;
            int rapport = Contact(s, npcId)?.rapport ?? 0;
            // Talk rules 2 (T2): the rapport gates read through the chemistry's one gate path.
            if (rapport < TalkChemistry.RapportGate(s, npcId, BackgroundRapport)) return 1;
            if (IsAllStar(npc) || rapport < TalkChemistry.RapportGate(s, npcId, DrivesRapport)) return 2;
            return s.Score(npcId, s.playerId) >= DrivesView || EpisodeEngine.FinishedPersonalBeat(s, npcId) ? 3 : 2;
        }

        /// <summary>
        /// Whether layer 4, where they stand, is open with a housemate (spec 3.7, the Friend numbers): rapport 6 (Guarded 8), their
        /// view of you 35, from week 2.
        /// </summary>
        public static bool StandingOpen(EpisodeState s, string npcId)
        {
            if (!On(s)) return false;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || s.week < TalkChemistry.WeekGate(s, npcId, StandingWeek)) return false;
            int need = ClassOf(npc) == TalkPools.Guarded ? StandingRapportGuarded : StandingRapport;
            return (Contact(s, npcId)?.rapport ?? 0) >= TalkChemistry.RapportGate(s, npcId, need) && s.Score(npcId, s.playerId) >= StandingView;
        }

        /// <summary>The pool a housemate's talks come from: <see cref="TalkPools.Star"/> for any All-Star template, else their class.</summary>
        public static string PoolOf(ContestantState who) => IsAllStar(who) ? TalkPools.Star : ClassOf(who);

        /// <summary>
        /// A housemate's temperament class from their six axes (spec 3.5): Spark at Sociable 2 or more with Bold or Warm 1 or more;
        /// Guarded at Sociable -1 or less, or Steady 3 or more with Sociable 0 or less; Steady otherwise.
        /// </summary>
        public static string ClassOf(ContestantState who)
        {
            var a = Personality.Of(who);
            if (a.Sociable >= 2 && (a.Bold >= 1 || a.Warm >= 1)) return TalkPools.Spark;
            if (a.Sociable <= -1 || a.Steady >= 3 && a.Sociable <= 0) return TalkPools.Guarded;
            return TalkPools.Steady;
        }

        /// <summary>Whether a card was built from an All-Stars template, whatever it is now named (ruling R7).</summary>
        public static bool IsAllStar(ContestantState who) =>
            StoryPeople.TemplateOf(who)?.Roster == CastTemplates.Roster.AllStars;

        /// <summary>A talk's priority: its own, or its kind's default.</summary>
        public static int PriorityOf(TalkRow talk) =>
            talk == null ? 0 : talk.Priority > 0 ? talk.Priority
                : TalkRomance.IsRomanceKind(talk.Kind) ? TalkRomance.PriorityOfKind(talk.Kind)
                : talk.Kind == TalkKind.Event ? EventPriority
                : talk.Kind != TalkKind.Rhythm ? LayerPriority : talk.When?.Window != null ? WindowPriority : RhythmPriority;
    }
}
