using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The main menu's season card (change 10): the look at the top of a save (<see cref="SaveHeader"/>)
    /// and the card's words (<see cref="SeasonCardWords"/>). The files here are written in the save's
    /// shape by the test; a save the store itself wrote is MainMenuSeasonCardSaveTests (EditMode only).
    /// In the Unity-free suite too. Every case works in a folder of its own under the temporary folder.
    /// </summary>
    public sealed class SaveHeaderTests
    {
        private const string Prefix = "GamesimSaveHeader-";
        private static readonly DateTime Now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        private string folder;

        [SetUp]
        public void MakeAFolderOfItsOwn()
        {
            folder = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void RemoveOnlyThatFolder()
        {
            if (string.IsNullOrEmpty(folder)) return;
            var resolved = Path.GetFullPath(folder);
            Assert.That(Path.GetFileName(resolved), Does.StartWith(Prefix));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        /// <summary>The envelope as the save store writes it, around a season of <paramref name="week"/> with the statuses given.</summary>
        private static string Envelope(int week, string savedAt, params int[] statuses)
        {
            var cast = string.Join(",", statuses.Select((status, i) =>
                "{\"id\":\"c" + i + "\",\"name\":\"Guest " + i + "\",\"status\":" + status + ",\"stats\":{\"social\":5}}"));
            return "{\n  \"format\": \"gamesim-unity-save\",\n  \"version\": 1,\n  \"savedAt\": \"" + savedAt + "\",\n"
                + "  \"checksum\": \"0000\",\n  \"state\": {\n    \"schemaVersion\": 31,\n    \"seed\": 7,\n    \"week\": " + week + ",\n"
                + "    \"phase\": 2,\n    \"playerId\": \"c0\",\n    \"contestants\": [" + cast + "],\n    \"relationships\": []\n  }\n}";
        }

        private string Write(string text, string name = "episode.json")
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        [Test]
        public void Peek_ReadsTheWeek_TheHouseguestsLeft_AndWhenItWasSaved()
        {
            string path = Write(Envelope(4, "2026-11-22T10:00:00.0000000Z", 0, 0, 1, 0, 0, 2, 0, 0, 0));
            var header = SaveHeader.Peek(path);
            Assert.That(header.HasFacts, Is.True);
            Assert.That(header.Week, Is.EqualTo(4));
            Assert.That(header.Left, Is.EqualTo(7), "Only the houseguests still in the house (status Active).");
            Assert.That(header.SavedUtc, Is.EqualTo(new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc)));
            Assert.That(header.Held, Is.False);
            Assert.That(SeasonCardWords.Facts(header, Now), Is.EqualTo("Week 4 · 7 houseguests left · saved 2 hours ago"));
        }

        [Test]
        public void Peek_ReadsTheHeaderOnly_WhatFollowsTheHouseguestsIsNeverRead()
        {
            // A load refuses this file (its checksum, and a tail that is not JSON at all); the look
            // stops once it has the week and the houseguests, so it still has them.
            string whole = Envelope(6, "2026-11-22T11:00:00Z", 0, 0, 0, 1);
            string cut = whole.Substring(0, whole.IndexOf("\"relationships\"", StringComparison.Ordinal)) + "\"relationships\": [ {{{ not json";
            var header = SaveHeader.Peek(Write(cut));
            Assert.That(header.HasFacts, Is.True);
            Assert.That(header.Week, Is.EqualTo(6));
            Assert.That(header.Left, Is.EqualTo(3));
        }

        [Test]
        public void Peek_AHeldSave_GivesThePlainCard_AndNeverThrows()
        {
            string path = Write(Envelope(3, "2026-11-22T11:00:00Z", 0, 0, 0));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SaveHeader held = null;
                Assert.DoesNotThrow(() => held = SaveHeader.Peek(path));
                Assert.That(held.HasFacts, Is.False, "Held by another program, its facts are not read,");
                Assert.That(held.Held, Is.True, "and the look knows it was a hold.");
                Assert.That(SeasonCardWords.Facts(held, Now), Is.Null, "The card shows its heading alone.");
            }
            Assert.That(SaveHeader.Peek(path).Week, Is.EqualTo(3), "Let go, the facts are read again.");
        }

        [Test]
        public void Peek_DoesNotChangeTheFile()
        {
            string path = Write(Envelope(2, "2026-11-22T11:00:00Z", 0, 0));
            var bytes = File.ReadAllBytes(path);
            var written = File.GetLastWriteTimeUtc(path);
            Assert.That(SaveHeader.Peek(path).HasFacts, Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(written));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a save at all")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"format\":\"something-else\",\"version\":1,\"state\":{\"week\":4,\"contestants\":[]}}")]
        [TestCase("{\"format\":\"gamesim-unity-save\",\"version\":2,\"state\":{\"week\":4,\"contestants\":[]}}")]
        [TestCase("{\"state\":{\"week\":4,\"contestants\":[]},\"format\":\"gamesim-unity-save\",\"version\":1}")]
        [TestCase("{\"format\":\"gamesim-unity-save\",\"version\":1,\"state\":{\"week\":\"four\",\"contestants\":[]}}")]
        [TestCase("{\"format\":\"gamesim-unity-save\",\"version\":1,\"state\":{\"week\":0,\"contestants\":[]}}")]
        [TestCase("{\"format\":\"gamesim-unity-save\",\"version\":1,\"state\":{\"week\":4,\"contestants\":7}}")]
        [TestCase("{\"format\":\"gamesim-unity-save\",\"version\":1,\"state\":{\"week\":4}}")]
        [TestCase("{\"format\":\"gamesim-unity-save\",\"version\":1,\"state\":{\"week\":4,\"contestants\":[{\"status\":0},")]
        public void Peek_AnythingButASave_GivesThePlainCard_AndNeverThrows(string text)
        {
            string path = text == null ? Path.Combine(folder, "missing.json") : Write(text);
            SaveHeader header = null;
            Assert.DoesNotThrow(() => header = SaveHeader.Peek(path));
            Assert.That(header.HasFacts, Is.False);
            Assert.That(header.Held, Is.False, "Missing or unreadable is not a hold.");
            Assert.That(SeasonCardWords.Facts(header, Now), Is.Null);
        }

        [Test]
        public void Peek_NoPathAndAFolder_GiveThePlainCard()
        {
            Assert.That(SaveHeader.Peek(null).HasFacts, Is.False);
            Assert.That(SaveHeader.Peek("  ").HasFacts, Is.False);
            Assert.That(SaveHeader.Peek(folder).HasFacts, Is.False, "A folder where the save should be.");
        }

        [Test]
        public void Peek_ASaveOverTheStoresLimit_IsNotRead()
        {
            string whole = Envelope(5, "2026-11-22T11:00:00Z", 0, 0);
            string path = Write(whole.Substring(0, whole.Length - 1) + new string(' ', (int)SaveHeader.MaximumBytes) + "}");
            Assert.That(SaveHeader.Peek(path).HasFacts, Is.False);
        }

        [Test]
        public void Peek_ContestantsWithNestedValues_AndAByteOrderMark_StillRead()
        {
            var envelope = new JObject
            {
                ["format"] = SaveHeader.Format,
                ["version"] = 1,
                ["savedAt"] = "2026-11-22T11:58:30.0000000Z",
                ["checksum"] = "0",
                ["state"] = new JObject
                {
                    ["schemaVersion"] = 31,
                    ["week"] = 5,
                    ["contestants"] = new JArray(
                        new JObject { ["id"] = "a", ["status"] = (int)ContestantStatus.Active, ["traits"] = new JArray(1, 2), ["card"] = new JObject { ["status"] = 1 } },
                        new JObject { ["id"] = "b", ["status"] = (int)ContestantStatus.Evicted },
                        new JObject { ["id"] = "c", ["status"] = (int)ContestantStatus.Active })
                }
            };
            string path = Path.Combine(folder, "bom.json");
            File.WriteAllText(path, envelope.ToString(Formatting.Indented), new UTF8Encoding(true));
            var header = SaveHeader.Peek(path);
            Assert.That(header.Week, Is.EqualTo(5));
            Assert.That(header.Left, Is.EqualTo(2), "A status inside a nested object is not the houseguest's.");
            Assert.That(SeasonCardWords.Facts(header, Now), Is.EqualTo("Week 5 · 2 houseguests left · saved 1 minute ago"));
        }

        [Test]
        public void Peek_CountsAStatusWrittenAsAWord()
        {
            string text = "{\"format\":\"gamesim-unity-save\",\"version\":1,\"state\":{\"week\":9,\"contestants\":"
                + "[{\"status\":\"Active\"},{\"status\":\"Evicted\"},{\"status\":\"Active\"}]}}";
            var header = SaveHeader.Peek(Write(text));
            Assert.That(header.Left, Is.EqualTo(2));
            Assert.That(header.SavedUtc, Is.Null);
            Assert.That(SeasonCardWords.Facts(header, Now), Is.EqualTo("Week 9 · 2 houseguests left"), "No time, no time part.");
        }

        [Test]
        public void Facts_OneLeftAndNoneLeft_ReadAsWords()
        {
            string one = Envelope(8, "2026-11-22T12:00:00Z", 0, 1, 2);
            Assert.That(SeasonCardWords.Facts(SaveHeader.Peek(Write(one, "one.json")), Now),
                Is.EqualTo("Week 8 · 1 houseguest left · saved just now"));
            string none = Envelope(12, "2026-11-22T12:00:00Z", 3, 4, 2);
            Assert.That(SeasonCardWords.Facts(SaveHeader.Peek(Write(none, "none.json")), Now),
                Is.EqualTo("Week 12 · season finished · saved just now"));
            Assert.That(SeasonCardWords.Facts(null, Now), Is.Null);
            Assert.That(SeasonCardWords.Facts(SaveHeader.None(), Now), Is.Null);
        }

        [TestCase(0, "saved just now")]
        [TestCase(-3600, "saved just now")]
        [TestCase(59, "saved just now")]
        [TestCase(60, "saved 1 minute ago")]
        [TestCase(59 * 60, "saved 59 minutes ago")]
        [TestCase(3600, "saved 1 hour ago")]
        [TestCase(2 * 3600, "saved 2 hours ago")]
        [TestCase(23 * 3600 + 3599, "saved 23 hours ago")]
        [TestCase(24 * 3600, "saved yesterday")]
        [TestCase(47 * 3600, "saved yesterday")]
        [TestCase(48 * 3600, "saved 2 days ago")]
        [TestCase(29 * 86400, "saved 29 days ago")]
        public void Saved_ReadsAsPlainWords(int secondsAgo, string expected)
        {
            Assert.That(SeasonCardWords.Saved(Now.AddSeconds(-secondsAgo), Now), Is.EqualTo(expected));
        }

        [Test]
        public void Saved_LongAgo_GivesTheDate()
        {
            string words = SeasonCardWords.Saved(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc), Now);
            Assert.That(words, Does.StartWith("saved on June 1"));
            Assert.That(words, Does.EndWith(", 2026"));
        }
    }
}
