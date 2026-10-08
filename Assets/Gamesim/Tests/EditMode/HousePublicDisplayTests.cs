using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class HousePublicDisplayTests
    {
        private static EpisodeState PublicState()=>new EpisodeState
        {
            week=4,seed=123,randomState=456,nextSequence=8,competitionRulesVersion=4,
            contestants={new ContestantState{id="one",name="Maya Hassan"},new ContestantState{id="two",name="Theo King"}}
        };

        [Test]
        public void ProjectionUsesAnnouncedIdentitiesAndCompetitionTitleWithoutChangingTheSeason()
        {
            var state=PublicState();state.phase=EpisodePhase.HoH;state.hohId="one";
            string before=JsonUtility.ToJson(state);
            string pending=HousePublicDisplays.PublicYardText(state);
            Assert.That(pending,Does.Contain("Week 4").And.Contain(EpisodeDirector.CompetitionTitleFor(state)));
            Assert.That(pending,Does.Not.Contain("Winner: Maya Hassan"),"An unresolved competition has no announced result.");
            Assert.That(JsonUtility.ToJson(state),Is.EqualTo(before));
            state.competitionResolved=true;
            Assert.That(HousePublicDisplays.PublicYardText(state),Does.Contain("Winner: Maya Hassan"));
            state.phase=EpisodePhase.Veto;state.vetoHolderId="two";state.vetoResolved=false;
            Assert.That(HousePublicDisplays.PublicYardText(state),Does.Not.Contain("Winner: Theo King"));
            state.vetoResolved=true;
            Assert.That(HousePublicDisplays.PublicYardText(state),Does.Contain("Winner: Theo King"));
        }

        [Test]
        public void PrivateRelationshipsAndScoresCannotChangeThePublicDisplay()
        {
            var state=PublicState();state.phase=EpisodePhase.Campaign;state.hohId="one";state.vetoHolderId="two";
            string publicText=HousePublicDisplays.PublicYardText(state);
            state.relationships.Add(new RelationshipState{fromId="one",toId="two",score=-99});
            state.contestants[0].stats.strategic=1234;state.contestants[1].stats.loyalty=-987;
            state.randomState=7654;state.nextSequence=500;
            Assert.That(HousePublicDisplays.PublicYardText(state),Is.EqualTo(publicText));
            Assert.That(publicText,Does.Not.Contain("1234").And.Not.Contain("987").And.Not.Contain("99"));
        }

        [Test]
        public void EachFinalRoundAndTheFinishedSeasonNamesItsOwnCommittedWinner()
        {
            var state=PublicState();state.phase=EpisodePhase.FinalHoHPart1;state.finalPart1WinnerId="one";
            Assert.That(HousePublicDisplays.PublicYardText(state),Does.Contain("Winner: Maya Hassan"));
            state.phase=EpisodePhase.FinalHoHPart2;state.finalPart2WinnerId="two";
            Assert.That(HousePublicDisplays.PublicYardText(state),Does.Contain("Winner: Theo King"));
            state.phase=EpisodePhase.FinalHoHPart3;state.hohId="one";state.competitionResolved=true;
            Assert.That(HousePublicDisplays.PublicYardText(state),Does.Contain("Winner: Maya Hassan"));
            state.phase=EpisodePhase.Finished;state.winnerId="two";
            Assert.That(HousePublicDisplays.PublicYardText(state),Is.EqualTo("Week 4\nSeason complete\nWinner: Theo King"));
            state.winnerId=null;
            Assert.That(HousePublicDisplays.PublicYardText(state),Does.Contain("To be announced").And.Not.Contain("Theo King"));
        }
    }
}
