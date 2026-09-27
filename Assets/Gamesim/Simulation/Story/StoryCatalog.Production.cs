using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// M6, production as a character: Have-Nots, the conduct ladder and its rare removal, and the
    /// breaking points that stress builds to. The ladder is signposted and never rolled: a conduct
    /// option always strikes, says so before it is chosen, and needs a second press to confirm.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> ProductionArcs()
        {
            yield return SlopWeek();
            yield return OnNotice();
            yield return DiaryRoomCalls();
            yield return BreakingPoint();
            yield return BreakingPointNpc();
        }

        // ---------------------------------------------------------------- slop-week

        private static ArcTemplate SlopWeek() => new ArcTemplate
        {
            id = "slop-week", lane = StoryLanes.Production, eyebrow = "Have-Not Week", title = "Slop Week",
            origin = "web:have_not; the pantry rule break native (Big Brother's own penalty for eating off the list)",
            rulesVersion = StoryRules.Production, oncePerHeadliner = false, cooldownWeeks = 1,
            startAnchors = new[] { StoryAnchors.NomsSet, StoryAnchors.VetoWon },
            roles = new[] { Optional("FELLOW"), Optional("HOH") },
            cast = c =>
            {
                if (!Production.IsHaveNot(c.state, P(c))) return null;
                string fellow = Npcs(c).Where(x => Production.IsHaveNot(c.state, x.id)).OrderBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return Bind().With("FELLOW", fellow).With("HOH", NpcHoh(c));
            },
            weight = (c, b) => 40,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "slop", surface = StorySurfaces.Scene, venue = Kitchen,
                    title = "Slop Week", summary = "You are a Have-Not this week.",
                    text = "Day three of slop. The kitchen smells like everybody else's dinner, and the pantry door is right there.",
                    lapse = "eat-slop",
                    options = new[]
                    {
                        Opt("rally", "Rally the Have-Nots", Low, Warm, "Make slop week something you do together.",
                            "You and {FELLOW} made a game of it. Slop week bonds people.", Move(Player, "FELLOW", 4), Stress(Player, -1))
                            .Needs((c, y) => y.Role("FELLOW") != null, "You are the only Have-Not"),
                        Opt("pantry", "Raid the pantry", High, Hardball, "One bite. Nobody will know. Production will.",
                            "You ate off the list. Production saw, of course.", Strike(Player, "ate off the Have-Not list"), Stress(Player, -1))
                            .Conduct(),
                        Opt("let-it-out", "Let it out", Medium, Bold, "Tell {HOH} exactly what you think of the week.",
                            "You let {HOH} have it. It felt great, for a minute.", Stress(Player, -2), Move(Player, "HOH", -4), Receipt("HOH", Player, StoryReceipts.Argued))
                            .Needs((c, y) => y.Role("HOH") != null, "The Head of Household is you"),
                        Lapse("eat-slop", "Eat the slop", "It's one week.", "You ate the slop. It's one week."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- on-notice (A3)

        /// <summary>Whether somebody on notice has already been struck this week: at most one a week.</summary>
        private static bool StruckThisWeek(StoryContext c, string id) => Production.For(c.state, id, false)?.lastStrikeWeek == c.state.week;

        private static ArcTemplate OnNotice() => new ArcTemplate
        {
            id = "on-notice", lane = StoryLanes.Production, eyebrow = "Production", title = "On Notice",
            origin = "native: the shape of the real removals, an escalation the audience watched build (08 §B4)",
            rulesVersion = StoryRules.Production, oncePerHeadliner = false, cooldownWeeks = 1,
            roles = new[] { Role("HOTHEAD", StoryPeople.Sensitivity.Conduct), Optional("TARGET", StoryPeople.Sensitivity.Conduct) },
            cast = c => c.talkingTo == null ? null : Bind().With("HOTHEAD", c.talkingTo).With("TARGET", c.about).Headlining(c.talkingTo),
            pulse = (c, y) =>
            {
                string hothead = y.Role("HOTHEAD");
                if (Production.Strikes(c.state, hothead) == 0) return "end:calmed";
                // A flare-up is witnessed once a week, at most: then the player may step in.
                bool askedThisWeek = c.state.houseEvents.Any(e => e.cycleId == y.Id && e.week == c.state.week && e.contentId.EndsWith(":at-it-again", StringComparison.Ordinal));
                if (y.Var("heat") >= 1 && !askedThisWeek && c.anchor != StoryAnchors.EvictionNight) return "at-it-again";
                return c.anchor == StoryAnchors.Conversation ? null : "flare";
            },
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "noted", surface = StorySurfaces.Npc, title = "Noted",
                    options = new[] { Opt("noted", "Noted", null, null).Then(Waits) },
                },
                new BeatTemplate
                {
                    id = "flare", surface = StorySurfaces.Npc, title = "A Flare-up",
                    npc = (c, y, options) =>
                    {
                        var who = c.Find(y.Role("HOTHEAD"));
                        if (who == null) return "back-down";
                        int volatility = Personality.Volatility(c.state, who.id, y.Role("TARGET"));
                        bool pressured = Personality.StressStep(who) >= 2 || Nominated(c, who.id) || Production.IsHaveNot(c.state, who.id);
                        bool mayEscalate = !StruckThisWeek(c, who.id) && string.IsNullOrEmpty(c.state.story.pendingRemovalId);
                        double back = Personality.Weight(who, 4, steady: 0.3);
                        double shout = Personality.Weight(who, 2, bold: 0.2);
                        double escalate = mayEscalate ? Personality.Weight(who, 0.6, bold: 0.2, vengeful: 0.2) * (1 + Math.Max(0, volatility) / 4.0)
                                                        * (pressured ? 1.5 : 1) * (y.Var("egged") > 0 ? 2 : 1) : 0;
                        double roll = StoryRandom.Unit(c.state, y.Id + ":flare:w" + c.state.week + ":" + c.anchor) * (back + shout + escalate);
                        return roll < back ? "back-down" : roll < back + shout ? "shout" : "escalate";
                    },
                    options = new[]
                    {
                        Opt("back-down", "Backs down", null, null).Then(Waits),
                        Opt("shout", "Shouts", null, null, Var("heat", 1)).Then(Waits),
                        Opt("escalate", "Escalates", null, null, Strike("HOTHEAD", "another outburst"), Var("egged", -1)).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "at-it-again", surface = StorySurfaces.Scene, venue = Bedroom,
                    title = "They're at It Again", summary = "A houseguest production has already warned is shouting again.",
                    text = "{HOTHEAD} is shouting again, and this time the whole house can hear it from the bedroom. Everyone knows production has already had a word.",
                    lapse = "stay-out",
                    options = new[]
                    {
                        Opt("step-in", "Step in", Medium, Warm, "Get {HOTHEAD} out of there before production does.",
                            "You got {HOTHEAD} out of the room before it went any further. {HOTHEAD} owes you for that.",
                            Var("heat", -2), Receipt("TARGET", Player, StoryReceipts.StoodUpFor), Hook(Player, "HOTHEAD"))
                            .Checked(50, "HOTHEAD", "{HOTHEAD} turned on you instead.", Grudge("HOTHEAD", Player, 50))
                            .Bonus("Social", "Charming", "Loyal").Then(Waits, Waits),
                        Opt("egg-on", "Egg them on", High, Hardball, "One more push and production does your work for you.",
                            "You gave {HOTHEAD} one more push.", Var("egged", 1))
                            .LockUnless("Manipulative", "Sneaky").Then(Waits),
                        Opt("report", "Report it", Medium, Candid, "Tell the Diary Room what you saw.",
                            "You told the Diary Room. Production added it to the file.", Strike("HOTHEAD", "reported from the Diary Room"))
                            .Extra((c, y) => StoryRandom.Chance(c.state, y.Id + ":report-known", 0.3)
                                ? Npcs(c).Where(x => c.state.Allied(x.id, y.Role("HOTHEAD"))).Select(x => Receipt("@" + x.id, Player, StoryReceipts.SoldOut))
                                : Enumerable.Empty<Fx>())
                            .Then(Waits),
                        Lapse("stay-out", "Stay out of it", "It's not your fight.", "You stayed out of it.", Var("heat", -1)).Then(Waits),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- diary-room-calls (A4)

        private static ArcTemplate DiaryRoomCalls() => new ArcTemplate
        {
            id = "diary-room-calls", lane = StoryLanes.Production, eyebrow = "The Diary Room", title = "The Diary Room Calls",
            origin = "native: production's ladder for the player (20 §3.5)",
            rulesVersion = StoryRules.Production, oncePerHeadliner = false, cooldownWeeks = 0, pairCooldownWeeks = 0,
            roles = new[] { Optional("OTHER") },
            cast = c => null,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "warning", surface = StorySurfaces.Summons, venue = DiaryRoom,
                    title = "The Diary Room Calls", summary = "Production has called you to the Diary Room: a warning.",
                    text = "\"Please come to the Diary Room.\" Production is polite about it, and very clear: that was a rule break, and this is your warning.",
                    lapse = "understood",
                    options = new[]
                    {
                        Opt("understood", "Understood", Low, Yield, "Take the warning.", "You took the warning. It helped to have it said.", Stress(Player, -1)),
                        Opt("ask", "Ask what happens next", Low, Candid, "Find out exactly where the line is.",
                            "Production told you exactly: a second strike is a Have-Not week and no Head of Household competition; a third, in the wrong week, sends you home.",
                            Memory(Player, "OTHER", "Production's ladder: a warning, then a Have-Not week and sitting out the next Head of Household, then removal.")),
                        Opt("push-back", "Push back", High, Bold, "Tell production they have it wrong.",
                            "You argued. Production listened, and the strike stays on your file for an extra week.", PushBack(Player))
                            .LockUnless("Stubborn", "Confrontational"),
                    },
                },
                new BeatTemplate
                {
                    id = "penalty", surface = StorySurfaces.Summons, venue = DiaryRoom,
                    title = "Penalty", summary = "Production has penalised you: a Have-Not week and no Head of Household competition.",
                    text = "Second strike. Production's penalty: a Have-Not week, and you sit out the next Head of Household competition. The house heard the announcement. Now they are watching how you take it.",
                    lapse = "on-the-chin",
                    options = new[]
                    {
                        Opt("on-the-chin", "Take it on the chin", Low, Yield, "Own it in front of everyone.",
                            "You took it on the chin, and the house saw you do it.")
                            .Extra((c, y) => Npcs(c).Where(x => Personality.Of(x).Honest >= 0).Select(x => Receipt("@" + x.id, Player, StoryReceipts.TookIt))),
                        Opt("blame-house", "Blame the house", High, Hardball, "It wasn't your fault, and you say so.",
                            "You blamed the house. It felt good; the house heard you pass the buck.", Stress(Player, -1), Grudge("OTHER", Player, 20))
                            .Extra((c, y) => Npcs(c).Where(x => Personality.Of(x).Honest >= 1).Select(x => Receipt("@" + x.id, Player, StoryReceipts.Snubbed))),
                    },
                },
                new BeatTemplate
                {
                    id = "removal", surface = StorySurfaces.Summons, venue = DiaryRoom,
                    title = "Production's Decision", summary = "Production has decided to remove you from the house.",
                    text = "Production has made its decision. When this week's social time ends, you will leave the house. There is nothing left to argue.",
                    lapse = "accept",
                    options = new[] { Lapse("accept", "Understood", "Hear it out.", "You heard production out.") },
                },
            },
        };

        // ---------------------------------------------------------------- breaking-point

        private static ArcTemplate BreakingPoint() => new ArcTemplate
        {
            id = "breaking-point", lane = StoryLanes.Personal, eyebrow = "Breaking Point", title = "Breaking Point",
            origin = "native: a CK3-style mental break when stress runs out of road",
            rulesVersion = StoryRules.Production, oncePerHeadliner = false, cooldownWeeks = 2,
            startAnchors = new[] { StoryAnchors.NomsSet, StoryAnchors.BlockSet, StoryAnchors.EvictionEve },
            cast = c =>
            {
                var player = PlayerOf(c);
                bool breaking = player?.stressLevel == "Overwhelmed" && (PlayerNominated(c) || Production.IsHaveNot(c.state, P(c)));
                return breaking ? Bind() : null;
            },
            weight = (c, b) => 50,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "breaking", surface = StorySurfaces.Scene, venue = Bathroom,
                    title = "Breaking Point", summary = "The pressure has caught up with you.",
                    text = "It all catches up with you at once: the block, the slop, the whispering. You are in the bathroom with the door locked, and something has to give.",
                    lapse = "go-quiet",
                    options = new[]
                    {
                        Opt("cry", "Cry it out", Low, Candid, "Let it out, and let whoever finds you see it.",
                            "You let it out. The people who care about you came to find you.", Stress(Player, -2))
                            .Extra((c, y) => Npcs(c).Where(x => c.Score(x.id, P(c)) >= 20).Select(x => Receipt("@" + x.id, Player, StoryReceipts.HeardOut))),
                        Opt("snap", "Snap at someone", High, Bold, "Somebody is going to hear about it.",
                            "You snapped, and it landed on someone who did not deserve all of it.", Stress(Player, -1), Move(Player, Pick, -8), Receipt(Pick, Player, StoryReceipts.Argued))
                            .Picks((c, y) => Npcs(c).Select(x => x.id)),
                        Lapse("go-quiet", "Go quiet", "Shut the world out for a day.", "You went quiet for a day. It helped a little.", Stress(Player, -1)),
                    },
                },
            },
        };

        private static ArcTemplate BreakingPointNpc() => new ArcTemplate
        {
            id = "breaking-point-npc", lane = StoryLanes.Production, eyebrow = "Breaking Point", title = "Breaking Point",
            origin = "native: an NPC's mental break, played by their axes",
            rulesVersion = StoryRules.Production, oncePerHeadliner = false, cooldownWeeks = 1,
            startAnchors = new[] { StoryAnchors.NomsSet, StoryAnchors.BlockSet },
            roles = new[] { Role("WHO", StoryPeople.Sensitivity.Conduct), Optional("NEAREST") },
            cast = c =>
            {
                var who = Npcs(c).Where(x => x.stressLevel == "Overwhelmed" && (Nominated(c, x.id) || Production.IsHaveNot(c.state, x.id)))
                    .OrderBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                if (who == null) return null;
                string nearest = Npcs(c).Where(x => x.id != who.id).OrderBy(x => c.Score(who.id, x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return Bind().With("WHO", who.id).With("NEAREST", nearest).Headlining(who.id);
            },
            weight = (c, b) => 20,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "breaking", surface = StorySurfaces.Npc, title = "Breaking Point",
                    options = new[]
                    {
                        Opt("cry", "Cries it out", null, "{WHO} broke down in the bathroom, and came out a little lighter.", Stress("WHO", -2))
                            .Ai(w => Personality.Weight(w, 3, warm: 0.3)),
                        Opt("snap", "Snaps", null, "{WHO} snapped at {NEAREST}, and the house heard all of it.",
                            Stress("WHO", -1), Move("WHO", "NEAREST", -8), Grudge("NEAREST", "WHO", 20))
                            .Ai(w => Personality.Weight(w, 2, bold: 0.3, vengeful: 0.2)),
                        Opt("quiet", "Goes quiet", null, "{WHO} stopped talking to anyone for a day.", Stress("WHO", -1))
                            .Ai(w => Personality.Weight(w, 2, steady: 0.3)),
                    },
                },
            },
        };
    }
}
