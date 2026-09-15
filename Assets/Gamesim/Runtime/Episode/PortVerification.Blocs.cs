using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    // Explicit standalone QA only. Never publishes the private round plan into game state/UI.
    public sealed partial class PortVerification
    {
        private bool verifyBlocs;
        private BlocReport blocReport;

        private void ObserveBlocTransition(EpisodeState before, EpisodeState after)
        {
            RequireSeason(before.blocRulesStartWeek == 1 && after.blocRulesStartWeek == 1,
                "A genuinely new verification season must retain its fresh-game bloc rules marker.");
            blocReport.markerChecks++;
            if (before.phase != EpisodePhase.Eviction || before.evictionResolved) return;

            foreach (var ballot in before.votes)
            {
                var retained = after.votes.SingleOrDefault(vote => vote.voterId == ballot.voterId);
                RequireSeason(retained != null && JsonUtility.ToJson(retained) == JsonUtility.ToJson(ballot),
                    "The actual eviction UI must preserve every previously committed ballot and reason.");
                blocReport.existingBallotChecks++;
            }
            var addedNpc = after.votes.Where(vote => vote.voterId != before.playerId
                && !before.votes.Any(old => old.voterId == vote.voterId)).ToArray();
            if (addedNpc.Length > 0)
            {
                string original = JsonUtility.ToJson(before);
                var expected = WebNativeEvictionRound.Evaluate(before, addedNpc.Select(vote => vote.voterId));
                RequireSeason(JsonUtility.ToJson(before) == original,
                    "Source coordination/evaluation must leave the entire input snapshot and episode RNG unchanged.");
                RequireSeason(expected.evaluations.Count == addedNpc.Length,
                    "The source round must evaluate exactly the new NPC ballots, never the player.");
                blocReport.planningNoMutationChecks++;
                blocReport.roundPlans++;
                if (expected.coordination.results.Count > 0) blocReport.coordinatedRoundPlans++;
                foreach (var ballot in addedNpc)
                {
                    var evaluation = expected.evaluations.Single(item => item.voterId == ballot.voterId);
                    string publicReason = (ballot.voterId == before.hohId ? "HoH tie-break: " : "")
                        + WebEvictionVoting.ExplainNative(before, evaluation);
                    RequireSeason(ballot.targetId == evaluation.selectedNomineeId && ballot.reason == publicReason,
                        "The actual committed NPC ballot must match the source round and public-only explanation.");
                    // Hold serializer ordering constant: an order-only tie change is not bloc influence.
                    var uncoordinated = WebEvictionVoting.FromNative(before, ballot.voterId);
                    var nominees = uncoordinated.nominees.ToDictionary(item => item.id, StringComparer.Ordinal);
                    uncoordinated.nominees = before.contestants.Where(actor => nominees.ContainsKey(actor.id))
                        .Select(actor => nominees[actor.id]).ToList();
                    uncoordinated.blocDirective = null;
                    bool changed = WebEvictionVoting.Evaluate(uncoordinated).selectedNomineeId != ballot.targetId;
                    if (changed) blocReport.changedNpcChoices++;
                    if (ballot.voterId == before.hohId)
                    {
                        RequireSeason(!expected.coordination.directives.Any(item => item.voterId == before.hohId),
                            "An NPC HoH tie-break must never receive a bloc directive.");
                        blocReport.npcTieBreaks++;
                    }
                    blocReport.npcBallotChecks++;
                }
                blocReport.rounds.Add(new BlocRoundCheck
                {
                    week = before.week, revision = before.revision, npcBallots = addedNpc.Length,
                    eligibleBlocs = expected.coordination.results.Count, independentDraws = expected.coordination.randomDraws,
                    episodeRandomStateBefore = before.randomState.ToString(),
                    // Counts only; no hidden membership, targets, factors or defection evidence in this report.
                    revealed = after.evictionResolved
                });
            }
            var addedEvents = after.events.Where(item => item.sequence >= before.nextSequence).ToArray();
            if (!after.evictionResolved)
            {
                RequireSeason(!addedEvents.Any(item => item.kind == "vote-reveal" || item.kind == "eviction"),
                    "Pending ballots must remain unrevealed by public events.");
                RequireSeason(before.randomState == after.randomState,
                    "Pending private ballots/coordination must not consume persisted episode RNG.");
                blocReport.pendingPrivacyChecks++;
            }
            else
            {
                foreach (var ballot in after.votes)
                {
                    string expectedText = before.Find(ballot.voterId).name + " voted to evict "
                        + before.Find(ballot.targetId).name + ". " + ballot.reason;
                    RequireSeason(addedEvents.Count(item => item.kind == "vote-reveal" && item.text == expectedText) == 1,
                        "Each revealed ballot must publish exactly its existing public reason, once.");
                }
                blocReport.revealPrivacyChecks++;
            }
        }

        private void FinishBlocVerification()
        {
            if (!verifyBlocs) return;
            bool complete = blocReport != null && blocReport.markerChecks > 0 && blocReport.markerReloadChecks >= 3
                && blocReport.roundPlans >= 1 && blocReport.planningNoMutationChecks == blocReport.roundPlans
                && blocReport.npcBallotChecks >= 3 && blocReport.revealPrivacyChecks >= 1 && seasonReport.finished;
            if (!complete) RecordSeasonError("Requested bloc verification did not complete its mandatory source-round, privacy, marker and reload checks.");
            if (blocReport == null) return;
            blocReport.status = complete && seasonErrors.Count == 0 ? "Passed" : "Failed";
            blocReport.unreachedBranches = new[]
            {
                blocReport.coordinatedRoundPlans == 0 ? "No alliance retained two eligible members in an observed NPC round; source coordination with an eligible pact is not certified by this run." : null,
                blocReport.changedNpcChoices == 0 ? "No NPC target differed from the identical-order uncoordinated comparator; changed-choice coverage belongs to the deterministic legal-pact regression, not this random season." : null,
                blocReport.npcTieBreaks == 0 ? "An NPC HoH tie-break was not reached." : null,
                blocReport.pendingPrivacyChecks == 0 ? "No pending-ballot step was reached before the immediate reveal." : null,
                "Historical in-flight rule-boundary behavior is covered by frozen-save regressions; this functional season is newly created through the actual UI."
            }.Where(value => value != null).ToArray();
        }

        [Serializable] private sealed class BlocRoundCheck
        {
            public int week, revision, npcBallots, eligibleBlocs, independentDraws;
            public string episodeRandomStateBefore;
            public bool revealed;
        }

        [Serializable] private sealed class BlocReport
        {
            public bool requested = true;
            public string status = "Running";
            public string workload = "Observe regular NPC ballots committed by actual UI during a genuine new season; compare source caller targets/public reasons, unchanged prior ballots and detached RNG, reveal privacy, rule-marker reloads. Optional pact influence is reported only when reached. No fabricated cast, seed, roles, relationships or ballots.";
            public int markerChecks, markerReloadChecks, roundPlans, planningNoMutationChecks, coordinatedRoundPlans,
                npcBallotChecks, changedNpcChoices, existingBallotChecks, pendingPrivacyChecks, revealPrivacyChecks, npcTieBreaks;
            public List<BlocRoundCheck> rounds = new List<BlocRoundCheck>();
            public string[] unreachedBranches;
        }
    }
}
