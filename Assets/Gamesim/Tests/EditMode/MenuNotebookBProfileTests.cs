using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 3, part b: a houseguest's profile holds what the game shows about them (ruling N6 i),
    /// any name on a reading page opens it with a way back to that page (N6 ii), and the player has a
    /// "You" card of the same frame (N6 iv). The profile gathers from the places that already show it,
    /// by their own readers and words, so it says nothing they would not - a case per place - and
    /// nothing the player is not allowed to know: a ballot they have not learned, a pact no word of
    /// has reached them, a houseguest's own motive. EditMode: the builders are the director's.
    /// </summary>
    public sealed class MenuNotebookBProfileTests
    {
        /// <summary>A whole season of eight, played to its end, with claims and promises on the record as BallotPrivacyTests plays it.</summary>
        private static EpisodeState Played(uint seed)
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed));
            for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var now = engine.Snapshot;
                if (now.phase == EpisodePhase.Eviction && now.evictionStage == EvictionStage.Voting && !now.evictionResolved && now.votes.Count == 0 && now.nominees.Count == 2)
                {
                    var voters = EpisodeEngine.Voters(now).Where(v => !v.isPlayer).ToArray();
                    if (voters.Length >= 2)
                    {
                        string first = EpisodeEngine.ProjectBallot(now, voters[0].id).selectedNomineeId;
                        now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[0].id, targetId = first, source = ClaimSource.Told });
                    }
                    if (voters.Length >= 3 && !now.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == voters[2].id && p.toId == now.playerId && p.kind == PromiseKind.Vote))
                    {
                        string third = EpisodeEngine.ProjectBallot(now, voters[2].id).selectedNomineeId;
                        now.promises.Add(new PromiseState { id = "profile-promise-" + now.week, fromId = voters[2].id, toId = now.playerId, targetId = now.nominees.First(id => id != third),
                            kind = PromiseKind.Vote, status = PromiseStatus.Active, week = now.week, expiresWeek = now.week });
                    }
                    engine = new EpisodeEngine(now);
                }
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True);
            }
            return engine.Snapshot;
        }

        private static List<ContestantState> Others(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).ToList();

        [Test]
        public void TheProfilesNotes_AreTheNotesPagesLines_LessWhatItsRecordsAndMemoriesSay()
        {
            var s = Played(4101);
            int drawn = 0;
            foreach (var actor in Others(s))
            {
                var source = HouseguestNotes.For(s, actor.id);
                var lines = EpisodeDirector.ProfileNotesLines(s, actor.id, out int more);
                var expected = source.Where(n => n.kind != HouseguestNotes.Kinds.Word && n.kind != HouseguestNotes.Kinds.Memory)
                    .Select(n => n.ToString()).ToList();
                Assert.That(lines, Is.EqualTo(expected.Take(EpisodeDirector.ProfileLinesShown).ToList()), actor.name + ": the notes card's lines, as it prints them.");
                Assert.That(more, Is.EqualTo(System.Math.Max(0, expected.Count - EpisodeDirector.ProfileLinesShown)));
                Assert.That(lines, Is.SubsetOf(source.Select(n => n.ToString()).ToList()), actor.name + ": nothing the notes would not say.");
                drawn += lines.Count;
            }
            Assert.That(drawn, Is.GreaterThan(0), "The season's claims put something on the profiles.");
        }

        [Test]
        public void TheProfilesBallots_AreTheVotePagesKnownOnes_AndNoUnknownBallotIsTold()
        {
            var s = Played(4102);
            var book = VoteRecords.Read(s);
            foreach (var actor in Others(s))
            {
                var lines = EpisodeDirector.ProfileVoteLines(s, actor.id);
                var known = book.Records.SelectMany(r => r.Ballots.Where(b => b.Known && b.VoterId == actor.id).Select(b => (r.Week, b))).ToList();
                Assert.That(lines, Has.Count.EqualTo(known.Count), actor.name + ": one line a ballot of theirs the vote page shows you.");
                for (int i = 0; i < known.Count; i++)
                {
                    Assert.That(lines[i], Does.StartWith("Week " + known[i].Week + " \u00b7 "), actor.name);
                    Assert.That(lines[i], Does.Contain(known[i].b.AgainstPlayer ? "you" : known[i].b.TargetName), actor.name + ": the page's words for it.");
                }
            }
            // The true ballots, which the engine logs to their voters alone: one the vote page does not show is not on the profile.
            var secret = BallotPrivacyTests.PrivateBallots(s);
            Assert.That(secret, Is.Not.Empty, "The season's ballots are on its record.");
            int hidden = 0;
            foreach (var (week, voter, _) in secret)
            {
                if (book.Records.Any(r => r.Week == week && r.Ballots.Any(b => b.Known && b.VoterId == voter))) continue;
                hidden++;
                Assert.That(EpisodeDirector.ProfileVoteLines(s, voter).Any(l => l.StartsWith("Week " + week + " \u00b7 ", System.StringComparison.Ordinal)), Is.False,
                    "A week " + week + " ballot you have not learned is not on its voter's profile.");
            }
            Assert.That(hidden, Is.GreaterThan(0), "Some ballots stay private, so the rule is tried.");
        }

        [Test]
        public void TheProfilesHeardAlliances_AreTheAlliancesPagesOwn_AndAPactNoWordReachedIsNotThere()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4103);
            s.week = 4;
            var others = Others(s);
            // A pact between two others that no word of has reached the player.
            s.alliances.Add(new AllianceState { id = "quiet", name = "The Quiet Pact", active = true, members = new List<string> { others[0].id, others[1].id } });
            foreach (var actor in others)
            {
                var lines = EpisodeDirector.ProfileHeardAllianceLines(s, actor.id);
                var heard = AllianceRead.Read(s).suspected.Count(p => p.memberIds.Contains(actor.id));
                Assert.That(lines, Has.Count.EqualTo(heard), actor.name + ": only the pacts the alliances page shows you.");
            }
            Assert.That(EpisodeDirector.ProfileHeardAllianceLines(s, others[0].id), Is.Empty, "A private pact is not on the profile.");
            Assert.That(string.Join("\n", EpisodeDirector.ProfileHeardAllianceLines(s, others[1].id)), Does.Not.Contain("Quiet"));
        }

        [Test]
        public void SomeoneThePlayerKnowsLittleAbout_HasOnlyWhatThePagesShow()
        {
            var s = Played(4104);
            var stranger = Others(s).OrderBy(c => HouseguestNotes.For(s, c.id).Count).First();
            if (string.IsNullOrEmpty(stranger.motive)) stranger.motive = "a motive of their own the player never learns";
            var lines = EpisodeDirector.ProfileNotesLines(s, stranger.id, out _)
                .Concat(EpisodeDirector.ProfileVoteLines(s, stranger.id))
                .Concat(EpisodeDirector.ProfileHeardAllianceLines(s, stranger.id)).ToList();
            Assert.That(lines.Any(l => l.Contains(stranger.motive)), Is.False, "Their motive is theirs.");
            var said = HouseguestNotes.For(s, stranger.id).Select(n => n.ToString())
                .Concat(EpisodeDirector.ProfileVoteLines(s, stranger.id))
                .Concat(EpisodeDirector.ProfileHeardAllianceLines(s, stranger.id)).ToList();
            Assert.That(lines, Is.SubsetOf(said), "Every line is one a page already shows.");
        }

        [Test]
        public void TheYouCard_SaysOnlyWhatOtherScreensShowThePlayer()
        {
            var s = Played(4105);
            var ally = Others(s).First();
            s.alliances.Add(new AllianceState { id = "yours", name = "The Morning Pact", active = true, members = new List<string> { s.playerId, ally.id } });

            var week = EpisodeDirector.YourWeekLines(s);
            Assert.That(week.First(), Is.EqualTo("Week " + s.week + "  ·  " + EpisodeHud.PhaseShort(s.phase)), "The overview's week and phase.");
            string holds = EpisodeDirector.HouseStatus(s);
            if (holds != null) Assert.That(week, Does.Contain(holds), "The episode screen's who-holds-what.");

            var deals = EpisodeDirector.YourDealLines(s, out int more);
            var source = CommitmentsRead.Of(s).Select(CommitmentsRead.Line).ToList();
            Assert.That(source, Is.Not.Empty, "The season's promises are on the record.");
            Assert.That(deals, Is.SubsetOf(source), "Deals & promises' own lines.");
            Assert.That(deals.Count + more, Is.EqualTo(source.Count), "All of them, or how many more.");

            var alliances = EpisodeDirector.YourAllianceLines(s);
            var yours = AllianceRead.Read(s).yours;
            Assert.That(alliances, Has.Count.EqualTo(yours.Count));
            for (int i = 0; i < yours.Count; i++) Assert.That(alliances[i], Does.StartWith(yours[i].name), "Each pact by the alliances page's name for it.");
            Assert.That(alliances.Any(l => l.StartsWith("The Morning Pact", System.StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void AProfilesWayBack_NamesThePageItCameFrom()
        {
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(null), Is.EqualTo(EpisodeHud.ProfileBackCaption), "From anywhere else: the directory.");
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.People), Is.EqualTo(EpisodeHud.ProfileBackCaption));
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Notes), Is.EqualTo("Back to your notes"));
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Word), Is.EqualTo("Back to Deals & promises"));
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Alliances), Is.EqualTo("Back to Alliances"));
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Network), Is.EqualTo("Back to Relationships"));
            Assert.That(EpisodeDirector.ProfileBackCaptionFor(EpisodeDirector.NotebookSection.Votes), Is.EqualTo(EpisodeHud.ProfileBackCaption),
                "A page that opens no profile is no place to go back to.");
            foreach (var section in new[] { EpisodeDirector.NotebookSection.People, EpisodeDirector.NotebookSection.Notes, EpisodeDirector.NotebookSection.Word,
                         EpisodeDirector.NotebookSection.Alliances, EpisodeDirector.NotebookSection.Network })
                Assert.That(WayOut.IsWayBack(EpisodeDirector.ProfileBackCaptionFor(section)), Is.True, section);
        }
    }
}
