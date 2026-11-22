using System;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// How a hunch reads a conversation or an act (plan 32 §I2): warm, scheming or tense. Saved only as a <c>Seen</c>
    /// standing's score (1 to 3); a code-only table, saved nowhere else.
    /// </summary>
    public static class HunchTone
    {
        public const int Warm = 1, Scheming = 2, Tense = 3;

        /// <summary>Whether a <c>Seen</c> standing's score is a tone.</summary>
        public static bool IsKnown(double score) => score == Warm || score == Scheming || score == Tense;

        /// <summary>
        /// The tone of a conversation, from its saved topic alone (§I2): bonding, casual and gossip are warm; strategy,
        /// alliance talk and nominations scheming; tension and rivalry tense. 0 for anything else. Never reads a score.
        /// </summary>
        public static int OfTopic(string topic)
        {
            switch (topic)
            {
                case "bonding": case "casual": case "gossip": return Warm;
                case "strategy": case "alliance_talk": case "nominations": return Scheming;
                case "tension": case "rivalry": return Tense;
                default: return 0;
            }
        }

        /// <summary>The tone of a sighted act (§I2): a private kind is scheming, a loud one tense, anything else warm.</summary>
        public static int OfAct(string kind) => NpcActKinds.IsPrivate(kind) ? Scheming : NpcActKinds.IsLoud(kind) ? Tense : Warm;

        /// <summary>How the Notice line says the tone: "talking warmly", "with their heads together", "snapping at each other".</summary>
        public static string Words(int tone) =>
            tone == Scheming ? "with their heads together" : tone == Tense ? "snapping at each other" : "talking warmly";
    }

    /// <summary>
    /// Overheard, in the rules (plan 32 §I, slice S2a; story rules 12): walking past two houseguests' conversation gives a
    /// free Notice, a hunch about the pair, and lingering gives a free Listen, today's eavesdrop reveal without its draws,
    /// capped and with a risk of being caught. No new command kind: at 12 the house's witness command
    /// (<see cref="EpisodeCommandKind.WitnessNpcAct"/>) whose text starts with <see cref="ConversationPrefix"/> comes here.
    /// Free: no tick and no draw from the season's stream; its only randomness is keyed (<see cref="StoryRandom"/>). Its
    /// records are a <see cref="ClaimSource.Seen"/> standing, an NPC-to-NPC <see cref="ClaimSource.Overheard"/> or
    /// <see cref="ClaimSource.Missed"/> standing, the <c>heard:</c> and <c>pact-heard:</c> cooldown keys and the
    /// <see cref="HunchEventKind"/> line, each refused below 12 (EpisodeValidation.HouseTalk.cs). Below 12 nothing here runs.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The witness command's text for a conversation: <c>conv:{sequence}</c> is a Notice.</summary>
        public const string ConversationPrefix = "conv:";
        /// <summary>... and <c>conv:{sequence}:listen</c> a Listen.</summary>
        public const string ListenSuffix = ":listen";
        /// <summary>The Notice's line kind (§L): new at 12.</summary>
        public const string HunchEventKind = "hunch";
        /// <summary>A Listen's record on its conversation, caught or not, for the rest of the week: <c>heard:{sequence}</c>.</summary>
        public const string HeardPrefix = "heard:";
        /// <summary>The week's one pact sentence, once heard: <c>pact-heard:{week}</c>.</summary>
        public const string PactHeardPrefix = "pact-heard:";

        /// <summary>The Notices a week (§P), sightings of acts included.</summary>
        public const int NoticesAWeek = 6;
        /// <summary>The Listens a week (§P), heard or caught.</summary>
        public const int ListensAWeek = 2;

        /// <summary>The chance a Listen is caught (§I3): 0.15, +0.10 when either of the pair is Intuitive or Analytical, -0.05 when the player is Introverted or Sneaky.</summary>
        public const double CaughtBase = 0.15, CaughtSharpPair = 0.10, CaughtQuietPlayer = -0.05;
        /// <summary>What being caught listening costs with the one who caught you (§I3), through a keyed change.</summary>
        public const double CaughtListeningImpact = -5;

        /// <summary>The paid listen-in's refusal at 12 (§I5): the listening is done by walking over.</summary>
        public const string WalkOverToListenRefusal = "Walk over and listen in.";

        /// <summary>Whether a witness command's text names a conversation rather than an act.</summary>
        public static bool IsConversationText(string text) => text != null && text.StartsWith(ConversationPrefix, StringComparison.Ordinal);

        /// <summary>The text for a Notice or a Listen on one conversation.</summary>
        public static string ConversationText(long sequence, bool listen) =>
            ConversationPrefix + sequence.ToString(CultureInfo.InvariantCulture) + (listen ? ListenSuffix : "");

        /// <summary>Reads <c>conv:{sequence}</c> or <c>conv:{sequence}:listen</c>; false for anything else.</summary>
        public static bool TryReadConversationText(string text, out long sequence, out bool listen)
        {
            sequence = 0; listen = false;
            if (!IsConversationText(text)) return false;
            string rest = text.Substring(ConversationPrefix.Length);
            if (rest.EndsWith(ListenSuffix, StringComparison.Ordinal)) { listen = true; rest = rest.Substring(0, rest.Length - ListenSuffix.Length); }
            return rest.Length > 0 && rest.All(ch => ch >= '0' && ch <= '9')
                && long.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out sequence) && sequence > 0;
        }

        /// <summary>Whether a standing is between two houseguests: neither end is the player.</summary>
        private static bool BetweenHouseguests(EpisodeState s, StandingRow row) =>
            row != null && row.fromId != s.playerId && row.toId != s.playerId;

        /// <summary>The <c>Seen</c> rows of this week: the Notice cap (§I1), counted from the ledger.</summary>
        public static int NoticedThisWeek(EpisodeState s) =>
            s?.ledger?.standings == null ? 0 : s.ledger.standings.Count(r => r != null && r.week == s.week && r.source == ClaimSource.Seen);

        /// <summary>
        /// The Listens of this week, heard or caught (§I1): the one source of truth for the cap, counted from the ledger's
        /// NPC-to-NPC <c>Overheard</c> and <c>Missed</c> standings, which at 12 only a free Listen writes.
        /// </summary>
        public static int ListenedThisWeek(EpisodeState s) =>
            s?.ledger?.standings == null ? 0 : s.ledger.standings.Count(r => r != null && r.week == s.week
                && (r.source == ClaimSource.Overheard || r.source == ClaimSource.Missed) && BetweenHouseguests(s, r));

        /// <summary>Whether the player has already noticed these two, either way round, in this tone this week.</summary>
        public static bool NoticedThisWeek(EpisodeState s, string a, string b, int tone) =>
            s?.ledger?.standings != null && s.ledger.standings.Any(r => r != null && r.week == s.week && r.source == ClaimSource.Seen
                && r.score == tone && ((r.fromId == a && r.toId == b) || (r.fromId == b && r.toId == a)));

        /// <summary>The pending conversation a witness text names, between these two either way round, this week; or null.</summary>
        private static NpcConversationState ConversationOf(EpisodeState s, long sequence, string a, string b) =>
            s.npcSocial?.pending?.FirstOrDefault(p => p != null && p.sequence == sequence && p.week == s.week
                && ((p.firstId == a && p.secondId == b) || (p.firstId == b && p.secondId == a)));

        /// <summary>
        /// Why the player cannot be said to notice (<c>conv:{sequence}</c>) or listen in on (<c>conv:{sequence}:listen</c>)
        /// this conversation now, or null where they can (§I1): story rules 12, the player in the house, the social week or
        /// the campaign, a pending conversation of these two with that sequence, neither of them the player; for a Notice,
        /// not these two in this tone already this week and fewer than <see cref="NoticesAWeek"/> this week; for a Listen,
        /// not this conversation already and fewer than <see cref="ListensAWeek"/> this week. Pure, for the house's watch.
        /// </summary>
        public static string OverhearRefusal(EpisodeState s, string text, string a, string b)
        {
            if (s == null || !StoryAt(s, StoryRules.Manners)) return WaveDKindRefusal;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return "Evicted players can follow the season but cannot influence it.";
            if (!NpcSocialState.IsEligiblePhase(s.phase)) return "Nobody is talking where you can hear right now.";
            if (!TryReadConversationText(text, out long sequence, out bool listen)) return "That moment has passed.";
            if (a == s.playerId || b == s.playerId) return "You were part of that.";
            var conversation = ConversationOf(s, sequence, a, b);
            if (conversation == null || s.Find(conversation.firstId)?.status != ContestantStatus.Active
                || s.Find(conversation.secondId)?.status != ContestantStatus.Active) return "That moment has passed.";
            if (!listen)
            {
                int tone = HunchTone.OfTopic(conversation.topic);
                if (tone == 0) return "That moment has passed.";
                if (NoticedThisWeek(s, conversation.firstId, conversation.secondId, tone)) return "You already noticed them.";
                if (NoticedThisWeek(s) >= NoticesAWeek) return "You have noticed enough for this week.";
                return null;
            }
            if (Cooling(s, HeardPrefix + sequence.ToString(CultureInfo.InvariantCulture))) return "You already heard them.";
            if (ListenedThisWeek(s) >= ListensAWeek) return "You have listened in enough this week.";
            return null;
        }

        /// <summary>The chance a Listen on these two is caught (§I3): from 0.10 to 0.25.</summary>
        public static double CaughtChance(EpisodeState s, ContestantState first, ContestantState second)
        {
            bool Has(ContestantState c, string trait) => c?.traits != null && c.traits.Contains(trait);
            bool sharp = new[] { first, second }.Any(c => Has(c, "Intuitive") || Has(c, "Analytical"));
            var player = s.Find(s.playerId);
            bool quiet = Has(player, "Introverted") || Has(player, "Sneaky");
            return CaughtBase + (sharp ? CaughtSharpPair : 0) + (quiet ? CaughtQuietPlayer : 0);
        }

        /// <summary>The keyed coin a Listen is caught on, and its catcher's: the same for one conversation always (§I3).</summary>
        public static string OverhearKey(long sequence) => "overhear:" + sequence.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The Notice's line (§I2), to the player alone: "You noticed A and B talking warmly in the kitchen." The room is
        /// the rendezvous' (<see cref="NpcPairing.RoomOf"/>).
        /// </summary>
        public static string NoticeLine(EpisodeState s, NpcConversationState conversation, int tone) =>
            "You noticed " + Name(s, conversation.firstId) + " and " + Name(s, conversation.secondId) + " " + HunchTone.Words(tone)
            + " in " + RoomWords.Where(NpcPairing.RoomOf(conversation.rendezvousId)) + ".";

        /// <summary>
        /// A Notice or a Listen (§I), refused by <see cref="OverhearRefusal"/> before anything moves. A Notice writes the pair's
        /// <c>Seen</c> standing, its score the tone, and the hunch line. A Listen first marks the conversation heard; then,
        /// on its keyed coin, it is caught - one of the pair, on a keyed index, thinks less of the player through a keyed
        /// change and remembers it, and the attempt is on the record as the pair's <c>Missed</c> row - or it hears what
        /// today's eavesdrop hears, with no draw: the pair's true standing, in a vote week the first speaker's ballot, the
        /// exact-pair pact sentence once a week and an act's clause, logged as today's eavesdrop line.
        /// </summary>
        private static void Overhear(EpisodeState s, EpisodeCommand c)
        {
            string refusal = OverhearRefusal(s, c.text, c.targetId, c.secondTargetId);
            Require(refusal == null, refusal);
            TryReadConversationText(c.text, out long sequence, out bool listen);
            var conversation = ConversationOf(s, sequence, c.targetId, c.secondTargetId);
            var first = s.Find(conversation.firstId);
            var second = s.Find(conversation.secondId);
            if (!listen)
            {
                int tone = HunchTone.OfTopic(conversation.topic);
                AddStanding(s, first.id, second.id, ClaimSource.Seen, tone);
                Log(s, HunchEventKind, NoticeLine(s, conversation, tone), s.playerId);
                return;
            }

            // Every Listen, caught or not, is this conversation's one: its coin always lands the same way.
            Cool(s, HeardPrefix + sequence.ToString(CultureInfo.InvariantCulture), s.week + 1);
            string key = OverhearKey(sequence);
            if (StoryRandom.Chance(s, key, CaughtChance(s, first, second)))
            {
                var catcher = StoryRandom.Index(s, key + ":who", 2) == 0 ? first : second;
                AddStanding(s, first.id, second.id, ClaimSource.Missed, 0);
                ChangeKeyed(s, s.playerId, catcher.id, CaughtListeningImpact, key + ":caught", catcher.name + " caught you listening in", "eavesdrop");
                Remember(s, catcher.id, s.playerId, "I caught you listening to a conversation you were not part of.", true);
                Log(s, "eavesdrop", catcher.name + " caught you listening in. That will cost you.", s.playerId, catcher.id);
                return;
            }

            double between = s.Score(first.id, second.id);
            string reading = between >= 25 ? "sounded close"
                : between <= -25 ? "sounded like they cannot stand each other"
                : "sounded careful with each other";
            AddStanding(s, first.id, second.id, ClaimSource.Overheard, between);
            string vote = OverheardVote(s, first, second);
            Remember(s, s.playerId, first.id, "I overheard " + first.name + " and " + second.name
                + " in week " + s.week + ". They " + reading + "." + vote, true);
            // One pact sentence a week (§I2): while the week's is held, the pact is not asked at all, so no knower is added.
            string pactHeld = PactHeardPrefix + s.week.ToString(CultureInfo.InvariantCulture);
            string pact = AllianceLeaks.On(s) && !Cooling(s, pactHeld) ? ListenIn(s, first, second) : null;
            if (pact != null) Cool(s, pactHeld, s.week + 1);
            var heard = AllWeekOn(s) ? OverheardAct(s, first.id, second.id, pact != null) : null;
            if (heard != null) heard.overheard = true;
            Log(s, "eavesdrop", EavesdropLine(first.name, second.name, reading, vote, heard == null ? null : ActClause(s, heard), pact), s.playerId);
        }

        /// <summary>
        /// A sighted act at 12 (§I2) is a Notice too: the pair's <c>Seen</c> row in the act's tone, sharing the Notice cap,
        /// once a pair and tone a week. The sighting's own line says it; no second line.
        /// </summary>
        private static void NoticeSighting(EpisodeState s, NpcActState act)
        {
            if (!StoryAt(s, StoryRules.Manners) || act == null) return;
            int tone = HunchTone.OfAct(act.kind);
            if (NoticedThisWeek(s) >= NoticesAWeek || NoticedThisWeek(s, act.actorId, act.partnerId, tone)) return;
            AddStanding(s, act.actorId, act.partnerId, ClaimSource.Seen, tone);
        }
    }
}
