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
                // At the night's results the week has not moved on yet, so the recap owed is this
                // week's; in the next week's social phase it is last week's. (rc1 and r2-walk's first
                // walk both said "week 4" for the final four's week 5.)
                int owedWeek = state.phase == EpisodePhase.Eviction ? state.week : state.week - 1;
                RequireSeason(!owed, "The week " + owedWeek + " recap never opened after its eviction (revision "
                    + state.revision + ", " + state.events.Count + " lines in the season's log).");
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
            int closedWeek = state.phase == EpisodePhase.Eviction ? state.week : state.week - 1;
            var observed = seasonReport.evictions.LastOrDefault(item => item.week == closedWeek && !item.recapObserved);
            if (observed != null)
            {
                observed.recapObserved = true;
                if (observed.entriesBefore == 256) seasonReport.fullLogEvictionRecaps++;
            }
            // Seen for this week: at the results stage the week has not advanced yet, so the next
            // week's social phase must not wait for it again.
            recapWeekSeen = state.phase == EpisodePhase.Eviction ? state.week + 1 : state.week;
        }

        // ---------------------------------------------------------------- reply cards

        /// <summary>
        /// Answers a houseguest who came to the player with the middle answer - the one that neither
        /// promises anything nor picks a fight - so the walk's own choices stay its own.
        /// </summary>
        private IEnumerator AnswerSeasonReplyCard(EpisodeState state)
        {
            var card = ReplyCards.Pending(state);
            var replies = card == null ? null : ReplyCards.Replies(card.kind);
            RequireSeason(replies != null && replies.Length >= 2, "A reply card must offer its answers.");
            yield return ClickSeasonButton(EpisodeHud.ReplyCaption(replies[1].Label));
            var after = seasonDirector.Snapshot;
            RequireSeason(after.revision == state.revision + 1 && after.replyCards.All(r => r.id != card.id),
                "Answering must settle the card it belongs to: " + card.kind);
            seasonReport.replyCardsAnswered++;
        }

        // ---------------------------------------------------------------- house events

        private IEnumerator ResolveSeasonHouseEvent(EpisodeState state, bool graphical)
        {
            var item = HouseEvents.Pending(state);
            RequireSeason(item != null && item.choices.Count > 0, "A pending situation must offer at least one choice.");
            // Free time's board shows the first beat waiting as a banner (EpisodeDirector.FreeTimeBoard.cs),
            // and its Answer opens the beat as the step, where its options are. View state: the step's
            // one commit is still the option below.
            if (item.IsStory && HasSeasonButton(EpisodeDirector.AnswerBeatCaption))
                yield return ClickSeasonButton(EpisodeDirector.AnswerBeatCaption);
            if (seasonReport.houseEventsResolved == 0) yield return CaptureSeason("house-event", graphical);

            // A story beat's first option may be locked, may name somebody or may be a rule break;
            // those take a second press. The first option that answers in one press does, and the
            // lapse option - never locked - always can. Under the role moments a promise of the veto
            // after the player's word on it is drawn locked as well (R3).
            int index = 0;
            if (item.IsStory)
            {
                bool actionsLeft = EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state);
                bool Answers(HouseEventChoice c) => !c.locked && EpisodeEngine.VetoWordRefusal(state, c) == null
                    && !c.pickPerson && !c.conduct && (!c.costsAction || actionsLeft);
                // The first answer that is not the lapse: a play's step is played, not let go.
                index = item.choices.FindIndex(c => !c.lapse && Answers(c));
                if (index < 0) index = item.choices.FindIndex(c => Answers(c));
                if (index < 0) index = item.choices.FindIndex(c => c.lapse);
            }
            var choice = item.choices[index];
            yield return ClickSeasonButton(EpisodeHud.EventChoiceCaption(choice.label));

            var after = seasonDirector.Snapshot;
            var resolved = after.houseEvents.FirstOrDefault(e => e.id == item.id);
            RequireSeason(after.revision == state.revision + 1 && resolved != null && resolved.resolved && resolved.chosenIndex == index,
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
                    case CompetitionMiniGames.Kind.Dice:
                        // One roll, then keep it: the same two buttons a player presses.
                        if (VisibleSeasonButtons().Any(b=>b.name=="Keep roll")) yield return TryClickCompetitionControl("Keep roll");
                        else yield return TryClickCompetitionControl("Roll dice");
                        if (lastClickLanded) inputs++;
                        break;
                    case CompetitionMiniGames.Kind.Words:
                        yield return SpellSeasonWord();
                        if (lastClickLanded) inputs++;
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
        /// An incoming answer stays free when the paid proposal budget is spent. A housemate may
        /// still be unreachable, and a paid proposal with no time left is a note rather than a failure.
        /// </summary>
        private IEnumerator ExerciseOptionalDeal(bool graphical)
        {
            seasonReport.dealOutcome = "Not reached";
            var snapshot = seasonDirector.Snapshot;
            if (snapshot.phase != EpisodePhase.Social || snapshot.Find(snapshot.playerId)?.status != ContestantStatus.Active)
            {
                RecordSeasonConversationAttempt("GenericDeal", "Ineligible", snapshot, null, null,
                    "The generic companion requires an active player in Social.");
                seasonReport.dealNote = "Generic proposal is out of scope outside active-player free time.";
                yield break;
            }
            // An already-open canonical owner precedes unrelated body/route discovery.
            // This only classifies the current view; never navigate or answer it to find a deal.
            string currentNpc = seasonDirector.TalkingToId;
            string currentOwner = string.IsNullOrEmpty(currentNpc) ? null : SeasonConversationBlocker(snapshot, currentNpc);
            if (currentOwner != null)
            {
                RecordSeasonConversationAttempt("GenericDeal", "BlockedByOtherView", snapshot, currentNpc, null, currentOwner);
                seasonReport.dealNote = currentOwner; yield break;
            }
            var npcs = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true))
                .Where(actor => actor.gameObject.activeInHierarchy)
                .Where(actor => { var who = snapshot.Find(actor.Id); return who != null && !who.isPlayer && who.status == ContestantStatus.Active; })
                .OrderBy(actor => actor.Id == ContentCatalog.MayaId ? 0 : 1).ToList();
            if (npcs.Count == 0)
            {
                RecordSeasonConversationAttempt("GenericDeal", "Unreachable", snapshot, null, null, "No active housemate body.");
                seasonReport.dealNote = "No housemate was available for the optional deal."; yield break;
            }
            HouseNpc chosen = null; var approach = Vector3.zero;
            foreach (var candidate in npcs)
                if (FindSeasonNpcApproach(candidate, out approach)) { chosen = candidate; break; }
            if (chosen == null)
            {
                RecordSeasonConversationAttempt("GenericDeal", "Unreachable", snapshot, null, null, "No safe active-body approach.");
                seasonReport.dealNote = "No safe approach to any housemate."; yield break;
            }
            string blocker = SeasonConversationBlocker(snapshot, seasonDirector.TalkingToId)
                ?? SeasonConversationBlocker(snapshot, chosen.Id);
            if (blocker != null)
            {
                RecordSeasonConversationAttempt("GenericDeal", "BlockedByOtherView", snapshot, chosen.Id, null, blocker);
                seasonReport.dealNote = blocker; yield break;
            }
            var selected = NpcDeals.Pending(snapshot).FirstOrDefault(deal => deal.proposerId == chosen.Id);
            bool invited = seasonDirector.IsConversationOpen && seasonDirector.InvitedDealRecordId != null;
            if (invited && (seasonDirector.TalkingToId != chosen.Id || selected == null
                || seasonDirector.InvitedDealRecordId != selected.id))
            {
                RecordSeasonConversationAttempt("PendingOffer", "BlockedByOtherView", snapshot, chosen.Id, EpisodeHud.DealAcceptCaption,
                    "A different actual invitation owns this view.", canonicalId: seasonDirector.InvitedDealRecordId);
                yield break;
            }
            if (!invited)
            {
                yield return CloseSeasonPanel();
                string missed = null;
                if (!seasonPlayer.TryMoveTo(approach)) missed = "The optional deal approach was not accepted.";
                else
                {
                    yield return WaitSeasonWalk("optional deal conversation", approach, false);
                    if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position, approach) >= 1.1f)
                        missed = "The optional deal approach did not complete within its route deadline.";
                    else if (!seasonDirector.TryOpenNpc(chosen.Id)) missed = chosen.Id + " was not interactable after approach.";
                }
                if (missed != null)
                {
                    RecordSeasonConversationAttempt("GenericDeal", "Unreachable", seasonDirector.Snapshot, chosen.Id, null, missed);
                    seasonReport.dealNote = missed; yield break;
                }
                yield return null; yield return null;
                yield return DrainSeasonConversationExchange("before-generic-deal", false);
            }
            var before = seasonDirector.Snapshot;
            blocker = SeasonConversationBlocker(before, chosen.Id);
            if (blocker != null)
            {
                RecordSeasonConversationAttempt("GenericDeal", "BlockedByOtherView", before, chosen.Id, null, blocker);
                seasonReport.dealNote = blocker; yield break;
            }
            if (!invited) yield return EnterSeasonConversationCategory(EpisodeDirector.DealsCategoryCaption);
            before = seasonDirector.Snapshot;
            RequireSeason(before.phase == EpisodePhase.Social && before.Find(before.playerId)?.status == ContestantStatus.Active
                && before.Find(chosen.Id)?.status == ContestantStatus.Active,
                "The selected generic conversation changed its active-player/Social source.");
            var offer = NpcDeals.Pending(before).FirstOrDefault(deal => deal.proposerId == chosen.Id);
            if (invited)
                RequireSeason(offer != null && seasonDirector.InvitedDealRecordId == offer.id,
                    "Navigation may not replace the selected real incoming invitation.");

            // Canonical selected identity and its real display locks precede ALL paid-AP guards.
            if (offer != null)
            {
                RequireSeason(offer.recipientId == before.playerId && offer.status == DealStatus.Proposed,
                    "The selected pending offer must belong to the active player.");
                string legalLock = SeasonPendingOfferLock(before, offer);
                if (offer.type == DealKind.FinalThree
                    && (before.Active.Count() <= NpcDeals.FinalThreeSize || NpcDeals.FinalFourBlockSet(before)))
                {
                    RecordSeasonConversationAttempt("PendingOffer", "PresentationMismatch", before, chosen.Id,
                        EpisodeHud.DealAcceptCaption, "The engine refuses this final-three yes but the renderer has no matching lock.", canonicalId: offer.id);
                    RequireSeason(false, "A source-known incoming-offer refusal needs its actual presentation lock.");
                }
                var baseline = ConversationBaseline();
                var accept = RequireSeasonOfferControl(before, offer, true, legalLock == null);
                var decline = RequireSeasonOfferControl(before, offer, false, true);
                if (legalLock != null)
                {
                    RequireSeason(accept.GetComponentInParent<Canvas>().GetComponentsInChildren<TMPro.TMP_Text>()
                        .Any(label => label.gameObject.activeInHierarchy && label.text == legalLock),
                        "A lawful selected-offer lock must retain its actual explanatory line.");
                    RequireConversationUnchanged(baseline, "lawfully locked incoming offer");
                    RecordSeasonConversationAttempt("PendingOffer", "LegallyLocked", before, chosen.Id,
                        EpisodeHud.DealAcceptCaption, legalLock + " Decline remains unpressed.", kind: offer.type, canonicalId: offer.id);
                }
                else
                {
                    var untouched = NpcDeals.Pending(before).Where(row => row.id != offer.id)
                        .ToDictionary(row => row.id, row => JsonUtility.ToJson(row), StringComparer.Ordinal);
                    yield return CaptureSeason("deal-offer", graphical);
                    yield return ClickSeasonOfferControl(before, offer, baseline);
                    var answeredState = seasonDirector.Snapshot;
                    RequireSeasonConversationCommit(before, answeredState, baseline, false, "PendingOffer");
                    var answered = SeasonDeal(answeredState, offer.id);
                    RequireSeason(answered != null && answered.status == DealStatus.Active
                        && answered.id == offer.id && answered.proposerId == offer.proposerId
                        && answered.recipientId == offer.recipientId && answered.type == offer.type
                        && answered.targetId == offer.targetId,
                        "The free control must settle exactly the selected source offer, never a same-caption sibling.");
                    RequireSeasonDealOwner(answeredState, answered);
                    foreach (var row in untouched)
                        RequireSeason(JsonUtility.ToJson(SeasonDeal(answeredState, row.Key)) == row.Value,
                            "Accepting one offer changed an unselected pending source row: " + row.Key);
                    seasonReport.dealsAnswered++; seasonReport.commands++;
                    seasonReport.dealKind = offer.type; seasonReport.dealOutcome = answered.status;
                    RecordSeasonConversationAttempt("PendingOffer", "Committed", before, chosen.Id, EpisodeHud.DealAcceptCaption,
                        "One exact free incoming answer; any actual linked veto price remains source-owned.",
                        answeredState, offer.type, offer.targetId, offer.id, answered.status);
                    yield return DrainSeasonConversationExchange("deal-accepted", graphical);
                }
            }
            else RecordSeasonConversationAttempt("PendingOffer", "Ineligible", before, chosen.Id,
                EpisodeHud.DealAcceptCaption, "No canonical pending offer from the selected housemate.");

            // Do not clear a new story/share owner to manufacture another optional opportunity.
            before = seasonDirector.Snapshot;
            blocker = SeasonConversationBlocker(before, chosen.Id);
            if (blocker != null)
            {
                RecordSeasonConversationAttempt("GenericDeal", "BlockedByOtherView", before, chosen.Id, null, blocker);
                seasonReport.dealNote = blocker; yield break;
            }
            // No-offer conversations already own the ordinary page; rebuilding it would
            // replace the very controls whose presence and command behavior we must verify.
            if (offer != null) yield return EnterSeasonConversationCategory(EpisodeDirector.DealsCategoryCaption);
            before = seasonDirector.Snapshot;
            if (EpisodeEngine.SocialActionsSpent(before) >= EpisodeEngine.SocialActionBudget(before))
            {
                RecordSeasonConversationAttempt("GenericDeal", "Ineligible", before, chosen.Id, null,
                    "No paid action remains after the separately classified free offer.");
                seasonReport.dealNote = seasonReport.dealsAnswered > 0
                    ? "Answered the exact incoming offer; no paid social action remained for another proposal."
                    : "The week's social budget was already spent before the optional paid proposal.";
                yield return CloseSeasonPanel(); yield break;
            }

            string chosenKind = null;
            var kinds = PlayerDeals.Available(before, chosen.Id);
            foreach (string kind in kinds)
            {
                bool picker = kind == DealKind.TargetAgreement;
                bool named = EpisodeEngine.LeverRulesOn(before) && (kind == DealKind.VoteSave || kind == DealKind.VoteEvict);
                if (picker || named)
                {
                    RecordSeasonConversationAttempt("GenericDeal", "UnsupportedNotSelected", before, chosen.Id, null,
                        picker ? "Target subject selection is outside this generic policy." : "Named vote belongs to the separate Campaign companion.", kind: kind);
                    continue;
                }
                chosenKind = kind; break;
            }
            if (chosenKind == null)
            {
                RecordSeasonConversationAttempt("GenericDeal", kinds.Count == 0 ? "Ineligible" : "UnsupportedNotSelected",
                    before, chosen.Id, null, "No supported canonical generic form; no leaf or command coverage claimed.");
                seasonReport.dealNote = "No supported generic proposal was selected for " + chosen.Id + ".";
                yield return CloseSeasonPanel(); yield break;
            }
            string caption = EpisodeHud.DealProposeCaption(DealKind.Title(chosenKind).ToLowerInvariant());
            RequireSeason(PlayerDeals.CanPropose(before, chosen.Id, chosenKind, null, out var reason),
                "The canonical generic selection changed eligibility: " + reason);
            RequireSeasonConversationLeaf("GenericDeal", chosen.Id, EpisodeDirector.DealsCategoryCaption, caption);
            var paidBaseline = ConversationBaseline();
            var priorIds = new HashSet<string>(CommitmentReferences.Deals(before).Select(row => row.id), StringComparer.Ordinal);
            yield return ClickSeasonButton(caption);
            var after = seasonDirector.Snapshot;
            RequireSeasonConversationCommit(before, after, paidBaseline, true, "GenericDeal");
            RequireSeason(EpisodeDirector.CommittedEvents(after, before.nextSequence).Any(row => row.kind == "deal"),
                "The exact paid proposal must publish its actual answer, including a real refusal.");
            var added = CommitmentReferences.Deals(after).Where(row => !priorIds.Contains(row.id)
                && row.proposerId == after.playerId && row.recipientId == chosen.Id && row.type == chosenKind
                && string.IsNullOrEmpty(row.targetId)).ToArray();
            RequireSeason(added.Length <= 1, "One ordinary proposal created duplicate matching owners.");
            var deal = added.SingleOrDefault();
            RequireSeason(CommitmentReferences.Deals(after).Count(row => !priorIds.Contains(row.id)
                && row.proposerId == after.playerId) == added.Length,
                "The proposal control committed a different kind, subject or recipient.");
            string answerPrefix = before.Find(chosen.Id).name + (deal == null ? " turned down a " : " agreed a ")
                + DealKind.Title(chosenKind).ToLowerInvariant() + ". ";
            RequireSeason(EpisodeDirector.CommittedEvents(after, before.nextSequence).Count(row => row.kind == "deal"
                && row.text != null && row.text.StartsWith(answerPrefix, StringComparison.Ordinal)
                && row.audienceIds != null && row.audienceIds.Contains(before.playerId) && row.audienceIds.Contains(chosen.Id)) == 1,
                "The exact selected generic tuple must publish its own actual agreed/refused answer.");
            if (deal != null) RequireSeasonDealOwner(after, deal);
            seasonReport.dealsProposed++; seasonReport.commands++;
            seasonReport.dealKind = chosenKind; seasonReport.dealOutcome = deal != null ? deal.status : "Refused";
            var counter = Negotiation.OpenCounter(after, chosen.Id);
            seasonReport.dealNote = "One canonical-first visible generic proposal; the actual housemate's answer is retained."
                + (counter != null ? " Its actual counter remains unanswered; no retry or automatic answer." : "");
            RecordSeasonConversationAttempt("GenericDeal", "Committed", before, chosen.Id, caption,
                deal == null ? "Real committed refusal, no agreement promoted." : "New exact source-owned agreement.",
                after, chosenKind, null, deal?.id, seasonReport.dealOutcome);
            yield return CaptureSeason("deal-proposed", graphical);
            yield return DrainSeasonConversationExchange("deal-answer", graphical);
            yield return CloseSeasonPanel();
        }

        private static string SeasonPendingOfferLock(EpisodeState state, DealState offer)
        {
            if (offer.type == DealKind.AllianceInvite && EpisodeEngine.InvitationPastPactCap(state, offer.proposerId))
                return EpisodeDirector.PactCapOfferLine(state, offer.proposerId);
            // A second word on the veto this week, as the rows draw it: under the role moments the Head of Household's ask
            // to keep the block is one too (ROLE-MOMENTS-PLAN R4).
            return EpisodeDirector.VetoWordLockLine(state, offer);
        }

        private Button[] SeasonOfferControls(EpisodeState source, DealState offer, bool accept)
        {
            var roots = seasonDirector.gameObject.scene.GetRootGameObjects();
            var buttons = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .Where(button => button.IsActive() && button.gameObject.activeInHierarchy).ToArray();
            int top = buttons.Select(button => button.GetComponentInParent<Canvas>()?.sortingOrder ?? 0).DefaultIfEmpty(0).Max();
            var huds = roots.SelectMany(root => root.GetComponentsInChildren<EpisodeHud>(true)).ToArray();
            return buttons.Where(button => (button.GetComponentInParent<Canvas>()?.sortingOrder ?? 0) == top
                && huds.Any(hud => hud.TryDescribeOfferControl(button, out var row)
                    && row.OfferId == offer.id && row.ProposerId == offer.proposerId && row.RecipientId == offer.recipientId
                    && row.Accept == accept && row.SessionId == source.sessionId
                    && row.Revision == source.revision && row.Phase == source.phase)).ToArray();
        }

        private Button RequireSeasonOfferControl(EpisodeState source, DealState offer, bool accept, bool interactable)
        {
            string caption = accept ? EpisodeHud.DealAcceptCaption : EpisodeHud.DealDeclineCaption;
            var rows = SeasonOfferControls(source, offer, accept);
            bool exact = rows.Length == 1 && rows[0].IsInteractable() == interactable
                && rows[0].GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == caption);
            if (!exact)
                RecordSeasonConversationAttempt("PendingOffer", "EligibleMissing", source, offer.proposerId, caption,
                    "Selected canonical offer lacks its exact current rendered " + (interactable ? "pressable" : "locked") + " control.",
                    kind: offer.type, canonicalId: offer.id);
            RequireSeason(exact, "Eligible conversation leaf is missing or not uniquely pressable: PendingOffer / "
                + caption + " / " + offer.id + ".");
            return rows[0];
        }

        private IEnumerator ClickSeasonOfferControl(EpisodeState source, DealState offer, ConversationNavigationBaseline baseline)
        {
            CheckSeasonDeadline();
            var button = RequireSeasonOfferControl(source, offer, true, true);
            button.Select(); yield return null; yield return null;
            RequireConversationUnchanged(baseline, "selected incoming offer focus/refind");
            button = RequireSeasonOfferControl(source, offer, true, true);
            button.onClick.Invoke(); yield return null; yield return null;
            var after = seasonDirector.Snapshot;
            seasonReport.buttons.Add(new SeasonButton { caption = EpisodeHud.DealAcceptCaption,
                phase = source.phase.ToString(), revisionBefore = source.revision, revisionAfter = after.revision,
                elapsedSeconds = Time.realtimeSinceStartupAsDouble - seasonStarted });
            ObserveSeasonEvictions(source, after);
        }

        private void RequireSeasonDealOwner(EpisodeState state, DealState projection)
        {
            var canonical = state.unifiedCommitments.Where(row => row.id == projection.id).ToArray();
            int raw = state.deals.Count(row => row.id == projection.id);
            RequireSeason(canonical.Length == 1 ? raw == 0 : canonical.Length == 0 && raw == 1,
                "A selected agreement must have one actual owner and no writable canonical mirror: " + projection.id);
        }

        // ---------------------------------------------------------------- required conversation leaves

        private string SeasonConversationBlocker(EpisodeState state, string npcId)
        {
            if (seasonDirector.IsChoosingSharedInformation) return "The current information-sharing choice owns the view.";
            if (string.IsNullOrEmpty(npcId) || state.Find(state.playerId)?.status != ContestantStatus.Active) return null;
            // An invitation can precede this story in the renderer; Back would expose the story.
            // Recognize its canonical ownership before normalization, never answer it to get a leaf.
            var beat = EpisodeEngine.OpenStoryBeats(state).FirstOrDefault(item => item.surface == StorySurfaces.Conversation
                && (item.involvedIds.Contains(npcId) || item.cast.Any(role => role.contestantId == npcId)));
            return beat != null ? "Conversation story " + beat.id + " owns " + npcId + "." : null;
        }

        private SeasonConversationAttempt RecordSeasonConversationAttempt(string feature, string disposition,
            EpisodeState before, string npcId, string caption, string note, EpisodeState after = null,
            string kind = null, string subjectId = null, string canonicalId = null, string outcome = null)
        {
            after = after ?? before;
            var row = new SeasonConversationAttempt
            {
                feature = feature, disposition = disposition, phase = before.phase.ToString(), npcId = npcId, subjectId = subjectId,
                kind = kind, caption = caption, note = note, canonicalId = canonicalId, outcome = outcome,
                week = before.week, unifiedMode = before.unifiedCommitmentRulesVersion,
                revisionBefore = before.revision, revisionAfter = after.revision,
                spentBefore = EpisodeEngine.SocialActionsSpent(before), spentAfter = EpisodeEngine.SocialActionsSpent(after),
                budgetBefore = EpisodeEngine.SocialActionBudget(before), budgetAfter = EpisodeEngine.SocialActionBudget(after),
            };
            seasonReport.conversationAttempts.Add(row);
            return row;
        }

        private void RequireSeasonConversationLeaf(string feature, string npcId, string category, string caption)
        {
            RequireSeason(seasonDirector.IsConversationOpen && seasonDirector.TalkingToId == npcId
                && seasonDirector.CurrentConversationCategory == category, "The selected leaf requires its actual ordinary category: " + feature);
            RequireSeason(SeasonConversationBlocker(seasonDirector.Snapshot, npcId) == null,
                "A different known owner appeared before the required conversation leaf: " + feature);
            int count = SeasonButtonsCarrying(caption).Length;
            if (count != 1)
                RecordSeasonConversationAttempt(feature, "EligibleMissing", seasonDirector.Snapshot, npcId, caption,
                    "Expected one active/interactable ordinary control; found " + count + ".");
            RequireSeason(count == 1, "Eligible conversation leaf is missing or not uniquely pressable: "
                + feature + " / " + caption + " / " + npcId + "." + SeasonButtonsOnOffer());
        }

        private void RequireSeasonLockedConversationLeaf(string caption)
        {
            var rows = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .Where(button => button.IsActive() && button.gameObject.activeInHierarchy
                    && button.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == caption)).ToArray();
            RequireSeason(rows.Length == 1 && !rows[0].IsInteractable(),
                "A source-explained legal lock must still draw its exact locked row: " + caption);
        }

        private void RequireSeasonConversationCommit(EpisodeState before, EpisodeState after,
            ConversationNavigationBaseline baseline, bool paid, string feature)
        {
            if (after.revision == before.revision)
                RequireConversationUnchanged(baseline, feature + " refused/no-op control");
            RequireSeason(after.revision == before.revision + 1
                && after.acceptedCommandIds.Count == before.acceptedCommandIds.Count + 1
                && after.acceptedCommandIds.Take(before.acceptedCommandIds.Count).SequenceEqual(before.acceptedCommandIds)
                && !string.IsNullOrEmpty(after.acceptedCommandIds.Last()),
                "The selected conversation leaf must commit exactly one fresh command: " + feature);
            RequireSeason(after.phase == before.phase && after.week == before.week
                && EpisodeEngine.SocialActionBudget(after) == EpisodeEngine.SocialActionBudget(before)
                && EpisodeEngine.SocialActionsSpent(after) == EpisodeEngine.SocialActionsSpent(before) + (paid ? 1 : 0),
                "The selected conversation leaf must retain its exact " + (paid ? "one paid" : "free") + " action boundary: " + feature);
            RequireSeason(EpisodeValidation.TryValidate(after, out var reason), "Invalid conversation result: " + feature + " / " + reason);
            RequireSeason(seasonDirector.SavePath == baseline.savePath, "A conversation command changed the selected slot: " + feature);
            var store = new Gamesim.Persistence.EpisodeSaveStore(baseline.savePath);
            RequireSeason(store.TryLoad(out var loaded, out reason) && JsonUtility.ToJson(loaded) == JsonUtility.ToJson(after),
                "The actual conversation result must be durably reloadable: " + feature + " / " + reason);
            RequireSeason(System.IO.File.Exists(store.BackupPath)
                && ReadConversationSlotBytes(store.BackupPath).SequenceEqual(baseline.saved),
                "One conversation command must retain the exact previous primary as backup: " + feature);
        }

        // ---------------------------------------------------------------- one actual Campaign named vote proposal

        private bool seasonNamedVoteAttempted;

        private static string SeasonNamedVoteIneligible(EpisodeState state)
        {
            if (state.phase != EpisodePhase.Campaign || state.Find(state.playerId)?.status != ContestantStatus.Active)
                return "An active player in Campaign is required.";
            if (state.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version || !EpisodeEngine.LeverRulesOn(state))
                return "This separate companion selects mode-2 named votes under the levers only.";
            if (EpisodeEngine.SocialActionsSpent(state) >= EpisodeEngine.SocialActionBudget(state))
                return "No existing paid action remains; no extra allowance is granted.";
            return null;
        }

        private IEnumerator ExerciseSeasonNamedVoteDeal(bool graphical)
        {
            if (seasonNamedVoteAttempted) yield break;
            var source = seasonDirector.Snapshot;
            string ineligible = SeasonNamedVoteIneligible(source);
            if (ineligible != null)
            {
                seasonNamedVoteAttempted = true; seasonReport.namedVoteDisposition = "Ineligible";
                RecordSeasonConversationAttempt("NamedVote", "Ineligible", source, null, null, ineligible);
                yield break;
            }
            // Candidate ordering is canonical and independent of visible captions. Physical
            // reachability may choose a body; a hidden preferred child may NOT choose another kind.
            var bodies = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true)).Where(npc => npc.gameObject.activeInHierarchy).ToList();
            HouseNpc chosen = null; string kind = null, subjectId = null;
            var approach = Vector3.zero; bool anyEligible = false;
            foreach (var voter in EpisodeEngine.Voters(source).Where(v => !v.isPlayer))
            {
                string voterKind = null, voterSubject = null;
                foreach (string candidateKind in new[] { DealKind.VoteEvict, DealKind.VoteSave })
                {
                    voterSubject = source.nominees.FirstOrDefault(id => id != source.playerId
                        && PlayerDeals.CanPropose(source, voter.id, candidateKind, id, out _));
                    if (voterSubject != null) { voterKind = candidateKind; break; }
                }
                if (voterKind == null) continue;
                anyEligible = true;
                var body = bodies.FirstOrDefault(npc => npc.Id == voter.id);
                if (body == null || !FindSeasonNpcApproach(body, out approach)) continue;
                chosen = body; kind = voterKind; subjectId = voterSubject; break;
            }
            if (chosen == null)
            {
                seasonNamedVoteAttempted = true;
                seasonReport.namedVoteDisposition = anyEligible ? "Unreachable" : "Ineligible";
                RecordSeasonConversationAttempt("NamedVote", seasonReport.namedVoteDisposition, source, null, null,
                    anyEligible ? "No eligible voter had a safe active-body approach." : "No canonical nonplayer-subject proposal is eligible.");
                yield break;
            }
            string caption = EpisodeHud.VoteDealCaption(kind, source.Find(subjectId).name);
            seasonReport.namedVoteKind = kind; seasonReport.namedVoteNpcId = chosen.Id; seasonReport.namedVoteSubjectId = subjectId;
            string owner = SeasonConversationBlocker(source, seasonDirector.TalkingToId)
                ?? SeasonConversationBlocker(source, chosen.Id);
            if (owner != null)
            {
                seasonNamedVoteAttempted = true; seasonReport.namedVoteDisposition = "BlockedByOtherView";
                RecordSeasonConversationAttempt("NamedVote", "BlockedByOtherView", source, chosen.Id, caption, owner,
                    kind: kind, subjectId: subjectId);
                yield break;
            }
            yield return CloseSeasonPanel();
            string missed = null;
            if (!seasonPlayer.TryMoveTo(approach)) missed = "The selected voter approach was not accepted.";
            else
            {
                yield return WaitSeasonWalk("named vote conversation", approach, false);
                if (!seasonPlayer.HasArrived || Vector3.Distance(seasonPlayer.transform.position, approach) >= 1.1f)
                    missed = "The selected voter route deadline expired.";
                else if (!seasonDirector.TryOpenNpc(chosen.Id)) missed = "The reached voter was not interactable.";
            }
            if (missed != null)
            {
                seasonNamedVoteAttempted = true; seasonReport.namedVoteDisposition = "Unreachable";
                RecordSeasonConversationAttempt("NamedVote", "Unreachable", seasonDirector.Snapshot, chosen.Id, caption, missed,
                    kind: kind, subjectId: subjectId);
                yield break;
            }
            yield return null; yield return null;
            yield return DrainSeasonConversationExchange("before-named-vote", false);
            var before = seasonDirector.Snapshot;
            ineligible = SeasonNamedVoteIneligible(before);
            if (ineligible == null && (!EpisodeEngine.Voters(before).Any(v => v.id == chosen.Id)
                || !before.nominees.Contains(subjectId) || subjectId == before.playerId))
                ineligible = "The selected voter or nonplayer nominee changed during the approach.";
            if (ineligible == null && !PlayerDeals.CanPropose(before, chosen.Id, kind, subjectId, out ineligible))
                ineligible = ineligible ?? "The selected proposal is no longer eligible.";
            owner = SeasonConversationBlocker(before, chosen.Id);
            if (ineligible != null || owner != null)
            {
                seasonNamedVoteAttempted = true;
                seasonReport.namedVoteDisposition = owner != null ? "BlockedByOtherView" : "Ineligible";
                RecordSeasonConversationAttempt("NamedVote", seasonReport.namedVoteDisposition, before, chosen.Id, caption,
                    owner ?? ineligible, kind: kind, subjectId: subjectId);
                yield return CloseSeasonPanel(); yield break;
            }
            yield return EnterSeasonConversationCategory(EpisodeDirector.DealsCategoryCaption);
            RequireSeasonConversationLeaf("NamedVote", chosen.Id, EpisodeDirector.DealsCategoryCaption, caption);
            before = seasonDirector.Snapshot;
            var baseline = ConversationBaseline();
            yield return CaptureSeason("named-vote-selected", graphical);
            RequireConversationUnchanged(baseline, "Capturing the selected named vote");
            yield return ClickSeasonButton(caption);
            var after = seasonDirector.Snapshot;
            RequireSeasonConversationCommit(before, after, baseline, true, "NamedVote");
            var committedEvents = EpisodeDirector.CommittedEvents(after, before.nextSequence).ToArray();
            RequireSeason(committedEvents.Any(e => e.kind == "deal"),
                "A named proposal must publish its actual deal answer.");
            var created = after.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy
                && row.origin == UnifiedCommitments.PlayerDeal && !before.unifiedCommitments.Any(old => old.id == row.id)).ToArray();
            RequireSeason(created.Length <= 1, "One named proposal may not publish multiple player-deal receipts.");
            var receipt = created.SingleOrDefault();
            if (receipt != null)
            {
                var projection = SeasonDeal(after, receipt.id);
                RequireSeason(receipt.kind == UnifiedVoteTogether.Vote && receipt.subtype == kind
                    && receipt.makerId == before.playerId && receipt.beneficiaryId == chosen.Id && receipt.targetId == subjectId
                    && receipt.status == DealStatus.Active && receipt.reciprocal && receipt.createdWeek == before.week
                    && receipt.expiresWeek == before.week && receipt.voteBindingWeek == before.week && receipt.voteFirstRevealWeek == before.week
                    && projection != null && projection.id == receipt.id && projection.type == kind
                    && projection.proposerId == before.playerId && projection.recipientId == chosen.Id && projection.targetId == subjectId
                    && projection.status == receipt.status && !after.deals.Any(raw => raw.id == receipt.id),
                    "The accepted named vote must live in its exact mode-2 canonical row and detached deal projection.");
                seasonReport.namedVoteReceipts++;
            }
            seasonNamedVoteAttempted = true;
            seasonReport.namedVoteDisposition = "Committed";
            seasonReport.namedVoteProposals++; seasonReport.dealsProposed++; seasonReport.commands++;
            seasonReport.namedVoteKind = kind; seasonReport.namedVoteNpcId = chosen.Id; seasonReport.namedVoteSubjectId = subjectId;
            seasonReport.namedVoteReceiptId = receipt?.id;
            seasonReport.namedVoteOutcome = receipt != null ? receipt.status
                : committedEvents.Any(e => e.kind == Negotiation.CounterEventKind)
                    ? "Refused; counter offered (unanswered)" : "Refused";
            RecordSeasonConversationAttempt("NamedVote", "Committed", before, chosen.Id, caption,
                "One visible proposal; the real answer is not retried. Other named kinds remain unexercised.", after,
                kind, subjectId, receipt?.id, seasonReport.namedVoteOutcome);
            var answerBaseline = ConversationBaseline();
            yield return CaptureSeason("named-vote-answer", graphical);
            RequireConversationUnchanged(answerBaseline, "Capturing the named vote answer");
            yield return DrainSeasonConversationExchange("named-vote-answer", graphical);
            yield return CloseSeasonPanel();
        }

        // ---------------------------------------------------------------- what the season left behind

        private void RecordSeasonSystems(EpisodeState finale)
        {
            seasonReport.storylinesBegun = finale.storylines.Count;
            seasonReport.houseEventsSeen = finale.houseEvents.Count;
            seasonReport.dealsRecorded = CommitmentReferences.Deals(finale).Count;
            seasonReport.modifiersCarried = finale.activeModifiers.Count;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// A deal of the season by its id, wherever it is held: the raw list, or a canonical row through the view (vote family
        /// V5f) - an accepted canonical Safety offer in mode 1, and in mode 2 every vote deal, which the raw list never holds.
        /// </summary>
        public static DealState SeasonDeal(EpisodeState state, string id) => CommitmentReferences.FindDeal(state, id);

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

        /// <summary>
        /// One letter on the word board, read the way a player does: the tiles still open are the
        /// letters left, and a word from the game's list - or a houseguest's name - that the letters
        /// on the board spell is the answer. Chooses the next letter of it, or skips a word it cannot read.
        /// </summary>
        private IEnumerator SpellSeasonWord()
        {
            lastClickLanded = false;
            var tiles = VisibleSeasonButtons().Where(b => b.name.StartsWith("Letter tile ", StringComparison.Ordinal))
                .OrderBy(b => int.Parse(b.name.Substring(12))).ToList();
            var surface = CompetitionSurface();
            var all = surface != null ? surface.GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.activeInHierarchy && b.name.StartsWith("Letter tile ", StringComparison.Ordinal))
                .OrderBy(b => int.Parse(b.name.Substring(12))).ToList() : new List<Button>();
            if (tiles.Count == 0 || all.Count == 0) { yield return null; yield break; }
            string board = new string(all.Select(b => b.GetComponentInChildren<TMPro.TMP_Text>().text.FirstOrDefault()).ToArray());
            var spelled = surface.GetComponentsInChildren<TMPro.TMP_Text>().FirstOrDefault(t => t.name == "Spelled letters");
            string sofar = spelled != null ? spelled.text : "";
            var names = seasonDirector.Snapshot.contestants.Select(c => CompetitionMiniGames.ScrambleForm(c.name));
            string answer = CompetitionMiniGames.BigBrotherWords.Concat(names)
                .FirstOrDefault(word => word.Length == board.Length && word.StartsWith(sofar, StringComparison.Ordinal)
                    && string.Concat(word.OrderBy(c => c)) == string.Concat(board.OrderBy(c => c)));
            if (answer == null) { yield return TryClickCompetitionControl("Skip word"); yield break; }
            // A right word's verdict shows it spelled whole on tiles that stay controls (a pad player
            // keeps a selection): nothing to choose until the next board is dealt. Reading the next
            // letter past the word's end threw, the first time a walk's season dealt words first.
            if (sofar.Length >= answer.Length) { yield return null; yield break; }
            char next = answer[sofar.Length];
            // A chosen tile stays a control, so a letter the word repeats ("MAYA") is the tile after
            // the ones already spelled with it, not the first carrying it again.
            int already = sofar.Count(letter => letter == next);
            var tile = tiles.Where(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == next.ToString()).Skip(already).FirstOrDefault();
            if (tile == null) { yield return null; yield break; }
            yield return TryClickCompetitionControl(tile.name);
        }

        private IEnumerator TryClickCompetitionControl(string name)
        {
            lastClickLanded=false;
            var button=VisibleSeasonButtons().FirstOrDefault(b=>b.name==name);
            if(button==null){yield return null;yield break;}
            button.onClick.Invoke();lastClickLanded=true;yield return null;yield return null;
        }
    }
}
