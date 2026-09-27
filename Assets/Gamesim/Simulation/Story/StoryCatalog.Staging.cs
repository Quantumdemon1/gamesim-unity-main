using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// M3, staging and reach: the HoH room, the alliance that drags its defector out, the week's
    /// real target, who gets called upstairs, the confrontations and pleas houseguests bring to
    /// you, and the Ringer's cheap Big Brother set pieces - goodbye messages and a prank war.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> Staging()
        {
            yield return HohRoomVisit();
            yield return TheAccounting();
            yield return TheBackdoor();
            yield return TheInviteList();
            yield return Confronted();
            yield return CampaignPitch();
            yield return GoodbyeMessage();
            yield return PrankWar();
        }

        // ---------------------------------------------------------------- hoh-room (A5)

        /// <summary>Whether the player is in the Head of Household's bottom two right now, as the HoH ranks the house.</summary>
        private static bool InBottomTwo(StoryContext c, string hoh) =>
            EpisodeEngine.NominationCandidates(c.state)
                .OrderBy(x => StoryConsumers.NominationPreference(c.state, hoh, x.id)).ThenBy(x => x.id, StringComparer.Ordinal)
                .Take(2).Any(x => x.isPlayer);

        private static ArcTemplate HohRoomVisit() => new ArcTemplate
        {
            id = "hoh-room", lane = StoryLanes.Game, eyebrow = "Head of Household", title = "The HoH Room",
            origin = "web:homesick_moment re-aimed at the HoH, plus post_hoh's pitch (phase-event-system.ts:44-76); the letter native",
            rulesVersion = StoryRules.Staging, startAnchors = new[] { StoryAnchors.HohCrowned },
            oncePerHeadliner = false, cooldownWeeks = 1,
            roles = new[] { Role("HOH"), Optional("RIVAL"), Optional("THREAT") },
            cast = c =>
            {
                // The fuller version of after-the-comp's pitch, so it steps aside with it where the
                // strategy windows play, and the letter from home goes with it.
                if (StrategyRules.Apply(c.state)) return null;
                string hoh = NpcHoh(c);
                if (hoh == null || c.Score(P(c), hoh) < 0) return null;
                string rival = Coldest(c, x => x.id != hoh);
                string threat = Npcs(c).Where(x => x.id != hoh && x.id != rival)
                    .OrderByDescending(x => x.hohWins + x.vetoWins).ThenByDescending(x => x.stats.competition)
                    .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return Bind().With("HOH", hoh).With("RIVAL", rival).With("THREAT", threat).Headlining(hoh);
            },
            weight = (c, b) => (c.AtLeast(StoryRules.Lore) && Lore.Facet(c.state, b.Get("HOH"), Lore.Facets.Home) != null ? 2 : 1) * 15,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "the-letter", surface = StorySurfaces.Approach, venue = HohRoom, closes = StoryAnchors.NomsSet,
                    title = "The HoH Room", summary = "The new Head of Household shows you the HoH room.",
                    text = "The house piles into the HoH room for the reveal: the photos, the snack basket, the letter from home. When the others drift back downstairs, {HOH} asks you to stay.",
                    alternates = new[]
                    {
                        "{HOH} is sitting on the edge of the HoH bed with the letter from home still in {HOH.their} hands, and waves you in. Everyone else has gone back downstairs.",
                    },
                    lapse = "downstairs",
                    options = new[]
                    {
                        Opt("read-letter", "Read it with them", Low, Warm, "Sit with {HOH} while {HOH} reads the letter from home.",
                            "{HOH} read you the letter from home, and you listened to every word.",
                            Move(Player, "HOH", 6), Receipt("HOH", Player, StoryReceipts.HeardOut), Reveal("HOH", Lore.Facets.Home))
                            .ShowIf((c, y) => !c.Real(y.Role("HOH"))),
                        Opt("congratulate", "Congratulate them", Low, Warm, "Tell {HOH} the win was earned.",
                            "You told {HOH} the win was earned, and meant it.", Move(Player, "HOH", 6), Receipt("HOH", Player, StoryReceipts.HeardOut))
                            .ShowIf((c, y) => c.Real(y.Role("HOH"))),
                        Opt("talk-game", "Talk game instead", Medium, Calculated, "Ask {HOH} straight out where you stand this week.",
                            "{HOH} told you straight where you stand this week.", Move(Player, "HOH", 8))
                            .Extra((c, y) =>
                            {
                                string hoh = y.Role("HOH");
                                string name = c.Find(hoh)?.name ?? "The Head of Household";
                                return new[] { Memory(Player, "HOH", InBottomTwo(c, hoh)
                                    ? name + " told me I am in the bottom two this week."
                                    : name + " told me I am not in the bottom two this week.") };
                            })
                            .Checked(50, "HOH", "{HOH} did not like being asked, and told you nothing.", Move(Player, "HOH", -4))
                            .LockUnless("Strategic", "Analytical"),
                        Opt("pitch-rival", "Pitch your rival", High, Calculated, "Steer {HOH} toward {RIVAL}.",
                            "You made the case against {RIVAL}, and {HOH} listened.", View("HOH", "RIVAL", -12))
                            .Checked(50, "HOH", "You made the case against {RIVAL}, and {RIVAL} heard about it by dinner.",
                                Told("RIVAL", Player, -8, StoryReceipts.SoldOut), Grudge("RIVAL", Player, 40))
                            .Bonus("Strategic", "Manipulative", "Charming")
                            .Needs((c, y) => y.Role("RIVAL") != null, "There is nobody to pitch"),
                        Opt("pitch-threat", "Pitch the comp threat", High, Calculated, "Remind {HOH} how much {THREAT} can win.",
                            "You pointed {HOH} at {THREAT}, and {HOH} did the maths.", View("HOH", "THREAT", -12))
                            .Checked(50, "HOH", "You pointed {HOH} at {THREAT}, and {THREAT} found out.",
                                Told("THREAT", Player, -8, StoryReceipts.SoldOut), Grudge("THREAT", Player, 40))
                            .Bonus("Strategic", "Analytical", "Competitive")
                            .Needs((c, y) => y.Role("THREAT") != null, "There is nobody to pitch"),
                        Lapse("downstairs", "Stay downstairs", "Leave {HOH} to it.", "You stayed downstairs.", Move(Player, "HOH", -2)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-accounting (A7)

        /// <summary>A houseguest who voted against an alliance-mate on the block, and the alliance they did it to.</summary>
        private static (string defector, string victim, AllianceState alliance) Defection(StoryContext c)
        {
            foreach (var vote in c.state.votes.OrderBy(v => v.voterId, StringComparer.Ordinal))
            {
                var voter = c.Find(vote.voterId);
                if (voter == null || voter.isPlayer || voter.status != ContestantStatus.Active || vote.targetId == P(c)) continue;
                var alliance = c.state.alliances.Where(a => a.active && a.members.Contains(vote.voterId) && a.members.Contains(vote.targetId))
                    .OrderBy(a => a.id, StringComparer.Ordinal).FirstOrDefault();
                if (alliance != null) return (vote.voterId, vote.targetId, alliance);
            }
            return (null, null, null);
        }

        private static IEnumerable<string> AllianceMembers(StoryContext c, StoryCycle y) =>
            c.state.alliances.Where(a => a.active && a.members.Contains(y.Role("DEFECTOR")) && a.members.Contains(y.Role("VICTIM")))
                .SelectMany(a => a.members).Distinct()
                .Where(id => id != y.Role("DEFECTOR") && id != P(c) && c.Find(id)?.status == ContestantStatus.Active);

        private static ArcTemplate TheAccounting() => new ArcTemplate
        {
            id = "the-accounting", lane = StoryLanes.Conflict, eyebrow = "House Meeting", title = "The Accounting",
            origin = "web:src/systems/emergent-event-system.ts:152-188 (houseMeetingEvent, never called); v5 terms native",
            rulesVersion = StoryRules.Staging, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionNight },
            roles = new[] { Role("DEFECTOR"), Departed("VICTIM") },
            cast = c =>
            {
                var (defector, victim, _) = Defection(c);
                return defector == null ? null : Bind().With("DEFECTOR", defector).With("VICTIM", victim).Headlining(defector);
            },
            weight = (c, b) => 20,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "living-room-now", surface = StorySurfaces.Meeting, venue = Living, fallout = StoryLog.HouseMeeting,
                    title = "Living Room, Now", summary = "An alliance wants the house to hear how one of its own voted.",
                    text = "{DEFECTOR}'s vote did not stay secret. {DEFECTOR} voted against {VICTIM}, {DEFECTOR.their} own alliance-mate, and the rest of that alliance wants everybody in the living room. Now.",
                    lapse = "stay-quiet",
                    options = new[]
                    {
                        Opt("back-alliance", "Back the alliance", Medium, Hardball, "Stand with the alliance against the one who crossed it.",
                            "You backed the alliance. {DEFECTOR} is out of it, and knows who helped.",
                            Move(Player, "DEFECTOR", -5), AllianceLeave("DEFECTOR", "VICTIM"))
                            .Extra((c, y) => AllianceMembers(c, y).Select(id => Move(Player, "@" + id, 3))),
                        Opt("defend", "Defend the defector", High, Warm, "Say what everyone is thinking: it was a game move.",
                            "You stood up for {DEFECTOR}. The alliance will not forget it, and neither will {DEFECTOR}.",
                            Move(Player, "DEFECTOR", 12))
                            .Extra((c, y) =>
                            {
                                var fx = AllianceMembers(c, y).Select(id => Move(Player, "@" + id, -5)).ToList();
                                var defector = c.Find(y.Role("DEFECTOR"));
                                double chance = 0.3 + 0.1 * Personality.Of(defector).Honest;
                                if (c.AtLeast(StoryRules.Bonds) && StoryRandom.Chance(c.state, y.Id + ":ride-or-die", chance))
                                    fx.Add(Bond(Player, "DEFECTOR", BondKinds.RideOrDie));
                                return fx;
                            }),
                        Opt("turn-on-them", "Turn it on them", High, Bold, "Blow the whole alliance open in front of the house.",
                            "You blew it all open. The alliance is finished in front of everyone, and every one of them blames you.",
                            AllianceEnd("DEFECTOR", "VICTIM"), SpreadAlliance("DEFECTOR", "VICTIM"))
                            .Extra((c, y) => AllianceMembers(c, y).Concat(new[] { y.Role("DEFECTOR") }).Select(id => Grudge("@" + id, Player, 50)))
                            .LockUnless("Confrontational"),
                        Lapse("stay-quiet", "Stay quiet", "Let the alliance settle its own business.",
                            "You kept quiet, and the alliance settled it without you: {DEFECTOR} is out.", AllianceLeave("DEFECTOR", "VICTIM")),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-backdoor (A9)

        /// <summary>Whether a told pawn lets the plan slip: keyed on how talkative and how discreet they are.</summary>
        private static bool PawnLeaks(StoryContext c, StoryCycle y, string pawnRole)
        {
            var pawn = c.Find(y.Role(pawnRole));
            if (pawn == null) return false;
            // The player's own pawns are told only if the player told them; an NPC Head of
            // Household tells both, because that is how a backdoor is sold.
            bool told = y.Role("HOH") != null || y.Var("told:" + pawnRole) > 0;
            if (!told) return false;
            var axes = Personality.Of(pawn);
            double chance = Math.Max(0.05, Math.Min(0.8, 0.2 + 0.1 * (axes.Sociable - axes.Honest)));
            return StoryRandom.Chance(c.state, y.Id + ":leak:" + pawnRole, chance);
        }

        private static ArcTemplate TheBackdoor() => new ArcTemplate
        {
            id = EpisodeEngine.BackdoorArc, lane = StoryLanes.Game, eyebrow = "The Backdoor", title = "The Backdoor",
            origin = "native: completes SetBackdoorPlan (EpisodeEngine.cs) and 08:97 (\"one is often backdoored\")",
            rulesVersion = StoryRules.Staging, oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1,
            roles = new[] { Optional("HOH"), Optional("TARGET"), Optional("PAWN1"), Optional("PAWN2") },
            cast = c => null,
            pulse = (c, y) =>
            {
                string target = y.Role("TARGET") ?? P(c);
                bool settled = y.Reached("renomination") || y.Reached("slammed-shut") || y.Reached("you-were-the-target");
                if (c.anchor == StoryAnchors.BlockSet && !settled)
                {
                    if (c.state.nominees.Contains(target)) return y.Role("HOH") == null ? "renomination" : null;
                    if (y.Role("TARGET") != null) return "slammed-shut";
                    return PawnLeaks(c, y, "PAWN1") || PawnLeaks(c, y, "PAWN2") ? "you-were-the-target" : null;
                }
                if (c.anchor == StoryAnchors.EvictionNight) return y.Reached("pawn-home") ? "end:done" : "pawn-home";
                return null;
            },
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "plan", surface = StorySurfaces.Npc, title = "The Plan",
                    npc = (c, y, options) => y.Role("HOH") == null ? "yours"
                        : y.Role("PAWN1") == null || y.Role("PAWN2") == null ? "pawn" : "quiet",
                    options = new[]
                    {
                        Opt("yours", "Your plan", null, null).Then("your-pawns"),
                        Opt("pawn", "You are a pawn", null, null).Then("just-a-pawn"),
                        Opt("quiet", "A quiet plan", null, null).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "your-pawns", surface = StorySurfaces.Scene, venue = HohRoom, closes = StoryAnchors.VetoWon,
                    title = "Just a Pawn", summary = "You nominated two pawns. Do they know?",
                    text = "{PAWN1} and {PAWN2} are sitting in the nomination chairs, and neither of them is who you are really after. {TARGET} is. The question is whether the pawns get to know that.",
                    lapse = "keep-guessing",
                    options = new[]
                    {
                        Opt("tell-both", "Tell them both", Medium, Candid, "Tell {PAWN1} and {PAWN2} they are pawns. Nobody likes being blindsided.",
                            "You told them both. They are grateful, and now two more people know your plan.",
                            VarFor("PAWN1", "told", 1), VarFor("PAWN2", "told", 1), Move(Player, "PAWN1", 3), Move(Player, "PAWN2", 3)).Then(Waits),
                        Opt("tell-one", "Tell one of them", Medium, Calculated, "Let one pawn in on it, and keep the other guessing.",
                            "You let one of them in on it.", VarFor(Pick, "told", 1), Move(Player, Pick, 3))
                            .Picks((c, y) => new[] { y.Role("PAWN1"), y.Role("PAWN2") }.Where(id => id != null)).Then(Waits),
                        Opt("keep-guessing", "Keep them guessing", Low, Calculated, "Say nothing. The fewer people who know, the better.",
                            "You said nothing, and both pawns are sweating.", View("PAWN1", Player, -4), View("PAWN2", Player, -4)).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "just-a-pawn", surface = StorySurfaces.Conversation, venue = HohRoom, closes = StoryAnchors.VetoWon,
                    title = "Just a Pawn", summary = "The Head of Household says you are only a pawn.",
                    text = "{HOH} calls you up to the HoH room before the veto players are drawn. \"You're not the target. You're a pawn. I need you to trust me on this.\"",
                    lapse = "say-nothing",
                    options = new[]
                    {
                        Opt("take-word", "Take their word for it", Low, Warm, "Accept {HOH}'s promise of safety.",
                            "{HOH} promised you are safe. You will find out on Thursday what that is worth.", Promise("HOH", Player, PromiseKind.Safety)).Then(Waits),
                        Opt("in-writing", "Get it in writing", Medium, Calculated, "Make {HOH} commit: a safety pact, not a promise.",
                            "{HOH} agreed to a safety pact. If {HOH} breaks it, the whole house, and the jury, will know.",
                            Deal("HOH", Player, DealKind.SafetyAgreement)).LockUnless("Analytical").Then(Waits),
                        Opt("call-it", "Call it what it is", High, Bold, "\"If I'm a pawn, who's the target?\"",
                            "{HOH} finally admitted it: {TARGET} is the one {HOH} wants out.", View("HOH", Player, -6))
                            .LockUnless("Confrontational").Then("warn-target"),
                        Lapse("say-nothing", "Say nothing", "Nod and go back downstairs.", "You nodded and went back downstairs.").Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "warn-target", anchor = StoryAnchors.Conversation, talkRole = "TARGET", surface = StorySurfaces.Conversation,
                    closes = StoryAnchors.BlockSet, title = "The Real Target", summary = "You know who the week is really aimed at.",
                    text = "You know something {TARGET} doesn't: {HOH} put up two pawns to get to {TARGET.them}.",
                    lapse = "keep-quiet",
                    options = new[]
                    {
                        Opt("warn", "Warn them", Medium, Warm, "Tell {TARGET} what {HOH} is planning.",
                            "You told {TARGET}. {TARGET} will be ready, and {TARGET} will not forget who told {TARGET.them}.",
                            Move(Player, "TARGET", 6), Grudge("TARGET", "HOH", 40), Var("leaked", 1)).Then(Waits),
                        Lapse("keep-quiet", "Keep it to yourself", "It is not your plan to ruin.", "You kept it to yourself.").Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "renomination", surface = StorySurfaces.Meeting, venue = Living,
                    title = "The Veto Meeting", summary = "You named the real target as the replacement nominee.",
                    text = "The veto was used, and you named {TARGET} as the replacement. The whole house watched you do it, and now {TARGET} is looking straight at you.",
                    lapse = "keep-short",
                    options = new[]
                    {
                        Opt("say-to-face", "Say it to their face", High, Candid, "Tell {TARGET} exactly why, in front of everyone.",
                            "You told {TARGET} exactly why, to {TARGET.their} face. The house respected it. {TARGET} did not.")
                            .Extra((c, y) => Npcs(c).Where(x => x.id != y.Role("TARGET")).Select(x => Receipt("@" + x.id, Player, StoryReceipts.HeardOut)))
                            .Then(Waits),
                        Opt("keep-short", "Keep it short", Low, Calculated, "Say the name and sit down.", "You said the name and sat down.").Then(Waits),
                        Opt("blame-house", "Blame the house", Medium, Yield, "\"It's what the house wanted.\"",
                            "You said it was what the house wanted. {TARGET} half believed it. The honest ones heard you pass the buck.",
                            Ease("TARGET", Player, 20))
                            .Extra((c, y) => Npcs(c).Where(x => x.id != y.Role("TARGET") && Personality.Of(x).Honest >= 1)
                                .Select(x => Receipt("@" + x.id, Player, StoryReceipts.Snubbed)))
                            .Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "slammed-shut", surface = StorySurfaces.Npc, title = "The Backdoor Slammed Shut",
                    options = new[]
                    {
                        Opt("shut", "The plan failed", null, null)
                            .Extra((c, y) => PawnLeaks(c, y, "PAWN1") || PawnLeaks(c, y, "PAWN2")
                                ? new[] { Grudge("TARGET", y.Role("HOH") == null ? Player : "HOH", 60) } : Enumerable.Empty<Fx>())
                            .Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "you-were-the-target", surface = StorySurfaces.Approach, venue = Hallway,
                    title = "You Were the Target", summary = "One of this week's pawns tells you who the week was really aimed at.",
                    text = "The veto meeting is over and you are still standing. Then one of the nominees pulls you aside: {HOH} put them up as a pawn, and the real target all along was you.",
                    lapse = "file-away",
                    options = new[]
                    {
                        Opt("confront-hoh", "Go straight to the HoH", High, Bold, "Let {HOH} know you know.", "You let {HOH} know you know. It got loud.",
                            Move(Player, "HOH", -8), Receipt("HOH", Player, StoryReceipts.Argued)).Then(Waits),
                        Lapse("file-away", "File it away", "Say nothing, and remember it.", "You filed it away.",
                            Memory(Player, "HOH", "The Head of Household planned to backdoor me.")).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "pawn-home", surface = StorySurfaces.Npc, title = "A Pawn Goes Home",
                    options = new[]
                    {
                        Opt("reckoning", "The pawn's reckoning", null, null)
                            .Extra((c, y) =>
                            {
                                // Only the player's own told pawns: "you told me I was safe".
                                if (y.Role("HOH") != null) return Enumerable.Empty<Fx>();
                                return new[] { "PAWN1", "PAWN2" }.Where(r => y.Var("told:" + r) > 0 && y.Role(r) != null
                                                                            && c.Find(y.Role(r))?.status != ContestantStatus.Active)
                                    .SelectMany(r => new[] { Grudge(r, Player, 50, GrudgeCauses.LieDiscovered), Receipt(r, Player, StoryReceipts.SoldOut) });
                            })
                            .Then("end:done"),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-invite-list (21 §5)

        private static ArcTemplate TheInviteList() => new ArcTemplate
        {
            id = "the-invite-list", lane = StoryLanes.Moment, eyebrow = "Head of Household", title = "The Invite List",
            origin = "web:Invite Up (roomActions.ts, +9 and \"others noticed who you picked\")",
            rulesVersion = StoryRules.Staging, startAnchors = new[] { StoryAnchors.HohCrowned }, oncePerHeadliner = false, cooldownWeeks = 1,
            cast = c => PlayerIsHoh(c) && Npcs(c).Count >= 2 ? Bind() : null,
            weight = (c, b) => 25,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "invite", surface = StorySurfaces.Scene, venue = HohRoom, closes = StoryAnchors.NomsSet,
                    title = "The Invite List", summary = "You are Head of Household, and the HoH room is yours.",
                    text = "The HoH room is yours for the week: the photos, the letter, the snack basket, the only lock in the house. Who gets to come up is its own kind of vote, and the house is watching the stairs.",
                    lapse = "alone",
                    options = new[]
                    {
                        Opt("invite-one", "Invite someone up", Low, Warm, "Pick one person to share it with. Everyone will notice who.",
                            "You invited one person up. Everyone noticed who.",
                            Move(Player, Pick, 9), Receipt(Pick, Player, StoryReceipts.HeardOut), Snub(Pick))
                            .Picks((c, y) => Npcs(c).Select(x => x.id)),
                        Opt("party", "Throw the room open", Low, Playful, "Everybody up. Snacks for the house.", "You threw the room open, and the house had a night.")
                            .Extra((c, y) => Npcs(c).Select(x => Move(Player, "@" + x.id, 2))),
                        Lapse("alone", "Keep the room to yourself", "Lock the door and enjoy it.", "You kept the room to yourself."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- confronted and campaign-pitch (21 G5)

        private static ArcTemplate Confronted() => new ArcTemplate
        {
            id = "confronted", lane = StoryLanes.Moment, eyebrow = "Confrontation", title = "Confronted",
            origin = "web:npc-social-behavior.ts:759-890 (they confront you: Apologize +10 / Deflect -2 / Escalate -15)",
            rulesVersion = StoryRules.Staging, oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1,
            roles = new[] { Role("THEM") },
            cast = c => c.talkingTo == null ? null : Bind().With("THEM", c.talkingTo).Headlining(c.talkingTo),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "tone", surface = StorySurfaces.Npc, title = "The Tone",
                    npc = (c, y, options) => c.Score(y.Role("THEM"), P(c)) < -40 ? "harsh" : "firm",
                    options = new[] { Opt("harsh", "Harsh", null, null).Then("harsh-words"), Opt("firm", "Firm", null, null).Then("firm-words") },
                },
                ConfrontationBeat("harsh-words",
                    "{THEM} finds you in the kitchen and does not bother lowering {THEM.their} voice. \"I'm done pretending. You know exactly what you did.\""),
                ConfrontationBeat("firm-words",
                    "{THEM} catches you alone. \"We need to talk. Something you did has been bothering me, and I'd rather say it to your face.\""),
            },
        };

        private static BeatTemplate ConfrontationBeat(string id, string text) => new BeatTemplate
        {
            id = id, surface = StorySurfaces.Approach, venue = Kitchen, title = "Confronted",
            summary = "A houseguest confronts you.", text = text, lapse = "deflect",
            options = new[]
            {
                Opt("apologize", "Apologize", Low, Yield, "Own your part in it.", "You apologised, and {THEM} took it.",
                    Move(Player, "THEM", 10), Ease("THEM", Player, 10)),
                Opt("deflect", "Deflect", Medium, Calculated, "Talk your way round it.", "You talked your way round it. {THEM} was not convinced.",
                    Move(Player, "THEM", -2)),
                Opt("escalate", "Escalate", High, Bold, "Give as good as you get.", "It turned into a shouting match.",
                    Move(Player, "THEM", -15), Grudge("THEM", Player, 20), Receipt("THEM", Player, StoryReceipts.Argued)),
            },
        };

        private static ArcTemplate CampaignPitch() => new ArcTemplate
        {
            id = "campaign-pitch", lane = StoryLanes.Moment, eyebrow = "The Block", title = "The Campaign",
            origin = "web:npc-social-behavior.ts:759-890 (a nominee campaigns to you); the vote_save deal completes web intent (21 D-G)",
            rulesVersion = StoryRules.Staging, oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1, urgent = true,
            roles = new[] { Role("THEM") },
            cast = c => c.talkingTo == null || !PlayerVotes(c) ? null : Bind().With("THEM", c.talkingTo).Headlining(c.talkingTo),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "tone", surface = StorySurfaces.Npc, title = "The Tone",
                    npc = (c, y, options) => c.Score(y.Role("THEM"), P(c)) >= 40 ? "close" : "cool",
                    options = new[] { Opt("close", "Close", null, null).Then("close-plea"), Opt("cool", "Cool", null, null).Then("cool-plea") },
                },
                CampaignBeat("close-plea", "{THEM} sits down next to you like it is any other night. \"You know I'd do it for you. I need your vote this week.\""),
                CampaignBeat("cool-plea", "{THEM} catches you in the hallway, a little stiff. \"I know we're not the closest. But I'm asking. I need your vote.\""),
            },
        };

        private static BeatTemplate CampaignBeat(string id, string text) => new BeatTemplate
        {
            id = id, surface = StorySurfaces.Approach, venue = Hallway, title = "The Campaign",
            summary = "A nominee asks for your vote.", text = text, lapse = "noncommittal",
            options = new[]
            {
                Opt("support", "Promise support", Medium, Warm, "Tell {THEM} you will vote to keep {THEM.them}. The reveal will show whether you meant it.",
                    "You told {THEM} you have {THEM.their} back.", Move(Player, "THEM", 8), Deal(Player, "THEM", DealKind.VoteSave, "THEM")),
                Opt("noncommittal", "Stay noncommittal", Low, Calculated, "Promise nothing.", "You promised nothing."),
                Opt("refuse", "Refuse", High, Candid, "Tell {THEM} the truth.", "You told {THEM} the truth.", Move(Player, "THEM", -5)),
            },
        };

        // ---------------------------------------------------------------- the Ringer (20 §4.3)

        /// <summary>
        /// A goodbye message for the houseguest just evicted, recorded in the Diary Room. The new
        /// juror's view of you is 30% of their vote, so what you say here is on the record.
        /// </summary>
        private static ArcTemplate GoodbyeMessage() => new ArcTemplate
        {
            id = "goodbye-message", lane = StoryLanes.Moment, eyebrow = "Eviction Night", title = "Goodbye Message",
            origin = "native: the Ringer's goodbye messages (20 §4.3); jury answers per WebJuryQuestioningCatalog.cs:34,44",
            rulesVersion = StoryRules.Staging, startAnchors = new[] { StoryAnchors.EvictionNight },
            roles = new[] { Departed("EVICTEE") },
            cast = c =>
            {
                string gone = Evicted(c);
                var who = c.Find(gone);
                return who == null || who.isPlayer ? null : Bind().With("EVICTEE", gone).Headlining(gone);
            },
            weight = (c, b) => 30,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "message", surface = StorySurfaces.Summons, venue = DiaryRoom,
                    title = "Goodbye Message", summary = "Record a goodbye message for the houseguest just evicted.",
                    text = "{EVICTEE} is gone. Production wants a goodbye message for the jury house, and {EVICTEE} will watch it tonight.",
                    lapse = "skip",
                    options = new[]
                    {
                        Opt("classy", "Keep it classy", Low, Warm, "Wish {EVICTEE} well, and mean it.", "You kept it classy.", View("EVICTEE", Player, 4)),
                        Opt("tell-why", "Tell them why", Medium, Candid, "Explain the move, straight.", "You explained it, straight.")
                            .Extra((c, y) =>
                            {
                                string lead = NpcSocialActions.LeadTrait(c.Find(y.Role("EVICTEE")));
                                return new[] { View("EVICTEE", Player, lead == "Confrontational" || lead == "Analytical" ? 6 : 2) };
                            }),
                        Opt("rub-it-in", "Rub it in", High, Hardball, "Enjoy it. On camera.", "You rubbed it in. It felt good, for about a minute.",
                            View("EVICTEE", Player, -8), Stress(Player, -1)).LockUnless("Confrontational"),
                        Lapse("skip", "Skip it", "Say nothing on camera.", "You skipped it."),
                    },
                },
            },
        };

        private static ArcTemplate PrankWar() => new ArcTemplate
        {
            id = "prank-war", lane = StoryLanes.Moment, eyebrow = "House Life", title = "Prank War",
            origin = "native: the Ringer's prank war (20 §4.3)",
            rulesVersion = StoryRules.Staging, minWeek = 2, startAnchors = new[] { StoryAnchors.EvictionNight },
            roles = new[] { Role("PRANKSTER"), Role("MARK") },
            cast = c =>
            {
                var npcs = Npcs(c);
                // A playful, bold houseguest: Funny or Impulsive, or anyone the axes read that way. No
                // shipped roster card carries either trait, so the axes are what make it reachable.
                bool Playful(ContestantState x) => Personality.Has(x, "Funny", "Impulsive")
                    || (Personality.Of(x).Sociable >= 2 && Personality.Of(x).Bold >= 1);
                foreach (var prankster in npcs.Where(Playful).OrderBy(x => x.id, StringComparer.Ordinal))
                {
                    // Friends, by this house's measure: houseguests warm to each other slowly, and the
                    // plan's twenty was a starting guess the sweep found almost nobody reaches.
                    var mark = npcs.Where(x => x.id != prankster.id && Mutual(c, prankster.id, x.id) >= 8)
                        .OrderByDescending(x => Mutual(c, prankster.id, x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                    if (mark != null) return Bind().With("PRANKSTER", prankster.id).With("MARK", mark.id).Headlining(prankster.id);
                }
                return null;
            },
            weight = (c, b) => 8,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "prank", surface = StorySurfaces.Scene, venue = Bedroom,
                    title = "Prank War", summary = "A prank has started a war, and you are being recruited.",
                    text = "{PRANKSTER} short-sheeted {MARK}'s bed and filled the pillowcase with cereal, and the house is loving it. Now {MARK} wants revenge, and wants you in on it.",
                    lapse = "stay-out",
                    options = new[]
                    {
                        Opt("prank-back", "Prank them back", Low, Playful, "Help {MARK} get even.", "You helped {MARK} get even, and the whole house was in on the joke.",
                            Move(Player, "MARK", 4), Move(Player, "PRANKSTER", 2), Receipt("MARK", Player, StoryReceipts.HeardOut)),
                        Opt("truce", "Call a truce", Low, Warm, "Get them both to shake on it.", "You got them to call it a draw.",
                            Move(Player, "PRANKSTER", 2), Move(Player, "MARK", 2)),
                        Opt("too-far", "Go too far", High, Bold, "Take the revenge one step further than anyone agreed.",
                            "You went too far, and it stopped being funny. {PRANKSTER} is not laughing.",
                            Move(Player, "PRANKSTER", -6), Receipt("PRANKSTER", Player, StoryReceipts.Argued), Grudge("PRANKSTER", Player, 20))
                            .LockUnless("Impulsive"),
                        Lapse("stay-out", "Stay out of it", "Watch it unfold from the couch.", "You watched it unfold from the couch."),
                    },
                },
            },
        };
    }
}
