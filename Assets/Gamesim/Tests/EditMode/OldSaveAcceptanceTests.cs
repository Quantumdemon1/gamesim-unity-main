using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Old-save acceptance (the owner's decision; Assets/Plans/OLD-SAVE-ACCEPTANCE.md): every committed save, one per
    /// save version and rules combination, loads in this build, migrates in memory, validates, replays the command script
    /// its reference build recorded to that build's season digest, byte for byte, and every simulation and presentation
    /// reader the screens print through renders it without an exception, a hidden ballot or a number for odds. Kept as
    /// the backup, each save is also recovered through the store as loading reads it (the owner's 22 October gate). The
    /// screens' own text assembly in the Runtime assembly (the director's notebook pages and diary room, the HUD's
    /// roster) is not run here: it formats what these readers return.
    ///
    /// <para>The fixtures (Fixtures/OldSaves/manifest.json) are real saves the standalone verification walks wrote between
    /// 2026-09-28 and 2026-10-10, copied byte for byte, and, for every cell no real mid-season file exists for, a save the
    /// schema's own build generated: its Simulation and Persistence sources at that commit (for schemas 2 to 5, which no
    /// commit here wrote, the Codex port's source snapshot that did), played by a fixed walker to mid-season and saved by
    /// its own store. A save made at the finale or after it is read, never counted as its cell's replay. The reference
    /// build is the oldest build that plays the save as this one does: the save's own schema's from 21 on, and for older
    /// saves the schema-20 build 7f398b94 - the migrations to 17, 18, 19 and 20 start their rules the week after the
    /// save's, so an older build plays on under fewer of them by design - or a later one where an unversioned change moved
    /// such a season (the plan lists each). Schema 22's earlier shapes are recorded by the last-shape build c2725b53 from
    /// the save given the two literals the migration writes. Tools/OldSaves recorded every script and digest with that
    /// build's own sources, under the editor's Mono. Schema 29's fixtures are the schema-29 build's own files (60d8f629),
    /// one for each commitment mode at dialogue rules 0: the default scene's season and a fresh shipped season played to
    /// mid-season, and a schema-28 mode-1 season carried on, each saved by that build's store and recorded by that build.
    /// Since dialogue D1 a fourth: a season the cast screen's conversation preview started on dialogue rules 1, saved
    /// mid-conversation by the build that starts it (36882bb8), checkpointed at 29 itself by the observer's native arm
    /// (CurrentOldSaveTrace), since it has no schema-28 form. Since SCH30 all four migrate to 30 in memory like every
    /// older one. Schema 30's are the SCH30 build's own (936b73a4), every pooled field off: the default scene's season
    /// and a fresh shipped season of each roster, Regular and All-Stars, at alumni rules 0; they load as they are, with
    /// no migration. The H1b build (0fef7b59) wrote the alumni-on cell: the same shipped All-Stars season at alumni
    /// rules 1, with its histories and the Hitmen's final two. The build S6 froze alumni rules 5 at (51dd14a5) wrote the
    /// alumni-5 cell: the same season at alumni rules 5, saved with a houseguests' pact grown past two and kept to itself.
    /// B9's builds wrote the tuning-1 cells, the shipped Regular season on tuning rules 1, walked plainly (8a0cee2e) and by
    /// the studier (46459d3e). B9's head (7ab18ab0), the last build whose cast screen chip starts dialogue rules 1, wrote
    /// the dialogue-1 cell at 30: that season with the conversation preview on, at dialogue rules 1 and tuning rules 1,
    /// saved mid-conversation at its case node and checkpointed at 30 by the observers' tuning arm. The chip-2 stack's
    /// head (779506e9), whose chip starts dialogue rules 2, wrote the dialogue-2 cell at 30: the same season on dialogue
    /// rules 2 and tuning rules 1, saved mid-conversation at its reply node with the voter's answer recorded, and
    /// checkpointed at 30 alone by the same arm. The enrolled head of story rules 10 (5863d964) wrote the story-rules-10
    /// cell: the shipped Regular season every new season is since the enrolment, on Blame and tuning rules 1, saved after a
    /// houseguest's veto and played on through another, so the Blame rule decides a holder's grudge on both sides of the
    /// save.</para>
    ///
    /// <para>Three digests (<see cref="OldSaveTrace"/>): the season's - the cross-build gate - must be the reference
    /// build's; the whole state's, blind to prose, and the verbatim text are this build's, pinned when the fixture was
    /// curated (where they differ from the reference build's, the plan says why: the ballot privacy of UI-UX-PASS-PLAN
    /// B0 and writing passes). A change that moves any of them on an old season is caught here.</para>
    /// </summary>
    public sealed class OldSaveAcceptanceTests
    {
        private static string Root =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
#else
            Path.Combine(AppContext.BaseDirectory, "OldSaves");
#endif

        private static JObject manifest;
        private static JObject Manifest => manifest ?? (manifest = JObject.Parse(File.ReadAllText(Path.Combine(Root, "manifest.json"))));

        private static JObject Entry(string id) =>
            Manifest["episodes"].Concat(Manifest["careers"]).Concat(Manifest["refused"]).Cast<JObject>().Single(e => (string)e["id"] == id);

        /// <summary>The episode fixtures by id. A fixed list, so a fixture that goes missing from the manifest fails rather than vanishing.</summary>
        private static readonly string[] Episodes =
        {
            // Real saves.
            "s16-director-w1-hoh", "s16-director-final-speeches", "s16-director-finished-winner", "s16-default-w1-social",
            "s17-director-finished", "s17-default-w1-social",
            "s18-director-w1-eviction", "s18-default-w1-social",
            "s19-director-finished", "s19-default-w1-social",
            "s20-director-finished-runner-up", "s20-default-w1-social",
            "s21-director-w1-hoh", "s21-default-w1-social",
            "s26-mode1-finished", "s26-default-w1-social",
            "s28-mode1-finished", "s28-mode2-finished", "s28-default-w1-social",
            // Generated by the schema's own build: for 2 to 5 the Codex port's source snapshots, before this repository's first commit.
            "s02-default-w2-veto", "s03-default-w2-campaign", "s04-default-w2-eviction", "s05-default-w3-hoh",
            "s06-default-w2-social", "s07-director-w2-hoh", "s07-default-w2-social",
            "s09-director-w2-veto", "s09-default-w2-social", "s10-director-w2-nomination", "s11-director-w2-campaign",
            "s12-director-w3-hoh", "s13-director-w2-eviction", "s13-default-w2-social", "s14-director-w2-veto-meeting",
            "s15-director-w2-campaign", "s17-director-w2-nomination", "s19-director-w2-eviction", "s20-director-w2-veto-meeting",
            "s22-director-early-w2-campaign", "s22-director-middle-w3-nomination", "s22-director-late-w3-veto", "s22-default-w2-social",
            "s23-director-w2-veto-meeting", "s23-default-w2-social", "s24-director-w3-nomination", "s24-default-w2-social",
            "s25-director-w2-eviction", "s25-default-w2-social", "s26-mode1-w2-veto", "s27-mode1-w3-social", "s27-default-w2-social",
            "s28-mode1-waved-off-w2-hoh", "s28-mode1-leaks-w2-eviction", "s28-mode1-war-rooms-w3-veto-meeting",
            "s28-mode1-allweek-w2-campaign", "s28-mode2-allweek-w3-campaign",
            // Written by this build, schema 29 at dialogue rules 0: the default scene's season (mode 0), a schema-28 mode-1
            // season carried on and saved again, and a fresh shipped season (mode 2).
            "s29-default-w2-social", "s29-mode1-allweek-w3-veto-meeting", "s29-mode2-allweek-w3-campaign",
            // Written by the build whose cast screen starts it (dialogue D1, 36882bb8): a fresh shipped season with the
            // conversation preview on, dialogue rules 1, saved mid-conversation.
            "s29-mode2-dialogue1-w2-campaign",
            // Written by the SCH30 build (936b73a4), schema 30 at dialogue rules 0 and alumni rules 0, the pooled fields off:
            // the default scene's season (mode 0), a fresh shipped Regular season and a fresh shipped All-Stars season (mode 2).
            "s30-default-w2-social", "s30-mode2-allweek-w3-campaign", "s30-mode2-allstars-w3-campaign",
            // Written by the H1b build (0fef7b59), schema 30 at alumni rules 1 and edition 1: the same shipped All-Stars
            // season, now with its alumni's histories (ALL-STAR-HISTORIES-PLAN 8.7, the alumni-on cell).
            "s30-mode2-allstars-alumni1-w3-campaign",
            // Written by the build S6 froze alumni rules 5 at (51dd14a5), schema 30 at alumni rules 5: the shipped All-Stars
            // season saved with a houseguests' pact grown past two (ALL-STAR-HISTORIES-PLAN 14.I, 14.L).
            "s30-mode2-allstars-alumni5-w3-campaign",
            // Written by B9-3's build (B9-BALANCE-PLAN §4.7), schema 30 at tuning rules 1: a fresh shipped Regular season,
            // which every new season since B9 starts on.
            "s30-mode2-allweek-tuning1-w3-campaign",
            // Written by B9 round 4's build (46459d3e; OLD-SAVE-ACCEPTANCE 2.9), the same season walked by the studier and
            // saved with a point of preparation banked, so tuning 1's preparation (its spend, the study's limit and the
            // final Head of Household counting it) fires after the load.
            "s30-mode2-allweek-tuning1-studier-w2-hoh",
            // Written by B9's head (7ab18ab0; OLD-SAVE-ACCEPTANCE 2.10), the last build whose cast screen chip starts dialogue
            // rules 1: a fresh shipped Regular season with the conversation preview on, schema 30 at dialogue rules 1 and
            // tuning rules 1, saved mid-conversation at its case node.
            "s30-mode2-dialogue1-tuning1-w1-campaign",
            // Written by the chip-2 stack's head (779506e9; OLD-SAVE-ACCEPTANCE 2.11), the build whose cast screen chip starts
            // dialogue rules 2: the same season on dialogue rules 2 and tuning rules 1, saved mid-conversation at its reply
            // node with the voter's answer recorded.
            "s30-mode2-dialogue2-tuning1-w1-campaign",
            // Written by story rules 10's enrolled head (5863d964; OLD-SAVE-ACCEPTANCE 2.12, STORY-RULES-10-PLAN §8), schema 30
            // at story rules 10 on tuning rules 1: a fresh shipped Regular season, saved after a houseguest's veto and played on
            // through another, so the Blame rule decides the holder's grudge before the save and after the load.
            "s30-mode2-allweek-story10-tuning1-w2-hoh",
            // Written by the role moments' enrolled head (ROLE-MOMENTS-PLAN R7; OLD-SAVE-ACCEPTANCE 2.13), schema 30 at story
            // rules 11 on tuning rules 1: a fresh shipped Regular season, saved at a veto meeting with the Head of Household's
            // keep-the-block ask to the player waiting for its answer.
            "s30-mode2-allweek-story11-tuning1-w3-veto-meeting",
        };

        /// <summary>
        /// Fixtures saved at the finale or after it. Each reads a real end state - the finale's receipts, a final
        /// argument, the jury - but plays little or nothing forward, so none counts as its cell's replay: every cell
        /// has a fixture saved in a week, before the finale, whose script plays the season on from there.
        /// </summary>
        private static readonly string[] ReadOnly =
        {
            "s16-director-final-speeches", "s16-director-finished-winner", "s17-director-finished", "s19-director-finished",
            "s20-director-finished-runner-up", "s26-mode1-finished", "s28-mode1-finished", "s28-mode2-finished",
        };

        private static readonly string[] FinalePhases =
            { "FinalHoHPart1", "FinalHoHPart2", "FinalHoHPart3", "FinalEviction", "Jury", "JuryQuestioning", "FinalSpeeches", "Finished" };

        private static readonly string[] Careers = { "career-earliest", "career-latest" };

        /// <summary>Saves this build refuses on purpose, each with the reason the plan gives.</summary>
        private static readonly string[] Refused = { "s14-storyline-director-w2-social" };

        // ------------------------------------------------------------------ the fixture set

        [Test]
        public void TheManifestListsEveryFixtureAndEveryFileIsTheSourceByteForByte()
        {
            var listed = Manifest["episodes"].Select(e => (string)e["id"]).ToList();
            Assert.That(listed, Is.EquivalentTo(Episodes), "The manifest's episodes are the fixtures this class plays.");
            Assert.That(Manifest["careers"].Select(e => (string)e["id"]), Is.EquivalentTo(Careers));
            Assert.That(Manifest["refused"].Select(e => (string)e["id"]), Is.EquivalentTo(Refused));
            foreach (var entry in Manifest["episodes"].Concat(Manifest["careers"]).Concat(Manifest["refused"]).Cast<JObject>())
            {
                var bytes = File.ReadAllBytes(Path.Combine(Root, (string)entry["save"]));
                Assert.That(bytes.Length, Is.EqualTo((int)entry["saveBytes"]), (string)entry["id"]);
                Assert.That(Sha256(bytes), Is.EqualTo((string)entry["saveSha256"]), (string)entry["id"] + ": never reseal or renormalise a save.");
                if ((string)entry["origin"] == "real")
                    Assert.That((string)entry["source"], Does.StartWith("GamesimVerify/"), "Where it came from, without a local path.");
                else
                    Assert.That((string)entry["writer"], Does.Match("^([0-9a-f]{8}|codex-port:unity-v[2-5]-source-202609(10|13))$"),
                        (string)entry["id"] + ": the build that wrote it, a commit or the Codex port's source snapshot.");
                // A real save names no writer, so only a generated one is held to its writer here.
                // From schema 29 on, a generated fixture is written and recorded by the build that writes its schema.
                if ((string)entry["origin"] != "real" && (int)entry["schema"] >= 29)
                    Assert.That((string)entry["referenceBuild"], Is.EqualTo((string)entry["writer"]),
                        (string)entry["id"] + ": a generated schema-29-or-later fixture is written and recorded by the build that writes its schema.");
                // From schema 30 a cell also names the alumni rules and the cast (ALL-STAR-HISTORIES-PLAN 8.7). Both are read
                // from the save itself, so a row cannot name a cell its file is not; so are the tuning rules and, since the
                // dialogue-1 cell at 30 (OLD-SAVE-ACCEPTANCE 2.10), the dialogue rules, and, since the story-rules-10 cell
                // (OLD-SAVE-ACCEPTANCE 2.12), the story rules, which name a cell from Blame on.
                if ((string)entry["kind"] == "episode" && (int)entry["schema"] >= 30)
                {
                    var saved = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Root, (string)entry["save"])))["state"];
                    bool alumni = saved["contestants"].Any(c => CastTemplates.Find((string)c["sourceTemplateId"])?.Roster == CastTemplates.Roster.AllStars);
                    Assert.That((string)entry["cast"], Is.EqualTo(alumni ? "allstars" : "regular"), (string)entry["id"] + ": the cast the save seats.");
                    Assert.That((int)entry["rules"]["alumniRulesVersion"], Is.EqualTo((int)saved["alumniRulesVersion"]), (string)entry["id"] + ": the save's own alumni rules.");
                    Assert.That((int)entry["rules"]["tuningRulesVersion"], Is.EqualTo((int)saved["tuningRulesVersion"]), (string)entry["id"] + ": the save's own tuning rules.");
                    Assert.That((int)entry["rules"]["dialogueRulesVersion"], Is.EqualTo((int)saved["dialogueRulesVersion"]), (string)entry["id"] + ": the save's own dialogue rules.");
                    Assert.That((int)entry["story"]["rulesVersion"], Is.EqualTo((int)saved["story"]["rulesVersion"]), (string)entry["id"] + ": the save's own story rules.");
                    Assert.That((int)entry["story"]["rulesStartWeek"], Is.EqualTo((int)saved["story"]["rulesStartWeek"]), (string)entry["id"] + ": the save's own story start week.");
                }
            }
            // One replayed fixture per cell of the plan's matrix: every schema a build wrote (not 1 or 8, which no surviving
            // build did), in each profile it shipped - the director's fresh season and the default scene's; before 6 the
            // director played the default scene's catalogue - with the unified commitment mode (uc), the all-week rules,
            // Wave D's leaks and war rooms where they branch, and schema 22's three shapes; from schema 29 the dialogue rules
            // too, each commitment mode at dialogue rules 0, and the shipped mode at dialogue rules 1, the conversation
            // preview's (D1); from schema 30 the alumni rules and the cast too, the default scene's season and the shipped
            // season of each roster at alumni rules 0, from H1b the shipped All-Stars season at alumni rules 1, and from S6
            // the same season at alumni rules 5, the value S6 froze; from B9 the shipped Regular season at tuning rules 1, which
            // every new season starts on (a cell names its tuning only where it is on), and from B9's round 4 the same season
            // walked by the studier (a cell names its walker only where it is not the plain one), and from B9's head the shipped
            // Regular season the conversation preview starts at 30, dialogue rules 1 on tuning rules 1, written by the last build
            // whose chip starts rules 1, and from the chip-2 stack's head the same season on dialogue rules 2, written by the
            // build whose chip starts rules 2, and from story rules 10's enrolment the shipped Regular season at story rules 10,
            // which every new season starts on (a cell names its story rules only from Blame on, so every earlier cell keeps its
            // name), and from the role moments' enrolment the same season at story rules 11. A cell counts only a fixture saved in a week, before the
            // finale, with a script to play: one saved at the finale or after it is read, never a cell's replay.
            var replayed = Manifest["episodes"].Where(e => (int)e["commands"] > 0 && !FinalePhases.Contains((string)e["phase"])).ToList();
            Assert.That(Manifest["episodes"].Except(replayed).Select(e => (string)e["id"]), Is.EquivalentTo(ReadOnly),
                "A fixture saved at the finale or after it reads an end state; it is never a cell's replay.");
            var cells = replayed.Select(Cell).ToList();
            Assert.That(cells, Is.Unique, "One replayed fixture a cell.");
            Assert.That(cells, Is.EquivalentTo(new[]
            {
                "2/default/uc0/allweek0", "3/default/uc0/allweek0", "4/default/uc0/allweek0", "5/default/uc0/allweek0",
                "6/default/uc0/allweek0", "7/director/uc0/allweek0", "7/default/uc0/allweek0",
                "9/director/uc0/allweek0", "9/default/uc0/allweek0", "10/director/uc0/allweek0", "11/director/uc0/allweek0",
                "12/director/uc0/allweek0", "13/director/uc0/allweek0", "13/default/uc0/allweek0", "14/director/uc0/allweek0",
                "15/director/uc0/allweek0",
                "16/director/uc0/allweek0", "16/default/uc0/allweek0", "17/director/uc0/allweek0", "17/default/uc0/allweek0",
                "18/director/uc0/allweek0", "18/default/uc0/allweek0", "19/director/uc0/allweek0", "19/default/uc0/allweek0",
                "20/director/uc0/allweek0", "20/default/uc0/allweek0", "21/director/uc0/allweek0", "21/default/uc0/allweek0",
                "22/director/uc0/allweek0/early", "22/director/uc0/allweek0/middle", "22/director/uc0/allweek0/late", "22/default/uc0/allweek0",
                "23/director/uc0/allweek0", "23/default/uc0/allweek0", "24/director/uc0/allweek0", "24/default/uc0/allweek0",
                "25/director/uc0/allweek0", "25/default/uc0/allweek0",
                "26/director/uc1/allweek0", "26/default/uc0/allweek0", "27/director/uc1/allweek0", "27/default/uc0/allweek0",
                "28/director/uc1/allweek0", "28/director/uc1/allweek0/leaks1/plans0", "28/director/uc1/allweek0/leaks1/plans1",
                "28/director/uc1/allweek1/leaks1/plans1", "28/director/uc2/allweek1/leaks1/plans1", "28/default/uc0/allweek0",
                "29/default/uc0/allweek0/dialogue0", "29/director/uc1/allweek1/leaks1/plans1/dialogue0",
                "29/director/uc2/allweek1/leaks1/plans1/dialogue0", "29/director/uc2/allweek1/leaks1/plans1/dialogue1",
                "30/default/uc0/allweek0/dialogue0/alumni0/regular", "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni0/regular",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni0/allstars",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni1/allstars",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni5/allstars",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni0/regular/tuning1",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni0/regular/tuning1/studier",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue1/alumni0/regular/tuning1",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue2/alumni0/regular/tuning1",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni0/regular/tuning1/story10",
                "30/director/uc2/allweek1/leaks1/plans1/dialogue0/alumni0/regular/tuning1/story11",
            }));
        }

        /// <summary>A fixture's cell: schema, profile, the rules the readers branch on, for the director's schema 22 which of its shapes, from schema 29 the dialogue rules, from schema 30 the alumni rules and the cast, from B9 the tuning rules where they are on, the story rules from Blame on (OLD-SAVE-ACCEPTANCE 2.12), and the recorder's walker where it is not the plain one (OLD-SAVE-ACCEPTANCE 2.9).</summary>
        private static string Cell(JToken e)
        {
            int On(string rule) => Math.Min(1, (int?)e["rules"][rule] ?? 0);
            int schema = (int)e["schema"];
            string profile = (string)e["profile"], id = (string)e["id"];
            return schema + "/" + profile + "/uc" + ((int?)e["rules"]["unifiedCommitmentRulesVersion"] ?? 0) + "/allweek" + On("allWeekRulesStartWeek")
                + (On("allianceLeakRulesStartWeek") + On("pactPlanRulesStartWeek") > 0 ? "/leaks" + On("allianceLeakRulesStartWeek") + "/plans" + On("pactPlanRulesStartWeek") : "")
                + (schema == 22 && profile == "director" ? (id.Contains("-early-") ? "/early" : id.Contains("-middle-") ? "/middle" : "/late") : "")
                + (schema >= 29 ? "/dialogue" + (int)e["rules"]["dialogueRulesVersion"] : "")
                + (schema >= 30 ? "/alumni" + (int)e["rules"]["alumniRulesVersion"] + "/" + (string)e["cast"] : "")
                + (schema >= 30 && ((int?)e["rules"]["tuningRulesVersion"] ?? 0) != 0 ? "/tuning" + (int)e["rules"]["tuningRulesVersion"] : "")
                + (e["story"] is JObject story && (int)story["rulesVersion"] >= StoryRules.Blame ? "/story" + (int)story["rulesVersion"] : "")
                + ((string)e["walker"] is string walker && walker != "plain" ? "/" + walker : "");
        }

        // ------------------------------------------------------------------ load, migrate, validate, replay

        [TestCaseSource(nameof(Episodes))]
        public void AnOldSaveLoadsMigratesValidatesAndReplaysToItsRecordedDigest(string id)
        {
            var entry = Entry(id);
            int schema = (int)entry["schema"];
            using var slot = new Slot((string)entry["save"]);
            var payload = (JObject)JObject.Parse(File.ReadAllText(slot.Store.SavePath))["state"];
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(schema));

            // The source-bound current adapter owns the live schema's admission; since SCH30 a schema-29 fixture migrates
            // to 30 in memory like every older one. Historical fixtures and their reference-schema checkpoint algorithm
            // are not rewritten.
            const int current = CurrentOldSaveTrace.Current;
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            if (schema < current) Assert.That(message, Does.Contain("Schema " + schema + " history was migrated to schema " + current + " in memory."));
            else Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(slot.Original), "Loading never rewrites the save.");
            Assert.That(File.Exists(slot.Store.BackupPath), Is.False, "Loading neither creates nor rotates a backup.");
            Assert.That(loaded.schemaVersion, Is.EqualTo(current));
            Assert.That(EpisodeValidation.TryValidate(loaded, out var error), Is.True, error);
            AssertRulesKept(payload, loaded);

            var (digest, state, text, final) = Replay(id, loaded);
            Assert.That(digest, Is.EqualTo((string)entry["digest"]),
                id + ": the season played from this save is not the season its reference build " + entry["referenceBuild"] + " played.");
            Assert.That(state, Is.EqualTo((string)entry["stateDigest"]), id + ": the state this build writes for the old season moved.");
            Assert.That(text, Is.EqualTo((string)entry["textDigest"]), id + ": the words this build writes for the old season moved.");
            Assert.That(final.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(final.winnerId, Is.EqualTo((string)entry["winnerId"]));

            // The disk format changes only on an explicit save: the original bytes become the backup, and the season reloads as it was.
            slot.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(slot.Original), "The pre-migration bytes are kept as the backup.");
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(CurrentOldSaveTrace.Checkpoint(again, current), Is.EqualTo(CurrentOldSaveTrace.Checkpoint(loaded, current)), "Saved and loaded again, the season is unchanged.");
        }

        /// <summary>
        /// A migration keeps every rule field the save carries and starts each one it adds where its step says: the
        /// voting blocs, the NPC conversations, the action budget, deals, house events, storylines, the story world, the
        /// read, the levers, the week and agency the week after the save's (schemas 5, 6, 9 to 12 and 16 to 20); the
        /// competition rules at 1 (13); the Have-Nots, the strategy windows and every rule from schema 21 on switched off.
        /// The story world a migration opens (16) plays story rules 9, the step's literal, whatever a new season plays.
        /// </summary>
        private static void AssertRulesKept(JObject payload, EpisodeState loaded)
        {
            int week = (int)payload["week"];
            var after = new Dictionary<string, int>
            {
                ["blocRulesStartWeek"] = week + 1,
                ["competitionRulesVersion"] = 1,
                ["socialBudgetRulesStartWeek"] = week + 1, ["dealRulesStartWeek"] = week + 1, ["eventRulesStartWeek"] = week + 1,
                ["storyRulesStartWeek"] = week + 1,
                ["readRulesStartWeek"] = week + 1, ["leverRulesStartWeek"] = week + 1, ["weekRulesStartWeek"] = week + 1, ["agencyRulesStartWeek"] = week + 1,
            };
            foreach (var field in ShippedRules.Fields(loaded).Where(f => !f.Key.Contains(".")))
            {
                if (payload[field.Key] is JValue kept)
                    Assert.That(field.Value, Is.EqualTo((int)kept), field.Key + " is the save's own.");
                else
                    Assert.That(field.Value, Is.EqualTo(after.TryGetValue(field.Key, out int start) ? start : 0), field.Key + " is the migration's.");
            }
            if (payload["story"] is JObject story)
            {
                Assert.That(loaded.story.rulesStartWeek, Is.EqualTo((int)story["rulesStartWeek"]));
                Assert.That(loaded.story.rulesVersion, Is.EqualTo((int)story["rulesVersion"]));
            }
            else
            {
                Assert.That(loaded.story.rulesStartWeek, Is.EqualTo(week + 1), "The story world opens the week after the save's.");
                Assert.That(loaded.story.rulesVersion, Is.EqualTo(StoryRules.Threads), "The story world opens under the step's own rules, not a new season's.");
            }
            if (payload["npcSocial"] is JObject npcSocial)
                Assert.That(loaded.npcSocial.rulesStartWeek, Is.EqualTo((int)npcSocial["rulesStartWeek"]));
            else
                Assert.That(loaded.npcSocial.rulesStartWeek, Is.EqualTo(week + 1), "NPC conversations begin the week after the save's.");
        }

        /// <summary>The recorded script, applied: every command accepted, every checkpoint hashed at the reference schema.</summary>
        private static (string season, string state, string text, EpisodeState final) Replay(string id, EpisodeState loaded, Action<EpisodeState, EpisodeState> each = null)
        {
            var replay = JObject.Parse(File.ReadAllText(Path.Combine(Root, (string)Entry(id)["replay"])));
            int schema = (int)replay["referenceSchema"];
            var expected = (JArray)replay["checkpoints"];
            var season = new List<string>();
            var state = new List<string>();
            var text = new List<string>();
            void Check(EpisodeState s, int index)
            {
                var (g, b, t) = CurrentOldSaveTrace.Checkpoint(s, schema);
                season.Add(g); state.Add(b); text.Add(t);
                Assert.That(g.Substring(0, 16), Is.EqualTo((string)expected[index]["season"]),
                    id + ": checkpoint " + index + " (week " + s.week + " " + s.phase + ", revision " + s.revision + ") is not the reference build's.");
            }

            Check(loaded, 0);
            each?.Invoke(null, loaded);
            var engine = new EpisodeEngine(loaded);
            int i = 0;
            foreach (JObject row in (JArray)replay["commands"])
            {
                var before = engine.Snapshot;
                var command = row.ToObject<EpisodeCommand>();
                var applied = engine.Apply(command);
                i++;
                Assert.That(applied.accepted, Is.True, id + ": command " + i + " (" + command.kind + ", week " + before.week + " " + before.phase + ") refused: " + applied.reason);
                Check(applied.state, i);
                each?.Invoke(before, applied.state);
            }
            Assert.That(i, Is.EqualTo(expected.Count - 1));
            return (OldSaveTrace.Digest(season), OldSaveTrace.Digest(state), OldSaveTrace.Digest(text), engine.Snapshot);
        }

        // ------------------------------------------------------------------ the readers

        [TestCaseSource(nameof(Episodes))]
        public void EveryReaderRendersAnOldSaveWithoutAHiddenBallotOrOdds(string id)
        {
            using var slot = new Slot((string)Entry(id)["save"]);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            int states = 0, lines = 0, hidden = 0;
            Replay(id, loaded, (before, after) =>
            {
                // The loaded state, every phase and every eviction stage the season passes through, and its end.
                if (before != null && before.phase == after.phase && before.evictionStage == after.evictionStage && before.week == after.week
                    && after.phase != EpisodePhase.Finished) return;
                var read = ReadersOf(after, id);
                states++; lines += read.Count;
                hidden += AssertNoHiddenBallot(after, read, id);
                AssertNoOdds(read, id + " week " + after.week + " " + after.phase);
            });
            TestContext.Out.WriteLine(id + ": " + states + " states read, " + lines + " lines, " + hidden + " ballots the player could not place checked.");
            Assert.That(states, Is.GreaterThan(0));
            Assert.That(lines, Is.GreaterThan(0));
        }

        /// <summary>
        /// Every line the simulation and presentation readers give the screens for a state (not the screens' own text
        /// assembly, which formats these lines): the ballot sentinel's readers (Your week, the houseguest
        /// notes, the commitments, the finalist read, the jury house, the finale's receipts, the player-visible log and
        /// the player's memories, which the notebook, the diary room and a profile print), and beside them Your word,
        /// the alliance read, the vote read where it is available, the weekly recap of every week, the season report's
        /// lines and the career entry at the end. Every string in what each returns, gathered by reflection.
        /// </summary>
        private static List<string> ReadersOf(EpisodeState s, string id)
        {
            var lines = new List<string>();
            void Add(string reader, Func<object> read)
            {
                List<string> said;
                try { said = StringsOf(read(), 0).ToList(); }
                catch (Exception error) { Assert.Fail(id + " week " + s.week + " " + s.phase + ": " + reader + " threw " + error); return; }
                lines.AddRange(said);
            }
            Add("the ballot sentinel's readers", () => BallotPrivacyTests.ReaderLines(s));
            Add("Your word", () => new object[] { YourWord.Word(s), YourWord.Breaches(s).Select(f => YourWord.HeardBy(s, f).Select(l => YourWord.HeardLine(s, f, l)).ToList()).ToList() });
            Add("the alliance read", () => AllianceRead.Read(s));
            Add("the vote read", () => VoteRead.Available(s) ? (object)VoteRead.Read(s) : null);
            // The recap is a week's own screen: its lines are dated by the week it recaps, as the other readers' are.
            foreach (var recap in WeeklyRecap.Season(s))
                Add("the weekly recap of week " + recap.week, () => StringsOf(recap, 0).Select(line => "Week " + recap.week + ": " + line).ToList());
            Add("the season report", () => new object[] { SeasonReport.CountLine(s), SeasonReport.GameSenseLine(s) });
            if (s.phase == EpisodePhase.Finished)
            {
                Add("the career entry", () => CareerLedger.Entry(s));
                Assert.That(SeasonBallots.Open(s), Is.True);
            }
            else
            {
                Assert.That(SeasonBallots.Open(s), Is.False, id + ": the tapes are sealed before the finale.");
                Assert.That(SeasonBallots.Read(s), Is.Empty, id + ": the table reads no ballot before the finale.");
            }
            return lines.Where(line => !string.IsNullOrEmpty(line)).ToList();
        }

        /// <summary>
        /// The ballot sentinel (BallotPrivacyTests) over these lines: a ballot the engine logged privately to its voter,
        /// in a week the player cannot place it, and a ballot cast but not yet revealed, are never named by a line. The
        /// season report's ballot table and the jury's ballots are left out by not being read: they open at the finale.
        /// </summary>
        private static int AssertNoHiddenBallot(EpisodeState s, List<string> lines, string id)
        {
            var hidden = BallotPrivacyTests.PrivateBallots(s).Where(b => !KnownBallots.Read(s, b.week).Knows(b.voterId)).ToList();
            if (s.phase == EpisodePhase.Eviction && !s.evictionResolved)
            {
                var sheet = KnownBallots.Read(s, s.week);
                hidden.AddRange(s.votes.Where(v => v.voterId != s.playerId && !sheet.Knows(v.voterId)).Select(v => (s.week, v.voterId, v.targetId)));
            }
            // The finalist read's juror leans, as the sentinel's readers print them ("Quinn · Emma · voted to evict them").
            var leans = new HashSet<string>(FinalistRead.Others(s).SelectMany(finalist =>
                FinalistRead.Read(s, finalist.id)?.jurors.Select(lean => finalist.name + " · " + lean.name + " · " + lean.reason) ?? Enumerable.Empty<string>()));
            foreach (var (week, voterId, targetId) in hidden)
            {
                // An undated line names no week, so the sentinel cannot tell which ballot it tells (F8). One reader writes
                // such a line about a ballot the player does know, and only its line is excused, only for that ballot: the
                // finalist read's juror lean, when the same voter's ballot against the same houseguest is known in another
                // week (s21-director-w1-hoh: unknown in week 1, proven in week 4; also s19, s22 early and s25). Every other
                // undated line is held against every hidden ballot.
                bool knownElsewhere = KnownBallots.Weeks(s).Any(other => other != week && KnownBallots.Read(s, other).TargetOf(voterId) == targetId);
                foreach (var line in lines)
                {
                    var dated = BallotPrivacyTests.WeekOf(line);
                    if (dated != null && dated != week) continue;
                    if (dated == null && knownElsewhere && leans.Contains(line)) continue;
                    Assert.That(BallotPrivacyTests.NamesTheBallot(s, line, voterId, targetId), Is.False,
                        id + ", week " + week + ": " + s.Find(voterId).name + "'s ballot is not the player's to know, and a reader says it: \"" + line + "\"");
                    Assert.That(KnownBallots.TellsAnUnknownBallot(s, voterId, BallotPrivacyTests.Unprefixed(line), week), Is.False,
                        id + ", week " + week + ": a reader tells " + s.Find(voterId).name + "'s ballot by its verdict: \"" + line + "\"");
                }
            }
            return hidden.Count;
        }

        /// <summary>
        /// A number for odds: a percentage in a sentence that speaks of somebody's chance - how likely they are to say yes,
        /// accept, vote, keep or break their word. The player is shown KnownOdds' words for those, never the number. A
        /// percentage anywhere else is a competition's own figure: its rules ("drains grip 70% faster") or the player's
        /// performance ("Played: performance 50%").
        /// </summary>
        private static readonly Regex Percentage = new Regex(@"\d\s*%|\d\s*per ?cent", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly string[] ChanceWords = { "chance", "odds", "likel", "probab", "accept", "agree", "say yes", "succeed", "success", "vote", "evict", "trust", "keep", "break", "lean" };

        private static void AssertNoOdds(List<string> lines, string where)
        {
            foreach (var line in lines)
                foreach (var sentence in line.Split(new[] { ". ", "? ", "! ", "\n", " · " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Percentage.IsMatch(sentence)) continue;
                    string lower = sentence.ToLowerInvariant();
                    Assert.That(ChanceWords.Where(word => lower.Contains(word)), Is.Empty, where + ": a reader shows a number for odds: \"" + line + "\"");
                }
        }

        /// <summary>Every string a reader's answer holds, through lists and the readers' own types; never a saved DTO's (*State), which a screen prints only through a reader.</summary>
        private static IEnumerable<string> StringsOf(object value, int depth)
        {
            if (value == null || depth > 6) yield break;
            if (value is string text) { yield return text; yield break; }
            if (value is IEnumerable items)
            {
                foreach (var item in items) foreach (var s in StringsOf(item, depth + 1)) yield return s;
                yield break;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.Name.EndsWith("State", StringComparison.Ordinal)) yield break;
            if (type.IsGenericType && type.FullName.StartsWith("System.ValueTuple", StringComparison.Ordinal))
            {
                foreach (var field in type.GetFields()) foreach (var s in StringsOf(field.GetValue(value), depth + 1)) yield return s;
                yield break;
            }
            if (type.Namespace == null || !type.Namespace.StartsWith("Gamesim", StringComparison.Ordinal)) yield break;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                foreach (var s in StringsOf(field.GetValue(value), depth + 1)) yield return s;
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.GetIndexParameters().Length == 0 && p.CanRead))
                foreach (var s in StringsOf(property.GetValue(value), depth + 1)) yield return s;
        }

        // ------------------------------------------------------------------ the seventeen historical readers

        /// <summary>
        /// The seventeen readers Codex's whole-save plan held open (PORT_COMPLETION_20261010, "all17"; the plan's hold
        /// map): asked of every old save, as it loads and where its recorded season ends, each gives the answer this build
        /// gave when the fixture was curated. So a change that moves how any of them reads an old season - a fresh-season
        /// rule, a balance retune, a mode-2 refactor - fails here, fixture by fixture, before it reaches a player's file.
        /// </summary>
        [TestCaseSource(nameof(Episodes))]
        public void TheSeventeenHistoricalReadersGiveTheirPinnedAnswersOnEveryOldSave(string id)
        {
            var entry = Entry(id);
            using var slot = new Slot((string)entry["save"]);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            string first = HoldAnswers(loaded);
            var (_, _, _, final) = Replay(id, loaded);
            string last = HoldAnswers(final);
            Assert.That(OldSaveTrace.Sha256(first + "\n" + last).Substring(0, 16), Is.EqualTo((string)entry["holdsDigest"]),
                id + ": a historical reader reads this old season otherwise. Loaded: " + Shorten(first) + " Ended: " + Shorten(last));
        }

        private static string Shorten(string text) => text.Length <= 600 ? text : text.Substring(0, 600) + "...";

        /// <summary>
        /// Every answer of the seventeen readers for one state, as canonical JSON: each deal and promise through
        /// CommitmentReferences (Deals, FindDeal, FindPromise), each deal's breaker (FinalistRead.DealBreaker) and whether
        /// its outcome is known (KnownBallots.DealOutcomeKnown), the final argument's references (FinalArgument.Resolves),
        /// each jury exchange's receipt week (FinaleQuestions.ReceiptWeek), the storage and reference validators
        /// (UnifiedCommitmentHearings.ValidateStorage and TryValidateProspectiveVoteStorage, UnifiedSafetySaveReferences.TryValidate
        /// and TryValidateProspectiveVote, UnifiedVoteFamilyValidation.TryValidate), and the canonical histories
        /// (UnifiedCommitmentHistory.Breaches and Fulfillments, UnifiedVoteHistory.Incidents, Fulfillments and FindDecision).
        /// A reader that refuses the state answers with its exception's type: that refusal is pinned too.
        /// </summary>
        internal static string HoldAnswers(EpisodeState s)
        {
            var answers = new JObject();
            void Ask(string name, Func<object> read)
            {
                JToken value;
                try
                {
                    object result = read();
                    value = result == null ? JValue.CreateNull() : result as JToken ?? new JValue(result);
                }
                catch (Exception error) when (!(error is AssertionException))
                {
                    value = new JValue("threw " + (error is TargetInvocationException wrapped && wrapped.InnerException != null ? wrapped.InnerException : error).GetType().Name);
                }
                answers[name] = value;
            }
            JToken Fields(object row) => row == null ? JValue.CreateNull() : (JToken)OldSaveTrace.Fields(row);
            JToken Each<T>(IEnumerable<T> rows) => new JArray(rows.Select(row => Fields(row)));

            Ask("CommitmentReferences.Deals", () => new JArray(CommitmentReferences.Deals(s).Select(d => d.id + "/" + d.type + "/" + d.status)));
            Ask("CommitmentReferences.FindDeal", () => new JArray(CommitmentReferences.Deals(s).Select(d => CommitmentReferences.FindDeal(s, d.id)?.id)));
            Ask("CommitmentReferences.FindPromise", () => new JArray(s.promises.Select(p => CommitmentReferences.FindPromise(s, p.id)?.id)));
            Ask("FinalistRead.DealBreaker", () => new JArray(CommitmentReferences.Deals(s).Select(d => FinalistRead.DealBreaker(s, d))));
            Ask("KnownBallots.DealOutcomeKnown", () => new JArray(CommitmentReferences.Deals(s).Select(d => KnownBallots.DealOutcomeKnown(s, d))));
            Ask("FinalArgument.Resolves", () => new JArray((s.finalArgument?.momentRefs ?? new List<string>()).Select(r => FinalArgument.Resolves(s, r))));
            Ask("FinaleQuestions.ReceiptWeek", () => new JArray(s.juryExchanges.Select(x => FinaleQuestions.ReceiptWeek(s, x))));
            Ask("UnifiedCommitmentHearings.ValidateStorage", () => UnifiedCommitmentHearings.ValidateStorage(s, out string error) + " " + error);
            Ask("UnifiedCommitmentHearings.TryValidateProspectiveVoteStorage", () => Internal(typeof(UnifiedCommitmentHearings), "TryValidateProspectiveVoteStorage", s));
            Ask("UnifiedCommitmentHistory.Breaches", () => Each(UnifiedCommitmentHistory.Breaches(s)));
            Ask("UnifiedCommitmentHistory.Fulfillments", () => Each(UnifiedCommitmentHistory.Fulfillments(s)));
            Ask("UnifiedSafetySaveReferences.TryValidate", () => UnifiedSafetySaveReferences.TryValidate(s, out string error) + " " + error);
            Ask("UnifiedSafetySaveReferences.TryValidateProspectiveVote", () => Internal(typeof(UnifiedSafetySaveReferences), "TryValidateProspectiveVote", s));
            Ask("UnifiedVoteFamilyValidation.TryValidate", () => UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out string error) + " " + error);
            Ask("UnifiedVoteHistory.FindDecision", () => LegacyDigestSchema30Observer.FindDecisionAnswers28(s));
            Ask("UnifiedVoteHistory.Fulfillments", () => Each(UnifiedVoteHistory.Fulfillments(s)));
            Ask("UnifiedVoteHistory.Incidents", () => Each(UnifiedVoteHistory.Incidents(s)));
            return OldSaveTrace.Canonical(answers, false);
        }

        /// <summary>One of the two internal validators, by reflection: the test assembly sees no internals (ProspectiveVoteFacade).</summary>
        private static string Internal(Type owner, string name, EpisodeState s)
        {
            var method = owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(EpisodeState), typeof(string).MakeByRefType() }, null);
            Assert.That(method, Is.Not.Null, owner.Name + "." + name);
            var arguments = new object[] { s, null };
            bool ok = (bool)method.Invoke(null, arguments);
            return ok + " " + arguments[1];
        }

        // ------------------------------------------------------------------ the alumni-5 cell

        /// <summary>
        /// The cell S6 froze (ALL-STAR-HISTORIES-PLAN 14.I, 14.J): the shipped All-Stars season at alumni rules 5, saved
        /// at its week-3 campaign with what only 5 writes standing in it - a houseguests' own pact grown past two while a
        /// known pair is live (14.L), private to its members and from an earlier week, so it is a join and not a birth.
        /// The four cases every fixture has load and replay it, read it with every reader, pin its seventeen holds and
        /// recover it from the backup; this one holds the file to the state it was chosen for: two known pairs live, so a
        /// bound of four, and the recruit's week-3 join rows beside both founders, both ways.
        /// A 12-house (seed 3387): no 8-house seed of 3002 to 3201 holds a grown pact at its week-3 campaign.
        /// </summary>
        [Test]
        public void TheAlumniFiveCellIsSavedWithAHouseguestsPactGrownPastTwoAndKeptToItself()
        {
            const string id = "s30-mode2-allstars-alumni5-w3-campaign";
            var entry = Entry(id);
            using var slot = new Slot((string)entry["save"]);
            Assert.That(slot.Store.TryLoad(out var s, out string message), Is.True, message);
            Assert.That(s.alumniRulesVersion, Is.EqualTo(AlumniRules.GrowingPacts), "Saved at alumni rules 5, the value S6 froze.");
            Assert.That((int)entry["rules"]["alumniRulesVersion"], Is.EqualTo(AlumniRules.GrowingPacts));
            Assert.That(s.week, Is.EqualTo(3));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(s.contestants.Count, Is.EqualTo(12), "The twelve-house the plan names when no eight-house seed has a grown pact.");

            Assert.That(KnownBlocs.LivePairs(s).Count, Is.EqualTo(2), "Two known pairs are live in the saved week.");
            int bound = KnownBlocs.CounterPactBound(s);
            Assert.That(bound, Is.EqualTo(4), "Two live pairs, so a houseguests' pact may hold four.");
            var grown = NpcAlliances.NpcOnlyPacts(s).Where(p => NpcAlliances.HouseguestsOwn(p) && p.members.Count > 2).ToList();
            Assert.That(grown.Select(p => p.id), Is.EqualTo(new[] { "alliance-npc-203" }), "One houseguests' pact grown past two.");
            var pact = grown[0];
            Assert.That(pact.members, Is.EqualTo(new[] { "chelsie-baham", "xavier-prather", "vanessa-rousso" }),
                "Its founders first, as born, and the recruit on the end.");
            Assert.That(pact.members.Count, Is.LessThanOrEqualTo(bound), "Within the bound, the live pairs plus two and never past four.");
            foreach (string member in pact.members)
            {
                var who = s.Find(member);
                Assert.That(who.isPlayer, Is.False, member);
                Assert.That(who.status, Is.EqualTo(ContestantStatus.Active), member + " is still in the house.");
                Assert.That(KnownBlocs.InLivePair(s, member), Is.False, member + " is in no live known pair.");
            }

            var fact = Knowledge.Of(s, FactKinds.Alliance, pact.id);
            Assert.That(fact, Is.Not.Null, "The pact's fact.");
            Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Private), "Nobody outside the pact has heard of it.");
            Assert.That(fact.knowers, Is.EquivalentTo(pact.members), "Known to its members, the recruit among them, and to nobody else.");
            Assert.That(fact.week, Is.LessThan(s.week), "Founded in an earlier week than the save's.");

            // The third seat is a join: the recruit's "was joined" rows beside each founder, both ways, in the save's week,
            // and none between the founders, whose row is the birth.
            string joined = pact.name + " was joined", recruit = pact.members[2];
            List<int> JoinWeeks(string from, string to) => s.relationships.Where(r => r.fromId == from && r.toId == to)
                .SelectMany(r => r.events).Where(e => e.type == "alliance-formed" && e.description == joined).Select(e => e.week).ToList();
            foreach (string founder in pact.members.Take(2))
            {
                Assert.That(JoinWeeks(founder, recruit), Is.EqualTo(new[] { s.week }), founder + " to " + recruit + ": joined in the save's week.");
                Assert.That(JoinWeeks(recruit, founder), Is.EqualTo(new[] { s.week }), recruit + " to " + founder + ": joined in the save's week.");
            }
            Assert.That(JoinWeeks(pact.members[0], pact.members[1]).Concat(JoinWeeks(pact.members[1], pact.members[0])), Is.Empty,
                "The founders' own row is the birth, not a join.");
        }

        // ------------------------------------------------------------------ the dialogue-1 cell at 30

        /// <summary>
        /// The last cell the cast screen's chip writes on dialogue rules 1 (OLD-SAVE-ACCEPTANCE 2.10): the shipped Regular
        /// season of eight with the conversation preview on, at schema 30 on tuning rules 1 and alumni rules 0, written by
        /// B9's head (7ab18ab0), the last build whose chip starts rules 1. The four cases every fixture has load and replay
        /// it, read it with every reader, pin its seventeen holds and recover it from the backup; this one holds the file to
        /// the point it was saved at: the week-1 campaign with the player nominated, one conversation open with the voter,
        /// its opening answered and spent, and D1's case node waiting unanswered. Rules 1 copies no statement.
        /// Seed 3101, the first from 3101 at which the recorder's converse opens a conversation.
        /// </summary>
        [Test]
        public void TheDialogueOneCellAtThirtyIsSavedMidConversationAtItsCaseNode()
        {
            const string id = "s30-mode2-dialogue1-tuning1-w1-campaign";
            var entry = Entry(id);
            using var slot = new Slot((string)entry["save"]);
            Assert.That(slot.Store.TryLoad(out var s, out string message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"), "Saved at 30, it loads as it is.");
            Assert.That(s.schemaVersion, Is.EqualTo(30));
            Assert.That(s.dialogueRulesVersion, Is.EqualTo(DialogueRules.Rules1), "Saved on dialogue rules 1, the chip's before rules 2.");
            Assert.That((int)entry["rules"]["dialogueRulesVersion"], Is.EqualTo(DialogueRules.Rules1));
            Assert.That(s.tuningRulesVersion, Is.EqualTo(TuningRules.Bands), "On tuning rules 1, as every fresh season from B9.");
            Assert.That((int)entry["rules"]["tuningRulesVersion"], Is.EqualTo(TuningRules.Bands));
            Assert.That(s.alumniRulesVersion, Is.EqualTo(0), "The Regular cast: no alumnus, alumni rules 0.");
            Assert.That(s.alumniCatalogEdition, Is.Null);
            Assert.That(DialogueRules.On(s), Is.True);
            Assert.That(s.week, Is.EqualTo(1));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(s.nominees, Does.Contain(s.playerId), "The player is nominated: the campaign the conversation belongs to.");

            Assert.That(s.dialogue.episodes.Count, Is.EqualTo(1), "One conversation in the store.");
            var episode = DialogueRules.Active(s);
            Assert.That(episode, Is.SameAs(s.dialogue.episodes[0]), "It is still open.");
            Assert.That(episode.npcId, Is.EqualTo("alex-chen"), "The first voter in the engine's order the conversation can start with.");
            Assert.That(s.Find(episode.npcId).status, Is.EqualTo(ContestantStatus.Active));
            Assert.That(episode.occasionKind, Is.EqualTo(DialogueCampaignV1.Kind));
            Assert.That(episode.occasionId, Is.EqualTo(DialogueRules.Occasion(s.week, episode.npcId)));
            Assert.That(episode.contentVersion, Is.EqualTo(DialogueCampaignV1.ContentVersion));
            Assert.That(episode.entry.kind, Is.EqualTo(DialogueCampaignV1.EntryKind), "Entered with an action.");
            Assert.That(episode.entry.windowActionAfter, Is.EqualTo(episode.entry.windowActionBefore + 1), "One action spent.");
            Assert.That(episode.responses.Select(r => r.responseKey), Is.EqualTo(new[] { DialogueCampaignV1.TemplateId + "/hear-your-case" }),
                "The opening answered: the voter will hear the case.");
            Assert.That(episode.statement, Is.Null, "Rules 1 copies no statement.");
            Assert.That(episode.evidence, Is.Empty);
            Assert.That(episode.time, Is.Null, "No ending receipt: the conversation has not ended.");

            var cycle = s.storylines.Single(x => x.id == episode.id);
            Assert.That(cycle.templateId, Is.EqualTo(DialogueCampaignV1.TemplateId), "No public rescue to cite: D1's own template.");
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Active));
            Assert.That(cycle.path.Select(p => p.beatId + "/" + p.optionId), Is.EqualTo(new[] { DialogueCampaignV1.Open + "/" + DialogueCampaignV1.OpenCase }));
            Assert.That(cycle.beatId, Is.EqualTo(DialogueCampaignV1.Case), "Saved at the case node.");
            var item = s.houseEvents.Single(x => x.id == cycle.eventId);
            Assert.That(item.resolved, Is.False, "The case node waits for the player's answer.");
            Assert.That(item.choices.Select(c => c.optionId), Is.EqualTo(new[] { DialogueCampaignV1.Appeal, DialogueCampaignV1.Leave }));
            Assert.That(item.lapseOptionId, Is.EqualTo(DialogueCampaignV1.Leave));
        }

        // ------------------------------------------------------------------ the dialogue-2 cell at 30

        /// <summary>
        /// The cell the cast screen's chip writes on dialogue rules 2 (OLD-SAVE-ACCEPTANCE 2.11): the shipped Regular season
        /// of eight with the conversation preview on, at schema 30 on tuning rules 1 and alumni rules 0, written by the
        /// chip-2 stack's head (779506e9), the build whose chip starts rules 2 with the conversation budget P1(d) left at 32.
        /// The four cases every fixture has load and replay it, read it with every reader, pin its seventeen holds and
        /// recover it from the backup; this one holds the file to the point it was saved at: the week-1 campaign with the
        /// player nominated, one conversation open with the voter on the vote-word tree, its open node's case taken with the
        /// entry's one action, the voter's answer drawn and recorded (a concern: no word, so no statement is copied), and
        /// the reply node it was drawn for waiting unanswered, offering no fact to cite. Seed 3101, the first from 3101 at
        /// which the recorder's converse opens a rules-2 conversation and saves it at its reply node: the dialogue-1 cell's
        /// season, which the two saves share up to the conversation.
        /// </summary>
        [Test]
        public void TheDialogueTwoCellAtThirtyIsSavedMidConversationAtItsReplyNode()
        {
            const string id = "s30-mode2-dialogue2-tuning1-w1-campaign";
            var entry = Entry(id);
            using var slot = new Slot((string)entry["save"]);
            Assert.That(slot.Store.TryLoad(out var s, out string message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"), "Saved at 30, it loads as it is.");
            Assert.That(s.schemaVersion, Is.EqualTo(30));
            Assert.That(s.dialogueRulesVersion, Is.EqualTo(DialogueRules.Rules2), "Saved on dialogue rules 2, the chip's since the chip-2 stack.");
            Assert.That((int)entry["rules"]["dialogueRulesVersion"], Is.EqualTo(DialogueRules.Rules2));
            Assert.That(s.tuningRulesVersion, Is.EqualTo(TuningRules.Bands), "On tuning rules 1, as every fresh season from B9.");
            Assert.That((int)entry["rules"]["tuningRulesVersion"], Is.EqualTo(TuningRules.Bands));
            Assert.That(s.alumniRulesVersion, Is.EqualTo(0), "The Regular cast: no alumnus, alumni rules 0.");
            Assert.That(s.alumniCatalogEdition, Is.Null);
            Assert.That(DialogueRules.On(s), Is.True);
            Assert.That(s.week, Is.EqualTo(1));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(s.nominees, Does.Contain(s.playerId), "The player is nominated: the campaign the conversation belongs to.");

            Assert.That(s.dialogue.episodes.Count, Is.EqualTo(1), "One conversation in the store.");
            var episode = DialogueRules.Active(s);
            Assert.That(episode, Is.SameAs(s.dialogue.episodes[0]), "It is still open.");
            Assert.That(episode.npcId, Is.EqualTo("alex-chen"), "The first voter in the engine's order the conversation can start with.");
            Assert.That(s.Find(episode.npcId).status, Is.EqualTo(ContestantStatus.Active));
            Assert.That(episode.occasionKind, Is.EqualTo(DialogueCampaignV1.Kind));
            Assert.That(episode.occasionId, Is.EqualTo(DialogueRules.Occasion(s.week, episode.npcId)));
            Assert.That(episode.contentVersion, Is.EqualTo(DialogueCampaignWord.ContentVersion));
            Assert.That(episode.entry.kind, Is.EqualTo(DialogueRules.ActionEntry), "Entered with an action.");
            Assert.That(episode.entry.windowActionAfter, Is.EqualTo(episode.entry.windowActionBefore + 1), "One action spent.");
            Assert.That(episode.responses.Select(r => r.responseKey), Is.EqualTo(new[] { DialogueCampaignWord.TemplateId + "/" + DialogueCampaignWord.Concern }),
                "The voter's answer, recorded as the open node's response: a concern.");
            Assert.That(episode.statement, Is.Null, "A concern is no word: nothing is copied as a statement.");
            Assert.That(episode.evidence, Is.Empty, "Nothing cited yet: the reply is unanswered.");
            Assert.That(episode.time, Is.Null, "No ending receipt: the conversation has not ended.");

            var cycle = s.storylines.Single(x => x.id == episode.id);
            Assert.That(cycle.templateId, Is.EqualTo(DialogueCampaignWord.TemplateId), "Rules 2's one tree.");
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Active));
            Assert.That(cycle.path.Select(p => p.beatId + "/" + p.optionId), Is.EqualTo(new[] { DialogueCampaignWord.Open + "/" + DialogueCampaignWord.OpenCase }),
                "The entry took the open node's case.");
            Assert.That(cycle.beatId, Is.EqualTo(DialogueCampaignWord.ReplyBeat(DialogueCampaignWord.Concern, false, false)),
                "Saved at the reply node drawn for a concern, with no rescue and no lie to offer.");
            var item = s.houseEvents.Single(x => x.id == cycle.eventId);
            Assert.That(item.resolved, Is.False, "The reply node waits for the player's answer.");
            Assert.That(item.choices.Select(c => c.optionId),
                Is.EqualTo(new[] { DialogueCampaignWord.AskConcern, DialogueCampaignWord.Press, DialogueCampaignWord.Leave }));
            Assert.That(item.lapseOptionId, Is.EqualTo(DialogueCampaignWord.Leave));
        }

        // ------------------------------------------------------------------ the story-rules-10 cell

        private const string StoryTenCell = "s30-mode2-allweek-story10-tuning1-w2-hoh";

        /// <summary>
        /// The first cell at story rules 10 (STORY-RULES-10-PLAN §8; OLD-SAVE-ACCEPTANCE 2.12): the shipped Regular season of
        /// eight as every new season starts since the enrolment, on Blame and tuning rules 1, written and recorded by the
        /// enrolled head (5863d964). The four cases every fixture has load and replay it, read it with every reader, pin its
        /// seventeen holds and recover it from the backup; this one holds the file to the point it was saved at and to the veto
        /// it was chosen for: the first state of week 2's Head of Household, at story rules 10, after week 1's veto, which a
        /// houseguest who was not the Head of Household used to save herself, with a houseguest named. So the replacement
        /// holds the Head of Household's grudge and none against the holder, where story rules 9 writes a forty.
        /// Seed 3102, the first from 3101 with such a veto before the save point and another after the load.
        /// </summary>
        [Test]
        public void TheStoryRulesTenCellIsSavedAtWeekTwosHeadOfHouseholdAfterAHouseguestsSelfSave()
        {
            var entry = Entry(StoryTenCell);
            using var slot = new Slot((string)entry["save"]);
            Assert.That(slot.Store.TryLoad(out var s, out string message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"), "Saved at 30, it loads as it is.");
            Assert.That(s.schemaVersion, Is.EqualTo(30));
            Assert.That(s.story.rulesVersion, Is.EqualTo(StoryRules.Blame), "Saved at story rules 10, every new season's since the enrolment.");
            Assert.That((int)entry["story"]["rulesVersion"], Is.EqualTo(StoryRules.Blame));
            Assert.That(s.story.rulesStartWeek, Is.EqualTo(1));
            Assert.That(s.tuningRulesVersion, Is.EqualTo(TuningRules.Bands), "On tuning rules 1, as every fresh season from B9.");
            Assert.That(s.dialogueRulesVersion, Is.EqualTo(0), "A shipped season without the conversation preview.");
            Assert.That(s.alumniRulesVersion, Is.EqualTo(0), "The Regular cast: no alumnus, alumni rules 0.");
            Assert.That(s.alumniCatalogEdition, Is.Null);
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(2), "The shipped mode 2.");
            Assert.That(s.contestants.Count, Is.EqualTo(8));
            Assert.That(s.Find(s.playerId).name, Is.EqualTo("You"));

            // The stop point: the first state of week 2's Head of Household, before it is played.
            Assert.That(s.week, Is.EqualTo(2));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.HoH));
            Assert.That(s.competitionResolved, Is.False, "Week 2's Head of Household is not played yet.");
            Assert.That(s.hohId, Is.Null);
            Assert.That(s.revision, Is.EqualTo(19));
            var replay = JObject.Parse(File.ReadAllText(Path.Combine(Root, (string)entry["replay"])));
            Assert.That((int)replay["checkpoints"][0]["revision"], Is.EqualTo(s.revision), "The script starts where the file stops.");

            // Week 1's veto, before the save point: the Head of Household jordan-taylor put quinn-martinez and casey-wilson up;
            // quinn-martinez won the veto and saved herself, and alex-chen was named (and evicted that week).
            Assert.That(s.previousHohId, Is.EqualTo("jordan-taylor"));
            Assert.That(s.contestants.Where(c => c.vetoWins > 0).Select(c => c.id + " " + c.vetoWins), Is.EqualTo(new[] { "quinn-martinez 1" }),
                "One veto played, won by a houseguest.");
            Assert.That(s.Find("quinn-martinez").nominationWeeks, Is.EqualTo(new[] { 1 }), "The holder was on the block: a self-save.");
            Assert.That(s.Find("alex-chen").nominationWeeks, Is.EqualTo(new[] { 1 }), "The replacement.");
            Assert.That(s.Find("alex-chen").isPlayer, Is.False);
            Assert.That(s.events.Where(e => e.week == 1 && e.kind == "veto").Select(e => e.text),
                Is.EqualTo(new[] { "Quinn Martinez saves herself; Alex Chen is the replacement nominee." }));
            var replaced = s.story.grudges.Single(g => g.holderId == "alex-chen" && g.targetId == "jordan-taylor");
            Assert.That(replaced.cause, Is.EqualTo(GrudgeCauses.Replacement), "The replacement resents the Head of Household who named them.");
            Assert.That(replaced.originWeek, Is.EqualTo(1));
            Assert.That(s.story.grudges.Where(g => g.holderId == "alex-chen" && g.targetId == "quinn-martinez"), Is.Empty,
                "Under Blame a houseguest's self-save forces nobody: no grudge against the holder (story rules 9 writes a forty).");
        }

        /// <summary>
        /// The story-rules-10 cell's script, played from the save: the veto uses it passes through, each with the
        /// replacement's grudge against the holder before and after it. In week 3 emma-brown, who is not the Head of Household
        /// (the player is), saves herself and casey-wilson is named: the Blame rule writes nothing. The player's own three
        /// vetoes keep the forty (C1n, decision 3 as accepted). Then the control: the same commands, the season forced to
        /// story rules 9 at the load, are all accepted, and the only use that changes is week 3's, where 9 writes the forty;
        /// hashed with the story rules put back to 10, so the field alone cannot part them, the season parts from the
        /// recorded one first there, at checkpoint 26, and its digest is another. So the cell pins story rules 10.
        /// </summary>
        [Test]
        public void TheStoryRulesTenCellPlaysTheBlameRuleAfterTheLoadAndPartsFromStoryRulesNineThere()
        {
            var entry = Entry(StoryTenCell);
            var expected = new[]
            {
                "8 week 2: player saves player, quinn-martinez named, HoH jamie-roberts: none -> replacement-veto 40 x1 w2",
                "26 week 3: emma-brown saves emma-brown, casey-wilson named, HoH player: none -> none",
                "43 week 4: player saves player, emma-brown named, HoH riley-johnson: nominated 68 x1 w3 -> replacement-veto 88 x2 w3",
                "61 week 5: player saves player, riley-johnson named, HoH casey-wilson: none -> replacement-veto 40 x1 w5",
            };

            using (var slot = new Slot((string)entry["save"]))
            {
                Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
                var uses = new List<string>();
                int index = -1;
                var (digest, _, _, _) = Replay(StoryTenCell, loaded, (before, after) =>
                {
                    index++;
                    string use = VetoUse(before, after, index);
                    if (use != null) uses.Add(use);
                });
                Assert.That(digest, Is.EqualTo((string)entry["digest"]));
                Assert.That(uses, Is.EqualTo(expected), "The vetoes the script plays through, at story rules 10.");
            }

            // The control: the same commands at story rules 9.
            using (var slot = new Slot((string)entry["save"]))
            {
                Assert.That(slot.Store.TryLoad(out var forced, out string message), Is.True, message);
                Assert.That(forced.story.rulesVersion, Is.EqualTo(StoryRules.Blame));
                forced.story.rulesVersion = StoryRules.Threads;
                var replay = JObject.Parse(File.ReadAllText(Path.Combine(Root, (string)entry["replay"])));
                var checkpoints = (JArray)replay["checkpoints"];
                var asItIs = new List<string>();
                var putBack = new List<string>();
                int partsAsItIs = -1, partsPutBack = -1;
                void Check(EpisodeState s, int at)
                {
                    string recorded = (string)checkpoints[at]["season"];
                    string season = CurrentOldSaveTrace.Checkpoint(s, CurrentOldSaveTrace.Current).season;
                    asItIs.Add(season);
                    if (partsAsItIs < 0 && season.Substring(0, 16) != recorded) partsAsItIs = at;
                    s.story.rulesVersion = StoryRules.Blame;
                    season = CurrentOldSaveTrace.Checkpoint(s, CurrentOldSaveTrace.Current).season;
                    s.story.rulesVersion = StoryRules.Threads;
                    putBack.Add(season);
                    if (partsPutBack < 0 && season.Substring(0, 16) != recorded) partsPutBack = at;
                }
                Check(forced, 0);
                var engine = new EpisodeEngine(forced);
                var uses = new List<string>();
                int i = 0;
                foreach (JObject row in (JArray)replay["commands"])
                {
                    var before = engine.Snapshot;
                    var applied = engine.Apply(row.ToObject<EpisodeCommand>());
                    i++;
                    Assert.That(applied.accepted, Is.True, "At story rules 9, command " + i + " (week " + before.week + " " + before.phase + ") refused: " + applied.reason);
                    Check(applied.state, i);
                    string use = VetoUse(before, applied.state, i);
                    if (use != null) uses.Add(use);
                }
                Assert.That(i, Is.EqualTo(checkpoints.Count - 1), "Every command of the script is played at story rules 9 too.");
                Assert.That(uses, Is.EqualTo(new[]
                {
                    expected[0],
                    "26 week 3: emma-brown saves emma-brown, casey-wilson named, HoH player: none -> replacement-veto 40 x1 w3",
                    expected[2], expected[3],
                }), "At story rules 9 the houseguest's self-save writes the forty; the player's vetoes are as at 10.");
                Assert.That(partsPutBack, Is.EqualTo(26), "With the story rules put back to 10, the season parts first at the houseguest's veto.");
                Assert.That(OldSaveTrace.Digest(putBack), Is.Not.EqualTo((string)entry["digest"]));
                Assert.That(partsAsItIs, Is.EqualTo(0), "As it is, the season's own story rules part it at the load.");
                Assert.That(OldSaveTrace.Digest(asItIs), Is.Not.EqualTo((string)entry["digest"]), "The same commands at story rules 9 give another season digest.");
            }
        }

        /// <summary>
        /// The veto used by the command that led from <paramref name="before"/> to <paramref name="after"/>, as a line: the
        /// checkpoint, the week, the holder, whom they saved, who was named, the Head of Household, and the replacement's grudge
        /// against the holder before and after; null for any other command. A veto use takes one nominee off the block and
        /// puts one on in the same week, and logs the veto line that saves; anything else that moves the block logs none.
        /// </summary>
        private static string VetoUse(EpisodeState before, EpisodeState after, int index)
        {
            if (before == null || before.week != after.week) return null;
            var off = before.nominees.Except(after.nominees).ToList();
            var on = after.nominees.Except(before.nominees).ToList();
            bool saves = after.events.Any(e => e.sequence >= before.nextSequence && e.kind == "veto" && (e.text.Contains(" saves ") || e.text.Contains(" save ")));
            Assert.That(off.Count == 1 && on.Count == 1, Is.EqualTo(saves), "Checkpoint " + index + ": a veto that saves changes the block by one, and only it does.");
            if (!saves) return null;
            string holder = after.vetoHolderId;
            string Row(EpisodeState s)
            {
                var g = s.story.grudges.FirstOrDefault(x => x.holderId == on[0] && x.targetId == holder);
                return g == null ? "none" : g.cause + " " + g.severity.ToString("0.##", CultureInfo.InvariantCulture) + " x" + g.count + " w" + g.originWeek;
            }
            return index + " week " + after.week + ": " + holder + " saves " + off[0] + ", " + on[0] + " named, HoH " + after.hohId + ": " + Row(before) + " -> " + Row(after);
        }

        private const string StoryElevenCell = "s30-mode2-allweek-story11-tuning1-w3-veto-meeting";

        /// <summary>
        /// The first cell at story rules 11 (ROLE-MOMENTS-PLAN R7; OLD-SAVE-ACCEPTANCE 2.13): the shipped Regular season of
        /// 8 as every new season starts since the role moments' enrolment, on Roles and tuning rules 1, saved at the
        /// first state of week 3's veto meeting with the Head of Household's keep-the-block ask (R4, a
        /// <see cref="DealKind.VetoHold"/> row, <see cref="NpcDeals.KeepTheBlockPrefix"/>) waiting for the player's answer.
        /// The four cases every fixture has load and replay it, read it with every reader, pin its seventeen holds and
        /// recover it from the backup; this one holds the file to the point it was saved at and to the ask it was chosen
        /// for: the Head of Household, a houseguest, asks the player, who holds the veto and is off the block, beside a
        /// nominee's own ask. Seed 3520, the first from 3201 at 8 whose plain walk files such an ask by week 4.
        /// </summary>
        [Test]
        public void TheStoryRulesElevenCellIsSavedAtTheVetoMeetingWithTheHeadOfHouseholdsKeepTheBlockAskPending()
        {
            var entry = Entry(StoryElevenCell);
            using var slot = new Slot((string)entry["save"]);
            Assert.That(slot.Store.TryLoad(out var s, out string message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"), "Saved at 30, it loads as it is.");
            Assert.That(s.schemaVersion, Is.EqualTo(30));
            Assert.That(s.story.rulesVersion, Is.EqualTo(StoryRules.Roles), "Saved at story rules 11, every new season's since the role moments' enrolment.");
            Assert.That((int)entry["story"]["rulesVersion"], Is.EqualTo(StoryRules.Roles));
            Assert.That(s.story.rulesStartWeek, Is.EqualTo(1));
            Assert.That(EpisodeEngine.RolesOn(s), Is.True, "The role moments are on.");
            Assert.That(s.tuningRulesVersion, Is.EqualTo(TuningRules.Bands), "On tuning rules 1, as every fresh season from B9.");
            Assert.That(s.dialogueRulesVersion, Is.EqualTo(0), "A shipped season without the conversation preview.");
            Assert.That(s.alumniRulesVersion, Is.EqualTo(0), "The Regular cast: no alumnus, alumni rules 0.");
            Assert.That(s.alumniCatalogEdition, Is.Null);
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(2), "The shipped mode 2.");
            Assert.That(s.contestants.Count, Is.EqualTo(8));
            Assert.That(s.Find(s.playerId).name, Is.EqualTo("You"));

            // The stop point: the first state of week 3's veto meeting, before the player decides.
            Assert.That(s.week, Is.EqualTo(3));
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            Assert.That(s.vetoResolved, Is.False, "The veto is not decided yet.");
            Assert.That(s.revision, Is.EqualTo(43));
            var replay = JObject.Parse(File.ReadAllText(Path.Combine(Root, (string)entry["replay"])));
            Assert.That((int)replay["checkpoints"][0]["revision"], Is.EqualTo(s.revision), "The script starts where the file stops.");

            // The ask: the Head of Household, a houseguest, asks the player - who holds the veto and is off the block - to
            // leave the nominations as they are, beside the nominees' own asks for the veto.
            Assert.That(s.hohId, Is.EqualTo("quinn-martinez"));
            Assert.That(s.Find(s.hohId).isPlayer, Is.False);
            Assert.That(s.vetoHolderId, Is.EqualTo(s.playerId), "The player holds the veto.");
            Assert.That(s.nominees, Is.EquivalentTo(new[] { "riley-johnson", "alex-chen" }));
            Assert.That(s.nominees, Does.Not.Contain(s.playerId));
            var hold = s.deals.Single(d => NpcDeals.IsKeepTheBlockId(d.id));
            Assert.That((hold.id, hold.type, hold.status, hold.proposerId, hold.recipientId, hold.week, hold.expiresWeek),
                Is.EqualTo(("deal-keep-274", DealKind.VetoHold, DealStatus.Proposed, s.hohId, s.playerId, 3, 3)),
                "One keep-the-block ask, from the Head of Household to the player, binding this week, not yet answered.");
            Assert.That(RoleOffers.VetoAsks(s).Where(ask => s.nominees.Contains(ask.proposerId)).Select(ask => ask.proposerId),
                Is.EquivalentTo(new[] { "alex-chen" }), "A nominee's own ask is on the board: the Head of Household's is a counter-offer.");
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
        }

        /// <summary>
        /// The control: the same file with its story rules resealed to 10 (the save's rulesVersion rewritten and its
        /// checksum computed again, as the store computes it) is refused by the store - "A season without the role moments
        /// has none of their records." - and left exactly as it was. What makes it refuse is the Head of Household's
        /// keep-the-block row: below Roles no season holds one (EpisodeValidation). So the cell pins story rules 11: no
        /// season at 10 can stand in for it.
        /// </summary>
        [Test]
        public void TheStoryRulesElevenCellResealedAtStoryRulesTenIsRefusedAndLeftAsItWas()
        {
            var entry = Entry(StoryElevenCell);
            using var slot = new Slot((string)entry["save"]);
            var saveJson = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
            var envelope = (JObject)saveJson.GetMethod("ParseObject", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { File.ReadAllText(slot.Store.SavePath) });
            var payload = (JObject)envelope["state"];
            Assert.That((int)payload["story"]["rulesVersion"], Is.EqualTo(StoryRules.Roles));
            payload["story"]["rulesVersion"] = StoryRules.Blame;
            string canonical = (string)saveJson.GetMethod("Canonical", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { payload });
            envelope["checksum"] = (string)saveJson.GetMethod("Hash", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { canonical });
            File.WriteAllText(slot.Store.SavePath, envelope.ToString(Formatting.None));
            var resealed = File.ReadAllBytes(slot.Store.SavePath);

            Assert.That(new EpisodeSaveStore(slot.Store.SavePath).TryLoad(out var refused, out string message), Is.False,
                "A season at story rules 10 holding the role moments' records is refused.");
            Assert.That(refused, Is.Null);
            Assert.That(message, Does.Contain("A season without the role moments has none of their records."));
            Assert.That(message, Does.Not.Contain("checksum"), "Refused for what it holds, not for a damaged file.");
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(resealed), "The refused file is left as it was.");

            // In memory: the keep-the-block row alone makes it refuse; without it the same season at 10 validates.
            using var original = new Slot((string)entry["save"]);
            Assert.That(original.Store.TryLoad(out var s, out string loaded), Is.True, loaded);
            s.story.rulesVersion = StoryRules.Blame;
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.EqualTo("A season without the role moments has none of their records."));
            s.deals.RemoveAll(d => NpcDeals.IsKeepTheBlockId(d.id));
            Assert.That(EpisodeValidation.TryValidate(s, out error), Is.True, "Without the Head of Household's row, nothing else in the file is the role moments' own: " + error);
        }

        /// <summary>
        /// The ask answered from the load, as a player who loads the file answers it: taken, saved and loaded again through the
        /// store, then kept (the veto not used: the Head of Household thinks eight better of the player) or broken (the veto
        /// used on a nominee: -45, against the player's word); and left alone, as the recorded script leaves it, it lapses at
        /// the decision, where the script's walker uses the veto anyway: the Head of Household was defied (-5 and a grudge),
        /// never a breach. Each end validates.
        /// </summary>
        [Test]
        public void TheStoryRulesElevenCellsKeepTheBlockAskIsKeptBrokenOrLeftFromTheLoad()
        {
            var entry = Entry(StoryElevenCell);
            foreach (bool use in new[] { false, true })
            {
                string what = use ? "taken, then broken" : "taken, then kept";
                using var slot = new Slot((string)entry["save"]);
                Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
                string hoh = loaded.hohId, holdId = loaded.deals.Single(d => NpcDeals.IsKeepTheBlockId(d.id)).id;
                int broken = NpcDeals.BrokenDeals(loaded, loaded.playerId);
                var taken = Applied(new EpisodeEngine(loaded), Act(loaded, EpisodeCommandKind.RespondToDeal, holdId, text: EpisodeEngine.AcceptDeal));
                Assert.That(taken.deals.Single(d => d.id == holdId).status, Is.EqualTo(DealStatus.Active), what);

                // Saved with the word given and loaded again: the row comes back as it was.
                slot.Store.Save(taken);
                Assert.That(new EpisodeSaveStore(slot.Store.SavePath).TryLoad(out var again, out message), Is.True, message);
                Assert.That(JsonConvert.SerializeObject(again.deals.Single(d => d.id == holdId)), Is.EqualTo(JsonConvert.SerializeObject(taken.deals.Single(d => d.id == holdId))), what);

                string saved = use ? again.nominees[0] : null;
                var decide = Act(again, EpisodeCommandKind.ResolveVeto, saved, EpisodeEngine.ReplacementCandidates(again).FirstOrDefault()?.id);
                decide.useVeto = use;
                var settled = Applied(new EpisodeEngine(again), decide);
                var hold = settled.deals.Single(d => d.id == holdId);
                var fromHoh = settled.relationships.Where(r => r.fromId == hoh && r.toId == settled.playerId).SelectMany(r => r.events)
                    .Where(e => e.week == settled.week && (e.type == "deal_fulfilled" || e.type == "deal_broken")).Select(e => e.type + " " + e.impactScore).ToList();
                if (use)
                {
                    Assert.That((hold.status, hold.brokenById, hold.settledWeek), Is.EqualTo((DealStatus.Broken, settled.playerId, settled.week)), what);
                    Assert.That(fromHoh, Is.EqualTo(new[] { "deal_broken -45" }), what + ": High, raised to Critical for an accepted offer.");
                    Assert.That(NpcDeals.BrokenDeals(settled, settled.playerId), Is.EqualTo(broken + 1), what + ": against the player's word.");
                }
                else
                {
                    Assert.That((hold.status, hold.brokenById, hold.settledWeek), Is.EqualTo((DealStatus.Fulfilled, (string)null, settled.week)), what);
                    Assert.That(fromHoh, Is.EqualTo(new[] { "deal_fulfilled 8" }), what + ": kept at the Low weight.");
                    Assert.That(NpcDeals.BrokenDeals(settled, settled.playerId), Is.EqualTo(broken), what);
                }
                Assert.That(EpisodeValidation.TryValidate(settled, out string error), Is.True, what + ": " + error);
            }

            // Left alone, as the recorded script leaves it: the ask lapses at the decision.
            using (var slot = new Slot((string)entry["save"]))
            {
                Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
                string holdId = loaded.deals.Single(d => NpcDeals.IsKeepTheBlockId(d.id)).id;
                string hoh = loaded.hohId, name = loaded.Find(hoh).name;
                int broken = NpcDeals.BrokenDeals(loaded, loaded.playerId);
                EpisodeState decided = null, undecided = null;
                var (digest, _, _, _) = Replay(StoryElevenCell, loaded, (before, after) =>
                {
                    if (decided == null && before != null && before.week == loaded.week && before.deals.Single(d => d.id == holdId).status == DealStatus.Proposed
                        && after.deals.Single(d => d.id == holdId).status != DealStatus.Proposed) { undecided = before; decided = after; }
                });
                Assert.That(digest, Is.EqualTo((string)entry["digest"]));
                Assert.That(decided, Is.Not.Null, "The script settles the ask in its week.");
                Assert.That(decided.deals.Single(d => d.id == holdId).status, Is.EqualTo(DealStatus.Expired), "The script never answers it: it lapses.");
                Assert.That(decided.vetoResolved, Is.True, "Settled at the veto decision.");
                Assert.That(!decided.nominees.SequenceEqual(undecided.nominees), Is.True, "The script's walker uses the veto.");
                Assert.That(decided.events.Count(e => e.kind == "deal" && e.text == "You used the veto after " + name + " asked you not to."), Is.EqualTo(1),
                    "Used anyway, the Head of Household was defied.");
                Assert.That(NpcDeals.BrokenDeals(decided, decided.playerId), Is.EqualTo(broken), "A lapse is never a breach.");
            }
        }

        private static EpisodeCommand Act(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "oldsave-r7-" + kind + "-" + s.revision + "-" + target, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static EpisodeState Applied(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + " " + command.targetId + ": " + result.reason);
            return result.state;
        }

        // ------------------------------------------------------------------ refused on purpose

        /// <summary>
        /// The story-arcs line's own "schema 14" (52cb23df): the story's fields under the number the game-flow line gave
        /// the Have-Nots. This build refuses the file whole, with the reason, and leaves it exactly as it was: it is never
        /// read as the shipped schema 14, and a save over it is refused rather than replacing it.
        /// </summary>
        [TestCaseSource(nameof(Refused))]
        public void AnOldSaveFromABranchsOwnSchemaNumberIsRefusedAndLeftAsItWas(string id)
        {
            var entry = Entry(id);
            using var slot = new Slot((string)entry["save"]);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain((string)entry["refusal"]));
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(slot.Original), "A refused save is never rewritten.");
            Assert.That(File.Exists(slot.Store.BackupPath), Is.False);
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1402u);
            Assert.Throws<InvalidDataException>(() => slot.Store.Save(fresh), "A save over an unreadable file is refused.");
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(slot.Original), "and the file stays as it was.");
        }

        // ------------------------------------------------------------------ recovered from the backup

        /// <summary>
        /// Recovery on the old fixtures (OWNER-DECISIONS-2026-10-22 §4, item 2). Every old save, kept as the backup, is
        /// recovered through the real store twice: with no primary, and with the primary cut off halfway, as an
        /// interrupted write leaves it. Loading refuses the primary and offers the backup. Recovery reads the backup as
        /// loading reads a primary - for an older save its frozen gates and the steps to 30, in memory (since SCH30 a
        /// schema-29 save too, through the frozen schema-29 gate) - and gives the season that loading the same file as the
        /// primary gives, valid at 30. It puts the backup's own bytes back as the primary, not rewritten at 30, and leaves
        /// the backup as it was. A damaged primary
        /// is kept beside them as .before-recovery-*, byte for byte; a missing one leaves nothing to keep.
        /// </summary>
        [TestCaseSource(nameof(Episodes))]
        public void AnOldSaveKeptAsTheBackupIsRecoveredWhetherThePrimaryIsMissingOrDamaged(string id)
        {
            var entry = Entry(id);
            int schema = (int)entry["schema"];
            const int current = CurrentOldSaveTrace.Current;
            using var reference = new Slot((string)entry["save"]);
            var payload = (JObject)Envelope(reference.Store.SavePath)["state"];
            Assert.That(reference.Store.TryLoad(out var loaded, out string loadMessage), Is.True, loadMessage);
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload((JObject)payload.DeepClone(), out bool stepped);
            Assert.That(stepped, Is.EqualTo(schema < current));
            // Read as the store reads it: a saved 0.62 is a double in the tree and a float in the state.
            var expected = Payload(migrated.ToObject<EpisodeState>(Serializer()));

            foreach (bool damaged in new[] { false, true })
            {
                string primary = id + (damaged ? ", a damaged primary" : ", no primary");
                using var slot = new Slot((string)entry["save"]);
                File.Move(slot.Store.SavePath, slot.Store.BackupPath);
                byte[] torn = Torn(slot.Original);
                if (damaged) File.WriteAllBytes(slot.Store.SavePath, torn);

                Assert.That(slot.Store.TryLoad(out var refused, out string message), Is.False, primary);
                Assert.That(refused, Is.Null);
                Assert.That(message, Does.StartWith(damaged ? "Local save could not be loaded: " : "No local save exists in this slot.")
                    .And.EndWith(" A validated backup is available; choose Recover backup to restore it."), primary);

                Assert.That(slot.Store.TryRecoverBackup(out var recovered, out message), Is.True, primary + ": " + message);
                Assert.That(message, Is.EqualTo("Backup restored and validated. The backup remains available."
                    + (damaged ? " Your previous save file was kept alongside it." : "")
                    + loadMessage.Substring("Local episode loaded and validated.".Length)), primary + ": recovery says what loading the file says.");
                if (schema < current) Assert.That(message, Does.Contain("Schema " + schema + " history was migrated to schema " + current + " in memory."));
                Assert.That(recovered.schemaVersion, Is.EqualTo(current), primary);
                Assert.That(EpisodeValidation.TryValidate(recovered, out var error), Is.True, primary + ": " + error);
                Assert.That(JToken.DeepEquals(Payload(recovered), expected), Is.True,
                    primary + ": the recovered season is the save's own, through the frozen gates and the steps to 30.");
                Assert.That(CurrentOldSaveTrace.Checkpoint(recovered, current), Is.EqualTo(CurrentOldSaveTrace.Checkpoint(loaded, current)),
                    primary + ": the season loading the same file as the primary gives.");
                if (schema == 28)
                    Assert.That(PersistenceMigrationTests.StripSchema29(Payload(recovered)).ToString(Formatting.None),
                        Is.EqualTo(payload.ToString(Formatting.None)), primary + ": without schema 29's literals, the schema-28 save token for token as its build wrote it.");
                else if (schema == 29)
                    Assert.That(PersistenceMigrationTests.StripSchema30(Payload(recovered)).ToString(Formatting.None),
                        Is.EqualTo(payload.ToString(Formatting.None)), primary + ": without schema 30's literals, the schema-29 save token for token as its build wrote it.");
                else if (schema == current)
                    Assert.That(Payload(recovered).ToString(Formatting.None), Is.EqualTo(payload.ToString(Formatting.None)),
                        primary + ": a schema-30 save is this build's own, recovered token for token as it wrote it.");

                Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(slot.Original), primary + ": recovery never rewrites or rotates the backup.");
                Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(slot.Original),
                    primary + ": the primary is the backup's own bytes; the disk format changes only on the next save.");
                var kept = Directory.GetFiles(slot.Folder, "episode.json.before-recovery-*.json");
                Assert.That(kept, Has.Length.EqualTo(damaged ? 1 : 0), primary + ": a damaged primary is kept beside it; a missing one leaves nothing to keep.");
                if (damaged) Assert.That(File.ReadAllBytes(kept[0]), Is.EqualTo(torn), primary + ": the damaged primary is kept byte for byte.");
                Assert.That(Directory.GetFiles(slot.Folder).Select(Path.GetFileName),
                    Is.EquivalentTo(new[] { "episode.json", "episode.json.backup" }.Concat(kept.Select(Path.GetFileName))), primary + ": nothing else is left in the slot.");

                Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, primary + ": " + message);
                Assert.That(message, Is.EqualTo(loadMessage));
                Assert.That(CurrentOldSaveTrace.Checkpoint(again, current), Is.EqualTo(CurrentOldSaveTrace.Checkpoint(loaded, current)),
                    primary + ": the recovered primary loads as the old save does.");
            }
        }

        /// <summary>
        /// A save this build refuses (F4) is never recovered either. Kept as the backup, it is not offered, and recovery
        /// refuses with the save's own reason and changes nothing: no primary installed, a damaged one left as it was,
        /// nothing kept beside it, the backup as it was.
        /// </summary>
        [TestCaseSource(nameof(Refused))]
        public void AnOldSaveFromABranchsOwnSchemaNumberIsNeverRecoveredFromTheBackup(string id)
        {
            var entry = Entry(id);
            foreach (bool damaged in new[] { false, true })
            {
                string primary = id + (damaged ? ", a damaged primary" : ", no primary");
                using var slot = new Slot((string)entry["save"]);
                File.Move(slot.Store.SavePath, slot.Store.BackupPath);
                byte[] torn = Torn(slot.Original);
                if (damaged) File.WriteAllBytes(slot.Store.SavePath, torn);

                Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.False, primary);
                Assert.That(loaded, Is.Null);
                Assert.That(message, Does.Not.Contain("Recover backup"), primary + ": a backup this build refuses is not offered.");
                Assert.That(slot.Store.TryRecoverBackup(out var recovered, out message), Is.False, primary);
                Assert.That(recovered, Is.Null);
                Assert.That(message, Does.StartWith("Backup recovery unavailable: ").And.Contain((string)entry["refusal"]), primary);
                Assert.That(Directory.GetFiles(slot.Folder).Select(Path.GetFileName),
                    Is.EquivalentTo(damaged ? new[] { "episode.json", "episode.json.backup" } : new[] { "episode.json.backup" }),
                    primary + ": nothing installed, kept or left pending.");
                Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(slot.Original), primary);
                if (damaged) Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(torn), primary + ": the damaged primary is left as it was.");
            }
        }

        // ------------------------------------------------------------------ the career

        [TestCaseSource(nameof(Careers))]
        public void AnOldCareerFileLoadsAndAddsUp(string id)
        {
            var entry = Entry(id);
            var directory = Path.Combine(Path.GetTempPath(), "GamesimOldCareer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var original = File.ReadAllBytes(Path.Combine(Root, (string)entry["save"]));
                var ledger = new CareerLedger(directory);
                File.WriteAllBytes(ledger.FilePath, original);
                var record = ledger.Load();
                Assert.That(ledger.Notice, Is.Null, "A real career file is read, never set aside.");
                Assert.That(ledger.Blocked, Is.False);
                Assert.That(record.schemaVersion, Is.EqualTo((int)entry["schema"]));
                Assert.That(record.seasons, Has.Count.EqualTo((int)entry["seasons"]));
                Assert.That(record.seasons[0].placement, Is.EqualTo((int)entry["placement"]));
                Assert.That(record.seasons[0].outcome, Is.EqualTo((string)entry["outcome"]));
                var summary = CareerSummary.Of(record);
                Assert.That(summary.Seasons, Is.EqualTo(1));
                Assert.That(summary.Line(), Does.StartWith("1 season"));
                Assert.That(summary.Note(), Does.StartWith("Win rate "));
                Assert.That(File.ReadAllBytes(ledger.FilePath), Is.EqualTo(original), "Reading never rewrites the career.");

                // A finished old season goes on the record beside it, once.
                using var slot = new Slot((string)Entry("s16-director-finished-winner")["save"]);
                Assert.That(slot.Store.TryLoad(out var finished, out string message), Is.True, message);
                Assert.That(ledger.Record(finished), Is.True);
                Assert.That(ledger.Record(finished), Is.False, "The same season is recorded once.");
                var again = ledger.Load();
                Assert.That(ledger.Notice, Is.Null);
                Assert.That(again.seasons, Has.Count.EqualTo(2));
                Assert.That(again.seasons[0].sessionId, Is.EqualTo(record.seasons[0].sessionId), "The old entry is kept as it was.");
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }
        }

        // ------------------------------------------------------------------ helpers

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>A save's envelope as the store reads it: no date parsing, so every string stays a string.</summary>
        private static JObject Envelope(string path)
        {
            using var reader = new JsonTextReader(new StringReader(File.ReadAllText(path))) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }

        /// <summary>The store's own serializer. It is internal, so the test assembly reaches it by reflection.</summary>
        private static JsonSerializer Serializer() =>
            (JsonSerializer)typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true)
                .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        /// <summary>A state as the store writes it.</summary>
        private static JObject Payload(EpisodeState state) => JObject.FromObject(state, Serializer());

        /// <summary>A save cut off halfway, as an interrupted write leaves it.</summary>
        private static byte[] Torn(byte[] save) => save.Take(save.Length / 2).ToArray();

        /// <summary>A fixture's bytes in a slot of its own, deleted afterwards.</summary>
        private sealed class Slot : IDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "GamesimOldSave-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public readonly byte[] Original;
            public string Folder => directory;

            public Slot(string save)
            {
                Directory.CreateDirectory(directory);
                Original = File.ReadAllBytes(Path.Combine(Root, save));
                Store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
                File.WriteAllBytes(Store.SavePath, Original);
            }

            public void Dispose()
            {
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }
        }
    }
}
