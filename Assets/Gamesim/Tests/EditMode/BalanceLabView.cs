#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What the player has (BALANCE plan B3): the screens' own readers and the controls they offer, over a
    /// season the policy never sees.
    ///
    /// <para><b>Gating is structural.</b> The season is a private field. No member returns it, a copy of it
    /// or any of its state objects (houseguests, relationships, pacts, deals, promises, the ledger, the
    /// story) - only this facade's own records and the readers' own records, which are built for the
    /// screens under the knowledge gate: <see cref="KnownOdds"/>, <see cref="VoteRead"/>,
    /// <see cref="AllianceRead"/>, <see cref="KnownBallots"/>, the player's own view of each houseguest,
    /// <see cref="HouseguestNotes"/>, <see cref="WaitingOnYou"/>, <see cref="CampaignBrief"/>, the story's
    /// shown odds and the player's commitments. BalanceLabTests holds every reachable member type to that
    /// by reflection. A policy is handed nothing else, so it cannot read a hidden view, a statistic, an
    /// NPC-only pact or the season's generator however it is written.</para>
    ///
    /// <para>What a houseguest is shown as: name, traits (the conversation's header names them), status,
    /// competition wins and nominations - never statistics, which no screen draws.</para>
    ///
    /// <para>The ledger is the player's record, but only its public and learned rows are here: each week's
    /// power, what people claimed about their votes, the standings the player learned, the calls they made.
    /// Never its competition rows, whose expected win is the analyst's odds.</para>
    /// </summary>
    internal sealed class PlayerView
    {
        private readonly EpisodeState s;
        private readonly uint coin;
        private int built;
        private VoteRead.Sheet voteRead;
        private WaitingOnYou.Reading waiting;

        internal PlayerView(EpisodeState state, uint coinSeed)
        {
            s = state ?? throw new ArgumentNullException(nameof(state));
            coin = coinSeed;
        }

        // ---------------------------------------------------------------- what every screen shows

        public sealed class Guest
        {
            public string id, name;
            public bool isPlayer;
            public ContestantStatus status;
            public IReadOnlyList<string> traits;
            public int hohWins, vetoWins, timesNominated;
        }

        public int Week => s.week;
        public EpisodePhase Phase => s.phase;
        public EvictionStage Stage => s.evictionStage;
        public string Me => s.playerId;
        public ContestantStatus MyStatus => s.Find(s.playerId).status;
        public bool InTheHouse => MyStatus == ContestantStatus.Active;
        public string Hoh => s.hohId;
        public string VetoHolder => s.vetoHolderId;
        public IReadOnlyList<string> Nominees => s.nominees.ToList();
        public IReadOnlyList<string> VetoPlayers => s.vetoPlayers.ToList();
        public bool CompetitionResolved => s.competitionResolved;
        public bool VetoResolved => s.vetoResolved;
        public bool EvictionResolved => s.evictionResolved;
        public int HouseCount => s.Active.Count();

        public IReadOnlyList<Guest> House => s.contestants.Select(c => new Guest
        {
            id = c.id, name = c.name, isPlayer = c.isPlayer, status = c.status, traits = (c.traits ?? new List<string>()).ToList(),
            hohWins = c.hohWins, vetoWins = c.vetoWins, timesNominated = c.timesNominated,
        }).ToList();

        public Guest Find(string id) => House.FirstOrDefault(g => g.id == id);

        /// <summary>The houseguests still in the house other than the player, in cast order.</summary>
        public IReadOnlyList<string> Others => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        public bool FreeTime => s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign;
        public bool DiaryPending => s.pendingDiary != null;
        public bool FirstNight => EpisodeEngine.IntroductionsOpen(s);
        public bool MetAndGreeted => s.openingBeatsSeen.Contains(OpeningBeat.MeetAndGreet);
        public IReadOnlyList<string> StillToMeet => EpisodeEngine.StillToMeet(s).Select(c => c.id).ToList();

        // ---------------------------------------------------------------- the player's own seats

        public int ActionsSpent => EpisodeEngine.SocialActionsSpent(s);
        public int ActionBudget => EpisodeEngine.SocialActionBudget(s);
        /// <summary>Whether one of the week's four windows is open: free time, the campaign, or a strategy window after a decision.</summary>
        public bool InAWindow => EpisodeEngine.Window(s) != Windows.None;
        public bool ActionsLeft => InTheHouse && InAWindow && ActionsSpent < ActionBudget;
        public bool CanBuyAction => ExtraActionPurchaseChoice.Available(s);
        public int BoughtThisWeek => s.boughtActionPoints;
        /// <summary>The player's competition preparation from studying the house, out of five: the study's own line says it.</summary>
        public int Preparation => s.playerStudyBonus;
        /// <summary>Whether the player is a Have-Not this week: the house announces it.</summary>
        public bool HaveNot => s.haveNots != null && s.haveNots.Contains(s.playerId);

        // ---------------------------------------------------------------- what the player knows

        /// <summary>How the player sees a houseguest: their own score, the relationship web's line from them.</summary>
        public double MyView(string id) => s.Score(s.playerId, id);

        /// <summary>How the houseguest sees the player, as far as the player can tell (<see cref="KnownOdds.PresumedView"/>).</summary>
        public double PresumedView(string id) => KnownOdds.PresumedView(s, id);

        public string Band(string fromId, string toId) => KnownOdds.Band(s, fromId, toId);
        public KnownOdds.Estimate DealOdds(string id, string type, string aboutId = null) => KnownOdds.Deal(s, id, type, aboutId);
        public KnownOdds.Estimate PleaOdds(string deciderId, string ask, string subjectId, string approach) => KnownOdds.Plea(s, deciderId, ask, subjectId, approach);
        public KnownOdds.Estimate AllianceOdds(string id) => KnownOdds.Alliance(s, id);
        public bool KnownPact(string a, string b) => KnownOdds.KnownPact(s, a, b);

        public bool VoteReadAvailable => Gamesim.Simulation.VoteRead.Available(s);
        /// <summary>The vote read, while one exists (<see cref="VoteReadAvailable"/>).</summary>
        public VoteRead.Sheet VoteSheet => voteRead ?? (voteRead = Gamesim.Simulation.VoteRead.Read(s));

        public List<AllianceRead.Pact> MyPacts => AllianceRead.Yours(s);
        public List<AllianceRead.SuspectedPact> SuspectedPacts => AllianceRead.Suspected(s);
        public KnownBallots.Sheet Ballots(int week) => KnownBallots.Read(s, week);
        public List<HouseguestNotes.Note> Notes(string id) => HouseguestNotes.For(s, id);
        public WaitingOnYou.Reading Waiting => waiting ?? (waiting = WaitingOnYou.Read(s));
        public List<CampaignBrief.Goal> Goals => CampaignBrief.Goals(s);
        public List<CommitmentsRead.Commitment> Commitments => CommitmentsRead.Of(s);
        public List<CommitmentsRead.Breach> WouldBreak(CommitmentsRead.Decision decision) => CommitmentsRead.WouldBreak(s, decision);

        /// <summary>Whether the player is in a standing pact with this houseguest, as the alliances page says.</summary>
        public bool AlliedWith(string id) => MyPacts.Any(p => p.active && p.members.Any(m => m.id == id));

        public sealed class PowerRow
        {
            public int week;
            public string hohId, vetoHolderId, evicteeId;
            public bool vetoUsed;
            public IReadOnlyList<string> nominees;
        }

        public sealed class Claim
        {
            public int week;
            public string voterId, targetId, source, status;
        }

        /// <summary>Each week's power, as the house saw it.</summary>
        public IReadOnlyList<PowerRow> Power => s.ledger.power.Select(p => new PowerRow
            { week = p.week, hohId = p.hohId, vetoHolderId = p.vetoHolderId, evicteeId = p.evicteeId, vetoUsed = p.vetoUsed, nominees = p.nominees.ToList() }).ToList();

        /// <summary>What houseguests said, or were overheard saying, their votes were.</summary>
        public IReadOnlyList<Claim> Claims => s.ledger.claims.Select(c => new Claim
            { week = c.week, voterId = c.voterId, targetId = c.targetId, source = c.source, status = c.status }).ToList();

        // ---------------------------------------------------------------- what the house has put to the player

        public sealed class Beat
        {
            public string id;
            public IReadOnlyList<Choice> choices;
        }

        public sealed class Choice
        {
            public string optionId;
            public bool lapse, locked, pickPerson, conduct, costsAction;
            public IReadOnlyList<string> eligibleIds;
            /// <summary>The chance the beat's card shows, in percent (<see cref="StoryOdds.Chance"/>).</summary>
            public int shownChance;
        }

        /// <summary>The story beats open to the player, as their cards show them.</summary>
        public IReadOnlyList<Beat> Beats => EpisodeEngine.OpenStoryBeats(s).Select(b => new Beat
        {
            id = b.id,
            choices = b.choices.Select(c => new Choice
            {
                optionId = c.optionId, lapse = c.lapse, locked = c.locked, pickPerson = c.pickPerson, conduct = c.conduct, costsAction = c.costsAction,
                eligibleIds = (c.eligibleIds ?? new List<string>()).Where(id => s.Find(id)?.status == ContestantStatus.Active).ToList(),
                shownChance = StoryOdds.Chance(s, b, c),
            }).ToList(),
        }).ToList();

        public sealed class Offer
        {
            public string id, fromId, type, aboutId;
        }

        /// <summary>Deals put to the player and waiting on an answer.</summary>
        public IReadOnlyList<Offer> Offers => (UnifiedCommitments.RulesOn(s) ? CommitmentReferences.Deals(s) : (IReadOnlyList<DealState>)s.deals)
            .Where(d => d != null && d.status == DealStatus.Proposed && d.recipientId == s.playerId)
            .Select(d => new Offer { id = d.id, fromId = d.proposerId, type = d.type, aboutId = d.targetId }).ToList();

        public sealed class Card
        {
            public string id, kind, fromId, aboutId;
            public IReadOnlyList<string> replies;
        }

        /// <summary>Houseguests who came to the player and wait on an answer, with the answers their card offers.</summary>
        public IReadOnlyList<Card> Cards => s.replyCards.Select(c => new Card
            { id = c.id, kind = c.kind, fromId = c.fromId, aboutId = c.aboutId, replies = ReplyCards.Replies(c.kind).Select(r => r.Key).ToList() }).ToList();

        /// <summary>Houseguests offering to swear loyalty to the player.</summary>
        public IReadOnlyList<string> OathOffers => s.oathOpportunities.ToList();

        // ---------------------------------------------------------------- what the screen offers

        /// <summary>Whether the open window lets the player say this to this houseguest: the rule the screen asks before it offers a word.</summary>
        public bool CanConverse(string id, EpisodeCommandKind kind) => EpisodeEngine.ConversationWindowRefusal(s, id, kind) == null;
        /// <summary>The names the rename offers for one of the player's pacts.</summary>
        public IReadOnlyList<string> PactNamesOnOffer(string pactId)
        {
            var pact = s.alliances.FirstOrDefault(a => a.id == pactId && a.active && a.members.Contains(s.playerId));
            return pact == null ? new List<string>() : PactNames.For(s, pact).ToList();
        }
        public bool CanPropose(string id, string type, string aboutId, out string reason) => PlayerDeals.CanPropose(s, id, type, aboutId, out reason);
        public bool CanLobby(string deciderId, string ask, string subjectId, out string reason) => StrategyRules.CanLobby(s, deciderId, ask, subjectId, out reason);
        public bool AskedThisWeek(string id) => EpisodeEngine.AskedThisWeek(s, id);
        public bool ReadThisWeek(string id) => EpisodeEngine.ReadThisWeek(s, id);
        public bool CalledThisWeek(string pactId) => s.ledger.calls.Any(k => k.week == s.week && k.allianceId == pactId);
        // No walk-in (WitnessProximity) until B5: the director offers one only for a pair the NPC world puts
        // together, and the lab does not drive that world; asking of any pair would hand a policy more than a player has.
        public bool RoomActsOpen => EpisodeEngine.RoomActsOpen(s);
        public IReadOnlyList<string> Voters => EpisodeEngine.Voters(s).Select(c => c.id).ToList();
        public IReadOnlyList<string> NominationCandidates => EpisodeEngine.NominationCandidates(s).Select(c => c.id).ToList();
        public IReadOnlyList<string> ReplacementCandidates => EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
        public bool VetoLockedAtFinalFour => EpisodeEngine.VetoIsLockedAtFinalFour(s);
        /// <summary>The nominee an NPC veto holder has saved, which a Head of Household player must replace.</summary>
        public string VetoSaved => EpisodeEngine.NpcVetoSave(s);

        public bool MustCompete => EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer);
        public bool MustNominate => s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId;
        public bool MustDecideVeto => s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved
            && (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null));
        public bool MustVote => s.phase == EpisodePhase.Eviction && !s.evictionResolved
            && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
            && !s.votes.Any(v => v.voterId == s.playerId) && (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s));
        public bool MustSpeakFromTheBlock => s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
            && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(x => x.speakerId == s.playerId);
        public bool MustFinalEvict => s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId;
        public bool MustVoteForAWinner => s.phase == EpisodePhase.Jury && !s.Active.Any(p => p.isPlayer) && !s.votes.Any(v => v.voterId == s.playerId)
            && (MyStatus == ContestantStatus.Jury || MyStatus == ContestantStatus.Evicted);

        public sealed class Question
        {
            public string finalistId, questionerId, category, receiptKind;
            public IReadOnlyList<string> responses;
        }

        /// <summary>The jury's question in front of the finale, when one is.</summary>
        public Question JuryQuestion
        {
            get
            {
                if (s.phase != EpisodePhase.JuryQuestioning || s.juryQuestionIndex >= s.juryExchanges.Count) return null;
                var q = s.juryExchanges[s.juryQuestionIndex];
                if (q.completed) return null;
                return new Question
                {
                    finalistId = q.finalistId, questionerId = q.questionerId, category = q.category, receiptKind = q.receiptKind,
                    responses = EpisodeEngine.FinaleOn(s) ? FinaleQuestions.Offered(q.category, q.receiptKind).ToList() : new List<string>(),
                };
            }
        }

        // ---------------------------------------------------------------- acting

        /// <summary>A command for the player's channel at what the player sees now.</summary>
        public EpisodeCommand Command(EpisodeCommandKind kind, string target = null, string second = null, string text = null) => new EpisodeCommand
        {
            id = "lab-" + s.revision.ToString(CultureInfo.InvariantCulture) + "-" + kind + "-" + (built++).ToString(CultureInfo.InvariantCulture),
            actorId = s.playerId, kind = kind, targetId = target, secondTargetId = second, text = text,
            expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>A coin of the policy's own, in [0, 1): keyed to the lab's season seed, the moment and a salt; never the season's generator.</summary>
        public double Coin(int salt) =>
            SeededRandom.HashSeed(coin.ToString(CultureInfo.InvariantCulture) + ":" + s.revision.ToString(CultureInfo.InvariantCulture) + ":" + built.ToString(CultureInfo.InvariantCulture)
                + ":" + salt.ToString(CultureInfo.InvariantCulture)) % 1000000u / 1000000.0;

        /// <summary>A coin's pick from a list, or null for an empty one.</summary>
        public string Pick(IReadOnlyList<string> ids, int salt) => ids == null || ids.Count == 0 ? null : ids[(int)(Coin(salt) * ids.Count) % ids.Count];
    }
}
#endif
