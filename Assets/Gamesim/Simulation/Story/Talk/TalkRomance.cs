using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A housemate's romance stance with the player (slice S3; rulings R7, R14, R15, U5). Derived, never saved: a regular reads
    /// their lore sheet (open, strategic, closed; a derived sheet is open at Sociable 1 or more, else closed); an All-Stars template,
    /// whatever the card is named, reads <see cref="TalkStanceTable"/> (here to play, undecided or open; a template the table does
    /// not hold reads here to play).
    /// </summary>
    public enum RomanceStance
    {
        Open,
        Strategic,
        Closed,
        HereToPlay,
        Undecided,
    }

    /// <summary>What a Flirt answer plays with a housemate (<see cref="TalkRomance.SceneFor"/>), and the scenes a choice plays.</summary>
    public enum RomanceScene
    {
        /// <summary>No scene: a tone answer that is not a Flirt, or a choice that plays nothing more.</summary>
        None,
        /// <summary>Interested: they answer in kind (the FlirtLine row's reaction at the landing) and the spark builds.</summary>
        InKind,
        /// <summary>Not interested: the soft no (spark 0, romance closed two weeks).</summary>
        SoftNo,
        /// <summary>Undecided, the first Flirt: the hold-back (no spark that window).</summary>
        HoldBack,
        /// <summary>A closed regular: the line drawn (Flirt then leaves their talks: flirt-removed).</summary>
        LineDrawn,
        /// <summary>Here to play: the turn-aside, by attempt.</summary>
        TurnAside,
        /// <summary>The player made it official and they said yes: a couple.</summary>
        Accepts,
        /// <summary>The player made it official and they said "not like that".</summary>
        Refuses,
    }

    /// <summary>The headline of what a romance answer changed, for the third beat (<see cref="TalkRomanceNotes.AfterLines"/>).</summary>
    public enum RomanceChange
    {
        None,
        /// <summary>The spark grew (<see cref="TalkResult.SparkBefore"/> to <see cref="TalkResult.SparkAfter"/>).</summary>
        Spark,
        SoftNo,
        HoldBack,
        LineDrawn,
        /// <summary>A turn-aside; <see cref="TalkResult.Attempt"/> says which.</summary>
        TurnAside,
        /// <summary>They are a couple now.</summary>
        Couple,
        /// <summary>"Not yet" to their declaration.</summary>
        NotYet,
        /// <summary>"Not like that", theirs or the player's.</summary>
        NotLikeThat,
        /// <summary>"Just friends": romance with them closed for the season, at no cost.</summary>
        JustFriends,
        /// <summary>The player's own Flirt lifted an earlier "Just friends".</summary>
        FriendsLifted,
        /// <summary>The couple ended kindly.</summary>
        Ended,
        /// <summary>A second declaration while a couple stood: both ended it.</summary>
        SecondDeclaration,
        /// <summary>A turn-aside choice was answered (laugh it off, push again, apologize).</summary>
        TurnAsideAnswered,
    }

    /// <summary>
    /// Romance with the player (slice S3, talk rules 3; spec 3.9 to 3.11 with the lead's rulings U1 to U10 and R8). Pure: the
    /// queries the screen, the notebook and the engine read. Nothing here draws from the season's stream (the interest and
    /// jealousy coins are keyed story coins). Every query is off (false, 0, null, None) below talk rules 3, and everything
    /// romantic also when <c>story.romanceStorylines</c> is off.
    ///
    /// <para>EVERY GATE is read through <see cref="RapportGate"/> and <see cref="WeekGate"/>: the chemistry's one place
    /// (<see cref="TalkChemistry.RapportGate"/>, <see cref="TalkChemistry.WeekGate"/>) plus the undecided stance's offsets, each
    /// applied once.</para>
    /// </summary>
    public static partial class TalkRomance
    {
        // ------------------------------------------------------------------ the numbers (spec 3.9 to 3.11, U2 to U8)

        /// <summary>Priorities (spec 3.13): jealousy 100, their declaration 95, an opening, a couple talk and an ex talk 60, a house reaction 40.</summary>
        public const int JealousyPriority = 100, DeclarationPriority = 95, RomancePriority = 60, ReactionPriority = 40;

        /// <summary>The soft no closes romance two weeks; "not like that" three; an ending for the season (<see cref="TalkRules.ForTheSeason"/>).</summary>
        public const int SoftNoWeeks = 2, NotLikeThatWeeks = 3;

        /// <summary>A Steady housemate declares after this many of the player's Flirts; an undecided one after <see cref="UndecidedDeclaresAfter"/>.</summary>
        public const int SteadyDeclaresAfter = 4, UndecidedDeclaresAfter = 6;

        /// <summary>"Not yet" is asked again at most twice (R8): a third "not yet" lets it lapse.</summary>
        public const int MostNotYets = 2;

        /// <summary>The undecided stance (U7): interest base 0.30, pace x0.6, rapport gates +3, week gates +1.</summary>
        public const double UndecidedBase = 0.30, UndecidedPace = 0.6;
        public const int UndecidedRapport = 3, UndecidedWeek = 1;

        /// <summary>Spark: 0 to 100, one counted Flirt a window adds min(<see cref="MostSparkStep"/>, round(5 x pace x L)).</summary>
        public const int MostSpark = 100, MostSparkStep = 15, ReadySpark = 40;

        /// <summary>The ready table's warmth counts the spark: mutual warmth plus spark / this (integer division) meets the row.</summary>
        public const int ReadyWarmthPerSpark = 3;

        /// <summary>The free warmth of a Flirt answered in kind at a good landing (in place of R5's +1; R5's stop at 25 still cuts it to 0).</summary>
        public const int InKindWarmth = 2;

        /// <summary>The acts (U3, U6): ending kindly -3 both ways; they end it -8; jealousy -5; a second declaration -10 each.</summary>
        public const int EndKindly = -3, TheyEndIt = -8, JealousyCost = -5, SecondDeclarationCost = -10;

        /// <summary>A couple ends when their view of the player is under this at an anchor.</summary>
        public const int EndsBelowView = 10;

        /// <summary>A Couple fact goes public after this many anchors (U8), or at once when witnessed.</summary>
        public const int PublicAfterAnchors = 3;

        /// <summary>The jealousy coin's chance at the next anchor (spec 3.9).</summary>
        public const double JealousyChance = 0.5;

        /// <summary>Flirting with another housemate counts toward jealousy while the player's spark with someone is this or more.</summary>
        public const int JealousSpark = 20;

        /// <summary>The ex talk is due for two weeks after a couple ends.</summary>
        public const int ExWeeks = 2;

        /// <summary>The jury terms (U6): took the no well +2, pushy -3, inside the story-terms band.</summary>
        public const int JuryTookTheNoWell = 2, JuryPushy = -3;

        /// <summary>The ledger type of a second declaration (permanent).</summary>
        public const string SecondDeclarationEvent = "second-declaration";

        /// <summary>The romance event rows' kind in the season's log.</summary>
        public const string EventKind = "talk-romance";

        // ------------------------------------------------------------------ on and off

        /// <summary>Whether the season plays talk rules 3 (free talk on, at level 3 or later), romance switch or not: T9, the house's reaction.</summary>
        public static bool LevelOn(EpisodeState s) => TalkChemistry.On(s) && TalkRules.At(s, TalkRules.Romance);

        /// <summary>Whether romance plays: talk rules 3 and <c>story.romanceStorylines</c>.</summary>
        public static bool On(EpisodeState s) => LevelOn(s) && s.story.romanceStorylines;

        // ------------------------------------------------------------------ stance (U5)

        /// <summary>The stance of an All-Stars template id by <see cref="TalkStanceTable"/>; a template it does not hold is here to play.</summary>
        public static RomanceStance StanceOfTemplate(string templateId) =>
            templateId != null && TalkStanceTable.ByTemplate.TryGetValue(templateId, out var stance) ? stance : RomanceStance.HereToPlay;

        /// <summary>A housemate's stance (derived, never saved): an All-Stars template by the table, whatever the card is named; a regular by their sheet.</summary>
        public static RomanceStance StanceOf(EpisodeState s, string npcId)
        {
            var who = s?.Find(npcId);
            if (who == null) return RomanceStance.Closed;
            if (FreeTalk.IsAllStar(who)) return StanceOfTemplate(StoryPeople.TemplateOf(who)?.Id);
            switch (Lore.SheetIn(s, npcId)?.romance)
            {
                case "open": return RomanceStance.Open;
                case "strategic": return RomanceStance.Strategic;
                case "closed": return RomanceStance.Closed;
                default: return Personality.Of(who).Sociable >= 1 ? RomanceStance.Open : RomanceStance.Closed;
            }
        }

        /// <summary>Whether the player's own card is an All-Stars template whose stance is here to play (U2): Flirt is never offered then.</summary>
        public static bool PlayerHereToPlay(EpisodeState s)
        {
            var player = s?.Find(s.playerId);
            return player != null && FreeTalk.IsAllStar(player) && StanceOfTemplate(StoryPeople.TemplateOf(player)?.Id) == RomanceStance.HereToPlay;
        }

        private static bool Undecided(EpisodeState s, string npcId) => StanceOf(s, npcId) == RomanceStance.Undecided;

        /// <summary>
        /// Whether a housemate holds a showmance with someone other than the player who is still in the house (a private or a
        /// public bond): while it holds, Flirt is not offered with them, they are never ready, and no couple with the player
        /// forms. A partner who has left the house (an apart bond) holds nobody back.
        /// </summary>
        public static bool InAnotherShowmance(EpisodeState s, string npcId) => HeldInTheHouse(s, npcId, s?.playerId);

        /// <summary>
        /// Whether a couple of the player and a housemate is barred by a showmance in the house: the housemate's with someone
        /// else (<see cref="InAnotherShowmance"/>), or the player's with someone else. The one test the screen's offers (through
        /// <see cref="Ready"/>, so Make it official and a declaration, and a declaration's Accept) and the engine's couple step
        /// share, so the screen never offers a couple the engine refuses. An apart bond bars nothing: once a partner leaves the
        /// house, a new couple can form on either side.
        /// </summary>
        public static bool CoupleBarred(EpisodeState s, string npcId) =>
            InAnotherShowmance(s, npcId) || HeldInTheHouse(s, s?.playerId, npcId);

        /// <summary>Whether <paramref name="id"/> holds a private or public showmance bond with anyone but <paramref name="other"/>.</summary>
        private static bool HeldInTheHouse(EpisodeState s, string id, string other)
        {
            if (id == null || s?.story?.bonds == null) return false;
            foreach (var bond in s.story.bonds)
            {
                if (bond == null || bond.kind != BondKinds.Showmance || (bond.status != BondStatus.Private && bond.status != BondStatus.Public)) continue;
                string partner = bond.aId == id ? bond.bId : bond.bId == id ? bond.aId : null;
                if (partner != null && partner != other) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the gates

        /// <summary>A rapport gate with a housemate: the chemistry's shift (kindred -2, grating +3), then the undecided stance's +3.</summary>
        public static int RapportGate(EpisodeState s, string npcId, int gate) =>
            TalkChemistry.RapportGate(s, npcId, gate) + (Undecided(s, npcId) ? UndecidedRapport : 0);

        /// <summary>A week gate with a housemate: the chemistry's shift (kindred -1), then the undecided stance's +1. 0 stays 0.</summary>
        public static int WeekGate(EpisodeState s, string npcId, int week) =>
            week <= 0 ? week : TalkChemistry.WeekGate(s, npcId, week) + (Undecided(s, npcId) ? UndecidedWeek : 0);

        /// <summary>The pair's rapport for the Flirt answer before any shift (spec 3.9): Spark+Spark 1, Spark+Steady 3, Steady+Steady 4, a Guarded one 7, Guarded+Guarded 9.</summary>
        public static int FlirtRapport(string playerClass, string npcClass)
        {
            int guarded = Count(TalkPools.Guarded, playerClass, npcClass), spark = Count(TalkPools.Spark, playerClass, npcClass);
            if (guarded == 2) return 9;
            if (guarded == 1) return 7;
            if (spark == 2) return 1;
            return spark == 1 ? 3 : 4;
        }

        private static int Count(string cls, string a, string b) => (a == cls ? 1 : 0) + (b == cls ? 1 : 0);

        /// <summary>The player's class (slice 2's read, with the lean) and the housemate's.</summary>
        private static (string player, string npc) Classes(EpisodeState s, string npcId) =>
            (TalkLean.PlayerClass(s), FreeTalk.ClassOf(s.Find(npcId)));

        /// <summary>
        /// Whether the Flirt answer is on offer with a housemate now (U2): romance on; the player's card not here to play; the
        /// housemate's rapport at the pair's gate (through <see cref="RapportGate"/>); not while grating; not while
        /// romanceClosedUntil is ahead; not after flirt-removed; not while a couple stands with them; a FlirtLine row for them.
        /// The window cap is the talk's own (<see cref="FreeTalk.Offer"/>).
        /// </summary>
        public static bool FlirtOpen(EpisodeState s, string npcId)
        {
            if (!On(s) || PlayerHereToPlay(s) || s.Find(s.playerId)?.status != ContestantStatus.Active) return false;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active) return false;
            var contact = FreeTalk.Contact(s, npcId);
            if (contact != null && (contact.talkFlags.Contains(TalkRules.FlirtRemoved) || contact.romanceClosedUntil > s.week
                || contact.romance == TalkRules.Couple)) return false;
            if (InAnotherShowmance(s, npcId)) return false;
            if (TalkChemistry.Band(s, npcId) == ChemistryBand.Grating) return false;
            var (pc, nc) = Classes(s, npcId);
            if ((contact?.rapport ?? 0) < RapportGate(s, npcId, FlirtRapport(pc, nc))) return false;
            return FlirtLine(s, npcId) != null;
        }

        /// <summary>
        /// Whether the talk on offer carries the Flirt answer as a fourth reply: a layer, rhythm, standing or stage talk while
        /// <see cref="FlirtOpen"/>, never a romance talk. A Flirt on a stage talk is the window's counted talk and leaves the stage
        /// talk due (not spent, no stage event row).
        /// </summary>
        public static bool FlirtOffered(EpisodeState s, string npcId, TalkRow talk) =>
            talk != null && (talk.Kind == TalkKind.Layer || talk.Kind == TalkKind.Rhythm || talk.Kind == TalkKind.Standing || talk.Kind == TalkKind.Stage)
            && FlirtOpen(s, npcId);

        /// <summary>The FlirtLine row the player's Flirt answer reads with a housemate in this window (their own table, else their class pool), or null.</summary>
        public static TalkRow FlirtLine(EpisodeState s, string npcId)
        {
            var npc = s?.Find(npcId);
            return npc == null ? null : Pick(s, npc, Mine(s, npc, TalkKind.FlirtLine), true);
        }

        // ------------------------------------------------------------------ interest, spark, ready

        /// <summary>
        /// A = base + class + 0.03 x clamp(affinity, -3, 3) + min(0.1, 0.02 x agreement) (spec 3.9 with the critic's cap). base: open
        /// 0.50, strategic and undecided 0.30; class Spark +0.15, Guarded -0.15. Reads no name, gender word or looks: the stance,
        /// the class (the six axes), the trait affinity and the agreement only.
        /// </summary>
        public static double InterestOf(EpisodeState s, string npcId)
        {
            var npc = s?.Find(npcId);
            if (npc == null) return 0;
            var stance = StanceOf(s, npcId);
            double value = stance == RomanceStance.Open ? 0.50 : stance == RomanceStance.Strategic || stance == RomanceStance.Undecided ? UndecidedBase : 0;
            string cls = FreeTalk.ClassOf(npc);
            value += cls == TalkPools.Spark ? 0.15 : cls == TalkPools.Guarded ? -0.15 : 0;
            value += 0.03 * Math.Max(-TalkChemistry.MostAffinity, Math.Min(TalkChemistry.MostAffinity, TalkChemistry.Affinity(s, npcId)));
            value += Math.Min(0.1, 0.02 * (FreeTalk.Contact(s, npcId)?.agreement ?? 0));
            return value;
        }

        /// <summary>The interest coin u, fixed per season: StoryRandom.Unit(s, "talk:v1:interest:" + npc + ":" + player).</summary>
        public static double InterestCoin(EpisodeState s, string npcId) =>
            StoryRandom.Unit(s, "talk:v1:interest:" + npcId + ":" + s.playerId);

        /// <summary>Interested: <see cref="InterestOf"/> &gt; <see cref="InterestCoin"/>; a strategic one only while their view of you is 30 or more; a here-to-play or closed housemate never.</summary>
        public static bool Interested(EpisodeState s, string npcId)
        {
            if (!On(s)) return false;
            var stance = StanceOf(s, npcId);
            if (stance == RomanceStance.HereToPlay || stance == RomanceStance.Closed) return false;
            if (stance == RomanceStance.Strategic && s.Score(npcId, s.playerId) < 30) return false;
            return InterestOf(s, npcId) > InterestCoin(s, npcId);
        }

        /// <summary>The pace (spec 3.9): Spark+Spark 2.5, Spark+Steady 1.4, Steady+Steady 1.0, Spark+Guarded 0.8, Steady+Guarded 0.6, Guarded+Guarded 0.4; kindred x1.25; undecided x0.6.</summary>
        public static double Pace(EpisodeState s, string npcId)
        {
            var (pc, nc) = Classes(s, npcId);
            int spark = Count(TalkPools.Spark, pc, nc), guarded = Count(TalkPools.Guarded, pc, nc);
            double pace = guarded == 2 ? 0.4 : guarded == 1 ? (spark == 1 ? 0.8 : 0.6) : spark == 2 ? 2.5 : spark == 1 ? 1.4 : 1.0;
            if (TalkChemistry.Band(s, npcId) == ChemistryBand.Kindred) pace *= 1.25;
            if (Undecided(s, npcId)) pace *= UndecidedPace;
            return pace;
        }

        /// <summary>One counted Flirt's spark step at a landing: min(15, round half up(5 x pace x L)), L = 1.5, 1.0, 0.5.</summary>
        public static int SparkStep(EpisodeState s, string npcId, int landing)
        {
            double l = landing > 0 ? 1.5 : landing < 0 ? 0.5 : 1.0;
            return Math.Min(MostSparkStep, (int)Math.Round(5 * Pace(s, npcId) * l + 1e-9, MidpointRounding.AwayFromZero));
        }

        /// <summary>The ready table's row for a pair (spec 3.9): mutual warmth, rapport, earliest week.</summary>
        public static (int warmth, int rapport, int week) ReadyRow(string playerClass, string npcClass)
        {
            int guarded = Count(TalkPools.Guarded, playerClass, npcClass), spark = Count(TalkPools.Spark, playerClass, npcClass);
            if (guarded == 2) return (50, 12, 5);
            if (guarded == 1) return (40, 9, 4);
            if (spark == 2) return (15, 3, 1);
            return spark == 1 ? (25, 5, 2) : (30, 6, 3);
        }

        /// <summary>
        /// Ready to be a couple: romance on and flirting with them, not grating, spark 40, and the pair's row of the ready table (a
        /// strategic stance needs 10 less warmth; rapport and week through the gates). Spark+Spark's week 1 is from the after-veto
        /// window. Interest is not read here: they declare only when interested, and the player's own "Make it official" is
        /// answered by it (yes, or their "not like that").
        /// </summary>
        public static bool Ready(EpisodeState s, string npcId)
        {
            if (!On(s) || s.Find(s.playerId)?.status != ContestantStatus.Active) return false;
            var npc = s.Find(npcId);
            var contact = FreeTalk.Contact(s, npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active || contact == null) return false;
            if (contact.romance != TalkRules.Flirting || contact.spark < ReadySpark || contact.romanceClosedUntil > s.week) return false;
            if (InAnotherShowmance(s, npcId)) return false;
            // The player's own showmance in the house bars it too, unless it is the couple free talk keeps: Make it official is
            // then a second declaration (both walk away), never the couple step.
            if (PartnerOf(s) == null && CoupleBarred(s, npcId)) return false;
            if (TalkChemistry.Band(s, npcId) == ChemistryBand.Grating) return false;
            var (pc, nc) = Classes(s, npcId);
            var row = ReadyRow(pc, nc);
            int warmth = row.warmth - (StanceOf(s, npcId) == RomanceStance.Strategic ? 10 : 0);
            // The spark counts toward the warmth (a third of it): at spark 40 the rows read 2, 12, 17, 27 and 37.
            if (StoryPeople.Mutual(s, npcId, s.playerId) + contact.spark / ReadyWarmthPerSpark < warmth) return false;
            if (contact.rapport < RapportGate(s, npcId, row.rapport)) return false;
            int week = WeekGate(s, npcId, row.week);
            if (s.week < week) return false;
            if (s.week == 1 && week == 1 && (EpisodeEngine.IsFirstNight(s) || EpisodeEngine.Window(s) < Windows.AfterVeto)) return false;
            return true;
        }

        // ------------------------------------------------------------------ what plays

        /// <summary>What a Flirt answer to a housemate plays now (<see cref="RomanceScene"/>): here to play a turn-aside, closed the line drawn, undecided's first the hold-back, then in kind when interested, else the soft no.</summary>
        public static RomanceScene SceneFor(EpisodeState s, string npcId)
        {
            if (!On(s)) return RomanceScene.None;
            switch (StanceOf(s, npcId))
            {
                case RomanceStance.HereToPlay: return RomanceScene.TurnAside;
                case RomanceStance.Closed: return RomanceScene.LineDrawn;
                case RomanceStance.Undecided when (FreeTalk.Contact(s, npcId)?.flirts ?? 0) == 0: return RomanceScene.HoldBack;
            }
            return Interested(s, npcId) ? RomanceScene.InKind : RomanceScene.SoftNo;
        }

        /// <summary>The choices on offer with a talk or scene row now, in screen order (Make it official only once ready; Just friends in every romance talk).</summary>
        public static List<TalkChoice> ChoicesNow(EpisodeState s, string npcId, TalkRow talk)
        {
            var choices = new List<TalkChoice>();
            if (talk == null || !On(s)) return choices;
            var contact = FreeTalk.Contact(s, npcId);
            switch (talk.Kind)
            {
                case TalkKind.FlirtLine:
                case TalkKind.Opening:
                    if (Ready(s, npcId)) choices.Add(TalkChoice.MakeItOfficial);
                    choices.Add(TalkChoice.JustFriends);
                    break;
                case TalkKind.Declaration:
                    if (!CoupleBarred(s, npcId)) choices.Add(TalkChoice.Accept);
                    if ((contact?.notYets ?? 0) < MostNotYets + 1) choices.Add(TalkChoice.NotYet);
                    choices.Add(TalkChoice.NotLikeThat);
                    choices.Add(TalkChoice.JustFriends);
                    break;
                case TalkKind.Couple:
                    choices.Add(TalkChoice.EndItKindly);
                    break;
                case TalkKind.Jealousy:
                    if (contact?.romance == TalkRules.Couple) choices.Add(TalkChoice.EndItKindly);
                    break;
                case TalkKind.TurnAside:
                    if (talk.Attempt == 1) { choices.Add(TalkChoice.LaughItOff); choices.Add(TalkChoice.PushAgain); choices.Add(TalkChoice.Apologize); }
                    break;
            }
            return choices;
        }

        /// <summary>The housemate the player is a couple with (contact.romance couple), or null.</summary>
        public static string PartnerOf(EpisodeState s) =>
            s?.story?.contacts?.FirstOrDefault(c => c != null && c.romance == TalkRules.Couple)?.npcId;

        /// <summary>
        /// The romance talk due with a housemate, or null, in priority order: jealousy (100), their declaration (95), a couple talk,
        /// an ex talk, their opening (60), a house reaction (40). Off below talk rules 3 and without the romance switch.
        /// </summary>
        public static TalkRow DueTalk(EpisodeState s, string npcId)
        {
            if (!On(s) || s.Find(s.playerId)?.status != ContestantStatus.Active) return null;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active) return null;
            var contact = FreeTalk.Contact(s, npcId);
            if (contact == null) return null;
            TalkRow row;
            if (contact.talkFlags.Contains(TalkRules.Jealous) && (row = Pick(s, npc, Mine(s, npc, TalkKind.Jealousy), true)) != null) return row;
            if (Declares(s, npc, contact) && (row = Pick(s, npc, Mine(s, npc, TalkKind.Declaration), true)) != null) return row;
            if (contact.romance == TalkRules.Couple && (row = Pick(s, npc, Mine(s, npc, TalkKind.Couple), true)) != null) return row;
            if (contact.talkFlags.Contains(TalkRules.Ex) && s.week <= contact.romanceWeek + ExWeeks
                && (row = Pick(s, npc, Mine(s, npc, TalkKind.Ex), true)) != null) return row;
            if (Opens(s, npc, contact))
            {
                var spent = new HashSet<string>(contact.spentTalks.Select(TalkRules.SpentId), StringComparer.Ordinal);
                if ((row = Pick(s, npc, Mine(s, npc, TalkKind.Opening).Where(r => !spent.Contains(r.Id)), false)) != null) return row;
            }
            if (contact.talkFlags.Contains(TalkRules.CoupleTalk))
            {
                int reaction = ReactionNumber(s, npcId);
                var spent = new HashSet<string>(contact.spentTalks.Select(TalkRules.SpentId), StringComparer.Ordinal);
                if ((row = Pick(s, npc, Mine(s, npc, TalkKind.Reaction).Where(r => r.Reaction == reaction && !spent.Contains(r.Id)), false)) != null)
                    return row;
            }
            return null;
        }

        /// <summary>A Spark housemate opens (R8: only after the player's first Flirt), interested, while Flirt is open and before a couple; never an undecided one.</summary>
        private static bool Opens(EpisodeState s, ContestantState npc, ContactState contact) =>
            contact.flirts >= 1 && FreeTalk.ClassOf(npc) == TalkPools.Spark && StanceOf(s, npc.id) != RomanceStance.Undecided
            && (contact.romance == "" || contact.romance == TalkRules.Flirting) && Interested(s, npc.id) && FlirtOpen(s, npc.id);

        /// <summary>
        /// They declare (R8: only after the player's first Flirt) when ready and the player is in no couple: a Spark housemate at
        /// once, a Steady one after 4 of the player's Flirts, an undecided one after 6, a Guarded one never (an undecided Guarded one
        /// never either: the undecided stance is never ahead of its class). "Not yet" brings it back a
        /// week later, at most twice.
        /// </summary>
        private static bool Declares(EpisodeState s, ContestantState npc, ContactState contact)
        {
            if (contact.flirts < 1 || contact.notYets > MostNotYets || PartnerOf(s) != null) return false;
            if (contact.notYets > 0 && s.week < contact.romanceWeek + 1) return false;
            string cls = FreeTalk.ClassOf(npc);
            if (cls == TalkPools.Guarded) return false;
            if (StanceOf(s, npc.id) == RomanceStance.Undecided) { if (contact.flirts < UndecidedDeclaresAfter) return false; }
            else if (cls == TalkPools.Steady && contact.flirts < SteadyDeclaresAfter) return false;
            return Interested(s, npc.id) && Ready(s, npc.id);
        }

        /// <summary>
        /// Which of the two house reactions a flagged housemate says: 1 ("You two aren't subtle."), or 2 ("Careful. A pair is a
        /// target.") when another housemate with a lower id holds the flag or has said a reaction.
        /// </summary>
        public static int ReactionNumber(EpisodeState s, string npcId)
        {
            foreach (var other in s.story.contacts)
            {
                if (other == null || other.npcId == npcId || string.CompareOrdinal(other.npcId, npcId) > 0) continue;
                if (other.talkFlags.Contains(TalkRules.CoupleTalk)
                    || other.spentTalks.Any(e => TalkBank.Find(TalkRules.SpentId(e))?.Kind == TalkKind.Reaction)) return 2;
            }
            return 1;
        }

        /// <summary>Where the romance with a housemate stands, in plain words for their page, or null when there is nothing to say.</summary>
        public static string Words(EpisodeState s, string npcId)
        {
            if (!LevelOn(s)) return null;
            var contact = FreeTalk.Contact(s, npcId);
            if (contact == null) return null;
            switch (contact.romance)
            {
                case TalkRules.Couple: return "You're a couple.";
                case TalkRules.Ended: return "It's over between you.";
                case TalkRules.Friends: return "Just friends, for now.";
                case TalkRules.Closed when contact.romanceClosedUntil > s.week: return "They said no, for now.";
                case TalkRules.Flirting when contact.spark >= ReadySpark: return "Something real between you.";
                case TalkRules.Flirting when contact.spark > 0: return "A spark between you.";
            }
            if (contact.talkFlags.Contains(TalkRules.FlirtRemoved))
                return contact.turnAsides > 0 ? "They're here to play. That's settled." : "They drew a line. Friends only.";
            if (contact.turnAsides > 0) return "They turned it aside.";
            if (contact.flirts > 0 && StanceOf(s, npcId) == RomanceStance.Undecided) return "They're taking it slow.";
            return null;
        }

        // ------------------------------------------------------------------ the rows

        /// <summary>Whether a kind is a romance kind (slice S3): never an ordinary talk.</summary>
        public static bool IsRomanceKind(TalkKind kind) => kind >= TalkKind.FlirtLine && kind <= TalkKind.Reaction;

        /// <summary>A romance kind's default priority (spec 3.13).</summary>
        public static int PriorityOfKind(TalkKind kind)
        {
            switch (kind)
            {
                case TalkKind.Jealousy: return JealousyPriority;
                case TalkKind.Declaration: return DeclarationPriority;
                case TalkKind.Reaction: return ReactionPriority;
                default: return RomancePriority;
            }
        }

        /// <summary>A scene row of a kind for a housemate now (their own table for an All-Star, else their class pool), or null; for a turn-aside, its attempt.</summary>
        public static TalkRow SceneRow(EpisodeState s, string npcId, TalkKind kind, int attempt = 0)
        {
            var npc = s?.Find(npcId);
            if (npc == null) return null;
            var rows = Mine(s, npc, kind);
            if (kind == TalkKind.TurnAside) rows = rows.Where(r => r.Attempt == attempt);
            return Pick(s, npc, rows, true);
        }

        /// <summary>The romance rows of a kind a housemate may say now: an All-Star only their own table's, a regular their class pool's (or a table of their own); well formed; their gates holding.</summary>
        private static IEnumerable<TalkRow> Mine(EpisodeState s, ContestantState npc, TalkKind kind)
        {
            string templateId = StoryPeople.TemplateOf(npc)?.Id;
            bool star = FreeTalk.IsAllStar(npc);
            string cls = FreeTalk.ClassOf(npc);
            var contact = FreeTalk.Contact(s, npc.id);
            foreach (var row in TalkBank.Rows)
            {
                if (row == null || row.Kind != kind) continue;
                bool mine = row.Owner != null ? templateId != null && row.Owner == templateId : !star && row.Pool == cls;
                if (!mine || !WellFormed(row)) continue;
                var when = row.When ?? new TalkGate();
                if (when.Week > 0 && s.week < WeekGate(s, npc.id, when.Week)) continue;
                if (when.Window != null && !FreeTalk.InWindow(s, when.Window)) continue;
                if (when.Spark > 0 && (contact?.spark ?? 0) < when.Spark) continue;
                yield return row;
            }
        }

        /// <summary>The row whose keyed coin is lowest: per window when it may repeat, else for good.</summary>
        private static TalkRow Pick(EpisodeState s, ContestantState npc, IEnumerable<TalkRow> rows, bool perWindow)
        {
            TalkRow best = null; double bestCoin = 2;
            int key = FreeTalk.WindowKey(s);
            foreach (var row in rows)
            {
                double coin = StoryRandom.Unit(s, perWindow ? "talk:v1:rom:" + npc.id + ":" + key + ":" + row.Id : "talk:v1:q:" + npc.id + ":" + row.Id);
                if (coin < bestCoin) { best = row; bestCoin = coin; }
            }
            return best;
        }

        private static bool HasChoice(TalkRow row, TalkChoice choice) =>
            row.Replies != null && row.Replies.Any(r => r != null && r.Choice == choice && r.Tone == null && !string.IsNullOrEmpty(r.Line));

        /// <summary>Whether a romance row has what its kind needs (API.md's table); a row that does not is never said.</summary>
        public static bool WellFormed(TalkRow row)
        {
            if (row == null || string.IsNullOrEmpty(row.Id) || row.Id.Length > TalkRules.MostIdLength || row.Id.IndexOf(TalkRules.ToneMark) >= 0)
                return false;
            var replies = row.Replies ?? Array.Empty<TalkReply>();
            bool open = !string.IsNullOrEmpty(row.Open);
            bool tonesOnly = replies.Length > 0 && replies.All(r => r != null && r.Choice == TalkChoice.None && TalkRules.IsTone(r.Tone) && r.Tone != TalkRules.Flirt
                && !string.IsNullOrEmpty(r.Line));
            bool TonesAnd(TalkChoice extra) =>
                replies.Count(r => r != null && r.Choice == TalkChoice.None && TalkRules.IsTone(r.Tone) && r.Tone != TalkRules.Flirt && !string.IsNullOrEmpty(r.Line)) >= 1
                && replies.All(r => r != null && (r.Choice == TalkChoice.None ? TalkRules.IsTone(r.Tone) && r.Tone != TalkRules.Flirt && !string.IsNullOrEmpty(r.Line)
                    : r.Choice == extra && r.Tone == null && !string.IsNullOrEmpty(r.Line)));
            switch (row.Kind)
            {
                case TalkKind.FlirtLine:
                    return replies.Length == 1 && replies[0] != null && replies[0].Tone == TalkRules.Flirt && replies[0].Choice == TalkChoice.None
                        && !string.IsNullOrEmpty(replies[0].Line);
                case TalkKind.Opening:
                    return open && replies.Any(r => r != null && r.Tone == TalkRules.Flirt && r.Choice == TalkChoice.None && !string.IsNullOrEmpty(r.Line))
                        && replies.All(r => r != null && (r.Choice == TalkChoice.None ? TalkRules.IsTone(r.Tone) && !string.IsNullOrEmpty(r.Line)
                            : r.Choice == TalkChoice.JustFriends && r.Tone == null && !string.IsNullOrEmpty(r.Line)));
                case TalkKind.SoftNo:
                case TalkKind.HoldBack:
                case TalkKind.LineDrawn:
                case TalkKind.Accepts:
                case TalkKind.Refuses:
                    return open;
                case TalkKind.TurnAside:
                    if (row.Attempt == 2 || row.Attempt == 3) return open;
                    return row.Attempt == 1 && open && !string.IsNullOrEmpty(row.Second) && HasChoice(row, TalkChoice.LaughItOff)
                        && HasChoice(row, TalkChoice.PushAgain) && HasChoice(row, TalkChoice.Apologize);
                case TalkKind.Declaration:
                    return open && HasChoice(row, TalkChoice.Accept) && HasChoice(row, TalkChoice.NotYet) && HasChoice(row, TalkChoice.NotLikeThat);
                case TalkKind.Couple:
                    return open && TonesAnd(TalkChoice.EndItKindly);
                case TalkKind.Jealousy:
                    return open && TonesAnd(TalkChoice.EndItKindly);
                case TalkKind.Ex:
                    return open && tonesOnly;
                case TalkKind.Reaction:
                    return open && tonesOnly && (row.Reaction == 1 || row.Reaction == 2);
                default:
                    return false;
            }
        }

        // ------------------------------------------------------------------ the command (talk rules 3)

        /// <summary>
        /// The command's text at talk rules 3: <c>talkId|answer</c> or <c>talkId|answer|seenById</c>, the answer a tone or
        /// <c>choice:&lt;id&gt;</c>, the optional third part the housemate the house reports in the same room (a witness, U8).
        /// </summary>
        public static string CommandText(string talkId, string answer, string seenById = null) =>
            (talkId ?? "") + "|" + (answer ?? "") + (string.IsNullOrEmpty(seenById) ? "" : "|" + seenById);

        /// <summary>Reads a talk rules 3 command's text back; false for anything else.</summary>
        public static bool TryParse(string text, out string talkId, out string answer, out string seenById)
        {
            talkId = null; answer = null; seenById = null;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split('|');
            if (parts.Length < 2 || parts.Length > 3 || parts.Any(string.IsNullOrEmpty)) return false;
            talkId = parts[0]; answer = parts[1]; seenById = parts.Length == 3 ? parts[2] : null;
            return true;
        }

        /// <summary>What a JustTalk command would do at talk rules 3 (null when refused): the engine's own answer, played on a copy of the state.</summary>
        public static TalkResult Result(EpisodeState s, string npcId, string text) => EpisodeEngine.PreviewRomanceTalk(s, npcId, text);
    }
}
