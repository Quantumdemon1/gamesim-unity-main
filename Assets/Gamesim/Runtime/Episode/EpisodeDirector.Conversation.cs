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
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, sightHits, offset.magnitude, HouseLayers.Sight, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false; // Fail closed if a crowded ray overflows the reusable buffer.
            for (int i = 0; i < count; i++)
                if (!sightHits[i].transform.IsChildOf(player.transform) && !sightHits[i].transform.IsChildOf(npc.transform)) return false;
            return true;
        }

        /// <summary>
        /// Who the player clicked on and is now walking towards, if anyone.
        ///
        /// <para>The same device as <c>headingToStation</c>, for the same reason: two houseguests
        /// standing together are both inside the 2.8 m reach, so without a remembered target the
        /// walk would end in a conversation with whichever of them happened to be nearer rather
        /// than with the one that was pointed at.</para>
        /// </summary>
        private string headingToNpcId;
        private Vector3 headingToNpcAt;
        private float headingToNpcDeadline;

        /// <summary>How close the walk aims, inside the 2.8 m a conversation needs.</summary>
        private const float ApproachDistance = 1.6f;

        /// <summary>
        /// How far the target may move from where they were aimed at before the walk is re-aimed.
        ///
        /// <para>Has to be smaller than the slack the approach leaves, and is: aiming 1.6 m out of a
        /// 2.8 m reach leaves 1.2 m, of which the agent's own 0.15 m stopping distance takes a
        /// slice. At the 1.2 m this first shipped as, a target that drifted the full budget and then
        /// stopped left a final gap of about 2.95 m - past talking range, under the re-aim
        /// threshold, so the walk finished and nothing opened. The arrival branch below is the real
        /// guard; this only decides how often a moving target is re-aimed at.</para>
        /// </summary>
        private const float ApproachDrift = 0.6f;

        /// <summary>Long enough to cross the house at a run several times.</summary>
        private const float WalkTimeout = 40f;

        /// <summary>Forget any errand the player was sent on. Their own choices outrank it.</summary>
        private void CancelTravel()
        {
            headingToNpcId = null;
            headingToStation = false;
            arrivingIn = null;
            sceneBeatPending = null;
        }

        /// <summary>
        /// Somewhere to stand that can actually see them.
        ///
        /// <para>Not a straight line from the player: a straight line knows nothing about walls, and
        /// the shipped house has a 1.5 m divider between the living room and the kitchen that a
        /// dollhouse camera looks straight over. Clicking somebody on the far side of it put the
        /// approach point in the player's own room, where the walk finished and the sight line
        /// stayed blocked forever.</para>
        ///
        /// <para>So the candidates are fanned around the target, nearest the player first, and each
        /// is tested against the NavMesh rather than against geometry - <see cref="NavMesh.Raycast"/>
        /// from the candidate to the target fails exactly where a wall or a gap stands between them,
        /// because that is what a bake encodes. The first candidate that both samples onto the mesh
        /// and has a clear line to the target wins.</para>
        /// </summary>
        private bool TryApproach(HouseNpc npc, out Vector3 approach)
        {
            approach = default;
            var agent = player.Agent;
            if (agent == null) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

            var toPlayer = player.transform.position - npc.transform.position;
            toPlayer.y = 0f;
            var facing = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.forward;

            for (int step = 0; step < 8; step++)
            {
                // Out from the player's side first, then alternating around them, so the natural
                // approach is preferred and the far side of the room is the last resort.
                float degrees = (step + 1) / 2 * 45f * (step % 2 == 0 ? 1f : -1f);
                var candidate = npc.transform.position
                    + Quaternion.Euler(0f, degrees, 0f) * facing * ApproachDistance;
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.6f, filter)) continue;
                if (NavMesh.Raycast(hit.position, npc.transform.position, out _, filter)) continue;
                approach = hit.position;
                return true;
            }
            return false;
        }

        /// <summary>
        /// A click on a houseguest out in the house.
        ///
        /// <para>Near enough and it opens; too far and it runs you there and opens on arrival. That
        /// second half is the point - the only route into a conversation in this build was to walk
        /// yourself inside 2.8 m and press E, so the one gesture every player tries on a person did
        /// nothing but move the camera.</para>
        ///
        /// <para>No command is submitted by any of this. Opening a conversation is a view change;
        /// the season is not touched until a social action inside it is pressed, so a click costs
        /// the seeded run nothing.</para>
        /// </summary>
        private void SelectHouseguest(HouseNpc npc)
        {
            if (npc == null || !IsReady || blockedRecovery || challengeActive || IsPanelOpen
                || !playerIsActive) return;
            if (CanTalk(npc) && TryOpenNpc(npc.Id)) { CancelTravel(); player.StopHere(); return; }

            // The existing errand is not dropped until the new one is known to work: a click that
            // cannot be walked to should leave the player doing what they were already doing.
            if (!TryApproach(npc, out var approach) || !player.TryRunTo(approach))
            {
                message = "You cannot reach " + npc.DisplayName + " from here.";
                Render();
                return;
            }
            CancelTravel();
            headingToNpcId = npc.Id;
            headingToNpcAt = npc.transform.position;
            headingToNpcDeadline = Time.unscaledTime + WalkTimeout;
            // Watch them go, without reframing the shot they were already looking at.
            cameraRig?.FocusSubject(player.transform, false);
            message = "Heading over to " + npc.DisplayName;
            Render();
        }

        /// <summary>
        /// Carries the walk the player asked for, and ends it one way or the other.
        ///
        /// <para>Driven from the director's own tick rather than from a callback on the agent,
        /// because "arrived" is a property of the path and the pause state rather than an event: a
        /// walk interrupted by a panel, a ceremony or a second click must not open anything.</para>
        ///
        /// <para>Every exit says something. A version of this had four exits and three of them were
        /// silent, so a walk that could not finish left the player standing in the middle of a room
        /// under a "Heading over to Dana" banner that would never come true.</para>
        /// </summary>
        private void TickWalkToHouseguest()
        {
            if (string.IsNullOrEmpty(headingToNpcId)) return;
            if (IsPanelOpen || challengeActive || !playerIsActive) { CancelTravel(); return; }

            var npc = housemates.FirstOrDefault(actor => actor != null && actor.Id == headingToNpcId
                && actor.gameObject.activeInHierarchy);
            // Not found THIS frame is not the same as gone: a body is briefly inactive while it is
            // seated, re-posed or rebuilt. The deadline is what ends a walk that cannot land.
            if (npc == null)
            {
                if (Time.unscaledTime > headingToNpcDeadline) GiveUpOnWalk(null);
                return;
            }
            if (Time.unscaledTime > headingToNpcDeadline) { GiveUpOnWalk(npc); return; }

            // TryOpenNpc can still refuse - an eviction lands between the range check and the open -
            // so the intent is only let go once a conversation actually exists.
            if (CanTalk(npc) && TryOpenNpc(npc.Id)) { CancelTravel(); player.StopHere(); return; }

            // Re-aim when they have moved far enough from where they were aimed at to matter, or
            // when the walk has finished without getting close enough. Leaving the path alone
            // otherwise matters: re-issuing it resets the agent's progress, and a version of this
            // compared their position against the APPROACH POINT - 1.6 m from them by construction -
            // so a houseguest standing perfectly still read as constant drift, the path was reissued
            // every tick, and the player never arrived anywhere at all.
            bool drifted = (npc.transform.position - headingToNpcAt).sqrMagnitude > ApproachDrift * ApproachDrift;
            bool stopped = player.HasArrived;
            if (!drifted && !stopped) return;

            if (TryApproach(npc, out var approach) && player.TryRunTo(approach))
            {
                headingToNpcAt = npc.transform.position;
                return;
            }
            // Only a walk that has actually stopped has failed; one still in motion can try again.
            if (stopped) GiveUpOnWalk(npc);
        }

        private void GiveUpOnWalk(HouseNpc npc)
        {
            CancelTravel();
            player.StopHere();
            message = npc == null
                ? "You lost track of who you were going to see."
                : "You could not get to " + npc.DisplayName + ".";
            Render();
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
                case EpisodeCommandKind.AskVote:
                case EpisodeCommandKind.VentAbout:
                case EpisodeCommandKind.SmallTalk:
                case EpisodeCommandKind.PersonalChat:
                case EpisodeCommandKind.RelationshipBuilding:
                // The room acts (D-E): time spent, nothing that can rebound.
                case EpisodeCommandKind.PillowTalk:
                case EpisodeCommandKind.Cook:
                case EpisodeCommandKind.InviteUp:
                case EpisodeCommandKind.PublicDefense:
                case EpisodeCommandKind.AllianceMeet:
                case EpisodeCommandKind.PlayAGame:
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
                case EpisodeCommandKind.ReadPerson:
                case EpisodeCommandKind.CallTheVote:
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
                case EpisodeCommandKind.Lobby:
                    return "strategic";
                case EpisodeCommandKind.PromiseSafety:
                case EpisodeCommandKind.PromiseVote:
                case EpisodeCommandKind.PromiseFinalTwo:
                case EpisodeCommandKind.FormAlliance:
                case EpisodeCommandKind.LeaveAlliance:
                case EpisodeCommandKind.SwearLoyalty:
                    return "strategic";
                case EpisodeCommandKind.StudyHouse:
                case EpisodeCommandKind.CompPractice:
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
            // Past the story boundary a walk-in is a story beat and shares its airtime; before it,
            // the legacy one-situation-a-week slot. The engine answers both.
            // Past the story boundary a walk-in is only ever offered: the Pull's Step in commits it
            // (plan §5.1), and walking out of the room, or the pair leaving it, withdraws the offer.
            bool story = EpisodeEngine.StoryOn(state);
            if (story) ClearWalkIn();
            if (!EpisodeEngine.ProximityOpen(state)) return;

            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return;

            var others = here.Occupants
                .Where(person => !person.IsPlayer && !string.IsNullOrEmpty(person.Id))
                .Select(person => person.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (!TryWalkInPair(others, out string first, out string second)) return;

            // Offered only when something would come of it: a pair no walk-in story will take this
            // week, in this room, is two people talking, not an event.
            if (!EpisodeEngine.ProximityOpen(state, first, second, here.Name)) return;
            if (story)
            {
                walkInFirst = first; walkInSecond = second; walkInRoom = here.Name; walkInWeek = state.week;
                return;
            }
            if (HouseEventSources.Proximity(state, first, second, here.Name, state.nextSequence) == null) return;
            Submit(new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = state.playerId,
                kind = EpisodeCommandKind.WitnessProximity, expectedPhase = state.phase,
                expectedRevision = state.revision, targetId = first,
                secondTargetId = second, text = here.Name,
            });
        }

        /// <summary>
        /// How close two houseguests stand to be together, and how near one of them the player has
        /// to be to have walked in on them: the reference's three and five units, with a metre's
        /// grace for a body posed at furniture, whose root waits at the approach.
        /// </summary>
        public const float WalkInPairMetres = 3f, WalkInReachMetres = 6f;

        /// <summary>
        /// The two houseguests in the player's room who are actually together: a conversation of
        /// their own first, otherwise the pair standing within a few metres of each other nearest
        /// the player. It was the first two by id, wherever in the room they stood and whatever they
        /// were doing, and the card said they were mid-argument (playtest, 2026-09-27).
        /// </summary>
        private bool TryWalkInPair(List<string> ids, out string first, out string second)
        {
            first = second = null;
            if (ids == null || ids.Count < 2 || player == null) return false;
            if (projected != null && projected.npcSocial != null && npcMeetings != null)
                foreach (var pending in projected.npcSocial.pending)
                    if (ids.Contains(pending.firstId) && ids.Contains(pending.secondId)
                        && npcPendingWorld.TryGetValue(pending.sequence, out var lease) && npcMeetings.ValidateArrivedPair(lease, out _))
                    {
                        bool inOrder = string.CompareOrdinal(pending.firstId, pending.secondId) <= 0;
                        first = inOrder ? pending.firstId : pending.secondId;
                        second = inOrder ? pending.secondId : pending.firstId;
                        return true;
                    }
            var standing = player.transform.position;
            float nearest = float.MaxValue;
            for (int a = 0; a < ids.Count; a++)
            for (int b = a + 1; b < ids.Count; b++)
            {
                var one = BodyFor(ids[a]);
                var two = BodyFor(ids[b]);
                if (one == null || two == null || Across(one.position, two.position) > WalkInPairMetres) continue;
                float reach = Mathf.Min(Across(standing, one.position), Across(standing, two.position));
                if (reach > WalkInReachMetres || reach >= nearest) continue;
                nearest = reach; first = ids[a]; second = ids[b];
            }
            return first != null;
        }

        /// <summary>The distance between two points across the floor, ignoring height.</summary>
        private static float Across(Vector3 from, Vector3 to) { from.y = to.y = 0f; return Vector3.Distance(from, to); }

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
                            Stakes(kind) + " · " + Chance(state, npc.id, kind, about),
                            // A target agreement names a third houseguest, so its row is fronted by
                            // their portrait and already spends its right-hand end on a trust
                            // reading. The tag has to sit left of that; the plain deal rows below
                            // have no portrait and no reading, so they do not.
                            EpisodeHud.TagSeat.PastReading);
                    }
                    continue;
                }
                // Under the levers a vote deal names who it is about, one row per nominee, so it can
                // enter a voter's ballot as an obligation and be judged at the reveal.
                if (EpisodeEngine.LeverRulesOn(state) && (kind == DealKind.VoteSave || kind == DealKind.VoteEvict))
                {
                    foreach (string nomineeId in state.nominees)
                    {
                        string about = nomineeId;
                        if (!PlayerDeals.CanPropose(state, npc.id, kind, about, out _)) continue;
                        hud.Tag(hud.ActionFor(about, EpisodeHud.VoteDealCaption(kind, about == state.playerId ? "you" : state.Find(about).name),
                                () => Commit(state, EpisodeCommandKind.ProposeDeal, npc.id, about, text: kind)),
                            Stakes(kind) + " · " + Chance(state, npc.id, kind, about), EpisodeHud.TagSeat.PastReading);
                    }
                    continue;
                }
                hud.Tag(hud.ActionFor(kind, EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant()),
                        () => Commit(state, EpisodeCommandKind.ProposeDeal, npc.id, text: kind)),
                    Stakes(kind) + " · " + Chance(state, npc.id, kind, null));
            }
        }

        /// <summary>
        /// What breaking this kind of deal would cost, in words.
        ///
        /// <para>The number has been modelled since deals were written and has never reached a
        /// pixel: <c>DealKind.DefaultTrust</c> bands every type, <c>DealTrust.Weight</c> turns the
        /// band into a multiplier of 1, 1.5, 2 or 3, and <c>DealResolution.Impact</c> spends it - so
        /// walking away from a veto commitment moves -45 and walking away from an information swap
        /// moves -15. A player choosing between them was being asked to guess at a three-fold
        /// difference the simulation already knew.</para>
        ///
        /// <para>A tag beside the control rather than words inside it, for the same reason the
        /// chance is: the caption is how a test and a screen reader find a button.</para>
        /// </summary>
        private static string Stakes(string kind)
        {
            switch (DealKind.DefaultTrust(kind))
            {
                case DealTrust.Critical: return "highest stakes";
                case DealTrust.High: return "high stakes";
                case DealTrust.Low: return "low stakes";
                default: return "medium stakes";
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
            // Legacy situations only: a story beat has its own card (EpisodeDirector.Story.cs) and
            // is answered by option id, never by label.
            var item = state.houseEvents.FirstOrDefault(e => !e.resolved && !e.IsStory);
            if (item == null) return;

            hud.HouseEventHeader(item.title, item.narrative);
            hud.EventChoices(item.choices.Select(choice =>
            {
                string label = choice.label;
                return (EpisodeHud.EventChoiceCaption(label), choice.description, EpisodeHud.RiskTag(choice.risk),
                    (Action)(() => Commit(state, EpisodeCommandKind.ResolveHouseEvent, item.id, text: label)));
            }).ToList());
            // What the player has seen this week, after the choices it informs.
            hud.KnownHouseEventContext(state,item);
            FrameHouseEvent(item);
        }

        // The whole house's moves and the two ways of buying time are tiles on the free-time and
        // campaign screens now (EpisodeDirector.FreeTimeScreen.cs, HouseMoves): neither is addressed
        // to a person, so they live on the screen rather than in a conversation, and each says what
        // it costs before it is pressed.
    }
}
