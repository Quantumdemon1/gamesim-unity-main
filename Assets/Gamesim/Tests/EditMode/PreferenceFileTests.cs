using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Playtest-safe preferences (PLAN A, A13), the Unity-free half: an isolated root's own
    /// preferences file, the rule that decides whether a session reads it, PlayerPrefs or the
    /// defaults, and the player log's lines about the settings.
    /// </summary>
    public sealed class PreferenceFileTests
    {
        private string directory;

        [SetUp]
        public void MakeDirectory()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimPreferenceFileTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void RemoveDirectory()
        {
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void AFile_RoundTripsEveryValue()
        {
            var file = new PreferenceFile();
            Assert.That(file.SetInt("Gamesim.LargeText", 1), Is.True);
            Assert.That(file.SetInt("Gamesim.Volume", -35), Is.True);
            Assert.That(file.Set("Gamesim.Language", "English"), Is.True);
            Assert.That(file.SetInt("Gamesim.LargeText", 1), Is.False, "The same value again is no change.");
            string path = Path.Combine(directory, PreferenceFile.FileName);
            File.WriteAllText(path, file.ToJson());

            var read = PreferenceFile.Read(path, out var reason);
            Assert.That(reason, Is.Null);
            Assert.That(read.GetInt("Gamesim.LargeText", 0), Is.EqualTo(1));
            Assert.That(read.GetInt("Gamesim.Volume", 0), Is.EqualTo(-35));
            Assert.That(read.GetString("Gamesim.Language", null), Is.EqualTo("English"));
            Assert.That(read.GetInt("Gamesim.Language", 7), Is.EqualTo(7), "Not a number: the fallback.");
            Assert.That(read.GetInt("Gamesim.Absent", 9), Is.EqualTo(9));
            Assert.That(read.ToJson(), Is.EqualTo(file.ToJson()), "Equal maps are equal files.");
            Assert.That(read.Set("Gamesim.Volume", null), Is.True, "A null value removes the key.");
            Assert.That(read.HasKey("Gamesim.Volume"), Is.False);
        }

        [Test]
        public void AFile_EscapesWhatJsonMust()
        {
            var file = new PreferenceFile();
            const string awkward = "quote \" backslash \\ newline \n tab \t unicode \u00e9\u4e2d and a \u0001 control";
            file.Set("Gamesim.Odd \"key\"", awkward);
            var read = PreferenceFile.Parse(file.ToJson(), out var reason);
            Assert.That(reason, Is.Null);
            Assert.That(read.GetString("Gamesim.Odd \"key\"", null), Is.EqualTo(awkward));
            Assert.That(file.ToJson(), Does.Contain("\\\"").And.Contain("\\\\").And.Contain("\\n").And.Not.Contain("\n tab"));
        }

        [Test]
        public void AFile_WritesItsKeysInOrdinalOrder()
        {
            var file = new PreferenceFile();
            foreach (var key in new[] { "b", "Gamesim.Volume", "a", "Gamesim.LargeText", "B", "Gamesim.CompactHud" }) file.SetInt(key, 1);
            var keys = file.Values.Keys.ToList();
            Assert.That(keys, Is.EqualTo(new[] { "B", "Gamesim.CompactHud", "Gamesim.LargeText", "Gamesim.Volume", "a", "b" }), "Ordinal: capitals before small letters.");
            string json = file.ToJson();
            var at = keys.Select(key => json.IndexOf("\"" + key + "\"", StringComparison.Ordinal)).ToList();
            Assert.That(at, Is.Ordered, "and the file lists them so.");
            Assert.That(json, Does.StartWith("{\n  \"B\": \"1\",").And.EndWith("}\n"), "Indented by two, LF, one final line break.");
        }

        [Test]
        public void AMissingFile_IsNoPreferencesAndNoProblem()
        {
            var read = PreferenceFile.Read(Path.Combine(directory, "missing", PreferenceFile.FileName), out var reason);
            Assert.That(reason, Is.Null, "A first launch is not a fault.");
            Assert.That(read.Count, Is.Zero);
            Assert.That(read.GetInt("Gamesim.LargeText", 0), Is.Zero);
            Assert.That(PreferenceFile.Read(null, out reason).Count, Is.Zero);
        }

        [TestCase("", "empty")]
        [TestCase("   ", "empty")]
        [TestCase("{ \"Gamesim.LargeText\": ", "not valid JSON")]
        [TestCase("[\"Gamesim.LargeText\"]", "not a JSON object")]
        [TestCase("\"Gamesim.LargeText\"", "not a JSON object")]
        [TestCase("{ \"Gamesim.LargeText\": 1 }", "is not a string")]
        [TestCase("{ \"Gamesim.LargeText\": \"1\", \"Gamesim.Volume\": { \"nested\": \"35\" } }", "is not a string")]
        [TestCase("{ \"Gamesim.LargeText\": \"1\", \"Gamesim.LargeText\": \"0\" }", "not valid JSON")]
        [TestCase("{ \"Gamesim.LargeText\": \"1\" } { }", "not valid JSON")]
        [TestCase("{ \"\": \"1\" }", "no key")]
        public void ACorruptFile_IsTheDefaultsWithAReason_AndNeverThrows(string text, string why)
        {
            string path = Path.Combine(directory, PreferenceFile.FileName);
            File.WriteAllText(path, text);
            PreferenceFile read = null;
            string reason = null;
            Assert.DoesNotThrow(() => read = PreferenceFile.Read(path, out reason));
            Assert.That(read.Count, Is.Zero, "Nothing half-read: the defaults.");
            Assert.That(reason, Does.Contain(why));
            Assert.That(read.GetInt("Gamesim.LargeText", 0), Is.Zero);
        }

        [Test]
        public void AFileThatCannotBeRead_IsTheDefaultsWithAReason()
        {
            string path = Path.Combine(directory, PreferenceFile.FileName);
            File.WriteAllText(path, "{ \"Gamesim.LargeText\": \"1\" }");
            var read = PreferenceFile.Read(path, out var reason, _ => throw new IOException("held"));
            Assert.That(read.Count, Is.Zero);
            Assert.That(reason, Does.Contain("could not be read").And.Contain("IOException"));
            Assert.That(reason, Does.Not.Contain(directory), "The reason names the failure, not the path (a user name on Windows).");
        }

        [Test]
        public void AValueThatLooksLikeADate_StaysTheTextItWas()
        {
            var read = PreferenceFile.Parse("{ \"Gamesim.When\": \"2026-10-08T12:00:00Z\", \"Gamesim.Volume\": \"35\" }", out var reason);
            Assert.That(reason, Is.Null);
            Assert.That(read.GetString("Gamesim.When", null), Is.EqualTo("2026-10-08T12:00:00Z"));
        }

        // ---------------------------------------------------------------- the route

        [Test]
        public void TheRoute_IsPlayerPrefsTheRootsFileOrTheDefaults()
        {
            Assert.That(PreferenceRouting.For(null, false), Is.EqualTo(PreferenceRoute.PlayerPrefs), "An ordinary install.");
            Assert.That(PreferenceRouting.For(null, true), Is.EqualTo(PreferenceRoute.PlayerPrefs), "The opt-in means nothing without a root.");
            Assert.That(PreferenceRouting.For(@"C:\root", true), Is.EqualTo(PreferenceRoute.RootFile), "A root that asked keeps its own.");
            Assert.That(PreferenceRouting.For(@"C:\root", false), Is.EqualTo(PreferenceRoute.Defaults),
                "A root that did not ask - a test, the verifier - reads the defaults and writes nothing: never inferred from the root alone.");
        }

        [Test]
        public void TheLaunchRoute_IsDecidedAsTheDirectorWillDecideIt()
        {
            string root = Path.Combine(directory, "playtest");
            Assert.That(PreferenceRouting.ForLaunch(null, false, new[] { "game.exe" }, out var found), Is.EqualTo(PreferenceRoute.PlayerPrefs));
            Assert.That(found, Is.Null);
            Assert.That(PreferenceRouting.ForLaunch(null, false, new[] { "game.exe", "--gamesim-save-root", root }, out found), Is.EqualTo(PreferenceRoute.RootFile),
                "A playtest root from the command line keeps its own.");
            Assert.That(found, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(PreferenceRouting.ForLaunch(null, false, new[] { "game.exe", "--gamesim-verify", "--gamesim-save-root", root }, out found),
                Is.EqualTo(PreferenceRoute.Defaults), "The verifier never does.");
            Assert.That(PreferenceRouting.ForLaunch(null, false, new[] { "game.exe", "--gamesim-save-root", "relative" }, out _),
                Is.EqualTo(PreferenceRoute.Defaults), "A malformed root asked for isolation: not the machine's PlayerPrefs.");
            Assert.That(PreferenceRouting.ForLaunch(null, false, new[] { "game.exe", "--gamesim-save-root" }, out _), Is.EqualTo(PreferenceRoute.Defaults));
            Assert.That(PreferenceRouting.ForLaunch(root, true, new[] { "game.exe" }, out found), Is.EqualTo(PreferenceRoute.RootFile), "A root already decided decides.");
            Assert.That(found, Is.EqualTo(root));
            Assert.That(PreferenceRouting.ForLaunch(root, false, new[] { "game.exe", "--gamesim-save-root", root }, out _), Is.EqualTo(PreferenceRoute.Defaults),
                "A root set by a test or the verifier, without the opt-in, stays on the defaults whatever the command line says.");
            Assert.That(PreferenceRouting.BesideSavesForCommandLine(new[] { "game.exe", "--gamesim-save-root", root }), Is.True);
            Assert.That(PreferenceRouting.BesideSavesForCommandLine(new[] { "game.exe", "--gamesim-verify", "--gamesim-save-root", root }), Is.False);
        }

        // ---------------------------------------------------------------- the log's lines

        private static List<KeyValuePair<string, string>> Settings(params string[] pairs) =>
            pairs.Select(pair => pair.Split('=')).Select(parts => new KeyValuePair<string, string>(parts[0], parts[1])).ToList();

        [Test]
        public void TheSessionLine_NamesEverySettingAsOneWordEach()
        {
            string line = SettingsEvidence.SessionLine(Settings("text=standard", "motion=full", "pace=suspenseful", "volume=35", "device=keyboard")
                .Concat(new[] { new KeyValuePair<string, string>("language", "Pig Latin"), new KeyValuePair<string, string>("empty", "") }));
            Assert.That(line, Is.EqualTo("Gamesim settings: text=standard motion=full pace=suspenseful volume=35 device=keyboard language=Pig_Latin empty=-"));
            Assert.That(SettingsEvidence.Word("a=b c"), Is.EqualTo("a_b_c"), "A value never splits when the log is read back.");
        }

        [Test]
        public void TheChangeLines_AreOnePerValueThatDiffersFromTheLastLogged()
        {
            var start = Settings("text=standard", "pace=suspenseful", "volume=35");
            Assert.That(SettingsEvidence.ChangeLines(start, start), Is.Empty, "Nothing changed, nothing said.");
            Assert.That(SettingsEvidence.ChangeLines(start, Settings("text=larger", "pace=quick", "volume=35")),
                Is.EqualTo(new[] { "Gamesim setting changed: text=larger", "Gamesim setting changed: pace=quick" }));
            Assert.That(SettingsEvidence.ChangeLines(start, Settings("text=standard", "pace=suspenseful", "volume=35", "hud=compact")),
                Is.EqualTo(new[] { "Gamesim setting changed: hud=compact" }), "A setting not logged before is a change.");
            Assert.That(SettingsEvidence.ChangeLines(null, Settings("text=larger")), Is.EqualTo(new[] { "Gamesim setting changed: text=larger" }));
            Assert.That(SettingsEvidence.DeviceLine(true), Is.EqualTo("Gamesim input device: pad"));
            Assert.That(SettingsEvidence.DeviceLine(false), Is.EqualTo("Gamesim input device: keyboard"));
        }
    }
}
