using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The houseguest directory as Refinement Kit 6 draws it: a row a houseguest, each saying
        /// name, status and your trust in columns that hold their line, filters that only choose
        /// rows, and a search that narrows the list as it is typed and survives a repaint.
        /// </summary>
        [UnityTest]
        public IEnumerator NotebookPeople_ColumnsFiltersAndSearch()
        {
            yield return SettleCast();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.People);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var state = director.Snapshot;
            var others = state.contestants.Where(c => !c.isPlayer).ToArray();

            var head = ActiveRect(EpisodeHud.NotebookHeaderName);
            Assert.That(head.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Does.Contain("Houseguests"));

            RectTransform[] Rows() => director.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name.StartsWith(EpisodeHud.RosterRowPrefix)).ToArray();
            var rows = Rows();
            Assert.That(rows.Select(r => r.name.Substring(EpisodeHud.RosterRowPrefix.Length)),
                Is.EquivalentTo(others.Select(c => c.name)), "Everyone but you, once each.");

            float? statusX = null, trustX = null;
            foreach (var row in rows)
            {
                var actor = others.Single(c => EpisodeHud.RosterRowPrefix + c.name == row.name);
                var cells = row.GetComponentsInChildren<TMP_Text>().ToArray();
                Assert.That(cells.Select(t => t.text), Does.Contain(actor.name));
                Assert.That(cells.Select(t => t.text), Does.Contain(EpisodeDirector.StatusWord(actor.status)));
                var trust = cells.Single(t => t.name == "Trust");
                Assert.That(trust.text, Is.EqualTo(EpisodeDirector.TrustFigure(state.Score(state.playerId, actor.id))),
                    actor.name + "'s row shows your own trust in them.");
                Assert.That(row.GetComponent<Button>(), Is.Not.Null, "A row opens its houseguest's profile.");

                // The columns hold their line from row to row.
                var status = row.Find("Status");
                float sx = status.position.x, tx = trust.rectTransform.position.x;
                if (statusX == null) { statusX = sx; trustX = tx; }
                Assert.That(sx, Is.EqualTo(statusX.Value).Within(.5f), "Status is one column.");
                Assert.That(tx, Is.EqualTo(trustX.Value).Within(.5f), "Your trust is one column.");

                foreach (var label in cells)
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its cell.");
                }
            }

            // A filter chooses rows, and commits nothing.
            int revision = state.revision;
            int active = others.Count(c => c.status == ContestantStatus.Active);
            int jury = others.Count(c => c.status == ContestantStatus.Jury);
            ButtonWithCaption("Active · " + active).onClick.Invoke();
            yield return null;
            Assert.That(Rows().Length, Is.EqualTo(active));
            ButtonWithCaption("Jury · " + jury).onClick.Invoke();
            yield return null;
            Assert.That(Rows().Length, Is.EqualTo(jury));
            if (jury == 0)
                Assert.That(ActiveRect(EpisodeHud.RosterEmptyName).GetComponent<TMP_Text>().text,
                    Is.EqualTo("No one is on the jury yet."), "An empty filter says so, truthfully.");
            ButtonWithCaption("All · " + others.Length).onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "A filter commits nothing.");

            // The search narrows in place, and keeps its words through a repaint.
            var target = others[others.Length - 1];
            string needle = target.name.Substring(0, Mathf.Min(4, target.name.Length)).ToLowerInvariant();
            var search = ActiveRect(EpisodeHud.RosterSearchName).GetComponent<TMP_InputField>();
            search.text = needle;
            yield return null;
            var matching = others.Where(c => c.name.ToLowerInvariant().Contains(needle)
                || (EpisodeDirector.CardLine(c) ?? "").ToLowerInvariant().Contains(needle)).Select(c => c.name);
            Assert.That(Rows().Select(r => r.name.Substring(EpisodeHud.RosterRowPrefix.Length)), Is.EquivalentTo(matching));
            ButtonWithCaption("Active · " + active).onClick.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.RosterSearchName).GetComponent<TMP_InputField>().text, Is.EqualTo(needle),
                "A repaint keeps what was typed.");
            ButtonWithCaption("All · " + others.Length).onClick.Invoke();
            yield return null;
            search = ActiveRect(EpisodeHud.RosterSearchName).GetComponent<TMP_InputField>();
            search.text = "zzzz";
            yield return null;
            Assert.That(Rows(), Is.Empty);
            Assert.That(ActiveRect(EpisodeHud.RosterEmptyName).GetComponent<TMP_Text>().text, Does.Contain("zzzz"));
            search.text = string.Empty;
            yield return null;
            Assert.That(Rows().Length, Is.EqualTo(others.Length));
            Assert.That(ActiveRect(EpisodeHud.RosterEmptyName), Is.Null);

            Assert.That(ButtonWithCaption("House activities"), Is.Not.Null, "The page's secondary action is in its foot.");
            if (Application.isBatchMode) yield return CaptureFraming("notebook-people");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A row opens that houseguest's profile, still on the Houseguests page: who they are, your
        /// own trust in them with what that number is not, the records you hold - and nothing
        /// private to them. "Back to Houseguests" and the rail both return to the directory.
        /// </summary>
        [UnityTest]
        public IEnumerator NotebookPeople_ARowOpensTheProfileAndBackReturns()
        {
            yield return SettleCast();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.People);
            yield return null;
            var state = director.Snapshot;
            var actor = state.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            int revision = state.revision;

            ActiveRect(EpisodeHud.RosterRowPrefix + actor.name).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(director.ProfileId, Is.EqualTo(actor.id));
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.People), "A profile is a view of the page.");
            var lit = ActiveRect(IconRail.RootName).GetComponentsInChildren<RectTransform>()
                .Where(t => t.name == IconRail.ActiveMarkName).ToArray();
            Assert.That(lit, Has.Length.EqualTo(1));
            Assert.That(lit[0].parent.name, Is.EqualTo("Houseguests"));
            Assert.That(ActiveRect(EpisodeHud.NotebookHeaderName).GetComponentsInChildren<TMP_Text>().Select(t => t.text),
                Does.Contain("Houseguest profile"));

            var identity = ActiveRect(EpisodeHud.ProfileIdentityName);
            Assert.That(identity.GetComponentsInChildren<TMP_Text>().Select(t => t.text), Does.Contain(actor.name));
            var trust = ActiveRect(EpisodeHud.ProfileTrustName).GetComponentsInChildren<TMP_Text>().ToArray();
            Assert.That(trust.Single(t => t.name == "Trust").text,
                Is.EqualTo(EpisodeDirector.TrustFigure(state.Score(state.playerId, actor.id))));
            Assert.That(string.Join("\n", trust.Select(t => t.text)), Does.Contain("not " + actor.name.Split(' ')[0] + "’s private opinion"));
            var records = ActiveRect(EpisodeHud.ProfileRecordsName).GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            Assert.That(records.First(), Does.StartWith("Head of Household "), "The ceremony counts are the house's record.");

            var page = ActiveRect(EpisodeDirector.NotebookSection.People);
            var words = page.GetComponentsInChildren<TMP_Text>().ToArray();
            if (!string.IsNullOrEmpty(actor.motive))
                Assert.That(words.Select(t => t.text), Has.None.Contains(actor.motive), "Their motive is theirs.");
            foreach (var label in words)
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box.");
            }
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading a profile commits nothing.");
            if (Application.isBatchMode) yield return CaptureFraming("notebook-profile");

            ButtonWithCaption(EpisodeHud.ProfileBackCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.ProfileId, Is.Null);
            Assert.That(ActiveRect(EpisodeHud.RosterRowPrefix + actor.name), Is.Not.Null, "Back is the directory.");

            director.ShowHouseguestProfile(actor.id);
            yield return null;
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.People);
            yield return null;
            Assert.That(director.ProfileId, Is.Null, "The rail's Houseguests is the directory, from a profile too.");
            director.ClosePanels();
            yield return null;
        }
    }
}
