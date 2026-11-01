using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// What the player is shown of the All-Stars' past games, from alumni rules 3 (ALL-STAR-HISTORIES-PLAN §14.F, §14.I's
    /// PlayMode list, §14.L.J): the alliances page's PAST GAMES after the pacts the player knows of - among them a
    /// suspected card of three faces and one of four, the outsider pacts value 5 grows - and a houseguest's profile, at
    /// every text size; the diary's "Before this season" for a player on an alumnus's card, at the standard and the
    /// largest; every new label at least 1.3 times its words and drawn whole. The captions are their own: nothing is
    /// appended to an existing one, and the same season
    /// held at 2 shows nothing new. A season started from the cast screen is enrolled and shows its past games (the audit
    /// of <c>EpisodePlayModeTests.CastSelect</c>). Photographed in a batch run at the standard and the largest text as
    /// 'past-games-*', '-largest' at the largest. The readers' words are pinned in the subset's PastGamesReadTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string PastGamesDan = "dan-gheesling", PastGamesWill = "dr-will-kirby", PastGamesDerrick = "derrick-levasseur",
            PastGamesJanelle = "janelle-pierzina", PastGamesCody = "cody-calafiore", PastGamesDanielle = "danielle-reyes",
            PastGamesVanessa = "vanessa-rousso", PastGamesTyler = "tyler-crispen", PastGamesChelsie = "chelsie-baham", PastGamesRachel = "rachel-reilly";

        private const string PastGamesHitmen = "bb16-hitmen", PastGamesCommittee = "bb22-committee";

        /// <summary>The houseguest on an alumnus's card.</summary>
        private static string OnCard(EpisodeState s, string templateId) =>
            s.contestants.Single(c => (c.sourceTemplateId ?? c.id) == templateId).id;

        /// <summary>
        /// The shipped All-Stars season of twelve in week one's free time, the player on <paramref name="playerTemplateId"/>'s
        /// card or a newcomer, enrolled at the latest alumni rules - held at <paramref name="heldAt"/> when given, the field
        /// alone (§14.G) - with <paramref name="write"/>'s records written in, saved to this test's slot and loaded the way a
        /// player loads one.
        /// </summary>
        private IEnumerator InstallAllStarsFixture(string playerTemplateId, int? heldAt, System.Action<EpisodeState> write = null)
        {
            var fixture = SeasonBuilder.Create(new SeasonBuilder.Choice
            {
                HouseSize = 12, Roster = CastTemplates.Roster.AllStars, PlayerTemplateId = playerTemplateId,
            }, 31u);
            ShippedRules.ApplyFresh(fixture);
            Assert.That(AlumniRules.On(fixture) && fixture.alumniRulesVersion == AlumniRules.Latest, Is.True,
                "The shipped All-Stars season enrols at the latest alumni rules.");
            if (heldAt.HasValue) fixture.alumniRulesVersion = heldAt.Value;
            write?.Invoke(fixture);
            Assert.That(EpisodeValidation.TryValidate(fixture, out var reason), Is.True, reason);
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            yield return SettleCast();
        }

        /// <summary>
        /// An outsider pact of houseguests, as value 5 grows one (§14.L), that the player was told of: its private fact with
        /// the player a knower, whispered, and the line the player read.
        /// </summary>
        private static AllianceState LeakedOutsiderPact(EpisodeState s, string name, params string[] templateIds)
        {
            var pact = PactRecord(s, "alliance-npc-", name, true, templateIds.Select(template => OnCard(s, template)).ToArray());
            Knowledge.AllianceFormed(s, pact);
            var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
            Knowledge.AddKnower(s, fact, s.playerId);
            Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
            s.ledger.alliances.Add(new AllianceRow { id = pact.id, why = "npc", startedWeek = 1 });
            PactLine(s, 1, StoryLog.Receipt, AllianceRead.LearnedLine(s, pact), s.playerId);
            return pact;
        }

        /// <summary>The faces on one card, by the prefix they are named with.</summary>
        private static string[] PastGameFacesNamed(RectTransform card, string prefix) =>
            card == null ? new string[0] : card.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name.StartsWith(prefix, System.StringComparison.Ordinal)).Select(rect => rect.name).ToArray();

        /// <summary>The captions of every live control on the open panel, in order.</summary>
        private string[] PanelControlCaptions()
        {
            var panel = LastActive("Episode panel");
            Assert.That(panel, Is.Not.Null);
            return panel.GetComponentsInChildren<Button>()
                .Where(button => button.IsActive())
                .Select(button => string.Join(" ", button.GetComponentsInChildren<TMP_Text>().Select(text => text.text)))
                .ToArray();
        }

        /// <summary>Every label under a part of the screen stands in a box at least 1.3 times its words, and draws them whole.</summary>
        private static void AssertEveryLineHasRoom(RectTransform root, string where)
        {
            Assert.That(root, Is.Not.Null, where);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>().Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)))
                AssertLineHasRoom(label, where);
        }

        /// <summary>Copy cut off anywhere on the open panel.</summary>
        private string[] ClippedOnThePanel()
        {
            var panel = LastActive("Episode panel");
            Assert.That(panel, Is.Not.Null);
            return panel.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
                .Select(label => "'" + Excerpt(label.text) + "'")
                .ToArray();
        }

        /// <summary>
        /// The alliances page of a twelve-house with both shared alliances seated, and two outsider pacts the player was
        /// told of - three faces and four (§14.L.J): the suspected cards as they always were, and after them PAST GAMES, its
        /// heading's caption and count apart, its note, and a card a seated shared alliance with a face for each of the two,
        /// the season and the public record on its eyebrow, the act verbatim and the pair's status. At every text size;
        /// photographed at the standard and the largest.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator PastGames_TheAlliancesPageShowsThePublicRecordAfterThreeAndFourFaceSuspectedCards()
        {
            RelationshipWeb.ClearSelection();
            yield return InstallAllStarsFixture(null, null, s =>
            {
                LeakedOutsiderPact(s, "The Three", PastGamesDan, PastGamesJanelle, PastGamesWill);
                LeakedOutsiderPact(s, "The Four", PastGamesDanielle, PastGamesVanessa, PastGamesChelsie, PastGamesRachel);
            });
            var state = director.Snapshot;
            var page = AllianceRead.Read(state);
            var games = KnownBlocs.PastGames(state);
            Assert.That(games.Select(game => game.FactId), Is.EqualTo(new[] { PastGamesHitmen, PastGamesCommittee }), "Both shared alliances are seated.");
            Assert.That(page.yours, Is.Empty);
            Assert.That(page.suspected.Select(card => card.memberIds.Count).OrderBy(count => count), Is.EqualTo(new[] { 3, 4 }), "A suspected card of three, and one of four.");
            string Suspected(AllianceRead.SuspectedPact card) =>
                EpisodeHud.SuspectedCardPrefix + string.Join(" & ", card.memberIds.Select(id => state.Find(id).name));

            foreach (var textSize in TextSizes.All)
            {
                yield return ApplyTextSize(textSize);
                string where = "the alliances page " + TextScales.At(textSize);
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
                yield return null; yield return null;
                // Taken with the page open: opening a panel may bank the house's clock as a commit of its own.
                int revision = director.Snapshot.revision;
                Canvas.ForceUpdateCanvases();

                // The pacts the player knows of, as they always were: a face a member, the headline naming them all, the count theirs.
                foreach (var card in page.suspected)
                {
                    var rect = LastActive(Suspected(card));
                    Assert.That(rect, Is.Not.Null, Suspected(card) + ": " + where);
                    var members = card.memberIds.Select(id => state.Find(id)).ToList();
                    Assert.That(PastGameFacesNamed(rect, EpisodeHud.PactFacePrefix), Is.EqualTo(members.Select(m => EpisodeHud.PactFacePrefix + m.name).ToArray()),
                        "A face a member of the " + members.Count + ": " + where);
                    Assert.That(CopyUnder(rect), Does.Contain(AllianceRead.Join(members.Select(m => m.name).ToList())), "Its headline names all " + members.Count + ": " + where);
                    AssertEveryLineHasRoom(rect, "The card of " + members.Count + " " + where);
                }
                Assert.That(CardsNamed(EpisodeHud.SuspectedCardPrefix), Is.EqualTo(page.suspected.Count), "No past game is a suspected card: " + where);
                Assert.That(CardsNamed(EpisodeHud.AllianceCardPrefix), Is.Zero, where);
                Assert.That(CopyUnder(LastActive("Other alliances heading")).Split('\n'), Is.EqualTo(new[] { EpisodeDirector.OtherAlliancesHeading, page.suspected.Count.ToString() }),
                    "The suspected count is the pacts' alone: " + where);

                // PAST GAMES: its caption and its count apart, its note, after the last suspected card.
                var heading = LastActive(EpisodeDirector.PastGamesHeadingName);
                Assert.That(heading, Is.Not.Null, "The past games' heading: " + where);
                Assert.That(CopyUnder(heading).Split('\n'), Is.EqualTo(new[] { EpisodeDirector.PastGamesHeading, games.Count.ToString() }),
                    "The heading's caption and its count, neither appended to the other: " + where);
                var note = LastActive(EpisodeDirector.PastGamesNoteName);
                Assert.That(CopyUnder(note), Does.Contain(EpisodeDirector.PastGamesNoteCopy), where);
                int headingAt = heading.GetSiblingIndex();
                Assert.That(page.suspected.Max(card => LastActive(Suspected(card)).GetSiblingIndex()), Is.LessThan(headingAt), "After the pacts the player knows of: " + where);
                Assert.That(note.GetSiblingIndex(), Is.EqualTo(headingAt + 1), "The note under the heading: " + where);

                // A card a seated shared alliance, by its fact's id, after the note: its season, its two faces, the act, the status.
                Assert.That(CardsNamed(EpisodeHud.PastGameCardPrefix), Is.EqualTo(games.Count), where);
                for (int i = 0; i < games.Count; i++)
                {
                    var game = games[i];
                    var card = LastActive(EpisodeHud.PastGameCardPrefix + game.FactId);
                    Assert.That(card, Is.Not.Null, game.FactId + ": " + where);
                    Assert.That(card.GetSiblingIndex(), Is.EqualTo(headingAt + 2 + i), "In fact-id order, after the note: " + where);
                    var pair = new[] { state.Find(game.FirstId), state.Find(game.SecondId) };
                    Assert.That(PastGameFacesNamed(card, EpisodeHud.PastGameFacePrefix), Is.EqualTo(pair.Select(m => EpisodeHud.PastGameFacePrefix + m.name).ToArray()),
                        "A face each, named as its own: " + where);
                    Assert.That(PastGameFacesNamed(card, EpisodeHud.PactFacePrefix), Is.Empty, "No pact's face: " + where);
                    string words = CopyUnder(card);
                    Assert.That(words, Does.Contain("BIG BROTHER " + game.Season + " · PUBLIC RECORD"), where);
                    Assert.That(words, Does.Contain(game.Act), "The act verbatim: " + where);
                    Assert.That(words, Does.Contain("Both in the house"), "The pair's status this season: " + where);
                    AssertEveryLineHasRoom(card, game.FactId + "'s card " + where);
                }
                AssertEveryLineHasRoom(heading, "The past games' heading " + where);
                AssertEveryLineHasRoom(note, "The past games' note " + where);

                string[] clipped = ClippedOnThePanel();
                Assert.That(clipped, Is.Empty, "Copy cut off on " + where + ": " + string.Join(" | ", clipped));
                AssertEveryLabelDraws(LastActive("Episode panel"), where);

                if (Application.isBatchMode && textSize != TextSize.Larger)
                {
                    string size = TextScales.Suffix(textSize);
                    string three = Suspected(page.suspected.Single(card => card.memberIds.Count == 3));
                    string four = Suspected(page.suspected.Single(card => card.memberIds.Count == 4));
                    string record = EpisodeHud.PastGameCardPrefix + PastGamesHitmen;
                    ScrollNotebookTo(three);
                    yield return CaptureFraming("past-games-suspected-three" + size, arrange: () => ScrollNotebookTo(three));
                    ScrollNotebookTo(four);
                    yield return CaptureFraming("past-games-suspected-four" + size, arrange: () => ScrollNotebookTo(four));
                    ScrollNotebookTo(record);
                    yield return CaptureFraming("past-games-record" + size, arrange: () => ScrollNotebookTo(record));
                }
                Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading the page commits nothing: " + where);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(TextSize.Standard);
        }

        /// <summary>
        /// The player on Cody's card: Derrick's profile carries his career, then their Hitmen past "between you" with its
        /// status, after the ceremony line, and its empty line speaks of this season; Dan's, with no past game between them,
        /// ends as it always did: the profile at every text size. The diary's record tab has a fourth row, "Before this
        /// season", after the room's three: the career's word, and the record and Cody's two past games, at the standard and
        /// the largest text. Photographed at the standard and the largest.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator PastGames_TheProfileAndTheDiaryShowThePlayersOwnPastGames()
        {
            yield return InstallAllStarsFixture(PastGamesCody, null);
            var state = director.Snapshot;
            string derrick = OnCard(state, PastGamesDerrick), dan = OnCard(state, PastGamesDan);
            Assert.That(OnCard(state, PastGamesCody), Is.EqualTo(state.playerId), "The player is Cody.");
            var derrickLines = KnownBlocs.ProfileLines(state, derrick);
            Assert.That(derrickLines.Count(line => line.StartsWith(KnownBlocs.BetweenYouPrefix, System.StringComparison.Ordinal)), Is.EqualTo(1));
            var danLines = KnownBlocs.ProfileLines(state, dan);
            Assert.That(danLines, Is.EqualTo(new[] { KnownBlocs.BeforeThisSeasonPrefix + AlumniEditions.Histories1.Career(PastGamesDan).Record }));
            var record = KnownBlocs.PlayerRecord(state);
            Assert.That(record, Is.Not.Null);
            Assert.That(record.Value, Is.EqualTo("Winner"));

            foreach (var textSize in TextSizes.All)
            {
                yield return ApplyTextSize(textSize);
                foreach (var (id, lines, empty, frame) in new[]
                         {
                             (derrick, derrickLines, KnownBlocs.NothingBetweenYouThisSeasonCopy, "past-games-profile"),
                             (dan, danLines, KnownBlocs.NothingBetweenYouCopy, (string)null),
                         })
                {
                    string where = state.Find(id).name + "'s profile " + TextScales.At(textSize);
                    director.ShowHouseguestProfile(id);
                    yield return null; yield return null;
                    int revision = director.Snapshot.revision;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(director.ProfileId, Is.EqualTo(id), where);
                    var records = LastActive(EpisodeHud.ProfileRecordsName);
                    var shown = records.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
                    Assert.That(shown.First(), Does.StartWith("Head of Household "), "The ceremony line first: " + where);
                    Assert.That(shown.Skip(1).Take(lines.Count), Is.EqualTo(lines), "Then the past games, verbatim: " + where);
                    Assert.That(shown.Last(), Is.EqualTo(empty), "The empty line: " + where);
                    Assert.That(shown.Length, Is.EqualTo(lines.Count + 2), "They count toward nothing of the player's this season: " + where);
                    // The records block is all S5 adds to the profile, and the Back control it moves down.
                    AssertEveryLineHasRoom(records, where);
                    foreach (var label in ButtonWithCaption(EpisodeHud.ProfileBackCaption).GetComponentsInChildren<TMP_Text>())
                        AssertLineHasRoom(label, "Back, under the records, " + where);
                    if (Application.isBatchMode && frame != null && textSize != TextSize.Larger)
                        yield return CaptureFraming(frame + TextScales.Suffix(textSize), arrange: () => ScrollNotebookTo(EpisodeHud.ProfileRecordsName));
                    Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Reading a profile commits nothing: " + where);
                }
                director.ClosePanels();
                yield return null;
            }

            // The diary in week one's free time opens on its pending tab, the free time's actions being
            // the player's to decide: the record is its own tab, turned to as a player turns to it.
            string beforeRow = EpisodeHud.DiaryRecordRowPrefix + EpisodeDirector.BeforeThisSeasonCaption;
            foreach (var textSize in new[] { TextSize.Standard, TextSize.Largest })
            {
                yield return ApplyTextSize(textSize);
                string where = "the diary's record " + TextScales.At(textSize);
                yield return OpenDiaryFixturePanel();
                ButtonWithCaption(EpisodeDirector.DiaryRecordTabCaption).onClick.Invoke();
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var rows = new[] { "Study preparation", "Diary persona", "Recorded jury impression", EpisodeDirector.BeforeThisSeasonCaption }
                    .Select(row => ActiveRect(EpisodeHud.DiaryRecordRowPrefix + row)).ToArray();
                Assert.That(rows.All(row => row != null), Is.True, "The room's three rows and the fourth: " + where);
                Assert.That(rows.Select(row => row.GetSiblingIndex()), Is.Ordered.Ascending, "The fourth after the room's three: " + where);
                var words = rows[3].GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
                Assert.That(words, Is.EqualTo(new[] { EpisodeDirector.BeforeThisSeasonCaption, record.Value, record.Note }),
                    "Its caption, the career's word, and the record with Cody's past games: " + where);
                Assert.That(record.Note, Does.Contain(AlumniEditions.Histories1.Pairs.Single(p => p.Id == PastGamesHitmen).Act)
                    .And.Contain(AlumniEditions.Histories1.Pairs.Single(p => p.Id == PastGamesCommittee).Act), where);
                // The room's three rows as they were, and the fourth: each label in a box 1.3 times its words, drawn whole.
                foreach (var row in rows) AssertEveryLineHasRoom(row, where);
                if (Application.isBatchMode)
                    yield return CaptureFraming("past-games-diary" + TextScales.Suffix(textSize), arrange: () => ScrollNotebookTo(beforeRow));
                director.ClosePanels();
                yield return null;
            }
            yield return WaitForDiaryExit();
            yield return ApplyTextSize(TextSize.Standard);
        }

        /// <summary>
        /// The caption audit (§14.F's Layout line, §14.L.G): the past games add captions of their own - "PAST GAMES", "Past
        /// game · " and its faces - and append to none. In the fixture's Regular season, and in an All-Stars season held at
        /// alumni rules 2, the page and the profile show nothing new and the empty line reads as it always did. The same
        /// season at the latest value carries the very same controls, by caption, on the page and on the profile, its
        /// headings' words are exactly their captions, and the empty state of the pacts the player knows of says what it
        /// always said.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator PastGames_TheCaptionsAreTheirOwnAndNothingNewShowsBelowThreeOrInARegularSeason()
        {
            RelationshipWeb.ClearSelection();
            // The fixture's own season: Regular, not on the alumni rules.
            yield return SettleCast();
            director.SuspendNpcAutonomyForDiagnostics();
            var regular = director.Snapshot;
            Assert.That(AlumniRules.At(regular, AlumniRules.Reputation), Is.False, "The fixture's season is not on the alumni rules.");
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
            yield return null; yield return null;
            Assert.That(LastActive(EpisodeDirector.PastGamesHeadingName), Is.Null, "A Regular season has no past games.");
            Assert.That(LastActive(EpisodeDirector.PastGamesNoteName), Is.Null);
            Assert.That(CardsNamed(EpisodeHud.PastGameCardPrefix), Is.Zero);
            var someone = regular.contestants.First(c => !c.isPlayer && c.status == ContestantStatus.Active);
            director.ShowHouseguestProfile(someone.id);
            yield return null; yield return null;
            var regularRecords = LastActive(EpisodeHud.ProfileRecordsName).GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
            Assert.That(regularRecords.Any(line => line.StartsWith(KnownBlocs.BeforeThisSeasonPrefix, System.StringComparison.Ordinal)
                || line.StartsWith(KnownBlocs.BetweenYouPrefix, System.StringComparison.Ordinal)), Is.False, "No past game in a Regular season.");
            Assert.That(regularRecords, Has.None.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy), "The empty line as it always read.");
            if (regularRecords.Length == 2)
                Assert.That(regularRecords.Last(), Is.EqualTo(KnownBlocs.NothingBetweenYouCopy), "Nothing between them: the line as it always read.");
            director.ClosePanels();
            yield return null;

            // An All-Stars season held at 2, then the same season at the latest value.
            string[] heldPage = null, heldProfile = null;
            foreach (int? heldAt in new int?[] { AlumniRules.Loyalty, null })
            {
                yield return InstallAllStarsFixture(PastGamesCody, heldAt);
                var state = director.Snapshot;
                bool shown = heldAt == null;
                string where = shown ? "at alumni rules " + AlumniRules.Latest : "held at " + heldAt;
                Assert.That(state.alumniRulesVersion, Is.EqualTo(heldAt ?? AlumniRules.Latest), where);
                string derrick = OnCard(state, PastGamesDerrick);

                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(CopyUnder(LastActive("Your alliances heading")).Split('\n'), Is.EqualTo(new[] { EpisodeDirector.YourAlliancesHeading, "0" }), where);
                Assert.That(CopyUnder(LastActive("Other alliances heading")).Split('\n'), Is.EqualTo(new[] { EpisodeDirector.OtherAlliancesHeading, "0" }), where);
                Assert.That(CopyUnder(LastActive(EpisodeHud.NoSuspectedName)), Does.Contain(EpisodeDirector.NoSuspectedCopy), "The empty state says what it always said: " + where);
                Assert.That(CopyUnder(LastActive(EpisodeHud.NoAlliancesName)), Does.Contain(EpisodeDirector.NoAlliancesCopy), where);
                var pageCaptions = PanelControlCaptions();
                if (!shown)
                {
                    Assert.That(LastActive(EpisodeDirector.PastGamesHeadingName), Is.Null, "Below 3, no past games: " + where);
                    Assert.That(CardsNamed(EpisodeHud.PastGameCardPrefix), Is.Zero, where);
                    heldPage = pageCaptions;
                }
                else
                {
                    Assert.That(CopyUnder(LastActive(EpisodeDirector.PastGamesHeadingName)).Split('\n'), Is.EqualTo(new[] { EpisodeDirector.PastGamesHeading, "2" }),
                        "Exactly its caption, the count apart: " + where);
                    Assert.That(director.GetComponentsInChildren<RectTransform>().Where(rect => rect.gameObject.activeInHierarchy
                            && rect.name.StartsWith("Past game", System.StringComparison.Ordinal) && !rect.name.StartsWith(EpisodeHud.PastGameFacePrefix, System.StringComparison.Ordinal))
                            .Select(rect => rect.name).Distinct(),
                        Is.EquivalentTo(new[] { EpisodeHud.PastGameCardPrefix + PastGamesHitmen, EpisodeHud.PastGameCardPrefix + PastGamesCommittee,
                            EpisodeDirector.PastGamesHeadingName, EpisodeDirector.PastGamesNoteName }), "Every new part by its own name: " + where);
                    Assert.That(pageCaptions, Is.EqualTo(heldPage), "The same controls by the same captions, none added and none changed: " + where);
                }

                director.ShowHouseguestProfile(derrick);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var records = LastActive(EpisodeHud.ProfileRecordsName).GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
                Assert.That(records.First(), Does.StartWith("Head of Household "), where);
                var profileCaptions = PanelControlCaptions();
                if (!shown)
                {
                    Assert.That(records, Is.EqualTo(new[] { records.First(), KnownBlocs.NothingBetweenYouCopy }), "Below 3 the profile is as it was: " + where);
                    heldProfile = profileCaptions;
                }
                else
                {
                    Assert.That(records.Last(), Is.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy), where);
                    Assert.That(records.Skip(1).Take(records.Length - 2), Is.EqualTo(KnownBlocs.ProfileLines(state, derrick)), where);
                    Assert.That(profileCaptions, Is.EqualTo(heldProfile), "The profile's controls are the same: " + where);
                    Assert.That(ButtonWithCaption(EpisodeHud.ProfileBackCaption), Is.Not.Null, where);
                }
                director.ClosePanels();
                yield return null;
            }
        }

        /// <summary>
        /// The audit of <c>EpisodePlayModeTests.CastSelect</c> (§14.I): an All-Stars season started from the cast screen
        /// as Will is enrolled at the latest alumni rules, and the page and the profile show its past games - the Hitmen on
        /// the page, and Danielle's Chilltown renomination as a past game between her and the player.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator PastGames_AnAllStarsSeasonFromTheCastScreenIsEnrolledAndShowsItsPastGames()
        {
            yield return OpenCastScreen();
            var will = CastTemplates.Find(PastGamesWill);
            CastButtons(CastTemplates.RosterName(CastTemplates.Roster.AllStars))[0].onClick.Invoke();
            yield return null;
            CastButtons(will.Name)[0].onClick.Invoke();
            yield return null;
            CastButtons("Play as Will")[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False, "The season started.");
            // Nobody in the house acts before the reading: the profile's empty line is about the player's own records.
            director.SuspendNpcAutonomyForDiagnostics();
            var fresh = director.Snapshot;
            Assert.That(fresh.Find(fresh.playerId).name, Is.EqualTo(will.Name));
            Assert.That(fresh.alumniRulesVersion, Is.EqualTo(AlumniRules.Latest), "The cast screen's All-Stars season enrols at the latest alumni rules.");
            Assert.That(AlumniRules.At(fresh, AlumniRules.Reputation), Is.True);
            var games = KnownBlocs.PastGames(fresh);
            Assert.That(games.Select(game => game.FactId), Does.Contain(PastGamesHitmen), "Derrick and Cody are both in the house.");
            var record = KnownBlocs.PlayerRecord(fresh);
            Assert.That(record, Is.Not.Null, "The player on Will's card has his record.");
            Assert.That(record.Value, Is.EqualTo("Winner"));
            yield return SettleCast();

            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(LastActive(EpisodeDirector.PastGamesHeadingName), Is.Not.Null, "PAST GAMES is on the page.");
            foreach (var game in games)
                Assert.That(LastActive(EpisodeHud.PastGameCardPrefix + game.FactId), Is.Not.Null, game.FactId);

            // The default house seats the roster in its order, so Danielle is in it with Will (§3.2).
            var danielle = fresh.contestants.SingleOrDefault(c => (c.sourceTemplateId ?? c.id) == PastGamesDanielle);
            Assert.That(danielle, Is.Not.Null, "Danielle is seated in the default All-Stars house.");
            director.ShowHouseguestProfile(danielle.id);
            yield return null; yield return null;
            var records = LastActive(EpisodeHud.ProfileRecordsName).GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
            Assert.That(records, Does.Contain(KnownBlocs.BetweenYouPrefix + AlumniEditions.Histories1.Pairs.Single(p => p.Id == "bb7-chilltown-renomination").Act),
                "Their past game, between them.");
            Assert.That(records.Last(), Is.EqualTo(KnownBlocs.NothingBetweenYouThisSeasonCopy));
            director.ClosePanels();
            yield return null;
        }
    }
}
