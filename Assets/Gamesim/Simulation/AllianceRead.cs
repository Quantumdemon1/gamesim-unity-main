using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What the player can know about alliances, for the notebook's alliances page
    /// (ACTIONS-DEALS-ALLIANCES-PLAN V3): their own pacts in full, and the pacts between other
    /// houseguests they have evidence of - and nothing else.
    ///
    /// <para><b>The player's own pacts</b> are theirs to see whole: who is in them, when the player
    /// came into each and how, whether it stands and, if not, when it ended and why, every call the
    /// player made in it with who followed and who did not (<see cref="SeasonLedger.calls"/>, which
    /// the call named to the player as it was made), the deals the player struck with its members,
    /// and the player's own reading of each member. How strong a pact is shows only through those:
    /// the player's own scores and the calls on the record. A member's view of the player, or of the
    /// pact, is never read. Why one ended is the line the engine told the player that week, while
    /// the log holds it and nothing else of theirs could have been meant by it. Past that, the
    /// ledger's reason is not taken at its word: "turned", "soured" and "ended" are all worked out
    /// from views the player never sees, so all three read one neutral line, <see cref="JustEnded"/>.
    /// Only a departure, which is public, is named, and only who was gone by then. A pact the player
    /// was brought into is dated by the invitation, as <see cref="FinalistRead.PlayerAlliedSince"/>
    /// dates it: the week its members made it is a week the player was never told.</para>
    ///
    /// <para><b>Another houseguest's pact</b> shows only on evidence the player holds: its own fact
    /// with the player among the knowers, or out in the open (<see cref="FinalistRead.AllianceCertainty"/>),
    /// or a line the player was shown that names everyone in it - a play's receipt, "You learned: ...
    /// are working together." The whisper that told the player dates a pact they know of; it never
    /// makes one known, because its two names could belong to more than one pact. A pact with no
    /// evidence never appears, not even as an unknown, and nothing the player cannot know of one is
    /// said either: not its name, not when it formed, not whether it still stands. One card is one
    /// set of people, however many records share it, so two pacts of the same people read as one and
    /// say nothing more. No listen-in, read or claim names a pact today: an overheard pair is a
    /// standing, and <see cref="ClaimSource.Ally"/> is declared and never written. When one does,
    /// it is evidence here.</para>
    ///
    /// <para>Pure and read-only, like <see cref="FinalistRead"/>: it neither mutates the state nor
    /// draws from its generator, and it lives in the simulation so the Unity-free subset tests it.</para>
    /// </summary>
    public static class AllianceRead
    {
        // ------------------------------------------------------------ the words

        /// <summary>
        /// Why one of the player's pacts ended, in the words they were told; <see cref="JustEnded"/>
        /// where the line has left the log, whatever the ledger's private reason.
        /// </summary>
        public const string FellApart = "It fell apart.", YouLeft = "You left it.", CalledOff = "It was called off.",
            JustEnded = "It ended.";
        /// <summary>How the player came into a pact whose invitation the record no longer holds.</summary>
        public const string BroughtIn = "You were brought into it.";
        /// <summary>A suspected pact's evidence when the line that told the player has left the record.</summary>
        public const string HeardOfIt = "You have heard they are working together.";
        /// <summary>A pact whose fact is out in the open. Tenseless: it says the house knows, not that the pact still stands.</summary>
        public const string OutInTheOpen = "The whole house knows about it.";
        /// <summary>The player's own reading of somebody, in the words the web uses (less "Allied", which every member is).</summary>
        public const string Friendly = "Friendly", Neutral = "Neutral", Wary = "Wary", Hostile = "Hostile";

        // ------------------------------------------------------------ what it returns

        /// <summary>The page: the player's pacts, standing ones first, then the others they have evidence of.</summary>
        public sealed class Page
        {
            public List<Pact> yours = new List<Pact>();
            public List<SuspectedPact> suspected = new List<SuspectedPact>();
        }

        /// <summary>One of the player's own pacts, whole.</summary>
        public sealed class Pact
        {
            public string id, name;
            public bool active;
            /// <summary>Everyone in it but the player, in the pact's own order, the departed included.</summary>
            public List<Member> members = new List<Member>();
            /// <summary>The week the player has been in it since, as the player knows it; 0 when the record cannot say.</summary>
            public int formedWeek;
            /// <summary>How it came about, as a sentence ("You invited Riley."), or null when the record holds nothing.</summary>
            public string formed;
            /// <summary>The week it ended, or 0 while it stands (or when the record cannot say).</summary>
            public int endedWeek;
            /// <summary>Why it ended, in the words the player was told; null while it stands.</summary>
            public string ended;
            /// <summary>Every call the player made in it, oldest first.</summary>
            public List<Call> calls = new List<Call>();
            /// <summary>The deals the player agreed with its members, oldest first.</summary>
            public List<Deal> deals = new List<Deal>();
        }

        /// <summary>A member of one of the player's pacts, as the player reads them.</summary>
        public sealed class Member
        {
            public string id, name;
            public ContestantStatus status;
            public bool inHouse;
            /// <summary>The player's own reading of them: <see cref="Friendly"/>, <see cref="Neutral"/>, <see cref="Wary"/> or <see cref="Hostile"/>.</summary>
            public string reading;
            /// <summary>The calls in this pact they followed, and the ones they did not.</summary>
            public int followed, ignored;
        }

        /// <summary>A call the player made in a pact: whom they named, who followed and who did not.</summary>
        public sealed class Call
        {
            public int week;
            public string targetId;
            public List<string> followed = new List<string>(), defected = new List<string>();
        }

        /// <summary>A deal the player agreed with a member: the week, with whom, what kind and where it stands.</summary>
        public sealed class Deal
        {
            public int week;
            public string withId, type, status;
            /// <summary>"Final Two Deal with Riley · agreed".</summary>
            public string text;
        }

        /// <summary>
        /// A pact between other houseguests the player has evidence of: who is in it, how sure the
        /// player can be (<see cref="FinalistRead.Confirmed"/> when the house knows,
        /// <see cref="FinalistRead.Suspected"/> when the player only heard), and what they saw or heard.
        /// </summary>
        public sealed class SuspectedPact
        {
            /// <summary>The members' ids, sorted: one card is one set of people.</summary>
            public string key;
            /// <summary>The members, in cast order.</summary>
            public List<string> memberIds = new List<string>();
            public string certainty;
            /// <summary>What the player saw or heard, dated lines first; never empty.</summary>
            public List<Evidence> evidence = new List<Evidence>();
        }

        /// <summary>One thing the player saw or heard: the week, or 0 where the record no longer says, and the line.</summary>
        public sealed class Evidence
        {
            public int week;
            public string text;
        }

        // ------------------------------------------------------------ the page

        public static Page Read(EpisodeState s) => new Page { yours = Yours(s), suspected = Suspected(s) };

        /// <summary>Every pact the player is in or was in: the standing ones first, each in the order the house made them.</summary>
        public static List<Pact> Yours(EpisodeState s)
        {
            var pacts = new List<Pact>();
            if (s?.alliances == null || string.IsNullOrEmpty(s.playerId)) return pacts;
            var ours = s.alliances.Where(a => a?.members != null && a.members.Contains(s.playerId)).ToList();
            foreach (var alliance in ours.Where(a => a.active).Concat(ours.Where(a => !a.active)))
                pacts.Add(Describe(s, alliance));
            return pacts;
        }

        /// <summary>
        /// The player's own reading of somebody: their own score toward them, in the web's bands.
        /// Never "Allied": inside a pact every member is, and the word would hide the reading.
        /// </summary>
        public static string ReadingWord(EpisodeState s, string otherId)
        {
            if (s == null || string.IsNullOrEmpty(otherId) || otherId == s.playerId) return Neutral;
            double score = s.Score(s.playerId, otherId);
            if (score >= FinalistRead.FriendThreshold) return Friendly;
            if (score <= FinalistRead.RivalThreshold) return Hostile;
            if (score <= FinalistRead.DistrustThreshold) return Wary;
            return Neutral;
        }

        private static Pact Describe(EpisodeState s, AllianceState alliance)
        {
            var row = s.ledger?.alliances?.FirstOrDefault(r => r != null && r.id == alliance.id);
            var calls = (s.ledger?.calls ?? new List<BlocCallRow>())
                .Where(c => c != null && c.allianceId == alliance.id && c.callerId == s.playerId)
                .OrderBy(c => c.week).ToList();
            var pact = new Pact { id = alliance.id, name = alliance.name, active = alliance.active };
            foreach (var id in alliance.members.Where(id => id != s.playerId))
            {
                var actor = s.Find(id);
                pact.members.Add(new Member
                {
                    id = id, name = actor?.name ?? id, status = actor?.status ?? ContestantStatus.Evicted,
                    inHouse = actor != null && actor.status == ContestantStatus.Active,
                    reading = ReadingWord(s, id),
                    followed = calls.Count(c => c.followed != null && c.followed.Contains(id)),
                    ignored = calls.Count(c => c.defected != null && c.defected.Contains(id)),
                });
            }
            foreach (var call in calls)
                pact.calls.Add(new Call
                {
                    week = call.week, targetId = call.targetId,
                    followed = new List<string>(call.followed ?? new List<string>()),
                    defected = new List<string>(call.defected ?? new List<string>()),
                });
            Formed(s, alliance, row, pact);
            if (!alliance.active) Ended(s, alliance, row, pact);
            pact.deals = Deals(s, alliance);
            return pact;
        }

        /// <summary>
        /// When the player came into it, and how, by the rules the finalist cards date it by
        /// (<see cref="FinalistRead.PlayerAlliedSince"/>). A pact the player made began with them,
        /// so its ledger row's week is theirs: by an invitation either way that week, by a story's
        /// beat - said in the story's own words - or by proposing it. One they were brought into was
        /// made by others first, in a week the player was never told; it is dated by the player's own
        /// "brought you into" line while the log still holds it, and by nothing once it does not.
        /// </summary>
        private static void Formed(EpisodeState s, AllianceState alliance, AllianceRow row, Pact pact)
        {
            var firsts = pact.members.Select(m => FinalistRead.FirstName(m.name)).ToList();
            pact.formedWeek = FinalistRead.PlayerAlliedSince(s, alliance);
            // With no ledger row (an old save) the order is all there is: the player's pacts open with them.
            bool founder = row != null ? FinalistRead.FoundedByPlayer(s, alliance, row)
                : alliance.members.Count > 0 && alliance.members[0] == s.playerId;
            if (!founder)
            {
                var invitation = FinalistRead.BroughtInLine(s, alliance, row);
                string joined = " brought you into " + alliance.name + ".";
                pact.formed = invitation != null
                    ? FinalistRead.FirstName(invitation.text.Substring(0, invitation.text.Length - joined.Length)) + " brought you in."
                    : BroughtIn;
                return;
            }
            string with = "You formed it with " + Join(firsts) + ".";
            if (row == null || row.startedWeek <= 0) { pact.formed = with; return; }
            if (row.why != null && row.why.StartsWith("story", StringComparison.Ordinal))
            {
                // A story's pact is said the way the story said it: "Riley and Jo let you in" is not
                // a pact the player formed, and "you proposed a three-way alliance" is.
                pact.formed = StoryOutcome(s, alliance, row.startedWeek) ?? "It came together in a story, with " + Join(firsts) + ".";
                return;
            }
            // An invitation the player put or accepted, agreed the week it began: that is how. Under the
            // commitment rules an agreed invitation - open-ended, where an unanswered one keeps its own
            // week - ends when a member is evicted (C1, X4), and is still how the pact began.
            bool rules = EpisodeEngine.CommitmentRulesOn(s);
            var invite = s.deals.LastOrDefault(d => d != null && d.type == DealKind.AllianceInvite && d.week == row.startedWeek
                && (d.status == DealStatus.Active || d.status == DealStatus.Accepted || d.status == DealStatus.Fulfilled || d.status == DealStatus.Broken
                    || (rules && d.status == DealStatus.Expired && d.expiresWeek == 0))
                && ((d.proposerId == s.playerId && alliance.members.Contains(d.recipientId))
                    || (d.recipientId == s.playerId && alliance.members.Contains(d.proposerId))));
            if (invite != null)
            {
                string other = First(s, invite.proposerId == s.playerId ? invite.recipientId : invite.proposerId);
                pact.formed = invite.proposerId == s.playerId ? "You invited " + other + "." : other + " invited you.";
                return;
            }
            pact.formed = with;
        }

        /// <summary>
        /// The line a story showed the player when its beat made the pact: the step that week whose
        /// option formed an alliance of exactly these people, in the story's own words ("Riley and
        /// Jo let you in. The three of you are a bloc now."). Null when no step on the record did.
        /// </summary>
        private static string StoryOutcome(EpisodeState s, AllianceState alliance, int week)
        {
            foreach (var cycle in s.storylines ?? new List<StorylineState>())
            {
                if (cycle?.path == null) continue;
                var arc = StoryCatalog.Find(cycle.templateId);
                if (arc == null) continue;
                foreach (var step in cycle.path.Where(p => p != null && p.week == week && p.result != StoryResults.Backfire))
                {
                    var option = arc.Beat(step.beatId)?.Option(step.optionId);
                    if (option?.effects == null || string.IsNullOrEmpty(option.outcome)) continue;
                    foreach (var fx in option.effects.Where(f => f != null && f.kind == StoryEffects.Alliance))
                    {
                        var ids = new[] { fx.from, fx.to, fx.third }.Where(role => !string.IsNullOrEmpty(role))
                            .Select(role => Cast(s, cycle, role)).ToList();
                        if (ids.Any(id => id == null) || ids.Distinct().Count() != alliance.members.Count || !ids.All(alliance.members.Contains)) continue;
                        return StoryText.Fill(s, option.outcome, cycle.cast);
                    }
                }
            }
            return null;
        }

        /// <summary>Who a story's role names in a cycle: the player, a named houseguest, or whoever the cycle cast in it.</summary>
        private static string Cast(EpisodeState s, StorylineState cycle, string role)
        {
            if (role == Fx.Player) return s.playerId;
            if (role.StartsWith("@", StringComparison.Ordinal)) return role.Substring(1);
            return cycle.cast?.FirstOrDefault(r => r != null && r.role == role)?.contestantId;
        }

        /// <summary>
        /// When it ended and why. The engine tells the player of every pact of theirs that ends, the
        /// week it ends, and that line - while the log holds it - says why. Past that, the ledger's
        /// reason speaks only where it is public: a departure. "Turned", "soured" and "ended" are all
        /// worked out from views the player never sees, so all three read <see cref="JustEnded"/>;
        /// telling them apart would tell the player what a partner privately thinks.
        /// </summary>
        private static void Ended(EpisodeState s, AllianceState alliance, AllianceRow row, Pact pact)
        {
            pact.endedWeek = row != null && row.endedWeek > 0 ? row.endedWeek : 0;
            pact.ended = Told(s, alliance, pact.endedWeek);
            if (pact.ended != null) return;
            string why = row?.why != null && row.why.Contains("/") ? row.why.Substring(row.why.IndexOf('/') + 1) : null;
            pact.ended = (why == "left-house" ? LeftTheHouse(s, alliance, pact.endedWeek) : null) ?? JustEnded;
        }

        /// <summary>
        /// The line the player read the week the pact ended, as the page says it, or null when the
        /// record holds none that can only have meant this pact. A line naming the pact itself comes
        /// first, then one naming everyone in it, then the player's own walk-out - which names a
        /// single member, so it is this pact's only when no other pact of the player's with that
        /// member ended the same week. Without a ledger row the week is unknown, and only a line
        /// naming the pact itself, by a name no other ended pact of theirs shares, is read.
        /// </summary>
        private static string Told(EpisodeState s, AllianceState alliance, int endedWeek)
        {
            var partners = alliance.members.Where(id => id != s.playerId).Select(id => s.Find(id)).Where(a => a != null).ToList();
            if (partners.Count == 0) return null;
            // The player's other pacts that ended that week - or, where a ledger never dated them, might have.
            var rivals = s.alliances.Where(a => a != null && a != alliance && !a.active && a.members != null && a.members.Contains(s.playerId)
                && (endedWeek == 0 || EndedWeek(s, a) == endedWeek || EndedWeek(s, a) == 0)).ToList();
            var lines = new HashSet<string>(Seen(s).Where(e => e.kind == "alliance" && e.text != null && (endedWeek == 0 || e.week == endedWeek))
                .Select(e => e.text), StringComparer.Ordinal);

            if (!rivals.Any(a => a.name == alliance.name))
            {
                if (lines.Contains(alliance.name + " is finished.")) return CalledOff;
                var leaver = endedWeek == 0 ? null : partners.FirstOrDefault(a => lines.Contains(a.name + " is out of " + alliance.name + "."));
                if (leaver != null) return FinalistRead.FirstName(leaver.name) + " is out of it.";
            }
            if (endedWeek == 0) return null;
            string names = string.Join(" and ", partners.Select(a => a.name));
            if (!rivals.Any(a => SamePartners(s, a, alliance)))
            {
                if (lines.Contains("Your alliance with " + names + " has fallen apart.")) return FellApart;
                if (lines.Contains(names + (partners.Count == 1 ? " has" : " have") + " left the house, and your alliance has ended."))
                    return Join(partners.Select(a => FinalistRead.FirstName(a.name)).ToList()) + " left the house.";
            }
            if (partners.Any(a => lines.Contains("You left the alliance with " + a.name + ".") && !rivals.Any(r => r.members.Contains(a.id))))
                return YouLeft;
            return null;
        }

        private static int EndedWeek(EpisodeState s, AllianceState alliance) =>
            s.ledger?.alliances?.FirstOrDefault(r => r != null && r.id == alliance.id)?.endedWeek ?? 0;

        private static bool SamePartners(EpisodeState s, AllianceState a, AllianceState b) =>
            new HashSet<string>(a.members.Where(id => id != s.playerId)).SetEquals(b.members.Where(id => id != s.playerId));

        /// <summary>
        /// Who had left the house by the week a pact ended, for one the ledger says ended with a
        /// departure: every partner, when all of them were gone by then (the engine ends a pact of
        /// the player's when nobody else in it is left in the house), or else the player. Null -
        /// and the neutral line - where the record cannot say who was gone by then: the ledger's
        /// "left-house" outranks every other reason, so a partner gone weeks earlier does not make
        /// a later souring a departure.
        /// </summary>
        private static string LeftTheHouse(EpisodeState s, AllianceState alliance, int endedWeek)
        {
            if (endedWeek <= 0) return null;
            var partners = alliance.members.Where(id => id != s.playerId).ToList();
            if (partners.Count > 0 && partners.All(id => GoneBy(s, id, endedWeek)))
                return Join(partners.Select(id => First(s, id)).ToList()) + " left the house.";
            return GoneBy(s, s.playerId, endedWeek) ? "You left the house." : null;
        }

        /// <summary>Whether somebody had left the house by a week, on the public record: evicted (that week's power row) or removed by production.</summary>
        private static bool GoneBy(EpisodeState s, string id, int week)
        {
            var who = s.Find(id);
            if (who == null || who.status == ContestantStatus.Active) return false;
            int? left = s.ledger?.power?.LastOrDefault(p => p != null && p.evicteeId == id)?.week
                ?? s.story?.removals?.FirstOrDefault(r => r != null && r.contestantId == id)?.week;
            return left.HasValue && left.Value <= week;
        }

        /// <summary>The deals the player agreed with the pact's members: agreed, kept or broken. Offers and what lapsed are the notes' business.</summary>
        private static List<Deal> Deals(EpisodeState s, AllianceState alliance)
        {
            var others = alliance.members.Where(id => id != s.playerId).ToList();
            // Under the commitment rules a deal the vote settled - a partnership too (C1) - says how it
            // ended once the player knows the ballot that settled it (KnownBallots).
            bool rules = EpisodeEngine.CommitmentRulesOn(s);
            return s.deals.Where(d => d != null && d.type != DealKind.AllianceInvite
                    && (d.status == DealStatus.Accepted || d.status == DealStatus.Active || d.status == DealStatus.Fulfilled || d.status == DealStatus.Broken)
                    && ((d.proposerId == s.playerId && others.Contains(d.recipientId)) || (d.recipientId == s.playerId && others.Contains(d.proposerId))))
                .OrderBy(d => d.week)
                .Select(d =>
                {
                    string with = d.proposerId == s.playerId ? d.recipientId : d.proposerId;
                    string about = string.IsNullOrEmpty(d.targetId) ? "" : ", on " + (d.targetId == s.playerId ? "you" : First(s, d.targetId));
                    string standing = rules && !KnownBallots.DealOutcomeKnown(s, d) ? KnownBallots.Unresolved : HouseguestNotes.DealStanding(d.status, d.proposerId == with);
                    return new Deal
                    {
                        week = d.week, withId = with, type = d.type, status = d.status,
                        text = DealKind.Title(d.type) + " with " + First(s, with) + about + " · " + standing,
                    };
                }).ToList();
        }

        // ------------------------------------------------------------ the pacts the player has evidence of

        /// <summary>
        /// The pacts between other houseguests the player has evidence of, one card a set of people,
        /// the earliest news first. Empty when the player knows of none - which is not the same as
        /// there being none.
        /// </summary>
        public static List<SuspectedPact> Suspected(EpisodeState s)
        {
            var cards = new List<SuspectedPact>();
            if (s?.alliances == null || string.IsNullOrEmpty(s.playerId)) return cards;
            var seen = Seen(s).ToList();
            foreach (var alliance in s.alliances)
            {
                if (alliance?.members == null || alliance.members.Count < 2 || alliance.members.Contains(s.playerId)) continue;
                string certainty = FinalistRead.AllianceCertainty(s, alliance);
                var evidence = new List<Evidence>();
                // A play's receipt names everyone in it: what the player was shown, said as it was shown.
                string learned = LearnedLine(s, alliance);
                foreach (var e in seen.Where(e => e.kind == StoryLog.Receipt && e.text == learned))
                    evidence.Add(new Evidence { week = e.week, text = e.text });
                if (certainty != null)
                {
                    // The whisper that told them dates a pact they know of; on its own it proves nothing.
                    var fact = Knowledge.Of(s, FactKinds.Alliance, alliance.id);
                    if (fact != null)
                    {
                        string whisper = WhisperLine(s, fact);
                        foreach (var e in seen.Where(e => e.kind == StoryLog.Whisper && e.text == whisper))
                            evidence.Add(new Evidence { week = e.week, text = e.text });
                    }
                    if (certainty == FinalistRead.Confirmed) evidence.Add(new Evidence { text = OutInTheOpen });
                    else if (evidence.Count == 0) evidence.Add(new Evidence { text = HeardOfIt });
                }
                // No fact the player holds and nothing they were shown: the pact is not theirs to see.
                if (certainty == null && evidence.Count == 0) continue;

                string key = string.Join("|", alliance.members.OrderBy(id => id, StringComparer.Ordinal));
                var card = cards.FirstOrDefault(c => c.key == key);
                if (card == null)
                {
                    card = new SuspectedPact
                    {
                        key = key, certainty = certainty ?? FinalistRead.Suspected,
                        memberIds = s.contestants.Where(c => alliance.members.Contains(c.id)).Select(c => c.id).ToList(),
                    };
                    cards.Add(card);
                }
                else if (certainty == FinalistRead.Confirmed) card.certainty = FinalistRead.Confirmed;
                foreach (var line in evidence)
                    if (!card.evidence.Any(x => x.week == line.week && x.text == line.text)) card.evidence.Add(line);
            }
            foreach (var card in cards)
            {
                // A dated line says it better than the filler that stands in for a line the log let go.
                if (card.evidence.Any(x => x.text != HeardOfIt)) card.evidence.RemoveAll(x => x.text == HeardOfIt);
                card.evidence = card.evidence.OrderBy(x => x.week > 0 ? 0 : 1).ThenBy(x => x.week).ToList();
            }
            return cards.OrderBy(c => c.evidence.Where(x => x.week > 0).Select(x => x.week).DefaultIfEmpty(int.MaxValue).Min())
                .ThenBy(c => Join(c.memberIds.Select(id => s.Find(id)?.name ?? id).ToList()), StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The pairs the relationship web may join with a suspected pact's marker: every two members
        /// of a pact the player has evidence of who are both still in the house, each pair once.
        /// </summary>
        public static List<(string first, string second)> SuspectedPairs(EpisodeState s)
        {
            var pairs = new List<(string first, string second)>();
            if (s == null) return pairs;
            foreach (var card in Suspected(s))
            {
                var here = card.memberIds.Where(id => s.Find(id)?.status == ContestantStatus.Active).ToList();
                for (int i = 0; i < here.Count; i++)
                    for (int j = i + 1; j < here.Count; j++)
                        if (!pairs.Any(p => (p.first == here[i] && p.second == here[j]) || (p.first == here[j] && p.second == here[i])))
                            pairs.Add((here[i], here[j]));
            }
            return pairs;
        }

        /// <summary>A play's receipt for a pact leaked to the player or let out: <see cref="PlayReceipts"/>' own words.</summary>
        public static string LearnedLine(EpisodeState s, AllianceState alliance) =>
            "You learned: " + Names(s, alliance.members) + " are working together.";

        /// <summary>The line a pact's fact reaches the player in through the house: the engine's own whisper, word for word.</summary>
        public static string WhisperLine(EpisodeState s, HouseFactState fact)
        {
            string actor = s.Find(fact.actorId)?.name ?? "somebody", subject = s.Find(fact.subjectId)?.name ?? "somebody";
            return "Word in the house: " + actor + " and " + subject + " are working together.";
        }

        // ------------------------------------------------------------ helpers

        /// <summary>The lines the player saw: their own, and the house's.</summary>
        private static IEnumerable<EpisodeEvent> Seen(EpisodeState s) =>
            (s.events ?? new List<EpisodeEvent>()).Where(e => e != null
                && (e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(s.playerId)));

        private static string First(EpisodeState s, string id) => FinalistRead.FirstName(s.Find(id)?.name ?? id);

        /// <summary>"Riley, Jo and Sam": full names in the given order, as the receipts say them.</summary>
        private static string Names(EpisodeState s, IList<string> ids)
        {
            var names = ids.Select(id => s.Find(id)?.name).Where(n => n != null).ToList();
            if (names.Count <= 1) return names.FirstOrDefault() ?? "someone";
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
        }

        /// <summary>"Riley", "Riley and Jo", "Riley, Jo and Sam".</summary>
        public static string Join(IList<string> words)
        {
            if (words == null || words.Count == 0) return string.Empty;
            if (words.Count == 1) return words[0];
            return string.Join(", ", words.Take(words.Count - 1)) + " and " + words[words.Count - 1];
        }
    }
}
