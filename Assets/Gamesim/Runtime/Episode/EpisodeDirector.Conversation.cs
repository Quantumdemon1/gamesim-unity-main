using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>Talking to a houseguest: the deal panel, house events, proximity, and the words a command is filed under.</summary>
    public sealed partial class EpisodeDirector
    {
        private HouseNpc NearestNpc()
        {
            if (!playerIsActive) return null;
            HouseNpc nearest = null; float nearestSquared = 2.8f * 2.8f;
            foreach (var npc in housemates)
            {
                if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                float squared = (player.transform.position - npc.transform.position).sqrMagnitude;
                if (squared <= nearestSquared && CanTalk(npc)) { nearest = npc; nearestSquared = squared; }
            }
            return nearest;
        }

        private bool CanTalk(HouseNpc npc)
        {
            var origin = player.transform.position + Vector3.up * 1.15f;
            var offset = npc.transform.position + Vector3.up * 1.15f - origin;
            if (offset.magnitude > 2.8f) return false;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, sightHits, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false; // Fail closed if a crowded ray overflows the reusable buffer.
            for (int i = 0; i < count; i++)
                if (!sightHits[i].transform.IsChildOf(player.transform) && !sightHits[i].transform.IsChildOf(npc.transform)) return false;
            return true;
        }

        public bool TryOpenNpc(string id)
        {
            if (!IsReady || blockedRecovery || challengeActive || projected.Find(projected.playerId).status != ContestantStatus.Active || projected.Find(id)?.status != ContestantStatus.Active) return false;
            var npc = housemates.FirstOrDefault(n => n.Id == id && n.gameObject.activeInHierarchy);
            if (npc == null || !CanTalk(npc)) return false;
            PauseNpcSocialForPanel();
            if (blockedRecovery) return false;
            ClosePanels(); focusedNpc = npc; player.SetInputEnabled(false); cameraRig.SetConversationFocus(player.transform, npc.transform);
            npc.GetComponent<CharacterPresentation>()?.SetTalking(true); Render(); return true;
        }

        /// <summary>
        /// A social action's category, drawn as a pill beside the control.
        ///
        /// <para>The grouping is real rather than invented: these commands already divide by what
        /// they commit. Talking and sharing information move a relationship and nothing else;
        /// promises and alliances write a binding record that comes due later; studying the house
        /// banks a competition bonus and touches no one. Unlike the confession "risk" badges, which
        /// have no counterpart in this simulation at all, this is a name for structure that is
        /// already there.</para>
        /// </summary>
        private static string Category(EpisodeCommandKind kind)
        {
            switch (kind)
            {
                case EpisodeCommandKind.Talk:
                case EpisodeCommandKind.ShareInformation:
                case EpisodeCommandKind.AskForIntel:
                case EpisodeCommandKind.VentAbout:
                case EpisodeCommandKind.SmallTalk:
                case EpisodeCommandKind.PersonalChat:
                case EpisodeCommandKind.RelationshipBuilding:
                    return "social";
                // Their own category for the same reason Eavesdrop and SpreadLie have one: these
                // are the conversations that can rebound, and the chip is the only warning before
                // an action is spent on one.
                case EpisodeCommandKind.DiscussGame:
                case EpisodeCommandKind.ShareSecret:
                case EpisodeCommandKind.SpreadRumor:
                case EpisodeCommandKind.HouseMeeting:
                    return "risky";
                case EpisodeCommandKind.StrategicDiscussion:
                case EpisodeCommandKind.BuyActionPoint:
                    return "strategic";
                // Their own category on purpose. These are the actions that can rebound on you, and
                // the chip is the only warning before you spend an action on one.
                case EpisodeCommandKind.Eavesdrop:
                case EpisodeCommandKind.SpreadLie:
                case EpisodeCommandKind.SchemeAgainst:
                    return "risky";
                case EpisodeCommandKind.SetBackdoorPlan:
                case EpisodeCommandKind.ProposeDeal:
                case EpisodeCommandKind.RespondToDeal:
                    return "strategic";
                case EpisodeCommandKind.PromiseSafety:
                case EpisodeCommandKind.PromiseVote:
                case EpisodeCommandKind.PromiseFinalTwo:
                case EpisodeCommandKind.FormAlliance:
                case EpisodeCommandKind.LeaveAlliance:
                case EpisodeCommandKind.SwearLoyalty:
                    return "strategic";
                case EpisodeCommandKind.StudyHouse:
                    return "preparation";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Watches for the player standing in a room with two other houseguests.
        ///
        /// <para>On a cadence rather than every frame: the check walks every character in the scene
        /// to find its room, and doing that sixty times a second to answer a question that can only
        /// come true once a week would be a lot of work for nothing.</para>
        ///
        /// <para>Only while the player is actually walking around. A proximity event is about being
        /// somewhere, so offering one while a panel is open would be the house reporting on a room
        /// the player is not currently in.</para>
        /// </summary>
        private void TickProximityWatch(float delta)
        {
            if (IsPanelOpen || challengeActive || projected == null) return;
            if (projected.phase != EpisodePhase.Social && projected.phase != EpisodePhase.Campaign) return;

            proximityWatch += delta;
            if (proximityWatch < ProximityWatchSeconds) return;
            proximityWatch = 0f;
            OfferProximityEvent(Snapshot);
        }

        /// <summary>
        /// Two houseguests the player has actually walked in on.
        ///
        /// <para>The one event source this port can serve better than the reference, which picks a
        /// location from a list of strings. Here the room is a room the player is standing in and
        /// the pair are whoever is standing in it with them.</para>
        ///
        /// <para>Offered rather than committed: it writes nothing until the player answers, and it
        /// waits for the ordinary one-situation-a-week rule like everything else.</para>
        /// </summary>
        private void OfferProximityEvent(EpisodeState state)
        {
            if (weeklyRecap == null || state == null) return;
            if (!HouseEvents.Ready(state)) return;
            if (HouseEvents.Pending(state) != null) return;

            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return;

            var others = here.Occupants
                .Where(person => !person.IsPlayer && !string.IsNullOrEmpty(person.Id))
                .OrderBy(person => person.Id, StringComparer.Ordinal)
                .ToList();
            if (others.Count < 2) return;

            var drawn = HouseEventSources.Proximity(state, others[0].Id, others[1].Id,
                here.Name, state.nextSequence);
            if (drawn == null) return;
            Submit(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = state.playerId,
                kind = EpisodeCommandKind.WitnessProximity, expectedPhase = state.phase,
                expectedRevision = state.revision, targetId = others[0].Id,
                secondTargetId = others[1].Id, text = here.Name,
            });
        }

        /// <summary>
        /// The deal table for one houseguest: what they have put to you, and what you can put to them.
        ///
        /// <para>The chance is drawn as a tag rather than folded into the caption, for the same
        /// reason the action category is: the words on a button are how tests and screen readers
        /// find it, and a number that moves every time the relationship does would make the control
        /// unfindable. Showing it at all is the reference's choice — it puts the odds on the screen
        /// rather than making the player guess.</para>
        /// </summary>
        private void DealPanel(EpisodeState state, ContestantState npc)
        {
            var waiting = NpcDeals.Pending(state).Where(d => d.proposerId == npc.id).ToList();
            foreach (var offer in waiting)
            {
                string id = offer.id;
                hud.Heading("AN OFFER FROM " + npc.name.ToUpperInvariant());
                hud.Paragraph(DealSentence(state, offer));
                hud.Tag(hud.ActionFor(id, EpisodeHud.DealAcceptCaption,
                        () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: EpisodeEngine.AcceptDeal)),
                    Category(EpisodeCommandKind.RespondToDeal));
                hud.ActionFor(id, EpisodeHud.DealDeclineCaption,
                    () => Commit(state, EpisodeCommandKind.RespondToDeal, id, text: "decline"));
            }

            var offers = PlayerDeals.Available(state, npc.id);
            if (offers.Count == 0) return;
            hud.Heading("WHAT YOU COULD PUT TO " + npc.name.ToUpperInvariant());
            foreach (string type in offers)
            {
                string kind = type;
                // A target agreement is about a third person, so it is offered per subject rather
                // than as one control that would have to ask "about whom?" after being clicked —
                // the same shape the vent and lie controls already use.
                if (kind == DealKind.TargetAgreement)
                {
                    foreach (var subject in state.Active.Where(c => !c.isPlayer && c.id != npc.id))
                    {
                        string about = subject.id;
                        if (!PlayerDeals.CanPropose(state, npc.id, kind, about, out _)) continue;
                        hud.Tag(hud.ActionFor(about, EpisodeHud.DealProposeCaption(
                                    DealKind.Title(kind).ToLowerInvariant() + " against " + subject.name),
                                () => Commit(state, EpisodeCommandKind.ProposeDeal, npc.id, about, text: kind)),
                            Category(EpisodeCommandKind.ProposeDeal) + " · " + Chance(state, npc.id, kind, about));
                    }
                    continue;
                }
                hud.Tag(hud.ActionFor(kind, EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant()),
                        () => Commit(state, EpisodeCommandKind.ProposeDeal, npc.id, text: kind)),
                    Category(EpisodeCommandKind.ProposeDeal) + " · " + Chance(state, npc.id, kind, null));
            }
        }

        /// <summary>
        /// "about even" rather than "51%", so the chip reads as a judgement and not a promise.
        ///
        /// <para>It rides beside the action category rather than replacing it — every other control
        /// in this panel says what kind of move it is, and a deal button should not be the one that
        /// stops. And it stays out of the caption, because the caption is how a test and a screen
        /// reader find the button, and a number that moves with the relationship would make it
        /// unfindable.</para>
        /// </summary>
        private static string Chance(EpisodeState state, string npcId, string type, string about)
        {
            double chance = PlayerDeals.AcceptanceChance(state, npcId, type, about);
            if (chance >= 75) return "likely";
            if (chance >= 55) return "favourable";
            if (chance >= 45) return "about even";
            if (chance >= 25) return "a stretch";
            return "unlikely";
        }

        /// <summary>What the houseguest is actually asking for, in words.</summary>
        private static string DealSentence(EpisodeState state, DealState offer)
        {
            string who = state.Find(offer.proposerId)?.name ?? "They";
            string about = offer.targetId == null ? null : state.Find(offer.targetId)?.name;
            switch (offer.type)
            {
                case DealKind.VetoUse:
                    return who + " is on the block and wants your word that you will use the veto on them.";
                case DealKind.VoteSave:
                    return who + " wants your vote to keep " + (about ?? "them") + " in the house this week.";
                case DealKind.VoteEvict:
                    return who + " wants your vote against " + (about ?? "the other nominee") + " this week.";
                case DealKind.VoteTogether:
                    return who + " wants the two of you to vote as a block this week.";
                case DealKind.TargetAgreement:
                    return who + " thinks " + (about ?? "somebody") + " is getting too strong and wants to work together on it.";
                case DealKind.SafetyAgreement:
                    return who + " is proposing that neither of you puts the other up.";
                case DealKind.InformationSharing:
                    return who + " wants to trade whatever the two of you hear around the house.";
                case DealKind.FinalTwo:
                    return who + " wants to sit beside you at the end.";
                case DealKind.AllianceInvite:
                    return who + " thinks it is time the two of you made it official.";
                default:
                    return who + " wants to partner up properly.";
            }
        }

        /// <summary>
        /// Whatever has happened to the house and is still waiting on an answer.
        ///
        /// <para>Each option says what it costs and how far it could rebound. The risk is drawn as a
        /// word beside the control rather than only as a colour, because a warning carried by colour
        /// alone is a warning some players never receive.</para>
        ///
        /// <para>There is no way to dismiss one. The reference does not offer one either, and an
        /// event you can wave away is a paragraph rather than a decision — but nothing forces an
        /// answer this turn, so it simply waits.</para>
        /// </summary>
        private void PendingHouseEvent(EpisodeState state)
        {
            var item = HouseEvents.Pending(state);
            if (item == null) return;

            hud.Heading(item.title.ToUpperInvariant());
            hud.Paragraph(item.narrative);
            foreach (var choice in item.choices)
            {
                string label = choice.label;
                var control = hud.ActionFor(item.id, EpisodeHud.EventChoiceCaption(label),
                    () => Commit(state, EpisodeCommandKind.ResolveHouseEvent, item.id, text: label));
                hud.Tag(control, EpisodeHud.RiskTag(choice.risk));
                if (!string.IsNullOrEmpty(choice.description)) hud.Paragraph(choice.description);
            }
        }

        /// <summary>
        /// The two things you can do to the whole house at once, and the two ways to buy more time.
        ///
        /// <para>These live in the phase panel rather than in a conversation because neither is
        /// addressed to a person. A house meeting is addressed to the room, and buying an action is
        /// addressed to nobody — it is a trade with the week itself.</para>
        ///
        /// <para>Each control says what it costs before it is pressed. A purchase that only revealed
        /// its price afterwards would be a trap rather than a decision.</para>
        /// </summary>
        private void HouseWideActions(EpisodeState state)
        {
            bool room = state.Active.Any(c => !c.isPlayer);
            if (!room) return;

            hud.Heading("THE WHOLE HOUSE");
            hud.Paragraph("House meetings affect everyone at once. Rallying is broadly positive; airing everything is volatile.");
            var meetingGrid = hud.ActionGrid(2);
            hud.Tag(hud.GridAction(meetingGrid,EpisodeHud.RallyHouseCaption,
                    () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.RallyTroops)),
                Category(EpisodeCommandKind.HouseMeeting));
            hud.Tag(hud.GridAction(meetingGrid,EpisodeHud.AirLaundryCaption,
                    () => Commit(state, EpisodeCommandKind.HouseMeeting, text: EpisodeEngine.AirDirtyLaundry)),
                Category(EpisodeCommandKind.HouseMeeting));

            int left = WebSocialVocabulary.PurchaseCeiling - state.boughtActionPoints;
            if (left <= 0)
            {
                hud.Paragraph("Extra-action purchases are exhausted for this season.");
                return;
            }

            hud.Heading("BUY MORE TIME");
            hud.Paragraph(left + (left == 1 ? " purchase remains. " : " purchases remain. ")
                + "Pay in goodwill: " + Mathf.Abs((int)WebSocialVocabulary.BurnOneCost)
                + " with one houseguest, or " + Mathf.Abs((int)WebSocialVocabulary.SpreadAllCost)
                + " with everyone.");
            var purchaseGrid = hud.ActionGrid(2);
            hud.Tag(hud.GridAction(purchaseGrid,EpisodeHud.BuyBurnOneCaption,
                    () => Commit(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.BurnOne)),
                Category(EpisodeCommandKind.BuyActionPoint));
            hud.Tag(hud.GridAction(purchaseGrid,EpisodeHud.BuySpreadCaption,
                    () => Commit(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll)),
                Category(EpisodeCommandKind.BuyActionPoint));
        }
    }
}
