using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The veto meeting staged (PACK8-PASS-PLAN C1): it is held in the living room, where the red
    /// chairs face the U, and framed there too when it plays on the HUD; its card on the living
    /// room's screen paces its pages as the reveals pace theirs; and the script the card is given
    /// has a face for everybody on the block before and after the meeting.
    /// </summary>
    public sealed class VetoMeetingStagingTests
    {
        [Test]
        public void TheVetoMeetingIsHeldAndFramedInTheLivingRoom()
        {
            Assert.That(EpisodeDirector.CeremonyStageRoom(CeremonySting.VetoKind), Is.EqualTo("Living"),
                "The meeting is staged in the living room (decision 5), not at the nomination table.");
            foreach (var kind in new[] { CeremonySting.NominationKind, CeremonySting.VetoKind, CeremonySting.EvictionKind })
                Assert.That(EpisodeDirector.CeremonyStageRoom(kind), Is.EqualTo(EpisodeDirector.CeremonyRoom(kind)),
                    kind + ": the card on the HUD frame is framed in the room the staged ceremony plays in.");
            Assert.That(EpisodeDirector.SummonsLine(CeremonySting.VetoKind), Is.EqualTo("The house takes its seats for the veto meeting."));
        }

        /// <summary>
        /// One meeting, one name (UI-UX-PASS-PLAN V0, decision 14): the strip, the card on the HUD
        /// frame, the card on the living room's screen, the episode screen's band and title, the week
        /// chip and the status line's phase all say "veto meeting", each in its own case. The strip
        /// and the band said VETO CEREMONY and the screen POWER OF VETO MEETING.
        /// </summary>
        [Test]
        public void TheMeetingGoesByOneName()
        {
            var names = new[]
            {
                ("the strip", CeremonySting.HeadlineFor(CeremonySting.VetoKind)),
                ("the card", CeremonyTakeover.TitleFor(CeremonySting.VetoKind)),
                ("the living room's screen", CeremonyTakeover.MeetingTitle),
                ("the episode screen's band", EpisodeDirector.PhaseTitle(EpisodePhase.VetoMeeting)),
                ("the episode screen's title", EpisodeDirector.VetoMeetingTitle),
                ("the week chip", EpisodeHud.PhaseShort(EpisodePhase.VetoMeeting)),
            };
            foreach (var (where, said) in names)
                Assert.That(said.ToLowerInvariant(), Is.EqualTo("veto meeting"), where + " calls it '" + said + "'.");
            Assert.That(EpisodeDirector.PhaseLine(EpisodePhase.VetoMeeting, 2), Is.EqualTo("Week 2 · Veto meeting"), "and so does the status line's phase.");
            Assert.That(CeremonySting.HeadlineFor(CeremonySting.NominationKind), Is.EqualTo("NOMINATION CEREMONY"),
                "The nomination is still a ceremony: only the veto meeting was two things.");
        }

        [Test]
        public void TheMeetingsPagesArePacedAsTheRevealsAre()
        {
            foreach (var page in new System.Func<CeremonyPace, float>[]
                     { CeremonyPacing.VetoIntro, CeremonyPacing.VetoQuestion, CeremonyPacing.VetoDecision, CeremonyPacing.VetoReplacement, CeremonyPacing.VetoFinal })
            {
                Assert.That(page(CeremonyPace.Quick), Is.GreaterThan(0f), "Every page is on screen at the quick pace.");
                Assert.That(page(CeremonyPace.Quick), Is.LessThan(page(CeremonyPace.Suspenseful)), "and shorter than at the suspenseful one.");
            }
            foreach (var pace in new[] { CeremonyPace.Suspenseful, CeremonyPace.Quick })
                Assert.That(CeremonyPacing.VetoMeeting(pace, used: true), Is.GreaterThan(CeremonyPacing.VetoMeeting(pace, used: false)),
                    pace + ": a used veto has the replacement's page as well.");
            Assert.That(CeremonyPacing.VetoMeeting(CeremonyPace.Suspenseful, used: true), Is.InRange(12f, 20f),
                "A scene, not a wait: the suspenseful meeting runs for about a quarter of a minute.");
        }

        [Test]
        public void TheScriptHasAFaceForTheBlockBeforeAndAfter()
        {
            var state = ContentCatalog.Create(43);
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.phase = EpisodePhase.VetoMeeting;
            state.hohId = npcs[0];
            state.vetoHolderId = npcs[3];
            var before = new List<string> { npcs[1], npcs[2] };
            state.nominees = new List<string> { npcs[2], npcs[4] };

            var script = EpisodeDirector.VetoMeetingScriptFor(state, before);
            Assert.That(script.Playable, Is.True, "A block before and after, each with a face.");
            foreach (var id in before.Concat(state.nominees).Concat(new[] { state.hohId, state.vetoHolderId }))
            {
                Assert.That(script.TryFace(id, out var face), Is.True, id + " has a face.");
                Assert.That(face.Name, Is.EqualTo(state.Find(id).name));
            }
            Assert.That(script.Meeting.savedId, Is.EqualTo(npcs[1]));
            Assert.That(script.Meeting.replacementId, Is.EqualTo(npcs[4]));

            // No block at all: nothing for the card to tell, and the generic card plays instead.
            state.nominees = new List<string>();
            Assert.That(EpisodeDirector.VetoMeetingScriptFor(state, new List<string>()).Playable, Is.False);
        }
    }
}
