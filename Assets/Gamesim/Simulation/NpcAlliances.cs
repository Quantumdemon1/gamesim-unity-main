using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Houseguests pairing up and falling out without the player in the room.
    ///
    /// <para>Until this existed, <c>alliances.Add</c> appeared exactly once in the whole simulation,
    /// on the player's own social path. No houseguest had ever formed an alliance with another
    /// houseguest in any season this project had run — which meant the voting-bloc system, one of
    /// the most substantial and best-tested things here, had only ever coordinated blocs the player
    /// personally built.</para>
    ///
    /// <para>Ported from the reference build's <c>evaluateAllianceDesire</c>. The standout term is
    /// <b>shared threats</b>: two people who both dislike the same third party are drawn together,
    /// fifteen points each. That is the enemy-of-my-enemy rule, and it is what makes a house form
    /// factions around a common problem rather than simply pairing off its friendliest members.</para>
    ///
    /// <para><b>Nothing here draws from the season's generator.</b> Every term is a function of
    /// state, so the whole pass is deterministic without spending a roll — which matters because a
    /// roll spent here would shift every competition and vote after it. The one thing it cannot
    /// avoid changing is the state itself: a season in which houseguests form alliances is a
    /// different season, and that is the point of it.</para>
    /// </summary>
    public static class NpcAlliances
    {
        /// <summary>The source's <c>NPC_ALLIANCE_MIN_RELATIONSHIP</c>: a floor, not a score.</summary>
        public const double MinimumRelationship = 25;

        /// <summary>The source's <c>NPC_ALLIANCE_MAX_PER_PERSON</c>.</summary>
        public const int MaximumEach = 3;

        /// <summary>Desire must clear this as well as the relationship floor.</summary>
        public const double ProposeThreshold = 25;

        /// <summary>
        /// What counts as disliking somebody, and so as a shared threat.
        ///
        /// <para>The source's own line, taken from the shared-threat term rather than invented: it
        /// counts a third party when both parties rate them below −20.</para>
        /// </summary>
        public const double DislikeLine = -20;

        /// <summary>
        /// How badly a pair has to sour before their alliance falls apart.
        ///
        /// <para>The source's <c>DISSOLUTION_THRESHOLD</c> in <c>alliance-system.ts</c>, read from
        /// the reference repository rather than from the design document — which describes the rule
        /// but never gives the number. It was authored here as a guess that reused the dislike line
        /// above, and the guess was right; only the comparison was wrong. The source dissolves on
        /// <c>score &lt; -20</c>, so a pair sitting exactly on the line stays allied.</para>
        /// </summary>
        public const double SourLine = -20;

        /// <summary>
        /// How much one houseguest wants to work with another.
        ///
        /// <para>The source's formula exactly: relationship at 0.4, shared threats at 15 apiece,
        /// strategic value at 0.15, trust either side of neutral at 0.3, less ten for every alliance
        /// already carried — diminishing returns on over-allying.</para>
        ///
        /// <para>In an All-Star season from alumni rules 4 (ALL-STAR-HISTORIES-PLAN §14.E.1) two houseguests outside a
        /// known pair of past games want each other more, for each pair they face
        /// (<see cref="KnownBlocs.CounterDesire"/>): the enemy-of-my-enemy rule again, for a bloc the house knows of from
        /// the record rather than from a quarrel. Added only where it is not nought, so every other season reads the
        /// source's formula bit for bit. The floor, the threshold and the pact cap below are untouched.</para>
        /// </summary>
        public static double Desire(EpisodeState state, string npcId, string targetId)
        {
            var target = state.Find(targetId);
            if (target == null) return 0;

            double relationship = state.Score(npcId, targetId);
            int sharedThreats = state.Active.Count(other =>
                other.id != npcId && other.id != targetId &&
                state.Score(npcId, other.id) < DislikeLine &&
                state.Score(targetId, other.id) < DislikeLine);
            double strategicValue = (target.hohWins + target.vetoWins) * 5 + target.stats.social * 3;
            double trust = ThreatAssessment.TrustScore(state, targetId, npcId);
            int carried = ActiveAlliancesFor(state, npcId).Count;

            double desire = relationship * 0.4
                   + sharedThreats * 15
                   + strategicValue * 0.15
                   + (trust - ThreatAssessment.NeutralTrust) * 0.3
                   - carried * 10
                   // Under agency, who they take to (NPC-AGENCY-PLAN.md §3.3): temperament, on top of the web's terms.
                   + (EpisodeEngine.AgencyOn(state) ? EpisodeEngine.DesirePerPoint * TraitAffinity.Compatibility(state.Find(npcId), target) : 0);
            // The known pairs, from alumni rules 4 (§14.E.1): nought below it, and for a pair's members and their partners.
            double counterBloc = KnownBlocs.CounterDesire(state, npcId, targetId);
            return counterBloc != 0 ? desire + counterBloc : desire;
        }

        /// <summary>
        /// The pact cap under agency (NPC-AGENCY-PLAN.md §3.4): one pact among houseguests per three houseguests in the house.
        /// It counts pacts, not members: a join from alumni rules 5 (<see cref="TryJoin"/>, ALL-STAR-HISTORIES-PLAN §14.L)
        /// adds no pact and takes no place under it.
        /// </summary>
        public static int PactCap(EpisodeState state) => state.Active.Count(c => !c.isPlayer) / 3;

        /// <summary>The active pacts with no player in them and two members still in the house.</summary>
        public static List<AllianceState> NpcOnlyPacts(EpisodeState state) =>
            state.alliances.Where(a => a.active && !a.members.Contains(state.playerId) && Intact(state, a)).ToList();

        /// <summary>
        /// Whether two houseguests may start a pact under the cap: neither already in one of their own, and the house below its
        /// share. A new pact only: joining one that stands is the separate <see cref="TryJoin"/> (from alumni rules 5).
        /// </summary>
        public static bool PactRoom(EpisodeState state, string npcId, string targetId)
        {
            var pacts = NpcOnlyPacts(state);
            return pacts.Count < PactCap(state) && !pacts.Any(a => a.members.Contains(npcId) || a.members.Contains(targetId));
        }

        /// <summary>Whether this houseguest would put the idea to that one.</summary>
        public static bool WouldPropose(EpisodeState state, string npcId, string targetId)
        {
            if (npcId == targetId) return false;
            if (state.Find(npcId)?.status != ContestantStatus.Active) return false;
            if (state.Find(targetId)?.status != ContestantStatus.Active) return false;
            if (state.Allied(npcId, targetId)) return false;
            if (ActiveAlliancesFor(state, npcId).Count >= MaximumEach) return false;
            if (ActiveAlliancesFor(state, targetId).Count >= MaximumEach) return false;
            if (GrudgeBetween(state, npcId, targetId)) return false;
            // The pact cap (NPC-AGENCY-PLAN.md §3.4): pacts among houseguests stay few enough to read.
            // The player's pacts are outside it, as a story's are.
            if (EpisodeEngine.AgencyOn(state) && npcId != state.playerId && targetId != state.playerId && !PactRoom(state, npcId, targetId))
                return false;
            return Warm(state, npcId, targetId);
        }

        /// <summary>
        /// Whether a member of a pact would have this houseguest brought into it (ACTIONS-DEALS-ALLIANCES-PLAN
        /// C5, "Bring {name} into {pact}"): the web's own bar for a pact of more than two
        /// (<c>AllianceManager.tsx</c>: nobody in it hostile to anybody else in it), which the port's
        /// <c>AllyThroughInvitation</c> keeps already - the member is not below
        /// <see cref="StrategyRules.HostilityLine"/> toward the newcomer - and the web's grudge rule either
        /// way. Not <see cref="WouldPropose"/>'s desire: a pact that exists is not a new one the member
        /// would have to want, and in a house where houseguests warm to each other slowly that bar let
        /// almost nobody in (the lead's ruling). The newcomer's own three pacts are hers to answer for,
        /// with her answer (<c>EpisodeEngine.BringIntoAlliance</c>). No roll: pure, like the rest of this class.
        /// </summary>
        public static bool WouldWelcome(EpisodeState state, string memberId, string inviteeId)
        {
            if (memberId == inviteeId) return false;
            if (state.Find(memberId)?.status != ContestantStatus.Active) return false;
            if (state.Find(inviteeId)?.status != ContestantStatus.Active) return false;
            if (GrudgeBetween(state, memberId, inviteeId)) return false;
            return state.Score(memberId, inviteeId) >= StrategyRules.HostilityLine;
        }

        /// <summary>
        /// The web's rule that a grudge blocks an alliance: nobody offers a pact to somebody they hold
        /// forty or more against, or who holds that against them. Behind the story boundary, where
        /// grudges exist at all.
        /// </summary>
        private static bool GrudgeBetween(EpisodeState state, string a, string b) =>
            EpisodeEngine.StoryAt(state, StoryRules.Grudges)
            && (Grudges.Severity(state, a, b) >= EpisodeEngine.AllianceGrudgeLine
                || Grudges.Severity(state, b, a) >= EpisodeEngine.AllianceGrudgeLine);

        /// <summary>Whether one houseguest wants the other: the relationship floor and the desire threshold, both.</summary>
        private static bool Warm(EpisodeState state, string npcId, string targetId) =>
            state.Score(npcId, targetId) >= MinimumRelationship
            && Desire(state, npcId, targetId) > ProposeThreshold;

        /// <summary>
        /// A pact a story made: two to four houseguests, no roll, the same record the weekly pass
        /// writes for every pair in it. Public because the private pair-only <see cref="Form"/>
        /// cannot make a three-person pact, and a story's "let's make it official" sometimes needs
        /// one. Returns null when it cannot be formed. A story's pact never grows by <see cref="TryJoin"/>:
        /// only the houseguests' own (<see cref="HouseguestsOwn"/>) do, born as pairs, from alumni rules 5.
        /// </summary>
        public static AllianceState FormFromStory(EpisodeState state, List<string> members)
        {
            if (state == null || members == null) return null;
            var ids = members.Where(id => state.Find(id)?.status == ContestantStatus.Active).Distinct().ToList();
            if (ids.Count < 2 || ids.Count > 4) return null;
            if (state.alliances.Any(a => a.active && ids.All(a.members.Contains))) return null;
            if (state.alliances.Count >= 100) return null;
            string name = ids.Count == 2
                ? "The " + First(state, ids[0]) + " and " + First(state, ids[1]) + " Pact"
                : "The " + string.Join(", ", ids.Take(ids.Count - 1).Select(id => First(state, id)))
                  + " and " + First(state, ids.Last()) + " Pact";
            var alliance = new AllianceState
            {
                id = "alliance-story-" + state.nextSequence++, name = name.Length > 100 ? name.Substring(0, 100) : name,
                members = ids, active = true,
            };
            // Under the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN C5) a story's pact with the player in
            // it never takes the name of one the player stands in already: the captions name them apart.
            if (EpisodeEngine.CommitmentRulesOn(state) && ids.Contains(state.playerId)) alliance.name = PactNames.Unique(state, alliance.name);
            state.alliances.Add(alliance);
            EpisodeEngine.AllianceFormedUnderRead(state, alliance);
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    RelationshipLedger.Record(state, ids[i], ids[j], "alliance-formed", 30, alliance.name + " was formed");
            return alliance;
        }

        /// <summary>
        /// The weekly pass: alliances that have soured fall apart, and houseguests who both want to
        /// work together start doing so.
        ///
        /// <para><b>Both parties must want it.</b> The source scores the proposer's desire and
        /// handles acceptance separately; this requires the feeling to be mutual, which is a
        /// simplification and is marked as one. It keeps the pass deterministic and one-directional
        /// desire would otherwise need an acceptance roll — a roll this cannot spend.</para>
        ///
        /// <para>At most one new alliance per houseguest per week, so a house that suddenly likes
        /// everybody does not pair off completely in a single evening.</para>
        ///
        /// <para><b>A season does not call this directly.</b> Alliance formation is the highest-priority
        /// thing a houseguest can do with a social turn, and turns are budgeted, so the weekly pass runs
        /// through <see cref="NpcSocialActions.Settle"/> — which is this loop with promises and the rest
        /// of the repertoire interleaved behind it. This stays because it is the alliance rule on its
        /// own, which is the only way to state what that rule is without the rest of the week in the
        /// way.</para>
        ///
        /// <para>From alumni rules 5 (ALL-STAR-HISTORIES-PLAN §14.L) a houseguest with no pact to start may
        /// join one that stands (<see cref="TryJoin"/>), as the beat and the social pass let them.</para>
        /// </summary>
        public static void Settle(EpisodeState state)
        {
            if (!NpcSocialState.AutonomyHasBegun(state)) return;
            Dissolve(state);

            // Cast order throughout, so the same house always pairs up the same way.
            var joined = new HashSet<string>(StringComparer.Ordinal);
            foreach (var npc in state.contestants.Where(c => c.status == ContestantStatus.Active && !c.isPlayer))
                if (!TryPropose(state, npc.id, joined)) TryJoin(state, npc.id, joined, out _);
        }

        /// <summary>
        /// One houseguest's alliance attempt for the week: the highest-desire partner who wants it
        /// back, or nothing.
        ///
        /// <para><paramref name="joined"/> is the set of people who have already paired off in this
        /// pass. It is what holds the pass to one new alliance per houseguest per week; without it a
        /// house that suddenly likes everybody pairs off completely in a single evening.</para>
        ///
        /// <para><b>Never the player.</b> The source excludes them from autonomous alliance
        /// generation outright, and the reason is worth stating: acceptance is decided here by
        /// mutual desire, and the player's side of that is computed from their scores rather than
        /// asked of them. Including them would enrol a player in an alliance they never agreed to,
        /// and the player's own <c>FormAlliance</c> is how they join one.</para>
        /// </summary>
        public static bool TryPropose(EpisodeState state, string npcId, ISet<string> joined)
        {
            if (joined != null && joined.Contains(npcId)) return false;

            string partner = state.contestants
                .Where(other => other.status == ContestantStatus.Active
                                && !other.isPlayer
                                && other.id != npcId
                                && (joined == null || !joined.Contains(other.id))
                                && WouldPropose(state, npcId, other.id)
                                && WouldPropose(state, other.id, npcId))
                .OrderByDescending(other => Desire(state, npcId, other.id))
                .ThenBy(other => other.id, StringComparer.Ordinal)
                .Select(other => other.id)
                .FirstOrDefault();

            if (partner == null) return false;
            Form(state, npcId, partner);
            if (joined != null) { joined.Add(npcId); joined.Add(partner); }
            return true;
        }

        /// <summary>The prefix of the houseguests' own pacts' ids: the weekly pass's (<see cref="Form"/>), never a story's.</summary>
        public const string HouseguestPactPrefix = "alliance-npc-";

        /// <summary>Whether a pact is one the houseguests formed themselves (<see cref="Form"/>): its id has <see cref="HouseguestPactPrefix"/>.</summary>
        public static bool HouseguestsOwn(AllianceState pact) =>
            pact?.id != null && pact.id.StartsWith(HouseguestPactPrefix, StringComparison.Ordinal);

        /// <summary>
        /// The least a joiner and every member in the house must think of each other, both ways, for a join (§14.L.D.1):
        /// one rumour (<see cref="NpcSocialActions.RumorImpact"/>) above <see cref="SourLine"/>, so a single rumour never
        /// takes a fresh edge of the pact past souring, and a cold pair is never joined. −5.
        /// </summary>
        public const double JoinWelcomeLine = SourLine - NpcSocialActions.RumorImpact;

        /// <summary>
        /// An outsider joins an outsider pact of houseguests while a known pair of past games stands (ALL-STAR-HISTORIES-PLAN
        /// §14.L, the owner's answer of 2026-10-29: "Yes, let outsider pacts grow past two"). From alumni rules 5 under
        /// agency only, with a live pair (<see cref="KnownBlocs.CounterPactBound"/> above two), and only into a pact the
        /// houseguests formed themselves (<see cref="HouseguestsOwn"/>) and nobody in the house holds with the player:
        /// <list type="bullet">
        /// <item>the pact is below the bound in the house, and kept to itself - its fact private, known to its members only,
        /// from an earlier week, and every member still in the house (so no pact grows in its founding week, nor once
        /// anybody outside it has learned of it: every reader joins the roster as it stands to its one fact; nor once it has
        /// lost a member, whom the jury's readers would still match with a later recruit);</item>
        /// <item>nobody in it stands in a live pair (<see cref="KnownBlocs.InLivePair"/>), is in
        /// <paramref name="joined"/> (one change a pact a week), or already shares a pact with the recruit; every one of
        /// them and the recruit rate each other at <see cref="JoinWelcomeLine"/> or more with no grudge either way; and no
        /// two of them and the recruit hold an active target agreement against a third of them;</item>
        /// <item>the recruit is an active houseguest, not the player, not in <paramref name="joined"/> or a live pair, in no
        /// pact of houseguests already (one each) and under <see cref="MaximumEach"/>; and they want in with one member, the
        /// sponsor - <see cref="Warm"/> toward them, a proposer's own bar - who rates them at
        /// <see cref="MinimumRelationship"/> or more.</item>
        /// </list>
        /// Of the sponsors, the recruit's highest desire, then sponsor and pact by id. The recruit goes on the end of the pact
        /// (its founder stays first and calls its bloc), learns its fact, and is on the ledger with every member in the house
        /// as a birth is ("alliance-formed", <see cref="EpisodeEngine.AllianceFormedImpact"/>, both ways); no score moves, no
        /// pact, line or name is new, and the cap, which counts pacts, takes no place. No roll: pure, like the rest of this
        /// class, and false before it reads anything below alumni rules 5.
        /// </summary>
        public static bool TryJoin(EpisodeState state, string npcId, ISet<string> joined, out string sponsorId)
        {
            sponsorId = null;
            // First, so a season below 5 reads nothing and plays byte for byte as before.
            if (!AlumniRules.At(state, AlumniRules.GrowingPacts) || !EpisodeEngine.AgencyOn(state)) return false;
            int bound = KnownBlocs.CounterPactBound(state);
            if (bound <= 2) return false;
            var recruit = state.Find(npcId);
            if (recruit == null || recruit.isPlayer || recruit.status != ContestantStatus.Active) return false;
            if ((joined != null && joined.Contains(npcId)) || KnownBlocs.InLivePair(state, npcId)) return false;
            var pacts = NpcOnlyPacts(state);
            if (pacts.Any(a => a.members.Contains(npcId))) return false;
            if (ActiveAlliancesFor(state, npcId).Count >= MaximumEach) return false;

            var pick = pacts
                .Where(p => HouseguestsOwn(p) && EpisodeEngine.InTheHouse(state, p) < bound && KeptToItself(state, p))
                .Select(p => (pact: p, here: p.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList()))
                .Where(x => x.here.All(m => !KnownBlocs.InLivePair(state, m) && (joined == null || !joined.Contains(m))
                                            && !state.Allied(npcId, m) && Welcomes(state, m, npcId) && Welcomes(state, npcId, m)))
                .Where(x => !TargetsWithin(state, x.here, npcId))
                .SelectMany(x => x.here.Where(m => Warm(state, npcId, m) && state.Score(m, npcId) >= MinimumRelationship)
                                        .Select(m => (pact: x.pact, sponsor: m, desire: Desire(state, npcId, m))))
                .OrderByDescending(c => c.desire)
                .ThenBy(c => c.sponsor, StringComparer.Ordinal)
                .ThenBy(c => c.pact.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (pick.pact == null) return false;
            Join(state, pick.pact, npcId);
            sponsorId = pick.sponsor;
            if (joined != null) { joined.Add(npcId); joined.Add(sponsorId); }
            return true;
        }

        /// <summary>Every active alliance this houseguest belongs to.</summary>
        public static List<AllianceState> ActiveAlliancesFor(EpisodeState state, string id) =>
            state.alliances.Where(a => a.active && a.members.Contains(id)).ToList();

        // ---------------------------------------------------------------- internals

        /// <summary>A member's first name as the season writes it into a pact's name (<see cref="CharacterNames.SeasonFirstName"/>).</summary>
        private static string First(EpisodeState state, string id) => CharacterNames.SeasonFirstName(state, state.Find(id).name);

        private static void Form(EpisodeState state, string first, string second)
        {
            string name = "The " + First(state, first) + " and " + First(state, second) + " Pact";
            var alliance = new AllianceState
            {
                id = HouseguestPactPrefix + state.nextSequence,
                name = name,
                members = new List<string> { first, second },
                active = true,
            };
            state.alliances.Add(alliance);
            EpisodeEngine.AllianceFormedUnderRead(state, alliance);
            RelationshipLedger.Record(state, first, second, "alliance-formed", 30, name + " was formed");
        }

        /// <summary>
        /// An alliance ends when either side has come to dislike the other - or, for an alliance
        /// the player is in, when the PLAYER has come to dislike their partner.
        ///
        /// <para>No ledger entry, and deliberately so: this is not something either of them did. A
        /// pact that dies because two people drifted apart is not a betrayal, and recording it as
        /// one would put a permanent grudge on the books that nobody earned.</para>
        ///
        /// <para>The player's alliance reads only the player's own score toward their partner. That
        /// is what the reference build's weekly check reads: <c>checkForDissolution</c> in
        /// <c>alliance-system.ts</c> looks at each pair once, from the earlier member toward the
        /// later, and every path that forms a player alliance there puts the player first. This
        /// used to read both directions, so the partner's private feeling - a number the player
        /// has no way to know - could end the player's alliance, and the ending itself was the
        /// only way the player ever learned it. Houseguests' own pacts still read both.</para>
        ///
        /// <para><b>Under the commitment rules</b> (ACTIONS-DEALS-ALLIANCES-PLAN C3) the player's pacts
        /// are two-sided again, the warning first: a partner whose own view of the player has fallen
        /// under <see cref="Allegiance.QuietLine"/> has gone quiet - they ignore the player's call, a read
        /// says how they see the player, and the notes say so in words - and one who has turned, under
        /// <see cref="SourLine"/>, ends it from their side. Somebody who has left the house has left every
        /// pact (X5), so only the members still in it are read, and a pact the player is no longer in
        /// the house for is a pact among houseguests.</para>
        ///
        /// <para>And a pact of three or more (C5, the lead's ruling on groups) goes on without the one who
        /// turned: they are taken out of it, as a leave takes the player out and C2's cut takes a betrayer
        /// out, and the rest of it hear "Riley Chen is out of The Trio.", the story's own words for a
        /// defector - an act of theirs, never a number. A pact of two ends, as C3 made it; and the player's
        /// own souring on anybody in it still ends the whole pact, as it always did.</para>
        ///
        /// <para>From alumni rules 5 (ALL-STAR-HISTORIES-PLAN §14.L) a houseguests' pact grown past two
        /// and still kept to its members goes on without a joiner who has soured with anybody in it
        /// (<see cref="ShedSourJoiners"/>), with no line and no ledger entry; its founders' souring, and
        /// any souring once somebody outside it knows of it, still ends it whole.</para>
        /// </summary>
        public static void Dissolve(EpisodeState state)
        {
            bool rules = EpisodeEngine.CommitmentRulesOn(state);
            foreach (var alliance in state.alliances.Where(a => a.active).ToList())
            {
                if (AlumniRules.At(state, AlumniRules.GrowingPacts)) ShedSourJoiners(state, alliance);
                var members = rules ? alliance.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList() : alliance.members;
                if (rules && members.Contains(state.playerId) && Intact(state, alliance)
                    && !members.Any(other => other != state.playerId && state.Score(state.playerId, other) < SourLine))
                {
                    foreach (string turned in members.Where(other => other != state.playerId && state.Score(other, state.playerId) < SourLine).ToList())
                    {
                        if (EpisodeEngine.TakeOutOfPact(state, alliance, turned)) break;
                        var name = state.Find(turned)?.name ?? turned;
                        EpisodeEngine.Log(state, "alliance", name + " is out of " + alliance.name + ".",
                            alliance.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).Concat(new[] { turned }).Distinct().ToArray());
                    }
                    continue;
                }
                bool soured = members.Contains(state.playerId)
                    ? members.Any(other => other != state.playerId && (state.Score(state.playerId, other) < SourLine
                        || (rules && state.Score(other, state.playerId) < SourLine)))
                    : members.Any(one =>
                        members.Any(other => one != other && state.Score(one, other) < SourLine));
                if (soured || !Intact(state, alliance)) alliance.active = false;
            }
        }

        /// <summary>
        /// Ends every alliance that no longer has two members in the house - the half of
        /// <see cref="Dissolve"/> that needs no week to pass.
        ///
        /// <para>Run at the final eviction. That eviction goes straight to the jury, so no social
        /// week follows it and <see cref="Dissolve"/> never sees the final three's evictee: their
        /// alliance used to outlive them into the jury, printed as active in the notebook and read
        /// by <see cref="WebJuryVoting.AllianceLoyalty"/> as a current alliance - the one juror in a
        /// season whose former ally scored 100 where every other juror's scored 25. The reference
        /// build takes an evictee out of every alliance at the final eviction as at any other.</para>
        ///
        /// <para>Souring is not judged here. That stays a weekly judgement, made as the social week
        /// opens; an eviction is the only thing that ends an alliance at the finale.</para>
        /// </summary>
        public static List<AllianceState> EndBroken(EpisodeState state)
        {
            var ended = state.alliances.Where(a => a.active && !Intact(state, a)).ToList();
            foreach (var alliance in ended) alliance.active = false;
            return ended;
        }

        /// <summary>Whether at least two of an alliance's members are still in the house.</summary>
        private static bool Intact(EpisodeState state, AllianceState alliance) =>
            alliance.members.Count(id => state.Find(id)?.status == ContestantStatus.Active) >= 2;

        /// <summary>
        /// §14.L: the pact's fact is private and known only to its members, so nobody outside it has seen its roster. False
        /// where the story's bonds do not run, and for a pact with no fact.
        /// </summary>
        private static bool PrivateToMembers(EpisodeState state, AllianceState pact)
        {
            if (!EpisodeEngine.StoryAt(state, StoryRules.Bonds)) return false;
            var fact = Knowledge.Of(state, FactKinds.Alliance, pact.id);
            return fact != null && fact.visibility == FactVisibility.Private && fact.knowers.All(pact.members.Contains);
        }

        /// <summary>
        /// §14.L.B rule 7: private to its members, from an earlier week than this one (so never in its founding week), and with
        /// every member still in the house. Somebody who has left the house has left the pact (X5) but stays on its roster and
        /// a knower of its fact, and every jury reader matches juror and finalist by that roster: a recruit taken in after they
        /// left would read as their ally at the finale. So a pact that has lost a member, to eviction or to shedding, never
        /// grows again.
        /// </summary>
        private static bool KeptToItself(EpisodeState state, AllianceState pact) =>
            PrivateToMembers(state, pact) && Knowledge.Of(state, FactKinds.Alliance, pact.id).week < state.week
            && pact.members.All(id => state.Find(id)?.status == ContestantStatus.Active);

        /// <summary>
        /// §14.L.B rule 8: an active target agreement between two of the pact-to-be - its members in the house and the
        /// recruit - against a third of it. One struck before the join would outlive it (an agreement runs to the end of its
        /// week, and the deals are struck as the week turns, before the pact window), and pull a member's nomination toward a
        /// pact-mate (<see cref="StrategyRules.NominationReluctance"/> reads the same raw list). Pure: no draw, no write.
        /// </summary>
        private static bool TargetsWithin(EpisodeState state, List<string> here, string recruitId)
        {
            var group = new HashSet<string>(here, StringComparer.Ordinal) { recruitId };
            return CommitmentReferences.RawDeals(state).Any(d => d != null && d.status == DealStatus.Active && d.type == DealKind.TargetAgreement
                && d.proposerId != null && d.recipientId != null && d.targetId != null
                && group.Contains(d.proposerId) && group.Contains(d.recipientId) && group.Contains(d.targetId));
        }

        /// <summary>§14.L.D.1: one side's welcome for a join - no grudge either way, and a view at <see cref="JoinWelcomeLine"/> or more.</summary>
        private static bool Welcomes(EpisodeState state, string fromId, string toId) =>
            !GrudgeBetween(state, fromId, toId) && state.Score(fromId, toId) >= JoinWelcomeLine;

        /// <summary>
        /// §14.L.D.4: the recruit on the end of the pact, so its founder stays first; the recruit a knower of its fact, which
        /// stays private with its founders as actor and subject; and "alliance-formed" with every member in the house, both
        /// ways, the record a birth writes. No score, line, memory, name or row.
        /// </summary>
        private static void Join(EpisodeState state, AllianceState pact, string recruitId)
        {
            var here = pact.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList();
            pact.members.Add(recruitId);
            // KeptToItself found the fact.
            Knowledge.AddKnower(state, Knowledge.Of(state, FactKinds.Alliance, pact.id), recruitId);
            foreach (var member in here)
                RelationshipLedger.Record(state, member, recruitId, "alliance-formed", EpisodeEngine.AllianceFormedImpact, pact.name + " was joined");
        }

        /// <summary>
        /// §14.L.F: a houseguests' pact past two, still private to its members, goes on without a joiner who has soured with
        /// anybody in it - the latest such joiner first, as C5's defector rule takes out the one who turned. The first two in
        /// its order are its founders; their souring still ends it whole. A shed member stays a knower, so the pact never
        /// grows or sheds again. No ledger entry, no line. Pure.
        /// </summary>
        private static void ShedSourJoiners(EpisodeState state, AllianceState pact)
        {
            if (pact.members.Count <= 2 || !HouseguestsOwn(pact) || pact.members.Contains(state.playerId) || !PrivateToMembers(state, pact)) return;
            while (true)
            {
                var here = pact.members.Where(id => state.Find(id)?.status == ContestantStatus.Active).ToList();
                if (here.Count <= 2) return;
                string sour = pact.members.Skip(2).Where(here.Contains)
                    .LastOrDefault(j => here.Any(o => o != j && (state.Score(j, o) < SourLine || state.Score(o, j) < SourLine)));
                if (sour == null) return;
                // More than two in the house: it goes on without them (EpisodeEngine.TakeOutOfPact).
                EpisodeEngine.TakeOutOfPact(state, pact, sour);
            }
        }

    }
}
