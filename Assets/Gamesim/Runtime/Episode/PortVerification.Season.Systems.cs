using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The systems the season walk did not exist for: the weekly recap, house events, deals and
    /// the competition minigames. Each arrived after the walk was written, so a standalone build
    /// that only ever took the accessible alternative and never met a recap was being verified
    /// against the season it used to have.
    ///
    /// <para>The recap is the one that would have stopped the walk outright. It goes up after
    /// every regular eviction as a full-screen scrim that <see cref="EpisodeDirector.IsPanelOpen"/>
    /// counts, so the next "open the station" step would have found a panel already open and then
    /// looked for a button the recap does not have.</para>
    ///
    /// <para>Deals and the minigame are exercised once each and reported, in the same spirit as the
    /// optional oath: the walk proves the controls commit what they say, not that a particular
    /// outcome was reached.</para>
    /// </summary>
    public sealed partial class PortVerification
    {
        private int recapWeekSeen;
        private bool lastClickLanded;

        // ---------------------------------------------------------------- weekly recap

        /// <summary>
        /// Waits for and dismisses the recap a regular eviction owes. It arrives only once the vote
        /// reveal has played — several seconds after the commit in a graphical run — and nothing
        /// may commit in between, or the director drops it as an interruption, correctly.
        /// </summary>
        private IEnumerator DismissWeeklyRecap(EpisodeState state, bool graphical)
        {
            if (recapWeekSeen == 0) recapWeekSeen = state.week;
            // The eviction has stages now: the recap is queued when the ballots resolve and opens
            // after the reveal, while the night is still at its results - and the director drops
            // it if anything commits first. So it is owed here, before the walk presses Continue,
            // as well as in the week after for a save that resumed past the reveal.
            bool owed = (state.week > recapWeekSeen && state.phase == EpisodePhase.Social)
                || (state.phase == EpisodePhase.Eviction && state.evictionResolved && state.week >= recapWeekSeen);
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (owed && !seasonDirector.IsWeeklyRecapOpen && Time.realtimeSinceStartupAsDouble < deadline)
            { CheckSeasonDeadline(); yield return null; }

            if (!seasonDirector.IsWeeklyRecapOpen)
            {
                RequireSeason(!owed, "The week " + (state.week - 1) + " recap never opened after its eviction.");
                yield break;
            }

            if (seasonReport.weeklyRecaps == 0) yield return CaptureSeason("weekly-recap", graphical);
            // The second recap is the first with an earlier week to review.
            if (seasonReport.weeklyRecaps == 1 && HasSeasonButtonText(WeeklyRecapScreen.ReviewCaption))
            {
                yield return ClickSeasonButton(WeeklyRecapScreen.ReviewCaption);
                RequireSeason(seasonDirector.IsWeeklyRecapOpen, "Reviewing earlier weeks must keep the recap open.");
                yield return CaptureSeason("weekly-recap-review", graphical);
                yield return ClickSeasonButton(WeeklyRecapScreen.BackCaption);
            }

            var before = seasonDirector.Snapshot;
            yield return ClickSeasonButton(WeeklyRecapScreen.ContinueCaption);
            RequireSeason(!seasonDirector.IsWeeklyRecapOpen && !seasonDirector.IsPanelOpen
                && seasonDirector.Snapshot.revision == before.revision,
                "Continuing past the recap must return the player to the house without committing anything.");
            seasonReport.weeklyRecaps++;
            // Seen for this week: at the results stage the week has not advanced yet, so the next
            // week's social phase must not wait for it again.
            recapWeekSeen = state.phase == EpisodePhase.Eviction ? state.week + 1 : state.week;
        }

        // ---------------------------------------------------------------- house events

        private IEnumerator ResolveSeasonHouseEvent(EpisodeState state, bool graphical)
        {
            var item = HouseEvents.Pending(state);
            RequireSeason(item != null && item.choices.Count > 0, "A pending situation must offer at least one choice.");
            if (seasonReport.houseEventsResolved == 0) yield return CaptureSeason("house-event", graphical);

            var choice = item.choices[0];
            yield return ClickSeasonButton(EpisodeHud.EventChoiceCaption(choice.label));

            var after = seasonDirector.Snapshot;
            var resolved = after.houseEvents.FirstOrDefault(e => e.id == item.id);
            RequireSeason(after.revision == state.revision + 1 && resolved != null && resolved.resolved && resolved.chosenIndex == 0,
                "Choosing an option must resolve the situation it belongs to: " + item.title);
            seasonReport.houseEventsResolved++;
            seasonReport.houseEventKinds.Add(item.kind);
        }

        // ---------------------------------------------------------------- minigames

        /// <summary>
        /// Plays the first competition the player is in, instead of taking the accessible
        /// alternative, and drives it through the panel's own controls — never through the
        /// director's keyboard path, which a screen-reader user does not have either.
        /// </summary>
        private IEnumerator PlaySeasonMiniGame(EpisodeState state, bool graphical)
        {
            var kind = CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(state));
            yield return ClickSeasonButton(CompetitionMiniGames.EnterCaption(kind));
            RequireSeason(seasonDirector.IsChallengeActive && seasonDirector.Snapshot.revision == state.revision,
                "Entering a challenge must open it without committing.");
            yield return CaptureSeason("minigame-" + kind.ToString().ToLowerInvariant(), graphical);

            double deadline = Time.realtimeSinceStartupAsDouble + CompetitionMiniGames.TimeLimit(kind) + 55;
            int inputs = 0;
            var faces = new Dictionary<string, List<int>>();
            while (seasonDirector.IsChallengeActive && Time.realtimeSinceStartupAsDouble < deadline)
            {
                CheckSeasonDeadline();
                // A capture or expensive frame can pause the surface. Resume through its visible
                // control, just as a player does; never tick a paused attempt behind the screen.
                if (CompetitionSurface() != null && CompetitionSurface().Paused)
                { yield return TryClickCompetitionControl(CompetitionSurface().IsAssembling ? "Pause assembly" : "Pause competition"); continue; }
                switch (kind)
                {
                    case CompetitionMiniGames.Kind.Precision:
                        // Three stops and it commits itself; where the marker is does not matter here.
                        yield return TryClickSeasonButton("STOP marker  [Space]");
                        if (lastClickLanded) inputs++;
                        break;
                    case CompetitionMiniGames.Kind.Endurance:
                        // Read only the visible grip and operate the same toggle a player uses.
                        var surface = CompetitionSurface();
                        var meter = surface != null ? surface.GetComponentsInChildren<TMPro.TMP_Text>().FirstOrDefault(t=>t.name=="Grip value") : null;
                        string gripText = meter != null ? meter.text.Split('%')[0].Replace("Grip ","") : "";
                        if (float.TryParse(gripText,out float grip)
                            && (HasSeasonButtonText("Hold to earn effort") && grip > 75
                                || state.competitionRulesVersion >= 2 && HasSeasonButtonText("Release to recover") && grip < 25))
                        { yield return TryClickCompetitionControl("Toggle grip"); if(lastClickLanded)inputs++; }
                        else yield return null;
                        break;
                    case CompetitionMiniGames.Kind.Reaction:
                        if (VisibleSeasonButtons().Any(b=>b.name=="Reaction target"))
                        { yield return TryClickCompetitionControl("Reaction target"); if (lastClickLanded) inputs++; }
                        else yield return null;
                        break;
                    default:
                        yield return FlipSeasonCards(faces);
                        if (lastClickLanded) inputs++;
                        break;
                }
            }

            var committed = seasonDirector.Snapshot;
            RequireSeason(!seasonDirector.IsChallengeActive && committed.revision == state.revision + 1 && committed.competitionResolved,
                "A played challenge must commit exactly one competition result when it ends.");
            seasonReport.minigameKind = kind.ToString();
            seasonReport.minigameInputs = inputs;
            var own = committed.competitionScores.FirstOrDefault(score => score.contestantId == state.playerId);
            seasonReport.minigameScore = own != null ? own.score : -1;
        }

        /// <summary>
        /// One move on the memory board, reading it the way a player does: "Card 3" is face down,
        /// "Card 3: star" is up or matched. Faces seen are remembered; a pair both of whose cards
        /// have been seen is turned over together, otherwise one more card is learned.
        /// </summary>
        private IEnumerator FlipSeasonCards(Dictionary<string, List<int>> faces)
        {
            var cards = new Dictionary<int, string>();
            var interactable = new HashSet<int>();
            foreach (var button in VisibleSeasonButtons())
            {
                if (!button.IsActive()) continue;
                string text = button.GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text)
                    .FirstOrDefault(t => t != null && t.StartsWith("Card ", StringComparison.Ordinal));
                if (text == null) continue;
                if (text.Contains("MATCHED")) continue;
                string body = text.Substring(5);
                int colon = body.IndexOf(':');
                if (!int.TryParse(colon < 0 ? body : body.Substring(0, colon), out int number)) continue;
                int index = number - 1;
                string face = colon < 0 ? null : body.Substring(colon + 1).Trim();
                cards[index] = face;
                if (button.IsInteractable()) interactable.Add(index);
                if (face == null) continue;
                if (!faces.TryGetValue(face, out var known)) faces[face] = known = new List<int>();
                if (!known.Contains(index)) known.Add(index);
            }

            foreach (var pair in faces)
            {
                if (pair.Value.Count < 2) continue;
                int a = pair.Value[0], b = pair.Value[1];
                if (!interactable.Contains(a) || !interactable.Contains(b)) continue;
                bool aDown = cards.TryGetValue(a, out var faceA) && faceA == null;
                bool bDown = cards.TryGetValue(b, out var faceB) && faceB == null;
                if (aDown && bDown)
                {
                    yield return TryClickSeasonButton(EpisodeHud.CardCaption(a, null));
                    yield return TryClickSeasonButton(EpisodeHud.CardCaption(b, null));
                    yield break;
                }
                // One of them is already up as the first flip: complete the pair.
                if (aDown != bDown)
                {
                    yield return TryClickSeasonButton(EpisodeHud.CardCaption(aDown ? a : b, null));
                    yield break;
                }
            }

            var unknown = cards.Where(card => card.Value == null && interactable.Contains(card.Key)
                    && !faces.Values.Any(list => list.Contains(card.Key)))
                .Select(card => card.Key).OrderBy(index => index).ToList();
            int pick = unknown.Count > 0 ? unknown[0]
                : cards.Where(card => card.Value == null && interactable.Contains(card.Key))
                    .Select(card => card.Key).DefaultIfEmpty(-1).First();
            if (pick < 0) { lastClickLanded = false; yield return null; yield break; }
            yield return TryClickSeasonButton(EpisodeHud.CardCaption(pick, null));
        }

        // ---------------------------------------------------------------- deals

        /// <summary>
        /// Puts one deal to a housemate, and answers an offer of theirs if one is on the table.
        /// Optional in the same way the oath is: the week's budget may already be spent, and a
        /// housemate may not be reachable, and either is a note rather than a failure.
        /// </summary>
        private IEnumerator ExerciseOptionalDeal(bool graphical)
        {
            seasonReport.dealOutcome = "Not reached";
            var snapshot = seasonDirector.Snapshot;
            var npcs = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .Where(actor => actor.gameObject.activeInHierarchy)
                .Where(actor => { var who = snapshot.Find(actor.Id); return who != null && !who.isPlayer && who.status == ContestantStatus.Active; })
                .OrderBy(actor => actor.Id == ContentCatalog.MayaId ? 0 : 1)
                .ToList();
            if (npcs.Count == 0) { seasonReport.dealNote = "No housemate was available for the optional deal."; yield break; }

            yield return CloseSeasonPanel();
            HouseNpc chosen = null; var approach = Vector3.zero;
            foreach (var candidate in npcs)
                if (FindSeasonNpcApproach(candidate, out approach)) { chosen = candidate; break; }
            if (chosen == null) { seasonReport.dealNote = "No safe approach to any housemate; optional deal coverage was skipped."; yield break; }
            if (!seasonPlayer.TryMoveTo(approach)) { seasonReport.dealNote = "The optional deal approach was not accepted."; yield break; }
            yield return WaitSeasonWalk("optional deal conversation", approach, false);
            if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position, approach) >= 1.1f)
            { seasonReport.dealNote = "The optional deal approach did not complete within its route deadline."; yield break; }
            if (!seasonDirector.TryOpenNpc(chosen.Id)) { seasonReport.dealNote = chosen.Id + " was not interactable after approach."; yield break; }
            yield return null; yield return null;

            var before = seasonDirector.Snapshot;
            if (before.phase != EpisodePhase.Social
                || EpisodeEngine.SocialActionsSpent(before) >= EpisodeEngine.SocialActionBudget(before))
            {
                seasonReport.dealNote = "The week's social budget was already spent before the optional deal attempt.";
                yield return CloseSeasonPanel();
                yield break;
            }

            // An offer on the table is answered before anything is put to them.
            var offer = NpcDeals.Pending(before).FirstOrDefault(deal => deal.proposerId == chosen.Id);
            if (offer != null && HasSeasonButtonText(EpisodeHud.DealAcceptCaption))
            {
                yield return CaptureSeason("deal-offer", graphical);
                yield return ClickSeasonButton(EpisodeHud.DealAcceptCaption);
                var answered = seasonDirector.Snapshot.deals.FirstOrDefault(deal => deal.id == offer.id);
                RequireSeason(answered != null && answered.status != DealStatus.Proposed, "Accepting an offer must settle its status.");
                seasonReport.dealsAnswered++;
                before = seasonDirector.Snapshot;
            }

            var kinds = PlayerDeals.Available(before, chosen.Id).Where(kind => kind != DealKind.TargetAgreement).ToList();
            string chosenKind = kinds.FirstOrDefault(kind =>
                HasSeasonButtonText(EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant())));
            if (chosenKind == null)
            {
                seasonReport.dealNote = "No proposal was available to put to " + chosen.Id + " this week.";
            }
            else
            {
                yield return ClickSeasonButton(EpisodeHud.DealProposeCaption(DealKind.Title(chosenKind).ToLowerInvariant()));
                var after = seasonDirector.Snapshot;
                var deal = after.deals.FirstOrDefault(d => d.proposerId == after.playerId && d.recipientId == chosen.Id && d.type == chosenKind);
                // A refusal records no deal - the engine adds one only when the housemate agrees - but
                // the proposal is a committed command either way: one revision, and the house says
                // something about it. The first standalone run to reach this branch failed here for
                // demanding a record of a deal the housemate had turned down.
                bool said = after.events.Skip(before.events.Count).Any(e => e.kind == "deal");
                RequireSeason(after.revision == before.revision + 1 && said,
                    "A proposal control must commit the proposal it names: " + chosenKind
                    + " (revision " + before.revision + " -> " + after.revision + "; status: " + seasonDirector.StatusMessage + ")");
                seasonReport.dealsProposed++;
                seasonReport.dealKind = chosenKind;
                seasonReport.dealOutcome = deal != null ? deal.status : "Refused";
                seasonReport.dealNote = "Put to " + chosen.Id + " through the conversation panel; the housemate's answer is the season's own.";
                yield return CaptureSeason("deal-proposed", graphical);
            }
            yield return CloseSeasonPanel();
        }

        // ---------------------------------------------------------------- what the season left behind

        private void RecordSeasonSystems(EpisodeState finale)
        {
            seasonReport.storylinesBegun = finale.storylines.Count;
            seasonReport.houseEventsSeen = finale.houseEvents.Count;
            seasonReport.dealsRecorded = finale.deals.Count;
            seasonReport.modifiersCarried = finale.activeModifiers.Count;
        }

        // ---------------------------------------------------------------- helpers

        private bool HasSeasonButtonText(string caption) => VisibleSeasonButtons()
            .Any(button => button.IsActive() && button.IsInteractable()
                && button.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == caption));

        /// <summary>
        /// A click that may miss: a minigame redraws whenever a target appears or a pair turns back,
        /// so a control read one frame can be gone the next. Records whether it landed instead of
        /// failing the season over it.
        /// </summary>
        private IEnumerator TryClickSeasonButton(string caption)
        {
            lastClickLanded = false;
            var button = VisibleSeasonButtons().FirstOrDefault(b => b.IsActive() && b.IsInteractable()
                && b.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == caption));
            if (button == null) { yield return null; yield break; }
            button.Select();
            yield return null;
            if (button == null || !button.IsActive() || !button.IsInteractable()) { yield return null; yield break; }
            button.onClick.Invoke();
            lastClickLanded = true;
            yield return null; yield return null;
        }

        private CompetitionGameScreen CompetitionSurface() => seasonDirector.gameObject.scene.GetRootGameObjects()
            .SelectMany(root=>root.GetComponentsInChildren<CompetitionGameScreen>()).FirstOrDefault(s=>s.IsShowing);

        private IEnumerator TryClickCompetitionControl(string name)
        {
            lastClickLanded=false;
            var button=VisibleSeasonButtons().FirstOrDefault(b=>b.name==name);
            if(button==null){yield return null;yield break;}
            button.onClick.Invoke();lastClickLanded=true;yield return null;yield return null;
        }
    }
}
