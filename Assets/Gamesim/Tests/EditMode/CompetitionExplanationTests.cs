using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class CompetitionExplanationTests
    {
        [TestCase(EpisodeCommandKind.Compete)]
        [TestCase(EpisodeCommandKind.SimulateCompetition)]
        public void DisplayedPlayerTermsReconcileToTheCommittedScore(EpisodeCommandKind route)
        {
            for(int seed=1;seed<=32;seed++)
            {
                var state=ContentCatalog.Create((uint)seed);state.competitionRulesVersion=3;
                state.phase=EpisodePhase.Veto;state.week=seed%5+1;
                state.vetoPlayers=state.Active.Select(c=>c.id).ToList();state.nominees.Add(state.playerId);
                state.hohId=state.Active.First(c=>!c.isPlayer).id;
                state.nominees.Add(state.Active.First(c=>!c.isPlayer && c.id!=state.hohId).id);
                state.playerStudyBonus=3;state.phaseEventCompBonus=1;
                state.activeModifiers.Add(new StoryModifierState{id="focus",name="Distracted",weeksLeft=2,competitionBonus=-1});
                var player=state.Find(state.playerId);player.stats.physical=3.173;player.stats.mental=6.891;
                player.stats.endurance=7.047;player.stats.social=2.515;player.stats.luck=8.869;player.stats.competition=4.111;
                var before=JsonConvert.SerializeObject(state);
                var result=Apply(state,route,route==EpisodeCommandKind.Compete?.371:0);
                string text=result.events.Last(e=>e.kind=="competition-performance").text;
                string terms=text.Substring(text.IndexOf("Your weighted stats:",StringComparison.Ordinal));
                var values=Regex.Matches(terms,@"(?:physical|mental|endurance|social|luck|Chance|nominee|preparation|event|storyline|performance|rounding) ([+-]?\d+(?:\.\d+)?)")
                    .Cast<Match>().Select(m=>decimal.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)).ToArray();
                Assert.That(values.Length,Is.EqualTo(12),text);
                decimal sum=decimal.Parse(Regex.Match(text,@"Sum = (\d+\.\d+)").Groups[1].Value,CultureInfo.InvariantCulture);
                Assert.That(values.Sum(),Is.EqualTo(sum),text);
                Assert.That(Math.Abs(values.Last()),Is.LessThanOrEqualTo(.06m),"Rounding must not conceal a missing or wrong scoring term.");
                Assert.That(sum,Is.EqualTo(Math.Round((decimal)result.competitionScores.Single(c=>c.contestantId==state.playerId).score,2,MidpointRounding.AwayFromZero)));
                Assert.That(values.Take(5).Sum(),Is.EqualTo((decimal)WebRules.WeightedCompetitionScore(player.stats,EpisodeEngine.CompetitionCategory(state),false,0,.5)).Within(.03m));
                Assert.That(text,Does.Contain("nominee +2.06").And.Contain("preparation +3").And.Contain("storyline -1"));
                foreach(var npc in state.Active.Where(c=>!c.isPlayer))Assert.That(text,Does.Not.Contain(npc.name));
                Assert.That(JsonConvert.SerializeObject(state),Is.EqualTo(before),"Explanation capture cannot mutate the command's input state.");
                var random=new SeededRandom(state.randomState);
                foreach(var actor in EpisodeEngine.CompetitionPlayers(state))
                {
                    double bonus=actor.isPlayer?EpisodeEngine.CommonCompetitionBonus(state)+(route==EpisodeCommandKind.Compete?.742:0):0;
                    double expected=WebRules.WeightedCompetitionScore(actor.stats,EpisodeEngine.CompetitionCategory(state),state.nominees.Contains(actor.id),bonus,random.NextDouble());
                    Assert.That(result.competitionScores.Single(c=>c.contestantId==actor.id).score,Is.EqualTo(expected));
                }
                Assert.That(result.randomState,Is.EqualTo(random.State),"Capturing explanation must not draw additional randomness.");
            }
        }

        [Test]
        public void NumericalExplanationSurvivesTheActualSaveEnvelope()
        {
            string directory=Path.Combine(Path.GetTempPath(),"GamesimCompetitionExplanation-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var state=ContentCatalog.Create(197);state.competitionRulesVersion=3;
                state=Apply(state,EpisodeCommandKind.Advance,0);state=Apply(state,EpisodeCommandKind.Compete,.621);
                var store=new EpisodeSaveStore(Path.Combine(directory,"episode.json"));store.Save(state);
                Assert.That(store.TryLoad(out var loaded,out string error),Is.True,error);
                Assert.That(loaded.events.Last(e=>e.kind=="competition-performance").text,
                    Is.EqualTo(state.events.Last(e=>e.kind=="competition-performance").text).And.Contains("Sum ="));
                Assert.That(loaded.randomState,Is.EqualTo(state.randomState));
                Assert.That(JsonConvert.SerializeObject(loaded.competitionScores),Is.EqualTo(JsonConvert.SerializeObject(state.competitionScores)));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        [Test]
        public void ObservingSurvivalRoundsDoesNotChangeLegacyArithmeticOrRandomConsumption()
        {
            var actors=ContentCatalog.Create(12).Active.Take(3).ToArray();
            var original=new SeededRandom(231);var observed=new SeededRandom(231);int count=0;
            var expected=WebEnduranceCompetition.Run(actors,original.NextDouble);
            var actual=WebEnduranceCompetition.Run(actors,observed.NextDouble,(time,id,roll,value,eliminated)=>
            {
                count++;var stats=actors.Single(c=>c.id==id).stats;
                Assert.That(value,Is.EqualTo((stats.endurance+stats.physical*.3)*(.5+roll*.5)));
                Assert.That(time,Is.GreaterThanOrEqualTo(10));
            });
            Assert.That(count,Is.EqualTo(5));Assert.That(observed.State,Is.EqualTo(original.State));
            Assert.That(JsonConvert.SerializeObject(actual),Is.EqualTo(JsonConvert.SerializeObject(expected)));
        }

        private static EpisodeState Apply(EpisodeState state,EpisodeCommandKind kind,double performance)
        {
            var result=new EpisodeEngine(state).Apply(new EpisodeCommand{id=Guid.NewGuid().ToString("N"),actorId=state.playerId,
                expectedPhase=state.phase,expectedRevision=state.revision,kind=kind,performance=performance});
            Assert.That(result.accepted,Is.True,result.reason);return result.state;
        }
    }
}
