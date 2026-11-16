using System;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house talk's line banks (plan 32 §K, <see cref="HouseAnswers"/>): every slot holds exactly three lines of at most
    /// fifteen words; a stated manner names its person and a neutral one names nobody; banks are keyed by voice (a trait),
    /// never by a person; and no slot an All-Star can fill carries a personal or romantic word. Later banks (the intel band at
    /// 13, the event words at 14) join these checks as they land. Pure simulation, so the Unity-free subset runs it.
    /// </summary>
    public sealed class LineBankLintTests
    {
        /// <summary>Words outside the game's register: romance and the personal. Matched as whole words, case-blind.</summary>
        private static readonly string[] OffRegister =
        {
            "love", "loved", "kiss", "kissed", "date", "dating", "babe", "baby", "honey", "sweetheart", "darling", "crush", "sexy", "hot",
            "cute", "boyfriend", "girlfriend", "husband", "wife", "married", "marry", "flirt", "flirting", "heart", "bed", "touch",
        };

        private static int Words(string line) => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

        [Test]
        public void EveryVoteSlotHoldsThreeShortLines()
        {
            int lines = 0;
            foreach (var voice in HouseAnswers.VoteBank)
            foreach (var slot in voice.Value)
            {
                Assert.That(slot.Value.Length, Is.EqualTo(HouseAnswers.LinesASlot), voice.Key + "/" + slot.Key);
                foreach (string line in slot.Value)
                {
                    lines++;
                    Assert.That(Words(line), Is.InRange(1, HouseAnswers.MostWords), voice.Key + "/" + slot.Key + ": " + line);
                    Assert.That(line.Replace(HouseAnswers.Name, "").IndexOfAny(new[] { '{', '}' }), Is.EqualTo(-1), "Only {NAME} is a slot: " + line);
                    Assert.That(Regex.IsMatch(line, @"\d"), Is.False, "House speech never says a number in digits: " + line);
                }
                Assert.That(slot.Value.Distinct().Count(), Is.EqualTo(HouseAnswers.LinesASlot), "Three different lines.");
            }
            Assert.That(lines, Is.EqualTo(75), "The vote bank: 5 voices × 5 manners × 3 (§K).");
        }

        [Test]
        public void EveryVoiceHasTheFourStatedMannersAndItsOneNeutralManner()
        {
            Assert.That(HouseAnswers.VoteBank.Keys, Is.EquivalentTo(new[] { Voice.Blunt, Voice.Warm, Voice.Chatty, Voice.Guarded, Voice.Plain }),
                "Desperate never answers the vote: the vote asks voters only.");
            foreach (var voice in HouseAnswers.VoteBank)
            {
                Assert.That(voice.Value.Keys, Is.EquivalentTo(new[] { Manner.Definite, Manner.Hedged, Manner.TurnedBack, Manner.LaidOnThick, Manner.NeutralFor(voice.Key) }), voice.Key);
                foreach (var slot in voice.Value)
                {
                    bool stated = slot.Key != Manner.NonAnswer && slot.Key != Manner.Deflect;
                    foreach (string line in slot.Value)
                        Assert.That(line.Contains(HouseAnswers.Name), Is.EqualTo(stated), voice.Key + "/" + slot.Key + ": a stated answer names its person, a neutral one nobody: " + line);
                }
            }
            Assert.That(HouseAnswers.VoteBank[Voice.Guarded][Manner.Deflect], Does.Contain("Ask me after."), "Today's deflection is kept.");
        }

        [Test]
        public void NoSlotAnAllStarCanFillIsPersonalOrRomantic()
        {
            var all = HouseAnswers.VoteBank.SelectMany(v => v.Value.SelectMany(slot => slot.Value))
                .Concat(new[] { HouseAnswers.ReadDoubt, HouseAnswers.ReadHolds, HouseAnswers.LastTimeTheyLied })
                .Concat(Manner.All.Select(HouseAnswers.MannerWords)).Concat(Voice.All.Select(HouseAnswers.VoiceWords));
            foreach (string line in all)
            foreach (string word in OffRegister)
                Assert.That(Regex.IsMatch(line, @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase), Is.False, "'" + word + "' in: " + line);
        }

        [Test]
        public void ALoggedLineIsFilledWithAFirstNameOrYouAndCapitalisedWhereItOpens()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 31u);
            var other = s.contestants.First(c => !c.isPlayer);
            string first = FinalistRead.FirstName(other.name);
            Assert.That(HouseAnswers.Fill(s, "{NAME}. Done deal.", other.id), Is.EqualTo(first + ". Done deal."));
            Assert.That(HouseAnswers.Fill(s, "{NAME}. Done deal.", s.playerId), Is.EqualTo("You. Done deal."));
            Assert.That(HouseAnswers.Fill(s, "Probably {NAME}? Don't hold me to it.", s.playerId), Is.EqualTo("Probably you? Don't hold me to it."));
            Assert.That(HouseAnswers.Fill(s, "Ask me after.", other.id), Is.EqualTo("Ask me after."));
        }

        [Test]
        public void TheNotebookHasWordsForEveryMannerAndVoice()
        {
            foreach (string manner in Manner.All) Assert.That(HouseAnswers.MannerWords(manner), Is.Not.Null.And.Not.Empty, manner);
            foreach (string voice in Voice.All) Assert.That(HouseAnswers.VoiceWords(voice), Is.Not.Null.And.Not.Empty, voice);
        }
    }
}
