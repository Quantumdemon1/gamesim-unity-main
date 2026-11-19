using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 31 (the free "Just talk" feature's S0, FIELDS.md): the talk ladder, the fresh season's level, and the bounds
    /// validation holds every new saved field to. Nothing here plays a talk: no command, no draw, no gain exists yet.
    /// Simulation only, so the Unity-free subset runs it too.
    /// </summary>
    public sealed class TalkRulesSchema31Tests
    {
        // ------------------------------------------------------------------ the ladder

        [Test]
        public void TheLadderIsBoundedAtComesToTalkAndShipsOff()
        {
            Assert.That(TalkRules.Latest, Is.EqualTo(TalkRules.ComesToTalk));
            Assert.That(new[] { TalkRules.Off, TalkRules.Talk, TalkRules.Chemistry, TalkRules.Romance, TalkRules.Couples, TalkRules.ComesToTalk },
                Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(TalkRules.Shipped, Is.EqualTo(TalkRules.Off), "No level is enrolled before its balance measurement (ruling R4).");
            Assert.That(TalkRules.Schema, Is.EqualTo(31));
        }

        [TestCase(-1, false)] [TestCase(0, true)] [TestCase(1, true)] [TestCase(5, true)] [TestCase(6, false)]
        public void IsDefinedIsTheClosedSetZeroToLatest(int level, bool defined) =>
            Assert.That(TalkRules.IsDefined(level), Is.EqualTo(defined));

        [TestCase(0, 30, true)] [TestCase(0, 31, true)] [TestCase(1, 30, false)] [TestCase(1, 31, true)]
        [TestCase(5, 30, false)] [TestCase(5, 31, true)] [TestCase(6, 31, false)]
        public void ALevelIsAdmittedOnlyFromTheSchemaThatIntroducedIt(int level, int schema, bool admitted) =>
            Assert.That(TalkRules.Admits(level, schema), Is.EqualTo(admitted));

        [Test]
        public void TheLiveSchemaIs31()
        {
            Assert.That(EpisodeState.LiveSchema, Is.EqualTo(31));
            Assert.That(new EpisodeState().schemaVersion, Is.EqualTo(31));
        }

        [Test]
        public void ApplyFreshWritesTheShippedLevelAndFieldsListsIt()
        {
            var s = Fresh();
            s.story.talkRules = TalkRules.Romance; // A value ApplyFresh must overwrite, so the write itself is proved.
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.talkRules, Is.EqualTo(TalkRules.Shipped));
            var fields = ShippedRules.Fields(s);
            // Second to last since the minds' ladder (story.mindRules) joined the list after it in the same schema.
            Assert.That(fields[fields.Count - 2].Key, Is.EqualTo("story.talkRules"));
            Assert.That(fields[fields.Count - 2].Value, Is.EqualTo(TalkRules.Off));
        }

        [Test]
        public void AFreshSeasonHoldsEveryTalkFieldAtItsDefault()
        {
            var s = Fresh();
            ShippedRules.ApplyFresh(s);
            Assert.That(s.story.talkRules, Is.Zero);
            Assert.That(s.story.talkLean, Is.Zero);
            Assert.That(s.story.talkPairs, Is.Empty);
            var c = new ContactState { npcId = Npc(s) };
            Assert.That((c.talkWindow, c.talks, c.talkTenths, c.agreement, c.standingWeek, c.stageReached, c.cool, c.spark),
                Is.EqualTo((0, 0, 0, 0, 0, 0, 0, 0)));
            Assert.That((c.romanceWeek, c.romanceClosedUntil, c.flirts, c.notYets, c.turnAsides, c.coupleAnchors, c.walkUpWindow, c.notNows, c.walkUpsOffUntil),
                Is.EqualTo((0, 0, 0, 0, 0, 0, 0, 0, 0)));
            Assert.That(new[] { c.talkPending, c.lastTone, c.priorTone, c.standing, c.romance }, Is.All.EqualTo(""));
            Assert.That(c.spentTalks, Is.Empty);
            Assert.That(c.talkFlags, Is.Empty);
            s.story.contacts.Add(c);
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
        }

        /// <summary>Nothing in this build writes a talk field: a season played on keeps every one at its default.</summary>
        [Test]
        public void PlayingASeasonWritesNoTalkField()
        {
            var s = Fresh();
            ShippedRules.ApplyFresh(s);
            var engine = new EpisodeEngine(s);
            for (int i = 0; i < 4000 && engine.Snapshot.week < 3 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var played = engine.Snapshot;
            Assert.That(played.week, Is.GreaterThanOrEqualTo(2), "The walk reached a later week.");
            Assert.That(played.story.talkRules, Is.Zero);
            Assert.That(played.story.talkLean, Is.Zero);
            Assert.That(played.story.talkPairs, Is.Empty);
            foreach (var c in played.story.contacts)
            {
                Assert.That(c.talkWindow + c.talks + c.talkTenths + c.agreement + c.standingWeek + c.stageReached + c.cool + c.spark
                    + c.romanceWeek + c.romanceClosedUntil + c.flirts + c.notYets + c.turnAsides + c.coupleAnchors + c.walkUpWindow
                    + c.notNows + c.walkUpsOffUntil, Is.Zero, c.npcId);
                Assert.That(c.talkPending + c.lastTone + c.priorTone + c.standing + c.romance, Is.Empty, c.npcId);
                Assert.That(c.spentTalks.Count + c.talkFlags.Count, Is.Zero, c.npcId);
            }
        }

        [Test]
        public void ClonesCopyTheTalkListsDeeply()
        {
            var c = new ContactState { npcId = "a" };
            c.spentTalks.Add("t1"); c.talkFlags.Add(TalkRules.Pushy);
            var copy = c.Clone();
            copy.spentTalks.Add("t2"); copy.talkFlags.Clear();
            Assert.That(c.spentTalks, Is.EqualTo(new[] { "t1" }));
            Assert.That(c.talkFlags, Is.EqualTo(new[] { TalkRules.Pushy }));
            var w = new StoryWorldState();
            w.talkPairs.Add(new TalkPairState { aId = "a", bId = "b", spark = 5 });
            var wc = w.Clone();
            wc.talkPairs[0].spark = 9; wc.talkPairs.Add(new TalkPairState());
            Assert.That(w.talkPairs.Count, Is.EqualTo(1));
            Assert.That(w.talkPairs[0].spark, Is.EqualTo(5));
        }

        // ------------------------------------------------------------------ validation: the story

        [TestCase(6)] [TestCase(-1)]
        public void AnUndefinedTalkLevelIsRefused(int level)
        {
            var s = Valid();
            s.story.talkRules = level;
            Refused(s, "Unsupported talk rules version.");
        }

        [TestCase(1)] [TestCase(5)]
        public void ADefinedTalkLevelIsAcceptedAtThirtyOne(int level)
        {
            var s = Valid();
            s.story.talkRules = level;
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
        }

        [Test]
        public void AtTalkRulesZeroEveryTalkFieldMustBeAtItsDefault()
        {
            foreach (Action<EpisodeState> edit in new Action<EpisodeState>[]
            {
                s => s.story.talkLean = 1,
                s => s.story.talkPairs.Add(Pair(s)),
                s => s.story.contacts[0].talks = 1,
                s => s.story.contacts[0].spentTalks.Add("t1"),
                s => s.story.contacts[0].romance = TalkRules.Friends,
                s => s.story.contacts[0].talkFlags.Add(TalkRules.Warned),
            })
            {
                var s = Valid();
                edit(s);
                Refused(s, "A season without free talk has none of its records.");
                s.story.talkRules = TalkRules.Latest;
                Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, "Within its bound at a level: " + reason);
            }
        }

        [TestCase(-7)] [TestCase(7)]
        public void TheLeanIsBounded(int lean)
        {
            var s = Valid(TalkRules.Chemistry);
            s.story.talkLean = lean;
            Refused(s, "Invalid talk pair data.");
            s.story.talkLean = Math.Sign(lean) * TalkRules.MostLean;
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
        }

        [TestCase("order")] [TestCase("player")] [TestCase("unknown")] [TestCase("duplicate")] [TestCase("spark")]
        [TestCase("ended-unformed")] [TestCase("ended-before-formed")] [TestCase("formed-future")] [TestCase("apart")] [TestCase("too-many")]
        public void PairsAreBounded(string defect)
        {
            var s = Valid(TalkRules.Couples);
            var pair = Pair(s);
            s.story.talkPairs.Add(pair);
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, "The control: " + reason);
            switch (defect)
            {
                case "order": (pair.aId, pair.bId) = (pair.bId, pair.aId); break;
                case "player": pair.bId = s.playerId; if (string.CompareOrdinal(pair.aId, pair.bId) > 0) (pair.aId, pair.bId) = (pair.bId, pair.aId); break;
                case "unknown": pair.bId = "zzzz-nobody"; break;
                case "duplicate": s.story.talkPairs.Add(pair.Clone()); break;
                case "spark": pair.spark = 101; break;
                case "ended-unformed": pair.endedWeek = 1; break;
                case "ended-before-formed": pair.formedWeek = s.week; pair.endedWeek = s.week - 1; break;
                case "formed-future": pair.formedWeek = s.week + 1; break;
                case "apart": pair.apartAnchors = TalkRules.MostCount + 1; break;
                default: while (s.story.talkPairs.Count <= TalkRules.MostPairs) s.story.talkPairs.Add(pair.Clone()); break;
            }
            Refused(s, "Invalid talk pair data.");
        }

        // ------------------------------------------------------------------ validation: each contact

        private static IEnumerable<TestCaseData> ContactDefects()
        {
            yield return Case("talkWindow-negative", c => c.talkWindow = -1);
            yield return Case("talkWindow-past-the-season", c => c.talkWindow = TalkRules.LastWindowKey(Week) + 1);
            yield return Case("talks", c => c.talks = TalkRules.MostCount + 1);
            yield return Case("tenths", c => c.talkTenths = 10);
            yield return Case("spent-null", c => c.spentTalks = null);
            yield return Case("spent-empty-id", c => c.spentTalks.Add(""));
            yield return Case("spent-long-id", c => c.spentTalks.Add(new string('t', TalkRules.MostIdLength + 1)));
            yield return Case("spent-duplicate", c => { c.spentTalks.Add("t1"); c.spentTalks.Add("t1"); });
            yield return Case("spent-too-many", c => { for (int i = 0; i <= TalkRules.MostSpentTalks; i++) c.spentTalks.Add("t" + i); });
            yield return Case("pending-null", c => c.talkPending = null);
            yield return Case("pending-long", c => c.talkPending = new string('p', TalkRules.MostIdLength + 1));
            yield return Case("agreement-high", c => c.agreement = TalkRules.MostAgreement + 1);
            yield return Case("agreement-low", c => c.agreement = -TalkRules.MostAgreement - 1);
            yield return Case("tone-unknown", c => c.lastTone = "shout");
            yield return Case("prior-without-last", c => c.priorTone = TalkRules.Warm);
            yield return Case("standing-future", c => { c.standingWeek = Week + 1; c.standing = "s"; });
            yield return Case("standing-undated", c => c.standing = "s");
            yield return Case("standing-empty", c => c.standingWeek = 1);
            yield return Case("standing-long", c => { c.standingWeek = 1; c.standing = new string('s', TalkRules.MostStandingLength + 1); });
            yield return Case("stage", c => c.stageReached = 6);
            yield return Case("cool", c => c.cool = 3);
            yield return Case("spark", c => c.spark = 101);
            yield return Case("romance-unknown", c => c.romance = "married");
            yield return Case("romance-week", c => c.romanceWeek = Week + 1);
            yield return Case("romance-closed", c => c.romanceClosedUntil = TalkRules.ForTheSeason + 1);
            yield return Case("flirts", c => c.flirts = -1);
            yield return Case("not-yets", c => c.notYets = 4);
            yield return Case("turn-asides", c => c.turnAsides = 4);
            yield return Case("couple-anchors", c => c.coupleAnchors = TalkRules.MostCount + 1);
            yield return Case("walk-up-window", c => c.walkUpWindow = TalkRules.LastWindowKey(Week) + 1);
            yield return Case("not-nows", c => c.notNows = 4);
            yield return Case("walk-ups-off", c => c.walkUpsOffUntil = -1);
            yield return Case("flag-unknown", c => c.talkFlags.Add("married"));
            yield return Case("flag-duplicate", c => { c.talkFlags.Add(TalkRules.Pushy); c.talkFlags.Add(TalkRules.Pushy); });
            yield return Case("flags-null", c => c.talkFlags = null);
        }

        private static TestCaseData Case(string name, Action<ContactState> edit) => new TestCaseData(name, edit).SetName("ContactFieldOutOfBoundIsRefused(" + name + ")");

        [TestCaseSource(nameof(ContactDefects))]
        public void ContactFieldOutOfBoundIsRefused(string name, Action<ContactState> edit)
        {
            var s = Valid(TalkRules.Latest);
            Assert.That(s.week, Is.EqualTo(Week));
            edit(s.story.contacts[0]);
            Refused(s, "Invalid contact data.");
        }

        [Test]
        public void EveryContactFieldAtItsBoundIsAccepted()
        {
            var s = Valid(TalkRules.Latest);
            var c = s.story.contacts[0];
            c.talkWindow = TalkRules.LastWindowKey(s.week); c.talks = TalkRules.MostCount; c.talkTenths = 9;
            for (int i = 0; i < TalkRules.MostSpentTalks; i++) c.spentTalks.Add("talk-" + i);
            c.talkPending = new string('p', TalkRules.MostIdLength); c.agreement = -TalkRules.MostAgreement;
            c.lastTone = TalkRules.Flirt; c.priorTone = TalkRules.CallOut;
            c.standingWeek = s.week; c.standing = "honest|close|worry|2";
            c.stageReached = 5; c.cool = 2; c.spark = 100; c.romance = TalkRules.Friends; c.romanceWeek = s.week;
            c.romanceClosedUntil = TalkRules.ForTheSeason; c.flirts = TalkRules.MostCount; c.notYets = 3; c.turnAsides = 3;
            c.coupleAnchors = TalkRules.MostCount; c.walkUpWindow = TalkRules.LastWindowKey(s.week); c.notNows = 3;
            c.walkUpsOffUntil = TalkRules.ForTheSeason;
            c.talkFlags.AddRange(TalkRules.Flags);
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
        }

        [Test]
        public void TheWindowKeyKeepsMoveInNightApartFromWeekOnesLastWindow()
        {
            // Ruling R5: move-in night is key 1; week one's windows are 10 to 13, so no two windows share a key.
            Assert.That(TalkRules.LastWindowKey(1), Is.EqualTo(13));
            Assert.That(TalkRules.LastWindowKey(0), Is.EqualTo(5));
        }

        // ------------------------------------------------------------------ helpers

        private const int Week = 3;

        private static EpisodeState Fresh() => SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3101u);

        /// <summary>A shipped season walked to week three with one contact at its defaults, at the given talk level.</summary>
        private static EpisodeState Valid(int level = TalkRules.Off)
        {
            if (Walked == null)
            {
                var s = Fresh();
                ShippedRules.ApplyFresh(s);
                var engine = new EpisodeEngine(s);
                for (int i = 0; i < 4000 && engine.Snapshot.week < Week; i++)
                    Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
                Walked = engine.Snapshot;
                Assert.That(Walked.week, Is.EqualTo(Week));
            }
            var copy = Walked.Clone();
            if (copy.story.contacts.Count == 0) copy.story.contacts.Add(new ContactState { npcId = Npc(copy) });
            copy.story.talkRules = level;
            Assert.That(EpisodeValidation.TryValidate(copy, out string reason), Is.True, "The control: " + reason);
            return copy;
        }

        private static EpisodeState Walked;

        private static string Npc(EpisodeState s) => s.contestants.First(c => !c.isPlayer).id;

        private static TalkPairState Pair(EpisodeState s)
        {
            var ids = s.contestants.Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).Take(2).ToArray();
            return new TalkPairState { aId = ids[0], bId = ids[1], spark = 10 };
        }

        private static void Refused(EpisodeState s, string reason)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.EqualTo(reason));
        }
    }
}
