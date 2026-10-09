using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The reader inventory guard (vote family V5a): every place Simulation or Runtime reads the commitment lists - the raw
    /// <c>.promises</c> and <c>.deals</c>, mode 1's gates <c>UnifiedCommitments.RulesOn(</c> and
    /// <c>UnifiedCommitmentHearings.RulesOn(</c>, the views (<c>CommitmentReferences.</c>) and the Safety history
    /// (<c>UnifiedCommitmentHistory.</c>) - is named here with why it may.
    /// The frozen historical validators are not scanned: they are the schemas they freeze.
    ///
    /// <para>Each entry is a file and a piece of the line's code with how many lines it names, and a reason: the authority itself
    /// (a view, a store, a validator, a migration); a writer, raw by design; a kind that stays raw (final two and three, the veto,
    /// pacts, information, partnership, target, loyalty); a list that is not a commitment list; a reader that already reads mode 2
    /// (through the views, or by a canonical id); a reader V5 moved; or a reader still pending the slice that moves it. A line
    /// that gains a read fails, and so does an entry whose lines have gone, so the list stays exact; each slice moves its
    /// pending entries, and V5f ends with none.</para>
    ///
    /// <para>Unity-free, so the subset runs it on every push; the editor finds the scripts through <c>Application.dataPath</c>,
    /// the subset by walking up from its own binary.</para>
    /// </summary>
    public sealed class CommitmentReaderScanTests
    {
        public enum Why { Authority, Writer, RawKind, NotCommitments, ModeAware, Moved, PendingV5a, PendingV5b, PendingV5c, PendingV5d, PendingV5e, PendingV5f }

        /// <summary>The slice the vote family has built through: no entry may still be pending it or an earlier one.</summary>
        private const Why Built = Why.PendingV5f;

        private sealed class Site
        {
            public readonly string File, Code, Reason;
            public readonly int Count;
            public readonly Why Why;
            public Site(string file, string code, int count, Why why, string reason) { File = file; Code = code; Count = count; Why = why; Reason = reason; }
        }

        private const string Sim = "Simulation/", Run = "Runtime/";

        /// <summary>Each file's sites. A null code names every site in the file; otherwise the first entry whose code a line contains claims it.</summary>
        private static readonly Site[] Allowlist =
        {
            // ---- the authority: the views, stores, histories, validators and migrations themselves
            new Site(Sim + "CommitmentReferences.cs", null, 13, Why.Authority, "the views themselves"),
            new Site(Sim + "UnifiedCommitments.cs", null, 16, Why.Authority, "the Safety policy and its storage check"),
            new Site(Sim + "UnifiedCommitmentStore.cs", null, 11, Why.Authority, "the Safety store"),
            new Site(Sim + "UnifiedCommitmentHistory.cs", null, 1, Why.Authority, "the Safety history's mode-1 gate"),
            new Site(Sim + "UnifiedCommitmentHearings.cs", null, 3, Why.Authority, "the hearing lineage and its storage check"),
            new Site(Sim + "UnifiedVoteStore.cs", null, 9, Why.Authority, "the Vote store and the creators' reads"),
            new Site(Sim + "UnifiedVoteAdmission.cs", null, 3, Why.Authority, "the Vote admission"),
            new Site(Sim + "UnifiedVoteReferences.cs", null, 2, Why.Authority, "the unchecked projections"),
            new Site(Sim + "UnifiedVoteFamilyValidation.cs", null, 18, Why.Authority, "the Vote family's validation"),
            new Site(Sim + "UnifiedSafetySaveReferences.cs", null, 4, Why.Authority, "the saved Safety references"),
            new Site(Sim + "EpisodeValidation.cs", null, 12, Why.Authority, "whole-episode validation"),
            new Site(Sim + "EpisodeValidation.UnifiedSafetyReferences.cs", null, 7, Why.Authority, "whole-episode validation, mode 1's hearing gate among it"),
            new Site(Sim + "EpisodeValidation.UnifiedVoteReferences.cs", null, 1, Why.Authority, "whole-episode validation"),
            new Site(Sim + "EpisodeState.cs", null, 2, Why.Authority, "the state's own clone"),
            new Site(Sim + "EpisodeEngine.UnifiedSafety.cs", null, 4, Why.Authority, "the Safety gateways' storage context"),
            new Site(Run + "Persistence/EpisodeSaveMigrations.cs", null, 6, Why.Authority, "save migrations"),
            new Site(Run + "Persistence/EpisodeSaveValidation.cs", null, 1, Why.Authority, "save validation"),

            // ---- writers, raw by design (never read through a detached view)
            new Site(Sim + "DealResolution.cs", "state.deals.Where(d => d.status == DealStatus.Active)", 1, Why.Writer, "the reveal's raw deal verdicts"),
            new Site(Sim + "EpisodeEngine.cs", "s.promises.Where(p => p.status == PromiseStatus.Active && p.expiresWeek > 0", 1, Why.Writer, "the week turn's raw promise expiry"),
            new Site(Sim + "EpisodeEngine.cs", "p.kind == PromiseKind.Vote && p.fromId == vote.voterId", 1, Why.Writer, "the reveal settles raw vote promises"),
            new Site(Sim + "EpisodeEngine.cs", "p.fromId == s.hohId &&", 1, Why.Writer, "the nomination settles raw Safety and loyalty promises"),
            new Site(Sim + "EpisodeEngine.cs", "Require(!s.promises.Any(", 1, Why.Writer, "MakePromise's raw branch"),
            new Site(Sim + "EpisodeEngine.cs", "s.promises.Add(", 1, Why.Writer, "MakePromise's raw branch"),
            new Site(Sim + "EpisodeEngine.cs", "else s.deals.Add(struck)", 1, Why.Writer, "ProposeDeal's raw branch"),
            new Site(Sim + "EpisodeEngine.Commitments.cs", "DealStatus.Binds(d.status)", 1, Why.Writer, "EndWithTheEvictee's raw loop"),
            new Site(Sim + "EpisodeEngine.Commitments.cs", "p.kind == PromiseKind.FinalTwo", 1, Why.Writer, "EndWithTheEvictee's raw loop"),
            new Site(Sim + "EpisodeEngine.Negotiation.cs", "s.deals.Add(", 5, Why.Writer, "the counter, the veto price and the ask"),
            new Site(Sim + "EpisodeEngine.Negotiation.cs", "var owned = s.deals.FirstOrDefault", 1, Why.Writer, "VoidThePrice's raw price"),
            new Site(Sim + "EpisodeEngine.Strategy.cs", "s.deals.Add(", 2, Why.Writer, "the lobby's raw branch"),
            new Site(Sim + "NpcDeals.cs", "state.deals.Add(", 3, Why.Writer, "the house's raw deals"),
            new Site(Sim + "NpcDeals.cs", "foreach (var deal in state.deals)", 1, Why.Writer, "the deal pass's raw expiry"),
            new Site(Sim + "NpcPromises.cs", "else state.promises.Add(promise)", 1, Why.Writer, "the house's raw promises"),
            new Site(Sim + "Story/EpisodeEngine.StoryEffects.cs", null, 2, Why.Writer, "a story's raw promise and deal"),
            new Site(Sim + "Story/EpisodeEngine.StoryHooks.cs", "s.promises.Where(p => p.status == PromiseStatus.Active && (p.fromId == id", 1, Why.Writer, "Expel's raw loop"),
            new Site(Sim + "Story/EpisodeEngine.StoryHooks.cs", "s.deals.Where(d => DealStatus.Binds(d.status) && (d.proposerId == id", 1, Why.Writer, "Expel's raw loop"),

            // ---- kinds that stay raw
            new Site(Sim + "AllianceRead.cs", "d.type == DealKind.AllianceInvite && d.week == row.startedWeek", 1, Why.RawKind, "a pact's invitation"),
            new Site(Sim + "CommitmentsRead.cs", "EpisodeEngine.FinalChoiceSettles(s, p, player)", 1, Why.RawKind, "final two"),
            new Site(Sim + "CommitmentsRead.cs", "d.type == DealKind.Partnership", 1, Why.RawKind, "partnership"),
            new Site(Sim + "CommitmentsRead.cs", "s.promises.Where(p => p.status == PromiseStatus.Active && p.fromId == player", 1, Why.RawKind,
                "mode 0's raw Safety and the loyalty promise; canonical Safety is read beside it"),
            new Site(Sim + "EndgameCommitments.cs", null, 2, Why.RawKind, "final two and three"),
            new Site(Sim + "EpisodeEngine.cs", "d.type == DealKind.VetoUse", 1, Why.RawKind, "the veto"),
            new Site(Sim + "EpisodeEngine.cs", "FinalChoiceSettles(s, p, s.hohId)", 1, Why.RawKind, "final two"),
            new Site(Sim + "EpisodeEngine.Commitments.cs", "DealKind.InformationSharing", 2, Why.RawKind, "information"),
            new Site(Sim + "EpisodeEngine.FinalChoice.cs", null, 1, Why.RawKind, "final three"),
            new Site(Sim + "EpisodeEngine.Negotiation.cs", "d.type == DealKind.VetoUse", 1, Why.RawKind, "the veto asks"),
            new Site(Sim + "FinalistRead.cs", "DealKind.FinalTwo && d.status == DealStatus.Active && Between", 1, Why.RawKind, "final two"),
            new Site(Sim + "FinalistRead.cs", "PromiseKind.FinalTwo", 3, Why.RawKind, "final two"),
            new Site(Sim + "FinalistRead.cs", "d.type == DealKind.FinalTwo", 2, Why.RawKind, "final two"),
            new Site(Sim + "Negotiation.cs", "d.type == DealKind.VetoUse", 1, Why.RawKind, "the veto"),
            new Site(Sim + "NpcDeals.cs", "d.type == DealKind.VetoUse", 1, Why.RawKind, "the veto"),
            new Site(Sim + "NpcPromises.cs", "p.kind == PromiseKind.FinalTwo", 1, Why.RawKind, "final two"),
            new Site(Sim + "Story/StoryCatalog.BondsAndSecrets.cs", null, 2, Why.RawKind, "final two"),
            new Site(Sim + "Story/StoryConsumers.cs", "d.type == DealKind.VetoUse", 1, Why.RawKind, "the veto"),
            new Site(Run + "Episode/EpisodeDirector.Strategy.cs", null, 1, Why.RawKind, "the veto"),
            new Site(Run + "Episode/EpisodeHud.Chrome.cs", null, 2, Why.RawKind, "final two and three on the chrome"),

            // ---- lists that are not the season's commitments
            new Site(Sim + "AllianceRead.cs", "pact.deals = ", 1, Why.NotCommitments, "the pact page's own lines"),
            new Site(Sim + "SeasonAutopsy.cs", "Count(c.", 2, Why.NotCommitments, "the autopsy's counts"),
            new Site(Sim + "WebEvictionVoting.cs", "foreach (var", 2, Why.NotCommitments, "the web evaluator's own copy, built from the season above"),
            new Site(Run + "Episode/EpisodeDirector.Alliances.cs", null, 1, Why.NotCommitments, "a pact page's lines"),
            new Site(Run + "Presentation/WeeklyRecap.cs", null, 1, Why.NotCommitments, "the recap's lines"),
            new Site(Run + "Presentation/WeeklyRecapScreen.cs", null, 2, Why.NotCommitments, "the recap's lines"),

            // ---- readers that already read mode 2: through the views, by a canonical id, or with a mode-2 branch of their own
            new Site(Sim + "CommitmentsRead.cs", "CommitmentReferences.Find", 2, Why.ModeAware, "a canonical Safety row's breach, by its id"),
            new Site(Sim + "EpisodeEngine.cs", "UnifiedCommitments.RulesOn(s) ? CommitmentReferences.FindCanonical(s, c.targetId)", 1, Why.ModeAware, "RespondToDeal: mode 2 answers on its own rows"),
            new Site(Sim + "EpisodeEngine.cs", "s.deals.FirstOrDefault(d => d.id == c.targetId", 1, Why.ModeAware, "RespondToDeal: a raw offer"),
            new Site(Sim + "EpisodeEngine.cs", "CommitmentReferences.FindDeal(s, deal.id)", 1, Why.ModeAware, "RespondToDeal: mode 1's answered Safety offer"),
            new Site(Sim + "EpisodeEngine.cs", "CommitmentReferences.FindDeal(s, c.targetId)", 1, Why.ModeAware, "RespondToDeal: mode 1's Safety offer"),
            new Site(Sim + "EpisodeEngine.Ledger.cs", null, 1, Why.ModeAware, "the reconcile reads the deal view"),
            new Site(Sim + "EpisodeEngine.Negotiation.cs", "if (UnifiedCommitments.RulesOn(s)", 1, Why.ModeAware, "the counter's Safety dispatch; mode 2 has its own"),
            new Site(Sim + "Story/EpisodeEngine.StoryHooks.cs", "UnifiedVoteStore.On(s) ? UnifiedVoteReferences.DealsUnchecked(s)", 1, Why.ModeAware,
                "StoryVotesRevealed, with its own mode-2 branch"),
            new Site(Sim + "EpisodeEngine.Negotiation.cs", "CommitmentReferences.FindCanonical(s, price.id)", 1, Why.ModeAware, "VoidThePrice, by the price's id"),
            new Site(Sim + "EpisodeEngine.UnifiedVote.cs", null, 1, Why.ModeAware, "the reveal's deal lane"),
            new Site(Sim + "FinalArgument.cs", "\"promise:\" + x.id == reference", 2, Why.ModeAware, "a moment's reference, by its id"),
            new Site(Sim + "FinalArgument.cs", "\"deal:\" + x.id == reference", 2, Why.ModeAware, "a moment's reference, by its id"),
            new Site(Sim + "FinaleQuestions.cs", "CommitmentReferences.FindPromise(s, id)", 3, Why.ModeAware, "a receipt's line, by its id"),
            new Site(Sim + "FinaleQuestions.cs", "CommitmentReferences.FindDeal(s, id)", 3, Why.ModeAware, "a receipt's line, by its id"),
            new Site(Sim + "FinaleQuestions.cs", "return promise == null ? null", 1, Why.ModeAware, "a receipt's line, by its id"),
            new Site(Sim + "FinaleQuestions.cs", "return deal == null ? null", 1, Why.ModeAware, "a receipt's line, by its id"),
            new Site(Sim + "FinalistRead.cs", "var canonical = CommitmentReferences.FindCanonical(s, deal.id)", 2, Why.ModeAware, "DealBreaker, with its own mode-2 branch"),
            new Site(Sim + "HoHPitches.cs", null, 3, Why.ModeAware, "the promise view and count"),
            new Site(Sim + "HouseDialogue.cs", "if (state.promises == null) return false", 1, Why.ModeAware, "a null guard before the view"),
            new Site(Sim + "HouseDialogue.cs", "foreach (var promise in CommitmentReferences.Promises(state))", 1, Why.ModeAware, "the promise view"),
            new Site(Sim + "HouseguestNotes.cs", null, 3, Why.ModeAware, "the views, and a canonical deal by its id"),
            new Site(Sim + "JuryHouseRead.cs", "CommitmentReferences.Deals(s).Where(d => (d.proposerId == player && d.recipientId == id)", 1, Why.ModeAware, "the deal view"),
            new Site(Sim + "JuryHouseRead.cs", "CommitmentReferences.Promises(s).Where(p => (p.fromId == player && p.toId == id)", 1, Why.ModeAware, "the promise view"),
            new Site(Sim + "JuryHouseRead.cs", "int deals = CommitmentReferences.Deals(s).Count", 1, Why.ModeAware, "the deal view"),
            new Site(Sim + "KnownBallots.cs", null, 2, Why.ModeAware, "settled weeks, with their own mode-2 branch"),
            new Site(Sim + "Negotiation.cs", "s?.deals == null", 1, Why.ModeAware, "Linked, with its own mode-2 branch"),
            new Site(Sim + "Negotiation.cs", "CommitmentReferences.FindDeal(s, d.linkedDealId)", 1, Why.ModeAware, "Linked"),
            new Site(Sim + "Negotiation.cs", "s.deals.FirstOrDefault(x => x.id == d.linkedDealId)", 1, Why.ModeAware, "Linked"),
            new Site(Sim + "SeasonAutopsy.cs", "UsesReferences(final)", 2, Why.ModeAware, "the autopsy's own mode-2 branch"),
            new Site(Run + "Episode/EpisodeDirector.Conversation.cs", null, 1, Why.ModeAware, "the deal count"),
            new Site(Run + "Episode/EpisodeDirector.Journal.cs", null, 1, Why.ModeAware, "the promise view"),
            new Site(Run + "Episode/EpisodeDirector.cs", null, 1, Why.ModeAware, "the promise view"),
            new Site(Run + "Presentation/DecisionContext.cs", null, 1, Why.ModeAware, "the promise view"),
            new Site(Run + "Presentation/RelationshipWeb.cs", null, 2, Why.ModeAware, "the views"),

            // ---- moved by vote family V5
            new Site(Sim + "NpcDeals.cs", "UnifiedCommitments.SafetyAuthorityOn(state) ? CommitmentReferences.Deals(state)", 1, Why.Moved, "V5a: Pending"),
            new Site(Sim + "NpcDeals.cs", "int legacy = CommitmentReferences.RawDeals(state)", 1, Why.Moved, "V5b: BrokenDeals, its vote breaches by incident (D1)"),
            new Site(Sim + "NpcDeals.cs", "UnifiedCommitmentHistory.", 2, Why.Moved, "V5b: BrokenDeals' Safety incidents, gate flipped"),
            new Site(Sim + "WebEvictionVoting.cs", "CommitmentReferences.Raw", 2, Why.Moved, "V5b: the NPC vote's promises and deals, breaches by incident (D1)"),
            new Site(Sim + "WebEvictionVoting.cs", "UnifiedCommitmentHistory.", 2, Why.Moved, "V5b: the NPC vote's Safety terms, gate flipped"),
            new Site(Sim + "EpisodeEngine.Levers.cs", null, 1, Why.Moved, "V5b: Obligations, the levers' terms"),
            new Site(Sim + "StrategyRules.cs", null, 2, Why.Moved, "V5b: NominationReluctance's deals and Safety incidents"),
            new Site(Sim + "EpisodeEngine.Betrayal.cs", null, 1, Why.Moved, "V5b: vote-deal betrayals"),
            new Site(Sim + "WebJuryVoting.cs", null, 4, Why.Moved, "V5b: the jury's obligations, breaches by incident (D1)"),
            new Site(Sim + "HouseEventSources.cs", null, 1, Why.Moved, "V5b: the crisis cast, gate flipped"),
            new Site(Sim + "VoteRead.cs", null, 1, Why.Moved, "V5b (V5d's): PartyTo, which a ballot's reason reads"),
            new Site(Sim + "WebEvictionVoting.cs", "state.deals.Count(d => d.status == \"broken\"", 1, Why.NotCommitments,
                "the web evaluator's threat, over its own copy of the season's"),
            new Site(Sim + "Story/StoryConsumers.cs", null, 3, Why.RawKind, "mode 0's raw Safety word; canonical Safety is read wherever it is the authority"),

            new Site(Sim + "ThreatAssessment.cs", null, 2, Why.Moved, "V5c: ReputationThreat - mode 1's raw promises, its vote promises counted beside, a reveal's own left out"),
            new Site(Sim + "Story/StoryOdds.cs", null, 4, Why.Moved, "V5c: the story's odds"),
            new Site(Sim + "Story/EpisodeEngine.StoryHooks.cs", null, 3, Why.Moved, "V5c: TryStartFromConversation"),
            new Site(Sim + "Story/StoryCatalog.HouseRemembers.cs", null, 3, Why.Moved, "V5c: the house remembers"),
            new Site(Sim + "Story/StoryCatalog.Plays.cs", null, 2, Why.Moved, "V5c: plays"),
            new Site(Sim + "Story/StoryCatalog.Ported.cs", null, 1, Why.Moved, "V5c: ported"),
            new Site(Sim + "Story/StoryCatalog.Spine.cs", null, 1, Why.Moved, "V5c: spine"),

            new Site(Sim + "Negotiation.cs", null, 13, Why.Moved,
                "V5d: Chance, Owed, CallInRefusal, SafetyHeld, BreachesAgainst (D1), CanonicalBreaches, BreachWords; a gate's raw branch is mode 0's"),
            new Site(Sim + "EpisodeEngine.Negotiation.cs", "promiseId", 2, Why.Moved, "V5d: CallInAPromise; the raw branch is mode 0's"),
            new Site(Sim + "PlayerDeals.cs", null, 3, Why.Moved, "V5d: HasBrokenPromise"),
            new Site(Sim + "YourWord.cs", "CommitmentReferences.FindDeal(s, fact.refId)", 1, Why.ModeAware, "a fact's deal, by its id"),
            new Site(Sim + "YourWord.cs", "CommitmentReferences.FindCanonical(s, fact.refId) is", 1, Why.ModeAware, "a fact's Safety row, by its id"),
            new Site(Sim + "YourWord.cs", null, 4, Why.Moved, "V5d: Breaches, and a fact's words; its audible facts gate on the hearing lineage's WritesOn"),
            new Site(Sim + "KnownOdds.cs", null, 2, Why.Moved, "V5d: History"),

            new Site(Sim + "FinaleQuestions.cs", null, 11, Why.Moved,
                "V5e: Receipts take the Vote owners, a receipt's party is checked wherever rows are canonical, a Vote receipt keeps mode 1's week"),
            new Site(Sim + "FinalArgument.cs", null, 8, Why.Moved, "V5e: the kept moments take the Vote owners; the argument's references wherever Safety is canonical"),
            new Site(Sim + "FinalCaseResume.cs", null, 4, Why.Moved, "V5e: the case's breaches - mode 1's raw rows, Safety by incident"),
            new Site(Sim + "FinalistRead.cs", "UnifiedCommitments.RulesOn(s)", 1, Why.ModeAware, "DealBreaker's mode-1 Safety branch; mode 2 has its own"),
            new Site(Sim + "FinalistRead.cs", null, 5, Why.Moved, "V5e: TowardYou (D1) and the kept deals"),
            new Site(Sim + "JuryHouseRead.cs", null, 8, Why.Moved, "V5e: a receipt's week, the reasons' views and raw deals"),
            new Site(Sim + "GameSense.cs", null, 6, Why.Moved, "V5e: the deal chances, scored once per group (D1), Vote rows dated as mode 1's"),
            new Site(Sim + "YourWeek.cs", null, 6, Why.Moved, "V5e: the week's word, Safety's canonical lines, the keeping gate"),

            new Site(Sim + "CommitmentsRead.cs", null, 12, Why.Moved,
                "V5f: the page's views (For, AtStake), Settled's raw views and Safety branch, the nominations' Safety rules, BallotRules' raw views; a gate's raw branch is mode 0's"),
            new Site(Sim + "AllianceRead.cs", null, 1, Why.Moved, "V5f: a pact's deals; the raw branch is mode 0's"),
            new Site(Sim + "HouseDialogue.cs", null, 4, Why.Moved, "V5f: the promise a line remembers; the raw branch is mode 0's"),
            new Site(Run + "Episode/EpisodeDirector.WalkOut.cs", null, 1, Why.Moved, "V5f: GoodbyeTone"),
            new Site(Run + "Presentation/WeeklyRecap.Ledger.cs", null, 2, Why.Moved, "V5f: the ledger's word count; the raw branch is mode 0's"),
            new Site(Run + "Episode/PortVerification.Season.Systems.cs", null, 3, Why.Moved, "V5f: the verification's deal lookups, through the views"),
        };

        /// <summary>Every read of the commitment lists, their mode-1 gates, their views or the Safety history.</summary>
        private static readonly Regex Read = new Regex(
            @"\.promises\b|\.deals\b|UnifiedCommitments\.RulesOn\(|UnifiedCommitmentHearings\.RulesOn\(|CommitmentReferences\.|UnifiedCommitmentHistory\.",
            RegexOptions.CultureInvariant);

        [Test]
        public void EveryCommitmentReadIsMovedOrNamed()
        {
            var sites = Scan();
            var claimed = Allowlist.ToDictionary(entry => entry, entry => 0);
            var problems = new List<string>();
            foreach (var site in sites)
            {
                var entry = Allowlist.FirstOrDefault(e => e.File == site.file && (e.Code == null || site.code.Contains(e.Code)));
                if (entry == null) problems.Add(site.file + ":" + site.line + " reads the commitments and is not named: " + site.code);
                else claimed[entry]++;
            }
            foreach (var pair in claimed.Where(pair => pair.Value != pair.Key.Count))
                problems.Add(pair.Key.File + " [" + (pair.Key.Code ?? "every site") + "] names " + pair.Value + " line(s); the list says " + pair.Key.Count
                    + (pair.Value > pair.Key.Count ? ": name the new read." : ": shrink the list to match."));
            TestContext.WriteLine(sites.Count + " reads in " + sites.Select(s => s.file).Distinct().Count() + " files; pending: "
                + string.Join(", ", Allowlist.Where(e => e.Why >= Why.PendingV5a).GroupBy(e => e.Why).Select(g => g.Key + " " + g.Sum(e => e.Count))));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void NothingIsPendingASliceAlreadyBuilt()
        {
            Assert.That(Allowlist.Where(e => e.Why >= Why.PendingV5a && e.Why <= Built).Select(e => e.File + " (" + e.Why + ")"), Is.Empty);
        }

        [Test]
        public void TheAllowlistNamesEachSiteOnceWithAReason()
        {
            foreach (var entry in Allowlist)
            {
                Assert.That(entry.Reason, Is.Not.Empty, entry.File);
                Assert.That(entry.File, Does.EndWith(".cs").And.Not.Contain("\\"), entry.File);
                Assert.That(entry.Count, Is.GreaterThan(0), entry.File + " [" + entry.Code + "]");
            }
            Assert.That(Allowlist.GroupBy(e => e.File + "\u0001" + e.Code).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, "Each file and code once.");
            foreach (var file in Allowlist.GroupBy(e => e.File))
                Assert.That(file.Count(e => e.Code == null), Is.LessThanOrEqualTo(1), file.Key + ": one entry for the rest of the file.");
        }

        [Test]
        public void TheScanCountsCodeAndIgnoresComments()
        {
            const string source = "var a = s.deals.Count; // s.promises in a comment\n"
                + "/* CommitmentReferences.Deals(s) in a block\n UnifiedCommitments.RulesOn(s) still in it */ var b = s.promises;\n"
                + "/// <see cref=\"CommitmentReferences.RawDeals\"/>\n"
                + "var c = UnifiedCommitmentHistory.Breaches(s); var d = s.dealsByWeek; var e = UnifiedCommitments.SafetyAuthorityOn(s);\n"
                + "var f = UnifiedCommitmentHearings.WritesOn(s);\n"
                + "var g = UnifiedCommitmentHearings.RulesOn(s);\n";
            var lines = Lines(source).Where(line => Read.IsMatch(line.code)).Select(line => line.line).ToList();
            Assert.That(lines, Is.EqualTo(new[] { 1, 3, 5, 7 }),
                "Code reads only: the lists, the views, the histories and mode 1's gates; comments, other names and the new gates are not.");
        }

        // ------------------------------------------------------------ the scan

        private static List<(string file, int line, string code)> Scan()
        {
            var sites = new List<(string file, int line, string code)>();
            string root = GamesimRoot();
            foreach (var top in new[] { "Simulation", "Runtime" })
                foreach (var path in Directory.GetFiles(Path.Combine(root, top), "*.cs", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    if (Path.GetFileName(path).StartsWith("Frozen", StringComparison.Ordinal)) continue;
                    string file = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    foreach (var line in Lines(File.ReadAllText(path)).Where(line => Read.IsMatch(line.code)))
                        sites.Add((file, line.line, line.code.Trim()));
                }
            return sites;
        }

        /// <summary>A source's lines with comments stripped (line comments and block comments both), numbered from 1.</summary>
        private static IEnumerable<(int line, string code)> Lines(string source)
        {
            var code = new System.Text.StringBuilder(source.Length);
            bool block = false;
            for (var index = 0; index < source.Length; index++)
            {
                char c = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';
                if (block)
                {
                    if (c == '*' && next == '/') { block = false; index++; }
                    else if (c == '\n') code.Append('\n');
                    continue;
                }
                if (c == '/' && next == '*') { block = true; index++; continue; }
                if (c == '/' && next == '/')
                {
                    while (index < source.Length && source[index] != '\n') index++;
                    code.Append('\n');
                    continue;
                }
                code.Append(c);
            }
            var lines = code.ToString().Split('\n');
            for (int i = 0; i < lines.Length; i++) yield return (i + 1, lines[i]);
        }

        private static string GamesimRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                if (Directory.Exists(Path.Combine(candidate, "Simulation"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
