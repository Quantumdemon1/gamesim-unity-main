using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The objective's title in words (UI-UX plan U1): every phase and role names a task in at most
    /// <see cref="ObjectiveWords.MaxLength"/> characters, the window after a phase's work is done says
    /// what came of it rather than the task already settled, and a player out of the game watches.
    /// The endgame's titles live in <c>EpisodeHud.ObjectiveTitle</c>, ahead of this table and outside
    /// its cap ("The final Head of Household decides" is 35 characters); the PlayMode Endgame partial
    /// pins those. Unity-free, so the dotnet subset runs it (Tools/SimulationTests).
    /// </summary>
    public sealed class ObjectiveWordsTests
    {
        /// <summary>The phases of a regular week: each names a task for an active player.</summary>
        private static readonly EpisodePhase[] Regular =
        {
            EpisodePhase.Social, EpisodePhase.HoH, EpisodePhase.Nomination, EpisodePhase.VetoSelection,
            EpisodePhase.Veto, EpisodePhase.VetoMeeting, EpisodePhase.Campaign, EpisodePhase.Eviction,
        };

        private static readonly string[] RoleNames = { "HoH", "nominee", "veto player", "holder", "other", "evicted" };

        /// <summary>An eight-person season in <paramref name="phase"/> with no role and no window set.</summary>
        private static EpisodeState Week(EpisodePhase phase)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice(), 23);
            s.phase = phase;
            s.hohId = null; s.previousHohId = null; s.vetoHolderId = null;
            s.nominees = new List<string>(); s.vetoPlayers = new List<string>(); s.votes = new List<VoteState>();
            s.competitionResolved = false; s.vetoResolved = false; s.evictionResolved = false;
            s.evictionStage = EvictionStage.Interaction;
            return s;
        }

        private static string Other(EpisodeState s, int n) => s.contestants.Where(c => !c.isPlayer).Skip(n).First().id;

        /// <summary>The player in <paramref name="role"/>, the other roles held by housemates.</summary>
        private static EpisodeState WithRole(EpisodePhase phase, string role)
        {
            var s = Week(phase);
            string me = s.playerId;
            s.hohId = Other(s, 0);
            switch (role)
            {
                case "HoH": s.hohId = me; break;
                case "nominee": s.nominees = new List<string> { me, Other(s, 1) }; break;
                case "veto player": s.vetoPlayers = new List<string> { me, Other(s, 1), Other(s, 2) }; break;
                case "holder": s.vetoHolderId = me; break;
                case "evicted": s.Find(me).status = ContestantStatus.Evicted; break;
            }
            return s;
        }

        [Test]
        public void EveryPhaseAndRoleNamesSomethingThatFitsTheBox()
        {
            foreach (EpisodePhase phase in Enum.GetValues(typeof(EpisodePhase)))
            foreach (var role in RoleNames)
            {
                string words = ObjectiveWords.For(WithRole(phase, role));
                Assert.That(words, Is.Not.Null.And.Not.Empty, phase + " as " + role);
                Assert.That(words.Length, Is.LessThanOrEqualTo(ObjectiveWords.MaxLength), phase + " as " + role + ": '" + words + "'");
            }
            Assert.That(ObjectiveWords.Fallback.Length, Is.LessThanOrEqualTo(ObjectiveWords.MaxLength));
        }

        [Test]
        public void NoRegularPhaseShowsTheFallbackToAnActivePlayer()
        {
            foreach (var phase in Regular)
            foreach (var role in RoleNames.Where(role => role != "evicted"))
                Assert.That(ObjectiveWords.For(WithRole(phase, role)), Is.Not.EqualTo(ObjectiveWords.Fallback), phase + " as " + role);
        }

        [Test]
        public void TheHeadOfHouseholdAndAHousemateReadDifferentTasksAtNominations()
        {
            string hoh = ObjectiveWords.For(WithRole(EpisodePhase.Nomination, "HoH"));
            string other = ObjectiveWords.For(WithRole(EpisodePhase.Nomination, "other"));
            Assert.That(hoh, Is.EqualTo("Name your nominees"));
            Assert.That(other, Is.EqualTo("The HoH is naming nominees"));
        }

        [Test]
        public void ANomineeAndAVoterReadDifferentTasksAtTheEviction()
        {
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Eviction, "nominee")), Is.EqualTo("Face the house vote"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Eviction, "other")), Is.EqualTo("Cast your eviction vote"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Eviction, "HoH")), Is.EqualTo("Break a tie if needed"));
        }

        [Test]
        public void TheOpenTasksByRole()
        {
            Assert.That(ObjectiveWords.For(Week(EpisodePhase.Social)), Is.EqualTo("Make this week's moves"));
            Assert.That(ObjectiveWords.For(Week(EpisodePhase.HoH)), Is.EqualTo("Win Head of Household"));
            var outgoing = Week(EpisodePhase.HoH); outgoing.previousHohId = outgoing.playerId;
            Assert.That(ObjectiveWords.For(outgoing), Is.EqualTo("Watch the HoH competition"), "The outgoing HoH cannot play.");
            Assert.That(ObjectiveWords.For(Week(EpisodePhase.VetoSelection)), Is.EqualTo("The veto players are drawn"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Veto, "veto player")), Is.EqualTo("Win the Power of Veto"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Veto, "other")), Is.EqualTo("Watch the veto competition"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.VetoMeeting, "holder")), Is.EqualTo("Decide on the veto"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.VetoMeeting, "other")), Is.EqualTo("The veto holder decides"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Campaign, "nominee")), Is.EqualTo("Campaign to stay"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Campaign, "HoH")), Is.EqualTo("Hear the campaign"));
            Assert.That(ObjectiveWords.For(WithRole(EpisodePhase.Campaign, "other")), Is.EqualTo("Choose your vote"));
        }

        /// <summary>
        /// A housemate holds the veto and will use it: the meeting waits on the HoH to name the
        /// replacement, so the HoH's card says so (ruling L1a). Where the veto saves nobody, the holder
        /// still decides; a player who is both HoH and holder decides on the veto first.
        /// </summary>
        [Test]
        public void AtTheVetoMeetingTheHoHNamesTheReplacementWhenAHousemateUsesTheVeto()
        {
            var saving = WithRole(EpisodePhase.VetoMeeting, "HoH");
            saving.nominees = new List<string> { Other(saving, 1), Other(saving, 2) };
            saving.vetoHolderId = Other(saving, 1);
            Assert.That(EpisodeEngine.NpcVetoSave(saving), Is.EqualTo(Other(saving, 1)), "Precondition: a holder on the block saves themself.");
            string words = ObjectiveWords.For(saving);
            Assert.That(words, Is.EqualTo("Name a replacement nominee"));
            Assert.That(words.Length, Is.LessThanOrEqualTo(ObjectiveWords.MaxLength), "'" + words + "' fits the box.");
            AssertWindow(saving, s => s.vetoResolved = true, "The veto meeting is over");

            // The Final 4 lock: a holder off the block may not use the veto, so it saves nobody.
            var locked = WithRole(EpisodePhase.VetoMeeting, "HoH");
            locked.nominees = new List<string> { Other(locked, 1), Other(locked, 2) };
            locked.vetoHolderId = Other(locked, 0);
            foreach (var gone in locked.contestants.Where(c => !c.isPlayer && c.id != Other(locked, 0)
                && c.id != Other(locked, 1) && c.id != Other(locked, 2)).ToList())
                gone.status = ContestantStatus.Evicted;
            Assert.That(locked.Active.Count(), Is.EqualTo(4), "Precondition: the Final 4.");
            Assert.That(EpisodeEngine.NpcVetoSave(locked), Is.Null, "Precondition: the veto saves nobody.");
            Assert.That(ObjectiveWords.For(locked), Is.EqualTo("The veto holder decides"));

            var both = WithRole(EpisodePhase.VetoMeeting, "HoH");
            both.nominees = new List<string> { Other(both, 1), Other(both, 2) };
            both.vetoHolderId = both.playerId;
            Assert.That(ObjectiveWords.For(both), Is.EqualTo("Decide on the veto"), "The holder's own decision comes first.");
        }

        /// <summary>A phase's done window, from a state with the task open, cloned with the flag set.</summary>
        private static void AssertWindow(EpisodeState open, Action<EpisodeState> settle, string expected)
        {
            string task = ObjectiveWords.For(open);
            var done = open.Clone();
            settle(done);
            string words = ObjectiveWords.For(done);
            Assert.That(words, Is.EqualTo(expected), "The window after '" + task + "'.");
            Assert.That(words.Length, Is.LessThanOrEqualTo(ObjectiveWords.MaxLength), "The window's words fit the box: '" + words + "'.");
            Assert.That(words, Is.Not.EqualTo(task), "The card no longer asks for the settled task.");
            Assert.That(ObjectiveWords.For(open), Is.EqualTo(task), "The open state itself is untouched.");
        }

        [Test]
        public void EachDoneWindowSaysWhatCameOfItNotTheTask()
        {
            var hoh = Week(EpisodePhase.HoH);
            AssertWindow(hoh, s => { s.competitionResolved = true; s.hohId = Other(s, 0); }, "The new HoH is crowned");
            AssertWindow(hoh, s => { s.competitionResolved = true; s.hohId = s.playerId; }, "You are Head of Household");

            var veto = WithRole(EpisodePhase.Veto, "veto player");
            AssertWindow(veto, s => { s.competitionResolved = true; s.vetoHolderId = Other(s, 1); }, "The veto is won");
            AssertWindow(veto, s => { s.competitionResolved = true; s.vetoHolderId = s.playerId; }, "You hold the Power of Veto");

            AssertWindow(WithRole(EpisodePhase.Nomination, "other"), s => s.nominees = new List<string> { Other(s, 1), Other(s, 2) }, "Nominees are named");
            AssertWindow(WithRole(EpisodePhase.Nomination, "HoH"), s => s.nominees = new List<string> { Other(s, 1), Other(s, 2) }, "Nominees are named");
            AssertWindow(WithRole(EpisodePhase.Nomination, "other"), s => s.nominees = new List<string> { s.playerId, Other(s, 2) }, "You are nominated");

            AssertWindow(WithRole(EpisodePhase.VetoMeeting, "holder"), s => s.vetoResolved = true, "The veto meeting is over");

            var vote = WithRole(EpisodePhase.Eviction, "other");
            vote.nominees = new List<string> { Other(vote, 1), Other(vote, 2) };
            vote.evictionStage = EvictionStage.Voting;
            AssertWindow(vote, s => s.votes.Add(new VoteState { voterId = s.playerId, targetId = Other(s, 1) }), "Your vote is in");
            AssertWindow(vote, s => s.evictionResolved = true, "The house has voted");
            AssertWindow(vote, s => s.evictionStage = EvictionStage.Results, "The house has voted");
            // The house's vote outranks the player's own ballot: once it is in, the card says so.
            var voted = vote.Clone();
            voted.votes.Add(new VoteState { voterId = voted.playerId, targetId = Other(voted, 1) });
            AssertWindow(voted, s => s.evictionResolved = true, "The house has voted");
            AssertWindow(voted, s => s.evictionStage = EvictionStage.Results, "The house has voted");

            var tie = WithRole(EpisodePhase.Eviction, "HoH");
            AssertWindow(tie, s => s.evictionStage = EvictionStage.Tiebreaker, "Break the tie");
            // The tie is the HoH's alone: a nominee at the tiebreak still faces the vote.
            var tied = WithRole(EpisodePhase.Eviction, "nominee");
            tied.evictionStage = EvictionStage.Tiebreaker;
            Assert.That(ObjectiveWords.For(tied), Is.EqualTo("Face the house vote"), "Only the HoH breaks the tie.");
        }

        /// <summary><paramref name="s"/> as a story season whose player serves production's sit-out penalty this week.</summary>
        private static EpisodeState SittingOut(EpisodeState s)
        {
            EpisodeEngine.EnableStory(s);
            Production.For(s, s.playerId, true).sitsOutWeek = s.week;
            return s;
        }

        private static bool Fielded(EpisodeState s) => EpisodeEngine.CompetitionPlayers(s).Any(c => c.id == s.playerId);

        /// <summary>
        /// A player serving production's penalty sits out the Head of Household competition
        /// (EpisodeEngine.CompetitionPlayers leaves them out, and the competition panel briefs them as a
        /// spectator), so the card says to watch it, as it does for the outgoing HoH, and never to win it.
        /// </summary>
        [Test]
        public void APlayerSittingOutProductionsPenaltyWatchesTheHoHCompetition()
        {
            var story = Week(EpisodePhase.HoH);
            EpisodeEngine.EnableStory(story);
            Assert.That(Fielded(story), Is.True, "Precondition: a story season fields a player without a penalty.");
            Assert.That(ObjectiveWords.For(story), Is.EqualTo("Win Head of Household"));

            var sitting = SittingOut(Week(EpisodePhase.HoH));
            Assert.That(Production.SitsOut(sitting, sitting.playerId), Is.True, "Precondition: the penalty is this week's.");
            Assert.That(Fielded(sitting), Is.False, "Precondition: the engine leaves the player out of the field.");
            Assert.That(ObjectiveWords.For(sitting), Is.EqualTo("Watch the HoH competition"));

            // The penalty is a week's: one served last week leaves the player in this week's field.
            var served = SittingOut(Week(EpisodePhase.HoH));
            served.week++;
            Assert.That(Fielded(served), Is.True, "Precondition: last week's penalty is spent.");
            Assert.That(ObjectiveWords.For(served), Is.EqualTo("Win Head of Household"));

            // Once the competition is won the window names the result, penalty or not.
            AssertWindow(sitting, s => { s.competitionResolved = true; s.hohId = Other(s, 0); }, "The new HoH is crowned");
        }

        /// <summary>
        /// Production's penalty is the HoH competition's alone: at the veto the draw decides who plays, so
        /// a drawn player under this week's penalty is still asked to win it, and one the draw left out
        /// (or a veto with no draw recorded) watches.
        /// </summary>
        [Test]
        public void AtTheVetoTheDrawDecidesPenaltyOrNot()
        {
            var drawn = SittingOut(WithRole(EpisodePhase.Veto, "veto player"));
            Assert.That(Production.SitsOut(drawn, drawn.playerId), Is.False, "Precondition: the penalty does not reach the veto.");
            Assert.That(Fielded(drawn), Is.True, "Precondition: the draw fields the player.");
            Assert.That(ObjectiveWords.For(drawn), Is.EqualTo("Win the Power of Veto"));

            var left = SittingOut(WithRole(EpisodePhase.Veto, "other"));
            Assert.That(ObjectiveWords.For(left), Is.EqualTo("Watch the veto competition"));

            var undrawn = Week(EpisodePhase.Veto);
            undrawn.vetoPlayers = null;
            Assert.That(ObjectiveWords.For(undrawn), Is.EqualTo("Watch the veto competition"), "No draw, no field.");
        }

        /// <summary>
        /// The card and the engine agree on every open competition the table names: it asks the player to
        /// win exactly when EpisodeEngine.CompetitionPlayers fields them, for each role, the outgoing HoH
        /// and the penalty included.
        /// </summary>
        [Test]
        public void TheCardAsksToWinExactlyWhenTheEngineFieldsThePlayer()
        {
            int fielded = 0, watching = 0;
            foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
            foreach (var role in RoleNames.Where(role => role != "evicted"))
            foreach (var setup in new Func<EpisodeState, EpisodeState>[]
                     {
                         s => s,
                         SittingOut,
                         s => { s.previousHohId = s.playerId; return s; },
                     })
            {
                var s = setup(WithRole(phase, role));
                bool inField = Fielded(s);
                string words = ObjectiveWords.For(s);
                Assert.That(words.StartsWith("Win "), Is.EqualTo(inField), phase + " as " + role + ": '" + words + "'");
                Assert.That(words.StartsWith("Watch "), Is.EqualTo(!inField), phase + " as " + role + ": '" + words + "'");
                if (inField) fielded++; else watching++;
            }
            Assert.That(fielded, Is.GreaterThan(0), "Some cases field the player.");
            Assert.That(watching, Is.GreaterThan(0), "Some cases leave the player out.");
        }

        [Test]
        public void APlayerOutOfTheGameWatchesEvenWithARoleLeftInState()
        {
            var lingering = WithRole(EpisodePhase.Nomination, "HoH");
            lingering.Find(lingering.playerId).status = ContestantStatus.Evicted;
            Assert.That(ObjectiveWords.For(lingering), Is.EqualTo("Watch the week unfold"), "An evicted HoH id names no task.");

            var noPlayer = Week(EpisodePhase.Campaign);
            noPlayer.playerId = "nobody";
            Assert.That(ObjectiveWords.For(noPlayer), Is.EqualTo("Watch the week unfold"));

            Assert.That(ObjectiveWords.For(null), Is.EqualTo(ObjectiveWords.Fallback));
        }
    }
}
