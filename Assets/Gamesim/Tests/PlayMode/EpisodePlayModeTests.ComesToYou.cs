using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Houseguests bring their asks to the player (OFFERS-COME-TO-YOU-PLAN, slices 1 and 2): the asker walks over, says
    /// their line and the dialogue opens by itself on the exact offer; talking to somebody with an ask opens on it; one
    /// who cannot come puts it to the player as a card where they stand; nothing a visit, a hearing, a card, Think it
    /// over or a miss does commits anything. Every test opts in after the fixture's reload
    /// turns asks off, pins scaled time at 30 fps (unscaled time, which the asks run on, is not pinned) and bounds its
    /// waits in real seconds or on the ask clock, never in frames.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string ComesMaya = ContentCatalog.MayaId, ComesJamie = "jamie-roberts";

        /// <summary>
        /// The invitation flow's saved house - offers from Maya and Jamie, every action spent - with asks on. The switch
        /// takes effect first (its reset and the first session key both clear the set-aside), then the named askers'
        /// records are held aside, and the walk-in watch is held. Returns once a visit has been reserved, from somebody
        /// not held aside, with the house's pairing live again from there - for test 9, whose subject is that pairing, and
        /// for tests whose asker is in the busy set from the reservation until their dialogue opens. No overview.
        /// </summary>
        private IEnumerator InstallComesToYouHouse(params string[] holdAsideAskers) => InstallComesToYouHouse(false, holdAsideAskers);

        /// <summary>
        /// <see cref="InstallComesToYouHouse(string[])"/>, with the house's pairing still held once the visit is reserved
        /// when <paramref name="holdPairing"/> - for a test whose subject is not the house's autonomy; the caller lets it
        /// go (npcApproachDiagnosticsSuppressed, <see cref="WithTheHouseHeld"/>).
        /// </summary>
        /// <remarks>
        /// Pairing is held from the bodies' assembly until the visit is reserved, whatever the caller keeps afterwards. The
        /// house pairs any asker with no visit in flight - the busy set holds back only the visit's asker and the one
        /// opening (OFFERS-COME-TO-YOU-PLAN C.2) - in cast order, Maya first, on every third whole second of NPC time. That
        /// clock runs from the bodies' binding through their assembly, and a scan that comes due while pairing is held is
        /// spent; the ask's first scan comes only after AskSettleSeconds of free time from the switch, which is thrown once
        /// the bodies have assembled. With t seconds of NPC time at the switch, the house's next scan comes at 3 * ceil(t / 3)
        /// and the ask's at about t + 2. On the primitive rig t is a frame and the ask won. The UMA build's three reds at
        /// this wait (2026-10-30) fit the house scanning first, for t in [1, 3) or [4, 6): it took the one free asker,
        /// whose ask came as the plan's card after 10 s of Busy refusals (plan A), and a card sets no visit. That cause is
        /// inferred from the code and the timings - no log line showed the pair, the refusals or the card - so every install
        /// writes the switch's report, t and the next scan among it, to the test's output for a green run to confirm it.
        /// Once the visit is reserved its asker is in the busy set, so from there the house pairs everybody else as it
        /// always does.
        /// </remarks>
        private IEnumerator InstallComesToYouHouse(bool holdPairing, params string[] holdAsideAskers)
        {
            yield return OptIntoComesToYouHouse(false, false, holdAsideAskers, holdPairing: true);
            yield return WaitForTheFirstVisit(holdAsideAskers);
            if (!holdPairing) NpcWrite("npcApproachDiagnosticsSuppressed", false);
            Assert.That(holdAsideAskers, Has.No.Member(director.CurrentAskerId));
            foreach (var id in HeldAside(holdAsideAskers)) Assert.That(director.IsAskSetAside(id), Is.True, id + " is still held aside.");
        }

        /// <summary>What <see cref="TurnAsksOnInTheLoadedHouse"/> saw as the switch took effect, for a failure's message.</summary>
        private string comesSwitchReport = "";

        /// <summary>When the switch took effect, in real seconds and in the house's NPC seconds.</summary>
        private float comesSwitchedAt;
        private double comesSwitchedAtNpc;

        /// <summary>
        /// Waits up to 35 real seconds (plan G's fixture, step 5) for a visit to be reserved from an asker not held aside, and stops
        /// at once on a card: a card sets no visit, and it is the plan's way for a busy asker's ask (A), so the wait cannot
        /// end well after it. The house's pairing is held throughout (<see cref="InstallComesToYouHouse(bool, string[])"/>), so
        /// nothing but the ask's own scan clock is read each frame. A reservation writes the switch's report and when the
        /// visit came to the test's output (not the log), so a green run shows the NPC clock it met. A miss says which way it
        /// went: the switch's report (the bodies' assembly, the NPC clock and its next pairing scan), every ask scan by whom
        /// it charged - the asker (busy, no route, not bound), the player's spot, or nobody (the house, the player, or no
        /// ask could come) - with the first such refusal's report and whether each free ask could come then, and the house's
        /// pairs and each free asker's report now.
        /// </summary>
        private IEnumerator WaitForTheFirstVisit(string[] holdAsideAskers)
        {
            var free = ComesToYou.Read(director.Snapshot).Select(ask => ask.askerId).Where(id => !holdAsideAskers.Contains(id))
                .Distinct().ToArray();
            string refusal = null;
            int scans = 0, onAsker = 0, onSpot = 0, onNobody = 0;
            object scanAt = ReportRead("askScanAt");
            var charged = ChargesOf(free);
            float spot = ReportRead("askSpotBlockedFor") is float blocked ? blocked : 0f;
            float deadline = Time.realtimeSinceStartup + 35f;
            while (director.CurrentNpcInvitationRecordId == null && !director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                object scanNow = ReportRead("askScanAt");
                if (Equals(scanNow, scanAt) || director.CurrentNpcInvitationRecordId != null) { scanAt = scanNow; continue; }
                scanAt = scanNow;
                scans++;
                var chargedNow = ChargesOf(free);
                float spotNow = ReportRead("askSpotBlockedFor") is float blockedNow ? blockedNow : 0f;
                // A count started again on this scan's frame - the spot's with the stillness anchor, a record's with a new
                // phase - and charged on the scan reads lower than before, not higher: any change to a count above nothing
                // is this scan's charge.
                bool onTheAsker = chargedNow.Any(record => record.Value > 0f
                    && (!charged.TryGetValue(record.Key, out float was) || was != record.Value));
                bool onTheSpot = !onTheAsker && spotNow != spot && spotNow > 0f;
                string kind = onTheAsker ? "charged to the asker (busy, no route or not bound)" : onTheSpot ? "charged to the player's spot"
                    : "charged to nobody (the house, the player, or no ask could come)";
                if (onTheAsker) onAsker++; else if (onTheSpot) onSpot++; else onNobody++;
                charged = chargedNow; spot = spotNow;
                if (refusal == null && !director.IsAskCardOpen)
                    refusal = "the first scan refused " + SinceTheSwitch() + ", " + kind + " (" + AskMayComeNow(free) + "): "
                        + string.Join(" | ", free.Select(AskerReport));
            }
            if (director.CurrentNpcInvitationRecordId != null)
            {
                TestContext.WriteLine("Comes-to-you install: " + director.CurrentAskerId + "'s visit reserved " + SinceTheSwitch()
                    + ", after " + scans + " ask scans. " + comesSwitchReport);
                yield break;
            }
            Assert.Fail("An asker sets out for the player" + (director.IsAskCardOpen ? " (a card opened instead, " + SinceTheSwitch() + ")" : "")
                + ". " + comesSwitchReport + " Since: " + scans + " ask scans (" + onAsker + " charged to the asker, " + onSpot
                + " to the player's spot, " + onNobody + " to nobody); " + (refusal ?? "no scan refused") + ". Now " + SinceTheSwitch()
                + ", the house's pairs (pairing held through the wait): " + string.Join(", ", free.Select(id => id + " with " + (HousePartnerOf(id) ?? "nobody")))
                + "; " + string.Join(" | ", free.Select(AskerReport)));
        }

        /// <summary>The seconds of refusals charged to each of these askers' records so far (askUnreachable), copied, for a report.</summary>
        private Dictionary<string, float> ChargesOf(IEnumerable<string> askerIds)
        {
            var charges = new Dictionary<string, float>();
            var unreachable = ReportRead("askUnreachable") as Dictionary<string, float>;
            var state = director.Snapshot;
            if (unreachable == null || state == null) return charges;
            foreach (var ask in ComesToYou.Read(state).Where(ask => askerIds.Contains(ask.askerId)))
                charges[ask.recordId] = unreachable.TryGetValue(ask.recordId, out float seconds) ? seconds : 0f;
            return charges;
        }

        /// <summary>
        /// Whether each of these askers' asks could be walked over now - the director's own AskMayCome: current, not set
        /// aside, under its cap, past its retry and its asker's gap, the conversation able to show it - or "?"; never an
        /// assertion, so a scan that tried nobody reads apart from one the house refused.
        /// </summary>
        private string AskMayComeNow(IEnumerable<string> askerIds)
        {
            var state = director == null ? null : director.Snapshot;
            if (state == null) return "may come ?";
            var mayCome = typeof(EpisodeDirector).GetMethod("AskMayCome", BindingFlags.Instance | BindingFlags.NonPublic);
            return string.Join(", ", ComesToYou.Read(state).Where(ask => askerIds.Contains(ask.askerId)).Select(ask =>
            {
                string verdict;
                try { verdict = mayCome == null ? "?" : mayCome.Invoke(director, new object[] { ask }).ToString(); }
                catch (Exception) { verdict = "?"; }
                return ask.recordId + " may come " + verdict;
            }));
        }

        /// <summary>Whom the house has paired a houseguest with - an approach under way, a meeting or a saved pending pair - or null; never an assertion.</summary>
        private string HousePartnerOf(string id)
        {
            var leases = ((ReportRead("npcPendingWorld") as Dictionary<long, HouseMeetingLease>)?.Values ?? Enumerable.Empty<HouseMeetingLease>())
                .Concat(((ReportRead("npcApproaches") as IList)?.Cast<object>() ?? Enumerable.Empty<object>())
                    .Select(approach => ReportField(approach, "lease") as HouseMeetingLease));
            foreach (var lease in leases)
                if (lease != null && (lease.FirstId == id || lease.SecondId == id)) return lease.FirstId == id ? lease.SecondId : lease.FirstId;
            var row = director.Snapshot?.npcSocial?.pending?.FirstOrDefault(pending => pending.firstId == id || pending.secondId == id);
            return row == null ? null : row.firstId == id ? row.secondId : row.firstId;
        }

        /// <summary>The house's NPC clock and its next pairing scan as they stand, for a report.</summary>
        private string NpcClockNow()
        {
            var social = director.Snapshot?.npcSocial;
            double fraction = (ReportRead("npcFrameFraction") is double frame ? frame : 0d) + (ReportRead("npcWorldFraction") is double world ? world : 0d);
            return "NPC clock " + (ReportRead("npcFreeSeconds") is double seconds ? seconds.ToString("0.00") : "?") + " s, tick "
                + social?.clockTick + " + " + fraction.ToString("0.00") + ", next pairing scan at tick " + social?.nextScanTick
                + (social != null ? " (" + (social.nextScanTick - social.clockTick - fraction).ToString("0.00") + " NPC s away)" : "")
                + ", pairing held " + ReportRead("npcApproachDiagnosticsSuppressed");
        }

        /// <summary>How long since the switch took effect, in real seconds, NPC seconds and on the ask clock, for a report.</summary>
        private string SinceTheSwitch() => (Time.realtimeSinceStartup - comesSwitchedAt).ToString("0.00") + " real s after the switch ("
            + (ReportRead("npcFreeSeconds") is double seconds ? (seconds - comesSwitchedAtNpc).ToString("0.00") : "?") + " NPC s, ask clock "
            + ReportRead("askClock") + ")";

        /// <summary>The houseguests' bodies still assembling, for a report.</summary>
        private int BodiesAssembling() =>
            SceneComponents<CharacterPresentation>().Count(body => body.gameObject.activeInHierarchy && body.IsBodyAssembling);

        /// <summary>
        /// The house saved and reloaded, every body assembled, asks on and taken effect, the named askers held aside; no
        /// visit waited for.
        /// </summary>
        private IEnumerator OptIntoComesToYou(params string[] holdAsideAskers) => OptIntoComesToYouHouse(false, false, holdAsideAskers);

        /// <summary>
        /// <see cref="OptIntoComesToYou"/> on the house of its choice: with <paramref name="threeAsks"/>, Taylor asks too
        /// (OFFERS-COME-TO-YOU-PLAN test 15); with <paramref name="story"/>, the story system is on, so a walk-in can be
        /// offered as a Pull. With <paramref name="holdPairing"/>, the house's pairing stays held from the bodies' assembly
        /// on, for <see cref="WithTheHouseHeld"/>.
        /// </summary>
        private IEnumerator OptIntoComesToYouHouse(bool threeAsks, bool story, string[] holdAsideAskers, bool holdPairing = false)
        {
            SaveNpcInvitationFlowState(threeAsks);
            if (story) SaveWithTheStoryOn();
            yield return ReloadEpisode();
            yield return TurnAsksOnInTheLoadedHouse(holdAsideAskers, holdPairing);
        }

        /// <summary>
        /// Asks on in the house just loaded, its walk-in watch held: every body assembled with nobody paired meanwhile, the
        /// switch taken effect - its reset and the first session key both clear the set-aside - and only then the named
        /// askers' records held aside. No visit waited for. With <paramref name="holdPairing"/>, the house's pairing is not
        /// let go after the assembly, so no director Update runs with it live before <see cref="WithTheHouseHeld"/>'s body.
        /// </summary>
        private IEnumerator TurnAsksOnInTheLoadedHouse(string[] holdAsideAskers, bool holdPairing = false)
        {
            comesSwitchReport = "";
            NpcWrite("proximityWatch", -1e6f);
            // Asks are still off meanwhile, so at most the invitation sets out, and the switch below lets it go.
            float assembling = Time.realtimeSinceStartup;
            yield return WaitForBodiesPairingNobody(holdPairing);
            string bodies = (Time.realtimeSinceStartup - assembling).ToString("0.00") + " real s for the bodies (" + BodiesAssembling()
                + " still assembling), " + NpcClockNow();
            string invited = director.CurrentNpcInvitationNpcId;
            var invitedBody = invited == null ? null : SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == invited);
            director.AsksEnabled = true;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!NpcRead<bool>("asksWereOn") && Time.realtimeSinceStartup < deadline) yield return null;
            comesSwitchedAt = Time.realtimeSinceStartup;
            comesSwitchedAtNpc = NpcRead<double>("npcFreeSeconds");
            comesSwitchReport = "Assembled after " + bodies + "; the invitation had " + (invited == null ? "sent nobody"
                : "sent " + invited + (invitedBody != null && player != null ? ", " + Vector3.Distance(invitedBody.transform.position, player.transform.position).ToString("0.0")
                    + " m from the player" : "")) + "; at the switch " + NpcClockNow() + ", the ask's first scan after "
                + EpisodeDirector.AskSettleSeconds + " s of free time.";
            Assert.That(NpcRead<bool>("asksWereOn"), Is.True, "The switch takes effect.");
            Assert.That(director.AsksAreOn, Is.True);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit before the settle and still clocks have run.");
            var aside = NpcRead<HashSet<string>>("askSetAside");
            foreach (var asker in holdAsideAskers)
            {
                var ids = ComesToYou.Read(director.Snapshot).Where(ask => ask.askerId == asker).Select(ask => ask.recordId).ToList();
                Assert.That(ids, Is.Not.Empty, asker + " has an ask to hold aside.");
                foreach (var id in ids) aside.Add(id);
            }
        }

        /// <summary>
        /// The invitation flow's house with asks off, as InstallNpcInvitationFlowHouse leaves it, the walk-in watch held
        /// and every body assembled before the invitation waited for sets out: whatever set out while they assembled
        /// starts afresh, as on a load.
        /// </summary>
        private IEnumerator InstallLegacyInvitationHouse()
        {
            SaveNpcInvitationFlowState();
            yield return ReloadEpisode();
            NpcWrite("proximityWatch", -1e6f);
            yield return WaitForBodiesPairingNobody();
            Assert.That(director.AsksAreOn, Is.False);
            NpcInvoke("ResetNpcInvitationsForLoad");
            Assert.That(director.ShowOverview(), Is.True);
            yield return WaitForVisibleNpcInvitation();
        }

        /// <summary>
        /// Waits until every body has assembled - one finishing later re-renders the HUD and re-selects a control - with
        /// the house's route planning held meanwhile, so the wait pairs nobody: the askers are as free when it ends as just
        /// after the reload, and planning runs as it always does from there - unless <paramref name="keepHeld"/>, when the
        /// caller lets it go (<see cref="WithTheHouseHeld"/>).
        /// </summary>
        private IEnumerator WaitForBodiesPairingNobody(bool keepHeld = false)
        {
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            try { yield return WaitForBodies(); }
            finally { if (!keepHeld) NpcWrite("npcApproachDiagnosticsSuppressed", false); }
            Assert.That(NpcRead<IList>("npcApproaches"), Is.Empty, "Nobody set out to pair while the bodies assembled.");
        }

        /// <summary>
        /// <paramref name="body"/> with the house's own pairing and ambient seats held, for a test whose subject is the ask
        /// queue rather than the house's autonomy (OFFERS-COME-TO-YOU-PLAN test 17 holds them the same way). The house pairs
        /// any asker with no visit in flight - the busy set holds back only the visit's asker and the one opening (plan C.2)
        /// - in cast order, Maya first, every third second of NPC time from a fresh house; a paired asker is refused as busy
        /// and her ask comes as a card, and a seated one is taken off the furniture (a conversation preempts a disposable
        /// routine). <paramref name="optIn"/> keeps pairing held from the bodies' assembly on (holdPairing), so no director
        /// Update runs with it live; both holds are let go afterwards.
        /// </summary>
        private IEnumerator WithTheHouseHeld(Func<IEnumerator> optIn, Func<IEnumerator> body)
        {
            float ambient = float.NaN;
            try
            {
                yield return optIn();
                Assert.That(NpcRead<bool>("npcApproachDiagnosticsSuppressed"), Is.True, "Pairing is held from the bodies' assembly on.");
                ambient = NpcRead<float>("nextAmbientActivity");
                NpcWrite("nextAmbientActivity", float.PositiveInfinity);
                Assert.That(NpcRead<IList>("npcApproaches"), Is.Empty, "Nobody set out to pair before the hold.");
                Assert.That(director.IsAskCardOpen, Is.False, "No card before the hold.");
                yield return body();
            }
            finally
            {
                if (director != null)
                {
                    NpcWrite("npcApproachDiagnosticsSuppressed", false);
                    if (!float.IsNaN(ambient)) NpcWrite("nextAmbientActivity", ambient);
                }
            }
        }

        private IEnumerable<string> HeldAside(string[] askers) =>
            NpcDeals.Pending(director.Snapshot).Where(row => askers.Contains(row.proposerId)).Select(row => row.id);

        /// <summary>Waits for the conversation to open on the asker, bounded in real seconds; prints why a walk missed.</summary>
        private IEnumerator WaitForAskDialogue(string askerId, float seconds = 35f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (director.TalkingToId == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (director.TalkingToId == null)
            {
                var motion = SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == askerId)?.GetComponent<HouseNpcMotion>();
                Assert.Fail("The dialogue never opened on " + askerId + ": misses " + director.AskMissCount(director.CurrentNpcInvitationRecordId)
                    + ", arrival " + motion?.ArrivalFailure + ", route " + motion?.LastRouteFailure + ". " + AskerReport(askerId));
            }
            Assert.That(director.TalkingToId, Is.EqualTo(askerId));
        }

        private EpisodeHud ComesHud => director.GetComponentInChildren<EpisodeHud>();

        private TMP_Text LastLabel(string name) =>
            director.GetComponentsInChildren<TMP_Text>(true).LastOrDefault(label => label.name == name && label.gameObject.activeInHierarchy);

        private static IEnumerator PinnedClock(Func<IEnumerator> body, float frame = 1f / 30f)
        {
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            Time.captureDeltaTime = frame;
            try { yield return body(); }
            finally { Time.captureDeltaTime = 0f; Application.runInBackground = background; }
        }

        // ------------------------------------------------------------------ 1. the walk-over opens on the offer

        [UnityTest]
        public IEnumerator ComesToYou_ArrivalOpensTheDialogueOnTheExactOffer() => PinnedClock(ArrivalOpensTheDialogue);

        private IEnumerator ArrivalOpensTheDialogue()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            Assert.That(askerId, Is.EqualTo(ComesMaya));
            var before = director.Snapshot;
            int spent = EpisodeEngine.SocialActionsSpent(before);
            yield return WaitForAskDialogue(askerId);
            // The open frame.
            Assert.That(ComesHud.PointerHeld, Is.True, "Pointer presses are held off the panel as it opens.");
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(recordId));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId));
            Assert.That(director.CurrentAskerId, Is.EqualTo(askerId));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.DealsCategoryCaption));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(director.IsAskSetAside(recordId), Is.True);
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption), "The keyboard opens on the way out that answers nothing.");
            yield return WaitForBodies();

            var line = NpcInvitation.Available(director.Snapshot).Single(row => row.RecordId == recordId).OpeningLine;
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + line + "\""));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskOfferHeading));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskWalkOverEyebrow("Maya")));
            Assert.That(LastLabel(EpisodeHud.AskTermsName)?.text, Is.Not.Null.And.Not.Empty);
            Assert.That(LastLabel(EpisodeHud.AskLapseName)?.text, Is.EqualTo(ComesToYou.LapseNextCampaign));
            foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.ThinkItOverCaption,
                         EpisodeDirector.ConversationTopicsBackCaption })
                Assert.That(FindButton(caption), Is.Not.Null, caption); // Single: exactly one of each.

            yield return WaitOutThePointerHold();
            Assert.That(ComesHud.PointerHeld, Is.False);
            var accept = ButtonWithCaption(EpisodeHud.DealAcceptCaption);
            Assert.That(ComesHud.TryDescribeOfferControl(accept, out var described), Is.True);
            Assert.That(described.OfferId, Is.EqualTo(recordId));
            Assert.That(ComesHud.TryReadOfferControl(accept, out var reading), Is.True);
            Assert.That(reading, Does.Contain(director.Snapshot.Find(askerId).name).And.Contain(LastLabel(EpisodeHud.AskTermsName).text));
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == recordId), Is.True, "Hearing the offer does not answer it.");
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(spent));

            var justBefore = director.Snapshot;
            accept.onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.revision, Is.EqualTo(justBefore.revision + 1));
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1));
            Assert.That(director.IsConversationExchangePlaying, Is.True);
            var expected = new EpisodeEngine(justBefore).Apply(new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.RespondToDeal, targetId = recordId, text = EpisodeEngine.AcceptDeal,
            });
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);
        }

        // ------------------------------------------------------------------ 2. Think it over

        [UnityTest]
        public IEnumerator ComesToYou_ThinkItOverCommitsNothingAndIsNotBroughtAgainThisPhase() => PinnedClock(ThinkItOverHeld);

        /// <summary>
        /// On a house whose pairing and ambient seats are held: the subject is the set-aside, and from Think it over on Maya
        /// has no visit in flight, so a house free to pair would take her (plan C.2's busy set) before the control below
        /// brings her ask again, and her ask would come as a card. Held, nothing but the set-aside keeps her from coming.
        /// </summary>
        private IEnumerator ThinkItOverHeld() => WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), ThinkItOverCommitsNothing);

        private IEnumerator ThinkItOverCommitsNothing()
        {
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            yield return WaitForAskDialogue(askerId);
            Assert.That(ComesHud.PointerHeld, Is.True, "The open frame: pointer presses are held off the panel.");
            yield return WaitOutThePointerHold();
            var think = ButtonWithCaption(EpisodeHud.ThinkItOverCaption);
            var before = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            think.onClick.Invoke();
            // The press frame.
            Assert.That(director.IsPanelOpen, Is.False);
            AssertEquivalent(before, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
            Assert.That(director.IsAskSetAside(recordId), Is.True);
            Assert.That(WaitingOnYou.Read(director.Snapshot).items.Any(item => item.id == recordId), Is.True, "The offer still waits.");

            // Neither gap hides the set-aside: the next scan may run at once, and Maya may come again.
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            NpcRead<Dictionary<string, float>>("askerNextAt").Remove(askerId);
            float start = NpcRead<float>("askClock");
            float need = EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds + 3 * EpisodeDirector.AskScanSeconds;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (NpcRead<float>("askClock") < start + need && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.EqualTo(recordId), "Not brought again this phase.");
                yield return null;
            }
            Assert.That(NpcRead<float>("askClock"), Is.GreaterThanOrEqualTo(start + need), "The ask clock ran: the wait was not vacuous.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.EqualTo(recordId));

            // Talking to Maya later this phase opens on the topics wheel, the offer waiting under Deals.
            deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            yield return OpenNearbyNpc(maya);
            Assert.That(director.TalkingToId, Is.EqualTo(askerId));
            Assert.That(director.CurrentConversationCategory, Is.Null);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            Assert.That(TagOn(FindButton(EpisodeDirector.DealsCategoryCaption)), Does.Contain("waiting"));
            director.ClosePanels();
            yield return null;

            // The control: let the record come again, and the same record leases within the same bound.
            NpcRead<HashSet<string>>("askSetAside").Remove(recordId);
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            deadline = Time.realtimeSinceStartup + 15f;
            while (director.CurrentNpcInvitationRecordId != recordId && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId), "Without the set-aside the same ask comes again.");
        }

        // ------------------------------------------------------------------ 3. walking away is a retry

        [UnityTest]
        public IEnumerator ComesToYou_WalkingAwayIsARetryNotADeferral() => PinnedClock(WalkingAwayHeld);

        /// <summary>
        /// On a house whose pairing and ambient seats are held: the subject is the miss and its retry, and between the two
        /// Maya has no visit in flight, so a house free to pair would take her (plan C.2's busy set) and her ask would come
        /// as a card, not again in person.
        /// </summary>
        private IEnumerator WalkingAwayHeld() => WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), WalkingAwayIsARetry);

        /// <summary>A point about 3 m from the player, on the floor, that keeps the player's head well inside the frame.</summary>
        private Vector3 AwayInFrame()
        {
            var view = cameraRig.ViewCamera;
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * Mathf.PI / 8f;
                var candidate = player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
                if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask)) continue;
                var seen = view.WorldToViewportPoint(hit.position + Vector3.up * 1.7f);
                if (seen.z > 0f && seen.x > .15f && seen.x < .85f && seen.y > .15f && seen.y < .85f) return hit.position;
            }
            Assert.Fail("No floor 3 m from the player stays in frame.");
            return player.transform.position;
        }

        private IEnumerator WalkingAwayIsARetry()
        {
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            Assert.That(director.NpcInvitationIsVisible, Is.False, "Warped before the asker arrives.");
            WarpPlayer(AwayInFrame());
            float deadline = Time.realtimeSinceStartup + 5f;
            while (director.CurrentNpcInvitationRecordId != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "The visit is let go.");
            Assert.That(director.AskMissCount(recordId), Is.EqualTo(1), "Walking away is a miss.");
            Assert.That(director.IsAskSetAside(recordId), Is.False, "and a retry, not a deferral.");

            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskRetrySeconds + 10f;
            while (director.CurrentNpcInvitationRecordId != recordId && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId), "The same ask comes again after its retry.");
            Assert.That(director.NpcInvitationIsVisible, Is.False);
            WarpPlayer(AwayInFrame());
            deadline = Time.realtimeSinceStartup + 5f;
            while (director.CurrentNpcInvitationRecordId != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.AskMissCount(recordId), Is.EqualTo(2));
            Assert.That(director.IsAskSetAside(recordId), Is.False);

            // Two misses: no further walk-over this phase. From slice 2 the ask comes as a card where the player stands,
            // once its retry has passed.
            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskRetrySeconds + 20f;
            while (!director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Not.EqualTo(recordId), "Two misses: no further walk-over this phase.");
                yield return null;
            }
            Assert.That(director.IsAskCardOpen, Is.True, "After two misses the ask comes as a card.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Card));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId));
            Assert.That(director.CurrentAskerId, Is.EqualTo(askerId));
        }

        // ------------------------------------------------------------------ 4. talking to the asker

        [UnityTest]
        public IEnumerator ComesToYou_TalkingToTheAskerOpensOnTheOffer() => PinnedClock(TalkingToTheAskerHeld);

        /// <summary>
        /// On a house whose pairing and ambient seats are held: the subject is the raised open, and once its conversation
        /// closes Maya has no visit in flight, so a house free to pair would take her (plan C.2's busy set) before the second
        /// conversation the test opens with her.
        /// </summary>
        private IEnumerator TalkingToTheAskerHeld() => WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), TalkingToTheAsker);

        /// <summary>Waits for the asker's line to be up and no whole second of the house's time due, holding the auto-open back meanwhile.</summary>
        private IEnumerator WaitForTheLineWithNoSecondDue()
        {
            float deadline = Time.realtimeSinceStartup + 35f;
            while ((!director.NpcInvitationIsVisible || NpcWholeSecondDue()) && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("askSpokeFor", 0f);
                yield return null;
            }
            Assert.That(director.NpcInvitationIsVisible, Is.True);
            Assert.That(NpcWholeSecondDue(), Is.False);
            Assert.That(director.TalkingToId, Is.Null);
        }

        private IEnumerator TalkingToTheAsker()
        {
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            yield return WaitForTheLineWithNoSecondDue();
            var waiting = director.Snapshot;
            var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            var mine = player.GetComponent<CharacterPresentation>();
            Assert.That(mine.FacingYaw, Is.NaN, "The player is not turned before the conversation opens.");
            Assert.That(director.TryOpenNpc(askerId), Is.True);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Raised));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(recordId));
            Assert.That(ComesHud.PointerHeld, Is.True);
            // Opening on the offer writes the house's installed seconds and nothing of its own (LIVE-SAVE-PLAN): memory
            // is as it was, and the disk now holds exactly that state.
            AssertEquivalent(waiting, director.Snapshot);
            AssertDialogueUiDisk(waiting);
            // Raised: the player turns to the asker, who is not turned - the heading they arrived with went with the
            // open's ClosePanels.
            float playerYaw = NpcRead<float>("askPlayerYaw");
            Assert.That(OffBearing(mine.FacingYaw, player.transform.position, asker.transform.position), Is.LessThan(20f),
                "A raised open turns the player to the asker: heading " + mine.FacingYaw + ".");
            Assert.That(playerYaw, Is.EqualTo(mine.FacingYaw), "The heading is the ask's.");
            Assert.That(NpcRead<float>("askAskerYaw"), Is.NaN, "A raised open does not turn the asker.");
            yield return null; yield return null;
            // The visit still in flight went on the next frame's cancel under the panel; the open's heading stays.
            Assert.That(NpcRead<HouseNpcInvitationLease>("askVisit"), Is.Null, "The visit went under the panel.");
            Assert.That(NpcRead<float>("askPlayerYaw"), Is.EqualTo(playerYaw), "The visit's going leaves the dialogue's heading the ask's,");
            Assert.That(mine.FacingYaw, Is.EqualTo(playerYaw), "and the player still faces the asker.");
            Assert.That(SelectedName(), Is.EqualTo(EpisodeDirector.ConversationTopicsBackCaption));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskRaisedEyebrow("Maya")));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "A raised dialogue has no Think it over.");

            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick.Invoke();
            Assert.That(director.CurrentConversationCategory, Is.Null, "Back to topics: the wheel.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            director.ClosePanels();
            Assert.That(mine.FacingYaw, Is.NaN, "Closing lets go of the heading the raised open gave the player.");
            Assert.That(NpcRead<float>("askPlayerYaw"), Is.NaN);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            yield return OpenNearbyNpc(asker);
            Assert.That(director.CurrentConversationCategory, Is.Null, "Raised once a phase: the wheel the second time.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
        }

        /// <summary>4d: a click on the asker whose line is up hears the ask, the way E does; with asks off, the click's usual route.</summary>
        [UnityTest]
        public IEnumerator ComesToYou_AClickOnTheAskerHearsTheAsk() => PinnedClock(AClickOnTheAsker);

        private IEnumerator AClickOnTheAsker()
        {
            var select = typeof(EpisodeDirector).GetMethod("SelectHouseguest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(select, Is.Not.Null);
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            yield return WaitForTheLineWithNoSecondDue();
            var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            var waiting = director.Snapshot;
            select.Invoke(director, new object[] { asker });
            Assert.That(director.TalkingToId, Is.EqualTo(askerId));
            Assert.That(ComesHud.PointerHeld, Is.True, "The open frame: the click that opened it presses nothing on the panel.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver), "The click hears the ask it walked over with.");
            Assert.That(NpcRead<Dictionary<string, int>>("askPresentations")[recordId], Is.EqualTo(1), "One presentation counted.");
            // The click writes the house's installed seconds and nothing of its own: memory as it was, and that on disk.
            AssertEquivalent(waiting, director.Snapshot);
            AssertDialogueUiDisk(waiting);
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskWalkOverEyebrow("Maya")));
            Assert.That(FindButton(EpisodeHud.ThinkItOverCaption), Is.Not.Null);

            // With asks off the same click takes its usual route: never the ask's dialogue.
            yield return InstallLegacyInvitationHouse();
            Assert.That(director.AsksAreOn, Is.False);
            var visitor = SceneComponents<HouseNpc>().Single(npc => npc.Id == director.CurrentNpcInvitationNpcId);
            select.Invoke(director, new object[] { visitor });
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            if (director.TalkingToId != null) Assert.That(director.InvitedDealRecordId, Is.Null, "Today's click opens no invited offer.");
        }

        /// <summary>4c: from a houseguest's free-time screen, asking or pitching opens on that category; the ask is not raised or set aside.</summary>
        [UnityTest]
        public IEnumerator ComesToYou_ComingToAskOrPitchOpensOnThatCategory() => PinnedClock(ComingToAskOrPitchHeld);

        /// <summary>
        /// On a house whose pairing and ambient seats are held: the subject is the category each conversation opens on, and
        /// once the first conversation has taken her visit Maya has none in flight, so a house free to pair could take her
        /// (plan C.2's busy set) - a panel's open and close commit the house's whole seconds - before the second.
        /// </summary>
        private IEnumerator ComingToAskOrPitchHeld() => WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), ComingToAskOrPitch);

        private IEnumerator ComingToAskOrPitch()
        {
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            foreach (var (intent, category) in new[] { (EpisodeDirector.IntentDeal, EpisodeDirector.DealsCategoryCaption),
                         (EpisodeDirector.IntentAsk, EpisodeDirector.LearnCategoryCaption) })
            {
                director.TalkWithIntent(askerId, intent);
                float deadline = Time.realtimeSinceStartup + 20f;
                while (director.TalkingToId != askerId && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.TalkingToId, Is.EqualTo(askerId), intent);
                Assert.That(director.CurrentConversationCategory, Is.EqualTo(category), intent);
                Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), intent + ": no ask dialogue.");
                Assert.That(director.IsAskSetAside(recordId), Is.False, intent + ": the ask is not set aside.");
                if (intent == EpisodeDirector.IntentDeal)
                    Assert.That(ShownText(), Does.Contain("AN OFFER FROM " + director.Snapshot.Find(askerId).name.ToUpperInvariant()),
                        "The Deals page shows her offer first.");
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>4b: where the conversation only greets, it draws the notice and raises nothing.</summary>
        [UnityTest]
        public IEnumerator ComesToYou_NoRaiseWhereTheConversationOnlyGreets() => PinnedClock(NoRaiseWhereItOnlyGreets);

        private IEnumerator NoRaiseWhereItOnlyGreets()
        {
            var state = FullHouse(4402, 8);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.Nomination;
            state.hohId = npcs[0];
            state.nominees = new List<string>();
            state.dealRulesStartWeek = 1;
            state.deals.Add(new DealState
            {
                id = NpcDeals.OfferPrefix + state.nextSequence++, type = DealKind.Partnership, proposerId = npcs[1],
                recipientId = state.playerId, status = DealStatus.Proposed, week = state.week, expiresWeek = state.week,
                trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
            });
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            NpcWrite("proximityWatch", -1e6f);
            yield return WaitForBodiesPairingNobody();
            director.AsksEnabled = true;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!NpcRead<bool>("asksWereOn") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(NpcRead<bool>("asksWereOn"), Is.True, "The switch takes effect.");
            var ask = ComesToYou.Read(director.Snapshot).Single(row => row.askerId == npcs[1]);
            deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            var before = director.Snapshot;
            yield return OpenNearbyNpc(SceneComponents<HouseNpc>().Single(npc => npc.Id == npcs[1]));
            Assert.That(director.TalkingToId, Is.EqualTo(npcs[1]));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "The notice, not the ask.");
            Assert.That(director.IsAskSetAside(ask.recordId), Is.False);
            Canvas.ForceUpdateCanvases();
            Assert.That(ActiveRect(EpisodeHud.ConversationNoticeName), Is.Not.Null, "The conversation draws the notice card.");
            Assert.That(LastLabel("NPC spoken dialogue")?.text, Does.Contain(HouseDialogue.Greeting(before, npcs[1])),
                "and its line is the greeting.");
            Assert.That(LastLabel(EpisodeHud.AskLineName), Is.Null, "No ask's line on it.");
        }

        // ------------------------------------------------------------------ 5. off in tests and verification

        [UnityTest]
        public IEnumerator ComesToYou_OffInTestsAndVerificationByDefault() => PinnedClock(OffByDefault);

        private IEnumerator OffByDefault()
        {
            Assert.That(director.AsksEnabled, Is.False, "The fixture turns asks off as it binds the director.");
            yield return InstallLegacyInvitationHouse();
            Assert.That(director.AsksEnabled, Is.False, "and again as it binds the reloaded one.");
            Assert.That(director.AsksAreOn, Is.False);
            Assert.That(NpcRead<HouseNpcInvitationBubble>("npcInvitationBubble").ViewCamera, Is.SameAs(cameraRig.ViewCamera),
                "The invitation's line is placed on the view the player sees, with asks off too.");
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.That(director.TalkingToId, Is.Null, "Nothing opens by itself with asks off.");
                yield return null;
            }
            string offerId = director.CurrentNpcInvitationRecordId;
            director.Interact();
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(offerId));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None));
            yield return null; yield return null;
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.DealsCategoryCaption.ToUpperInvariant()), "The legacy invited view: its category chrome.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null);

            director.AsksEnabled = null;
            Assert.That(director.AsksAreOn, Is.EqualTo(!Application.isBatchMode), "The default: on in the editor and the player, off in a batch run.");
            director.AsksEnabled = false;
        }

        // ------------------------------------------------------------------ 6. held in the overview and under the emote menu

        [UnityTest]
        public IEnumerator ComesToYou_HeldInTheOverviewAndUnderTheEmoteMenu() => PinnedClock(HeldUnderChrome);

        private IEnumerator HeldUnderChrome()
        {
            // Each on a house of its own whose pairing and ambient seats are held: the subject is the chrome's hold, and a
            // lease that runs out under it is a miss, after which Maya has no visit in flight until her retry - when a house
            // free to pair would take her (plan C.2's busy set) and her ask would come as a card, not as the walk-over.
            yield return WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), HeldUnderTheOverview);

            // The emote menu on a house of its own, as the overview had: a walk-over just reserved, which a card never is (a
            // card sets no CurrentNpcInvitationRecordId). An asker held aside while the house ran on, beside the last asker,
            // may by then come as a card (OFFERS-COME-TO-YOU-PLAN A, slice 2: busy, or a spot that fits no visitor), and a
            // card is a panel the emote menu never opens over.
            yield return WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), HeldUnderTheEmoteMenu);

            // And no visit starts under it (OFFERS-COME-TO-YOU-PLAN A: with the emote menu open, new visits wait): the menu
            // opened as asks take effect, before the settle has run, on a house whose pairing is held, so Maya stays free to
            // come once it closes.
            yield return WithTheHouseHeld(() => OptIntoComesToYouHouse(false, false, new[] { ComesJamie }, holdPairing: true), NoVisitStartsUnderTheMenu);
        }

        /// <summary>The overview up over a walk-over just reserved: the line shows, nothing opens, and it opens once the overview goes.</summary>
        private IEnumerator HeldUnderTheOverview()
        {
            string askerId = director.CurrentAskerId;
            Assert.That(director.ShowOverview(), Is.True);
            yield return HeldThenOpens(askerId, "the overview", () => director.EndOverview());
            director.ClosePanels();
            yield return null;
        }

        /// <summary>The emote menu up over a walk-over just reserved: the line shows, nothing opens, and it opens once the menu closes.</summary>
        private IEnumerator HeldUnderTheEmoteMenu()
        {
            string askerId = director.CurrentAskerId;
            Assert.That(director.IsAskCardOpen, Is.False, "A walk-over, not a card: " + AskerReport(askerId));
            director.ToggleEmoteMenu();
            Assert.That(director.EmoteMenuOpen, Is.True, "The emote menu opens over the visit: " + AskerReport(askerId));
            yield return HeldThenOpens(askerId, "the emote menu", () => director.ToggleEmoteMenu());
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The emote menu up from before the settle: for 5 real seconds no visit is reserved, no card opens and the settle
        /// clock stays at zero; once it closes, Maya's visit is reserved within 10 real seconds.
        /// </summary>
        private IEnumerator NoVisitStartsUnderTheMenu()
        {
            director.ToggleEmoteMenu();
            if (!director.EmoteMenuOpen) Assert.Fail("The emote menu opens before any visit: " + AskerReport(ComesMaya));
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (!director.EmoteMenuOpen) Assert.Fail("The emote menu stays up: " + AskerReport(ComesMaya));
                if (director.CurrentNpcInvitationRecordId != null || director.IsAskCardOpen || NpcRead<float>("askFreeRun") > 0f)
                    Assert.Fail("New visits wait under the emote menu, and the settle with them: " + AskerReport(ComesMaya));
            }
            director.ToggleEmoteMenu();
            Assert.That(director.EmoteMenuOpen, Is.False, "The emote menu closes.");
            float deadline = Time.realtimeSinceStartup + 10f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (director.CurrentNpcInvitationRecordId == null)
                Assert.Fail("Once the emote menu closes, a visit is reserved within 10 s: " + AskerReport(ComesMaya));
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya), "The one asker not held aside comes. " + AskerReport(ComesMaya));
        }

        /// <summary>The asker's line shows and nothing opens for 5 real seconds from the frame it is first up; once the chrome goes, the dialogue opens.</summary>
        private IEnumerator HeldThenOpens(string askerId, string chrome, Action close)
        {
            float deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline) yield return null;
            if (!director.NpcInvitationIsVisible) Assert.Fail("The line shows under " + chrome + ": " + AskerReport(askerId));
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until)
            {
                if (director.TalkingToId != null) Assert.Fail("Nothing opens under " + chrome + ": " + AskerReport(askerId));
                yield return null;
            }
            close();
            // The lease may run out while held: under the overview or the emote menu (closable chrome) that is a miss, and
            // the ask comes again after its retry, inside the wait.
            yield return WaitForAskDialogue(askerId, 40f);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver), "Opens as a walk-over: " + AskerReport(askerId));
        }

        // ------------------------------------------------------------------ 7. a seated asker does not block the next

        [UnityTest]
        public IEnumerator ComesToYou_SeatedAskerDoesNotBlockTheNext() => PinnedClock(SeatedAsker);

        /// <summary>
        /// The first-listed asker seated through the coordinator's furniture activity, as the house-activity tests seat
        /// one - on a coordinator with no planner of its own - so the house's pairing and ambient seats are held: a
        /// conversation the house starts takes a seated houseguest off the furniture, as it should, and Jamie, seated by the
        /// test and not cooling down, is one the house's first pairing scan would try. The subject is the ask queue, and
        /// the seat has to hold for the card's 10 s: every second charged is a seated, busy Jamie's.
        /// </summary>
        private IEnumerator SeatedAsker() =>
            WithTheHouseHeld(() => OptIntoComesToYouHouse(false, false, Array.Empty<string>(), holdPairing: true), SeatedAskerKeepsTheSeat);

        private IEnumerator SeatedAskerKeepsTheSeat()
        {
            // Jamie's ask is first in public order ("jamie-roberts" before "maya-hassan"): seat Jamie.
            var first = ComesToYou.Read(director.Snapshot).First();
            Assert.That(first.askerId, Is.EqualTo(ComesJamie));
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            float seated = Time.realtimeSinceStartup;
            Assert.That(coordinator.LeaseCount, Is.Zero, "Nobody paired before the seat. " + SeatReport(coordinator, null, null, seated));
            var seat = HouseFurniture.InScene(player.gameObject.scene).FirstOrDefault(anchor => coordinator.ActivityAnchorAvailable(anchor));
            Assert.That(seat, Is.Not.Null, "A free piece of furniture.");
            Assert.That(coordinator.TryReserveActivity(ComesJamie, seat, out var activity, out var reason), Is.True,
                reason + " " + SeatReport(coordinator, seat, null, seated));
            seated = Time.realtimeSinceStartup;

            float deadline = Time.realtimeSinceStartup + 35f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya), "The next asker comes while Jamie sits. " + SeatReport(coordinator, seat, activity, seated));
            director.ClosePanels();

            // Jamie's own refusals add up to the card's time, setting nothing aside, and from slice 2 the ask comes as a card
            // where the player stands - on the very scan that makes it due - Jamie staying in their seat throughout: a
            // paired Jamie would be refused as busy too, so a seat lost on the way would let the test pass without a seated
            // asker.
            var unreachable = NpcRead<Dictionary<string, float>>("askUnreachable");
            NpcRead<HashSet<string>>("askSetAside").UnionWith(HeldAside(new[] { ComesMaya }));
            deadline = Time.realtimeSinceStartup + 40f;
            while ((!unreachable.TryGetValue(first.recordId, out float seconds) || seconds < EpisodeDirector.AskUnreachableSecondsBeforeCard)
                   && !director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                if (director.IsAskSetAside(first.recordId))
                    Assert.Fail("A busy asker's refusals set nothing aside. " + SeatReport(coordinator, seat, activity, seated));
                if (activity.Released)
                    Assert.Fail("Jamie keeps the seat while their own refusals add up. " + SeatReport(coordinator, seat, activity, seated));
                yield return null;
            }
            Assert.That(unreachable.TryGetValue(first.recordId, out float charged) ? charged : 0f,
                Is.GreaterThanOrEqualTo(EpisodeDirector.AskUnreachableSecondsBeforeCard),
                "No card before Jamie's own refusals add up to the card's time. " + SeatReport(coordinator, seat, activity, seated));
            // The scan that makes it due opens it, in its place in the order: no scan later, after something listed behind it.
            Assert.That(director.IsAskCardOpen, Is.True, "A busy asker's ask comes as a card on the scan that makes it due. "
                + SeatReport(coordinator, seat, activity, seated));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Card), "As a card. " + SeatReport(coordinator, seat, activity, seated));
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(first.recordId), "Jamie's ask. " + SeatReport(coordinator, seat, activity, seated));
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesJamie), "From Jamie. " + SeatReport(coordinator, seat, activity, seated));
            Assert.That(activity.Released, Is.False, "Jamie stays in the seat as the card opens. " + SeatReport(coordinator, seat, activity, seated));
            coordinator.ReleaseActivity(activity);
        }

        /// <summary>Jamie's seat as it stands this frame - whose it is, and what took it if it went - beside the asks' report on Jamie.</summary>
        private string SeatReport(HouseMeetingCoordinator coordinator, HouseInteractionAnchor seat, HouseActivityLease activity, float seatedAt)
        {
            bool held = coordinator != null && activity != null && coordinator.TryGetActivity(ComesJamie, out var now) && ReferenceEquals(now, activity);
            return "Seat " + (seat != null ? seat.VenueId + " #" + seat.Slot + " (free " + coordinator?.ActivityAnchorAvailable(seat) + ")" : "none")
                + ", released " + (activity != null ? activity.Released.ToString() : "-") + ", still Jamie's " + held + ", "
                + (Time.realtimeSinceStartup - seatedAt).ToString("0.0") + " s since the seat. " + AskerReport(ComesJamie);
        }

        // ------------------------------------------------------------------ 8. no ask while walking, at any frame rate

        [UnityTest]
        public IEnumerator ComesToYou_NoAskWhileWalkingAtHighFrameRate() => PinnedClock(NoAskWhileWalkingHeld, 1f / 240f);

        /// <summary>
        /// Jamie held aside, Maya the one asker who can come. The walk outlasts the house's first pairing scan (every third
        /// second of NPC time from a fresh house), which pairs idle houseguests in cast order, Maya first; a paired asker is
        /// refused as busy and her ask becomes a card, so no visit would come once the player stands. Nobody pairs off or
        /// takes a seat meanwhile, so the walk and then the still time are all that stand between the player and the visit.
        /// </summary>
        private IEnumerator NoAskWhileWalkingHeld() =>
            WithTheHouseHeld(() => OptIntoComesToYouHouse(false, false, new[] { ComesJamie }, holdPairing: true), NoAskWhileWalking);

        private IEnumerator NoAskWhileWalking()
        {
            AssertAskerUnpaired(ComesMaya, "as the walk starts");
            var view = cameraRig.ViewCamera;
            var from = player.transform.position;
            Vector3 to = from;
            for (int direction = 0; direction < 16 && to == from; direction++)
            {
                float angle = direction * Mathf.PI / 8f;
                var candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f;
                if (!NavMesh.SamplePosition(candidate, out var hit, .4f, player.Agent.areaMask)
                    || NavMesh.Raycast(from, hit.position, out _, player.Agent.areaMask)) continue;
                // The player's head well inside the frame there too: out of frame, no visit starts and the settle restarts.
                var seen = view.WorldToViewportPoint(hit.position + Vector3.up * 1.7f);
                if (seen.z > 0f && seen.x > .15f && seen.x < .85f && seen.y > .15f && seen.y < .85f) to = hit.position;
            }
            Assert.That(to, Is.Not.EqualTo(from), "Two floor points in one room, both in frame.");
            float until = Time.realtimeSinceStartup + 4f, arrived = float.NaN;
            bool outbound = true;
            Assert.That(player.TryMoveTo(to), Is.True);
            while (Time.realtimeSinceStartup < until)
            {
                if (director.CurrentNpcInvitationRecordId != null)
                    Assert.Fail("No visit is reserved while the player walks: " + AskerReport(ComesMaya));
                if (player.HasArrived)
                {
                    outbound = !outbound;
                    if (Time.realtimeSinceStartup < until) Assert.That(player.TryMoveTo(outbound ? to : from), Is.True);
                    else arrived = StandingSince();
                }
                yield return null;
            }
            // The walk alone held the visit back: Maya was free throughout, and the settle has run.
            AssertAskerUnpaired(ComesMaya, "after the walk");
            Assert.That(NpcRead<float>("askFreeRun"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds),
                "Only the walk held the visit back: the settle has run. " + AskerReport(ComesMaya));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!player.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.HasArrived, Is.True, "The last leg arrives. " + AskerReport(ComesMaya));
            if (float.IsNaN(arrived)) arrived = StandingSince();
            float waitedFrom = Time.realtimeSinceStartup;
            deadline = waitedFrom + 10f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (director.CurrentNpcInvitationRecordId == null)
                Assert.Fail("Once the player stands, a visit is reserved within 10 s: player at " + player.transform.position + " (from " + from
                    + ", to " + to + "), " + (Time.unscaledTime - arrived).ToString("0.00") + " s standing in unscaled time, "
                    + (Time.realtimeSinceStartup - waitedFrom).ToString("0.00") + " real s waited. " + AskerReport(ComesMaya));
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya), "The one asker not held aside comes. " + AskerReport(ComesMaya));
            // Plan test 8: no sooner than AskStillSeconds of unscaled time after the arrival - read on the runtime's own clock,
            // so a frame preempted before this coroutine resumes cannot shorten it; only float rounding needs the tolerance.
            float standing = Time.unscaledTime - arrived;
            Assert.That(standing, Is.GreaterThanOrEqualTo(EpisodeDirector.AskStillSeconds - .005f),
                "No sooner than the still time after the arrival, in unscaled time: " + standing.ToString("0.000") + " s, "
                + (Time.realtimeSinceStartup - waitedFrom).ToString("0.000") + " real s waited; " + AskClocksNow() + ".");
        }

        /// <summary>
        /// When the player, first seen standing on this frame, began to stand on the stillness clock's own terms, in
        /// unscaled time: the clock credits the first still frame's own unscaled time, at most the frame allowance
        /// (ComesToYouStillness.Step), and every later frame's, so the arrival is read from the start of that frame, never
        /// after it, and the still time from it is at least the time the clock credits.
        /// </summary>
        private static float StandingSince() =>
            Time.unscaledTime - Mathf.Min(Time.unscaledDeltaTime, EpisodeDirector.AskFrameAllowanceSeconds);

        // ------------------------------------------------------------------ 9. the asker is never paired or walked in on

        [UnityTest]
        public IEnumerator ComesToYou_AskerIsNeverPairedOrWalkedInOn() => PinnedClock(AskerNeverPaired);

        /// <summary>
        /// While the asker comes over, while their line is up and across the open, they are in no pending pair and no
        /// approach, and nobody walks in on them. The open is made to carry the house's time up to a pairing scan - the
        /// flush inside TryOpenNpc, after the visit itself has been let go, is where the busy set matters - and the walk-in
        /// is tried on the asker with somebody put beside them, against the same try with asks off.
        /// </summary>
        private IEnumerator AskerNeverPaired()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            // The house's pairing is this test's subject: with it held, every check below that the asker is unpaired would
            // pass whether or not the busy set holds her back.
            Assert.That(NpcRead<bool>("npcApproachDiagnosticsSuppressed"), Is.False, "The house's pairing is live from the reservation on: "
                + comesSwitchReport);
            // This test runs the walk-in watch.
            NpcWrite("proximityWatch", 0f);
            string askerId = director.CurrentAskerId;
            int walkInsBefore = WalkInsOn(askerId);

            // The line up, no pending pair the open's flush would wait on, and the asker free in the saved world - no
            // pending pair, no cooldown - so only the ask keeps them out of the scan. The auto-open is held meanwhile.
            bool Ready() => director.CurrentAskerId == askerId && director.NpcInvitationIsVisible && director.TalkingToId == null
                && (bool)NpcInvoke("AllPendingNpcLeasesReady") && !NpcPairing.Busy(director.Snapshot).Contains(askerId);
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!Ready() && Time.realtimeSinceStartup < deadline)
            {
                if (director.CurrentNpcInvitationRecordId != null)
                {
                    AssertAskerUnpaired(askerId, "while they bring an ask");
                    Assert.That(new[] { NpcRead<string>("walkInFirst"), NpcRead<string>("walkInSecond") }, Has.No.Member(askerId),
                        "nor walked in on.");
                }
                // The house runs live from the reservation on: it is this test's subject. A visit lost while the line is held
                // back leaves the asker with no visit until her retry, when the house may pair her and her ask comes as the
                // plan's card (A, C.2) - the plan's way, not the busy set failing, which the checks above would have caught.
                if (director.IsAskCardOpen)
                    Assert.Fail("The visit was lost while its line was held back, and the ask came as a card: " + AskerReport(askerId)
                        + " " + comesSwitchReport);
                NpcWrite("askSpokeFor", 0f);
                // The wait may outlast a lease; a miss here is the test's holding the line back, so the asker keeps coming.
                NpcRead<Dictionary<string, int>>("askMisses").Clear();
                yield return null;
            }
            Assert.That(Ready(), Is.True, "The line is up and the house could pair the asker: " + AskerReport(askerId) + " " + comesSwitchReport);
            AssertAskerUnpaired(askerId, "with their line up");

            // Somebody beside the asker is no walk-in while the asker is with the player; with asks off, the same two are.
            var tryWalkIn = typeof(EpisodeDirector).GetMethod("TryWalkInPair", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tryWalkIn, Is.Not.Null);
            bool WalkIn()
            {
                var arguments = new object[] { new List<string> { askerId, ComesJamie }, null, null };
                return (bool)tryWalkIn.Invoke(director, arguments);
            }
            var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            var other = SceneComponents<HouseNpc>().Single(npc => npc.Id == ComesJamie);
            Vector3 home = other.transform.position;
            Assert.That(PutBodyAt(other, BesideAsker(asker)), Is.True);
            Physics.SyncTransforms();
            bool withAsks = WalkIn();
            // Read at once, so no frame sees the switch turn: TickAsks never lets the visit go for it.
            director.AsksEnabled = false;
            bool withoutAsks = WalkIn();
            director.AsksEnabled = true;
            PutBodyAt(other, home);
            Physics.SyncTransforms();
            Assert.That(withoutAsks, Is.True, "The control: with asks off, the two of them are walked in on.");
            Assert.That(withAsks, Is.False, "With the asker bringing an ask, nobody walks in on them.");

            // E hears the ask; its open carries the house's time up to the next pairing scan.
            var state = director.Snapshot;
            long clock = state.npcSocial.clockTick, scanAt = state.npcSocial.nextScanTick;
            NpcWrite("npcFrameFraction", (double)Math.Max(1L, scanAt - clock));
            NpcWrite("npcWorldFraction", 0d);
            Assert.That(NpcRead<bool>("npcApproachDiagnosticsSuppressed"), Is.False, "The house's pairing is live for the open's scan: "
                + AskerReport(askerId));
            director.Interact();
            Assert.That(director.TalkingToId, Is.EqualTo(askerId), "E hears the ask.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(Math.Max(clock + 1, scanAt)),
                "The open's flush installed the house's time up to the scan.");
            Assert.That(director.Snapshot.npcSocial.nextScanTick, Is.GreaterThan(scanAt), "and the scan came due, so the house planned its pairs.");
            AssertAskerUnpaired(askerId, "across the open");
            yield return null;
            AssertAskerUnpaired(askerId, "once the dialogue is up");
            Assert.That(WalkInsOn(askerId), Is.EqualTo(walkInsBefore), "No walk-in on the asker was committed.");
        }

        /// <summary>The asker is in no pending pair, saved or held, and in no approach under way.</summary>
        private void AssertAskerUnpaired(string askerId, string when)
        {
            var pairs = NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld");
            Assert.That(pairs.Values.Any(lease => lease.FirstId == askerId || lease.SecondId == askerId), Is.False,
                "The asker is in no pending pair " + when + ".");
            Assert.That(director.Snapshot.npcSocial.pending.Any(row => row.firstId == askerId || row.secondId == askerId), Is.False,
                "nor a saved one " + when + ".");
            foreach (var approach in NpcRead<IList>("npcApproaches"))
            {
                var lease = (HouseMeetingLease)NpcObjectField(approach, "lease");
                Assert.That(lease.FirstId != askerId && lease.SecondId != askerId, Is.True,
                    "nor an approach " + when + ": " + lease.FirstId + " and " + lease.SecondId + ".");
            }
        }

        /// <summary>Walk-ins committed on a houseguest: the situations the walk-in watch has drawn about them.</summary>
        private int WalkInsOn(string id) => director.Snapshot.houseEvents.Count(row => row != null
            && row.kind == HouseEventKind.Proximity && row.involvedIds != null && row.involvedIds.Contains(id));

        /// <summary>A floor point beside the asker, well within a walk-in pair's reach of them and clear of the player.</summary>
        private Vector3 BesideAsker(HouseNpc asker)
        {
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                var candidate = asker.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;
                if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask)) continue;
                var across = hit.position - asker.transform.position; across.y = 0f;
                var fromPlayer = hit.position - player.transform.position; fromPlayer.y = 0f;
                if (across.magnitude <= EpisodeDirector.WalkInPairMetres - .5f && fromPlayer.magnitude >= .8f) return hit.position;
            }
            Assert.Fail("No floor beside " + asker.DisplayName + ".");
            return asker.transform.position;
        }

        // ------------------------------------------------------------------ also in slice 1: a stalled frame

        /// <summary>
        /// A stall is not time the line was on screen. With Run In Background off, a window out of focus stops the player
        /// loop while the clock runs on; counted whole, coming back would open the dialogue at once. One frame held for
        /// 1.5 s, against the 1.2 s beat, adds at most <see cref="EpisodeDirector.AskFrameAllowanceSeconds"/>; the beat
        /// still runs out once the game's own frames add up.
        /// </summary>
        [UnityTest]
        public IEnumerator ComesToYou_AStalledFrameDoesNotOpenTheDialogue() => PinnedClock(AStalledFrameDoesNotOpen);

        private IEnumerator AStalledFrameDoesNotOpen()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            float deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("askSpokeFor", 0f);
                yield return null;
            }
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The asker's line is up.");
            NpcWrite("askSpokeFor", 0f);
            System.Threading.Thread.Sleep(1500);
            yield return null;
            yield return null;
            Assert.That(director.TalkingToId, Is.Null,
                "A 1.5 s stall spends at most " + EpisodeDirector.AskFrameAllowanceSeconds + " s of the line's beat.");
            Assert.That(NpcRead<float>("askSpokeFor"), Is.LessThan(EpisodeDirector.AskSpeakBeatSeconds));
            Assert.That(director.AskMissCount(recordId), Is.Zero, "and a stall is no miss.");
            yield return WaitForAskDialogue(askerId);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver), "The beat still runs out on the game's frames.");
        }

        /// <summary>The director's own gate for a story scene that could stage now (EpisodeDirector.ComesToYou.cs), read as it stands this frame.</summary>
        private bool SceneStageWaitingNow()
        {
            var gate = typeof(EpisodeDirector).GetProperty("SceneStageWaiting", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(gate, Is.Not.Null, "The director's scene-waiting gate.");
            return (bool)gate.GetValue(director);
        }

        /// <summary>
        /// The Story Pull now up has held asks back its yield already: what a test about something else spends, so the Pull
        /// is not what holds a visit there (its yield is ComesToYou_APullYieldsAfterItsTime's business). Nothing while no
        /// Pull has been up.
        /// </summary>
        private void SpendThePullsYield()
        {
            if (NpcRead<string>("askPullKey") != null) NpcWrite("askPullHeld", EpisodeDirector.AskPullYieldSeconds);
        }

        /// <summary>The ask clocks and the gates beside them, for a failure's message.</summary>
        private string AskClocksNow() => "free " + ReportRead("askFreeRun") + " s, still " + ReportRead("askStillFor")
            + " s, clock " + ReportRead("askClock") + ", next " + ReportRead("askNextAt") + ", Pull held "
            + ReportRead("askPullHeld") + " s (up " + (ComesHud != null ? ComesHud.PullShowing.ToString() : "no HUD") + "), in frame " + ReportRead("askPlayerInFrame")
            + ", panel " + director.IsPanelOpen + ", card " + director.IsAskCardOpen;

        /// <summary>
        /// A private director field for a report, or "?": never an assertion, so a report built on a passing path, or on a
        /// failure's, cannot put a missing seam's message in place of what it reports. Test bodies read seams with NpcRead,
        /// where a missing one should fail.
        /// </summary>
        private object ReportRead(string name) => (director == null ? null
            : typeof(EpisodeDirector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(director)) ?? "?";

        /// <summary>A field of a runtime object for a report, or null: never an assertion (<see cref="ReportRead"/>).</summary>
        private static object ReportField(object owner, string name) =>
            owner?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(owner);

        /// <summary>
        /// Everything the asks hold about one asker this frame, for a failure's message, so a red says which gate, card or
        /// refusal it met: the asker's records (seconds of their own refusals - busy, no route, not bound - misses, set
        /// aside), what holds their body (the lease token names its owner: "approach" a house pairing, "invitation" a
        /// visit, "activity" a seat), whether the house has paired them; the card or visit up, the panel and chrome, the
        /// player's spot's count and the ask clocks; and the house's pairs, clock tick and next pairing scan.
        /// </summary>
        private string AskerReport(string askerId)
        {
            var motion = SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == askerId)?.GetComponent<HouseNpcMotion>();
            var unreachable = ReportRead("askUnreachable") as Dictionary<string, float>;
            var coordinator = ReportRead("npcMeetings") as HouseMeetingCoordinator;
            var social = director.Snapshot?.npcSocial;
            var records = director.Snapshot == null ? new List<string>()
                : ComesToYou.Read(director.Snapshot).Where(ask => ask.askerId == askerId).Select(ask => ask.recordId).ToList();
            string Record(string id) => id + " (unreachable " + (unreachable != null && unreachable.TryGetValue(id, out float seconds) ? seconds : 0f)
                + " s, misses " + director.AskMissCount(id) + ", set aside " + director.IsAskSetAside(id) + ")";
            var leases = ((ReportRead("npcPendingWorld") as Dictionary<long, HouseMeetingLease>)?.Values ?? Enumerable.Empty<HouseMeetingLease>())
                .Concat(((ReportRead("npcApproaches") as IList)?.Cast<object>() ?? Enumerable.Empty<object>())
                    .Select(approach => ReportField(approach, "lease") as HouseMeetingLease));
            bool paired = leases.Any(lease => lease != null && (lease.FirstId == askerId || lease.SecondId == askerId))
                || (social != null && social.pending.Any(row => row.firstId == askerId || row.secondId == askerId));
            string seat = coordinator != null && coordinator.TryGetActivity(askerId, out var activity) ? ", seated at " + activity.Anchor?.VenueId : "";
            var nearby = typeof(EpisodeDirector).GetProperty("NearbyOfferUp", BindingFlags.Instance | BindingFlags.NonPublic);
            return askerId + ": " + (records.Count == 0 ? "no ask" : string.Join(", ", records.Select(Record))) + "; lease "
                + (motion?.LeaseId ?? "none") + ", state " + motion?.State + ", arrival " + motion?.ArrivalFailure + ", route "
                + motion?.LastRouteFailure + ", paired " + paired + seat + ". Asks: card " + director.IsAskCardOpen + ", way "
                + director.CurrentAskWay + ", ask " + (director.CurrentAskRecordId ?? "none") + " (" + (director.CurrentAskerId ?? "nobody")
                + "), visit " + (director.CurrentNpcInvitationRecordId ?? "none") + ", line up " + director.NpcInvitationIsVisible
                + ", talking to " + (director.TalkingToId ?? "nobody") + ", emote menu " + director.EmoteMenuOpen + ", spot blocked "
                + ReportRead("askSpotBlockedFor") + " s, card settle " + ReportRead("askCardSettle") + " s, nearby "
                + (nearby != null ? nearby.GetValue(director) : "?") + ", " + AskClocksNow() + ". House: pairs " + coordinator?.LeaseCount
                + ", paused " + coordinator?.IsPaused + ", ready " + coordinator?.IsReady + ", last failure " + coordinator?.LastFailure
                + ", tick " + social?.clockTick + " (next scan " + social?.nextScanTick + "), pairing held "
                + ReportRead("npcApproachDiagnosticsSuppressed") + ". " + director.NpcAutonomyDiagnostic;
        }

        /// <summary>Why the asker's line never came up, for a failure's message.</summary>
        private string AskerWalkNow(string askerId)
        {
            var motion = SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == askerId)?.GetComponent<HouseNpcMotion>();
            return "visit " + (director.CurrentNpcInvitationRecordId ?? "none") + ", arrival " + motion?.ArrivalFailure + ", route "
                + motion?.LastRouteFailure + ", " + AskClocksNow() + ". " + director.NpcAutonomyDiagnostic;
        }

        // ------------------------------------------------------------------ also in slice 1: a scene waiting to stage

        /// <summary>
        /// A story scene waiting to stage holds new visits and the card back, never a visit already in flight
        /// (OFFERS-COME-TO-YOU-PLAN, C.2's gates): nothing stages over the visit, its dialogue still opens by itself on the
        /// line's beat, and the scene stages once the conversation has closed. The scene is the peace fixture's moment, open
        /// as the season loads, with a houseguest outside its cast bringing an offer (the house of
        /// SceneStage_AnInvitationUpKeepsTheFloorUntilItIsAnswered). A meeting between two others, standing at its venue,
        /// holds the house's pair slots as the visit sets out, so the house cannot take the scene then; it ends with the
        /// visit in flight, and from there the scene waits. Nobody else pairs off or takes a seat meanwhile, so that meeting
        /// is the only thing between the scene and its stage. The open is bounded on the line's own beat clock.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_SceneWaitingDoesNotStallAVisitInFlight() => PinnedClock(SceneWaitingDoesNotStallAVisit);

        private IEnumerator SceneWaitingDoesNotStallAVisit()
        {
            string askerId = null;
            yield return InstallPeaceFixture(suspend: false, arrange: state =>
            {
                foreach (var npc in state.Active.Where(person => !person.isPlayer))
                {
                    state.relationships.Single(edge => edge.fromId == npc.id && edge.toId == state.playerId).score = 10;
                    state.relationships.Single(edge => edge.fromId == state.playerId && edge.toId == npc.id).score = 60;
                }
                var asker = state.Active.Where(person => !person.isPlayer && person.id != peaceFirstId && person.id != peaceSecondId)
                    .OrderBy(person => person.id, StringComparer.Ordinal).First();
                state.relationships.Single(edge => edge.fromId == asker.id && edge.toId == state.playerId).score = 90;
                NpcDeals.Propose(state);
                askerId = asker.id;
                Assert.That(ComesToYou.Read(state).Any(ask => ask.askerId == askerId), Is.True, askerId + " has an ask to bring the player.");
            });
            director.BuildNpcWorldForDiagnostics();
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null);
            Assert.That(EpisodeEngine.OpenStoryBeats(director.Snapshot).Any(item => item.id == peaceEventId), Is.True,
                "The moment is open as the season loads.");
            // Only the asker comes: every other ask is held aside.
            var others = ComesToYou.Read(director.Snapshot).Select(ask => ask.askerId).Where(id => id != askerId).Distinct().ToArray();
            // Pairing held from the bodies' assembly on, so no director Update runs with it live before the hold below.
            yield return TurnAsksOnInTheLoadedHouse(others, holdPairing: true);
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            float ambient = NpcRead<float>("nextAmbientActivity");
            NpcWrite("nextAmbientActivity", float.PositiveInfinity);
            try { yield return SceneWaitsBehindTheVisit(askerId); }
            finally
            {
                NpcWrite("npcApproachDiagnosticsSuppressed", false);
                NpcWrite("nextAmbientActivity", ambient);
            }
        }

        private IEnumerator SceneWaitsBehindTheVisit(string askerId)
        {
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            Assert.That(coordinator, Is.Not.Null, "The house's world.");
            // The house's world bound and running, and its own meetings over first, so the one below is the only thing
            // keeping the scene from its stage; the scan is held meanwhile, so no visit sets out before that meeting does.
            NpcWrite("askNextAt", float.MaxValue);
            float deadline = Time.realtimeSinceStartup + 40f;
            while ((!coordinator.IsReady || coordinator.IsPaused || coordinator.LeaseCount > 0 || NpcRead<IList>("npcApproaches").Count > 0)
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(coordinator.IsReady && !coordinator.IsPaused, Is.True, "The house's world is bound and running: " + SceneStageDiagnostic());
            Assert.That(coordinator.LeaseCount, Is.Zero, "The house's own meetings are over: " + SceneStageDiagnostic());

            HouseMeetingLease meeting = null;
            try
            {
                // In one frame: whatever the season staged as it loaded let go, two others meeting, and the moment's one try
                // unspent - the moment wanted, and the house unable to take it while the meeting holds its pair slots. The
                // house is small by now (the peace fixture is a week-two campaign), so the two may be the scene's own.
                if (coordinator.HasSceneStage) coordinator.EndSceneStage();
                var free = director.Snapshot.Active.Where(person => !person.isPlayer && person.id != askerId)
                    .Select(person => person.id).OrderBy(id => id, StringComparer.Ordinal).ToList();
                string reason = "fewer than two others in the house";
                for (int first = 0; first < free.Count && meeting == null; first++)
                    for (int second = first + 1; second < free.Count && meeting == null; second++)
                        coordinator.TryReservePair("comes-to-you-scene-meeting", free[first], free[second], out meeting, out reason);
                Assert.That(meeting, Is.Not.Null, "Two others meet: " + reason);
                NpcWrite("sceneStagedBeat", null);
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit set out before the meeting.");

                // The two stand at their venue before the visit sets out, so the meeting's end leaves nobody stopped
                // mid-route on the asker's way, and the visit's own route and place are found with them where they stay.
                // The scan is still held.
                var meetingFirst = SceneComponents<HouseNpc>().Single(npc => npc.Id == meeting.FirstId).GetComponent<HouseNpcMotion>();
                var meetingSecond = SceneComponents<HouseNpc>().Single(npc => npc.Id == meeting.SecondId).GetComponent<HouseNpcMotion>();
                Assert.That(meetingFirst != null && meetingSecond != null, Is.True, "Both of the meeting's bodies move.");
                bool MeetingOnMark() => meetingFirst.IsOnMark(meeting.Token) && meetingSecond.IsOnMark(meeting.Token);
                deadline = Time.realtimeSinceStartup + 30f;
                while (!MeetingOnMark() && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    Assert.That(coordinator.HasSceneStage, Is.False, "Nothing stages while the meeting holds the house: " + SceneStageDiagnostic());
                    Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit sets out while the scan is held.");
                }
                Assert.That(MeetingOnMark(), Is.True, "The two meeting stand at their venue: " + meeting.FirstId + " arrival "
                    + meetingFirst.ArrivalFailure + ", route " + meetingFirst.LastRouteFailure + "; " + meeting.SecondId + " arrival "
                    + meetingSecond.ArrivalFailure + ", route " + meetingSecond.LastRouteFailure + ". " + SceneStageDiagnostic());
                Assert.That(coordinator.TryGetLease(meeting.Token, out _), Is.True, "The meeting holds the house.");
                NpcWrite("askNextAt", NpcRead<float>("askClock"));

                deadline = Time.realtimeSinceStartup + 35f;
                while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline)
                {
                    SpendThePullsYield();
                    yield return null;
                    Assert.That(coordinator.HasSceneStage, Is.False, "Nothing stages while the meeting holds the house: " + SceneStageDiagnostic());
                }
                if (director.CurrentNpcInvitationRecordId == null)
                    Assert.Fail("No visit set out while the meeting held the house: " + AskClocksNow() + ", scene waiting "
                        + SceneStageWaitingNow() + ". " + AskerReport(askerId) + " " + comesSwitchReport);
                string recordId = director.CurrentNpcInvitationRecordId;
                Assert.That(director.CurrentAskerId, Is.EqualTo(askerId));
                // As it sets out the moment is wanted and its try unspent, but the house cannot take it: no scene waits yet.
                Assert.That(SceneStageWaitingNow(), Is.False, "No scene waits while the house cannot take it: " + SceneStageDiagnostic());
                Assert.That(NpcRead<HouseEventState>("stagedBeat")?.id, Is.EqualTo(peaceEventId), "The moment is the scene the house would stage.");
                Assert.That(NpcRead<string>("sceneStagedBeat"), Is.Null, "Its one try is unspent.");
                Assert.That(coordinator.TryGetLease(meeting.Token, out _), Is.True, "The meeting still holds the house.");
                Assert.That(coordinator.CanBeginSceneStage, Is.False, SceneStageDiagnostic());

                // The meeting ends with the visit in flight: from here the scene waits.
                Assert.That(coordinator.Release(meeting), Is.True, "The meeting ends.");
                meeting = null;
                Assert.That(coordinator.LeaseCount, Is.Zero, SceneStageDiagnostic());
                Assert.That(coordinator.CanBeginSceneStage, Is.True, "The house could take the scene now: " + SceneStageDiagnostic());
                Assert.That(SceneStageWaitingNow(), Is.True, "The scene waits to stage.");
                Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId));

                // The visit keeps the floor: nothing stages over it while the asker comes over.
                deadline = Time.realtimeSinceStartup + 35f;
                while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline)
                {
                    SpendThePullsYield();
                    yield return null;
                    Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId),
                        "The visit in flight keeps the floor while the scene waits: misses " + director.AskMissCount(recordId) + ".");
                    Assert.That(coordinator.HasSceneStage, Is.False, "Nothing stages over a visit in flight: " + SceneStageDiagnostic());
                    Assert.That(SceneStageWaitingNow(), Is.True, "The scene still waits: " + SceneStageDiagnostic());
                }
                if (!director.NpcInvitationIsVisible) Assert.Fail("The asker's line never came up: " + AskerWalkNow(askerId));

                // The line up: the dialogue opens by itself on its beat while the scene waits. The beat runs on the
                // director's own clock - each frame's time, capped, and from nothing again whenever the line cannot be
                // said - so the wait is bounded there: a second past the beat, against the 10 Hz step that opens it. A
                // stretched frame or a flicker of the line moves the clock as it moves the open; the real-time guard
                // only stops a hang. The scene is read before each frame, as the open frame pauses the house.
                float spoke = Time.realtimeSinceStartup;
                while (director.TalkingToId == null && NpcRead<float>("askSpokeFor") < EpisodeDirector.AskSpeakBeatSeconds + 1f
                    && Time.realtimeSinceStartup < spoke + 35f)
                {
                    Assert.That(SceneStageWaitingNow(), Is.True, "The scene waits while the line is up: " + SceneStageDiagnostic());
                    Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId),
                        "The visit keeps the floor while its line is up: misses " + director.AskMissCount(recordId) + ".");
                    SpendThePullsYield();
                    yield return null;
                    Assert.That(coordinator.HasSceneStage, Is.False, "Nothing stages over the line: " + SceneStageDiagnostic());
                }
                Assert.That(director.TalkingToId, Is.EqualTo(askerId), "The dialogue opens by itself on the line's beat, the scene waiting: "
                    + (Time.realtimeSinceStartup - spoke).ToString("0.00") + " s after the line came up, line on screen "
                    + NpcRead<float>("askSpokeFor") + " s against a beat of " + EpisodeDirector.AskSpeakBeatSeconds + " s, "
                    + AskClocksNow() + ", scene waiting " + SceneStageWaitingNow() + ", misses " + director.AskMissCount(recordId) + ".");
                // The open frame.
                Assert.That(ComesHud.PointerHeld, Is.True, "Pointer presses are held off the panel as it opens.");
                Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
                Assert.That(director.InvitedDealRecordId, Is.EqualTo(recordId));
                Assert.That(director.AskMissCount(recordId), Is.Zero, "The visit is no miss.");
                Assert.That(NpcRead<string>("sceneStagedBeat"), Is.Null, "The scene's one try waited for the visit.");
                yield return null; yield return null;
                Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption), "The keyboard opens on the way out that answers nothing.");

                // Under the conversation the scene still waits: a panel holds its stage.
                float until = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    Assert.That(coordinator.HasSceneStage, Is.False, "Nothing stages under the conversation: " + SceneStageDiagnostic());
                    Assert.That(director.TalkingToId, Is.EqualTo(askerId));
                }

                // Closed: the scene stages.
                director.ClosePanels();
                yield return WaitForSceneStage(peaceEventId, 10f, "The scene stages once the conversation has closed");
                Assert.That(director.AskMissCount(recordId), Is.Zero);
            }
            finally
            {
                if (meeting != null) coordinator.Release(meeting);
            }
        }

        // ------------------------------------------------------------------ also in slice 1: following somebody else

        /// <summary>
        /// The camera following somebody else takes the player out of the frame: a visit in flight goes, as no miss and
        /// with nothing set aside, and no visit starts while the player is out of it - not when the follow is let go
        /// either, for the camera stays where it is and is never moved for an ask - until the camera is back on the
        /// player, when the same ask sets out again after the settle time. Nobody pairs off or takes a seat meanwhile, so
        /// the houseguest followed stays where they are.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_FollowingSomeoneElseCancelsWithoutAMiss() => PinnedClock(FollowingSomeoneElse);

        /// <summary>
        /// Pairing held from the bodies' assembly on, not only once the visit is reserved: the follow lets the visit go,
        /// after which Maya has no visit in flight, so a house free to pair would take her (plan C.2's busy set) before the
        /// same ask sets out again.
        /// </summary>
        private IEnumerator FollowingSomeoneElse() =>
            WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), FollowingSomeoneElseCancels);

        private IEnumerator FollowingSomeoneElseCancels()
        {
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            int misses = director.AskMissCount(recordId);
            Assert.That(NpcRead<bool>("askPlayerInFrame"), Is.True, "The player is in frame as the visit sets out.");

            // Somebody else standing still - in no meeting under way, and holding no reservation of the house's (an
            // activity, an act, a wander, a meeting or a scene), so nothing walks or stands them up while followed - the
            // farthest from the player first, whose follow takes the player out of the frame. The camera goes to each in
            // turn - into their room, with a cut where it is another room's (RS5), a view that leaves the player out and
            // so is out of frame whatever the room's framing takes in; the dialogue's own opening is held meanwhile, so a
            // line that comes up stays a line, and the visit stays in flight while the player is in frame.
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            Assert.That(coordinator, Is.Not.Null, "The house's world.");
            var walking = new HashSet<string>(StringComparer.Ordinal);
            foreach (var approach in NpcRead<IList>("npcApproaches"))
            {
                var lease = (HouseMeetingLease)NpcObjectField(approach, "lease");
                walking.Add(lease.FirstId); walking.Add(lease.SecondId);
            }
            foreach (var lease in NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld").Values)
            {
                walking.Add(lease.FirstId); walking.Add(lease.SecondId);
            }
            bool Unreserved(HouseNpc npc)
            {
                var motion = npc.GetComponent<HouseNpcMotion>();
                return motion != null && motion.LeaseId == null && !coordinator.TryGetActivity(npc.Id, out _);
            }
            var candidates = SceneComponents<HouseNpc>()
                .Where(npc => npc.Id != askerId && !walking.Contains(npc.Id) && npc.gameObject.activeInHierarchy
                    && director.Snapshot.Find(npc.Id)?.status == ContestantStatus.Active && Unreserved(npc))
                .OrderByDescending(npc => Vector3.Distance(npc.transform.position, player.transform.position))
                .ToList();
            Assert.That(candidates, Is.Not.Empty, "Somebody else to follow.");
            HouseNpc other = null;
            var tried = new List<string>();
            float deadline;
            // The ask's retry wait and the next scan as they stood the frame before, so the frame the player leaves it can
            // show the cancel put no wait on the ask: only the settle time holds it once the player is back (plan C.2).
            var retryAt = NpcRead<Dictionary<string, float>>("askRetryAt");
            string RetryNow() => retryAt.TryGetValue(recordId, out float at) ? at.ToString("R") : "none";
            string retryBefore = RetryNow();
            float nextAtBefore = NpcRead<float>("askNextAt");
            foreach (var candidate in candidates)
            {
                director.FollowHouseguest(candidate.Id);
                Assert.That(director.FollowedId, Is.EqualTo(candidate.Id), "The camera follows " + candidate.DisplayName + ".");
                deadline = Time.realtimeSinceStartup + 5f;
                while (NpcRead<bool>("askPlayerInFrame") && Time.realtimeSinceStartup < deadline)
                {
                    NpcWrite("askSpokeFor", 0f);
                    retryBefore = RetryNow();
                    nextAtBefore = NpcRead<float>("askNextAt");
                    yield return null;
                    Assert.That(director.TalkingToId, Is.Null, "Nothing opens while the camera rides over.");
                    if (NpcRead<bool>("askPlayerInFrame"))
                        Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId),
                            "The visit stays in flight while the player is in frame: misses " + director.AskMissCount(recordId) + ".");
                }
                tried.Add(candidate.DisplayName + " " + Vector3.Distance(candidate.transform.position, player.transform.position).ToString("0.0") + " m away");
                if (!NpcRead<bool>("askPlayerInFrame")) { other = candidate; break; }
            }
            Assert.That(other, Is.Not.Null, "Following somebody else takes the player out of the frame; tried " + string.Join(", ", tried) + ".");
            // The frame the player left it: the visit went, as no miss and with nothing set aside.
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "A visit in flight goes the frame the player is out of it.");
            Assert.That(NpcRead<HouseNpcInvitationLease>("askVisit"), Is.Null, "Its lease is let go,");
            Assert.That(director.NpcInvitationIsVisible, Is.False, "and its line with it.");
            Assert.That(director.AskMissCount(recordId), Is.EqualTo(misses), "Following somebody else is no miss,");
            Assert.That(director.IsAskSetAside(recordId), Is.False, "and no deferral.");
            Assert.That(RetryNow(), Is.EqualTo(retryBefore), "The cancel puts no retry wait on the ask,");
            Assert.That(NpcRead<float>("askNextAt"), Is.EqualTo(nextAtBefore), "nor puts off the next scan.");

            // While the player is out of frame no visit starts: the free-house clock waits for them. Long enough for the
            // settle, the still time and three scans.
            float until = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + 3f * EpisodeDirector.AskScanSeconds + 2f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Assert.That(director.FollowedId, Is.EqualTo(other.Id), "Still following " + other.DisplayName + ".");
                Assert.That(NpcRead<bool>("askPlayerInFrame"), Is.False, "The player stays out of frame while " + other.DisplayName + " is followed.");
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit starts while the player is out of frame.");
                Assert.That(NpcRead<float>("askFreeRun"), Is.Zero, "The free-house clock waits for the player to be in frame.");
            }
            Assert.That(director.AskMissCount(recordId), Is.EqualTo(misses));
            Assert.That(director.IsAskSetAside(recordId), Is.False);

            // Let go, the camera stays where it is - it is never moved for an ask - so the ask still waits.
            director.FollowHouseguest(other.Id);
            Assert.That(director.FollowedId, Is.Null, "The same houseguest again lets go.");
            until = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskScanSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Assert.That(NpcRead<bool>("askPlayerInFrame"), Is.False, "Letting go leaves the camera where it is.");
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit starts while the player is still out of frame.");
                Assert.That(NpcRead<float>("askFreeRun"), Is.Zero, "The free-house clock waits for the frame, not for the follow.");
            }

            // The camera back on the player: the same ask sets out again once the house has settled.
            director.FollowHouseguest(director.Snapshot.playerId);
            deadline = Time.realtimeSinceStartup + 10f;
            while (!NpcRead<bool>("askPlayerInFrame") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(NpcRead<bool>("askPlayerInFrame"), Is.True, "Following the player brings them back into frame.");
            float back = Time.realtimeSinceStartup;
            // The settle is measured on the director's free-house clock, which moves only while a visit may start: the
            // frame the player came back has credited at most one frame's allowance of it already.
            float clockBack = NpcRead<float>("askClock");
            deadline = back + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds + 2f * EpisodeDirector.AskScanSeconds + 5f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId),
                "The same ask sets out again once the player is back in frame: " + AskerReport(askerId) + " " + comesSwitchReport);
            Assert.That(NpcRead<float>("askClock") - clockBack, Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds - EpisodeDirector.AskFrameAllowanceSeconds),
                "No sooner than the settle time after the player is back in frame: " + AskClocksNow() + ".");
            Assert.That(director.AskMissCount(recordId), Is.EqualTo(misses));
            Assert.That(director.IsAskSetAside(recordId), Is.False);
        }

        // ------------------------------------------------------------------ also in slice 1: a Pull yields after its time

        /// <summary>
        /// Owner question 5, answered 2026-10-30 with the default: a Story Pull on screen holds new asks back for 20 s
        /// (<see cref="EpisodeDirector.AskPullYieldSeconds"/>, pinned here) of free, still time, then yields; a stretch in
        /// the middle with the player kept from counting as still adds nothing to it. The visit then sets out beside the
        /// Pull; the Pull gives way once the asker's line is up - the two never share the screen - and comes back
        /// undeclined once the conversation has closed, its yield still spent. The Pull is a walk-in found as the proximity
        /// watch finds one, which writes nothing (InvitationAndPull's staging), on the invitation flow's house with the
        /// story on. Nobody pairs off or takes a seat meanwhile, so the Pull is all that holds the house, which the watch
        /// checks gate by gate.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_APullYieldsAfterItsTime() => PinnedClock(APullYields);

        /// <summary>Owner question 5, answered 2026-10-30: the default, a Pull holds new asks back 20 s.</summary>
        private const float OwnersPullYieldSeconds = 20f;

        /// <summary>
        /// How long, in the middle of the Pull's yield, the player is kept from counting as still, so the test shows the
        /// yield's clock counts only free, still time.
        /// </summary>
        private const float PullStretchSeconds = 3f;

        private IEnumerator APullYields()
        {
            // The runtime's constant is compiled into this assembly, so every bound below would move with it: it is
            // pinned to the owner's answer first.
            Assert.That(EpisodeDirector.AskPullYieldSeconds, Is.EqualTo(OwnersPullYieldSeconds),
                "Owner question 5, answered 2026-10-30: the default, a Pull holds new asks back 20 s.");
            // Pairing held from the bodies' assembly on, so no director Update runs with it live before the hold below.
            yield return OptIntoComesToYouHouse(false, true, new[] { ComesJamie }, holdPairing: true);
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            float ambient = NpcRead<float>("nextAmbientActivity");
            NpcWrite("nextAmbientActivity", float.PositiveInfinity);
            try { yield return APullYieldsAfterItsTime(); }
            finally
            {
                NpcWrite("npcApproachDiagnosticsSuppressed", false);
                NpcWrite("nextAmbientActivity", ambient);
            }
        }

        private IEnumerator APullYieldsAfterItsTime()
        {
            string recordId = ComesToYou.Read(director.Snapshot).Single(ask => ask.askerId == ComesMaya).recordId;
            Assert.That(director.IsOverview || director.IsBriefing, Is.False, "The house's own view, where the Pull's card is built.");
            var now = director.Snapshot;
            Assert.That(EpisodeEngine.StoryOn(now), Is.True, "A walk-in is offered as a Pull only under the story system.");
            var pair = now.Active.Where(actor => !actor.isPlayer && actor.id != ComesMaya).Select(actor => actor.id)
                .OrderBy(id => id, StringComparer.Ordinal).Take(2).ToList();
            Assert.That(pair, Has.Count.EqualTo(2), "Two others to walk in on.");
            NpcWrite("walkInFirst", pair[0]);
            NpcWrite("walkInSecond", pair[1]);
            NpcWrite("walkInRoom", "Living");
            NpcWrite("walkInWeek", now.week);
            NpcWrite("proximityWatch", -1e6f);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!ComesHud.PullShowing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ComesHud.PullShowing, Is.True, "The walk-in's Pull is up.");
            Assert.That(director.PullOffered, Is.EqualTo(EpisodeHud.StepInCaption));
            Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "No visit set out before the Pull came up.");
            float shown = Time.realtimeSinceStartup;
            string pullKey = NpcRead<string>("askPullKey");
            Assert.That(pullKey, Is.Not.Null, "The asks' yield clock is on this Pull.");

            // Held back for its time: no visit sets out while the Pull has been up less than its yield. The watch runs to
            // the earliest a visit can set out: the yield, the stretch below (which credits the yield nothing) and the
            // settle after them, less what four frames can each credit ahead of the real time they cover - the two around
            // `shown`, the one the stretch starts on and the first free one - at most one allowance each.
            float gatesFrom = shown + EpisodeDirector.AskStillSeconds + .5f, stretchFrom = shown + 5f, stretchTo = stretchFrom + PullStretchSeconds;
            float watchTo = shown + OwnersPullYieldSeconds + PullStretchSeconds + EpisodeDirector.AskSettleSeconds
                - 4f * EpisodeDirector.AskFrameAllowanceSeconds;
            var visitHouseFree = typeof(EpisodeDirector).GetMethod("VisitHouseFree", BindingFlags.Instance | BindingFlags.NonPublic);
            var askHouseAvailable = typeof(EpisodeDirector).GetProperty("AskHouseAvailable", BindingFlags.Instance | BindingFlags.NonPublic);
            var waitWhileSeated = typeof(EpisodeDirector).GetField("AsksWaitWhileSeated", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(visitHouseFree != null && askHouseAvailable != null && waitWhileSeated != null, Is.True, "The director's house gates.");
            object[] houseTerms = { false, waitWhileSeated.GetRawConstantValue() };
            float heldAtStretch = float.NaN;
            bool stretchChecked = false;
            while (Time.realtimeSinceStartup < watchTo)
            {
                float real = Time.realtimeSinceStartup;
                if (director.CurrentNpcInvitationRecordId != null)
                    Assert.Fail("No visit sets out while the Pull has been up less than its yield: one did "
                        + (real - shown).ToString("0.0") + " s into the Pull's " + OwnersPullYieldSeconds + " s (" + AskClocksNow() + ").");
                Assert.That(ComesHud.PullShowing, Is.True, "The Pull stays up meanwhile.");
                Assert.That(NpcRead<string>("askPullKey"), Is.EqualTo(pullKey), "It is the same Pull.");
                Assert.That(director.TalkingToId, Is.Null);
                if (real >= gatesFrom && real < stretchFrom)
                {
                    // Only the Pull holds the house: the player in frame and still, the house free but for the Pull - the
                    // gate the asks share with the invitation - and so the free-house clock stopped.
                    Assert.That(NpcRead<bool>("askPlayerInFrame"), Is.True, "The player is in frame while the Pull holds the house,");
                    Assert.That(NpcRead<float>("askStillFor"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskStillSeconds), "and still,");
                    Assert.That((bool)visitHouseFree.Invoke(director, houseTerms), Is.True, "and the house is free but for the Pull,");
                    Assert.That((bool)askHouseAvailable.GetValue(director), Is.False, "which holds it: " + AskClocksNow() + ".");
                    Assert.That(NpcRead<float>("askFreeRun"), Is.Zero, "The free-house clock waits on the Pull alone.");
                }
                if (real >= stretchFrom && real < stretchTo)
                {
                    // The player kept from counting as still: each frame credits stillness at most one frame's allowance,
                    // under the still time, so the yield's clock should not move.
                    if (float.IsNaN(heldAtStretch)) heldAtStretch = NpcRead<float>("askPullHeld");
                    NpcWrite("askStillFor", 0f);
                }
                else if (real >= stretchTo && !stretchChecked)
                {
                    stretchChecked = true;
                    Assert.That(float.IsNaN(heldAtStretch), Is.False, "The stretch ran.");
                    Assert.That(NpcRead<float>("askPullHeld") - heldAtStretch, Is.LessThanOrEqualTo(EpisodeDirector.AskFrameAllowanceSeconds),
                        "The Pull's yield counts only still time: " + PullStretchSeconds + " s un-still from " + heldAtStretch + " s held; "
                        + AskClocksNow() + ".");
                }
                yield return null;
            }
            Assert.That(stretchChecked, Is.True, "The stretch was checked.");

            // Then it yields: the visit sets out beside it.
            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + 2f * EpisodeDirector.AskScanSeconds + 20f;
            while (director.CurrentNpcInvitationRecordId == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (director.CurrentNpcInvitationRecordId == null)
                Assert.Fail("No visit set out once the Pull had been up its time: " + AskerReport(ComesMaya) + " " + comesSwitchReport);
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(recordId), "Maya sets out.");
            // The yield's clock goes on counting under the settle that follows it - the Pull up, the player still, the house
            // free: the same frames the free-house clock counts, which ran from nothing at most one allowance before the
            // yield was spent - so it reads the yield and the settle, less that allowance, once the visit sets out.
            Assert.That(NpcRead<float>("askPullHeld"),
                Is.GreaterThanOrEqualTo(OwnersPullYieldSeconds + EpisodeDirector.AskSettleSeconds - EpisodeDirector.AskFrameAllowanceSeconds),
                "Only once the Pull's yield is spent, and the settle after it: " + AskClocksNow() + ".");
            Assert.That(ComesHud.PullShowing, Is.True, "The visit sets out beside the Pull.");
            Assert.That(NpcRead<string>("askPullKey"), Is.EqualTo(pullKey));

            // The Pull keeps its place until the asker's line is up, and then gives way to it: the two never share the screen.
            deadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (!director.NpcInvitationIsVisible)
                    Assert.That(ComesHud.PullShowing, Is.True, "The Pull stays up while the asker comes over.");
            }
            if (!director.NpcInvitationIsVisible) Assert.Fail("The asker's line never came up: " + AskerWalkNow(ComesMaya));
            // The Pull's tick runs before the asks' in a frame, so it reads the line the frame after it came up.
            float spoke = Time.realtimeSinceStartup;
            int frames = 0;
            while (ComesHud.PullShowing && director.NpcInvitationIsVisible && Time.realtimeSinceStartup < spoke + 1f)
            {
                yield return null;
                frames++;
            }
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The line is still up.");
            Assert.That(ComesHud.PullShowing, Is.False, "The Pull hides once the asker's line shows.");
            Assert.That(frames, Is.LessThanOrEqualTo(2), "at once: the frame after the line came up.");
            Assert.That(director.PullOffered, Is.Null);

            // The dialogue opens by itself; the Pull stays down beside the line and under the conversation.
            bool lineWasUp = true;
            deadline = Time.realtimeSinceStartup + 35f;
            while (director.TalkingToId == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                bool lineUp = director.NpcInvitationIsVisible;
                if (lineUp && lineWasUp) Assert.That(ComesHud.PullShowing, Is.False, "The Pull and the line never share the screen.");
                lineWasUp = lineUp;
            }
            if (director.TalkingToId == null) Assert.Fail("The dialogue never opened on Maya: " + AskerWalkNow(ComesMaya));
            Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya));
            // The open frame.
            Assert.That(ComesHud.PointerHeld, Is.True, "Pointer presses are held off the panel as it opens.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(director.InvitedDealRecordId, Is.EqualTo(recordId));
            Assert.That(ComesHud.PullShowing, Is.False, "The conversation hides the Pull too.");
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption), "The keyboard opens on the way out that answers nothing.");
            Assert.That(ComesHud.PullShowing, Is.False);

            // Closed: the Pull comes back as it was - undeclined, the walk-in still the player's to step into - and its
            // yield stays spent, so the same Pull does not hold the next ask back again.
            director.ClosePanels();
            deadline = Time.realtimeSinceStartup + 3f;
            while (!ComesHud.PullShowing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ComesHud.PullShowing, Is.True, "The Pull comes back once the conversation has closed.");
            Assert.That(director.PullOffered, Is.EqualTo(EpisodeHud.StepInCaption), "The same walk-in's Pull.");
            Assert.That(NpcRead<HashSet<string>>("declinedPulls"), Is.Empty, "Nothing was turned down: the Pull only gave way.");
            Assert.That(NpcRead<string>("walkInFirst"), Is.EqualTo(pair[0]), "The walk-in is still the player's to step into.");
            Assert.That(NpcRead<string>("askPullKey"), Is.EqualTo(pullKey), "The same Pull,");
            Assert.That(NpcRead<float>("askPullHeld"), Is.GreaterThanOrEqualTo(OwnersPullYieldSeconds), "its yield still spent.");
            Assert.That(director.AskMissCount(recordId), Is.Zero);
        }

        // ------------------------------------------------------------------ 10. captures and the frame's fit

        [UnityTest]
        public IEnumerator ComesToYou_CapturesTheDialogueAtEachSizeAndMotion() => PinnedClock(CapturesTheDialogue);

        private IEnumerator CapturesTheDialogue()
        {
            foreach (var textSize in new[] { TextSize.Standard, TextSize.Largest })
                foreach (bool reduced in new[] { false, true })
                {
                    string at = textSize + (reduced ? ", reduced motion" : "");
                    // The subject is the dialogue's frame, not the house's autonomy: pairing is held from the bodies' assembly
                    // to the open. The settings panels below cancel the visit just reserved and hand its asker back, and a
                    // house free to pair would take an asker the next scan needs (the busy set holds back only the visit's
                    // asker and the one opening, plan C.2), whose ask would then come as a card.
                    yield return InstallComesToYouHouse(true, ComesJamie);
                    // Jamie waits too, so the dialogue draws its "1 more" line: one visit at a time, so Jamie's ask waits
                    // on Maya's, and whichever of them comes first, the other is the one more who wants a word.
                    var aside = NpcRead<HashSet<string>>("askSetAside");
                    foreach (var id in HeldAside(new[] { ComesJamie })) aside.Remove(id);
                    yield return ApplyTextSize(textSize);
                    if (reduced)
                    {
                        director.OpenSettings();
                        yield return null;
                        ButtonWithCaption("Reduce character motion").onClick.Invoke();
                        yield return null;
                        director.ClosePanels();
                        yield return null;
                        Assert.That(cameraRig.ReducedMotion, Is.True, at + ": the setting took, so this pass is not vacuous.");
                    }
                    // Waits for the open, keeping the shot from the frame before it.
                    Vector3 focusBefore = cameraRig.DesiredFocus;
                    float deadline = Time.realtimeSinceStartup + 45f;
                    while (director.TalkingToId == null && Time.realtimeSinceStartup < deadline)
                    {
                        focusBefore = cameraRig.DesiredFocus;
                        yield return null;
                    }
                    if (director.TalkingToId == null)
                        Assert.Fail(at + ": the dialogue opens. " + AskerReport(ComesJamie) + " | " + AskerReport(ComesMaya));
                    // Open: the hold is let go, so the house runs on under the dialogue as it does after any open.
                    NpcWrite("npcApproachDiagnosticsSuppressed", false);
                    string askerId = director.TalkingToId;
                    Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver), at);
                    Assert.That(director.CurrentAskerId, Is.EqualTo(askerId), at);
                    Assert.That(cameraRig.IsConversationFocused, Is.True, at);
                    var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
                    if (reduced)
                    {
                        Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.LessThanOrEqualTo(.01f), at + ": the camera does not move.");
                        var seen = cameraRig.ViewCamera.WorldToViewportPoint(asker.transform.position + Vector3.up * 1.6f);
                        Assert.That(seen.z > 0f && seen.x >= 0f && seen.x <= 1f && seen.y >= 0f && seen.y <= 1f, Is.True, at + ": the asker is in frame.");
                    }
                    else Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.GreaterThan(.01f), at + ": the rig takes the two-shot.");
                    yield return WaitForBodies();
                    yield return WaitOutThePointerHold();
                    Assert.That(LastLabel(EpisodeHud.AskMoreName)?.text, Is.EqualTo(EpisodeHud.AskMoreWaitingLine(1)),
                        at + ": the other asker is the one more who wants a word.");
                    AssertEveryLabelDraws(at + ": the ask dialogue");
                    foreach (var name in new[] { EpisodeHud.AskLineName, EpisodeHud.AskStandingName, EpisodeHud.AskTermsName,
                                 EpisodeHud.AskLapseName, EpisodeHud.AskMoreName })
                    {
                        var label = LastLabel(name);
                        Assert.That(label, Is.Not.Null, at + ": " + name + " is drawn.");
                        Canvas.ForceUpdateCanvases();
                        Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f), at + ": " + name + "'s box.");
                        Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(12f), at + ": " + name + " at 1080p.");
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), at + ": " + name + " fits.");
                    }
                    // The frame first, so a red below still leaves it to look at.
                    if (Application.isBatchMode)
                        yield return CaptureFraming("comes-to-you-in-person" + (reduced ? "-reduced-motion" : "") + TextScales.Suffix(textSize));
                    // OFFERS-COME-TO-YOU-PLAN C.3 and test 10: at Largest, 1920x1080 - the HUD's 1600x900 canvas, where the batch
                    // screen's 4:3 one is 139 units taller - the answer controls, AskBody's Accept and Turn down, sit inside the
                    // viewport as the dialogue opens, and the dialogue is not paged - both measured on that canvas. The keyboard
                    // opens on Think it over (A.5), so the HUD reveals it, its bottom on the viewport's.
                    if (textSize == TextSize.Largest)
                    {
                        yield return AtFrame(1600, 900, () =>
                        {
                            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.NextPageCaption), Is.Null, at + " at 1920x1080: the dialogue is not paged.");
                            foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.ThinkItOverCaption })
                                AssertInsideItsViewport(FindButton(caption), at + " at 1920x1080: " + caption);
                        });
                        // Back to topics is reached by scrolling, not on another page (plan C.3: do not page): scrolled into
                        // view and pressable, the pointer hold waited out above.
                        ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption);
                    }
                }
        }

        /// <summary>
        /// A control wholly inside its scroll view's viewport, as the dialogue opens - no scrolling. Measured in the
        /// viewport's own units, as the HUD's reveal measures it, so it holds through a capture's lens too. A miss says on
        /// what screen and canvas, how far the content is scrolled, where the keyboard is and every row's height.
        /// </summary>
        private static void AssertInsideItsViewport(Button button, string what)
        {
            Canvas.ForceUpdateCanvases();
            var scroll = button.GetComponentInParent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null, what + ": the control is in a scroll view, so the check is not vacuous.");
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var box = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, button.transform);
            var view = viewport.rect;
            if (box.min.y >= view.yMin - .5f && box.max.y <= view.yMax + .5f) return;
            var root = scroll.GetComponentInParent<Canvas>()?.rootCanvas;
            var content = scroll.content;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            string rows = content == null ? "none" : string.Join(", ", content.Cast<Transform>().Where(row => row.gameObject.activeSelf)
                .Select(row => row.name + " " + ((row as RectTransform)?.rect.height ?? 0f).ToString("0")));
            Assert.Fail(what + " sits inside the viewport: " + box.min.y.ToString("0.0") + ".." + box.max.y.ToString("0.0") + " in "
                + view.yMin.ToString("0.0") + ".." + view.yMax.ToString("0.0") + " (viewport units). Screen " + Screen.width + "x" + Screen.height
                + ", canvas " + (root != null ? ((RectTransform)root.transform).rect.size + " at x" + root.scaleFactor.ToString("0.#####") : "none")
                + "; content " + (content != null ? content.rect.height.ToString("0") + " tall, scrolled " + content.anchoredPosition.y.ToString("0") : "missing")
                + " (" + scroll.verticalNormalizedPosition.ToString("0.###") + "); keyboard on " + (selected != null ? selected.name : "nothing")
                + "; rows top to bottom: " + rows + ".");
        }

        // ================================================================== slice 2: it pops up when they cannot come

        /// <summary>
        /// The invitation flow's house with asks on and walk-overs off, so every ask comes as a card where the player
        /// stands; <paramref name="threeAsks"/> adds Taylor's ask (test 15) and <paramref name="story"/> the story system,
        /// for a staged Pull. Returns as the switch takes effect, before the settle and still times could open a card.
        /// </summary>
        private IEnumerator InstallComesToYouCardHouse(bool threeAsks = false, bool story = false, params string[] holdAsideAskers)
        {
            yield return OptIntoComesToYouHouse(threeAsks, story, holdAsideAskers);
            director.AskWalkOvers = false;
            Assert.That(director.IsAskCardOpen, Is.False, "No card before the settle and still times have run.");
        }

        /// <summary>The saved house again with the story system on and nothing open, so a walk-in can be offered as a Pull (InvitationAndPull's staging).</summary>
        private void SaveWithTheStoryOn()
        {
            var store = new EpisodeSaveStore(director.SavePath);
            Assert.That(store.TryLoad(out var state, out string message), Is.True, message);
            EpisodeEngine.EnableStory(state, state.week);
            // Held at Blame by name since R7 enrolled Roles: the staging is about the walk-in's Pull, not the role moments.
            state.story.rulesVersion = StoryRules.Blame;
            Assert.That(EpisodeEngine.OpenStoryBeats(state), Is.Empty, "Nothing open: the only Pull is the walk-in a test stages.");
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            store.Save(state);
        }

        /// <summary>When <see cref="WaitForAskCardUp"/> first saw the card, in real seconds.</summary>
        private float comesCardSeenAt;

        /// <summary>
        /// Waits for an ask's card, bounded in real seconds, with nobody setting out meanwhile; says why on a miss. On the
        /// open frame: the card's way, no conversation, a panel, the ask set aside and pointer presses held off it. After
        /// 2 frames the keyboard is on Think it over.
        /// </summary>
        private IEnumerator WaitForAskCardUp(string at, float seconds = 45f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, at + ": with walk-overs off nobody sets out.");
                yield return null;
            }
            comesCardSeenAt = Time.realtimeSinceStartup;
            if (!director.IsAskCardOpen)
                Assert.Fail(at + ": no card opened. Free " + NpcRead<float>("askFreeRun") + " s, chrome-free " + NpcRead<float>("askCardSettle")
                    + " s, still " + NpcRead<float>("askStillFor") + " s, spot " + NpcRead<float>("askSpotBlockedFor") + " s, clock "
                    + NpcRead<float>("askClock") + ", next " + NpcRead<float>("askNextAt") + ", in frame "
                    + NpcRead<bool>("askPlayerInFrame") + ", panel " + director.IsPanelOpen + ". " + director.NpcAutonomyDiagnostic);
            // The open frame.
            Assert.That(ComesHud.PointerHeld, Is.True, at + ": pointer presses are held off the card as it opens.");
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Card), at);
            Assert.That(director.IsPanelOpen, Is.True, at + ": the card is a panel.");
            Assert.That(director.TalkingToId, Is.Null, at + ": no conversation.");
            Assert.That(director.IsAskSetAside(director.CurrentAskRecordId), Is.True, at + ": set aside for the phase as it opens.");
            yield return null; yield return null;
            Assert.That(SelectedName(), Is.EqualTo(EpisodeHud.ThinkItOverCaption), at + ": the keyboard opens on the way out that answers nothing.");
        }

        /// <summary>Every named label of an ask's page: a box at least 1.3x its font, the font at least 12, the copy inside the box.</summary>
        private void AssertAskLabelsFit(string at, params string[] names)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var name in names)
            {
                var label = LastLabel(name);
                Assert.That(label, Is.Not.Null, at + ": " + name + " is drawn.");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .5f), at + ": " + name + "'s box.");
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(12f), at + ": " + name + " at 1080p.");
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), at + ": " + name + " fits.");
            }
        }

        // ------------------------------------------------------------------ 11. the card opens without a walk

        [UnityTest]
        public IEnumerator ComesToYou_CardOpensWithoutAWalkAndAnswersFree() => PinnedClock(CardOpensWithoutAWalk);

        private IEnumerator CardOpensWithoutAWalk()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            string recordId = ComesToYou.Read(director.Snapshot).Single(ask => ask.askerId == ComesMaya).recordId;
            int spent = EpisodeEngine.SocialActionsSpent(director.Snapshot);
            yield return WaitForAskCardUp("the card");
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId), "The card is Maya's exact offer.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya));
            Assert.That(ComesHud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Stage), "The card stands on the Stage layout,");
            Assert.That(cameraRig.IsConversationFocused, Is.False, "and the camera frames no two-shot.");
            yield return WaitForBodies();

            var line = NpcInvitation.Available(director.Snapshot).Single(row => row.RecordId == recordId).OpeningLine;
            Assert.That(LastLabel(EpisodeHud.AskLineName)?.text, Is.EqualTo("\"" + line + "\""));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskDirectEyebrow));
            Assert.That(ShownText(), Does.Contain(EpisodeHud.AskOfferHeading));
            Assert.That(LastLabel(EpisodeHud.AskTermsName)?.text, Is.Not.Null.And.Not.Empty);
            Assert.That(LastLabel(EpisodeHud.AskLapseName)?.text, Is.EqualTo(ComesToYou.LapseNextCampaign));
            foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.ThinkItOverCaption })
                Assert.That(FindButton(caption), Is.Not.Null, caption); // Single: exactly one of each.
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.ConversationTopicsBackCaption), Is.Null, "A card is no conversation: no topics to go back to.");

            yield return WaitOutThePointerHold();
            Assert.That(ComesHud.PointerHeld, Is.False);
            var accept = ButtonWithCaption(EpisodeHud.DealAcceptCaption);
            Assert.That(ComesHud.TryDescribeOfferControl(accept, out var described), Is.True);
            Assert.That(described.OfferId, Is.EqualTo(recordId));
            Assert.That(ComesHud.TryReadOfferControl(accept, out var reading), Is.True);
            Assert.That(reading, Does.Contain(director.Snapshot.Find(ComesMaya).name).And.Contain(LastLabel(EpisodeHud.AskTermsName).text));
            Assert.That(NpcDeals.Pending(director.Snapshot).Any(row => row.id == recordId), Is.True, "Seeing the card does not answer it.");
            Assert.That(EpisodeEngine.SocialActionsSpent(director.Snapshot), Is.EqualTo(spent));

            var justBefore = director.Snapshot;
            accept.onClick.Invoke();
            var answered = director.Snapshot;
            Assert.That(answered.revision, Is.EqualTo(justBefore.revision + 1));
            Assert.That(answered.acceptedCommandIds.Count, Is.EqualTo(justBefore.acceptedCommandIds.Count + 1));
            Assert.That(EpisodeEngine.SocialActionsSpent(answered), Is.EqualTo(spent), "Answering is free.");
            var command = new EpisodeCommand
            {
                id = answered.acceptedCommandIds.Last(), actorId = justBefore.playerId,
                expectedPhase = justBefore.phase, expectedRevision = justBefore.revision,
                kind = EpisodeCommandKind.RespondToDeal, targetId = recordId, text = EpisodeEngine.AcceptDeal,
            };
            // What the conversation would have said for it, from the committed log: the card's reply page says it.
            var said = ConversationExchange.Create(justBefore, answered, command,
                answered.events.Where(entry => entry.sequence >= justBefore.nextSequence).ToList());
            Assert.That(said, Is.Not.Null, "The answer has words of its own.");
            var expected = new EpisodeEngine(justBefore).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            AssertEquivalent(expected.state, answered);

            // The reply page: the player's words, hers, and the way back to the house.
            Assert.That(director.IsAskCardOpen, Is.True, "The card stays up for the reply.");
            Assert.That(director.IsConversationExchangePlaying, Is.False, "No conversation plays it.");
            Assert.That(LastLabel(EpisodeHud.AskYourWordsName)?.text, Is.EqualTo("\"" + said.PlayerLine + "\""));
            Assert.That(LastLabel(EpisodeHud.AskReplyName)?.text, Is.EqualTo("\"" + said.NpcLine + "\""));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ThinkItOverCaption), Is.Null, "Nothing is left to think over.");
            // The ask clock stands still under the card: an answered card is a presentation too, so the gap counts from here.
            float clock = NpcRead<float>("askClock");
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            Assert.That(director.IsAskCardOpen, Is.False);
            Assert.That(director.IsPanelOpen, Is.False, "Back to the house closes the card.");
            Assert.That(NpcRead<float>("askNextAt"), Is.GreaterThanOrEqualTo(clock + EpisodeDirector.AskGapSeconds - .01f),
                "After an answered card the next ask waits its gap.");
        }

        // ------------------------------------------------------------------ 12. closing the card is Think it over

        [UnityTest]
        public IEnumerator ComesToYou_ClosingTheCardIsThinkItOver() => PinnedClock(ClosingTheCard);

        private IEnumerator ClosingTheCard()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            foreach (bool escape in new[] { true, false })
            {
                string how = escape ? "Escape" : "ClosePanels";
                yield return WaitForAskCardUp(how);
                string recordId = director.CurrentAskRecordId;
                // No whole second of the house's time comes due across the press, so a commit seen would be the card's.
                NpcWrite("npcFrameFraction", 0d);
                NpcWrite("npcWorldFraction", 0d);
                // The ask clock stands still under the card: the gap counts from here.
                float clock = NpcRead<float>("askClock");
                var before = director.Snapshot;
                byte[] bytes = File.ReadAllBytes(director.SavePath);
                if (escape) yield return PressKey(Key.Escape);
                else director.ClosePanels();
                Assert.That(director.IsAskCardOpen, Is.False, how + " closes the card.");
                Assert.That(director.IsPanelOpen, Is.False, how);
                AssertEquivalent(before, director.Snapshot);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath), how + " commits nothing.");
                Assert.That(director.IsAskSetAside(recordId), Is.True, how + ": the ask is set aside for the phase, as Think it over sets it.");
                Assert.That(WaitingOnYou.Read(director.Snapshot).items.Any(item => item.id == recordId), Is.True, how + ": the offer still waits.");
                Assert.That(NpcRead<float>("askNextAt"), Is.GreaterThanOrEqualTo(clock + EpisodeDirector.AskGapSeconds - .01f), how + ": the next ask waits its gap.");
                // The same ask brought again, for the other way out.
                NpcRead<HashSet<string>>("askSetAside").Remove(recordId);
                NpcWrite("askNextAt", NpcRead<float>("askClock"));
            }
        }

        // ------------------------------------------------------------------ 13. the card's reply narrates nothing

        [UnityTest]
        public IEnumerator ComesToYou_CardReplyLeaksNoNarration() => PinnedClock(CardReplyLeaksNothing);

        private IEnumerator CardReplyLeaksNothing()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            yield return WaitForAskCardUp("the card");
            yield return WaitOutThePointerHold();
            ButtonWithCaption(EpisodeHud.DealAcceptCaption).onClick.Invoke();
            Assert.That(LastLabel(EpisodeHud.AskReplyName), Is.Not.Null, "The card shows the reply,");
            Assert.That(NpcRead<string>("conversationNarration"), Is.Null, "and narrates nothing a conversation would show next.");
            ButtonWithCaption(EpisodeDirector.SceneCardDoneCaption).onClick.Invoke();
            Assert.That(director.IsPanelOpen, Is.False);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (NpcWholeSecondDue() && Time.realtimeSinceStartup < deadline) yield return null;
            yield return OpenNearbyNpc(SceneComponents<HouseNpc>().Single(npc => npc.Id == ComesMaya));
            Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya));
            yield return null;
            Assert.That(ShownText(), Does.Not.Contain("WHAT HAPPENED"), "The next conversation opens with nothing of the card's answer.");
        }

        // ------------------------------------------------------------------ 14. never over chrome

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_NeverOverChrome() => PinnedClock(NeverOverChrome);

        private IEnumerator NeverOverChrome()
        {
            yield return InstallComesToYouCardHouse(false, false, ComesJamie);
            string recordId = ComesToYou.Read(director.Snapshot).Single(ask => ask.askerId == ComesMaya).recordId;
            // Every pass brings Maya's ask again: the caps would stop the fourth.
            NpcWrite("askRecordCap", 100); NpcWrite("askPhaseCap", 100); NpcWrite("askWeekCap", 100);
            // The control: due, with nothing up, the card opens at the next scan.
            yield return HoldTheCardAtItsGap("nothing up", recordId);
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            yield return WaitForAskCardUp("nothing up", EpisodeDirector.AskScanSeconds + 2f);
            director.ClosePanels();
            yield return null;

            var home = player.transform.position;
            yield return CardHeldUnder("the settings", recordId, () => director.OpenSettings(), () => director.IsPanelOpen,
                () => director.ClosePanels(), true);
            yield return CardHeldUnder("the notebook", recordId, () => director.OpenJournal(), () => director.IsPanelOpen,
                () => director.ClosePanels(), true);
            yield return CardHeldUnder("the episode screen", recordId,
                () => { WarpPlayer(director.StationPosition); Assert.That(director.TryOpenPhasePanel(), Is.True); },
                () => director.IsPhasePanelOpen, () => { director.ClosePanels(); WarpPlayer(home); }, true);
            yield return CardHeldUnder("the emote menu", recordId, () => director.ToggleEmoteMenu(), () => director.EmoteMenuOpen,
                () => director.ToggleEmoteMenu(), true);
            // The map-only overview holds no new visit: the free-house clock and the scan run beneath it, so only the card's
            // own gate holds the card there, and the card waits its settle time once the overview has closed.
            yield return CardHeldUnder("the overview", recordId, () => Assert.That(director.ShowOverview(), Is.True), () => director.IsOverview,
                () => director.EndOverview(), true, scanMustRun: true);

            // A staged Pull, on the same house with the story on (InvitationAndPull's staging): a walk-in found as the
            // proximity watch finds one, which writes nothing.
            yield return InstallComesToYouCardHouse(false, true, ComesJamie);
            recordId = ComesToYou.Read(director.Snapshot).Single(ask => ask.askerId == ComesMaya).recordId;
            Assert.That(director.IsOverview || director.IsBriefing, Is.False, "The house's own view, where the Pull's card is built.");
            var now = director.Snapshot;
            Assert.That(EpisodeEngine.StoryOn(now), Is.True, "A walk-in is offered as a Pull only under the story system.");
            var pair = now.Active.Where(actor => !actor.isPlayer && actor.id != ComesMaya).Select(actor => actor.id)
                .OrderBy(id => id, StringComparer.Ordinal).Take(2).ToList();
            Assert.That(pair, Has.Count.EqualTo(2), "Two others to walk in on.");
            yield return CardHeldUnder("a staged Pull", recordId, () =>
                {
                    NpcWrite("walkInFirst", pair[0]);
                    NpcWrite("walkInSecond", pair[1]);
                    NpcWrite("walkInRoom", "Living");
                    NpcWrite("walkInWeek", now.week);
                    NpcWrite("proximityWatch", -1e6f);
                }, () => ActiveRect(EpisodeHud.PullCardName) != null,
                () => FindButton(EpisodeHud.StayOutCaption).onClick.Invoke(), true);
        }

        /// <summary>The house brought to the moment the card would open: the ask free to come again and the settle and still times run, only its gap holding it.</summary>
        private IEnumerator HoldTheCardAtItsGap(string what, string recordId)
        {
            NpcRead<HashSet<string>>("askSetAside").Remove(recordId);
            NpcWrite("askNextAt", float.MaxValue);
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((NpcRead<float>("askFreeRun") < EpisodeDirector.AskSettleSeconds || NpcRead<float>("askStillFor") < EpisodeDirector.AskStillSeconds)
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(NpcRead<float>("askFreeRun"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds), what + ": the house has settled.");
            Assert.That(NpcRead<float>("askStillFor"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskStillSeconds), what + ": the player stands still.");
            Assert.That(director.IsAskCardOpen, Is.False, what);
        }

        /// <summary>
        /// The card due but for its gap, then the chrome up and the gap let go: for 30 frames, and at least the settle,
        /// still and two scans' time in real seconds, no card. Once the chrome closes the card opens - after a panel, or
        /// anything else that holds the house, only once the settle time has run again. With
        /// <paramref name="scanMustRun"/>, the house stays free under the chrome - the player in frame, the free-house
        /// clock settled, the scan running and reaching the card - so only the card's own gate holds it.
        /// </summary>
        private IEnumerator CardHeldUnder(string what, string recordId, Action open, Func<bool> up, Action close, bool settlesAgain,
            bool scanMustRun = false)
        {
            yield return HoldTheCardAtItsGap(what, recordId);
            open();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!up() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(up(), Is.True, what + " is up.");
            // Only the chrome holds the card now.
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            float scanWas = NpcRead<float>("askScanAt");
            int frames = 0;
            float until = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + 2f * EpisodeDirector.AskScanSeconds;
            while (frames < 30 || Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames++;
                Assert.That(director.IsAskCardOpen, Is.False, "No card over " + what + " (frame " + frames + ").");
                Assert.That(up(), Is.True, what + " stays up while it is watched (frame " + frames + ").");
                if (scanMustRun)
                    Assert.That(NpcRead<bool>("askPlayerInFrame"), Is.True, "Under " + what + " the player stays in frame (frame " + frames + ").");
            }
            if (scanMustRun)
            {
                Assert.That(NpcRead<float>("askFreeRun"), Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds), "Under " + what + " the house stays free.");
                Assert.That(NpcRead<float>("askScanAt"), Is.GreaterThan(scanWas), "Under " + what + " the scan runs and reaches the card.");
            }
            close();
            float closed = Time.realtimeSinceStartup;
            yield return WaitForAskCardUp("once " + what + " has closed", 20f);
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(recordId), what);
            if (settlesAgain)
                Assert.That(comesCardSeenAt - closed, Is.GreaterThanOrEqualTo(EpisodeDirector.AskSettleSeconds - EpisodeDirector.AskFrameAllowanceSeconds),
                    "After " + what + " the card waits the settle time again.");
            director.ClosePanels();
            yield return null;
        }

        // ------------------------------------------------------------------ 15. several asks, one at a time, in public order

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_SeveralAsksOneAtATimeInPublicOrder() => PinnedClock(SeveralAsksInOrder);

        private IEnumerator SeveralAsksInOrder()
        {
            // Three houseguests ask, so three cards: one at a time, in the queue's public order, a gap apart on the free
            // house's clock, each saying how many others still want a word.
            yield return InstallComesToYouCardHouse(threeAsks: true);
            var queue = ComesToYou.Read(director.Snapshot);
            Assert.That(queue.Select(ask => ask.askerId).Distinct().Count(), Is.EqualTo(3),
                "Three houseguests ask: " + string.Join(", ", queue.Select(ask => ask.askerId + "/" + ask.type)));
            var order = queue.Select(ask => ask.recordId).ToList();
            float openedAt = float.NaN;
            for (int i = 0; i < 3; i++)
            {
                string at = "card " + (i + 1);
                yield return WaitForAskCardUp(at, 60f);
                Assert.That(director.CurrentAskRecordId, Is.EqualTo(order[i]), at + " is the " + (i + 1) + ". in public order.");
                // The clock stands still under the card: this is when it opened.
                float clock = NpcRead<float>("askClock");
                if (i > 0) Assert.That(clock - openedAt, Is.GreaterThanOrEqualTo(EpisodeDirector.AskGapSeconds - .01f), at + " came a gap after the last.");
                openedAt = clock;
                if (i < 2) Assert.That(LastLabel(EpisodeHud.AskMoreName)?.text, Is.EqualTo(EpisodeHud.AskMoreWaitingLine(2 - i)), at + ": who else wants a word.");
                else Assert.That(LastLabel(EpisodeHud.AskMoreName), Is.Null, at + ": nobody else waits.");
                yield return WaitOutThePointerHold();
                ButtonWithCaption(EpisodeHud.ThinkItOverCaption).onClick.Invoke();
                Assert.That(director.IsAskCardOpen, Is.False, at + ": Think it over closes the card.");
                Assert.That(director.IsAskSetAside(order[i]), Is.True, at);
            }

            // With the phase's cap at 2, the third asker's card does not open this phase.
            yield return InstallComesToYouCardHouse(threeAsks: true);
            NpcWrite("askPhaseCap", 2);
            order = ComesToYou.Read(director.Snapshot).Select(ask => ask.recordId).ToList();
            for (int i = 0; i < 2; i++)
            {
                string at = "capped card " + (i + 1);
                yield return WaitForAskCardUp(at, 45f);
                Assert.That(director.CurrentAskRecordId, Is.EqualTo(order[i]), at);
                if (i == 1) Assert.That(LastLabel(EpisodeHud.AskMoreName), Is.Null, at + ": the phase's cap is reached, so nobody else comes this phase.");
                yield return WaitOutThePointerHold();
                ButtonWithCaption(EpisodeHud.ThinkItOverCaption).onClick.Invoke();
                // The gap is the first half's subject: the next scan may run at once.
                NpcWrite("askNextAt", NpcRead<float>("askClock"));
            }
            float start = NpcRead<float>("askClock");
            float need = EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds + 3 * EpisodeDirector.AskScanSeconds;
            float until = Time.realtimeSinceStartup + 20f;
            while (NpcRead<float>("askClock") < start + need && Time.realtimeSinceStartup < until)
            {
                Assert.That(director.IsAskCardOpen, Is.False, "The third asker's card does not open in this phase.");
                yield return null;
            }
            Assert.That(NpcRead<float>("askClock"), Is.GreaterThanOrEqualTo(start + need), "The ask clock ran: the wait was not vacuous.");
            Assert.That(director.IsAskCardOpen, Is.False);
            // The phase moves on. A real Advance from this house's free time reaches the Head of Household competition,
            // where no ask comes (NpcInvitation.PhaseAllows), and then the nominations, where without the week's windows
            // only those deciding can talk; so the ask model's own phase key is moved on, which is what its caps count
            // against. The two asks already put once stay out under a cap of one each, so only the third can come.
            NpcWrite("askRecordCap", 1);
            NpcWrite("askPhase", EpisodePhase.HoH);
            yield return WaitForAskCardUp("after the phase moves on", 20f);
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(order[2]), "The third asker's card opens in the next phase.");
            Assert.That(NpcRead<int>("askPhaseShown"), Is.EqualTo(1), "The phase's count started again.");
        }

        // ------------------------------------------------------------------ 16. captures of the card

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_CapturesTheCardAtEachSize() => PinnedClock(CapturesTheCard);

        private IEnumerator CapturesTheCard()
        {
            foreach (var textSize in new[] { TextSize.Standard, TextSize.Largest })
            {
                string at = textSize.ToString();
                // Nobody held aside: one card at a time, so the second asker is the one more who wants a word.
                yield return InstallComesToYouCardHouse();
                string first = ComesToYou.Read(director.Snapshot).First().askerId;
                yield return ApplyTextSize(textSize);
                yield return WaitForAskCardUp(at);
                Assert.That(director.CurrentAskerId, Is.EqualTo(first), at + ": the first in public order.");
                Assert.That(ComesHud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Stage), at);
                yield return WaitForBodies();
                yield return WaitOutThePointerHold();
                Assert.That(LastLabel(EpisodeHud.AskMoreName)?.text, Is.EqualTo(EpisodeHud.AskMoreWaitingLine(1)),
                    at + ": the other asker is the one more who wants a word.");
                AssertEveryLabelDraws(at + ": the ask card");
                AssertAskLabelsFit(at + ": the ask card", EpisodeHud.AskLineName, EpisodeHud.AskStandingName, EpisodeHud.AskTermsName,
                    EpisodeHud.AskLapseName, EpisodeHud.AskMoreName);
                // The frame first, so a red below still leaves it to look at; then, as for the dialogue (test 10, plan C.3), the
                // answer controls inside the viewport at Largest on the 1920x1080 canvas, not the batch screen's taller 4:3 one.
                if (Application.isBatchMode) yield return CaptureFraming("comes-to-you-card" + TextScales.Suffix(textSize));
                if (textSize == TextSize.Largest)
                    yield return AtFrame(1600, 900, () =>
                    {
                        foreach (var caption in new[] { EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption, EpisodeHud.ThinkItOverCaption })
                            AssertInsideItsViewport(FindButton(caption), at + " at 1920x1080: " + caption);
                    });

                // The reply page's two lines, at the same size. Test 11 presses as a player does; here only the page matters.
                Assert.That(director.IsAskCardOpen, Is.True, at + ": the card is still up for its answer.");
                FindButton(EpisodeHud.DealAcceptCaption).onClick.Invoke();
                yield return null;
                Assert.That(FindButton(EpisodeDirector.SceneCardDoneCaption), Is.Not.Null, at + ": the reply page's way back.");
                AssertEveryLabelDraws(at + ": the card's reply");
                AssertAskLabelsFit(at + ": the card's reply", EpisodeHud.AskYourWordsName, EpisodeHud.AskReplyName);
                director.ClosePanels();
                yield return null;
            }
        }

        // ------------------------------------------------------------------ 17. a spot that fits no visitor

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_ASpotThatFitsNoVisitorBringsTheFirstAskAsACard() => PinnedClock(ASpotThatFitsNoVisitor);

        /// <summary>
        /// The player stands still where no visitor can stand beside them: the spot is charged to no ask - nothing missed,
        /// out of reach or set aside, nobody setting out - until it has refused visitors for the card's time, when the
        /// first listed ask comes as a card. Back on open floor the spot's count starts again and the next ask is walked
        /// over: the spot made no other ask card-due. Nobody pairs off or takes a seat meanwhile, so every refusal is the
        /// spot's and never an asker's own.
        /// </summary>
        private IEnumerator ASpotThatFitsNoVisitor()
        {
            // Pairing held from the bodies' assembly on, so no director Update runs with it live before the hold below.
            yield return OptIntoComesToYouHouse(false, false, Array.Empty<string>(), holdPairing: true);
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            float ambient = NpcRead<float>("nextAmbientActivity");
            NpcWrite("nextAmbientActivity", float.PositiveInfinity);
            try { yield return SpotThatFitsNoVisitor(); }
            finally
            {
                NpcWrite("npcApproachDiagnosticsSuppressed", false);
                NpcWrite("nextAmbientActivity", ambient);
            }
        }

        private IEnumerator SpotThatFitsNoVisitor()
        {
            var queue = ComesToYou.Read(director.Snapshot);
            var first = queue.First();
            Assert.That(first.askerId, Is.EqualTo(ComesJamie), "Jamie's ask is listed first.");
            var second = queue.First(ask => ask.askerId == ComesMaya);
            string[] askers = { ComesJamie, ComesMaya };
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var home = player.transform.position;

            // The scan held while the player moves there and both askers are seen free, so no wait of the test's is charged.
            NpcWrite("askNextAt", float.MaxValue);
            WarpPlayer(SpotNoVisitorFits(coordinator));
            var refusals = new Dictionary<string, HouseNpcInvitationRefusal>();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (true)
            {
                foreach (var asker in askers)
                {
                    Assert.That(coordinator.TryReserveNpcInvitation(asker, player, out _, out _, out var refusal), Is.False,
                        asker + ": no visitor fits the spot.");
                    refusals[asker] = refusal;
                }
                if (refusals.Values.All(kind => kind == HouseNpcInvitationRefusal.PlayerSpot) || Time.realtimeSinceStartup >= deadline) break;
                yield return null;
            }
            foreach (var asker in askers)
                Assert.That(refusals[asker], Is.EqualTo(HouseNpcInvitationRefusal.PlayerSpot), asker + " is refused for the player's spot, not for their own reasons.");
            var unreachable = NpcRead<Dictionary<string, float>>("askUnreachable");
            Assert.That(unreachable.Values.Where(seconds => seconds > 0f), Is.Empty, "Nothing charged before the scan runs.");
            NpcWrite("askNextAt", NpcRead<float>("askClock"));

            // Charged to nobody until the spot has refused visitors for the card's time.
            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + EpisodeDirector.AskUnreachableSecondsBeforeCard + 10f;
            while (!director.IsAskCardOpen && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(director.CurrentNpcInvitationRecordId, Is.Null, "Nobody sets out to a spot no visitor fits.");
                Assert.That(unreachable.Values.Where(seconds => seconds > 0f), Is.Empty, "The spot is charged to no asker.");
                foreach (var ask in new[] { first, second })
                {
                    Assert.That(director.AskMissCount(ask.recordId), Is.Zero, ask.askerId + ": no miss.");
                    Assert.That(director.IsAskSetAside(ask.recordId), Is.False, ask.askerId + ": nothing set aside.");
                }
                yield return null;
            }
            float spot = NpcRead<float>("askSpotBlockedFor");
            if (!director.IsAskCardOpen)
                Assert.Fail("The spot's card never opened: spot " + spot + " s, free " + NpcRead<float>("askFreeRun") + " s, still "
                    + NpcRead<float>("askStillFor") + " s, in frame " + NpcRead<bool>("askPlayerInFrame") + ". " + AskerReport(first.askerId)
                    + " " + comesSwitchReport);
            Assert.That(spot, Is.GreaterThanOrEqualTo(EpisodeDirector.AskUnreachableSecondsBeforeCard), "No card before the spot has held for the card's time.");
            Assert.That(director.CurrentAskRecordId, Is.EqualTo(first.recordId), "The first listed ask comes as the card.");
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesJamie));
            Assert.That(unreachable.Values.Where(seconds => seconds > 0f), Is.Empty, "Still charged to nobody.");
            Assert.That(director.IsAskSetAside(second.recordId), Is.False, "Only the card's ask is set aside.");
            yield return WaitForAskCardUp("the spot's card", 1f);

            // Back on open floor: the spot's count starts again with the stillness, and the next ask is walked over.
            director.ClosePanels();
            WarpPlayer(home);
            yield return null;
            Assert.That(NpcRead<float>("askSpotBlockedFor"), Is.Zero, "The spot's count starts again where the player stands now.");
            NpcWrite("askNextAt", NpcRead<float>("askClock"));
            deadline = Time.realtimeSinceStartup + EpisodeDirector.AskSettleSeconds + EpisodeDirector.AskStillSeconds
                + 2f * EpisodeDirector.AskScanSeconds + 3f;
            while (director.CurrentNpcInvitationRecordId == null && !director.IsAskCardOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsAskCardOpen, Is.False, "The spot made no other ask card-due.");
            Assert.That(director.CurrentNpcInvitationRecordId, Is.EqualTo(second.recordId), "Maya sets out to the player's open floor: "
                + AskerReport(second.askerId) + " " + comesSwitchReport);
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya));
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.None), "A walk-over on its way.");
            Assert.That(NpcRead<float>("askSpotBlockedFor"), Is.Zero);
        }

        /// <summary>
        /// The floor point nearest the player, their head well inside the frame, that lies in no room's safe interior - a
        /// doorway, a floor's edge - so the coordinator refuses every visitor for the player's spot before it looks for a
        /// point beside them (HouseMeetingCoordinator.TryReserveNpcInvitation's PlayerSpot).
        /// </summary>
        private Vector3 SpotNoVisitorFits(HouseMeetingCoordinator coordinator)
        {
            var field = typeof(HouseMeetingCoordinator).GetField("rooms", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "The coordinator's room query.");
            var rooms = (HouseRoomQuery)field.GetValue(coordinator);
            var view = cameraRig.ViewCamera;
            var from = player.transform.position;
            float radius = player.Agent.radius, best = float.PositiveInfinity;
            Vector3 found = from;
            for (float x = -14f; x <= 14f; x += .2f)
                for (float z = -14f; z <= 14f; z += .2f)
                {
                    var candidate = from + new Vector3(x, 0f, z);
                    float distance = new Vector2(x, z).sqrMagnitude;
                    if (distance >= best || !NavMesh.SamplePosition(candidate, out var hit, .1f, player.Agent.areaMask)
                        || Mathf.Abs(hit.position.y - from.y) > .2f || rooms.TryLocate(hit.position, radius, out _)) continue;
                    var seen = view.WorldToViewportPoint(hit.position + Vector3.up * 1.7f);
                    if (seen.z <= 0f || seen.x <= .15f || seen.x >= .85f || seen.y <= .15f || seen.y >= .85f) continue;
                    best = distance;
                    found = hit.position;
                }
            Assert.That(best, Is.LessThan(float.PositiveInfinity), "A floor point in frame that fits no visitor: a doorway or a floor's edge.");
            return found;
        }

        // ================================================================== slice 3: the walk-over holds

        /// <summary>How far a heading is from the bearing between two points, in degrees; infinite for no heading at all.</summary>
        private static float OffBearing(float facing, Vector3 from, Vector3 to)
        {
            if (float.IsNaN(facing)) return float.PositiveInfinity;
            var toward = to - from;
            return Mathf.Abs(Mathf.DeltaAngle(facing, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg));
        }

        /// <summary>
        /// Waits for the asker's line to be up, bounded in real seconds, holding the dialogue's own opening back; says why on
        /// a miss, about the asker expected - the visit's own may be none by then.
        /// </summary>
        private IEnumerator WaitForTheLineHeldBack(string at, string askerId, float seconds = 35f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("askSpokeFor", 0f);
                yield return null;
            }
            NpcWrite("askSpokeFor", 0f);
            if (!director.NpcInvitationIsVisible) Assert.Fail(at + ": the asker's line never came up. " + AskerReport(askerId));
            Assert.That(director.TalkingToId, Is.Null, at + ": the dialogue is held back.");
        }

        /// <summary>The bubble's four labels - the name, the line and the two captions - read off its panel.</summary>
        private static TMP_Text[] BubbleLabels(HouseNpcInvitationBubble bubble) =>
            bubble.HearControl.transform.parent.GetComponentsInChildren<TMP_Text>(true);

        [UnityTest]
        public IEnumerator ComesToYou_TheAskerAndThePlayerTurnToEachOther() => PinnedClock(TheyTurnToEachOtherHeld);

        /// <summary>
        /// On a house whose pairing and ambient seats are held: the subject is the two headings, and a missed walk followed
        /// to its retry leaves Maya with no visit in flight meanwhile, so a house free to pair could take her (plan C.2's
        /// busy set) and her ask would come as a card. The house's own presentation, which the test watches, runs on.
        /// </summary>
        private IEnumerator TheyTurnToEachOtherHeld() => WithTheHouseHeld(() => InstallComesToYouHouse(true, ComesJamie), TheyTurnToEachOther);

        /// <summary>
        /// The director's visit answers from arrival: it sets out on its route's budget, and its answer window starts
        /// again as the asker gets there. The asker turns to the player as they arrive, and the house's own presentation
        /// leaves that heading be while their line is up; as the dialogue opens the player turns to them and they stay
        /// turned to the player while it is up; closing lets go of both headings, which were the ask's.
        /// </summary>
        private IEnumerator TheyTurnToEachOther()
        {
            string askerId = director.CurrentAskerId;
            var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == askerId);
            var theirs = asker.GetComponent<CharacterPresentation>();
            var mine = player.GetComponent<CharacterPresentation>();
            Assert.That(theirs != null && mine != null, Is.True, "Both have a body to turn.");

            // Reserved this frame, so its expiry is still the walk's budget: never more than the most a walk gets. A
            // missed walk retried is followed to its own visit and budget.
            var meetings = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var visit = NpcRead<HouseNpcInvitationLease>("askVisit");
            Assert.That(visit, Is.Not.Null, "The visit is the director's.");
            float sent = meetings.NpcInvitationExpiresAt(visit);
            Assert.That(sent - Time.unscaledTime, Is.GreaterThan(0f).And.LessThanOrEqualTo(HouseMeetingCoordinator.NpcInvitationTravelMax + .01f),
                "The walk sets out on its route's budget.");
            float lineDeadline = Time.realtimeSinceStartup + 35f;
            while (!director.NpcInvitationIsVisible && Time.realtimeSinceStartup < lineDeadline)
            {
                NpcWrite("askSpokeFor", 0f);
                var now = NpcRead<HouseNpcInvitationLease>("askVisit");
                if (now != null && !ReferenceEquals(now, visit)) { visit = now; sent = meetings.NpcInvitationExpiresAt(now); }
                yield return null;
            }
            // Waited above: this only holds the open back and says why on a miss.
            yield return WaitForTheLineHeldBack("the asker's arrival", askerId, 0f);
            Assert.That(NpcRead<HouseNpcInvitationLease>("askVisit"), Is.SameAs(visit), "The line is the visit followed.");
            float window = meetings.NpcInvitationExpiresAt(visit);
            Assert.That(window, Is.Not.NaN, "The visit is still the one in flight.");
            Assert.That(window, Is.Not.EqualTo(sent), "The answer window starts again as the asker arrives; the walk's budget ran to " + sent + ".");
            Assert.That(window - Time.unscaledTime, Is.LessThanOrEqualTo(HouseMeetingCoordinator.NpcInvitationLifetime + .01f),
                "It runs its lifetime from the arrival.");

            Assert.That(OffBearing(theirs.FacingYaw, asker.transform.position, player.transform.position), Is.LessThan(20f),
                "On arrival the asker turns to the player: heading " + theirs.FacingYaw + ".");

            // The house's own presentation runs at 10 Hz meanwhile, and leaves the asker's heading be.
            double freeBefore = NpcRead<double>("npcFreeSeconds");
            float until = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < until) { NpcWrite("askSpokeFor", 0f); yield return null; }
            Assert.That(NpcRead<double>("npcFreeSeconds") - freeBefore, Is.GreaterThan(.2d), "The house's own presentation ran meanwhile.");
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The line is still up.");
            Assert.That(OffBearing(theirs.FacingYaw, asker.transform.position, player.transform.position), Is.LessThan(20f),
                "The house leaves the asker's heading be while their line is up: heading " + theirs.FacingYaw + ".");
            Assert.That(mine.FacingYaw, Is.NaN, "The player is not turned before the dialogue opens.");

            yield return WaitForAskDialogue(askerId);
            Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
            Assert.That(OffBearing(mine.FacingYaw, player.transform.position, asker.transform.position), Is.LessThan(20f),
                "As the dialogue opens the player turns to the asker: heading " + mine.FacingYaw + ".");
            Assert.That(OffBearing(theirs.FacingYaw, asker.transform.position, player.transform.position), Is.LessThan(20f),
                "and the asker stays turned to the player.");

            // The house goes on under the dialogue - its per-frame cancel of a visit already gone among it - and the
            // headings stay the ones the open gave until the panel closes.
            yield return null; yield return null;
            Assert.That(director.TalkingToId, Is.EqualTo(askerId), "The dialogue is still up.");
            float playerYaw = NpcRead<float>("askPlayerYaw"), askerYaw = NpcRead<float>("askAskerYaw");
            Assert.That(playerYaw, Is.Not.NaN, "The heading the open gave the player is still the ask's.");
            Assert.That(askerYaw, Is.Not.NaN, "and so is the asker's.");
            Assert.That(mine.FacingYaw, Is.EqualTo(playerYaw), "The player still faces the asker.");
            Assert.That(theirs.FacingYaw, Is.EqualTo(askerYaw), "The asker still faces the player.");

            director.ClosePanels();
            Assert.That(mine.FacingYaw, Is.NaN, "Closing lets go of the heading the ask gave the player,");
            Assert.That(theirs.FacingYaw, Is.NaN, "and of the asker's.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ComesToYou_AStandingTalkSpotKeepsThePlayersFacing() => PinnedClock(AStandingTalkSpotKeepsFacing);

        /// <summary>
        /// With asks on, Talk takes the player and an asker to a standing place, where each turns to the other before the
        /// conversation opens on her ask: the open's ClosePanels lets go only of a heading an ask gave, and the ask gives
        /// none over the talk spot's, so the player still faces her. Every seat is set aside for the test, so the place is
        /// a standing one; no visit sets out, and nobody pairs off onto the standing marks meanwhile.
        /// </summary>
        private IEnumerator AStandingTalkSpotKeepsFacing()
        {
            // Pairing held from the bodies' assembly on, so no director Update runs with it live before the hold below.
            yield return OptIntoComesToYouHouse(false, false, Array.Empty<string>(), holdPairing: true);
            NpcWrite("askNextAt", float.MaxValue);
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            var seats = SceneComponents<HouseInteractionAnchor>().Where(anchor => anchor.enabled && anchor.Seated).ToList();
            try
            {
                foreach (var seat in seats) seat.enabled = false;
                StandInARoomWithAStandingPlace();
                AskForTheTalkSpots();
                var asker = SceneComponents<HouseNpc>().Single(npc => npc.Id == ComesMaya);
                director.TalkFromCastMenu(ComesMaya);
                yield return null;
                var spot = director.CurrentTalkSpot;
                Assert.That(spot, Is.Not.Null, "Talk sends the two to a place: " + NpcRead<HouseMeetingCoordinator>("npcMeetings").LastFailure);
                Assert.That(spot.Seated, Is.False, "A standing place: every seat is set aside.");
                float deadline = Time.realtimeSinceStartup + 45f;
                while (director.TalkingToId == null && director.CurrentTalkSpot == spot && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.TalkingToId, Is.EqualTo(ComesMaya), "The conversation opens once both are there. The place is "
                    + (director.CurrentTalkSpot == spot ? "still held" : "let go") + ", the walk is to " + (director.WalkingToId ?? "nobody") + ".");
                Assert.That(director.CurrentTalkSpot, Is.SameAs(spot), "It opened at the place.");
                Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.Raised),
                    "It opens on her ask, so the ask's own turning ran with asks on.");
                var mine = player.GetComponent<CharacterPresentation>();
                Assert.That(OffBearing(mine.FacingYaw, player.transform.position, asker.transform.position), Is.LessThan(20f),
                    "The talk spot's heading survives the open: heading " + mine.FacingYaw + ".");
                Assert.That(NpcRead<float>("askPlayerYaw"), Is.NaN, "The ask gave the player no heading over the talk spot's.");
                director.ClosePanels();
                yield return null;
            }
            finally
            {
                foreach (var seat in seats) if (seat != null) seat.enabled = true;
                NpcWrite("npcApproachDiagnosticsSuppressed", false);
            }
        }

        /// <summary>
        /// The player moved, where needed, to stand beside a standing place to talk - a standing meeting venue's mark or a
        /// standing talk spot's - in its room, so Talk has a standing place in the player's room as well as wherever the
        /// houseguest is.
        /// </summary>
        private void StandInARoomWithAStandingPlace()
        {
            var standingVenues = new HashSet<string>(HouseInteractionAnchors.Meetings.Where(venue => !venue.Seated).Select(venue => venue.Id));
            var marks = SceneComponents<HouseInteractionAnchor>().Where(anchor => anchor.isActiveAndEnabled && !anchor.Posed && anchor.VenueId != null
                && (standingVenues.Contains(anchor.VenueId) || anchor.VenueId.StartsWith(HouseConversationSpots.Prefix))).ToList();
            Assert.That(marks, Is.Not.Empty, "The house has standing places to talk.");
            Assert.That(HouseRoomQuery.TryCreate(player.gameObject.scene, out var rooms, out var why), Is.True, why);
            rooms.TryLocate(player.transform.position, player.Agent.radius, out var yourRoom);
            if (marks.Any(mark => mark.RoomId == yourRoom)) return;
            foreach (var mark in marks.OrderBy(mark => (mark.Approach - player.transform.position).sqrMagnitude))
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4f;
                    var candidate = mark.Approach + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.6f;
                    if (!NavMesh.SamplePosition(candidate, out var hit, .4f, player.Agent.areaMask)) continue;
                    if (!rooms.TryLocate(hit.position, player.Agent.radius, out var room) || room != mark.RoomId) continue;
                    WarpPlayer(hit.position);
                    return;
                }
            Assert.Fail("No floor beside a standing place to talk.");
        }

        [UnityTest]
        public IEnumerator ComesToYou_TheLineIsLocalisedAndCurrentTextKeepsItsRawWords() => PinnedClock(TheLineIsLocalised);

        /// <summary>
        /// The asker's bubble goes through the localisation layer - their name, their line and the two captions - while
        /// CurrentText stays the raw line and each control keeps its English caption as its name, so the same two
        /// controls hear and put off the ask.
        /// </summary>
        private IEnumerator TheLineIsLocalised()
        {
            yield return InstallComesToYouHouse(ComesJamie);
            string recordId = director.CurrentNpcInvitationRecordId, askerId = director.CurrentAskerId;
            string line = ComesToYou.Read(director.Snapshot).Single(ask => ask.recordId == recordId).line;
            string name = director.Snapshot.Find(askerId).name;
            string language = Localisation.Language;
            const string Mark = "[translated] ";
            try
            {
                Localisation.Use("test", new Dictionary<string, string>
                {
                    { line, Mark + line }, { name, Mark + name },
                    { HouseNpcInvitationBubble.HearCaption, Mark + "hear" }, { HouseNpcInvitationBubble.NotNowCaption, Mark + "not now" },
                });
                // A line already up before the table is read again at the bubble's next 10 Hz show.
                TMP_Text Shown(HouseNpcInvitationBubble on) => BubbleLabels(on).Single(label => label.name == HouseNpcInvitationBubble.LineName);
                float deadline = Time.realtimeSinceStartup + 35f;
                HouseNpcInvitationBubble bubble = null;
                while (Time.realtimeSinceStartup < deadline)
                {
                    NpcWrite("askSpokeFor", 0f);
                    bubble = NpcRead<HouseNpcInvitationBubble>("npcInvitationBubble");
                    if (director.NpcInvitationIsVisible && bubble != null && Shown(bubble).text == Mark + line) break;
                    yield return null;
                }
                NpcWrite("askSpokeFor", 0f);
                Assert.That(director.NpcInvitationIsVisible, Is.True, "The asker's line is up.");
                Assert.That(bubble, Is.Not.Null);
                Assert.That(Shown(bubble).text, Is.EqualTo(Mark + line), "The player reads the localised line.");
                Assert.That(bubble.CurrentText, Is.EqualTo(line), "CurrentText is the raw line, never its translation.");
                Assert.That(BubbleLabels(bubble).Single(label => label.name == "Invitation speaker").text, Is.EqualTo(Mark + name),
                    "and the localised name.");
                var hear = bubble.HearControl;
                Assert.That(hear.name, Is.EqualTo(HouseNpcInvitationBubble.HearCaption), "The control keeps its English caption as its name,");
                Assert.That(bubble.NotNowControl.name, Is.EqualTo(HouseNpcInvitationBubble.NotNowCaption));
                Assert.That(hear.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(Mark + "hear"), "and shows the localised one.");
                Assert.That(bubble.NotNowControl.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(Mark + "not now"));
                Canvas.ForceUpdateCanvases();
                foreach (var label in BubbleLabels(bubble)) Assert.That(label.isTextOverflowing, Is.False, label.name + " fits its localised words.");

                hear.onClick.Invoke();
                Assert.That(director.TalkingToId, Is.EqualTo(askerId), "The same control hears the ask.");
                Assert.That(director.CurrentAskWay, Is.EqualTo(EpisodeDirector.AskWay.WalkOver));
                Assert.That(director.InvitedDealRecordId, Is.EqualTo(recordId));
            }
            finally { Localisation.Load(language); }
        }

        [UnityTest]
        public IEnumerator ComesToYou_TheLinesBoxesFitAtLargest() => PinnedClock(TheLinesBoxesFit);

        /// <summary>
        /// At Largest the asker's bubble draws every label - the name, the line and both captions - in a box at least 1.3x
        /// its font, the copy inside it, and places it on the view the player sees.
        /// </summary>
        private IEnumerator TheLinesBoxesFit()
        {
            SaveNpcInvitationFlowState();
            yield return ReloadEpisode();
            // Largest first, chosen as a player chooses it, before asks are on (the reload restores the default size): the
            // settings panel then cancels no visit, and the line is the house's own first walk-over. Its flush plans no
            // pairing. A visit the settings cancel hands its asker back to a house free to pair her before she can be
            // asked again - Maya, the one asker not held aside - and a busy asker's ask comes as a card, not a line.
            NpcWrite("proximityWatch", -1e6f);
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            yield return ApplyTextSize(TextSize.Largest);
            Assert.That(director.TextSize, Is.EqualTo(TextSize.Largest), "The size took, so this pass is not vacuous.");
            yield return WithTheHouseHeld(() => TurnAsksOnInTheLoadedHouse(new[] { ComesJamie }, holdPairing: true), TheLinesBoxesFitHeld);
        }

        private IEnumerator TheLinesBoxesFitHeld()
        {
            yield return WaitForTheLineHeldBack("at Largest", ComesMaya, 35f);
            Assert.That(director.CurrentAskerId, Is.EqualTo(ComesMaya), "The line is Maya's, the one asker not held aside.");
            foreach (var id in HeldAside(new[] { ComesJamie })) Assert.That(director.IsAskSetAside(id), Is.True, id + " is still held aside.");
            Assert.That(director.TextSize, Is.EqualTo(TextSize.Largest), "and still at Largest.");
            var bubble = NpcRead<HouseNpcInvitationBubble>("npcInvitationBubble");
            Assert.That(bubble.ViewCamera, Is.SameAs(cameraRig.ViewCamera), "The line is placed on the view the player sees.");
            yield return null;
            NpcWrite("askSpokeFor", 0f);
            Assert.That(director.NpcInvitationIsVisible, Is.True, "The line is still up.");
            Canvas.ForceUpdateCanvases();
            var labels = BubbleLabels(bubble);
            Assert.That(labels.Select(label => label.name), Is.EquivalentTo(new[] { "Invitation speaker", HouseNpcInvitationBubble.LineName, "Caption", "Caption" }));
            float largest = TextSizes.Scale(TextSize.Largest);
            foreach (var label in labels)
            {
                string what = label.name + " (" + label.text + ")";
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(16f * largest - .01f), what + " is drawn at Largest.");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f),
                    what + "'s box: " + label.rectTransform.rect.height + " for a font of " + label.fontSize + ".");
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), what + " fits its box.");
                Assert.That(label.isTextOverflowing, Is.False, what);
            }
        }
    }
}
