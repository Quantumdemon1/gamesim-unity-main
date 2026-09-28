using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The read on screen (STRATEGY-LOOP-PLAN.md §2): the notebook's read tab, the campaign board's
    /// line under each voter, and the question asked in a conversation, free and once a week.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A season at the campaign, with two on the block and the veto settled.</summary>
        private IEnumerator InstallCampaign(uint seed, System.Action<EpisodeState> more = null) => InstallStrategySeason(seed, state =>
        {
            AtVetoMeeting(state, false);
            state.phase = EpisodePhase.Campaign;
            state.vetoResolved = true;
            more?.Invoke(state);
        });

        /// <summary>Every houseguest at the given standing toward the player: a strategist who is not close keeps the vote to themselves.</summary>
        private static void WarmToPlayer(EpisodeState state, double score)
        {
            foreach (var npc in state.Active.Where(c => !c.isPlayer))
            {
                var row = state.relationships.FirstOrDefault(r => r.fromId == npc.id && r.toId == state.playerId);
                if (row == null) state.relationships.Add(row = new RelationshipState { fromId = npc.id, toId = state.playerId });
                row.score = score;
            }
        }

        /// <summary>The card the notebook's read tab draws for a voter, after opening the tab.</summary>
        private IEnumerator OpenReadTab()
        {
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes);
            yield return null;
            ButtonWithCaption(EpisodeHud.VoteReadTabCaption).onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private string ReadCardWords(string voterName)
        {
            var card = ActiveRect(EpisodeHud.VoteReadCardPrefix + voterName);
            Assert.That(card, Is.Not.Null, voterName + " has a read card.");
            return string.Join("\n", card.GetComponentsInChildren<TMP_Text>().Select(t => t.text));
        }

        [UnityTest]
        public IEnumerator VoteRead_TheNotebookReadsTheHouseDuringACampaign()
        {
            yield return InstallCampaign(46);
            var state = director.Snapshot;
            Assume.That(VoteRead.Available(state), "The fixture is a campaign with two on the block.");
            Assume.That(state.ledger.standings, Is.Empty, "and nothing has been learned yet.");
            int revision = state.revision;
            yield return OpenReadTab();
            var whip = ActiveRect(EpisodeHud.WhipCountName);
            Assert.That(whip, Is.Not.Null, "The whip count leads the page.");
            Assert.That(string.Join("\n", whip.GetComponentsInChildren<TMP_Text>().Select(t => t.text)), Does.Contain("No read on the house yet"));
            var voters = EpisodeEngine.Voters(state).Where(v => !v.isPlayer).ToList();
            Assert.That(voters, Is.Not.Empty);
            foreach (var voter in voters)
            {
                var words = ReadCardWords(voter.name);
                Assert.That(words, Does.Contain("No read yet"), "Nothing learned, nothing read: " + voter.name);
                Assert.That(words, Does.Contain("you don't know could change this"), "and the card says how much it cannot see.");
            }
            Assert.That(ActiveRect(EpisodeHud.VotesPrivacyName), Is.Not.Null, "The page says a read is not a ballot.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading the read commits nothing.");
            if (Application.isBatchMode) yield return CaptureFraming("notebook-vote-read");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator VoteRead_TheReadTabIsOnlyThereWhileThereIsAVoteToRead()
        {
            yield return InstallStrategySeason(46, state => { state.phase = EpisodePhase.Social; });
            Assume.That(VoteRead.Available(director.Snapshot), Is.False);
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes);
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.VoteReadTabCaption), Is.Null, "Nothing to read, no tab.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator VoteRead_TheCampaignBoardCarriesEachVotersRead()
        {
            yield return InstallCampaign(46);
            yield return OpenStation();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var sheet = VoteRead.Read(state);
            var grid = ActiveRect(EpisodeHud.CampaignVotersName);
            Assert.That(grid, Is.Not.Null, "The votes are a grid of cards.");
            var lines = grid.GetComponentsInChildren<TMP_Text>().Where(t => t.name == EpisodeHud.VoteReadLineName).ToList();
            Assert.That(lines, Has.Count.EqualTo(sheet.voters.Count), "One read line per voter.");
            Assert.That(lines.Select(t => t.text), Is.EquivalentTo(sheet.voters.Select(r => EpisodeDirector.ReadHeadline(state, r))),
                "Each line is that voter's read.");
            Assert.That(lines.Select(t => t.text), Has.All.EqualTo("No read yet"), "and nothing has been learned yet.");
            foreach (var line in lines)
            {
                line.ForceMeshUpdate();
                Assert.That(line.isTextOverflowing, Is.False, "'" + line.text + "' fits its box.");
            }
            var button = ButtonWithCaptionOrNull(EpisodeHud.CastTalkCaption((state.Find(sheet.voters[0].voterId).name ?? "").Split(' ')[0]));
            Assert.That(button, Is.Not.Null, "The talk button is still under the line.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator VoteRead_AskingAVoterStraightIsFreeOnceAWeekAndTheReadShowsWhatTheySaid()
        {
            yield return InstallCampaign(46, state => WarmToPlayer(state, 30));
            director.ClosePanels();
            yield return null;
            var state = director.Snapshot;
            var bodies = SceneComponents<HouseNpc>().Where(n => n.gameObject.activeInHierarchy).ToList();
            var voter = EpisodeEngine.Voters(state).FirstOrDefault(v => !v.isPlayer && bodies.Any(n => n.Id == v.id)
                && !(v.traits.Contains("Strategic") && !v.traits.Contains("Loyal") && state.Score(v.id, state.playerId) < 25));
            Assume.That(voter, Is.Not.Null, "A voter with a body who will answer; voters: " + string.Join("; ",
                EpisodeEngine.Voters(state).Where(v => !v.isPlayer).Select(v => v.name + " " + string.Join("/", v.traits) + " view "
                    + state.Score(v.id, state.playerId) + (bodies.Any(n => n.Id == v.id) ? " body" : " no body"))));
            var npc = bodies.Single(n => n.Id == voter.id);
            WarpPlayer(npc.transform.position + (player.transform.position - npc.transform.position).normalized * 1.4f);
            yield return null;
            Assert.That(director.TryOpenNpc(voter.id), Is.True, "The conversation opens.");
            yield return null;
            int spent = EpisodeEngine.SocialActionsSpent(director.Snapshot);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReadPersonCaption), Is.Not.Null, "A look is on offer too.");
            ButtonWithCaption(EpisodeHud.AskVoteCaption).onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            var claim = after.ledger.claims.FirstOrDefault(k => k.voterId == voter.id && k.week == after.week && k.source == ClaimSource.Told);
            Assert.That(claim, Is.Not.Null, "They answered, and it is on the record.");
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(spent), "Asking is free.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskVoteCaption), Is.Null, "and once a week.");
            director.ClosePanels();
            yield return null;

            yield return OpenReadTab();
            Assert.That(ReadCardWords(voter.name), Does.Contain("Told you: evict " + after.Find(claim.targetId).name), "The notebook keeps what they said.");
            director.ClosePanels();
            yield return null;

            yield return OpenStation();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var grid = ActiveRect(EpisodeHud.CampaignVotersName);
            var line = grid.GetComponentsInChildren<TMP_Text>()
                .Single(t => t.name == EpisodeHud.VoteReadLineName && t.transform.parent.name == "Voter · " + voter.name);
            var read = VoteRead.Read(director.Snapshot).voters.Single(r => r.voterId == voter.id);
            Assert.That(line.text, Is.EqualTo(EpisodeDirector.ReadHeadline(director.Snapshot, read)), "The board carries the read.");
            Assert.That(line.text, Does.Contain(after.Find(claim.targetId).name), "and what they said is in it.");
            director.ClosePanels();
            yield return null;
        }
    }
}
