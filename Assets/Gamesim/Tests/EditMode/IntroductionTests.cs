using System;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The first night's introductions, as the engine commits them.
    ///
    /// <para>The content and the scoring table are <see cref="WebIntroductionsTests"/>'. What is
    /// pinned here is what an introduction does to a season: it is worth the reference build's
    /// bonus and nothing else, it is free, it happens once per houseguest and only on the first
    /// night, it is on the record the way any change is, and - the property that lets the opening
    /// be watched or skipped without consequence for anything but the player's own choices - it
    /// never draws from the season's generator.</para>
    ///
    /// <para>The house is the regular roster's first eight: the player, then Alex (Strategic,
    /// Social), Emma (Analytical, Strategic), Jordan (Social, Sneaky), Casey (Social, Strategic),
    /// Riley (Analytical, Strategic), Jamie (Emotional, Strategic) and Quinn (Confrontational,
    /// Social).</para>
    /// </summary>
    public sealed class IntroductionTests
    {
        private const string Alex = "alex-chen", Emma = "emma-brown", Jordan = "jordan-taylor", Casey = "casey-wilson",
            Riley = "riley-johnson", Jamie = "jamie-roberts", Quinn = "quinn-martinez";

        private const string FirstNightOnly = "Introductions happen on the first night, before the first competition.";

        // ---------------------------------------------------------------- when

        /// <summary>
        /// The first night is the opening social window, which "week one, social time" does not
        /// pin down: week one has a second social window after the first eviction.
        /// </summary>
        [Test]
        public void IntroductionsBelongToTheFirstNight()
        {
            var opening = Season();
            Assert.That(EpisodeEngine.IsFirstNight(opening), Is.True);
            Assert.That(EpisodeEngine.IntroductionsOpen(opening), Is.True);
            var engine = new EpisodeEngine(opening);
            var accepted = Introduce(engine, Alex, WebIntroductions.Calculated);
            Assert.That(accepted.accepted, Is.True, accepted.reason);

            var competing = new EpisodeEngine(Season());
            var advanced = Apply(competing, EpisodeCommandKind.Advance);
            Assert.That(advanced.accepted, Is.True, advanced.reason);
            Assert.That(competing.Snapshot.phase, Is.EqualTo(EpisodePhase.HoH));
            AssertRefused(competing, Alex, WebIntroductions.Calculated, FirstNightOnly);

            var afterTheEviction = opening.Clone();
            afterTheEviction.evictionResolved = true;
            var underAHead = opening.Clone();
            underAHead.hohId = Casey;
            var aLaterWeek = opening.Clone();
            aLaterWeek.week = 2;
            foreach (var (name, state) in new[] { ("after the eviction", afterTheEviction), ("under a Head of Household", underAHead), ("in week two", aLaterWeek) })
            {
                Assert.That(EpisodeEngine.IsFirstNight(state), Is.False, name);
                Assert.That(EpisodeEngine.IntroductionsOpen(state), Is.False, name);
                AssertRefused(new EpisodeEngine(state), Alex, WebIntroductions.Calculated, FirstNightOnly, name);
            }
            Assert.That(EpisodeEngine.IsFirstNight(null), Is.False);
        }

        /// <summary>Recording the meet-and-greet is what ends it, whoever is still unmet.</summary>
        [Test]
        public void IntroductionsCloseWithTheMeetAndGreet()
        {
            var engine = new EpisodeEngine(Season());
            Assert.That(Mark(engine, OpeningBeat.MeetAndGreet).accepted, Is.True);

            var after = engine.Snapshot;
            Assert.That(EpisodeEngine.IsFirstNight(after), Is.True, "The night goes on after the introductions.");
            Assert.That(EpisodeEngine.IntroductionsOpen(after), Is.False);
            AssertRefused(engine, Alex, WebIntroductions.Calculated, "The introductions are over.");
        }

        // ---------------------------------------------------------------- what it is worth

        /// <summary>
        /// The reference build's bonus, exactly, on the player's side - the social bonus cannot move
        /// numbers this small once rounded, and a loss is never scaled - and 80-120% of it handed
        /// back, by the season's hashed roll for that houseguest.
        /// </summary>
        [TestCase(Alex, WebIntroductions.Calculated, 3)]
        [TestCase(Emma, WebIntroductions.Warm, -3)]
        [TestCase(Jamie, WebIntroductions.Bold, 1)]
        [TestCase(Quinn, WebIntroductions.Bold, 3)]
        public void AFirstImpressionIsWorthTheWebsBonus(string npcId, string approach, int bonus)
        {
            var engine = new EpisodeEngine(Season());
            var result = Introduce(engine, npcId, approach);
            Assert.That(result.accepted, Is.True, result.reason);

            var s = engine.Snapshot;
            Assert.That(s.Score(s.playerId, npcId), Is.EqualTo((double)bonus), "The player's side is the bonus exactly.");
            double roll = WebIntroductions.ReciprocalRoll(5, npcId);
            Assert.That(s.Score(npcId, s.playerId), Is.EqualTo(bonus * (0.8 + 0.4 * roll)).Within(1e-9),
                "The houseguest hands back the bonus scaled by their reciprocal roll.");
            Assert.That(s.Score(npcId, s.playerId), Is.Not.EqualTo((double)bonus).Within(1e-6),
                "The fixture's roll is not the midpoint, so an unscaled reciprocal would show.");
        }

        /// <summary>
        /// One roll answers both of the reducer's draws, so the event on the houseguest's edge
        /// records the change that edge was given. The reference build drew twice there.
        /// </summary>
        [Test]
        public void TheRecordedImpactIsTheImpactApplied()
        {
            var engine = new EpisodeEngine(Season());
            IntroduceEverybody(engine);

            var s = engine.Snapshot;
            foreach (var npc in s.Active.Where(c => !c.isPlayer))
            {
                var theirs = Edge(s, npc.id, s.playerId).events.Single(e => e.type == "introduced");
                Assert.That(theirs.impactScore, Is.EqualTo(s.Score(npc.id, s.playerId)).Within(1e-12), npc.id);
                var yours = Edge(s, s.playerId, npc.id).events.Single(e => e.type == "introduced");
                Assert.That(yours.impactScore, Is.EqualTo(s.Score(s.playerId, npc.id)).Within(1e-12), npc.id);
            }
        }

        /// <summary>
        /// <b>The opening must not be able to re-roll a season.</b> One draw here would move every
        /// competition and vote after it, which would make how the player spent the first night -
        /// or whether they skipped it - a difference in who wins for reasons nobody chose.
        /// </summary>
        [Test]
        public void NoIntroductionSpendsTheSeasonsGenerator()
        {
            var engine = new EpisodeEngine(Season());
            var before = engine.Snapshot;
            IntroduceEverybody(engine);

            var after = engine.Snapshot;
            Assert.That(EpisodeEngine.StillToMeet(after), Is.Empty);
            Assert.That(after.randomState, Is.EqualTo(before.randomState));
            Assert.That(after.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
        }

        /// <summary>
        /// On the first night the house comes to the player. Meeting everybody leaves the week's
        /// interactions untouched, and a conversation afterwards still costs one.
        /// </summary>
        [Test]
        public void IntroductionsCostNoInteraction()
        {
            var engine = new EpisodeEngine(Season());
            var before = engine.Snapshot;
            int spent = EpisodeEngine.SocialActionsSpent(before), budget = EpisodeEngine.SocialActionBudget(before);
            IntroduceEverybody(engine);

            var after = engine.Snapshot;
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent));
            Assert.That(EpisodeEngine.SocialActionBudget(after), Is.EqualTo(budget));

            var talk = Apply(engine, EpisodeCommandKind.Talk, Alex);
            Assert.That(talk.accepted, Is.True, talk.reason);
            Assert.That(EpisodeEngine.SocialActionsSpent(engine.Snapshot), Is.EqualTo(spent + 1));
        }

        // ---------------------------------------------------------------- who

        [Test]
        public void AHouseguestIsIntroducedOnlyOnce()
        {
            var engine = new EpisodeEngine(Season());
            Assert.That(Introduce(engine, Alex, WebIntroductions.Calculated).accepted, Is.True);
            var once = engine.Snapshot;

            AssertRefused(engine, Alex, WebIntroductions.Warm, "You have already met Alex Chen.");
            var after = engine.Snapshot;
            Assert.That(after.Score(after.playerId, Alex), Is.EqualTo(once.Score(once.playerId, Alex)));
            Assert.That(after.Score(Alex, after.playerId), Is.EqualTo(once.Score(Alex, once.playerId)));
        }

        [Test]
        public void OnlyTheThreeApproachesAndActiveHouseguestsAreAccepted()
        {
            var engine = new EpisodeEngine(Season());
            const string approachOnly = "Introduce yourself Warm, Calculated or Bold.";
            AssertRefused(engine, Alex, "shy", approachOnly);
            AssertRefused(engine, Alex, null, approachOnly);
            AssertRefused(engine, Alex, "Warm", approachOnly, "The command carries the approach's id, not its caption.");

            const string houseguestOnly = "Introduce yourself to an active houseguest.";
            AssertRefused(engine, engine.Snapshot.playerId, WebIntroductions.Warm, houseguestOnly);
            AssertRefused(engine, "nobody-at-all", WebIntroductions.Warm, houseguestOnly);
            AssertRefused(engine, null, WebIntroductions.Warm, houseguestOnly);

            var gone = Season();
            gone.Find(Quinn).status = ContestantStatus.Evicted;
            AssertRefused(new EpisodeEngine(gone), Quinn, WebIntroductions.Bold, houseguestOnly);
        }

        // ---------------------------------------------------------------- the record

        /// <summary>
        /// On both edges, as any change is: one event that fades, the reference build's note, and the
        /// week it happened in.
        /// </summary>
        [Test]
        public void AnIntroductionIsOnTheRecordAndFades()
        {
            var engine = new EpisodeEngine(Season());
            Introduce(engine, Alex, WebIntroductions.Calculated);
            Introduce(engine, Emma, WebIntroductions.Warm);

            var s = engine.Snapshot;
            foreach (var edge in new[] { Edge(s, s.playerId, Alex), Edge(s, Alex, s.playerId) })
            {
                string which = edge.fromId + " -> " + edge.toId;
                Assert.That(edge.events, Has.Count.EqualTo(1), which);
                Assert.That(edge.events[0].type, Is.EqualTo("introduced"), which);
                Assert.That(edge.events[0].decayable, Is.True,
                    "Having been introduced is ordinary social traffic, not something anybody did to anybody.");
                Assert.That(edge.events[0].description, Is.EqualTo("Calculated first impression (match)"), which);
                Assert.That(edge.notes, Does.Contain("Calculated first impression (match)"), which);
                Assert.That(edge.lastInteractionWeek, Is.EqualTo(1), which);
            }
            Assert.That(Edge(s, s.playerId, Emma).notes, Does.Contain("Warm first impression (clash)"));
            Assert.That(Edge(s, Emma, s.playerId).notes, Does.Contain("Warm first impression (clash)"));
        }

        [Test]
        public void AnIntroductionMovesTheArc()
        {
            var engine = new EpisodeEngine(Season());
            Introduce(engine, Alex, WebIntroductions.Calculated);

            var arcs = engine.Snapshot.relationshipArcs;
            Assert.That(arcs.Select(a => a.npcId), Is.EqualTo(new[] { Alex }));
            Assert.That(arcs[0].intensity, Is.GreaterThan(0));
            Assert.That(arcs[0].weeklyHistory.Single().reason, Is.EqualTo("Calculated first impression (match)"));
        }

        /// <summary>
        /// The house hears that the player introduced themselves, and to whom. It is not told how it
        /// went: the meet-and-greet shows an answer, not a number or a verdict.
        /// </summary>
        [Test]
        public void TheLogSaysWhoButNotHowMuch()
        {
            var engine = new EpisodeEngine(Season());
            int before = engine.Snapshot.events.Count;
            Introduce(engine, Alex, WebIntroductions.Calculated);

            var s = engine.Snapshot;
            Assert.That(s.events, Has.Count.EqualTo(before + 1));
            var entry = s.events.Last();
            Assert.That(entry.kind, Is.EqualTo("introduction"));
            Assert.That(entry.text, Is.EqualTo("You introduced yourself to Alex Chen."));
            Assert.That(entry.audienceIds, Is.EquivalentTo(new[] { s.playerId, Alex }));
            Assert.That(entry.text.Any(char.IsDigit), Is.False);

            Introduce(engine, Emma, WebIntroductions.Warm);
            string clash = engine.Snapshot.events.Last().text;
            Assert.That(clash, Is.EqualTo("You introduced yourself to Emma Brown."));
            foreach (var word in new[] { "match", "neutral", "clash" })
                Assert.That(clash, Does.Not.Contain(word));
        }

        /// <summary>
        /// Nothing happens to anybody the player has not met, as in the reference build: skipping the
        /// rest of the introductions leaves them strangers, to be talked to later like anyone else.
        /// </summary>
        [Test]
        public void UnmetHouseguestsGetNothing()
        {
            var engine = new EpisodeEngine(Season());
            Introduce(engine, Alex, WebIntroductions.Calculated);

            var s = engine.Snapshot;
            foreach (var edge in s.relationships.Where(r => !(r.fromId == s.playerId && r.toId == Alex) && !(r.fromId == Alex && r.toId == s.playerId)))
            {
                string which = edge.fromId + " -> " + edge.toId;
                Assert.That(edge.score, Is.EqualTo(0), which);
                Assert.That(edge.events, Is.Empty, which);
                Assert.That(edge.notes, Is.Empty, which);
            }
            Assert.That(s.relationshipArcs.Select(a => a.npcId), Is.EqualTo(new[] { Alex }));
        }

        /// <summary>
        /// Who has been met is read off the ledger, so there is nothing new to save and a reload
        /// resumes the meet-and-greet at the first houseguest still to meet, in cast order.
        /// </summary>
        [Test]
        public void IntroductionProgressIsReadFromTheLedger()
        {
            var engine = new EpisodeEngine(Season());
            var cast = new[] { Alex, Emma, Jordan, Casey, Riley, Jamie, Quinn };
            var start = engine.Snapshot;
            Assert.That(EpisodeEngine.StillToMeet(start).Select(c => c.id), Is.EqualTo(cast));
            Assert.That(cast.Any(id => EpisodeEngine.HasIntroduced(start, id)), Is.False);

            Introduce(engine, Jordan, WebIntroductions.Calculated);

            var s = engine.Snapshot;
            foreach (var id in cast)
            {
                Assert.That(EpisodeEngine.HasIntroduced(s, id), Is.EqualTo(id == Jordan), id);
                Assert.That(EpisodeEngine.HasIntroduced(s.Clone(), id), Is.EqualTo(id == Jordan), id + ", copied");
            }
            Assert.That(EpisodeEngine.StillToMeet(s).Select(c => c.id), Is.EqualTo(cast.Where(id => id != Jordan)));
        }

        [Test]
        public void EveryIntroductionLeavesAValidSeason()
        {
            var engine = new EpisodeEngine(Season());
            IntroduceEverybody(engine, state => Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error));
            Assert.That(Mark(engine, OpeningBeat.MeetAndGreet).accepted, Is.True);
            Assert.That(EpisodeValidation.TryValidate(engine.Snapshot, out var final), Is.True, final);
        }

        [Test]
        public void IntroductionsReplayIdentically()
        {
            var first = new EpisodeEngine(Season());
            var second = new EpisodeEngine(Season());
            IntroduceEverybody(first);
            IntroduceEverybody(second);
            Mark(first, OpeningBeat.MeetAndGreet);
            Mark(second, OpeningBeat.MeetAndGreet);
            Assert.That(JsonConvert.SerializeObject(second.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(first.Snapshot)));
        }

        // ---------------------------------------------------------------- fixtures

        private static EpisodeState Season() =>
            SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 5u);

        private static RelationshipState Edge(EpisodeState s, string from, string to) =>
            s.relationships.Single(r => r.fromId == from && r.toId == to);

        /// <summary>Meets everybody in cast order, taking the three approaches in turn.</summary>
        private static void IntroduceEverybody(EpisodeEngine engine, Action<EpisodeState> after = null)
        {
            var guests = EpisodeEngine.StillToMeet(engine.Snapshot);
            for (int i = 0; i < guests.Count; i++)
            {
                var result = Introduce(engine, guests[i].id, WebIntroductions.Approaches[i % 3].Id);
                Assert.That(result.accepted, Is.True, guests[i].id + ": " + result.reason);
                after?.Invoke(result.state);
            }
        }

        private static CommandResult Introduce(EpisodeEngine engine, string npcId, string approach)
        {
            var state = engine.Snapshot;
            return engine.Apply(new EpisodeCommand
            {
                id = "introduce-" + npcId + "-" + state.revision,
                actorId = state.playerId,
                expectedRevision = state.revision,
                expectedPhase = state.phase,
                kind = EpisodeCommandKind.Introduce,
                targetId = npcId,
                secondTargetId = approach,
            });
        }

        private static void AssertRefused(EpisodeEngine engine, string npcId, string approach, string reason, string because = null)
        {
            string before = JsonConvert.SerializeObject(engine.Snapshot);
            var result = Introduce(engine, npcId, approach);
            Assert.That(result.accepted, Is.False, because ?? npcId + " / " + approach);
            Assert.That(result.reason, Is.EqualTo(reason), because);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before), "A refused introduction changes nothing.");
        }

        private static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null)
        {
            var state = engine.Snapshot;
            return engine.Apply(new EpisodeCommand
            {
                id = kind + "-" + state.revision,
                actorId = state.playerId,
                expectedRevision = state.revision,
                expectedPhase = state.phase,
                kind = kind,
                targetId = target,
            });
        }

        private static CommandResult Mark(EpisodeEngine engine, string beat)
        {
            var state = engine.Snapshot;
            return engine.Apply(new EpisodeCommand
            {
                id = "beat-" + beat + "-" + state.revision,
                actorId = state.playerId,
                expectedRevision = state.revision,
                expectedPhase = state.phase,
                kind = EpisodeCommandKind.MarkOpeningBeat,
                targetId = beat,
            });
        }
    }
}
