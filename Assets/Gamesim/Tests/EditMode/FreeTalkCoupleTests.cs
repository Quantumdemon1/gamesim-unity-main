using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Free talk slice 3 (talk rules 3): the couple as the existing showmance bond and Couple fact (U4), the house's reaction
    /// (U8), endings, jealousy and a second declaration (spec 3.9). The tests' own romance rows only (no ordinary talk competes);
    /// Unity-free.
    /// </summary>
    public sealed class FreeTalkCoupleTests
    {
        private static IDisposable RomanceOnly() => TalkBank.Use(FreeTalkS3Fixture.Romance());

        /// <summary>A season in week 4 where an interested Spark housemate's declaration is on offer to a Spark player.</summary>
        private static EpisodeEngine Declaring(out string npc, uint seedFrom = 3111u)
        {
            var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out npc, week: 2);
            FreeTalkS3Fixture.Flirting(s, npc, spark: 80, rapport: 30, flirts: 2, views: 70);
            var engine = new EpisodeEngine(s);
            Assert.That(FreeTalk.Offer(engine.Snapshot, npc).Talk?.Kind, Is.EqualTo(TalkKind.Declaration), "The control: their declaration.");
            return engine;
        }

        /// <summary>A couple made by accepting their declaration, the house told of nobody else (or of the witness).</summary>
        private static EpisodeEngine Couple(out string npc, string seenBy = null)
        {
            var engine = Declaring(out npc);
            var offer = FreeTalk.Offer(engine.Snapshot, npc);
            FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.Accept), seenBy));
            return engine;
        }

        private static HouseFactState CoupleFact(EpisodeState s, string npc) =>
            Knowledge.Of(s, FactKinds.Couple, StoryConsumers.CoupleRef(s, s.playerId, npc));

        [Test]
        public void ACoupleIsTheExistingShowmanceBondAndCoupleFactAndStartsLateNightsAtItsSecondBeat()
        {
            using (RomanceOnly())
            {
                var engine = Couple(out string npc);
                var s = engine.Snapshot;
                var bond = Bonds.Between(s, s.playerId, npc, BondKinds.Showmance);
                Assert.That(bond, Is.Not.Null, "The existing showmance bond.");
                Assert.That(bond.status, Is.EqualTo(BondStatus.Private));
                var fact = CoupleFact(s, npc);
                Assert.That(fact, Is.Not.Null, "The existing Couple fact, by the bond's id.");
                Assert.That(fact.refId, Is.EqualTo(bond.id));
                Assert.That(fact.visibility, Is.EqualTo(FactVisibility.Whispered));
                Assert.That(fact.knowers, Does.Contain(s.playerId).And.Contain(npc));
                Assert.That(StoryConsumers.NominationPreference(s, npc, s.playerId), Is.GreaterThan(s.Score(npc, s.playerId)), "The existing consumers read it: a partner protects.");
                var thread = s.storylines.FirstOrDefault(x => x.templateId == "late-nights");
                if (thread != null)
                {
                    Assert.That(thread.cast.Any(r => r.role == "PARTNER" && r.contestantId == npc), Is.True, "Late Nights runs on this couple.");
                    Assert.That(thread.path.Any(p => p.beatId == "after-lights-out"), Is.False, "Its opening beat is never played at talk rules 3.");
                }
                Assert.That(EpisodeEngine.PoolCandidates(s, new StoryContext(s, StoryAnchors.EvictionNight)).Any(c => c.template.id == "late-nights"),
                    Is.False, "Late Nights' opening beat is not offered at talk rules 3.");
            }
        }

        [Test]
        public void ACoupleGoesPublicAfterThreeAnchorsAndTwoThirdHousematesEachSayAReactionOnce()
        {
            using (RomanceOnly())
            {
                var engine = Couple(out string npc);
                var s = engine.Snapshot;
                FreeTalkS3Fixture.Anchor(s, StoryAnchors.EvictionNight);
                FreeTalkS3Fixture.Anchor(s, StoryAnchors.HohCrowned);
                Assert.That(CoupleFact(s, npc).visibility, Is.Not.EqualTo(FactVisibility.Public), "Two anchors: not yet.");
                Assert.That(s.story.contacts.Count(c => c.talkFlags.Contains(TalkRules.CoupleTalk)), Is.EqualTo(0));
                FreeTalkS3Fixture.Anchor(s, StoryAnchors.Conversation);
                Assert.That(CoupleFact(s, npc).visibility, Is.Not.EqualTo(FactVisibility.Public), "A conversation is not an anchor of the house.");
                FreeTalkS3Fixture.Anchor(s, StoryAnchors.NomsSet);
                Assert.That(CoupleFact(s, npc).visibility, Is.EqualTo(FactVisibility.Public), "Three anchors: public.");
                foreach (var other in s.Active.Where(x => !x.isPlayer && x.id != npc))
                    Assert.That(Knowledge.Knows(CoupleFact(s, npc), other.id), Is.True, other.id + " knows.");
                var reacting = s.story.contacts.Where(c => c.talkFlags.Contains(TalkRules.CoupleTalk)).Select(c => c.npcId).ToList();
                Assert.That(reacting.Count, Is.EqualTo(2), "Two third housemates.");
                Assert.That(reacting, Does.Not.Contain(npc));
                FreeTalkS3Fixture.ValidAndReloads(s, "public");

                engine = new EpisodeEngine(s);
                var said = new List<int>();
                foreach (var who in reacting)
                {
                    var offer = FreeTalk.Offer(engine.Snapshot, who);
                    Assert.That(offer.Talk?.Kind, Is.EqualTo(TalkKind.Reaction), who + ": a reaction talk on offer.");
                    said.Add(offer.Talk.Reaction);
                    FreeTalkS3Fixture.Say(engine, who, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Warm));
                    engine = FreeTalkS3Fixture.NextWindow(engine, who);
                    Assert.That(TalkRomance.DueTalk(engine.Snapshot, who), Is.Null, who + ": once.");
                }
                Assert.That(said.OrderBy(x => x).ToList(), Is.EqualTo(new[] { 1, 2 }), "\"You two aren't subtle.\" and \"Careful. A pair is a target.\"");
                // The existing consumers act unchanged: a Head of Household who knows of the couple is 15 readier to split it.
                var known = engine.Snapshot;
                var unknown = known.Clone();
                var hidden = CoupleFact(unknown, npc);
                hidden.visibility = FactVisibility.Whispered;
                hidden.knowers.Remove(reacting[0]);
                Assert.That(StoryConsumers.NominationPreference(known, reacting[0], npc) - StoryConsumers.NominationPreference(unknown, reacting[0], npc),
                    Is.EqualTo(-15).Within(1e-9), "A known couple is a target.");
            }
        }

        [Test]
        public void ACoupleSeenTogetherGoesPublicAtOnce()
        {
            using (RomanceOnly())
            {
                var probe = Declaring(out string npc).Snapshot;
                string witness = probe.Active.First(x => !x.isPlayer && x.id != npc).id;
                var engine = Couple(out npc, witness);
                Assert.That(CoupleFact(engine.Snapshot, npc).visibility, Is.EqualTo(FactVisibility.Public), "Witnessed: public at once.");
                Assert.That(engine.Snapshot.story.contacts.Count(c => c.talkFlags.Contains(TalkRules.CoupleTalk)), Is.EqualTo(2));
            }
        }

        [Test]
        public void EndingItKindlyCostsThreeBothWaysClosesRomanceForTheSeasonAndAnExTalkIsDueForTwoWeeks()
        {
            using (RomanceOnly())
            {
                var engine = FreeTalkS3Fixture.NextWindow(Couple(out string npc), npc);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                Assert.That(offer.Talk?.Kind, Is.EqualTo(TalkKind.Couple), "The couple's talk.");
                Assert.That(TalkRomance.ChoicesNow(engine.Snapshot, npc, offer.Talk), Has.Member(TalkChoice.EndItKindly));
                var before = engine.Snapshot;
                FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.EndItKindly)));
                var after = engine.Snapshot;
                var c = FreeTalk.Contact(after, npc);
                Assert.That(Bonds.Holds(after, after.playerId, npc, BondKinds.Showmance), Is.False, "The bond ended.");
                Assert.That(Bonds.Betrayed(after, after.playerId, npc, BondKinds.Showmance), Is.False, "Kindly: no betrayal.");
                Assert.That(c.romance, Is.EqualTo(TalkRules.Ended));
                Assert.That(c.romanceClosedUntil, Is.EqualTo(TalkRules.ForTheSeason));
                Assert.That(after.Score(after.playerId, npc) - before.Score(before.playerId, npc), Is.EqualTo(-3).Within(1e-9), "-3 the player's way.");
                Assert.That(after.Score(npc, after.playerId) - before.Score(npc, before.playerId), Is.InRange(-3.6, -2.4), "-3 theirs, 0.8 to 1.2 of it.");
                Assert.That(TalkRomance.FlirtOpen(after, npc), Is.False, "Closed for the season.");

                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                Assert.That(FreeTalk.Offer(engine.Snapshot, npc).Talk?.Kind, Is.EqualTo(TalkKind.Ex), "The ex talk.");
                var later = engine.Snapshot;
                later.week += TalkRomance.ExWeeks + 1;
                FreeTalkS3Fixture.Anchor(later, StoryAnchors.HohCrowned);
                Assert.That(FreeTalk.Contact(later, npc).talkFlags, Does.Not.Contain(TalkRules.Ex), "Due for two weeks only.");
            }
        }

        [Test]
        public void TheyEndItBelowTenAtAnAnchorOrWhenTheirPartnerPutsThemUp()
        {
            using (RomanceOnly())
            {
                var s = Couple(out string npc).Snapshot;
                FreeTalkS2Fixture.Views(s, npc, 5, 50);
                FreeTalkS3Fixture.Anchor(s, StoryAnchors.HohCrowned);
                var c = FreeTalk.Contact(s, npc);
                Assert.That(c.romance, Is.EqualTo(TalkRules.Ended));
                Assert.That(Bonds.Holds(s, s.playerId, npc, BondKinds.Showmance), Is.False);
                Assert.That(s.Score(npc, s.playerId), Is.LessThan(5 - 6), "-8 more on their view.");
                Assert.That(c.romanceClosedUntil, Is.EqualTo(TalkRules.ForTheSeason));

                var t = Couple(out npc).Snapshot;
                t.hohId = t.playerId;
                double view = t.Score(npc, t.playerId);
                FreeTalkS3Fixture.PartnerNamed(t, npc);
                Assert.That(FreeTalk.Contact(t, npc).romance, Is.EqualTo(TalkRules.Ended), "Nominated by the partner: they end it.");
                Assert.That(Bonds.Betrayed(t, t.playerId, npc, BondKinds.Showmance), Is.True);
                Assert.That(t.Score(npc, t.playerId) - view, Is.InRange(-9.6, -6.4), "-8 more.");
            }
        }

        [Test]
        public void AFlirtElsewhereMayGetBackToThePartnerOnTheNextAnchorsKeyedCoin()
        {
            using (TalkBank.Use(FreeTalkS3Fixture.Table()))
            {
                var engine = Couple(out string partner);
                var s = engine.Snapshot;
                // Not one of the house's own pair: someone in another showmance is not offered a Flirt.
                var other = s.Active.First(x => !x.isPlayer && x.id != partner && !FreeTalk.IsAllStar(x) && !TalkRomance.InAnotherShowmance(s, x.id)).id;
                FreeTalkS2Fixture.ClassAs(s, other, TalkPools.Spark);
                FreeTalkFixture.ContactOf(s, other).rapport = 20;
                FreeTalkFixture.ContactOf(s, other).talkWindow = 0;
                engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, other);
                Assert.That(TalkRomance.FlirtOffered(engine.Snapshot, other, offer.Talk), Is.True, "The control: Flirt with someone else.");
                FreeTalkS3Fixture.Say(engine, other, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                var after = engine.Snapshot;
                Assert.That(FreeTalk.Contact(after, partner).talkFlags, Does.Contain(TalkRules.MayHear), "It may get back to the partner.");

                // Each anchor has its own keyed coin: find one that hears and one that does not.
                foreach (bool hears in new[] { true, false })
                {
                    string anchor = null; int week = after.week;
                    for (int w = after.week; w < after.week + 30 && anchor == null; w++)
                        foreach (var a in new[] { StoryAnchors.HohCrowned, StoryAnchors.NomsSet, StoryAnchors.VetoWon, StoryAnchors.BlockSet, StoryAnchors.EvictionNight })
                        {
                            var probe = after.Clone(); probe.week = w;
                            if (StoryRandom.Unit(probe, "talk:v1:jealous:" + partner + ":" + w + ":" + a) < TalkRomance.JealousyChance == hears) { anchor = a; week = w; break; }
                        }
                    Assert.That(anchor, Is.Not.Null, "A keyed coin each way.");
                    var at = after.Clone(); at.week = week;
                    var rng = at.randomState;
                    double view = at.Score(partner, at.playerId);
                    FreeTalkS3Fixture.Anchor(at, anchor);
                    var c = FreeTalk.Contact(at, partner);
                    Assert.That(c.talkFlags, Does.Not.Contain(TalkRules.MayHear), "The coin is spent.");
                    Assert.That(at.randomState, Is.EqualTo(rng), "No draw from the season's stream.");
                    if (hears)
                    {
                        Assert.That(c.talkFlags, Does.Contain(TalkRules.Jealous));
                        Assert.That(at.Score(partner, at.playerId) - view, Is.InRange(-6.0, -4.0), "-5 their view of you.");
                        Assert.That(TalkRomance.DueTalk(at, partner)?.Kind, Is.EqualTo(TalkKind.Jealousy), "Their jealousy talk, ahead of all.");
                        Assert.That(TalkRomance.ChoicesNow(at, partner, TalkRomance.DueTalk(at, partner)), Has.Member(TalkChoice.EndItKindly));
                    }
                    else
                    {
                        Assert.That(c.talkFlags, Does.Not.Contain(TalkRules.Jealous));
                        Assert.That(at.Score(partner, at.playerId), Is.EqualTo(view));
                    }
                }
            }
        }

        [Test]
        public void ASecondDeclarationWhileACoupleStandsEndsBothWithAPermanentLedgerEvent()
        {
            using (TalkBank.Use(FreeTalkS3Fixture.Table()))
            {
                var engine = Couple(out string partner);
                var s = engine.Snapshot;
                // The house's own pairs are ended first: the game lets nobody court a housemate in another showmance.
                foreach (var b in s.story.bonds.Where(b => b.kind == BondKinds.Showmance && BondStatus.Holds(b.status)
                             && b.aId != s.playerId && b.bId != s.playerId).ToList())
                    Bonds.End(s, b.aId, b.bId, BondKinds.Showmance, false);
                string other = null;
                foreach (var x in s.Active.Where(x => !x.isPlayer && x.id != partner && !FreeTalk.IsAllStar(x)))
                {
                    var stance = TalkRomance.StanceOf(s, x.id);
                    if (stance != RomanceStance.Open && stance != RomanceStance.Strategic) continue;
                    FreeTalkS2Fixture.ClassAs(s, x.id, TalkPools.Spark);
                    FreeTalkS3Fixture.Flirting(s, x.id, spark: 80, rapport: 30, flirts: 2, views: 70);
                    FreeTalk.Contact(s, x.id).agreement = 10;
                    if (TalkRomance.Interested(s, x.id) && TalkRomance.Ready(s, x.id)) { other = x.id; break; }
                }
                Assert.That(other, Is.Not.Null, "An interested, ready second housemate.");
                Assert.That(TalkRomance.DueTalk(s, other)?.Kind, Is.Not.EqualTo(TalkKind.Declaration), "They do not declare while the player is in a couple.");
                var c = FreeTalk.Contact(s, other);
                var flirtRow = TalkRomance.FlirtLine(s, other);
                c.talkWindow = FreeTalk.WindowKey(s);
                c.talkPending = flirtRow.Id;
                engine = new EpisodeEngine(s);
                var before = engine.Snapshot;
                var said = FreeTalkS3Fixture.Say(engine, other, TalkRomance.CommandText(flirtRow.Id, TalkChoices.Answer(TalkChoice.MakeItOfficial)));
                var after = engine.Snapshot;
                Assert.That(said.Change, Is.EqualTo(RomanceChange.SecondDeclaration));
                foreach (var id in new[] { partner, other })
                {
                    Assert.That(FreeTalk.Contact(after, id).romance, Is.EqualTo(TalkRules.Ended), id);
                    Assert.That(FreeTalk.Contact(after, id).romanceClosedUntil, Is.EqualTo(TalkRules.ForTheSeason), id);
                    Assert.That(after.Score(id, after.playerId) - before.Score(id, before.playerId), Is.InRange(-12.0, -8.0), id + ": -10.");
                    var edge = after.relationships.Single(r => r.fromId == id && r.toId == after.playerId);
                    var row = edge.events.SingleOrDefault(e => e.type == TalkRomance.SecondDeclarationEvent);
                    Assert.That(row, Is.Not.Null, id + ": the ledger event.");
                    Assert.That(row.decayable, Is.False, id + ": it never fades.");
                }
                Assert.That(Bonds.Betrayed(after, after.playerId, partner, BondKinds.Showmance), Is.True, "The partner's bond ends as a betrayal.");
                Assert.That(Bonds.ShowmancePartner(after, after.playerId), Is.Null, "No couple stands.");
            }
        }

        [Test]
        public void EverySceneAndChoiceLeavesTheSeasonsStreamByteEqualAndEveryStateSavesAndLoads()
        {
            // Say() asserts the stream and the reload on each answer; this walks a couple from the first Flirt to its end.
            using (TalkBank.Use(FreeTalkS3Fixture.Table()))
            {
                var s = FreeTalkS3Fixture.WithInterested(TalkPools.Spark, TalkPools.Spark, out string npc, week: 2);
                FreeTalkS3Fixture.Flirting(s, npc, spark: 30, rapport: 30, flirts: 0, views: 70).romance = "";
                var engine = new EpisodeEngine(s);
                var offer = FreeTalk.Offer(engine.Snapshot, npc);
                var flirt = FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Flirt));
                Assert.That(flirt.Scene, Is.EqualTo(RomanceScene.InKind));
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).talkPending, Is.EqualTo(flirt.SceneTalkId), "Saved mid-scene (Say reloaded it).");
                if (flirt.Choices.Contains(TalkChoice.MakeItOfficial))
                    FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(flirt.SceneTalkId, TalkChoices.Answer(TalkChoice.MakeItOfficial)));
                else
                {
                    engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                    offer = FreeTalk.Offer(engine.Snapshot, npc);
                    FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.Accept)));
                }
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).romance, Is.EqualTo(TalkRules.Couple));
                // From here the romance rows only, so the couple's talk is the talk on offer.
                using (TalkBank.Use(FreeTalkS3Fixture.Romance()))
                {
                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                offer = FreeTalk.Offer(engine.Snapshot, npc);
                FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Warm));
                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                offer = FreeTalk.Offer(engine.Snapshot, npc);
                FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkChoices.Answer(TalkChoice.EndItKindly)));
                engine = FreeTalkS3Fixture.NextWindow(engine, npc);
                offer = FreeTalk.Offer(engine.Snapshot, npc);
                FreeTalkS3Fixture.Say(engine, npc, TalkRomance.CommandText(offer.Talk.Id, TalkRules.Straight));
                Assert.That(FreeTalk.Contact(engine.Snapshot, npc).romance, Is.EqualTo(TalkRules.Ended));
                }
            }
        }
    }
}
