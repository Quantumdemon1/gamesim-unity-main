using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// M5, bonds, showmances and secrets: the stories that stand on who knows what. A showmance is
    /// a bond and a private fact until somebody notices; a secret is lore that only an arc reveals;
    /// a favour is a hook you spend into a deal the engine settles.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> BondsAndSecrets()
        {
            yield return LateNights();
            yield return BehindClosedDoors();
            yield return LooseLips();
            yield return EmergencyMeeting();
            yield return WhatTheyLeftOut();
            yield return RideOrDie();
            yield return CallInTheFavour();
            yield return StagedFeud();
            yield return SpyScreen();
        }

        // ---------------------------------------------------------------- late-nights (A1)

        /// <summary>The player's showmance fact: whoever else knows about the two of you.</summary>
        private static HouseFactState CoupleFact(StoryContext c, StoryCycle y) =>
            Knowledge.Of(c.state, FactKinds.Couple, StoryConsumers.CoupleRef(c.state, P(c), y.Role("PARTNER")));

        private static int OthersWhoKnow(StoryContext c, StoryCycle y)
        {
            var fact = CoupleFact(c, y);
            if (fact == null) return 0;
            if (fact.visibility == FactVisibility.Public) return 99;
            return fact.knowers.Count(id => id != P(c) && id != y.Role("PARTNER"));
        }

        private static ArcTemplate LateNights() => new ArcTemplate
        {
            id = "late-nights", lane = StoryLanes.Personal, eyebrow = "Showmance", title = "Late Nights",
            origin = "web:showmance_rumor (Shut It Down -2 / Lean Into It +5); the bond, exposure and veto dilemma native",
            rulesVersion = StoryRules.Bonds, minWeek = 2, playerNeeds = StoryPeople.Sensitivity.Romance,
            startAnchors = new[] { StoryAnchors.EvictionNight },
            conversationTopics = new[] { EpisodeCommandKind.PersonalChat, EpisodeCommandKind.RelationshipBuilding, EpisodeCommandKind.PillowTalk },
            roles = new[] { Role("PARTNER", StoryPeople.Sensitivity.Romance), Optional("GOSSIP") },
            cast = c =>
            {
                bool Open(ContestantState x) => Lore.RomanceOpen(c.state, x.id) && Bonds.ShowmancePartner(c.state, x.id) == null
                    && Mutual(c, P(c), x.id) >= 25 && (c.state.story.contacts.FirstOrDefault(k => k.npcId == x.id)?.rapport ?? 0) >= 4;
                string partner = c.focus != null
                    ? Among(c).Where(Open).OrderByDescending(x => Mutual(c, P(c), x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id
                    : c.talkingTo != null && c.Find(c.talkingTo) is ContestantState talked && Open(talked) ? talked.id : Warmest(c, Open);
                if (partner == null || Bonds.ShowmancePartner(c.state, P(c)) != null) return null;
                string gossip = Npcs(c).Where(x => x.id != partner && !c.state.Allied(P(c), x.id))
                    .OrderByDescending(x => Personality.Of(x).Sociable).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return Bind().With("PARTNER", partner).With("GOSSIP", gossip).Headlining(partner);
            },
            weight = (c, b) =>
            {
                double weight = 10;
                var contact = c.state.story.contacts.FirstOrDefault(k => k.npcId == b.Get("PARTNER"));
                // Time together this week, or the bedroom's pillow talk lately (21 §C4, decision D-E).
                if ((contact != null && contact.lastWeek == c.state.week && contact.weekCount >= 2)
                    || EpisodeEngine.PillowTalkedLately(c.state, b.Get("PARTNER"))) weight *= 2;
                if (Personality.Has(PlayerOf(c), "Introverted")) weight *= 0.3;
                return weight;
            },
            pulse = (c, y) =>
            {
                if (!Bonds.Holds(c.state, P(c), y.Role("PARTNER"), BondKinds.Showmance)) return "end:over";
                // The veto dilemma: at the veto, if you hold it with your partner on the block, or
                // your partner is the week's backdoor target.
                if (c.anchor == StoryAnchors.VetoWon && c.state.vetoHolderId == P(c)
                    && (Nominated(c, y.Role("PARTNER")) || (OthersWhoKnow(c, y) >= 2 && EpisodeEngine.BackdoorPlanned(c.state) == y.Role("PARTNER"))))
                    return "veto-stakes";
                if (c.anchor == StoryAnchors.BlockSet && (y.Var("partner-up") > 0 || y.Var("coming") > 0) && !y.Reached("verdict"))
                    return "verdict";
                if (!y.Reached("everyones-talking") && OthersWhoKnow(c, y) >= 2) return "everyones-talking";
                if (!y.Reached("everyones-talking") && c.anchor != StoryAnchors.EvictionNight)
                {
                    var partner = c.Find(y.Role("PARTNER"));
                    var fact = CoupleFact(c, y);
                    double chance = 0.2 + (Personality.Has(partner, "Emotional", "Impulsive") ? 0.1 : 0)
                                    + (fact != null && y.Role("GOSSIP") != null && fact.knowers.Contains(y.Role("GOSSIP")) ? 0.1 : 0);
                    if (StoryRandom.Chance(c.state, y.Id + ":exposure:w" + c.state.week + ":" + c.anchor, chance)) return "noticed";
                }
                return null;
            },
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "after-lights-out", surface = StorySurfaces.Approach, venue = Kitchen,
                    title = "After Lights Out", summary = "Late at night, it is just the two of you.",
                    text = "Everyone else went to bed an hour ago. {PARTNER} is still at the kitchen table with you, and neither of you has moved to leave.",
                    alternates = new[] { "{PARTNER} finds you in the backyard after the lights go down, and sits closer than the bench needs." },
                    lapse = "not-now",
                    options = new[]
                    {
                        Opt("lean-in", "Lean in", Medium, Warm, "Stop pretending this is just friendship.",
                            "You and {PARTNER} stopped pretending. For now, it stays between the two of you.",
                            Move(Player, "PARTNER", 6), Bond(Player, "PARTNER", BondKinds.Showmance, BondStatus.Private),
                            Fact(Player, "PARTNER", FactKinds.Couple, FactVisibility.Private)).Then(Waits),
                        Opt("strategic", "Keep it strategic", Medium, Calculated, "Turn the feeling into a final two instead.",
                            "You kept it about the game: an alliance, and a final-two offer.",
                            Alliance(Player, "PARTNER"), Deal(Player, "PARTNER", DealKind.FinalTwo)).LockUnless("Strategic", "Analytical").Then("end:strategic"),
                        Opt("just-friends", "Just friends", Low, Candid, "Say it kindly, and mean it.", "You told {PARTNER} it's friendship, and {PARTNER} took it well enough.",
                            Move(Player, "PARTNER", -2)).Then("end:friends"),
                        Lapse("not-now", "Not now", "Not tonight.", "You said goodnight.").Then("end:not-now"),
                    },
                },
                new BeatTemplate
                {
                    id = "noticed", surface = StorySurfaces.Npc, title = "Somebody Noticed",
                    options = new[]
                    {
                        Opt("whisper", "A whisper", null, null, Fact(Player, "PARTNER", FactKinds.Couple, FactVisibility.Whispered, "GOSSIP")).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "everyones-talking", surface = StorySurfaces.Scene, venue = Table,
                    title = "Everyone's Talking", summary = "The house has worked out that two houseguests are together.",
                    text = "The kitchen goes quiet when you and {PARTNER} walk in together. The house knows. The only question is what you say now.",
                    lapse = "say-nothing",
                    options = new[]
                    {
                        Opt("own-it", "Own it", High, Bold, "Say it out loud: you are together, and you are a pair.",
                            "You owned it. The house is counting you and {PARTNER} as one vote now.",
                            Alliance(Player, "PARTNER"), Receipt("PARTNER", Player, StoryReceipts.Showmance),
                            Bond(Player, "PARTNER", BondKinds.Showmance, BondStatus.Public), Spread(FactKinds.Couple, FactVisibility.Public))
                            .Extra((c, y) => Npcs(c).Where(x => x.id != y.Role("PARTNER") && !c.state.Allied(P(c), x.id))
                                .Select(x => Told("@" + x.id, Player, -4, null)))
                            .Then(Waits),
                        Opt("deny", "Deny it", Medium, Calculated, "\"We're just close. That's all.\"",
                            "You denied it, and the house bought it. For now.")
                            .Checked(50, "PARTNER", "{PARTNER} heard you deny it, and the house did not believe you anyway.",
                                Told("PARTNER", Player, -10, StoryReceipts.Snubbed), Spread(FactKinds.Couple, FactVisibility.Public))
                            .Then(Waits, Waits),
                        Opt("shut-down", "Shut it down", Low, Candid, "End it before it ends your game.",
                            "You ended it. {PARTNER} understood, mostly.",
                            Move(Player, "PARTNER", -2), BondEnd(Player, "PARTNER", BondKinds.Showmance, false)).Then("end:shut-down"),
                        Lapse("say-nothing", "Say nothing", "Let them talk.", "You said nothing, and the house decided for you.",
                            Spread(FactKinds.Couple, FactVisibility.Public)).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "veto-stakes", surface = StorySurfaces.Npc, title = "The Stakes",
                    npc = (c, y, options) => Nominated(c, y.Role("PARTNER")) ? "partner-up" : "coming",
                    options = new[]
                    {
                        Opt("partner-up", "Your partner is up", null, null, Var("partner-up", 1)).Then("on-the-block"),
                        Opt("coming", "They're coming for your partner", null, null, Var("coming", 1)).Then("coming-for-them"),
                    },
                },
                new BeatTemplate
                {
                    id = "on-the-block", surface = StorySurfaces.Summons, venue = DiaryRoom, closes = StoryAnchors.BlockSet,
                    title = "On the Block", summary = "You hold the veto, and the person you are closest to is on the block.",
                    text = "You are holding the Power of Veto, and {PARTNER} is sitting on the block. The whole house is waiting to see what a showmance is worth.",
                    lapse = "undecided",
                    options = new[]
                    {
                        Opt("promise-veto", "Promise to use it on them", Medium, Warm, "Tell {PARTNER} you will pull {PARTNER.them} off.",
                            "You promised {PARTNER} the veto. The meeting will show whether you meant it.", Deal(Player, "PARTNER", DealKind.VetoUse)).Then(Waits),
                        Lapse("undecided", "Keep your options open", "Decide in the room.", "You kept your options open.").Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "coming-for-them", surface = StorySurfaces.Summons, venue = DiaryRoom, closes = StoryAnchors.BlockSet,
                    title = "They're Coming for Them", summary = "The Head of Household means to backdoor your partner.",
                    text = "It is obvious now: the Head of Household put up two pawns to get to {PARTNER}. If you use the veto, {PARTNER} is the replacement. If you don't, a pawn you could have saved goes home.",
                    lapse = "decide-later",
                    options = new[]
                    {
                        Opt("pocket", "Keep it in your pocket", Medium, Calculated, "Protect {PARTNER} by not using the veto at all.",
                            "You decided the veto stays around your neck.", Memory(Player, "PARTNER", "I kept the veto in my pocket to protect my partner.")).Then(Waits),
                        Opt("anyway", "Use it anyway", High, Bold, "Save the pawn, and let the replacement fall where it falls.",
                            "You decided to use it anyway, whatever it costs {PARTNER}.", Memory(Player, "PARTNER", "I chose to use the veto even though it could put my partner up.")).Then(Waits),
                        Lapse("decide-later", "Decide in the room", "You will know when you are in there.", "You left it for the meeting.").Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "verdict", surface = StorySurfaces.Npc, title = "What It Was Worth",
                    options = new[]
                    {
                        Opt("verdict", "The verdict", null, null)
                            .Extra((c, y) =>
                            {
                                bool partnerUp = Nominated(c, y.Role("PARTNER"));
                                if (y.Var("partner-up") > 0)
                                    return partnerUp
                                        ? new[] { Receipt("PARTNER", Player, StoryReceipts.ShowmanceBetrayed), BondEnd(Player, "PARTNER", BondKinds.Showmance, true), Grudge("PARTNER", Player, 80) }
                                        : new[] { Receipt("PARTNER", Player, StoryReceipts.StoodUpFor) };
                                // The backdoor: a partner on the block now means you used the veto anyway.
                                return partnerUp
                                    ? new[] { Receipt("PARTNER", Player, StoryReceipts.ShowmanceBetrayed), BondEnd(Player, "PARTNER", BondKinds.Showmance, true), Grudge("PARTNER", Player, 80) }
                                    : NpcNominees(c).Select(x => Receipt("@" + x.id, Player, StoryReceipts.Snubbed)).ToArray();
                            })
                            .Then(Waits),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- behind-closed-doors

        private static ArcTemplate BehindClosedDoors() => new ArcTemplate
        {
            id = "behind-closed-doors", lane = StoryLanes.Game, eyebrow = "Secrets", title = "Behind Closed Doors",
            origin = "web:secret_deal; the couple fact native (20 §2.9)",
            rulesVersion = StoryRules.Bonds, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.HohCrowned },
            roles = new[] { Role("A", StoryPeople.Sensitivity.Romance), Role("B", StoryPeople.Sensitivity.Romance), Optional("HOH") },
            cast = c =>
            {
                var fact = c.state.story.facts.Where(f => f.kind == FactKinds.Couple && f.visibility != FactVisibility.Public && f.knowers.Contains(P(c))
                                                          && f.actorId != P(c) && f.subjectId != P(c)
                                                          && c.Find(f.actorId)?.status == ContestantStatus.Active && c.Find(f.subjectId)?.status == ContestantStatus.Active)
                    .OrderBy(f => f.id, StringComparer.Ordinal).FirstOrDefault();
                if (fact == null) return null;
                string hoh = NpcHoh(c);
                if (hoh == fact.actorId || hoh == fact.subjectId) hoh = null;
                return Bind().With("A", fact.actorId).With("B", fact.subjectId).With("HOH", hoh).Headlining(fact.actorId);
            },
            weight = (c, b) => 15,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "you-know", surface = StorySurfaces.Scene, venue = Bedroom,
                    title = "Behind Closed Doors", summary = "You know about a couple the house has not worked out yet.",
                    text = "You know something the house doesn't: {A} and {B} are together. They think nobody has noticed.",
                    lapse = "keep",
                    options = new[]
                    {
                        Opt("keep", "Keep it to yourself", Low, Calculated, "It is their business, until it isn't.", "You kept it to yourself."),
                        Opt("tell-hoh", "Tell the Head of Household", High, Hardball, "A couple is two votes. {HOH} should know.",
                            "You told {HOH}. A couple is two votes, and {HOH} can count.", Fact("A", "B", FactKinds.Couple, FactVisibility.Known, "HOH"))
                            .Checked(60, "A", "{HOH} knows now, and so do {A} and {B}: they know exactly who told.",
                                Fact("A", "B", FactKinds.Couple, FactVisibility.Known, "HOH"), Grudge("A", Player, 40), Grudge("B", Player, 40))
                            .Needs((c, y) => y.Role("HOH") != null, "Nobody to tell"),
                        Opt("leverage", "Use it as leverage", High, Hardball, "Let them know you know, and what that is worth.",
                            "{A} and {B} both owe you now, and they know it.", Hook(Player, "A"), Hook(Player, "B"), Grudge("A", Player, 20)),
                        Opt("join", "Ask to join them", Medium, Warm, "Two votes are good. Three are better.",
                            "{A} and {B} let you in. The three of you are a bloc now.", Alliance(Player, "A", "B"))
                            .Checked(50, "A", "They denied everything, and now they are wary of you.", Move(Player, "A", -5), Move(Player, "B", -5)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- loose-lips

        private static bool FinalTwoWith(StoryContext c, string npcId) =>
            c.state.deals.Any(d => DealStatus.Binds(d.status) && d.type == DealKind.FinalTwo
                                   && ((d.proposerId == P(c) && d.recipientId == npcId) || (d.proposerId == npcId && d.recipientId == P(c))))
            || c.state.promises.Any(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.FinalTwo
                                         && ((p.fromId == P(c) && p.toId == npcId) || (p.fromId == npcId && p.toId == P(c))));

        private static ArcTemplate LooseLips() => new ArcTemplate
        {
            id = "loose-lips", lane = StoryLanes.Game, eyebrow = "Secrets", title = "Loose Lips",
            origin = "web:secret_alliance_exposed; 08 shape 4 (\"your final two got out\")",
            rulesVersion = StoryRules.Bonds, minWeek = 3, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet },
            roles = new[] { Role("ALLY"), Role("PARTNER") },
            cast = c =>
            {
                var partner = Npcs(c).Where(x => FinalTwoWith(c, x.id)).OrderBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                if (partner == null) return null;
                string ally = Warmest(c, x => x.id != partner.id && c.state.Allied(P(c), x.id) && !FinalTwoWith(c, x.id));
                if (ally == null) return null;
                // It gets out on a keyed draw, a little more likely the more talkative the partner.
                double chance = 0.15 + 0.05 * Math.Max(0, Personality.Of(partner).Sociable - Personality.Of(partner).Honest);
                return StoryRandom.Chance(c.state, "w" + c.state.week + ":" + c.anchor + ":loose-lips:" + partner.id, chance)
                    ? Bind().With("ALLY", ally).With("PARTNER", partner.id).Headlining(ally) : null;
            },
            weight = (c, b) => 20,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "it-got-out", surface = StorySurfaces.Approach, venue = Storage,
                    title = "Loose Lips", summary = "An ally has found out about your final two with somebody else.",
                    text = "{ALLY} corners you in the storage room. \"Final two with {PARTNER}? When exactly were you going to tell me?\"",
                    lapse = "change-subject",
                    options = new[]
                    {
                        Opt("come-clean", "Come clean", Medium, Candid, "Tell {ALLY} the truth, and why.",
                            "You told {ALLY} the truth. It hurt, and {ALLY} respected it.", Move(Player, "ALLY", -4), Receipt("ALLY", Player, StoryReceipts.HeardOut)),
                        Opt("deny", "Deny it", High, Calculated, "\"Whoever told you that is playing you.\"", "{ALLY} believed you.")
                            .Checked(50, "ALLY", "{ALLY} knew you were lying, and now it is about the lie.",
                                Move(Player, "ALLY", -10), Grudge("ALLY", Player, 40, GrudgeCauses.LieDiscovered)),
                        Opt("under-bus", "Throw them under the bus", High, Hardball, "It was {PARTNER}'s idea, and {PARTNER} is the problem.",
                            "You pointed {ALLY} at {PARTNER}, and {ALLY} is looking at {PARTNER} differently now.",
                            View("ALLY", "PARTNER", -10), Move(Player, "ALLY", 3))
                            .Checked(55, "PARTNER", "{PARTNER} heard what you said, and the final two may not survive it.",
                                View("ALLY", "PARTNER", -10), Grudge("PARTNER", Player, 50), Told("PARTNER", Player, -10, StoryReceipts.SoldOut)),
                        Lapse("change-subject", "Change the subject", "Not now.", "You changed the subject. {ALLY} noticed.", Move(Player, "ALLY", -6)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- emergency-meeting

        private static ArcTemplate EmergencyMeeting() => new ArcTemplate
        {
            id = "emergency-meeting", lane = StoryLanes.Conflict, eyebrow = "House Meeting", title = "Emergency Meeting",
            origin = "web:crisis house_meeting (mid-week-crisis-system.ts); the knowledge requirement native",
            rulesVersion = StoryRules.Bonds, minWeek = 2, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionEve, StoryAnchors.EvictionNight },
            roles = new[] { Role("CALLER") },
            cast = c =>
            {
                // Somebody who resents you enough, and knows something about you to say.
                var caller = Npcs(c).Where(x => c.Grudge(x.id, P(c)) >= 60 && c.state.story.facts.Any(f => f.knowers.Contains(x.id)
                                                    && f.visibility != FactVisibility.Public && (f.actorId == P(c) || f.subjectId == P(c))))
                    .OrderByDescending(x => c.Grudge(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return caller == null ? null : Bind().With("CALLER", caller.id).Headlining(caller.id);
            },
            weight = (c, b) => 15,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "meeting", surface = StorySurfaces.Meeting, venue = Living, fallout = StoryLog.HouseMeeting,
                    title = "Emergency Meeting", summary = "A houseguest has called the house together, and it is about you.",
                    text = "{CALLER} has called the whole house into the living room, and it's about you. {CALLER} knows something, and is about to say it in front of everyone.",
                    lapse = "sit-there",
                    options = new[]
                    {
                        Opt("own-it", "Own it", High, Candid, "Stand up and say it before {CALLER} can.",
                            "You owned it before {CALLER} could. The honest ones respected that.", Move(Player, "CALLER", 2))
                            .Extra((c, y) => Npcs(c).Where(x => x.id != y.Role("CALLER") && Personality.Of(x).Honest >= 1)
                                .Select(x => Receipt("@" + x.id, Player, StoryReceipts.HeardOut))),
                        Opt("deny", "Deny it", High, Calculated, "Make {CALLER} prove it.", "{CALLER} could not prove it, and the room turned on {CALLER} instead.",
                            Move(Player, "CALLER", -4))
                            .Extra((c, y) => Npcs(c).Where(x => x.id != y.Role("CALLER")).Select(x => View("@" + x.id, "CALLER", -2)))
                            .Checked(45, "CALLER", "Nobody believed you, and everybody saw you try.",
                                Move(Player, "CALLER", -4))
                            .Bonus("Charming", "Manipulative", "Deceptive"),
                        Opt("turn-it", "Turn it on them", High, Bold, "Everyone in this room has something. Start with {CALLER}'s.",
                            "You turned it on {CALLER}, and the meeting became about {CALLER}.",
                            Move(Player, "CALLER", -10), Grudge("CALLER", Player, 20))
                            .Extra((c, y) => Npcs(c).Where(x => x.id != y.Role("CALLER")).Select(x => View("@" + x.id, "CALLER", -4)))
                            .Checked(40, "CALLER", "It looked exactly like what it was: a dodge.",
                                Move(Player, "CALLER", -10), Grudge("CALLER", Player, 20)),
                        Lapse("sit-there", "Sit there and take it", "Let {CALLER} say it.", "You sat there and took it.",
                            Receipt("CALLER", Player, StoryReceipts.TookIt)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- what-they-left-out (A8)

        private static ArcTemplate WhatTheyLeftOut() => new ArcTemplate
        {
            id = "what-they-left-out", lane = StoryLanes.Personal, eyebrow = "Secrets", title = "What They Left Out",
            origin = "web:proximity npc-secret (Keep it +5 / Leverage -4); depth-four secrets native",
            rulesVersion = StoryRules.Bonds, minWeek = 2, playerNeeds = StoryPeople.Sensitivity.Personal,
            startAnchors = new[] { StoryAnchors.EvictionNight },
            conversationTopics = new[] { EpisodeCommandKind.ShareSecret, EpisodeCommandKind.PersonalChat },
            roles = new[] { Role("CONFIDANT", StoryPeople.Sensitivity.Personal), Optional("LEAKER") },
            cast = c =>
            {
                bool Ready(ContestantState x)
                {
                    var secret = Lore.Facet(c.state, x.id, Lore.Facets.Secret);
                    if (secret == null || Lore.Knows(c.state, secret.id)) return false;
                    var learned = Lore.Learned(c.state, x.id);
                    // Under the reach rules knowing someone well - three things, one of them past
                    // small talk, which Know Them is for - is trust enough to be told the rest.
                    return Bonds.Holds(c.state, P(c), x.id, BondKinds.Confidant)
                           || learned.Any(f => f.depth >= 3)
                           || (c.AtLeast(StoryRules.Reach) && learned.Count >= 3 && learned.Any(f => f.depth >= 2));
                }
                string confidant = c.talkingTo != null && c.Find(c.talkingTo) is ContestantState talked && Ready(talked) ? talked.id : Warmest(c, Ready);
                if (confidant == null) return null;
                string leaker = Npcs(c).Where(x => x.id != confidant && Personality.Has(x, "Sneaky", "Deceptive") && c.state.Allied(x.id, confidant))
                    .OrderBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return Bind().With("CONFIDANT", confidant).With("LEAKER", leaker).Headlining(confidant);
            },
            weight = (c, b) => 15,
            pulse = (c, y) =>
            {
                if (y.Reached("somebody-knows")) return "end:done";
                if (c.anchor != StoryAnchors.BlockSet) return null;
                var leaker = c.Find(y.Role("LEAKER"));
                if (leaker == null) return "end:kept";
                var axes = Personality.Of(leaker);
                double chance = Math.Max(0.05, Math.Min(0.6, 0.2 + 0.1 * (axes.Sociable - axes.Honest)));
                return StoryRandom.Chance(c.state, y.Id + ":leak", chance) ? "somebody-knows" : "end:kept";
            },
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "keep-a-secret", surface = StorySurfaces.Conversation, venue = Bedroom,
                    title = "Can You Keep a Secret?", summary = "A houseguest trusts you with something nobody else in the house knows.",
                    text = "{CONFIDANT} checks the door twice before saying anything. \"I've never told anyone in here this. Can you keep a secret?\"",
                    lapse = "change-subject",
                    options = new[]
                    {
                        Opt("promise-keep", "Promise to keep it", Low, Warm, "\"It stays with me.\"", "{CONFIDANT} told you, and you promised it would stay with you.",
                            Reveal("CONFIDANT", Lore.Facets.Secret), Receipt("CONFIDANT", Player, StoryReceipts.SecretKept)).Then(Waits),
                        Opt("ask-why", "Ask why they're telling you", Medium, Candid, "\"Why me? Why now?\"",
                            "{CONFIDANT} told you the secret, and then why: the thing {CONFIDANT} could never forgive.",
                            Reveal("CONFIDANT", Lore.Facets.Secret), Reveal("CONFIDANT", Lore.Facets.Unforgivable))
                            .LockUnless("Analytical", "Intuitive").Then(Waits),
                        Opt("remember", "Remember it for later", Medium, Calculated, "Listen, and file it away for when it is worth something.",
                            "You listened carefully, and filed it away.", Reveal("CONFIDANT", Lore.Facets.Secret), Hook(Player, "CONFIDANT"))
                            .Against("Loyal").Then(Waits),
                        Lapse("change-subject", "Change the subject", "Some things are better not known.", "You changed the subject before {CONFIDANT} could say it.").Then("end:unheard"),
                    },
                },
                new BeatTemplate
                {
                    id = "somebody-knows", surface = StorySurfaces.Approach, venue = Hallway,
                    title = "Somebody Else Knows", summary = "A secret you were trusted with is out.",
                    text = "{CONFIDANT} finds you, white-faced. \"Somebody knows. I told one person in this house, and that was you.\"",
                    lapse = "say-nothing",
                    options = new[]
                    {
                        Opt("swear", "Swear it wasn't you", Medium, Candid, "Because it wasn't.", "{CONFIDANT} believed you.")
                            .Checked(70, "CONFIDANT", "{CONFIDANT} did not believe you.",
                                Receipt("CONFIDANT", Player, StoryReceipts.SecretExposed), Grudge("CONFIDANT", Player, 40))
                            .ShowIf((c, y) => !y.Took("remember")),
                        Opt("swear-hooked", "Swear it wasn't you", Medium, Candid, "You know how this looks.", "Against the odds, {CONFIDANT} believed you.")
                            .Checked(30, "CONFIDANT", "{CONFIDANT} did not believe you, and remembered how interested you had been.",
                                Receipt("CONFIDANT", Player, StoryReceipts.SecretExposed), Grudge("CONFIDANT", Player, 40))
                            .ShowIf((c, y) => y.Took("remember")),
                        Opt("tell-who", "Tell them who did", Medium, Hardball, "It was {LEAKER}. You are sure of it.",
                            "You told {CONFIDANT} it was {LEAKER}. That friendship is over.",
                            Move("CONFIDANT", "LEAKER", -15), Receipt("CONFIDANT", "LEAKER", StoryReceipts.SecretExposed))
                            .Needs((c, y) => y.Role("LEAKER") != null && c.state.memories.Any(m => m.ownerId == P(c) && m.subjectId == y.Role("LEAKER")),
                                "You have nothing on anybody"),
                        Opt("own-it", "Own it", High, Candid, "It was you. Say so.", "You owned it. {CONFIDANT} will not trust you with anything again.",
                            Receipt("CONFIDANT", Player, StoryReceipts.SecretExposed), Grudge("CONFIDANT", Player, 30), Move(Player, "CONFIDANT", -5)),
                        Lapse("say-nothing", "Say nothing", "Let {CONFIDANT} think what {CONFIDANT.they} likes.", "You said nothing, which said enough.",
                            Receipt("CONFIDANT", Player, StoryReceipts.SecretExposed)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- ride-or-die

        private static ArcTemplate RideOrDie() => new ArcTemplate
        {
            id = "ride-or-die", lane = StoryLanes.Personal, eyebrow = "Ride or Die", title = "Ride or Die",
            origin = "native: completes the loyalty oath (WebLoyaltyOaths) with a final-two promise and a bond",
            rulesVersion = StoryRules.Bonds, minWeek = 3, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet },
            roles = new[] { Role("PARTNER") },
            cast = c =>
            {
                if (Npcs(c).Any(x => Bonds.Holds(c.state, P(c), x.id, BondKinds.RideOrDie) && FinalTwoWith(c, x.id))) return null;
                // Fifty both ways was almost nobody's closest ally; under the reach rules thirty-five.
                double bond = c.AtLeast(StoryRules.Reach) ? 35 : 50;
                var partner = Among(c).Where(x => (Bonds.Holds(c.state, P(c), x.id, BondKinds.RideOrDie)
                                                  || c.state.loyaltyOaths.Any(o => (o.playerId == P(c) && o.targetId == x.id) || (o.targetId == P(c) && o.playerId == x.id))
                                                  || c.state.Allied(P(c), x.id))
                                                 && Mutual(c, P(c), x.id) >= bond && !FinalTwoWith(c, x.id))
                    .OrderByDescending(x => Mutual(c, P(c), x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return partner == null ? null : Bind().With("PARTNER", partner.id).Headlining(partner.id);
            },
            weight = (c, b) => c.anchor == StoryAnchors.BlockSet && !c.AtLeast(StoryRules.Reach) ? 0 : 12,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "final-two", surface = StorySurfaces.Approach, venue = Yard,
                    title = "Ride or Die", summary = "Your closest ally wants to make it a final two.",
                    text = "{PARTNER} sits down next to you in the backyard and does not bother with small talk. \"Final two. You and me. I'm serious.\"",
                    lapse = "not-yet",
                    options = new[]
                    {
                        Opt("shake", "Shake on it", Medium, Warm, "Final two. Whatever it takes.",
                            "You shook on it. Ride or die, all the way to the end.",
                            Promise("PARTNER", Player, PromiseKind.FinalTwo), Promise(Player, "PARTNER", PromiseKind.FinalTwo),
                            Bond(Player, "PARTNER", BondKinds.RideOrDie), Move(Player, "PARTNER", 5)),
                        Opt("not-yet", "Not yet", Low, Calculated, "It is too early to promise the end.", "You said not yet. {PARTNER} said fine, and meant most of it.",
                            Move(Player, "PARTNER", -2)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- call-in-the-favour

        private static ArcTemplate CallInTheFavour() => new ArcTemplate
        {
            id = "call-in-the-favour", lane = StoryLanes.Game, eyebrow = "A Favour", title = "Call in the Favour",
            origin = "web:immunityFromNomination (completed); the hook native",
            rulesVersion = StoryRules.Bonds, oncePerHeadliner = false, cooldownWeeks = 1,
            startAnchors = new[] { StoryAnchors.HohCrowned, StoryAnchors.VetoWon, StoryAnchors.BlockSet },
            roles = new[] { Role("HOLDER") },
            cast = c =>
            {
                string holder = null;
                if (c.anchor == StoryAnchors.HohCrowned) holder = NpcHoh(c);
                else if (c.anchor == StoryAnchors.VetoWon && PlayerNominated(c)) holder = NpcVetoHolder(c);
                else if (c.anchor == StoryAnchors.BlockSet && (PlayerNominated(c) || PlayerVotes(c)))
                    holder = EpisodeEngine.Voters(c.state).Where(v => !v.isPlayer && Hooks.Has(c.state, P(c), v.id))
                        .OrderBy(v => v.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return holder != null && Hooks.Has(c.state, P(c), holder) ? Bind().With("HOLDER", holder).Headlining(holder) : null;
            },
            weight = (c, b) => 30,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "favour", surface = StorySurfaces.Conversation, venue = Hallway,
                    title = "Call in the Favour", summary = "Somebody who owes you is in a position to pay it back.",
                    text = "{HOLDER} owes you, and both of you know it. This week {HOLDER} is in a position to pay it back.",
                    lapse = "save-it",
                    options = new[]
                    {
                        Opt("safety", "Ask for safety", Medium, Hardball, "Your name stays out of the nomination chairs.",
                            "{HOLDER} agreed: you are safe this week.", Deal("HOLDER", Player, DealKind.SafetyAgreement), SpendHook(Player, "HOLDER"))
                            .ShowIf((c, y) => c.state.hohId == y.Role("HOLDER")),
                        Opt("veto", "Ask for the veto", Medium, Hardball, "Pull you off the block.",
                            "{HOLDER} agreed to use the veto on you.", Deal("HOLDER", Player, DealKind.VetoUse), SpendHook(Player, "HOLDER"))
                            .ShowIf((c, y) => c.state.vetoHolderId == y.Role("HOLDER") && PlayerNominated(c)),
                        Opt("vote-save", "Ask for their vote", Medium, Hardball, "A vote to keep you.",
                            "{HOLDER} gave you the vote.", Deal(Player, "HOLDER", DealKind.VoteSave, Player), SpendHook(Player, "HOLDER"))
                            .ShowIf((c, y) => PlayerNominated(c) && EpisodeEngine.Voters(c.state).Any(v => v.id == y.Role("HOLDER"))),
                        Opt("vote-evict", "Ask for their vote against someone", Medium, Hardball, "Name the nominee you want gone.",
                            "{HOLDER} agreed to vote your way.", Deal(Player, "HOLDER", DealKind.VoteEvict, Pick), SpendHook(Player, "HOLDER"))
                            .Picks((c, y) => c.state.nominees.Where(id => id != P(c)))
                            .ShowIf((c, y) => PlayerVotes(c) && EpisodeEngine.Voters(c.state).Any(v => v.id == y.Role("HOLDER"))),
                        Lapse("save-it", "Save it for later", "A favour keeps.", "You kept the favour in your pocket."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- staged-feud (A10)

        private static List<string> Buyers(StoryContext c, StoryCycle y) =>
            Npcs(c).Where(x => x.id != y.Role("SHOWRUNNER") && c.Grudge(x.id, y.Role("SHOWRUNNER")) >= 40).Select(x => x.id).ToList();

        private static int AllianceKnowers(StoryContext c, StoryCycle y)
        {
            var alliance = c.state.alliances.FirstOrDefault(a => a.active && a.members.Contains(P(c)) && a.members.Contains(y.Role("SHOWRUNNER")));
            var fact = alliance == null ? null : Knowledge.Of(c.state, FactKinds.Alliance, alliance.id);
            if (fact == null) return 0;
            return fact.visibility == FactVisibility.Public ? 99 : fact.knowers.Count(id => id != P(c) && id != y.Role("SHOWRUNNER"));
        }

        private static ArcTemplate StagedFeud() => new ArcTemplate
        {
            id = "staged-feud", lane = StoryLanes.Conflict, eyebrow = "Showmanship", title = "Give Them a Show",
            origin = "native: the Big Brother fake fight; the showrunner's card (CastTemplates.cs, Quinn Martinez)",
            rulesVersion = StoryRules.Bonds, minWeek = 2, startAnchors = new[] { StoryAnchors.EvictionNight },
            roles = new[] { Role("SHOWRUNNER", StoryPeople.Sensitivity.Conduct) },
            cast = c =>
            {
                // Bold two and Sociable two was one card in the regular roster. Under the reach rules
                // anybody bold and social enough between them (three, neither at zero) who likes you.
                bool reach = c.AtLeast(StoryRules.Reach);
                bool Showy(ContestantState x)
                {
                    var axes = Personality.Of(x);
                    return reach ? axes.Bold >= 1 && axes.Sociable >= 1 && axes.Bold + axes.Sociable >= 3 : axes.Bold >= 2 && axes.Sociable >= 2;
                }
                var showrunner = Npcs(c).Where(x => !StoryPeople.IsRealPerson(x) && Showy(x)
                                                    && Mutual(c, P(c), x.id) >= (reach ? 10 : 20) && !c.state.Allied(P(c), x.id))
                    .OrderByDescending(x => Mutual(c, P(c), x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return showrunner == null ? null : Bind().With("SHOWRUNNER", showrunner.id).Headlining(showrunner.id);
            },
            weight = (c, b) => Lore.SheetIn(c.state, b.Get("SHOWRUNNER"))?.goal == "centre-of-the-story" ? 20 : 10,
            pulse = (c, y) =>
            {
                if (!c.state.Allied(P(c), y.Role("SHOWRUNNER"))) return "end:over";
                if (!y.Reached("it-was-fake") && AllianceKnowers(c, y) >= 3) return "it-was-fake";
                if (y.Reached("it-was-fake")) return "end:done";
                if (c.anchor == StoryAnchors.BlockSet && !y.Reached("the-fight")) return "the-fight";
                var a = Personality.Of(c.Find(y.Role("SHOWRUNNER")));
                double chance = (0.15 + (a.Sociable >= 2 && a.Honest >= 1 ? 0.1 : 0)) * (y.Var("cooled") > 0 ? 0.5 : 1);
                return StoryRandom.Chance(c.state, y.Id + ":exposure:w" + c.state.week + ":" + c.anchor, chance) ? "slip" : null;
            },
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "give-them-a-show", surface = StorySurfaces.Approach, venue = Living,
                    title = "Give Them a Show", summary = "A houseguest proposes a fake feud to hide a real alliance.",
                    text = "{SHOWRUNNER} corners you on the living-room couch, right where the cameras are, and smiles like it is a threat. \"You and me, we fight. Loudly. Every week. And nobody ever works out we're together.\"",
                    lapse = "not-my-style",
                    options = new[]
                    {
                        Opt("lets-do-it", "Let's do it", Medium, Playful, "A private alliance behind a public war.",
                            "You and {SHOWRUNNER} shook on it in private, and screamed at each other in public by dinner.",
                            Alliance(Player, "SHOWRUNNER"))
                            .Extra((c, y) => Buyers(c, y).Select(id => View("@" + id, Player, 4))).Then(Waits),
                        Opt("for-real", "Do it for real", High, Bold, "Have an actual row. {SHOWRUNNER} will love it anyway.",
                            "You had a real row, and {SHOWRUNNER} was delighted.", Receipt("SHOWRUNNER", Player, StoryReceipts.Argued), Move(Player, "SHOWRUNNER", 3))
                            .LockUnless("Confrontational").Then("end:for-real"),
                        Lapse("not-my-style", "Not my style", "Decline.", "You declined. {SHOWRUNNER} will find another co-star.",
                            View("SHOWRUNNER", Player, -4)).Then("end:declined"),
                    },
                },
                new BeatTemplate
                {
                    id = "slip", surface = StorySurfaces.Npc, title = "A Slip",
                    options = new[]
                    {
                        Opt("slip", "Somebody saw", null, null)
                            .Extra((c, y) =>
                            {
                                var witness = Keyed(c, y.Id + ":witness", Npcs(c).Where(x => x.id != y.Role("SHOWRUNNER")).ToList());
                                return witness == null ? Enumerable.Empty<Fx>() : new[] { LeakAlliance(Player, "SHOWRUNNER", "@" + witness.id) };
                            })
                            .Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "the-fight", surface = StorySurfaces.Scene, venue = Living, fallout = StoryLog.Blowup,
                    title = "The Fight", summary = "Time to sell the fake feud to the house again.",
                    text = "It is time for this week's episode. {SHOWRUNNER} catches your eye across the living room, and then it starts.",
                    lapse = "tone-down",
                    options = new[]
                    {
                        Opt("sell-it", "Sell it", High, Bold, "Go big. Make the house believe it.", "You sold it. The house has no idea.")
                            .Checked(60, "SHOWRUNNER", "Somebody caught the two of you laughing about it afterwards.")
                            .BackfireExtra((c, y) =>
                            {
                                var witness = Keyed(c, y.Id + ":fight-witness", Npcs(c).Where(x => x.id != y.Role("SHOWRUNNER")).ToList());
                                return witness == null ? Enumerable.Empty<Fx>() : new[] { LeakAlliance(Player, "SHOWRUNNER", "@" + witness.id) };
                            })
                            .LockUnless("Confrontational", "Impulsive", "Competitive").Then(Waits, Waits),
                        Opt("tone-down", "Tone it down", Low, Calculated, "Cool the feud off before somebody looks too closely.",
                            "You let it cool. Fewer people are watching now.", Var("cooled", 1)).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "it-was-fake", surface = StorySurfaces.Meeting, venue = Living, fallout = StoryLog.HouseMeeting,
                    title = "It Was Fake?", summary = "The house has worked out the feud was staged.",
                    text = "The whole house knows now: you and {SHOWRUNNER} have been working together all along. Everyone who took a side in your fights wants an explanation.",
                    lapse = "say-nothing",
                    options = new[]
                    {
                        Opt("own-it", "Own it", High, Bold, "Yes, it was a show. It was a good one.",
                            "You owned it. The alliance is public, and everybody who bought it resents you both.",
                            SpreadAlliance(Player, "SHOWRUNNER"))
                            .Extra((c, y) => Buyers(c, y).SelectMany(id => new[] { Grudge("@" + id, Player, 50, GrudgeCauses.LieDiscovered), Grudge("@" + id, "SHOWRUNNER", 50, GrudgeCauses.LieDiscovered) }))
                            .LockUnless("Confrontational", "Competitive", "Impulsive", "Charming"),
                        Opt("blame", "Blame them", High, Hardball, "It was {SHOWRUNNER}'s idea. You just went along.",
                            "You put it all on {SHOWRUNNER}. {SHOWRUNNER} will not forget it.", Grudge("SHOWRUNNER", Player, 60))
                            .Extra((c, y) => Buyers(c, y).SelectMany(id => new[] { Grudge("@" + id, Player, 25, GrudgeCauses.LieDiscovered), Grudge("@" + id, "SHOWRUNNER", 50, GrudgeCauses.LieDiscovered) })),
                        Opt("deny", "Deny it", High, Calculated, "It was real. Every word.", "You held the line, and somehow the house half believed you.")
                            .Checked(35, "SHOWRUNNER", "Nobody believed you, and the denial made it worse.")
                            .BackfireExtra((c, y) => Buyers(c, y).SelectMany(id => new[]
                                { Grudge("@" + id, Player, 50, GrudgeCauses.LieDiscovered), Grudge("@" + id, "SHOWRUNNER", 50, GrudgeCauses.LieDiscovered) })),
                        Lapse("say-nothing", "Say nothing", "Let the house think what it likes.", "You said nothing. The house decided for you.",
                            SpreadAlliance(Player, "SHOWRUNNER")),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- spy-screen (21 §5)

        private static ArcTemplate SpyScreen() => new ArcTemplate
        {
            id = "spy-screen", lane = StoryLanes.Moment, eyebrow = "Head of Household", title = "The Spy Screen",
            origin = "web:Spy Screen (roomActions.ts); chosen from recorded conversations, never randomly (21 §5)",
            rulesVersion = StoryRules.Bonds, oncePerHeadliner = false, cooldownWeeks = 1,
            startAnchors = new[] { StoryAnchors.NomsSet },
            roles = new[] { Role("A"), Role("B") },
            cast = c =>
            {
                if (!PlayerIsHoh(c)) return null;
                // The most recent private conversation between two houseguests this week, from the ledger.
                var talk = c.state.relationships
                    .Where(r => r.fromId != P(c) && r.toId != P(c) && string.CompareOrdinal(r.fromId, r.toId) < 0
                                && c.Find(r.fromId)?.status == ContestantStatus.Active && c.Find(r.toId)?.status == ContestantStatus.Active)
                    // This week or the social window just gone: the house talks after the eviction,
                    // and the spy screen shows the Head of Household what it is still talking about.
                    .SelectMany(r => r.events.Where(e => e.week >= c.state.week - 1 && (e.type == "talk" || e.type == "alliance-meeting"))
                        .Select(e => new { r.fromId, r.toId, e.sequence }))
                    .OrderByDescending(x => x.sequence).FirstOrDefault();
                return talk == null ? null : Bind().With("A", talk.fromId).With("B", talk.toId).Headlining(talk.fromId);
            },
            weight = (c, b) => 20,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "on-screen", surface = StorySurfaces.Scene, venue = HohRoom,
                    title = "The Spy Screen", summary = "From the HoH room you watch two houseguests talking in private.",
                    text = "From the HoH room's spy screen you watch {A} and {B} with their heads together in the storage room. They check the door twice. They have no idea you are watching.",
                    lapse = "keep",
                    options = new[]
                    {
                        Opt("confront", "Confront them", High, Bold, "Go down there and ask what that was about.", "You went downstairs and asked. It did not go well.",
                            Move(Player, "A", -6), Move(Player, "B", -6), Receipt("A", Player, StoryReceipts.Argued), LeakAlliance("A", "B", Player)),
                        Opt("keep", "Keep it", Low, Calculated, "Say nothing, and remember what you saw.", "You kept it to yourself.",
                            Memory(Player, "A", "I watched them whispering together on the spy screen."), LeakAlliance("A", "B", Player)),
                        Opt("use-it", "Use it", Medium, Hardball, "Let {A} know you saw, privately.", "{A} knows you saw, and owes you for keeping quiet.",
                            Hook(Player, "A"), LeakAlliance("A", "B", Player)),
                    },
                },
            },
        };
    }
}
