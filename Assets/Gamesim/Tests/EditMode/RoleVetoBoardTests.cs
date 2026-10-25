using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The veto board's economics (ROLE-MOMENTS-PLAN R3, §3.0 and §3.2), through the public engine. Under the role moments
    /// the engine holds the player to one word on the veto a week - a second yes, a double click, a repeated command and a
    /// story's promise of the veto are refused or change nothing, while any other yes stands - and nobody asks a holder
    /// who is on the block; an honest no costs the card's tone's no and silence costs more, at the decision, on the tone
    /// the card showed before the decision moved anything, only for an ask still waiting, and never as a breach; the
    /// Bitter, Desperate and Threatening riders settle where the card said they would, through the engine's own writers,
    /// inside the story's favour bound, and only on the board's own asks - never on a word a houseguest gave the player,
    /// nor at a houseguest's veto; every commitment mode settles alike; and filing the asks draws nothing. A season without the role moments - every committed fixture - answers,
    /// decides and replays as it always did, two veto yeses included. Pure simulation: the Unity-free subset runs it.
    /// <c>RoleVetoBoardPersistenceTests</c> reloads the board through the shipping save store.
    /// </summary>
    public sealed class RoleVetoBoardTests
    {
        private const string Nominee = RoleTone.Role.Nominee;

        // ------------------------------------------------------------------ one word on the veto

        /// <summary>
        /// Under the role moments a yes to one nominee's ask is the player's word on the veto, and a yes to the other's is
        /// refused in the engine's own words - nothing moves, draws or is written - and the other ask is still the player's
        /// to turn down.
        /// </summary>
        [Test]
        public void UnderRolesASecondVetoYesIsRefusedAndChangesNothing()
        {
            var engine = Board(RoleTone.Pragmatic, out string first, out string second);
            var meeting = engine.Snapshot;
            Assert.That(Negotiation.VetoWordGiven(meeting), Is.False);
            var taken = Apply(engine, Answer(meeting, AskOf(meeting, first).id, true));
            Assert.That(Negotiation.VetoWordGiven(taken), Is.True, "The yes is the player's word on the veto.");

            string before = JsonConvert.SerializeObject(taken);
            var refused = engine.Apply(Answer(taken, AskOf(taken, second).id, true));
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo(Negotiation.VetoSavesOneRefusal));
            Assert.That(refused.reason, Is.EqualTo("You have already given your word on the veto this week, and it saves one of them."));
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before), "A refused yes moves, draws and writes nothing.");
            Assert.That(AskOf(engine.Snapshot, second).status, Is.EqualTo(DealStatus.Proposed), "The other ask is still on the table,");

            var declined = Apply(engine, Answer(engine.Snapshot, AskOf(engine.Snapshot, second).id, false));
            Assert.That(AskOf(declined, second).status, Is.EqualTo(DealStatus.Declined), "and the player's to turn down.");
            Assert.That(declined.deals.Count(d => Negotiation.IsVetoWord(d.type) && d.week == declined.week && d.status == DealStatus.Active), Is.EqualTo(1));
        }

        /// <summary>
        /// A double click and a repeated command commit one yes: the same command again is a duplicate, a second press
        /// sent against the old revision is stale, one against the new finds the offer gone - and the other card's yes,
        /// pressed twice, is refused twice. One word stands.
        /// </summary>
        [Test]
        public void ADoubleClickOrARepeatedCommandCommitsOneYes()
        {
            var engine = Board(RoleTone.Pragmatic, out string first, out string second);
            var meeting = engine.Snapshot;
            string ask = AskOf(meeting, first).id;
            var yes = Answer(meeting, ask, true);
            var taken = Apply(engine, yes);
            string after = JsonConvert.SerializeObject(taken);

            var again = engine.Apply(yes);
            Assert.That((again.accepted, again.duplicate), Is.EqualTo((false, true)), "The same command id: already committed.");
            var stale = Answer(meeting, ask, true);
            stale.id = "role-veto-double-click";
            var late = engine.Apply(stale);
            Assert.That(late.accepted, Is.False);
            Assert.That(late.reason, Does.StartWith("The episode changed"), "A second press sent against the revision the first changed.");
            var gone = engine.Apply(Answer(taken, ask, true));
            Assert.That(gone.accepted, Is.False);
            Assert.That(gone.reason, Is.EqualTo("That offer is no longer on the table."), "Sent again against the new revision.");

            var other = Answer(taken, AskOf(taken, second).id, true);
            for (int press = 0; press < 2; press++)
            {
                var refused = engine.Apply(other);
                Assert.That((refused.accepted, refused.duplicate, refused.reason), Is.EqualTo((false, false, Negotiation.VetoSavesOneRefusal)),
                    "The other card's yes, press " + (press + 1) + ".");
            }
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(after), "One yes, and nothing else, was committed.");
        }

        /// <summary>
        /// Before the role moments - every season shipped and recorded today - the engine takes a second veto yes, as it
        /// always did (only the meeting's row locked it): both asks stand, the decision keeps one and breaks the other,
        /// and the recorded commands replay byte for byte.
        /// </summary>
        [Test]
        public void BeforeTheRoleMomentsTwoVetoYesesStillStandAndReplay()
        {
            var engine = Board(RoleTone.Pragmatic, out string first, out string second, roles: false);
            var start = engine.Snapshot;
            Assert.That(start.story.rulesVersion, Is.EqualTo(StoryRules.Threads));
            Assert.That(EpisodeEngine.RolesOn(start), Is.False);
            var commands = new List<EpisodeCommand>();
            EpisodeState Step(EpisodeCommand command) { commands.Add(command); return Apply(engine, command); }

            var one = Step(Answer(start, AskOf(start, first).id, true));
            var two = Step(Answer(one, AskOf(one, second).id, true));
            Assert.That(new[] { AskOf(two, first).status, AskOf(two, second).status }, Is.All.EqualTo(DealStatus.Active), "Both yeses stand.");
            var decided = Step(Decide(two, true, first));
            Assert.That(AskOf(decided, first).status, Is.EqualTo(DealStatus.Fulfilled));
            Assert.That((AskOf(decided, second).status, AskOf(decided, second).brokenById), Is.EqualTo((DealStatus.Broken, decided.playerId)));
            Assert.That(AllEvents(decided, EpisodeEngine.LapsedOfferEvent), Is.Empty);

            var replay = new EpisodeEngine(start);
            foreach (var command in commands) Apply(replay, command);
            Assert.That(JsonConvert.SerializeObject(replay.Snapshot), Is.EqualTo(JsonConvert.SerializeObject(decided)), "The season replays byte for byte.");
        }

        /// <summary>
        /// The one test behind the rule reads exactly what naming a price for the veto has always read inline: a veto
        /// commitment of this week, standing or accepted, with the player on either side - every kind, status, week and
        /// side - and the price refuses on it in the same words in every season. R4 adds the keep-the-block deal, a word
        /// on the veto only the role moments write, so a season without them reads exactly what it always read.
        /// </summary>
        [Test]
        public void TheOneWordTestReadsWhatThePriceRefusalAlwaysRead()
        {
            var basis = Board(RoleTone.Pragmatic, out string first, out string second).Snapshot;
            bool Inline(EpisodeState s) => s.deals.Any(d => (d.type == DealKind.VetoUse || d.type == DealKind.VetoHold) && d.week == s.week
                && (d.status == DealStatus.Active || d.status == DealStatus.Accepted) && (d.proposerId == s.playerId || d.recipientId == s.playerId));
            int cases = 0;
            foreach (string kind in DealKind.All)
            foreach (string status in DealStatus.All)
            foreach (int week in new[] { basis.week - 1, basis.week })
            foreach (var (from, to) in new[] { (first, basis.playerId), (basis.playerId, first), (first, second) })
            {
                var s = basis.Clone();
                s.deals.Add(new DealState { id = "deal-word-" + cases, type = kind, status = status, week = week, proposerId = from, recipientId = to, trustImpact = DealTrust.Low });
                Assert.That(Negotiation.VetoWordGiven(s), Is.EqualTo(Inline(s)), kind + " " + status + " week " + week + " " + from + " to " + to);
                cases++;
            }
            Assert.That(cases, Is.GreaterThan(100));

            foreach (bool roles in new[] { true, false })
            {
                var engine = Board(RoleTone.Pragmatic, out string asker, out string other, roles: roles);
                var meeting = engine.Snapshot;
                Assert.That(Negotiation.VetoPriceRefusal(meeting, other, DealKind.VoteSave), Is.Not.EqualTo(Negotiation.VetoSavesOneRefusal), "roles " + roles);
                var taken = Apply(engine, Answer(meeting, AskOf(meeting, asker).id, true));
                Assert.That(Negotiation.VetoPriceRefusal(taken, other, DealKind.VoteSave), Is.EqualTo(Negotiation.VetoSavesOneRefusal), "roles " + roles);
            }
        }

        /// <summary>
        /// The showmance's promise of the veto is a word on it. Under the role moments it is not drawn once a word is
        /// given, nor while the partner's own ask waits on the board (where a yes is that word, and the story would write
        /// no deal beside the ask); drawn before the word, it is refused once one is - and drawn locked, by the one test
        /// the engine and the director share (<see cref="EpisodeEngine.VetoWordRefusal"/>) - and a promise given first
        /// locks a yes in turn. Before them the promise is drawn and taken beside a yes, as it always was. The partner's
        /// own ask is turned down first: while it waits, the story writes no second deal between the two of them in any
        /// season.
        /// </summary>
        [Test]
        public void UnderRolesAStorysPromiseOfTheVetoIsAWordOnIt()
        {
            var option = StoryCatalog.Find("late-nights").Beat("on-the-block").Option("promise-veto");
            Assert.That(option.effects.Any(fx => fx.kind == StoryEffects.Deal && fx.type == DealKind.VetoUse), Is.True, "The option promises the veto.");
            foreach (bool roles in new[] { true, false })
            {
                var basis = Board(RoleTone.Pragmatic, out string first, out string partner, roles: roles).Snapshot;
                string beat = PromiseBeat(basis, partner);
                var cycle = new StoryCycle(basis.storylines.Single(x => x.eventId == beat), StoryCatalog.Find("late-nights"));
                bool Drawn(EpisodeState at) => option.showIf(new StoryContext(at, StoryAnchors.VetoWon), cycle);
                Assert.That(Drawn(basis), Is.EqualTo(!roles), "roles " + roles + ": while the partner's own ask waits, the role moments draw no promise beside it.");
                var engine = new EpisodeEngine(basis);
                var turnedDown = Apply(engine, Answer(engine.Snapshot, AskOf(engine.Snapshot, partner).id, false));
                Assert.That(Drawn(turnedDown), Is.True, "roles " + roles + ": no word given yet, and no ask of theirs waiting.");
                Assert.That(turnedDown.houseEvents.Single(e => e.id == beat).choices.Select(choice => EpisodeEngine.VetoWordRefusal(turnedDown, choice)),
                    Is.All.Null, "roles " + roles + ": nothing to refuse before the word.");
                var taken = Apply(engine, Answer(turnedDown, AskOf(turnedDown, first).id, true));
                Assert.That(Drawn(taken), Is.EqualTo(!roles),
                    "roles " + roles + ": once a word is given the role moments draw no promise of the veto.");
                Assert.That(taken.houseEvents.Single(e => e.id == beat).choices.Select(choice => EpisodeEngine.VetoWordRefusal(taken, choice)),
                    Is.EqualTo(roles ? new[] { Negotiation.VetoSavesOneRefusal, null } : new string[] { null, null }),
                    "roles " + roles + ": drawn before the word, the promise is refused - the director draws it locked - and the lapse never is.");
                var promise = engine.Apply(Progress(taken, beat, "promise-veto"));
                if (roles)
                {
                    Assert.That((promise.accepted, promise.reason), Is.EqualTo((false, Negotiation.VetoSavesOneRefusal)), "A promise drawn before the word is refused.");
                    var kept = Apply(engine, Progress(engine.Snapshot, beat, "undecided"));
                    Assert.That(kept.deals.Count(d => d.type == DealKind.VetoUse && DealStatus.Binds(d.status) && d.proposerId == kept.playerId), Is.Zero);
                }
                else
                {
                    Assert.That(promise.accepted, Is.True, promise.reason);
                    Assert.That(promise.state.deals.Where(d => d.type == DealKind.VetoUse && d.status == DealStatus.Active).Select(d => DealResolution.Partner(d, promise.state.playerId)),
                        Is.EquivalentTo(new[] { first, partner }), "Before the role moments the story's word stands beside the yes.");
                }
            }

            // The other way round: the promise given first, the partner's own ask turned down, and a yes is refused.
            var other = Board(RoleTone.Pragmatic, out string asking, out string promised).Snapshot;
            string promiseBeat = PromiseBeat(other, promised);
            var story = new EpisodeEngine(other);
            Apply(story, Answer(story.Snapshot, AskOf(story.Snapshot, promised).id, false));
            var given = Apply(story, Progress(story.Snapshot, promiseBeat, "promise-veto"));
            Assert.That(given.deals.Single(d => d.type == DealKind.VetoUse && d.status == DealStatus.Active).recipientId, Is.EqualTo(promised));
            var refused = story.Apply(Answer(given, AskOf(given, asking).id, true));
            Assert.That((refused.accepted, refused.reason), Is.EqualTo((false, Negotiation.VetoSavesOneRefusal)), "The story's word locks a yes.");

            // Only the player's own word: an option in which a houseguest gives the player theirs on the veto, as a favour
            // called in does, is not the player's second word, and stands.
            var theirs = Board(RoleTone.Pragmatic, out string yesTo, out string giver).Snapshot;
            string favour = PromiseBeat(theirs, giver, theirs: true);
            var called = new EpisodeEngine(theirs);
            Apply(called, Answer(called.Snapshot, AskOf(called.Snapshot, giver).id, false));
            var word = Apply(called, Answer(called.Snapshot, AskOf(called.Snapshot, yesTo).id, true));
            Assert.That(Negotiation.VetoWordGiven(word), Is.True);
            Assert.That(word.houseEvents.Single(e => e.id == favour).choices.Select(choice => EpisodeEngine.VetoWordRefusal(word, choice)), Is.All.Null,
                "Their word is not the player's to refuse.");
            var held = Apply(called, Progress(word, favour, "promise-veto"));
            Assert.That(held.deals.Any(d => d.type == DealKind.VetoUse && d.status == DealStatus.Active && d.proposerId == giver && d.recipientId == held.playerId),
                Is.True, "The houseguest's word to the player stands beside the player's own.");
        }

        /// <summary>
        /// The one-word rule refuses only a second word on the veto: under the role moments a yes to any other offer after
        /// the player's word on the veto - a partnership put to them at the meeting - stands, as it always did.
        /// </summary>
        [Test]
        public void AVetoWordLocksOnlyVetoWords()
        {
            var engine = Board(RoleTone.Pragmatic, out string first, out string second);
            var meeting = engine.Snapshot.Clone();
            string other = meeting.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active && c.id != first && c.id != second)
                .Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).First();
            string id = NpcDeals.OfferPrefix + meeting.nextSequence++;
            meeting.deals.Add(new DealState
            {
                id = id, type = DealKind.Partnership, proposerId = other, recipientId = meeting.playerId, status = DealStatus.Proposed,
                week = meeting.week, expiresWeek = meeting.week, trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
            });
            Assert.That(EpisodeValidation.TryValidate(meeting, out string error), Is.True, error);
            engine = new EpisodeEngine(meeting);
            var taken = Apply(engine, Answer(meeting, AskOf(meeting, first).id, true));
            Assert.That(Negotiation.VetoWordGiven(taken), Is.True);
            var partnership = engine.Apply(Answer(taken, id, true));
            Assert.That(partnership.accepted, Is.True, partnership.reason);
            Assert.That(partnership.state.deals.Single(d => d.id == id).status, Is.EqualTo(DealStatus.Active), "A yes to a partnership is no word on the veto.");
        }

        // ------------------------------------------------------------------ the trap ask

        /// <summary>
        /// A holder who is on the block too is asked nothing under the role moments: saving themselves would break the
        /// ask at -45. Before them the other nominee asks, as they always did.
        /// </summary>
        [Test]
        public void UnderRolesNobodyAsksAHolderWhoIsOnTheBlock()
        {
            foreach (bool roles in new[] { true, false })
            {
                var s = NominatedHolder(roles, out string other);
                var meeting = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.Advance));
                Assert.That(meeting.accepted, Is.True, meeting.reason);
                Assert.That(meeting.state.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
                var asks = meeting.state.deals.Where(d => RoleOffers.IsVetoAsk(meeting.state, d)).Select(d => d.proposerId).ToList();
                if (roles) Assert.That(asks, Is.Empty, "Under the role moments the trap ask is not put.");
                else Assert.That(asks, Is.EqualTo(new[] { other }), "Before them it is, as it always was.");
            }
        }

        // ------------------------------------------------------------------ a no, and silence

        /// <summary>Under the role moments an honest no to a nominee's ask costs its card's tone's no, every tone.</summary>
        [TestCase(RoleTone.Bitter)]
        [TestCase(RoleTone.Threatening)]
        [TestCase(RoleTone.Desperate)]
        [TestCase(RoleTone.Friendly)]
        [TestCase(RoleTone.Earnest)]
        [TestCase(RoleTone.Calculated)]
        [TestCase(RoleTone.Pragmatic)]
        public void AnHonestNoCostsTheTonesNo(string tone)
        {
            var engine = Board(tone, out string first, out _);
            var meeting = engine.Snapshot;
            var card = RoleOffers.VetoAskCard(meeting, AskOf(meeting, first));
            Assert.That(card.tone, Is.EqualTo(tone), "The card wears the tone,");
            Assert.That(card.answers, Is.EqualTo(RoleOffers.Answers(RoleQuotes.VetoAsk, tone)), "and says what a no and silence cost.");
            double before = meeting.Score(meeting.playerId, first);
            var no = Apply(engine, Answer(meeting, AskOf(meeting, first).id, false));
            var stake = RoleTone.Stakes(tone);
            Assert.That(Events(no, no.playerId, first, "deal_declined").Select(e => e.impactScore), Is.EqualTo(new[] { stake.decline }), tone);
            Assert.That(no.Score(no.playerId, first) - before, Is.EqualTo(stake.decline).Within(1e-9), tone + ": the player's read moves by the no.");
            Assert.That(stake.decline, Is.InRange(-3.0, 0.0));
        }

        /// <summary>
        /// Under the role moments an ask left hanging costs its card's tone's lapse once the decision lapses it - more than
        /// the no, every tone - and the player is told; and it is no breach: the ask expires, never Broken, nobody else
        /// hears of it, and nothing counts against the player's word or how the house reads them.
        /// </summary>
        [TestCase(RoleTone.Bitter)]
        [TestCase(RoleTone.Threatening)]
        [TestCase(RoleTone.Desperate)]
        [TestCase(RoleTone.Friendly)]
        [TestCase(RoleTone.Earnest)]
        [TestCase(RoleTone.Calculated)]
        [TestCase(RoleTone.Pragmatic)]
        public void SilenceCostsTheTonesLapseAndIsNoBreach(string tone)
        {
            var engine = Board(tone, out string first, out string second);
            var meeting = engine.Snapshot;
            int broken = NpcDeals.BrokenDeals(meeting, meeting.playerId), breaches = AllEvents(meeting, "deal_broken").Count;
            int page = CommitmentsRead.With(meeting, first).Count(c => c.outcome == CommitmentsRead.Outcomes.Broken);
            var decided = Apply(engine, Decide(meeting, false, null));
            var stake = RoleTone.Stakes(tone);
            Assert.That(stake.lapse, Is.LessThan(stake.decline), tone + ": silence costs more than a no.");

            var ask = AskOf(decided, first);
            Assert.That((ask.status, ask.brokenById, ask.settledWeek), Is.EqualTo((DealStatus.Expired, (string)null, 0)), tone + ": it lapses, as it always did.");
            Assert.That(Events(decided, decided.playerId, first, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore), Is.EqualTo(new[] { stake.lapse }), tone);
            Assert.That(decided.events.Count(e => e.kind == "deal" && e.text == "You left a veto commitment from " + decided.Find(first).name + " unanswered."),
                Is.EqualTo(1), tone + ": the player is told.");
            Assert.That(NpcDeals.BrokenDeals(decided, decided.playerId), Is.EqualTo(broken), tone + ": nothing against the player's word,");
            Assert.That(AllEvents(decided, "deal_broken"), Has.Count.EqualTo(breaches), tone + ": no breach on anybody's record,");
            Assert.That(CommitmentsRead.With(decided, first).Count(c => c.outcome == CommitmentsRead.Outcomes.Broken), Is.EqualTo(page), tone + ": nor on the Your word page,");
            Assert.That(AllEvents(decided, EpisodeEngine.LapsedOfferEvent).All(e => e.edge.Contains(decided.playerId)
                && (e.edge.Contains(first) || e.edge.Contains(second))), Is.True, tone + ": and nobody else hears of it.");
        }

        /// <summary>
        /// Before the role moments a no costs -3 whatever the card would say, and silence nothing: the ask expires at the
        /// decision with no change, no line, and no draw.
        /// </summary>
        [Test]
        public void BeforeTheRoleMomentsANoCostsThreeAndSilenceNothing()
        {
            var engine = Board(RoleTone.Pragmatic, out string first, out string second, roles: false);
            var meeting = engine.Snapshot;
            Assert.That(RoleTone.Stakes(RoleTone.Of(meeting, first, Nominee)).decline, Is.Not.EqualTo(PlayerDeals.DeclineImpact), "The card's own no would differ.");
            var no = Apply(engine, Answer(meeting, AskOf(meeting, first).id, false));
            Assert.That(Events(no, no.playerId, first, "deal_declined").Select(e => e.impactScore), Is.EqualTo(new[] { PlayerDeals.DeclineImpact }));
            var decided = Apply(engine, Decide(no, false, null));
            Assert.That(AskOf(decided, second).status, Is.EqualTo(DealStatus.Expired));
            Assert.That(AllEvents(decided, EpisodeEngine.LapsedOfferEvent), Is.Empty, "Silence costs nothing,");
            Assert.That(decided.events.Any(e => e.text.Contains("unanswered")), Is.False, "and says nothing.");
            Assert.That(decided.story.grudges.Any(g => g.holderId == first && g.targetId == decided.playerId && g.cause == GrudgeCauses.Story), Is.False);
        }

        /// <summary>
        /// Silence is charged on the tone the card showed before the decision, not the one the decision leaves: an
        /// Earnest nominee left hanging and saved anyway reads Friendly after the save lifts the player's read of them,
        /// and is charged Earnest's lapse.
        /// </summary>
        [Test]
        public void SilenceIsChargedOnTheToneTheCardShowedBeforeTheDecisionMovedIt()
        {
            var engine = Board(RoleTone.Earnest, out string first, out _);
            var meeting = engine.Snapshot;
            Assert.That(RoleOffers.ToneOf(meeting, AskOf(meeting, first)), Is.EqualTo(RoleTone.Earnest));
            var saved = Apply(engine, Decide(meeting, true, first));
            Assert.That(RoleTone.Of(saved, first, Nominee), Is.EqualTo(RoleTone.Friendly),
                "The save lifted the player's read of them: after the decision their card would read Friendly,");
            Assert.That(RoleTone.Stakes(RoleTone.Friendly).lapse, Is.Not.EqualTo(RoleTone.Stakes(RoleTone.Earnest).lapse));
            Assert.That(Events(saved, saved.playerId, first, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(RoleTone.Earnest).lapse }), "and silence is charged on the Earnest the card showed.");
        }

        /// <summary>
        /// Nor on the tone the house's last beats leave: the decision's command first fires the window's remaining beats
        /// (<see cref="EpisodeEngine.Close"/>), and an Earnest nominee's beat that seeks the player out lifts the player's
        /// read of them past Close. The card the player saw said Earnest, and silence is charged Earnest's lapse.
        /// </summary>
        [Test]
        public void SilenceIsChargedOnTheToneTheCardShowedBeforeTheHousesLastBeats()
        {
            var engine = MovedByTheLastBeats(out string asker);
            var meeting = engine.Snapshot;
            var closed = meeting.Clone();
            EpisodeEngine.Close(closed);
            Assert.That((RoleOffers.ToneOf(meeting, AskOf(meeting, asker)), RoleOffers.ToneOf(closed, AskOf(closed, asker))),
                Is.EqualTo((RoleTone.Earnest, RoleTone.Friendly)), "The last beats move the card from Earnest to Friendly,");
            var decided = Apply(engine, Decide(meeting, false, null));
            Assert.That(Events(decided, decided.playerId, asker, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(RoleTone.Earnest).lapse }), "and silence is charged on the Earnest the card showed.");
        }

        /// <summary>
        /// Only an ask still waiting when the decision is made was left hanging. A price the player named for the veto, and
        /// had taken, answers the nominee's own ask - it expires then, before the meeting decides - so the decision charges
        /// no silence for it; the other nominee's ask, still waiting, is charged its lapse.
        /// </summary>
        [Test]
        public void APriceTakenAnswersTheAskAndNoSilenceIsCharged()
        {
            var engine = Board(RoleTone.Desperate, out string first, out string second);
            var meeting = engine.Snapshot;
            EpisodeState named = null;
            for (uint r = 1; r < 400 && named == null; r++)
            {
                var s = meeting.Clone();
                s.randomState = r * 2654435761u;
                var trial = new EpisodeEngine(s);
                var result = trial.Apply(Command(s, EpisodeCommandKind.Negotiate, first, null, Negotiation.VetoPriceMove(DealKind.VoteSave)));
                Assert.That(result.accepted, Is.True, result.reason);
                if (AskOf(result.state, first).status != DealStatus.Expired) continue;
                named = result.state;
                engine = trial;
            }
            Assert.That(named, Is.Not.Null, "No roll took the price.");
            Assert.That(Negotiation.VetoWordGiven(named), Is.True, "The price taken is the player's word on the veto.");
            var decided = Apply(engine, Decide(named, true, first));
            Assert.That(Events(decided, decided.playerId, first, EpisodeEngine.LapsedOfferEvent), Is.Empty, "The ask a price answered was never left hanging,");
            Assert.That(decided.events.Any(e => e.text == "You left a veto commitment from " + decided.Find(first).name + " unanswered."), Is.False, "and nothing says it was.");
            Assert.That(Events(decided, decided.playerId, second, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(RoleTone.Pragmatic).lapse }), "The other nominee's was.");
        }

        // ------------------------------------------------------------------ only the board's asks

        /// <summary>
        /// A houseguest's own word to the player on the veto - a story's, here a Desperate nominee's - is no card on the
        /// board: nobody asked the player to answer it. The decision keeps it as the deal it is and settles no rider or
        /// silence on it; only the asks the board showed settle - the giver's own, turned down, and the other nominee's,
        /// left hanging.
        /// </summary>
        [Test]
        public void AWordAHouseguestGaveThePlayerIsNoCardAndPaysNoRider()
        {
            var basis = Board(RoleTone.Desperate, out string giver, out string other).Snapshot;
            string beat = PromiseBeat(basis, giver, theirs: true);
            var engine = new EpisodeEngine(basis);
            var declined = Apply(engine, Answer(basis, AskOf(basis, giver).id, false));
            var given = Apply(engine, Progress(declined, beat, "promise-veto"));
            var word = given.deals.Single(d => d.type == DealKind.VetoUse && d.status == DealStatus.Active && d.proposerId == giver && d.recipientId == given.playerId);
            Assert.That(RoleOffers.IsVetoAsk(given, word), Is.False, "Their word is no ask,");
            Assert.That(RoleOffers.VetoAsks(given).Select(d => d.proposerId), Is.EqualTo(new[] { other }), "and the board shows only the other nominee's.");

            var decided = Apply(engine, Decide(given, true, giver));
            Assert.That(decided.deals.Single(d => d.id == word.id).status, Is.EqualTo(DealStatus.Fulfilled), "The veto kept their word,");
            Assert.That(Hooks.Has(decided, decided.playerId, giver), Is.False, "and a Desperate houseguest owes nothing for a word that was theirs,");
            Assert.That(decided.events.Any(e => e.text.Contains("owes you a favour")), Is.False, "nor is anything said.");
            Assert.That(Events(decided, decided.playerId, giver, EpisodeEngine.LapsedOfferEvent), Is.Empty);
            Assert.That(Events(decided, decided.playerId, other, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(RoleTone.Pragmatic).lapse }), "The other nominee's ask, left hanging, is charged.");
        }

        /// <summary>
        /// A favour called in at a houseguest's veto (Call in the Favour's "Ask for the veto") is the holder's word to the
        /// nominated player, and their decision settles no board: nobody asks the player for a veto they do not hold. The
        /// holder keeps their word and the favour the player spent stays spent, under the role moments as before them (R3's
        /// review: the first build settled it as a kept Desperate ask and gave the favour back).
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void AFavourCalledInAtAHousemateVetoSettlesNoBoard(bool roles)
        {
            var s = BeforeTheMeeting(RoleTone.Pragmatic, out string first, out string second, 2, roles);
            // The player goes on the block in the second nominee's place; a third houseguest, on the block in week one -
            // Desperate, read as a nominee - holds the veto and owes the player a favour.
            string holder = s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active && c.id != s.hohId && c.id != first && c.id != second)
                .Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).First();
            var player = s.Find(s.playerId);
            var two = s.Find(second);
            two.nominationWeeks.Remove(s.week);
            two.timesNominated--;
            s.nominees[1] = s.playerId;
            player.nominationWeeks.Add(s.week);
            player.timesNominated++;
            var h = s.Find(holder);
            if (!h.nominationWeeks.Contains(1)) { h.nominationWeeks.Insert(0, 1); h.timesNominated++; }
            s.vetoHolderId = holder;
            if (!s.vetoPlayers.Contains(holder)) s.vetoPlayers[s.vetoPlayers.Count - 1] = holder;
            s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == holder ? 100 : i }).ToList();
            Set(s, holder, s.playerId, 100);
            Set(s, s.playerId, holder, 0);
            s.story.hooks.Add(new HookState { id = "hook-role-veto-board", holderId = s.playerId, overId = holder, week = 1 });
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);

            var engine = new EpisodeEngine(s);
            var meeting = Apply(engine, Command(s, EpisodeCommandKind.Advance));
            Assert.That((meeting.phase, EpisodeEngine.RolesOn(meeting)), Is.EqualTo((EpisodePhase.VetoMeeting, roles)));
            Assert.That(meeting.deals.Where(d => RoleOffers.IsVetoAsk(meeting, d)), Is.Empty, "Nobody asks a player who does not hold the veto.");
            meeting.houseEvents.RemoveAll(e => e.chosenIndex < 0 && e.kind == HouseEventKind.Story);
            string beat = FavourBeat(meeting, holder);
            engine = new EpisodeEngine(meeting);
            var called = Apply(engine, Progress(meeting, beat, "veto"));
            var word = called.deals.Single(d => d.type == DealKind.VetoUse && d.proposerId == holder && d.recipientId == called.playerId && d.week == called.week);
            Assert.That(word.status, Is.EqualTo(DealStatus.Active));
            Assert.That(RoleOffers.IsVetoAsk(called, word), Is.False, "The holder's word is no ask on a board.");
            Assert.That(Hooks.Has(called, called.playerId, holder), Is.False, "The favour is spent.");
            Assert.That(EpisodeEngine.NpcVetoSave(called), Is.EqualTo(called.playerId), "The holder will save the player.");
            Assert.That(RoleTone.Of(called, holder, Nominee), Is.EqualTo(RoleTone.Desperate), "Read as a nominee, the holder would wear a favour's rider.");

            var decided = Apply(engine, Command(called, EpisodeCommandKind.Advance));
            Assert.That(decided.vetoResolved, Is.True);
            Assert.That(decided.deals.Single(d => d.id == word.id).status, Is.EqualTo(DealStatus.Fulfilled), "The holder kept their word,");
            Assert.That(Hooks.Has(decided, decided.playerId, holder), Is.False, "and the favour the player spent stays spent:");
            Assert.That(decided.events.Any(e => e.text.Contains("owes you a favour")), Is.False, "the player gave no word to be owed for.");
            Assert.That(AllEvents(decided, EpisodeEngine.LapsedOfferEvent), Is.Empty, "Nothing was left hanging.");
        }

        // ------------------------------------------------------------------ the riders

        /// <summary>
        /// A Bitter nominee whose ask was taken and kept eases their grudge against the player by thirty; broken, it does
        /// not ease. The grudge starts at its ceiling, so the breach's own grudge cannot hide an easing.
        /// </summary>
        [Test]
        public void ABitterNomineesGrudgeEasesWhenTheirWordIsKept()
        {
            foreach (bool keep in new[] { true, false })
            {
                var basis = Board(RoleTone.Bitter, out string first, out string second).Snapshot;
                basis.story.grudges.Add(new GrudgeState { holderId = first, targetId = basis.playerId, cause = GrudgeCauses.Nominated, severity = Grudges.Ceiling, originWeek = 1, count = 1 });
                var engine = new EpisodeEngine(basis);
                var taken = Apply(engine, Answer(basis, AskOf(basis, first).id, true));
                var decided = Apply(engine, Decide(taken, true, keep ? first : second));
                Assert.That(AskOf(decided, first).status, Is.EqualTo(keep ? DealStatus.Fulfilled : DealStatus.Broken));
                double grudge = Grudges.Severity(decided, first, decided.playerId);
                if (keep) Assert.That(grudge, Is.EqualTo(Grudges.Ceiling - RoleTone.BitterEase), "Kept: their grudge eases by thirty.");
                else Assert.That(grudge, Is.EqualTo(Grudges.Ceiling), "Broken: it does not ease.");
            }
        }

        /// <summary>A Desperate nominee whose ask was taken and kept owes the player a favour, and the player is told; broken, they owe nothing.</summary>
        [Test]
        public void ADesperateNomineeOwesAFavourWhenTheirWordIsKept()
        {
            foreach (bool keep in new[] { true, false })
            {
                var engine = Board(RoleTone.Desperate, out string first, out string second);
                var taken = Apply(engine, Answer(engine.Snapshot, AskOf(engine.Snapshot, first).id, true));
                Assert.That(Hooks.Has(taken, taken.playerId, first), Is.False);
                var decided = Apply(engine, Decide(taken, true, keep ? first : second));
                Assert.That(Hooks.Has(decided, decided.playerId, first), Is.EqualTo(keep), "keep " + keep);
                Assert.That(decided.events.Any(e => e.kind == "deal" && e.text == decided.Find(first).name + " owes you a favour for your word on the veto."),
                    Is.EqualTo(keep), "keep " + keep + ": the receipt.");
            }
        }

        /// <summary>
        /// A Threatening nominee turned down or left hanging, and left on the block, holds a grudge of thirty against the
        /// player. Saved anyway, they hold none; taken and broken, the breach is theirs to hold and the rider adds nothing.
        /// Other tones hold no rider's grudge.
        /// </summary>
        [Test]
        public void AThreateningNomineeLeftOnTheBlockHoldsAGrudge()
        {
            foreach (var (answer, saveThem, grudge) in new[] { ("decline", false, 30.0), ("leave", false, 30.0), ("decline", true, 0.0), ("leave", true, 0.0) })
            {
                var engine = Board(RoleTone.Threatening, out string first, out string second);
                var at = engine.Snapshot;
                if (answer == "decline") at = Apply(engine, Answer(at, AskOf(at, first).id, false));
                var decided = Apply(engine, Decide(at, saveThem, saveThem ? first : null));
                Assert.That(Grudges.Severity(decided, first, decided.playerId), Is.EqualTo(grudge), answer + (saveThem ? ", saved" : ", left up"));
                if (grudge > 0) Assert.That(decided.story.grudges.Single(g => g.holderId == first && g.targetId == decided.playerId).cause, Is.EqualTo(GrudgeCauses.Story));
            }

            var broken = Board(RoleTone.Threatening, out string asker, out string other);
            var yes = Apply(broken, Answer(broken.Snapshot, AskOf(broken.Snapshot, asker).id, true));
            var breach = Apply(broken, Decide(yes, true, other));
            Assert.That(AskOf(breach, asker).status, Is.EqualTo(DealStatus.Broken));
            var held = breach.story.grudges.Single(g => g.holderId == asker && g.targetId == breach.playerId);
            Assert.That(held.cause, Is.EqualTo(GrudgeCauses.DealBroken), "Taken and broken: the breach's grudge, with no rider on top.");

            foreach (string tone in new[] { RoleTone.Earnest, RoleTone.Pragmatic })
            {
                var engine = Board(tone, out string first, out _);
                var decided = Apply(engine, Decide(Apply(engine, Answer(engine.Snapshot, AskOf(engine.Snapshot, first).id, false)), false, null));
                Assert.That(Grudges.Severity(decided, first, decided.playerId), Is.Zero, tone + " carries no rider.");
            }
        }

        /// <summary>
        /// The favour comes through the story's own writer, inside its bound of 32: with 32 unspent favours held in the
        /// house there is no room and nothing is owed or said; with one of them spent, room is made and the favour owed.
        /// </summary>
        [Test]
        public void ADesperateNomineesFavourKeepsInsideTheStorysBound()
        {
            foreach (bool oneSpent in new[] { false, true })
            {
                var basis = Board(RoleTone.Desperate, out string first, out _).Snapshot;
                var npcs = basis.contestants.Where(c => !c.isPlayer && c.id != first).Select(c => c.id).ToList();
                basis.story.hooks.Clear();
                for (int i = 0; i < 32; i++)
                    basis.story.hooks.Add(new HookState { id = "hook-fill-" + i, holderId = npcs[i % npcs.Count], overId = npcs[(i + 1) % npcs.Count], week = 1, spent = oneSpent && i == 0 });
                var engine = new EpisodeEngine(basis);
                var taken = Apply(engine, Answer(basis, AskOf(basis, first).id, true));
                var decided = Apply(engine, Decide(taken, true, first));
                Assert.That(AskOf(decided, first).status, Is.EqualTo(DealStatus.Fulfilled));
                Assert.That(decided.story.hooks, Has.Count.EqualTo(32), "one spent " + oneSpent + ": never past the bound.");
                Assert.That(Hooks.Has(decided, decided.playerId, first), Is.EqualTo(oneSpent), "one spent " + oneSpent);
                Assert.That(decided.events.Any(e => e.text.Contains("owes you a favour")), Is.EqualTo(oneSpent), "one spent " + oneSpent + ": said only where owed.");
            }
        }

        // ------------------------------------------------------------------ modes and randomness

        /// <summary>
        /// Every commitment mode settles the board alike: the price struck with the yes, a second yes refused, the kept
        /// word, and the Threatening nominee left hanging on the block - their lapse and their grudge.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryCommitmentModeSettlesTheBoardAlike(int mode)
        {
            var engine = Board(RoleTone.Threatening, out string first, out string second, mode: mode);
            var meeting = engine.Snapshot;
            Assert.That(meeting.unifiedCommitmentRulesVersion, Is.EqualTo(mode));
            var yes = Apply(engine, Answer(meeting, AskOf(meeting, second).id, true));
            Assert.That(Negotiation.PriceOf(yes, AskOf(yes, second)), Is.Not.Null, "mode " + mode + ": the ask's price is struck with the yes.");
            var refused = engine.Apply(Answer(yes, AskOf(yes, first).id, true));
            Assert.That((refused.accepted, refused.reason), Is.EqualTo((false, Negotiation.VetoSavesOneRefusal)), "mode " + mode);
            var decided = Apply(engine, Decide(yes, true, second));
            Assert.That(AskOf(decided, second).status, Is.EqualTo(DealStatus.Fulfilled), "mode " + mode);
            Assert.That(AskOf(decided, first).status, Is.EqualTo(DealStatus.Expired), "mode " + mode);
            Assert.That(Events(decided, decided.playerId, first, EpisodeEngine.LapsedOfferEvent).Select(e => e.impactScore),
                Is.EqualTo(new[] { RoleTone.Stakes(RoleTone.Threatening).lapse }), "mode " + mode);
            Assert.That(Grudges.Severity(decided, first, decided.playerId), Is.EqualTo(RoleTone.ThreateningGrudge), "mode " + mode);
        }

        /// <summary>Filing the asks in a season at Roles is roll-free: the season's generator is where it was.</summary>
        [Test]
        public void FilingTheAsksDrawsNothing()
        {
            var s = BeforeTheMeeting(RoleTone.Desperate, out string first, out string second, 2, true);
            Assert.That(EpisodeEngine.RolesOn(s), Is.True);
            uint before = s.randomState;
            int sequence = s.nextSequence;
            NpcDeals.AskForTheVeto(s);
            Assert.That(s.deals.Where(d => RoleOffers.IsVetoAsk(s, d)).Select(d => d.proposerId), Is.EquivalentTo(new[] { first, second }));
            Assert.That(s.randomState, Is.EqualTo(before), "No draw from the season's stream.");
            Assert.That(s.nextSequence, Is.GreaterThan(sequence), "(The ids and lines it writes take sequence numbers, as they always did.)");
        }

        // ------------------------------------------------------------------ fixtures

        private static readonly Dictionary<(int mode, bool roles), List<EpisodeState>> Walks = new Dictionary<(int, bool), List<EpisodeState>>();

        /// <summary>
        /// A season at story rules Roles (or at Threads, the shipped rules, without <paramref name="roles"/>) in commitment
        /// mode <paramref name="mode"/>, played through the public engine to week two's veto competition with the player
        /// neither Head of Household nor on the block, and the two nominees clean of any record with the player that the
        /// ladder reads; then handed the veto, as R1's fixture hands it. The first nominee is shaped to wear
        /// <paramref name="tone"/> - by the record, traits and the player's reading the ladder reads - and the second to
        /// wear Pragmatic; both are warm enough to ask, and hold no grudge against the player. Not yet at the meeting.
        /// </summary>
        internal static EpisodeState BeforeTheMeeting(string tone, out string first, out string second, int mode, bool roles)
        {
            foreach (var walked in WeekTwoVetoes(mode, roles))
            {
                var s = walked.Clone();
                string one = s.nominees[0], two = s.nominees[1];
                first = one;
                second = two;
                Shape(s, first, tone);
                Shape(s, second, RoleTone.Pragmatic);
                s.story.grudges.RemoveAll(g => (g.holderId == one || g.holderId == two) && g.targetId == s.playerId);
                if (RoleTone.Of(s, first, Nominee) != tone || RoleTone.Of(s, second, Nominee) != RoleTone.Pragmatic) continue;
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
                return s;
            }
            Assert.Fail("No walked season could be shaped to " + tone + ".");
            first = second = null;
            return null;
        }

        /// <summary>
        /// <see cref="BeforeTheMeeting"/>, advanced into the meeting by the engine, which files both nominees' asks; the
        /// first's card wears <paramref name="tone"/> there.
        /// </summary>
        internal static EpisodeEngine Board(string tone, out string first, out string second, int mode = 2, bool roles = true)
        {
            var s = BeforeTheMeeting(tone, out first, out second, mode, roles);
            var engine = new EpisodeEngine(s);
            var meeting = Apply(engine, Command(s, EpisodeCommandKind.Advance));
            Assert.That(meeting.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(EpisodeEngine.RolesOn(meeting), Is.EqualTo(roles));
            Assert.That(meeting.deals.Where(d => RoleOffers.IsVetoAsk(meeting, d)).Select(d => d.proposerId), Is.EquivalentTo(new[] { first, second }), "Both nominees asked.");
            if (roles) Assert.That(RoleOffers.ToneOf(meeting, AskOf(meeting, first)), Is.EqualTo(tone), "The first card wears the tone at the meeting.");
            return engine;
        }

        /// <summary>The week-two veto competitions the fixture's seeds reach, walked once per mode and rules.</summary>
        private static List<EpisodeState> WeekTwoVetoes(int mode, bool roles)
        {
            if (Walks.TryGetValue((mode, roles), out var held)) return held;
            var found = new List<EpisodeState>();
            for (uint seed = 7101; seed < 7161 && found.Count < 3; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = roles ? StoryRules.Roles : StoryRules.Threads;
                start.unifiedCommitmentRulesVersion = mode == 2 ? UnifiedVoteFamilyValidation.Version : mode;
                start.unifiedHearingRulesVersion = mode == 0 ? 0 : UnifiedCommitmentHearings.ProspectiveVersion;
                var walk = new EpisodeEngine(start);
                for (int i = 0; i < 400 && !(walk.Snapshot.week == 2 && walk.Snapshot.phase == EpisodePhase.Veto); i++)
                {
                    var step = walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot));
                    Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                    if (walk.Snapshot.week > 2) break;
                }
                var s = walk.Snapshot;
                if (s.week != 2 || s.phase != EpisodePhase.Veto || s.competitionResolved || s.Find(s.playerId).status != ContestantStatus.Active
                    || s.hohId == s.playerId || s.nominees.Contains(s.playerId) || !s.nominees.All(id => Clean(s, id))) continue;
                found.Add(s);
            }
            Assert.That(found, Is.Not.Empty, "No seed reached week two's veto with the player free and both nominees clean.");
            Walks[(mode, roles)] = found;
            return found;
        }

        /// <summary>Nothing between the player and them the ladder reads: never on the block before, never put up or wronged by the player, no pact.</summary>
        private static bool Clean(EpisodeState s, string id)
        {
            var who = s.Find(id);
            return !RoleTone.OnTheBlockBefore(s, who) && !RoleTone.YouNominatedThemBefore(s, who) && !RoleTone.YouBrokeYourWordBefore(s, id)
                && !s.Allied(s.playerId, id);
        }

        /// <summary>
        /// Makes a nominee wear a tone by what the ladder reads (§3.0): a word the player broke last week (Bitter); Bold and
        /// Vengeful traits (Threatening); a week on the block before (Desperate); the player's own reading of them as close
        /// (Friendly); Warm (Earnest); Steady (Calculated); nothing at all (Pragmatic). Warm enough toward the player to ask,
        /// whatever the record costs them.
        /// </summary>
        private static void Shape(EpisodeState s, string id, string tone)
        {
            var who = s.Find(id);
            who.traits = new List<string>();
            Set(s, s.playerId, id, 0);
            Set(s, id, s.playerId, 50);
            switch (tone)
            {
                case RoleTone.Bitter:
                    s.deals.Add(new DealState
                    {
                        id = "deal-word-" + id, type = DealKind.InformationSharing, proposerId = s.playerId, recipientId = id,
                        status = DealStatus.Broken, week = 1, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
                        brokenById = s.playerId, settledWeek = 1,
                    });
                    break;
                case RoleTone.Threatening: who.traits = new List<string> { "Competitive", "Stubborn" }; break;
                case RoleTone.Desperate:
                    if (!who.nominationWeeks.Contains(1)) { who.nominationWeeks.Insert(0, 1); who.timesNominated++; }
                    break;
                case RoleTone.Friendly: Set(s, s.playerId, id, 30); break;
                case RoleTone.Earnest: who.traits = new List<string> { "Emotional" }; break;
                case RoleTone.Calculated: who.traits = new List<string> { "Analytical" }; break;
            }
        }

        /// <summary>
        /// A meeting at Roles, reached as <see cref="BeforeTheMeeting"/> reaches one but in week one or two of the seeds
        /// below, where the house's last beats move a card: both nominees Earnest (Emotional) and read by the player at 14,
        /// one under Close, and a beat left in the window that seeks one of them out with the player and lifts the read.
        /// The engine is at the meeting; <paramref name="asker"/> is the nominee whose card the beats move.
        /// </summary>
        private static EpisodeEngine MovedByTheLastBeats(out string asker)
        {
            for (uint seed = 7170; seed < 7230; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = StoryRules.Roles;
                var walk = new EpisodeEngine(start);
                for (int week = 1; week <= 2; week++)
                {
                    for (int i = 0; i < 400 && !(walk.Snapshot.week == week && walk.Snapshot.phase == EpisodePhase.Veto); i++)
                    {
                        Assert.That(walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot)).accepted, Is.True, "seed " + seed);
                        if (walk.Snapshot.week > week) break;
                    }
                    var s = walk.Snapshot;
                    if (s.week != week || s.phase != EpisodePhase.Veto || s.competitionResolved || s.hohId == s.playerId
                        || s.nominees.Contains(s.playerId) || s.Find(s.playerId).status != ContestantStatus.Active) continue;
                    foreach (string id in s.nominees)
                    {
                        s.Find(id).traits = new List<string> { "Emotional" };
                        Set(s, id, s.playerId, 50);
                        Set(s, s.playerId, id, ReplyCards.CloseReading - 1);
                    }
                    s.vetoHolderId = s.playerId;
                    if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                    s.competitionResolved = true;
                    s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                    var engine = new EpisodeEngine(s);
                    var meeting = engine.Apply(Command(s, EpisodeCommandKind.Advance));
                    if (!meeting.accepted) continue;
                    var closed = meeting.state.Clone();
                    EpisodeEngine.Close(closed);
                    var moved = meeting.state.deals.Where(d => RoleOffers.IsVetoAsk(meeting.state, d) && RoleOffers.ToneOf(meeting.state, d) == RoleTone.Earnest
                        && RoleOffers.ToneOf(closed, d) == RoleTone.Friendly).Select(d => d.proposerId).FirstOrDefault();
                    if (moved == null) continue;
                    asker = moved;
                    return engine;
                }
            }
            Assert.Fail("No seed's last beats moved a card.");
            asker = null;
            return null;
        }

        /// <summary>
        /// A season at Roles (or at Threads) walked to week one's veto competition with the player nominated by the Head
        /// of Household, then handed the veto; the other nominee warm enough to ask.
        /// </summary>
        private static EpisodeState NominatedHolder(bool roles, out string other)
        {
            for (uint seed = 7001; seed < 7121; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = roles ? StoryRules.Roles : StoryRules.Threads;
                var walk = new EpisodeEngine(start);
                for (int i = 0; i < 200 && walk.Snapshot.phase != EpisodePhase.Veto; i++)
                    Assert.That(walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot)).accepted, Is.True, "seed " + seed);
                var s = walk.Snapshot;
                if (s.phase != EpisodePhase.Veto || s.competitionResolved || !s.nominees.Contains(s.playerId)) continue;
                other = s.nominees.Single(id => id != s.playerId);
                Set(s, other, s.playerId, 50);
                s.vetoHolderId = s.playerId;
                if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
                s.competitionResolved = true;
                s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
                return s;
            }
            Assert.Fail("No seed nominated the player in week one.");
            other = null;
            return null;
        }

        /// <summary>
        /// The showmance's "On the Block" beat with <paramref name="partner"/> as the partner, put to the player as the arc
        /// draws it - the promise of the veto and the lapse, each with its catalogue effects - for a test to answer. With
        /// <paramref name="theirs"/> each effect's two parties change places: the partner gives the player their word.
        /// </summary>
        private static string PromiseBeat(EpisodeState s, string partner, bool theirs = false) =>
            StoryBeat(s, "late-nights", "on-the-block", "PARTNER", partner, theirs, "veto-board", "A promise.");

        /// <summary>
        /// Call in the Favour's beat with <paramref name="holder"/> as the one who owes the player, put to the player as the
        /// arc draws it - asking for the veto and saving the favour, each with its catalogue effects - for a test to answer.
        /// </summary>
        private static string FavourBeat(EpisodeState s, string holder) =>
            StoryBeat(s, "call-in-the-favour", "favour", "HOLDER", holder, false, "favour", "A favour.", "veto", "save-it");

        /// <summary>
        /// A story beat of <paramref name="templateId"/>'s with its one role cast as <paramref name="who"/>, running and put
        /// to the player as the arc draws it: the named options (every one, without names) with their catalogue effects,
        /// the player and <paramref name="who"/> changing places in each with <paramref name="theirs"/>. Returns its event's id.
        /// </summary>
        internal static string StoryBeat(EpisodeState s, string templateId, string beatId, string role, string who, bool theirs, string name,
            string description, params string[] optionIds)
        {
            var template = StoryCatalog.Find(templateId);
            var beat = template.Beat(beatId);
            var cast = new List<StoryRoleState> { new StoryRoleState { role = role, contestantId = who } };
            string cycleId = "story-role-" + name, eventId = "house-event-role-" + name;
            s.storylines.Add(new StorylineState
            {
                id = cycleId, templateId = template.id, category = template.eyebrow, title = template.title, eventId = eventId,
                status = StorylineStatus.Active, week = s.week, beatId = beat.id, lane = template.lane, cast = cast.Select(r => r.Clone()).ToList(),
            });
            string Who(string part) => (part == Fx.Player) != theirs ? s.playerId : who;
            StoryEffectState Resolve(Fx fx) => new StoryEffectState
            {
                kind = fx.kind, fromId = Who(fx.from), toId = Who(fx.to), type = fx.type, text = fx.text, amount = fx.amount, weeks = fx.weeks,
            };
            s.houseEvents.Add(new HouseEventState
            {
                id = eventId, kind = HouseEventKind.Story, title = beat.title, narrative = beat.summary, involvedIds = new List<string> { who },
                week = s.week, chosenIndex = -1, contentId = template.id + ":" + beat.id, cycleId = cycleId, closesAnchor = beat.closes,
                surface = beat.surface, venue = beat.venue, lapseOptionId = beat.lapse, cast = cast,
                choices = beat.options.Where(option => optionIds.Length == 0 || optionIds.Contains(option.id)).Select(option => new HouseEventChoice
                {
                    label = option.label, description = description, optionId = option.id, lapse = option.id == beat.lapse, next = option.next,
                    effects = option.effects.Select(Resolve).ToList(),
                }).ToList(),
            });
            return eventId;
        }

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>This week's veto ask from <paramref name="who"/>.</summary>
        internal static DealState AskOf(EpisodeState s, string who) => s.deals.Single(d => RoleOffers.IsVetoAsk(s, d) && d.proposerId == who);

        /// <summary>This week's ledger records of a type from one houseguest about another.</summary>
        internal static List<RelationshipEventState> Events(EpisodeState s, string from, string to, string type) =>
            s.relationships.Where(r => r.fromId == from && r.toId == to).SelectMany(r => r.events).Where(e => e.type == type && e.week == s.week).ToList();

        /// <summary>Every ledger record of a type, with the pair it is between.</summary>
        private static List<(RelationshipEventState record, string[] edge)> AllEvents(EpisodeState s, string type) =>
            s.relationships.SelectMany(r => r.events.Where(e => e.type == type).Select(e => (e, new[] { r.fromId, r.toId }))).ToList();

        internal static EpisodeState Apply(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + " " + command.targetId + ": " + result.reason);
            return result.state;
        }

        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "role-veto-" + kind + "-" + s.revision + "-" + target + "-" + text, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>The player's free answer to an offer.</summary>
        internal static EpisodeCommand Answer(EpisodeState s, string offerId, bool accept) =>
            Command(s, EpisodeCommandKind.RespondToDeal, offerId, text: accept ? EpisodeEngine.AcceptDeal : "decline");

        /// <summary>The player's veto decision: used on <paramref name="saved"/>, or not used.</summary>
        internal static EpisodeCommand Decide(EpisodeState s, bool use, string saved)
        {
            var command = Command(s, EpisodeCommandKind.ResolveVeto, saved, EpisodeEngine.ReplacementCandidates(s).FirstOrDefault()?.id);
            command.useVeto = use;
            return command;
        }

        internal static EpisodeCommand Progress(EpisodeState s, string eventId, string optionId) =>
            Command(s, EpisodeCommandKind.ProgressStoryline, eventId, optionId);
    }
}
