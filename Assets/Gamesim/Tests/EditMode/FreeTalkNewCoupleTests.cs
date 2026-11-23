using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3's second fixes (talk rules 3), Unity-free: once the player's partner has left the house (evicted by the
    /// house in a walked season: their bond apart, the couple ended at the anchor), a new couple can form, by accepting a
    /// declaration or by Make it official; a housemate whose own partner has left can be courted and can become the player's
    /// partner; and through a level-3 week every answer and choice the talk panel offers is one the engine accepts.
    /// </summary>
    public sealed class FreeTalkNewCoupleTests
    {
        private static IDisposable Tables() => TalkBank.Use(FreeTalkS3Fixture.Table());

        private sealed class Walked
        {
            public EpisodeState State;
            public string Left, Next;
        }

        private static Walked partnerLeft, pairLeft;

        [Test]
        public void ANewCoupleFormsByTheirDeclarationAfterThePartnerLeaves()
        {
            using (Tables())
            {
                var s = AfterThePartnerLeft(out string partner, out string next);
                Assert.That(TalkRomance.CoupleBarred(s, next), Is.False, "A partner who has left the house holds nobody back.");
                Assert.That(TalkRomance.Ready(s, next), Is.True, "The next housemate is ready.");

                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, next);
                Assert.That(offer.Talk?.Kind, Is.EqualTo(TalkKind.Declaration), "They declare.");
                Assert.That(TalkScreenLines.OfferChoices(engine.Snapshot, next, offer.Talk).Select(b => b.Choice), Has.Member(TalkChoice.Accept),
                    "The screen offers Accept.");
                FreeTalkS3Fixture.Say(engine, next, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.Accept)));
                NewCouple(engine.Snapshot, partner, next);
            }
        }

        [Test]
        public void ANewCoupleFormsByMakeItOfficialAfterThePartnerLeaves()
        {
            using (Tables())
            {
                var s = AfterThePartnerLeft(out string partner, out string next);
                // They were told "Not yet" this week, so they do not declare again before next week: the player flirts, and Make
                // it official is offered in the scene their answer leaves open (or at once, when the talk on offer is their opening).
                var contact = FreeTalk.Contact(s, next);
                contact.notYets = 1;
                contact.romanceWeek = s.week;

                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, next);
                Assert.That(offer.Available, Is.True, "The control: a talk with them.");
                Assert.That(offer.Talk.Kind, Is.Not.EqualTo(TalkKind.Declaration), "The control: they do not declare.");
                string talkId = offer.Talk.Id;
                List<TalkChoice> choices;
                if (offer.Talk.Kind == TalkKind.Opening)
                    choices = TalkScreenLines.OfferChoices(engine.Snapshot, next, offer.Talk).Select(b => b.Choice).ToList();
                else
                {
                    var replies = TalkScreenLines.Replies(engine.Snapshot, next, offer.Talk);
                    Assert.That(TalkScreenLines.FlirtShown(engine.Snapshot, next, offer.Talk, replies), Is.True, "The control: the Flirt answer is shown.");
                    var flirted = FreeTalkS3Fixture.Say(engine, next, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                    Assert.That(flirted.Scene, Is.EqualTo(RomanceScene.InKind), "The control: they flirt back.");
                    var open = TalkScreenLines.PendingScene(engine.Snapshot, next);
                    Assert.That(open, Is.Not.Null, "The control: their answer leaves the scene open.");
                    talkId = open.Row.Id;
                    choices = open.Choices.Select(b => b.Choice).ToList();
                }
                Assert.That(choices, Has.Member(TalkChoice.MakeItOfficial), "The screen offers Make it official.");
                var made = FreeTalkS3Fixture.Say(engine, next, TalkRomance.CommandText(talkId, TalkChoices.Answer(TalkChoice.MakeItOfficial)));
                Assert.That(made.Change, Is.EqualTo(RomanceChange.Couple), "Make it official made the couple.");
                NewCouple(engine.Snapshot, partner, next);
            }
        }

        [Test]
        public void AHousemateWhosePartnerLeftCanBeCourtedAndBecomeThePlayersPartner()
        {
            using (Tables())
            {
                var s = AfterTheirPartnerLeft(out string other, out string next);
                Assert.That(Bonds.Between(s, next, other, BondKinds.Showmance)?.status, Is.EqualTo(BondStatus.Apart), "The control: their bond is apart.");
                // The same season with their partner still in the house: barred.
                var held = s.Clone();
                Bonds.Between(held, next, other, BondKinds.Showmance).status = BondStatus.Private;
                Assert.That(TalkRomance.FlirtOpen(held, next), Is.False, "The control: no Flirt while their partner is in the house.");
                Assert.That(TalkRomance.Ready(held, next), Is.False, "The control: never ready while their partner is in the house.");
                Assert.That(TalkRomance.CoupleBarred(held, next), Is.True, "The control: barred.");

                Assert.That(TalkRomance.InAnotherShowmance(s, next), Is.False, "Their partner has left: nothing holds them back.");
                Assert.That(TalkRomance.FlirtOpen(s, next), Is.True, "The Flirt answer is offered with them again.");
                Assert.That(TalkRomance.Ready(s, next), Is.True, "Ready again.");

                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, next);
                Assert.That(offer.Talk?.Kind, Is.EqualTo(TalkKind.Declaration), "They declare.");
                Assert.That(TalkScreenLines.OfferChoices(engine.Snapshot, next, offer.Talk).Select(b => b.Choice), Has.Member(TalkChoice.Accept),
                    "The screen offers Accept.");
                FreeTalkS3Fixture.Say(engine, next, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.Accept)));
                var after = engine.Snapshot;
                Assert.That(TalkRomance.PartnerOf(after), Is.EqualTo(next), "They are the player's partner now.");
                Assert.That(Bonds.Between(after, after.playerId, next, BondKinds.Showmance)?.status, Is.EqualTo(BondStatus.Private));
                Assert.That(Bonds.Between(after, next, other, BondKinds.Showmance)?.status, Is.EqualTo(BondStatus.Apart),
                    "The house's own pair is left as it was.");
            }
        }

        [Test]
        public void EveryAnswerTheScreenOffersThroughALevelThreeWeekIsAccepted()
        {
            using (Tables())
            {
                var s = AfterThePartnerLeft(out _, out string next);
                var engine = new EpisodeEngine(s);
                int week = s.week, tried = 0, talks = 0, lastKey = -1;
                bool coupled = false;
                var refused = new List<string>();
                for (int step = 0; step < 3000; step++)
                {
                    var at = engine.Snapshot;
                    if (at.Find(at.playerId).status != ContestantStatus.Active || at.phase == EpisodePhase.Finished
                        || at.week > week && FreeTalk.WindowKey(at) != 0) break;
                    int key = FreeTalk.WindowKey(at);
                    if (key != 0 && key != lastKey)
                    {
                        lastKey = key;
                        tried += EveryOffer(at, refused, true);
                        foreach (string npc in FreeTalkFixture.Npcs(at).Select(c => c.id).ToList())
                        {
                            var now = engine.Snapshot;
                            string answer = Policy(now, npc, next, out string talkId);
                            if (answer == null) continue;
                            FreeTalkS3Fixture.Say(engine, npc, TalkScreenLines.AnswerText(now, talkId, answer, null));
                            talks++;
                            var after = engine.Snapshot;
                            coupled |= TalkRomance.PartnerOf(after) == next;
                            tried += EveryOffer(after, refused, false);
                        }
                    }
                    var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                    Assert.That(result.accepted, Is.True, "The walk: " + result.reason);
                }
                Assert.That(refused, Is.Empty, "Every answer the screen offered was accepted:\n" + string.Join("\n", refused));
                Assert.That(coupled, Is.True, "The control: the walk made the new couple.");
                Assert.That(talks, Is.GreaterThan(1), "The control: the player talked.");
                Assert.That(tried, Is.GreaterThan(20), "The control: the walk tried the screen's answers.");
            }
        }

        // ------------------------------------------------------------------ the seasons

        /// <summary>
        /// The player and their partner (the first of two ready housemates) are a couple; the house turns on the partner and the
        /// season walks on until they have left (their bond apart, the couple ended) and a window opens with the player and the
        /// second, still courted, in the house. Kept, and handed out as a copy.
        /// </summary>
        private static EpisodeState AfterThePartnerLeft(out string partner, out string next)
        {
            if (partnerLeft == null)
                partnerLeft = Search("a couple whose partner left", (s, one, two, note) =>
                {
                    var engine = new EpisodeEngine(s);
                    var offer = FreeTalk.Offer(engine.Snapshot, one);
                    if (offer.Talk?.Kind != TalkKind.Declaration) { note("no declaration from the first"); return null; }
                    FreeTalkS3Fixture.Say(engine, one, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.Accept)));
                    if (TalkRomance.PartnerOf(engine.Snapshot) != one) { note("no couple"); return null; }
                    // Left as an eviction leaves them: the couple ended at the anchor and the bond apart (not ended or betrayed).
                    return Departs(engine, one, two, x => FreeTalk.Contact(x, one)?.romance == TalkRules.Ended
                        && Bonds.Between(x, x.playerId, one, BondKinds.Showmance)?.status == BondStatus.Apart, note);
                });
            partner = partnerLeft.Left;
            next = partnerLeft.Next;
            var state = partnerLeft.State.Clone();
            Assert.That(Bonds.Between(state, state.playerId, partner, BondKinds.Showmance)?.status, Is.EqualTo(BondStatus.Apart),
                "The control: the old bond is apart.");
            Assert.That(TalkRomance.PartnerOf(state), Is.Null, "The control: no couple stands.");
            return state;
        }

        /// <summary>
        /// The second of two ready housemates in a showmance of the house's own with a third, free of the week's roles; the house
        /// turns on the third and the season walks on until they have left (the pair's bond apart) and a window opens with the
        /// player and the second in the house. Kept, and handed out as a copy.
        /// </summary>
        private static EpisodeState AfterTheirPartnerLeft(out string other, out string next)
        {
            if (pairLeft == null)
                pairLeft = Search("a house pair whose partner left", (s, one, two, note) =>
                {
                    string third = FreeTalkFixture.Npcs(s).FirstOrDefault(c => c.id != one && c.id != two && !FreeTalk.IsAllStar(c) && Free(s, c))?.id;
                    if (third == null) { note("no third housemate"); return null; }
                    if (Bonds.Form(s, two, third, BondKinds.Showmance, BondStatus.Private, null) == null) { note("no bond"); return null; }
                    return Departs(new EpisodeEngine(s), third, two,
                        x => Bonds.Between(x, two, third, BondKinds.Showmance)?.status == BondStatus.Apart, note);
                });
            other = pairLeft.Left;
            next = pairLeft.Next;
            return pairLeft.State.Clone();
        }

        /// <summary>
        /// Seasons walked to the first window of week two at talk rules 3 with the player in the house and two housemates who,
        /// made Spark for a Spark player, open to romance and courted (<see cref="Courting"/>), are interested and ready, neither
        /// an All-Star, the first free of the week's roles: the first of a fixed run of seeds where <paramref name="play"/>, from
        /// there, reaches its state. A failure says, by count, why no seed served.
        /// </summary>
        private static Walked Search(string what, Func<EpisodeState, string, string, Action<string>, Walked> play)
        {
            var why = new Dictionary<string, int>();
            void Note(string reason) { why.TryGetValue(reason, out int n); why[reason] = n + 1; }
            for (uint seed = 3201u; seed < 3321u; seed++)
            {
                var engine = new EpisodeEngine(FreeTalkS3Fixture.Fresh(seed));
                bool PlayerOut(EpisodeState x) => x.Find(x.playerId).status != ContestantStatus.Active;
                FreeTalkFixture.WalkUntil(engine, x => PlayerOut(x) || x.week >= 2 && FreeTalk.WindowKey(x) != 0 || x.phase == EpisodePhase.Finished);
                var s = engine.Snapshot;
                if (PlayerOut(s) || s.week < 2 || FreeTalk.WindowKey(s) == 0) { Note("no week-two window"); continue; }
                var ready = new List<string>();
                foreach (var npc in FreeTalkFixture.Npcs(s))
                {
                    if (FreeTalk.IsAllStar(npc)) { Note("all-star"); continue; }
                    var probe = s.Clone();
                    Courting(probe, npc.id);
                    if (!TalkRomance.Interested(probe, npc.id)) { Note("not interested, stance " + TalkRomance.StanceOf(probe, npc.id)); continue; }
                    if (!TalkRomance.Ready(probe, npc.id)) { Note("not ready"); continue; }
                    ready.Add(npc.id);
                }
                string one = ready.FirstOrDefault(id => Free(s, s.Find(id)));
                string two = ready.FirstOrDefault(id => id != one);
                if (one == null || two == null) { Note("ready " + ready.Count + (one == null ? ", none free" : "")); continue; }
                Courting(s, one);
                Courting(s, two);
                var found = play(s, one, two, Note);
                if (found != null) return found;
            }
            Assert.Fail("No season had " + what + ": " + string.Join("; ", why.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + " x" + k.Value)));
            return null;
        }

        /// <summary>
        /// The house turns on <paramref name="leaving"/> (every housemate's view of them -100; the player's is left as it is) and the
        /// digests' walker plays on until they have left the house, <paramref name="done"/> holds and a window is open, with the
        /// player and <paramref name="next"/> in the house, <paramref name="next"/> in no showmance in the house: that state, or null
        /// (a reason noted) when the walk does not get there within three more weeks.
        /// </summary>
        private static Walked Departs(EpisodeEngine engine, string leaving, string next, Func<EpisodeState, bool> done, Action<string> note)
        {
            var s = engine.Snapshot;
            foreach (var other in s.contestants.Where(c => !c.isPlayer && c.id != leaving)) FreeTalkFixture.SetScore(s, other.id, leaving, -100);
            engine = new EpisodeEngine(s);
            int start = s.week;
            for (int i = 0; i < 3000; i++)
            {
                var at = engine.Snapshot;
                if (at.phase == EpisodePhase.Finished || at.Find(at.playerId).status != ContestantStatus.Active
                    || at.Find(next).status != ContestantStatus.Active || at.week > start + 3) { note("the walk lost the player or the next one"); return null; }
                if (at.Find(leaving).status != ContestantStatus.Active && FreeTalk.WindowKey(at) != 0 && done(at))
                {
                    if (InAHousePair(at, next)) { note("the next one paired by the house"); return null; }
                    return new Walked { State = at, Left = leaving, Next = next };
                }
                var result = engine.Apply(EpisodeEngineTests.NextCommand(at));
                if (!result.accepted) { note("the walk refused: " + result.reason); return null; }
            }
            note("the walk did not end");
            return null;
        }

        /// <summary>Whether someone holds a private or public showmance bond (read here, not through the rule under test).</summary>
        private static bool InAHousePair(EpisodeState s, string id) =>
            s.story.bonds.Any(b => b.kind == BondKinds.Showmance && (b.status == BondStatus.Private || b.status == BondStatus.Public)
                                   && (b.aId == id || b.bId == id));

        /// <summary>Not the Head of Household, on the block or holding the veto: someone who can leave without the week's roles.</summary>
        private static bool Free(EpisodeState s, ContestantState who) =>
            who.id != s.hohId && !s.nominees.Contains(who.id) && who.id != s.vetoHolderId;

        /// <summary>
        /// A housemate made Spark for a Spark player, open to romance, and courted: flirting, a spark of 80, rapport 30, two
        /// Flirts, views of 70, agreement 5.
        /// </summary>
        private static void Courting(EpisodeState s, string npcId)
        {
            FreeTalkS3Fixture.Classes(s, npcId, TalkPools.Spark, TalkPools.Spark);
            // Open to romance: a sheet written closed is read as derived instead, whose stance follows the traits ("Social").
            if (TalkRomance.StanceOf(s, npcId) == RomanceStance.Closed && s.story.lore.FirstOrDefault(l => l.contestantId == npcId) is LoreCastState lore)
                lore.source = LoreSources.Derived;
            FreeTalkS3Fixture.Flirting(s, npcId, spark: 80, rapport: 30, flirts: 2, views: 70);
            FreeTalkFixture.ContactOf(s, npcId).agreement = 5;
        }

        private static void NewCouple(EpisodeState s, string left, string next)
        {
            Assert.That(TalkRomance.PartnerOf(s), Is.EqualTo(next), "A new couple stands.");
            Assert.That(Bonds.Between(s, s.playerId, next, BondKinds.Showmance)?.status, Is.EqualTo(BondStatus.Private), "Their showmance bond, private.");
            Assert.That(Bonds.Between(s, s.playerId, left, BondKinds.Showmance)?.status, Is.EqualTo(BondStatus.Apart), "The old bond stays apart.");
        }

        // ------------------------------------------------------------------ the walk

        /// <summary>
        /// The walk's player: with the housemate they court, a choice that makes the couple whenever the screen offers one; with
        /// anyone, the Flirt answer whenever the screen shows it, else the talk's first reply (a talk with choices only, such as
        /// someone else's declaration, is left); an open scene with anyone else is left too.
        /// </summary>
        private static string Policy(EpisodeState s, string npcId, string courted, out string talkId)
        {
            talkId = null;
            var open = TalkScreenLines.PendingScene(s, npcId);
            if (open != null)
            {
                talkId = open.Row.Id;
                return npcId == courted ? open.Choices.FirstOrDefault(b => b.Choice == TalkChoice.MakeItOfficial || b.Choice == TalkChoice.Accept)?.Answer : null;
            }
            var offer = FreeTalk.Offer(s, npcId);
            if (!offer.Available) return null;
            talkId = offer.Talk.Id;
            if (npcId == courted)
            {
                var couple = TalkScreenLines.OfferChoices(s, npcId, offer.Talk)
                    .FirstOrDefault(b => b.Choice == TalkChoice.MakeItOfficial || b.Choice == TalkChoice.Accept);
                if (couple != null) return couple.Answer;
            }
            var replies = TalkScreenLines.Replies(s, npcId, offer.Talk);
            return TalkScreenLines.FlirtShown(s, npcId, offer.Talk, replies) ? TalkRules.Flirt : replies.FirstOrDefault()?.Tone;
        }

        /// <summary>
        /// Every answer the talk panel offers now (an open scene's choices; the talk on offer's choices, its Flirt answer and,
        /// with <paramref name="tones"/>, its replies), each tried through the engine on the season as it stands: a refusal is
        /// listed. Returns how many were tried.
        /// </summary>
        private static int EveryOffer(EpisodeState s, List<string> refused, bool tones)
        {
            int tried = 0;
            foreach (var npc in FreeTalkFixture.Npcs(s))
            {
                var open = TalkScreenLines.PendingScene(s, npc.id);
                if (open != null)
                    foreach (var button in open.Choices) tried += Try(s, npc.id, open.Row.Id, button.Answer, refused);
                var offer = FreeTalk.Offer(s, npc.id);
                if (!offer.Available) continue;
                var replies = TalkScreenLines.Replies(s, npc.id, offer.Talk);
                if (tones)
                    foreach (var reply in replies) tried += Try(s, npc.id, offer.Talk.Id, reply.Tone, refused);
                if (TalkScreenLines.FlirtShown(s, npc.id, offer.Talk, replies)) tried += Try(s, npc.id, offer.Talk.Id, TalkRules.Flirt, refused);
                foreach (var button in TalkScreenLines.OfferChoices(s, npc.id, offer.Talk)) tried += Try(s, npc.id, offer.Talk.Id, button.Answer, refused);
            }
            return tried;
        }

        private static int Try(EpisodeState s, string npcId, string talkId, string answer, List<string> refused)
        {
            var command = TalkScreenLines.AnswerCommand(s, npcId, talkId, answer, null, "offer-" + s.revision + "-" + npcId + "-" + answer);
            var result = new EpisodeEngine(s).Apply(command);
            if (!result.accepted) refused.Add("week " + s.week + " " + s.phase + ", " + npcId + ", " + talkId + " " + answer + ": " + result.reason);
            return 1;
        }
    }
}
