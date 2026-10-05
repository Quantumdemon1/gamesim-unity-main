using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class InformationShareChoiceTests
    {
        private static IEnumerable<int> Sizes => Enumerable.Range(3, 14);
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static string Recipient(EpisodeState s) => s.Active.First(c => !c.isPlayer).id;
        private static EpisodeState Fresh(int size = 8)
        {
            var s = EconomyRulesTests.Fresh(size);
            s.memories.Clear();
            string subject = s.Active.Last(c => !c.isPlayer).id;
            for (int i = 0; i < 3; i++)
                s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = subject, text = "Memory " + i,
                    week = s.week, isPrivate = true });
            return s;
        }
        private static EpisodeCommand Share(EpisodeState s, string reference = null) => new EpisodeCommand
        {
            id = "share-" + s.revision, expectedRevision = s.revision, expectedPhase = s.phase,
            actorId = s.playerId, targetId = Recipient(s), kind = EpisodeCommandKind.ShareInformation, secondTargetId = reference
        };
        private static EpisodeState Accepted(EpisodeState s, string reference)
        {
            var result = new EpisodeEngine(s).Apply(Share(s, reference));
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }

        [TestCaseSource(nameof(Sizes))]
        public void EveryOfferedMemoryCanBeSelectedAndOnlyTheIntendedRecipientLearnsIt(int size)
        {
            var s = Fresh(size);
            var choice = InformationShareChoice.Open(s, Recipient(s));
            string before = Json(s);
            Assert.That(choice.Options.Select(o => o.Text), Is.EqualTo(s.memories.Select(m => m.text).Reverse()));
            Assert.That(choice.IsCurrent(s), Is.True);
            foreach (var option in choice.Options)
            {
                Assert.That(option.Reference.Length, Is.LessThanOrEqualTo(InformationShareChoice.ReferenceLimit));
                Assert.That(choice.CanChoose(s, option.Reference), Is.True);
                var after = Accepted(s, option.Reference);
                var receipt = after.memories.Last();
                Assert.That((receipt.ownerId, receipt.subjectId, receipt.text, receipt.week, receipt.isPrivate),
                    Is.EqualTo((Recipient(s), option.SubjectId, option.ReceivedText, s.week, true)));
                Assert.That(after.memories.Count, Is.EqualTo(s.memories.Count + 1));
                Assert.That(Json(after.memories.Take(s.memories.Count)), Is.EqualTo(Json(s.memories)));
                Assert.That(after.events.Last().audienceIds, Is.EquivalentTo(new[] { s.playerId, Recipient(s) }));
                Assert.That(after.events.Last().text, Does.Not.Contain(option.Text));
                Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1));
                Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
            }
            Assert.That(Json(s), Is.EqualTo(before), "Opening and resolving choices never change the supplied snapshot.");
            Assert.Throws<NotSupportedException>(() => ((IList<InformationShareChoice.Option>)choice.Options).Clear());
            Assert.That(InformationShareChoice.Open(s, Recipient(s)), Is.Not.SameAs(choice));
        }

        [Test]
        public void HiddenBallotsNpcMemoriesAndMemoriesAboutTheRecipientAreNeverOfferedOrResolved()
        {
            var s = Fresh();
            string recipient = Recipient(s), subject = s.memories[0].subjectId;
            var original = InformationShareChoice.Open(s, recipient);
            string oldToken = original.Options.Last().Reference;
            s.memories.Add(new MemoryState { ownerId = recipient, subjectId = subject, week = 1, text = "NPC private secret", isPrivate = true });
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = recipient, week = 1, text = "About the recipient", isPrivate = true });
            string hidden = s.Find(subject).name + " broke a Vote promise.";
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = subject, week = 1, text = hidden, isPrivate = true });
            Assert.That(KnownBallots.PlayerMemories(s).Any(m => m.text == hidden), Is.False);
            var choice = InformationShareChoice.Open(s, recipient);
            Assert.That(choice.Options.Select(o => o.Text), Is.EqualTo(new[] { "Memory 2", "Memory 1", "Memory 0" }));
            // Even a perfectly formatted, matching hash is not authority to share a forbidden
            // record. Mint these hostile inputs directly; production exposes no such operation.
            var mint = typeof(InformationShareChoice).GetMethod("ReferenceFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(mint, Is.Not.Null);
            for (int i = 3; i < s.memories.Count; i++)
            {
                string forged = (string)mint.Invoke(null, new object[] { s, recipient, i });
                Assert.That(InformationShareChoice.TryResolve(s, recipient, forged, out _), Is.False);
                var engine = new EpisodeEngine(s);
                Assert.That(engine.Apply(Share(s, forged)).accepted, Is.False);
                Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(s)));
            }
            // Turn a previously legitimate record into a private-ballot receipt without changing
            // its index/revision. An old view must not reveal it or silently share something else.
            s.memories[0].text = hidden;
            Assert.That(InformationShareChoice.TryResolve(s, recipient, oldToken, out _), Is.False);
            Assert.That(original.CanChoose(s, oldToken), Is.False);
        }

        [Test]
        public void ExactReferencesAreDistinctForIdenticalTextRecordsAndCultureIndependent()
        {
            var s = Fresh();
            foreach (var m in s.memories) m.text = "Same text: <b>literal</b> 😀";
            var options = InformationShareChoice.Open(s, Recipient(s)).Options;
            Assert.That(options.Select(o => o.Reference).Distinct().Count(), Is.EqualTo(3));
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                Assert.That(InformationShareChoice.Open(s, Recipient(s)).Options.Select(o => o.Reference),
                    Is.EqualTo(options.Select(o => o.Reference)));
            }
            finally { CultureInfo.CurrentCulture = previous; }
            Assert.That(InformationShareChoice.TryResolve(s, Recipient(s), options[0].Reference, out var detached), Is.True);
            detached.text = "caller mutation";
            Assert.That(s.memories.Last().text, Is.Not.EqualTo(detached.text));
        }

        [TestCase("session")][TestCase("revision")][TestCase("phase")][TestCase("text")]
        [TestCase("subject")][TestCase("owner")][TestCase("privacy")][TestCase("week")]
        [TestCase("index")][TestCase("recipient")][TestCase("gate")][TestCase("budget")]
        public void ChangedAuthorityOrSourceCannotReuseAReference(string change)
        {
            var s = Fresh();
            string recipient = Recipient(s);
            var choice = InformationShareChoice.Open(s, recipient);
            var option = choice.Options[0];
            var memory = s.memories.Last();
            switch (change)
            {
                case "session": s.sessionId += "-new"; break;
                case "revision": s.revision++; break;
                case "phase": s.phase = EpisodePhase.Campaign; break;
                case "text": memory.text += " changed"; break;
                case "subject": memory.subjectId = s.playerId; break;
                case "owner": memory.ownerId = recipient; break;
                case "privacy": memory.isPrivate = false; break;
                case "week": s.week = 2; memory.week = 2; break;
                case "index": s.memories.RemoveAt(0); break;
                case "recipient": recipient = s.Active.First(c => !c.isPlayer && c.id != recipient && c.id != memory.subjectId).id; break;
                case "gate": s.economyRulesVersion = 0; break;
                case "budget": s.windowActions[Windows.AfterEviction] = EpisodeEngine.SocialActionBudget(s); break;
            }
            Assert.That(InformationShareChoice.TryResolve(s, recipient, option.Reference, out _), Is.False, change);
            if (change != "recipient") Assert.That(choice.CanChoose(s, option.Reference), Is.False, change);
        }

        [TestCase("unknown")][TestCase(" ")][TestCase("memory-v1:")][TestCase("memory-v1:-1:x")]
        [TestCase("memory-v1:999:x")][TestCase("memory-v1:2147483648:x")]
        [TestCase("hash")][TestCase("long")][TestCase("leading-zero")][TestCase("uppercase")]
        public void ForgedReferencesRejectAtomicallyAndNeverFallBackToTheLatestMemory(string invalid)
        {
            var s = Fresh();
            string reference = InformationShareChoice.Open(s, Recipient(s)).Options[0].Reference;
            switch (invalid)
            {
                case "hash": reference = reference.Substring(0, reference.Length - 1) + (reference.EndsWith("0") ? "1" : "0"); break;
                case "long": reference = new string('x', InformationShareChoice.ReferenceLimit + 1); break;
                case "leading-zero": reference = reference.Insert(InformationShareChoice.ReferencePrefix.Length, "0"); break;
                case "uppercase": reference = reference.ToUpperInvariant(); break;
                default: reference = invalid; break;
            }
            var engine = new EpisodeEngine(s);
            var result = engine.Apply(Share(s, reference));
            Assert.That(result.accepted, Is.False);
            Assert.That(result.reason, Does.Contain("memory you can still share"));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(s)));
        }

        [TestCase(false)][TestCase(true)]
        public void LegacyAndDelayedRulesKeepIgnoringThePreviouslyUnusedPayload(bool delayed)
        {
            var s = Fresh();
            string reference = InformationShareChoice.Open(s, Recipient(s)).Options.Last().Reference;
            if (delayed) s.weekRulesStartWeek = s.week + 1; else s.economyRulesVersion = 0;
            Assert.That(InformationShareChoice.Open(s, Recipient(s)), Is.Null);
            string expected = Json(Accepted(s, null));
            foreach (string oldPayload in new[] { "forged", reference, " " , "" })
                Assert.That(Json(Accepted(s, oldPayload)), Is.EqualTo(expected));
        }

        [Test]
        public void SelectingTheLatestOrdinaryMemoryMatchesTheExistingGenericCommandExactly()
        {
            var s = Fresh();
            string reference = InformationShareChoice.Open(s, Recipient(s)).Options[0].Reference;
            Assert.That(Json(Accepted(s, reference)), Is.EqualTo(Json(Accepted(s, null))), "Same warmth, reciprocal RNG, receipts, events and cost.");
            var command = Share(s, reference); command.text = "FORGED CALLER CONTENT";
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(Json(result.state), Is.EqualTo(Json(Accepted(s, reference))), "Client text is not memory authority.");
        }

        [TestCase(1985)][TestCase(1986)][TestCase(2000)]
        public void LongValidMemoriesHaveAnExactBoundedPreviewAndRetainTheOriginal(int length)
        {
            var s = Fresh();
            s.memories.Last().text = new string('x', length);
            var option = InformationShareChoice.Open(s, Recipient(s)).Options[0];
            Assert.That(option.Shortened, Is.EqualTo(length + InformationShareChoice.ReceiptPrefix.Length > InformationShareChoice.MemoryLimit));
            Assert.That(option.ReceivedText.Length, Is.LessThanOrEqualTo(InformationShareChoice.MemoryLimit));
            var after = Accepted(s, option.Reference);
            Assert.That(after.memories.Last().text, Is.EqualTo(option.ReceivedText));
            Assert.That(after.memories[2].text, Is.EqualTo(s.memories[2].text));
        }

        [Test]
        public void ExcerptsDoNotSplitSurrogatePairsAndReferencesDistinguishMalformedCodeUnits()
        {
            var s = Fresh();
            int cut = InformationShareChoice.MemoryLimit - InformationShareChoice.ReceiptPrefix.Length - 1;
            s.memories.Last().text = new string('a', cut - 1) + "😀" + new string('b', 10);
            var option = InformationShareChoice.Open(s, Recipient(s)).Options[0];
            Assert.That(option.ReceivedText.EndsWith("a…"), Is.True);
            Accepted(s, option.Reference);
            s.memories.Last().text = "old \ud800 record";
            string first = InformationShareChoice.Open(s, Recipient(s)).Options[0].Reference;
            s.memories.Last().text = "old \udc00 record";
            Assert.That(InformationShareChoice.Open(s, Recipient(s)).Options[0].Reference, Is.Not.EqualTo(first));
            Assert.That(InformationShareChoice.TryResolve(s, Recipient(s), first, out _), Is.False);
        }

        [Test]
        public void AFullRecipientMemoryUsesTheExistingCapWithoutMutatingAnyPlayerRecord()
        {
            var s = Fresh(); string recipient = Recipient(s), subject = s.memories[0].subjectId;
            for (int i = 0; i < 30; i++) s.memories.Add(new MemoryState { ownerId = recipient, subjectId = subject, week = 1, text = "old " + i, isPrivate = true });
            var option = InformationShareChoice.Open(s, recipient).Options.Last();
            var after = Accepted(s, option.Reference);
            Assert.That(after.memories.Count(m => m.ownerId == recipient), Is.EqualTo(30));
            Assert.That(after.memories.Any(m => m.ownerId == recipient && m.text == "old 0"), Is.False);
            Assert.That(Json(after.memories.Where(m => m.ownerId == s.playerId)), Is.EqualTo(Json(s.memories.Where(m => m.ownerId == s.playerId))));
        }

        [Test]
        public void DuplicateDeliveryReloadAndBudgetRemainBounded()
        {
            var s = Fresh();
            var choice = InformationShareChoice.Open(s, Recipient(s));
            var command = Share(s, choice.Options[0].Reference);
            var engine = new EpisodeEngine(s);
            Assert.That(engine.Apply(command).accepted, Is.True);
            string committed = Json(engine.Snapshot);
            Assert.That(engine.Apply(command).duplicate, Is.True);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            var restored = JsonConvert.DeserializeObject<EpisodeState>(Json(s), new JsonSerializerSettings
                { ObjectCreationHandling = ObjectCreationHandling.Replace });
            Assert.That(Json(new EpisodeEngine(restored).Apply(command).state), Is.EqualTo(committed), "Pure serialized-state replay, not Unity persistence acceptance.");
            var next = engine.Snapshot;
            Assert.That(engine.Apply(Share(next, command.secondTargetId)).accepted, Is.False, "A new command ID/revision cannot recycle an old reference.");
            var newChoice = InformationShareChoice.Open(next, Recipient(next));
            Assert.That(engine.Apply(Share(next, newChoice.Options[0].Reference)).accepted, Is.True);
            Assert.That(InformationShareChoice.Open(engine.Snapshot, Recipient(s)), Is.Null, "Both move-in actions were spent.");
            Assert.That(engine.Apply(Share(engine.Snapshot, newChoice.Options[0].Reference)).accepted, Is.False);
        }

        [Test]
        public void EmptyAndIneligibleChoicesCannotGainAuthorityAndAllEligibleRecordsStayReachable()
        {
            Assert.That(InformationShareChoice.Open(null, null), Is.Null);
            var s = Fresh(); string recipient = Recipient(s);
            Assert.That(InformationShareChoice.Open(s, s.playerId), Is.Null);
            Assert.That(InformationShareChoice.Open(s, "missing"), Is.Null);
            s.memories.Clear();
            Assert.That(InformationShareChoice.Open(s, recipient).Options, Is.Empty);
            for (int i = 0; i < 30 * s.contestants.Count; i++) s.memories.Add(new MemoryState
                { ownerId = s.playerId, subjectId = s.playerId, week = 1, text = "Known " + i, isPrivate = true });
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            var choice = InformationShareChoice.Open(s, recipient);
            Assert.That(choice.Options.Count, Is.EqualTo(s.memories.Count), "The reader never silently drops valid older records.");
            foreach (var option in choice.Options) Assert.That(choice.CanChoose(s, option.Reference), Is.True);
            s.Find(recipient).status = ContestantStatus.Jury;
            Assert.That(choice.IsCurrent(s), Is.False);
            s.Find(recipient).status = ContestantStatus.Active; s.Find(s.playerId).status = ContestantStatus.Jury;
            Assert.That(choice.IsCurrent(s), Is.False);
        }

        [Test]
        public void OmittingATargetCannotBypassTheFreshSeasonKnowledgeBoundary()
        {
            var s = Fresh(); string subject = s.memories[0].subjectId;
            string hidden = s.Find(subject).name + " broke a Vote promise.";
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = subject, week = 1, text = hidden, isPrivate = true });
            var after = Accepted(s, null);
            Assert.That(after.memories.Last().text, Is.EqualTo(InformationShareChoice.ReceiptPrefix + "Memory 2"));
            s.memories.RemoveRange(0, 3);
            var engine = new EpisodeEngine(s);
            Assert.That(engine.Apply(Share(s)).accepted, Is.False);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(s)));
            s.economyRulesVersion = 0;
            Assert.That(Accepted(s, null).memories.Last().text, Is.EqualTo(InformationShareChoice.ReceiptPrefix + hidden),
                "Old saves keep their original stored-memory policy; no old outcome/history is rewritten.");
        }

        [TestCase(EpisodePhase.Nomination)]
        [TestCase(EpisodePhase.VetoSelection)]
        [TestCase(EpisodePhase.Veto)]
        [TestCase(EpisodePhase.VetoMeeting)]
        [TestCase(EpisodePhase.Campaign)]
        public void ExplicitSharingUsesTheCurrentDecisionWindowsOwnBudget(EpisodePhase phase)
        {
            var s = Fresh();
            s.phase = phase; s.hohId = s.playerId;
            if (phase != EpisodePhase.Nomination)
                s.nominees = s.Active.Where(c => !c.isPlayer).Skip(1).Take(2).Select(c => c.id).ToList();
            if (phase == EpisodePhase.Veto || phase == EpisodePhase.VetoMeeting || phase == EpisodePhase.Campaign)
            {
                s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).Select(c => c.id).ToList();
                s.vetoHolderId = s.playerId;
            }
            s.vetoResolved = phase == EpisodePhase.Campaign;
            var choice = InformationShareChoice.Open(s, Recipient(s));
            Assert.That(choice, Is.Not.Null);
            var after = Accepted(s, choice.Options.Last().Reference);
            Assert.That(after.phase, Is.EqualTo(phase));
            Assert.That(after.windowActions[EpisodeEngine.Window(s)], Is.EqualTo(1));
            Assert.That(after.memories.Last().text, Is.EqualTo(choice.Options.Last().ReceivedText));
        }
    }
}
