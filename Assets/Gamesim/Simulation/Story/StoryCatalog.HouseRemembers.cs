using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// M2, the house remembers: arcs that stand on grudges. A nomination, a broken word, a vote
    /// against an ally - each leaves somebody holding something, and these are the stories that
    /// holding it tells: a blow-up in the kitchen, a feud, a swing vote, a pile-on, the web's four
    /// other branching stories, and the Big Brother build's reckonings, agendas and pleas.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> HouseRemembers()
        {
            yield return KitchenBlowup();
            yield return ColdShoulder();
            yield return BadBlood();
            yield return TheFlip();
            yield return TheHouseTurns();
            yield return TheConspiracy();
            yield return PowerShift();
            yield return BetrayalEvidence();
            yield return SecretAllianceOffer();
            yield return TheReckoning();
            yield return TheReckoningYours();
            yield return TheAgenda();
            yield return CaughtTalking();
            yield return FinalPlea();
            yield return VentSession();
        }

        // ---------------------------------------------------------------- the pair with a reason

        /// <summary>
        /// The NPC pair with the most reason to fight: a grudge of forty or more either way, or
        /// mutual warmth of ten below zero. The hothead is the more volatile of the two right now.
        /// </summary>
        private static (string hothead, string target) FeudingPair(StoryContext c)
        {
            var npcs = Npcs(c);
            (string a, string b, double heat) best = (null, null, 0);
            for (int i = 0; i < npcs.Count; i++)
                for (int j = i + 1; j < npcs.Count; j++)
                {
                    string a = npcs[i].id, b = npcs[j].id;
                    double grudge = Math.Max(c.Grudge(a, b), c.Grudge(b, a));
                    double mutual = Mutual(c, a, b);
                    if (grudge < 40 && mutual > -10) continue;
                    double heat = grudge + Math.Max(0, -mutual);
                    if (best.a == null || heat > best.heat) best = (a, b, heat);
                }
            if (best.a == null) return (null, null);
            bool aHot = Personality.Volatility(c.state, best.a, best.b) >= Personality.Volatility(c.state, best.b, best.a);
            return aHot ? (best.a, best.b) : (best.b, best.a);
        }

        private static ArcBinding FeudCast(StoryContext c, bool hot)
        {
            var (hothead, target) = FeudingPair(c);
            if (hothead == null) return null;
            bool volatileNow = Personality.Volatility(c.state, hothead, target) >= 1;
            if (volatileNow != hot) return null;
            var witnesses = Npcs(c).Where(x => x.id != hothead && x.id != target).OrderBy(x => x.id, StringComparer.Ordinal).ToList();
            var first = Keyed(c, "witness", witnesses);
            var second = first == null ? null : witnesses.Where(x => x.id != first.id).FirstOrDefault();
            return Bind().With("HOTHEAD", hothead).With("TARGET", target)
                .With("WITNESS1", first?.id).With("WITNESS2", second?.id).Headlining(hothead);
        }

        private static double FeudWeight(StoryContext c, ArcBinding b)
        {
            double weight = 10;
            if (b.Get("HOTHEAD") == c.state.hohId || b.Get("TARGET") == c.state.hohId
                || Nominated(c, b.Get("HOTHEAD")) || Nominated(c, b.Get("TARGET"))) weight *= 1.5;
            return weight;
        }

        /// <summary>Backing one side of a fight: +6 with them, −8 with the other, and a mark on both.</summary>
        private static OptionTemplate BackSide(string id, string label, string backed, string other, string description, string outcome) =>
            Opt(id, label, High, Bold, description, outcome,
                Move(Player, backed, 6), Move(Player, other, -8),
                Receipt(backed, Player, StoryReceipts.StoodUpFor), Receipt(other, Player, StoryReceipts.SoldOut),
                VarFor(backed, "sided", 1));

        /// <summary>After a side was taken: the one you backed comes to find you (A2 B3).</summary>
        private static BeatTemplate ThanksBeat(string id, string role) => new BeatTemplate
        {
            id = id, surface = StorySurfaces.Approach, venue = Yard,
            title = "Thanks for Having My Back", summary = "The houseguest you backed in a fight comes to find you.",
            text = "{" + role + "} finds you in the backyard. \"You didn't have to do that the other day. I won't forget it.\"",
            lapse = "not-now",
            options = new[]
            {
                Opt("make-official", "Make it official", Medium, Warm, "Turn it into an alliance.",
                    "You and {" + role + "} made it official.", Alliance(Player, role)).Then(Waits),
                Opt("keep-loose", "Keep it loose", Low, Calculated, "Agree to vote together, and nothing more.",
                    "You agreed to vote together this week, and left it at that.", Deal(Player, role, DealKind.VoteTogether)).Then(Waits),
                NotNow().Then(Waits),
            },
        };

        /// <summary>The kitchen blow-up's pulse: a second flare-up at the block, then the thanks, then it is over.</summary>
        private static string FeudPulse(StoryContext c, StoryCycle y)
        {
            bool thanked = y.Reached("thanks-hothead") || y.Reached("thanks-target");
            bool sided = y.Var("sided:HOTHEAD") > 0 || y.Var("sided:TARGET") > 0;
            bool brewing = y.Var("heat") >= 1 && !y.Reached("blow-up");
            if (thanked && !brewing) return "end:done";
            if (c.anchor == StoryAnchors.BlockSet && brewing) return "blow-up";
            if (c.anchor == StoryAnchors.EvictionNight)
            {
                if (sided && !thanked) return y.Var("sided:HOTHEAD") > y.Var("sided:TARGET") ? "thanks-hothead" : "thanks-target";
                if (!brewing) return "end:done";
            }
            return null;
        }

        // ---------------------------------------------------------------- kitchen-blowup (A2)

        private static ArcTemplate KitchenBlowup() => new ArcTemplate
        {
            id = "kitchen-blowup", lane = StoryLanes.Conflict, eyebrow = "House Drama", title = "The Kitchen Blow-up",
            origin = "web:src/systems/proximity-event-system.ts (npcs-arguing) and emergent-event-system.ts (confrontation); grudge trigger native",
            rulesVersion = StoryRules.Grudges, minWeek = 2, minActive = 4,
            startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet },
            // A blow-up is conduct: never about a real, identifiable person, on either side of it.
            roles = new[] { Role("HOTHEAD", StoryPeople.Sensitivity.Conduct), Role("TARGET", StoryPeople.Sensitivity.Conduct), Optional("WITNESS1"), Optional("WITNESS2") },
            cast = c => FeudCast(c, true),
            weight = FeudWeight,
            pulse = FeudPulse,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "words", surface = StorySurfaces.Scene, venue = Kitchen,
                    title = "Words in the Kitchen", summary = "Two houseguests are going at it in the kitchen.",
                    text = "It starts over the dishes and it is not about the dishes. {HOTHEAD} is in {TARGET}'s face, voices rising, and everyone in the kitchen has stopped pretending not to listen.",
                    alternates = new[]
                    {
                        "{HOTHEAD} slams a cupboard and turns on {TARGET}. Whatever has been building between them all week is coming out now, in front of the whole kitchen.",
                        "Dinner is not finished and neither, apparently, is {HOTHEAD}. {TARGET} said one thing too many, and now the two of them are shouting across the counter.",
                    },
                    lapse = "stay-out",
                    options = new[]
                    {
                        BackSide("back-starter", "Back the one who started it", "HOTHEAD", "TARGET",
                            "Stand with {HOTHEAD}. {TARGET} will not forget it.", "You backed {HOTHEAD}. {TARGET} looked at you like you had picked a side for life.").Then(Waits),
                        BackSide("back-receiver", "Back the one on the receiving end", "TARGET", "HOTHEAD",
                            "Stand with {TARGET}. {HOTHEAD} will not forget it.", "You stepped in for {TARGET}, and {HOTHEAD} turned on you instead.").Then(Waits),
                        Opt("calm", "Calm them both down", Medium, Warm, "Get between them and bring the temperature down.",
                            "You got between them and it worked. By the end {HOTHEAD} and {TARGET} were nearly laughing about it.",
                            Move("HOTHEAD", "TARGET", 5), Move(Player, "HOTHEAD", 3), Move(Player, "TARGET", 3), Ease("HOTHEAD", "TARGET", 20))
                            .Checked(45, "HOTHEAD", "\"Don't tell me to calm down.\" Now they were both angry, and some of it was at you.",
                                Move(Player, "HOTHEAD", -5), Move(Player, "TARGET", -5), Reveal("HOTHEAD", Lore.Facets.HotButton), Var("heat", 1))
                            .Bonus("Charming", "Funny", "Flexible").Then("end:peace", Waits),
                        Opt("egg-on", "Egg it on", High, Hardball, "Say the one thing that makes it worse. Quietly.",
                            "You said just the right thing at just the wrong moment. {HOTHEAD} and {TARGET} will be at each other's throats for weeks.",
                            Move("HOTHEAD", "TARGET", -12), Grudge("HOTHEAD", "TARGET", 20), Grudge("TARGET", "HOTHEAD", 20), Var("heat", 1))
                            .Checked(55, "HOTHEAD", "Somebody saw exactly what you were doing, and so did both of them.",
                                Grudge("HOTHEAD", Player, 40), Grudge("TARGET", Player, 40))
                            .LockUnless("Sneaky", "Manipulative").Against("Loyal").Then(Waits, Waits),
                        Lapse("stay-out", "Stay out of it", "Not your fight.", "You kept your head down and let them get on with it.").Then("aftermath"),
                    },
                },
                new BeatTemplate
                {
                    id = "aftermath", surface = StorySurfaces.Npc, title = "After the Row",
                    npc = (c, y, options) => NpcDraw(c, y, "aftermath", y.Role("HOTHEAD"), options,
                        h => Personality.Weight(h, 3, steady: 0.3), h => Personality.Weight(h, 2, warm: 0.3, honest: 0.2),
                        h => Personality.Weight(h, 2, bold: 0.25, vengeful: 0.25)),
                    options = new[]
                    {
                        Opt("fizzle", "It fizzles", null, "It blew over as fast as it blew up.").Then("end:fizzled"),
                        Opt("apology", "An apology", null, "{HOTHEAD} came back later and apologised to {TARGET}.",
                            Move("HOTHEAD", "TARGET", 4), Ease("TARGET", "HOTHEAD", 10)).Then("end:apology"),
                        Opt("simmer", "It simmers", null, "It did not blow over. {HOTHEAD} is still counting.", Var("heat", 1)).Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "blow-up", surface = StorySurfaces.Meeting, venue = Living, fallout = StoryLog.Blowup,
                    title = "The Blow-up", summary = "The fight between two houseguests has boiled over in front of the house.",
                    text = "It was always going to happen again. {HOTHEAD} and {TARGET} are on their feet in the living room, and this time the whole house is watching.",
                    lapse = "walk-away",
                    options = new[]
                    {
                        Opt("break-up", "Break it up", High, Bold, "Get between them before somebody does something they cannot take back.",
                            "You got between them and held the line. Both of them will remember who stepped in.",
                            Receipt("HOTHEAD", Player, StoryReceipts.StoodUpFor), Receipt("TARGET", Player, StoryReceipts.StoodUpFor),
                            Trust("WITNESS1", Player, 2, "Broke up the fight"), Trust("WITNESS2", Player, 2, "Broke up the fight"))
                            .Checked(35, "HOTHEAD", "You got caught in the middle of it, and it rattled you more than you let on.",
                                Modifier(Player, "rattled", 1))
                            .Bonus("Social", "Charming", "Loyal").Then("flare", "flare"),
                        BackSide("side-starter", "Take the starter's side", "HOTHEAD", "TARGET",
                            "In front of everyone, stand with {HOTHEAD}.", "You stood with {HOTHEAD} in front of the whole house.").Then("flare"),
                        BackSide("side-receiver", "Take the other side", "TARGET", "HOTHEAD",
                            "In front of everyone, stand with {TARGET}.", "You stood with {TARGET} in front of the whole house.").Then("flare"),
                        Lapse("walk-away", "Walk away", "Leave the room.", "You walked out and let the house deal with it.").Then("flare"),
                    },
                },
                FlareBeat("flare", "HOTHEAD", "a fight in the living room"),
                ThanksBeat("thanks-hothead", "HOTHEAD"),
                ThanksBeat("thanks-target", "TARGET"),
            },
        };

        /// <summary>
        /// After a blow-up the hothead decides how far it goes: backs down, shouts, or - from the
        /// production rules on, at a volatility of five or more and for a fictional houseguest only -
        /// escalates into a strike, and <c>on-notice</c> begins.
        /// </summary>
        private static BeatTemplate FlareBeat(string id, string role, string incident) => new BeatTemplate
        {
            id = id, surface = StorySurfaces.Npc, title = "How Far It Goes",
            npc = (c, y, options) =>
            {
                var who = c.Find(y.Role(role));
                if (who == null) return "back-down";
                string other = y.Role(role == "HOTHEAD" ? "TARGET" : "HOTHEAD");
                int volatility = Personality.Volatility(c.state, who.id, other);
                // Five was the regular roster's hottest houseguest and nobody else; under the reach
                // rules four is enough, which is still a short fuse.
                bool mayEscalate = c.AtLeast(StoryRules.Production) && volatility >= (c.AtLeast(StoryRules.Reach) ? 4 : 5) && !StoryPeople.IsRealPerson(who)
                                   && string.IsNullOrEmpty(c.state.story.pendingRemovalId);
                double back = Personality.Weight(who, 3, steady: 0.3);
                double shout = Personality.Weight(who, 2, bold: 0.2);
                double escalate = mayEscalate ? Personality.Weight(who, 1, bold: 0.25, vengeful: 0.25) * (1 + volatility / 4.0) : 0;
                double roll = StoryRandom.Unit(c.state, y.Id + ":" + id + ":w" + c.state.week) * (back + shout + escalate);
                return roll < back ? "back-down" : roll < back + shout ? "shout" : "escalate";
            },
            options = new[]
            {
                Opt("back-down", "Backs down", null, null).Then(Waits),
                Opt("shout", "Shouts", null, null, Var("heat", 1)).Then(Waits),
                Opt("escalate", "Escalates", null, null, Strike(role, incident)).Then(Waits),
            },
        };

        /// <summary>An NPC-held choice between three options by their weights, keyed on the cycle and the week.</summary>
        private static string NpcDraw(StoryContext c, StoryCycle y, string purpose, string holderId, OptionTemplate[] options,
            params Func<ContestantState, double>[] weights)
        {
            var holder = c.Find(holderId);
            var values = weights.Select(w => holder == null ? 1 : w(holder)).ToArray();
            double roll = StoryRandom.Unit(c.state, y.Id + ":" + purpose + ":w" + c.state.week) * values.Sum();
            for (int i = 0; i < values.Length && i < options.Length; i++)
            {
                if (roll < values[i]) return options[i].id;
                roll -= values[i];
            }
            return options[Math.Min(values.Length, options.Length) - 1].id;
        }

        // ---------------------------------------------------------------- cold-shoulder (A2 variant)

        private static ArcTemplate ColdShoulder() => new ArcTemplate
        {
            id = "cold-shoulder", lane = StoryLanes.Conflict, eyebrow = "House Drama", title = "The Silent Treatment",
            origin = "native: kitchen-blowup's Cold Shoulder variant, for a houseguest who goes quiet instead of loud",
            rulesVersion = StoryRules.Grudges, minWeek = 2, minActive = 4,
            startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet },
            roles = new[] { Role("HOTHEAD"), Role("TARGET"), Optional("WITNESS1"), Optional("WITNESS2") },
            cast = c => FeudCast(c, false),
            weight = FeudWeight,
            pulse = FeudPulse,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "silent", surface = StorySurfaces.Scene, venue = Bedroom,
                    title = "The Silent Treatment", summary = "One houseguest has stopped talking to another and started counting votes.",
                    text = "{HOTHEAD} has stopped talking to {TARGET} altogether. No scene, no shouting: {HOTHEAD} just leaves the room when {TARGET} walks in, and has started quietly counting votes.",
                    lapse = "stay-out",
                    options = new[]
                    {
                        BackSide("back-quiet", "Back the one who went quiet", "HOTHEAD", "TARGET",
                            "Take {HOTHEAD}'s side.", "You made it clear you are with {HOTHEAD}.").Then(Waits),
                        BackSide("back-frozen", "Back the one frozen out", "TARGET", "HOTHEAD",
                            "Take {TARGET}'s side.", "You made it clear you are with {TARGET}.").Then(Waits),
                        Opt("truce", "Broker a truce", Medium, Warm, "Get them in a room together and talking again.",
                            "It took an hour on the bedroom floor, but {HOTHEAD} and {TARGET} are speaking again.",
                            Ease("HOTHEAD", "TARGET", 20), Move(Player, "HOTHEAD", 3), Move(Player, "TARGET", 3))
                            .Checked(40, "HOTHEAD", "Neither of them wanted a peacemaker, and both of them said so.",
                                Move(Player, "HOTHEAD", -3), Move(Player, "TARGET", -3))
                            .Bonus("Social", "Charming", "Loyal").Then("end:peace", Waits),
                        Opt("use-it", "Use it", High, Calculated, "Tell {HOTHEAD} what {TARGET} has been saying.",
                            "You told {HOTHEAD} what {TARGET} said. {HOTHEAD} is grateful, and colder than ever toward {TARGET}.",
                            View("HOTHEAD", "TARGET", -8), Move(Player, "HOTHEAD", 4))
                            .Checked(60, "TARGET", "It worked on {HOTHEAD}, but {TARGET} found out exactly where it came from.",
                                View("HOTHEAD", "TARGET", -8), Move(Player, "HOTHEAD", 4), Grudge("TARGET", Player, 40), Told("TARGET", Player, -6, StoryReceipts.SoldOut))
                            .LockUnless("Strategic").Then(Waits, Waits),
                        Lapse("stay-out", "Stay out of it", "It will sort itself out. Or it won't.", "You stayed out of it.").Then(Waits),
                    },
                },
                ThanksBeat("thanks-hothead", "HOTHEAD"),
                ThanksBeat("thanks-target", "TARGET"),
            },
        };

        // ---------------------------------------------------------------- bad-blood

        private static ArcTemplate BadBlood() => new ArcTemplate
        {
            id = "bad-blood", lane = StoryLanes.Conflict, eyebrow = "Bad Blood", title = "Bad Blood",
            origin = "web:the confront popup (npc-social-behavior.ts:759-890); grudge trigger and nemesis native",
            rulesVersion = StoryRules.Grudges, minWeek = 2,
            startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet },
            roles = new[] { Role("RIVAL"), Optional("PEACEMAKER") },
            cast = c =>
            {
                var rival = Npcs(c).Where(x => c.Grudge(x.id, P(c)) >= 40)
                    .OrderByDescending(x => c.Grudge(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                if (rival == null) return null;
                var peacemaker = Npcs(c).Where(x => x.id != rival.id && c.Score(x.id, P(c)) >= 20 && c.Score(x.id, rival.id) >= 20)
                    .OrderByDescending(x => Math.Min(c.Score(x.id, P(c)), c.Score(x.id, rival.id))).ThenBy(x => x.id, StringComparer.Ordinal)
                    .FirstOrDefault();
                return Bind().With("RIVAL", rival.id).With("PEACEMAKER", peacemaker?.id).Headlining(rival.id);
            },
            weight = (c, b) => 10 * Math.Min(2, c.Grudge(b.Get("RIVAL"), P(c)) / 40),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "bad-blood", surface = StorySurfaces.Approach, venue = Hallway,
                    title = "Bad Blood", summary = "A houseguest has not forgiven you, and the house has noticed.",
                    text = "{RIVAL} has not said a civil word to you in days. Whatever happened, {RIVAL} has not forgotten it, and now you are alone in the hallway together.",
                    lapse = "fester",
                    options = new[]
                    {
                        Opt("clear-air", "Clear the air", Medium, Candid, "Say it straight and try to put it behind you.",
                            "You said your piece and {RIVAL} heard it. It is not friendship, but it is not war any more.",
                            Ease("RIVAL", Player, 20), Move(Player, "RIVAL", 5), Receipt("RIVAL", Player, StoryReceipts.MadePeace))
                            .Checked(50, "RIVAL", "It went the wrong way. Ten minutes later you were both shouting.",
                                Grudge("RIVAL", Player, 20), Receipt("RIVAL", Player, StoryReceipts.Argued), Move(Player, "RIVAL", -4))
                            .Bonus("Loyal", "Social", "Charming"),
                        Opt("peacemaker", "Bring in a peacemaker", Low, Warm, "Ask {PEACEMAKER}, who gets on with you both, to sit down with you.",
                            "{PEACEMAKER} sat the two of you down, and it helped more than either of you expected.",
                            Ease("RIVAL", Player, 30), Move(Player, "RIVAL", 3), Move(Player, "PEACEMAKER", 3))
                            .Checked(60, "RIVAL", "{RIVAL} felt ambushed, and {PEACEMAKER} got caught in the middle.",
                                Move(Player, "PEACEMAKER", -3), Grudge("RIVAL", Player, 10))
                            .Needs((c, y) => y.Role("PEACEMAKER") != null, "Nobody gets on with you both"),
                        Opt("get-in-face", "Get in their face", High, Bold, "Settle it the loud way, in front of everyone.",
                            "You got right in {RIVAL}'s face. The house will be talking about it for days, and so will production.",
                            Strike(Player, "got in a houseguest's face"), Move(Player, "RIVAL", -10),
                            Receipt("RIVAL", Player, StoryReceipts.PublicBlowup), Grudge("RIVAL", Player, 20), Bond(Player, "RIVAL", BondKinds.Nemesis))
                            .LockUnless("Confrontational", "Impulsive").Conduct(),
                        Lapse("fester", "Let it fester", "Say nothing. It will keep.", "You let it fester. It kept.",
                            Grudge("RIVAL", Player, 20))
                            .Extra((c, y) => c.Grudge(y.Role("RIVAL"), P(c)) + 10 >= 80
                                ? new[] { Bond(Player, "RIVAL", BondKinds.Nemesis) } : Enumerable.Empty<Fx>()),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-flip (A6)

        /// <summary>The player's own vote commitments this week, counted the way the player would count them.</summary>
        private static int CommittedVoters(StoryContext c) =>
            EpisodeEngine.Voters(c.state).Count(v => !v.isPlayer && (
                c.state.deals.Any(d => DealStatus.Binds(d.status) && (d.type == DealKind.VoteSave || d.type == DealKind.VoteEvict || d.type == DealKind.VoteTogether)
                                       && ((d.proposerId == v.id && d.recipientId == P(c)) || (d.proposerId == P(c) && d.recipientId == v.id)))
                || c.state.promises.Any(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.Vote && p.fromId == v.id && p.toId == P(c))));

        private static ArcTemplate TheFlip() => new ArcTemplate
        {
            id = "the-flip", lane = StoryLanes.Game, eyebrow = "The Vote", title = "The Flip",
            origin = "native: Big Brother's flip (08 §B3, shape 3); a deal because DealObligation reads a deal by target",
            rulesVersion = StoryRules.Grudges, minActive = 5, cooldownWeeks = 2,
            startAnchors = new[] { StoryAnchors.BlockSet },
            oncePerHeadliner = false,
            roles = new[] { Role("SWING"), Optional("HOH") },
            cast = c =>
            {
                if (!PlayerVotes(c) && !PlayerNominated(c)) return null;
                var voters = EpisodeEngine.Voters(c.state).Where(v => !v.isPlayer).ToList();
                if (voters.Count == 0 || CommittedVoters(c) * 2 >= voters.Count) return null;
                var swing = voters.Where(v => !c.state.deals.Any(d => DealStatus.Binds(d.status)
                                                   && ((d.proposerId == v.id && d.recipientId == P(c)) || (d.proposerId == P(c) && d.recipientId == v.id))))
                    .OrderBy(v => Math.Abs(c.Score(v.id, P(c)))).ThenBy(v => v.id, StringComparer.Ordinal).FirstOrDefault();
                return swing == null ? null : Bind().With("SWING", swing.id).With("HOH", NpcHoh(c)).Headlining(swing.id);
            },
            weight = (c, b) => 12,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "the-count", surface = StorySurfaces.Conversation, venue = Yard,
                    title = "The Count", summary = "One undecided vote could decide the week.",
                    text = "You have done the maths three times and it keeps coming out the same: this week comes down to {SWING}. Nobody has locked {SWING} in yet. Nobody but you seems to have noticed.",
                    lapse = "let-it-ride",
                    options = new[]
                    {
                        Opt("pitch", "Make the pitch", Medium, Calculated, "Name the nominee you want gone and ask {SWING} to vote with you.",
                            "{SWING} shook on it. Unless something changes, that vote is yours.",
                            Deal(Player, "SWING", DealKind.VoteEvict, Pick))
                            .Picks((c, y) => c.state.nominees.Where(id => id != P(c)))
                            .Checked(50, "SWING", "{SWING} would not commit, and by the evening the Head of Household knew you had asked.",
                                Move(Player, "SWING", -2), Told("HOH", Player, -6, null))
                            .Needs((c, y) => PlayerVotes(c), "You are not voting this week").CostsAction(),
                        Opt("plead", "Ask for their vote", Medium, Warm, "You are on the block. Ask {SWING} to keep you.",
                            "{SWING} gave you their word. On Thursday you find out what it is worth.",
                            Deal(Player, "SWING", DealKind.VoteSave, Player))
                            .Checked(50, "SWING", "{SWING} would not commit, and by the evening the Head of Household knew you had asked.",
                                Move(Player, "SWING", -2), Told("HOH", Player, -6, null))
                            .Needs((c, y) => PlayerNominated(c), "You are not on the block").CostsAction(),
                        Opt("favour", "Call in the favour", Low, Hardball, "{SWING} owes you one. This is when you collect.",
                            "{SWING} owed you, and {SWING} knows it. The vote is yours.",
                            Deal(Player, "SWING", DealKind.VoteEvict, Pick), SpendHook(Player, "SWING"))
                            .Picks((c, y) => c.state.nominees.Where(id => id != P(c)))
                            .Needs((c, y) => c.AtLeast(StoryRules.Bonds) && PlayerVotes(c) && Hooks.Has(c.state, P(c), y.Role("SWING")),
                                "Nothing to call in").CostsAction(),
                        Opt("favour-save", "Call in the favour to stay", Low, Hardball, "{SWING} owes you one. Collect it now.",
                            "{SWING} owed you, and {SWING} knows it. That vote is yours.",
                            Deal(Player, "SWING", DealKind.VoteSave, Player), SpendHook(Player, "SWING"))
                            .Needs((c, y) => c.AtLeast(StoryRules.Bonds) && PlayerNominated(c) && Hooks.Has(c.state, P(c), y.Role("SWING")),
                                "Nothing to call in").CostsAction(),
                        Lapse("let-it-ride", "Let it ride", "Trust the numbers you have.", "You let it ride."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-house-turns

        private static ArcTemplate TheHouseTurns() => new ArcTemplate
        {
            id = "the-house-turns", lane = StoryLanes.Conflict, eyebrow = "House Drama", title = "The House Turns",
            origin = "web:pile_on (grudge-reactions); three holders of forty native",
            // Eviction eve alone is the week's most crowded anchor: from the reach rules the final
            // block is a second chance, and a house turning on someone then is no less true.
            rulesVersion = StoryRules.Grudges, minActive = 5, startAnchors = new[] { StoryAnchors.EvictionEve, StoryAnchors.BlockSet },
            roles = new[] { Role("TARGET"), Role("AGGRESSOR1"), Role("AGGRESSOR2"), Role("AGGRESSOR3") },
            cast = c =>
            {
                foreach (var target in Npcs(c).Where(x => x.id != c.state.hohId)
                             .OrderByDescending(x => Grudges.HoldersAgainst(c.state, x.id, 40).Count).ThenBy(x => x.id, StringComparer.Ordinal))
                {
                    var holders = Grudges.HoldersAgainst(c.state, target.id, 40).Where(id => id != P(c))
                        .OrderByDescending(id => c.Grudge(id, target.id)).ThenBy(id => id, StringComparer.Ordinal).Take(3).ToList();
                    if (holders.Count < 3) return null;
                    return Bind().With("TARGET", target.id).With("AGGRESSOR1", holders[0]).With("AGGRESSOR2", holders[1])
                        .With("AGGRESSOR3", holders[2]).Headlining(target.id);
                }
                return null;
            },
            weight = (c, b) => c.anchor == StoryAnchors.BlockSet && !c.AtLeast(StoryRules.Reach) ? 0 : 15,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "turns", surface = StorySurfaces.Meeting, venue = Living, fallout = StoryLog.Blowup,
                    title = "The House Turns", summary = "The house has turned on one of its own.",
                    text = "{TARGET} walks into the living room and the conversation stops. {AGGRESSOR1}, {AGGRESSOR2} and {AGGRESSOR3} have all had enough of {TARGET}, and tonight they are saying so, one after another.",
                    lapse = "head-down",
                    options = new[]
                    {
                        Opt("join", "Join in", High, Bold, "Add your voice. The numbers are already there.",
                            "You added your voice to theirs. {TARGET} will not forget who was in the room.",
                            Move(Player, "AGGRESSOR1", 3), Move(Player, "AGGRESSOR2", 3), Move(Player, "AGGRESSOR3", 3),
                            Move(Player, "TARGET", -10), Grudge("TARGET", Player, 40, GrudgeCauses.PileOn)),
                        Opt("defend", "Defend them", High, Warm, "Stand next to {TARGET} when nobody else will.",
                            "You stood next to {TARGET} when nobody else would. That kind of thing gets remembered, on both sides.",
                            Move(Player, "TARGET", 10), Receipt("TARGET", Player, StoryReceipts.StoodUpFor),
                            Told("AGGRESSOR1", Player, -3, null), Told("AGGRESSOR2", Player, -3, null), Told("AGGRESSOR3", Player, -3, null),
                            Bond(Player, "TARGET", BondKinds.Confidant)),
                        Lapse("head-down", "Keep your head down", "This is not your fight.", "You kept your head down."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the web's other branching stories

        private static ArcTemplate TheConspiracy() => new ArcTemplate
        {
            id = "the-conspiracy", lane = StoryLanes.Game, eyebrow = "The Conspiracy", title = "The Conspiracy",
            origin = "web:src/systems/branching-story-system.ts:101-179 (conspiracy); grudge trigger native",
            rulesVersion = StoryRules.Grudges, minWeek = 3, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionNight },
            group = "branching", groupCooldownWeeks = 3,
            roles = new[] { Role("RIVAL"), Role("ALLY") },
            cast = c =>
            {
                var rival = Npcs(c).Where(x => (Arc(c, x.id)?.arcType == "rivalry" && Arc(c, x.id).intensity >= 60)
                                               || (c.Grudge(x.id, P(c)) >= 60 && Npcs(c).Any(o => o.id != x.id && c.state.Allied(x.id, o.id))))
                    .OrderByDescending(x => Math.Max(Arc(c, x.id)?.intensity ?? 0, c.Grudge(x.id, P(c))))
                    .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                string ally = rival == null ? null : Warmest(c, x => x.id != rival.id);
                return ally == null ? null : Bind().With("RIVAL", rival.id).With("ALLY", ally).Headlining(rival.id);
            },
            weight = (c, b) => BranchingWeight(c),
            beats = ThreeActs(
                new BeatTemplate
                {
                    id = "discovery", surface = StorySurfaces.Scene, venue = Storage, title = "Act I · Discovery",
                    summary = "You overhear a plan to put you on the block.",
                    text = "You stop outside the storage room. {RIVAL} is inside counting votes, and the name {RIVAL} keeps coming back to is yours. The plan to get you out is already moving, and it has been for days.",
                    lapse = "walk-on",
                    options = new[]
                    {
                        Opt("confront", "Confront them directly", High, Bold, "Walk in and call {RIVAL} out in front of whoever is in there.",
                            "You walked straight in and called it. {RIVAL} was not expecting you.", Move(Player, "RIVAL", -8), Receipt("RIVAL", Player, StoryReceipts.Argued)),
                        Opt("confide", "Confide in an ally", Low, Warm, "Pull {ALLY} aside and tell {ALLY.them} what you heard.",
                            "You told {ALLY} everything you heard.", SocialBonus(2)),
                        Opt("gather-intel", "Gather more intel", Medium, Calculated, "Stay out of sight and hear the whole plan.",
                            "You stayed out of sight and heard the whole thing.", CompBonus(2)),
                        Lapse("walk-on", "Keep walking", "Pretend you heard nothing.", "You kept walking."),
                    },
                },
                new[]
                {
                    new Act { title = "Act II · Deliberation", text = "{RIVAL} knows you know. Everyone in the room is waiting for your next move.",
                        options = new[]
                        {
                            Opt("double-down", "Double down", High, Bold, "Expose the plot to the whole house.", "You told the whole house what {RIVAL} was planning.", Move(Player, "RIVAL", -12)),
                            Opt("offer-truce", "Offer a truce", Medium, Yield, "Propose a ceasefire with {RIVAL}.", "You offered {RIVAL} a way out of it.", Move(Player, "RIVAL", 5), Ease("RIVAL", Player, 20)),
                        } },
                    new Act { title = "Act II · Deliberation", text = "{ALLY} is on your side. The question is what the two of you do about it.",
                        options = new[]
                        {
                            Opt("build-coalition", "Build a counter-alliance", Medium, Calculated, "Work with {ALLY} to rally the people {RIVAL} has not reached.", "You and {ALLY} started building numbers of your own.", SocialBonus(3), Alliance(Player, "ALLY")),
                            Opt("wait-watch", "Wait and watch", Low, Calculated, "Keep it quiet and wait for the right moment.", "You decided to sit on it.", CompBonus(1)),
                        } },
                    new Act { title = "Act II · Deliberation", text = "You heard all of it. Now you know exactly how {RIVAL} means to do it.",
                        options = new[]
                        {
                            Opt("use-info", "Use the information", High, Hardball, "Go to {RIVAL} privately with what you know.", "You let {RIVAL} know what you knew.", Move(Player, "RIVAL", -5)),
                            Opt("share-house", "Share it with the house", Medium, Candid, "Tell everyone and let the house decide.", "You told the house, and let the house decide what to make of {RIVAL}.", SocialBonus(1)),
                        } },
                },
                new[]
                {
                    new Act { title = "Act III · Action", text = "The house knows. {RIVAL} is exposed, and furious.", options = new[]
                    {
                        Opt("rally", "Rally the house", High, Bold, "Call a house meeting and finish it.", "You called the house together and made it stick.", SocialBonus(2), Grudge("RIVAL", Player, 30)),
                        Opt("retreat", "Strategic retreat", Low, Calculated, "Let the dust settle and win competitions.", "You let the dust settle.", CompBonus(3)),
                    } },
                    new Act { title = "Act III · Action", text = "{RIVAL} is listening. The truce is on the table.", options = new[]
                    {
                        Opt("seal-deal", "Seal the deal", Medium, Warm, "Shake on it: a pact of necessity.", "You and {RIVAL} shook on it.", Move(Player, "RIVAL", 8), Deal(Player, "RIVAL", DealKind.SafetyAgreement)),
                        Opt("keep-guard", "Keep your guard up", Low, Calculated, "Accept the truce, and expect the knife.", "You accepted, and you did not believe a word of it.", CompBonus(2)),
                    } },
                    new Act { title = "Act III · Action", text = "You have the numbers. Now use them.", options = new[]
                    {
                        Opt("strike-first", "Strike first", High, Hardball, "Get {RIVAL} out before {RIVAL} gets you.", "You went after {RIVAL} first.", Move(Player, "RIVAL", -15), Grudge("RIVAL", Player, 40)),
                        Opt("play-defense", "Play defense", Low, Calculated, "Focus on winning the veto to protect yourself.", "You put everything into the next competition.", CompBonus(4)),
                    } },
                    new Act { title = "Act III · Action", text = "You have been patient. {RIVAL} still has no idea.", options = new[]
                    {
                        Opt("spring-trap", "Spring the trap", High, Bold, "Reveal everything at the worst possible moment for {RIVAL}.", "You waited for the perfect moment, and took it.", SocialBonus(2)),
                        Opt("let-go", "Let it go", Low, Yield, "Sometimes the smartest move is no move.", "You let it go.", SocialBonus(1), CompBonus(1)),
                    } },
                    new Act { title = "Act III · Action", text = "{RIVAL} knows what you know. What do you want for your silence?", options = new[]
                    {
                        Opt("demand-safety", "Demand safety", High, Hardball, "Tell {RIVAL} to keep you safe or face exposure.", "{RIVAL} agreed to keep you safe, and hates you for it.", Move(Player, "RIVAL", -10), Promise("RIVAL", Player, PromiseKind.Safety), Grudge("RIVAL", Player, 50)),
                        Opt("negotiate", "Negotiate mutual benefit", Medium, Calculated, "Frame it as good for both of you.", "You found something that works for both of you.", Move(Player, "RIVAL", 3), SocialBonus(2), Deal(Player, "RIVAL", DealKind.SafetyAgreement)),
                    } },
                    new Act { title = "Act III · Action", text = "The house knows about the plot. The house is looking at you now.", options = new[]
                    {
                        Opt("take-lead", "Take the lead", High, Bold, "Position yourself as the house's protector.", "You stepped into the space {RIVAL} left.", SocialBonus(3)),
                        Opt("fade-back", "Fade into the background", Low, Calculated, "Let others take the spotlight.", "You let somebody else take the credit.", CompBonus(2)),
                    } },
                }),
        };

        private static ArcTemplate PowerShift() => new ArcTemplate
        {
            id = "power-shift", lane = StoryLanes.Game, eyebrow = "The Power Shift", title = "The Power Shift",
            origin = "web:src/systems/branching-story-system.ts:253-319 (power_shift); the alliance must be real, native; a pair from the reach rules",
            rulesVersion = StoryRules.Grudges, minWeek = 3, minActive = 5, startAnchors = new[] { StoryAnchors.EvictionNight },
            group = "branching", groupCooldownWeeks = 3,
            roles = new[] { Role("LEADER"), Role("MEMBER") },
            cast = c =>
            {
                // The houseguests' own pacts are pairs (NpcAlliances.Form), so an NPC alliance of
                // three never formed and this never came round. Under the reach rules two are a bloc.
                int least = c.AtLeast(StoryRules.Reach) ? 2 : 3;
                foreach (var alliance in c.state.alliances.Where(a => a.active && !a.members.Contains(P(c)) && a.members.Count >= least)
                             .OrderByDescending(a => a.members.Count).ThenBy(a => a.id, StringComparer.Ordinal))
                {
                    var live = alliance.members.Where(id => c.Find(id)?.status == ContestantStatus.Active).OrderBy(id => id, StringComparer.Ordinal).ToList();
                    string leader = live.Where(id => (Arc(c, id)?.intensity ?? 0) >= 60 || c.Grudge(id, P(c)) >= 40)
                        .OrderByDescending(id => Arc(c, id)?.intensity ?? 0).FirstOrDefault();
                    if (leader == null || live.Count < least) continue;
                    return Bind().With("LEADER", leader).With("MEMBER", live.First(id => id != leader)).Headlining(leader);
                }
                if (!c.AtLeast(StoryRules.Reach)) return null;
                // Under the reach rules the discovery can be the bloc forming: somebody with a reason to
                // run the house without you (an intense arc with you, or a grudge), and the houseguest
                // they get on with best. The discovery's options make the pact.
                foreach (var candidate in Npcs(c).Where(x => (Arc(c, x.id)?.intensity ?? 0) >= 60 || c.Grudge(x.id, P(c)) >= 40)
                             .OrderByDescending(x => Arc(c, x.id)?.intensity ?? 0).ThenBy(x => x.id, StringComparer.Ordinal))
                {
                    var partner = Npcs(c).Where(x => x.id != candidate.id && !c.state.Allied(P(c), x.id) && Mutual(c, candidate.id, x.id) >= 10)
                        .OrderByDescending(x => Mutual(c, candidate.id, x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                    if (partner != null) return Bind().With("LEADER", candidate.id).With("MEMBER", partner.id).Headlining(candidate.id);
                }
                return null;
            },
            weight = (c, b) => BranchingWeight(c),
            beats = ThreeActs(
                new BeatTemplate
                {
                    id = "discovery", surface = StorySurfaces.Scene, venue = Yard, title = "Act I · Discovery",
                    summary = "A new alliance is forming, and you are not in it.",
                    text = "Out in the backyard {LEADER} is pulling people together: quiet promises, a count on the fingers, {MEMBER} nodding along. If this group holds, it could run the rest of the game, and you are not in it.",
                    lapse = "stay-out",
                    options = new[]
                    {
                        // Whatever the player does next, the pact is made (a no-op where it already stood) and
                        // they saw who is in it: the read counts it from here.
                        Opt("try-join", "Try to join", High, Warm, "Walk over and pitch yourself as worth having.", "You walked over and pitched yourself.", Move(Player, "LEADER", 3), SocialBonus(2),
                            Alliance("LEADER", "MEMBER"), LeakAlliance("LEADER", "MEMBER", Player)),
                        Opt("oppose", "Form a counter-alliance", Medium, Calculated, "Rally the outsiders against the new bloc.", "You started counting outsiders.", SocialBonus(3),
                            Alliance("LEADER", "MEMBER"), LeakAlliance("LEADER", "MEMBER", Player)),
                        Opt("sabotage", "Sabotage from within", High, Hardball, "Sow discord among the members.", "You started working on the cracks.", Move(Player, "LEADER", -6),
                            Alliance("LEADER", "MEMBER"), LeakAlliance("LEADER", "MEMBER", Player)),
                        Lapse("stay-out", "Stay out of it", "Watch it happen.", "You watched from the patio.",
                            Alliance("LEADER", "MEMBER"), LeakAlliance("LEADER", "MEMBER", Player)),
                    },
                },
                new[]
                {
                    new Act { title = "Act II · Deliberation", text = "{LEADER} is listening, but not sold. What do you bring?", options = new[]
                    {
                        Opt("prove-worth", "Prove your worth", Medium, Calculated, "Offer {LEADER} something only you know.", "You gave {LEADER} something worth having.", Move(Player, "LEADER", 6), SocialBonus(2)),
                        Opt("play-humble", "Play the humble card", Low, Yield, "\"I just want to survive. I'll vote how you say.\"", "You made yourself small and useful.", Move(Player, "LEADER", 4)),
                    } },
                    new Act { title = "Act II · Deliberation", text = "The outsiders are listening. How hard do you push?", options = new[]
                    {
                        Opt("recruit", "Recruit aggressively", High, Bold, "Promise safety and deals to build numbers fast.", "You promised everybody something.", SocialBonus(4)),
                        Opt("quiet-resistance", "Quiet resistance", Low, Calculated, "A small, tight group that moves in the shadows.", "You kept it small and quiet.", SocialBonus(2), CompBonus(2)),
                    } },
                    new Act { title = "Act II · Deliberation", text = "The bloc has cracks. {MEMBER} is one of them.", options = new[]
                    {
                        Opt("plant-doubt", "Plant seeds of doubt", High, Calculated, "Whisper to {MEMBER} that {LEADER} cannot be trusted.", "You made sure {MEMBER} started wondering about {LEADER}.", Move(Player, "LEADER", -8), View("MEMBER", "LEADER", -8)),
                        Opt("create-drama", "Manufacture drama", High, Hardball, "Stage something that forces the members to choose sides.", "You lit a fuse under the new alliance.", SocialBonus(3), View("MEMBER", "LEADER", -5)),
                    } },
                },
                new[]
                {
                    new Act { title = "Act III · Action", text = "You are in. Now make it last.", options = new[]
                    {
                        Opt("cement", "Cement your position", Medium, Warm, "Lock in a final two inside the alliance.", "You and {LEADER} shook on a final two.", Move(Player, "LEADER", 8), Deal(Player, "LEADER", DealKind.FinalTwo)),
                        Opt("double-agent", "Become a double agent", High, Hardball, "Stay in, and feed the other side.", "You stayed in, and started talking to the other side.", CompBonus(3)),
                    } },
                    new Act { title = "Act III · Action", text = "They let you close. What do you do with it?", options = new[]
                    {
                        Opt("plant-mole", "Plant a mole", High, Calculated, "Lead from outside while someone reports back.", "You made sure someone would tell you everything.", SocialBonus(3), CompBonus(1)),
                        Opt("seek-peace", "Seek peace", Low, Yield, "Propose a non-aggression pact.", "You offered {LEADER} a truce between the two groups.", Move(Player, "LEADER", 5), SocialBonus(1), Deal(Player, "LEADER", DealKind.SafetyAgreement)),
                    } },
                    new Act { title = "Act III · Action", text = "Your side has numbers now. Spend them.", options = new[]
                    {
                        Opt("all-out", "Go all out", High, Bold, "Launch a full campaign to break the bloc this week.", "You went after the bloc with everything.", SocialBonus(4)),
                        Opt("lay-low", "Lay low", Low, Calculated, "Your work is done. Wait for the cracks.", "You sat back and waited.", CompBonus(2), SocialBonus(1)),
                    } },
                    new Act { title = "Act III · Action", text = "Quiet has worked so far. One member might be ready to move.", options = new[]
                    {
                        Opt("flip-member", "Flip a key member", High, Calculated, "Go after {MEMBER}, the weakest link.", "{MEMBER} is listening to you now, not {LEADER}.", SocialBonus(3), Move(Player, "LEADER", -5), Move(Player, "MEMBER", 5), View("MEMBER", "LEADER", -10)),
                        Opt("accept-order", "Accept the new order", Low, Yield, "If you can't beat them, survive them.", "You made your peace with it.", CompBonus(3)),
                    } },
                    new Act { title = "Act III · Action", text = "The doubt is spreading. Someone is going to ask where it came from.", options = new[]
                    {
                        Opt("expose-all", "Expose everything", High, Bold, "Say it all out loud and claim you did it for the house.", "You said it all, out loud.", Move(Player, "LEADER", -15)),
                        Opt("deny", "Deny involvement", Medium, Calculated, "\"Who could have done this?\"", "You looked as shocked as anyone.", SocialBonus(2)),
                    } },
                    new Act { title = "Act III · Action", text = "The alliance is at each other's throats. You could stop now.", options = new[]
                    {
                        Opt("fan-flames", "Fan the flames", High, Hardball, "Keep going until it implodes.", "You kept feeding it until it burned.", SocialBonus(4), View("MEMBER", "LEADER", -6)),
                        Opt("step-back", "Step back", Low, Calculated, "You have done enough. Let it play out.", "You stepped back and watched.", CompBonus(2)),
                    } },
                }),
        };

        private static ArcTemplate BetrayalEvidence() => new ArcTemplate
        {
            id = "betrayal-evidence", lane = StoryLanes.Game, eyebrow = "The Betrayal Evidence", title = "The Betrayal Evidence",
            origin = "web:src/systems/branching-story-system.ts:321-387 (betrayal_evidence); grudge trigger native",
            rulesVersion = StoryRules.Grudges, minWeek = 3, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionNight },
            group = "branching", groupCooldownWeeks = 3,
            roles = new[] { Role("SCHEMER") },
            cast = c =>
            {
                var schemer = Npcs(c).Where(x => (Arc(c, x.id)?.arcType == "rivalry" && Arc(c, x.id).intensity >= 60) || c.Grudge(x.id, P(c)) >= 60)
                    .OrderByDescending(x => Math.Max(Arc(c, x.id)?.intensity ?? 0, c.Grudge(x.id, P(c))))
                    .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return schemer == null ? null : Bind().With("SCHEMER", schemer.id).Headlining(schemer.id);
            },
            weight = (c, b) => BranchingWeight(c),
            beats = ThreeActs(
                new BeatTemplate
                {
                    id = "discovery", surface = StorySurfaces.Scene, venue = Living, title = "Act I · Discovery",
                    summary = "You find a page of vote counts in somebody else's handwriting.",
                    text = "Tucked under a couch cushion is a page torn from the HoH room notepad: vote counts, alliances, and a plan to turn on the people who trust {SCHEMER} most, including people in yours. It is in {SCHEMER}'s handwriting.",
                    lapse = "put-back",
                    options = new[]
                    {
                        Opt("memorise", "Memorise it", Low, Calculated, "Learn every line, then put it back.", "You learned every line of it.", CompBonus(2)),
                        Opt("confront-private", "Confront them privately", Medium, Candid, "Show {SCHEMER} what you found and ask for answers.", "You showed {SCHEMER} the page.", Move(Player, "SCHEMER", -5)),
                        Opt("destroy", "Destroy the evidence", High, Calculated, "Get rid of it, and keep what it told you.", "You tore it up and kept every word.", CompBonus(3)),
                        Lapse("put-back", "Put it back", "Pretend you never saw it.", "You put it back exactly where you found it."),
                    },
                },
                new[]
                {
                    new Act { title = "Act II · Deliberation", text = "You know {SCHEMER}'s whole plan, and {SCHEMER} has no idea you do.", options = new[]
                    {
                        Opt("show-allies", "Share it with your alliance", Medium, Candid, "Rally your allies with hard proof.", "You told your people exactly what {SCHEMER} is planning.", SocialBonus(3)),
                        Opt("hold-leverage", "Hold it as leverage", Low, Calculated, "Keep it as a card for later.", "You kept it, for when it is worth most.", CompBonus(3), Hook(Player, "SCHEMER")),
                    } },
                    new Act { title = "Act II · Deliberation", text = "{SCHEMER} has seen the page. The next thing either of you says matters.", options = new[]
                    {
                        Opt("demand-deal", "Demand a deal", High, Hardball, "Make {SCHEMER} deal with you, or you go public.", "{SCHEMER} agreed to leave you off the block, through gritted teeth.", Move(Player, "SCHEMER", -3), SocialBonus(2), Deal("SCHEMER", Player, DealKind.SafetyAgreement)),
                        Opt("read-reaction", "Read their reaction", Low, Calculated, "Watch how {SCHEMER} responds, and adjust.", "You watched {SCHEMER} squirm, and learned a lot.", CompBonus(2), SocialBonus(1)),
                    } },
                    new Act { title = "Act II · Deliberation", text = "The page is gone. What it said is not.", options = new[]
                    {
                        Opt("use-secretly", "Use it quietly", Medium, Calculated, "Steer conversations knowing exactly what {SCHEMER} plans.", "You started steering every conversation.", SocialBonus(3), CompBonus(1)),
                        Opt("disinformation", "Spread disinformation", High, Hardball, "Feed {SCHEMER} a false picture of the house.", "You fed {SCHEMER} a house that does not exist.", SocialBonus(2)),
                    } },
                },
                new[]
                {
                    new Act { title = "Act III · Action", text = "Your allies are with you. What now?", options = new[]
                    {
                        Opt("public-trial", "Put it to the house", High, Bold, "Call everyone together and lay it all out.", "You laid it out in front of everyone. {SCHEMER} had nowhere to hide.", Move(Player, "SCHEMER", -15), Grudge("SCHEMER", Player, 60)),
                        Opt("strategic-leak", "Leak it slowly", Medium, Calculated, "Let it out a little at a time.", "You let it out a line at a time.", SocialBonus(3)),
                    } },
                    new Act { title = "Act III · Action", text = "The card is in your hand. When do you play it?", options = new[]
                    {
                        Opt("drop-at-noms", "Save it for nominations", High, Bold, "Play it at the perfect moment.", "You are saving it for the ceremony.", CompBonus(2)),
                        Opt("subtle-hints", "Drop subtle hints", Low, Calculated, "Let others work it out, with your help.", "You let the house work it out for itself.", SocialBonus(2), CompBonus(1)),
                    } },
                    new Act { title = "Act III · Action", text = "{SCHEMER} is ready to give you something to make this go away.", options = new[]
                    {
                        Opt("extract-safety", "Extract a promise of safety", Medium, Hardball, "Your silence for your safety.", "{SCHEMER} promised to keep you safe.", Move(Player, "SCHEMER", 3), CompBonus(2), Promise("SCHEMER", Player, PromiseKind.Safety)),
                        Opt("self-destruct", "Let them self-destruct", Low, Yield, "{SCHEMER}'s own plan will finish {SCHEMER} off.", "You decided to let {SCHEMER} do the work.", SocialBonus(1), CompBonus(2)),
                    } },
                    new Act { title = "Act III · Action", text = "You know how {SCHEMER} thinks now.", options = new[]
                    {
                        Opt("turn-tables", "Turn the tables", High, Calculated, "Use {SCHEMER}'s own strategy against {SCHEMER.them}.", "You turned {SCHEMER}'s own plan around.", CompBonus(3), SocialBonus(2)),
                        Opt("self-preservation", "Focus on yourself", Low, Calculated, "Forget the drama. Win competitions and stay safe.", "You put the page out of your mind and got to work.", CompBonus(4)),
                    } },
                    new Act { title = "Act III · Action", text = "Everything you know is working. How far do you take it?", options = new[]
                    {
                        Opt("double-cross", "Double-cross them", High, Hardball, "Work with {SCHEMER}, then blindside {SCHEMER.them} on eviction night.", "You smiled at {SCHEMER} and started counting votes against {SCHEMER.them}.", SocialBonus(4)),
                        Opt("stay-clean", "Stay clean", Low, Candid, "Keep your hands clean.", "You kept your hands clean.", SocialBonus(1)),
                    } },
                    new Act { title = "Act III · Action", text = "{SCHEMER} is chasing a house that does not exist.", options = new[]
                    {
                        Opt("master-plan", "Execute your plan", High, Bold, "Every piece is in place. Move.", "You moved, and everything was where you left it.", SocialBonus(3), CompBonus(2)),
                        Opt("watch-shadows", "Watch from the shadows", Low, Calculated, "You planted the seeds. Watch them grow.", "You sat back and watched it work.", CompBonus(3)),
                    } },
                }),
        };

        private static ArcTemplate SecretAllianceOffer() => new ArcTemplate
        {
            id = "secret-alliance-offer", lane = StoryLanes.Game, eyebrow = "The Secret Alliance Offer", title = "The Secret Alliance Offer",
            origin = "web:src/systems/branching-story-system.ts:389-455 (secret_alliance); the alliances are real, native",
            rulesVersion = StoryRules.Grudges, minWeek = 3, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionNight },
            group = "branching", groupCooldownWeeks = 3,
            roles = new[] { Role("OFFERER"), Optional("ALLY") },
            cast = c =>
            {
                var offerer = Npcs(c).Where(x => (Arc(c, x.id)?.intensity ?? 0) >= 60 && !c.state.Allied(P(c), x.id))
                    .OrderByDescending(x => Arc(c, x.id).intensity).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                if (offerer == null) return null;
                return Bind().With("OFFERER", offerer.id).With("ALLY", Warmest(c, x => x.id != offerer.id)).Headlining(offerer.id);
            },
            weight = (c, b) => BranchingWeight(c),
            beats = ThreeActs(
                new BeatTemplate
                {
                    id = "discovery", surface = StorySurfaces.Approach, venue = Storage, title = "Act I · Discovery",
                    summary = "A houseguest proposes a secret alliance, just the two of you.",
                    text = "{OFFERER} corners you in the storage room with a proposition. \"I know we haven't always seen eye to eye, but what if we worked together? Just us. Nobody else knows.\"",
                    lapse = "not-now",
                    options = new[]
                    {
                        Opt("intrigued", "I'm intrigued", Low, Warm, "\"Tell me more. What exactly are you proposing?\"", "You told {OFFERER} to keep talking.", Move(Player, "OFFERER", 5)),
                        Opt("suspicious", "I'm suspicious", Medium, Calculated, "\"Why me? Why now? What's your angle?\"", "You asked {OFFERER} what the angle was.", CompBonus(2)),
                        Opt("hard-pass", "Hard pass", High, Candid, "\"I don't think so. I'm good where I am.\"", "You turned {OFFERER} down flat.", Move(Player, "OFFERER", -5)),
                        Lapse("not-now", "Not now", "\"Let me think about it.\"", "You said you would think about it."),
                    },
                },
                new[]
                {
                    new Act { title = "Act II · Deliberation", text = "{OFFERER} lays it out, and it is a good offer.", options = new[]
                    {
                        Opt("accept-terms", "Accept the alliance", Medium, Warm, "Shake on it: you and {OFFERER} against the house.", "You and {OFFERER} shook on it.", Move(Player, "OFFERER", 10), SocialBonus(2), Alliance(Player, "OFFERER")),
                        Opt("negotiate-terms", "Negotiate terms", Low, Calculated, "\"I want guarantees before I commit.\"", "You asked for guarantees.", Move(Player, "OFFERER", 3), CompBonus(2)),
                    } },
                    new Act { title = "Act II · Deliberation", text = "{OFFERER} did not expect the questions.", options = new[]
                    {
                        Opt("demand-proof", "Demand proof of loyalty", Medium, Hardball, "\"Do something for me first.\"", "You told {OFFERER} to prove it.", CompBonus(3)),
                        Opt("accept-cautiously", "Accept, cautiously", Low, Calculated, "\"Okay. But if you play me, it's over.\"", "You said yes, with your eyes open.", Move(Player, "OFFERER", 5), SocialBonus(1), Alliance(Player, "OFFERER")),
                    } },
                    new Act { title = "Act II · Deliberation", text = "{OFFERER} takes the refusal badly.", options = new[]
                    {
                        Opt("walk-away", "Walk away", Low, Candid, "You have said your piece.", "You walked away.", Move(Player, "OFFERER", -8)),
                        Opt("counter-truce", "Offer a truce instead", Medium, Yield, "\"No alliance, but no war either.\"", "You offered a truce instead.", Move(Player, "OFFERER", 2), SocialBonus(2)),
                    } },
                },
                new[]
                {
                    new Act { title = "Act III · Action", text = "The alliance is real. How do you run it?", options = new[]
                    {
                        Opt("create-signal", "Create a signal", Low, Playful, "A secret sign, so you can talk in public.", "You and {OFFERER} settled on a signal nobody else would notice.", Move(Player, "OFFERER", 5), SocialBonus(3)),
                        Opt("first-target", "Choose a first target", High, Hardball, "Decide right now who goes first.", "You and {OFFERER} picked a first target.", CompBonus(3)),
                    } },
                    new Act { title = "Act III · Action", text = "{OFFERER} is waiting on your answer.", options = new[]
                    {
                        Opt("trial-period", "A trial period", Low, Calculated, "\"One week. Let's see how it goes.\"", "You agreed to one week.", Move(Player, "OFFERER", 3), SocialBonus(1)),
                        Opt("all-in", "Go all in", Medium, Warm, "Commit and start coordinating votes.", "You went all in with {OFFERER}.", Move(Player, "OFFERER", 8), SocialBonus(3), Alliance(Player, "OFFERER")),
                    } },
                    new Act { title = "Act III · Action", text = "You asked for proof. {OFFERER} is waiting to hear what would count.", options = new[]
                    {
                        Opt("set-test", "Set up a test", Medium, Calculated, "A scenario to find out whether {OFFERER} is genuine.", "You set a quiet test.", CompBonus(2), SocialBonus(1)),
                        Opt("trust-gut", "Trust your gut", Low, Warm, "Something feels right. Go with it.", "You went with your gut, and with {OFFERER}.", Move(Player, "OFFERER", 7), SocialBonus(2), Alliance(Player, "OFFERER")),
                    } },
                    new Act { title = "Act III · Action", text = "You are in, with one eye open.", options = new[]
                    {
                        Opt("guard-up", "Keep your guard up", Low, Calculated, "Extend the olive branch, and keep watching.", "You kept watching.", Move(Player, "OFFERER", 4), SocialBonus(1)),
                        Opt("spy", "Watch their every move", Medium, Calculated, "Accept, and watch everything {OFFERER} does.", "You started keeping count of everything {OFFERER} does.", CompBonus(3)),
                    } },
                    new Act { title = "Act III · Action", text = "{OFFERER} has not forgotten the refusal.", options = new[]
                    {
                        Opt("burn-bridge", "Burn the bridge", High, Bold, "Tell the house about {OFFERER}'s offer.", "You told the house exactly what {OFFERER} offered you.", Move(Player, "OFFERER", -15), Grudge("OFFERER", Player, 60)),
                        Opt("keep-door-open", "Keep the door open", Low, Yield, "\"Not now, maybe later. No hard feelings.\"", "You left the door open.", Move(Player, "OFFERER", -2), SocialBonus(1)),
                    } },
                    new Act { title = "Act III · Action", text = "A truce is on the table. It could be more.", options = new[]
                    {
                        Opt("propose-bigger", "Propose something bigger", High, Bold, "\"Forget a truce. Let's make it three.\"", "You proposed a three-way alliance.", SocialBonus(4), Move(Player, "OFFERER", 6), Alliance(Player, "OFFERER", "ALLY")),
                        Opt("neutral-zone", "Agree to a neutral zone", Low, Yield, "Agree not to target each other, and nothing more.", "You agreed to leave each other alone.", Move(Player, "OFFERER", 3), Deal(Player, "OFFERER", DealKind.SafetyAgreement)),
                    } },
                }),
        };

        // ---------------------------------------------------------------- the reckonings (21 G2)

        /// <summary>A broken word the player broke, raised by the houseguest it was broken to. The web always plays it.</summary>
        private static ArcTemplate TheReckoning() => new ArcTemplate
        {
            id = "the-reckoning", lane = StoryLanes.Moment, eyebrow = "The Reckoning", title = "We Need to Talk",
            origin = "web:BetrayalFlagContext.tsx:31-78 and the confront menu (npc-social-behavior.ts:759-890), persisted per 21 D-D",
            rulesVersion = StoryRules.Grudges, oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1, urgent = true,
            roles = new[] { Role("THEM") },
            cast = c => c.talkingTo == null ? null : Bind().With("THEM", c.talkingTo).Headlining(c.talkingTo),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "they-raise-it", surface = StorySurfaces.Conversation, title = "We Need to Talk",
                    summary = "A houseguest you broke your word to wants to talk about it.",
                    text = "{THEM} has been waiting for this. \"You gave me your word, and you broke it. I want to hear you explain that.\"",
                    lapse = "deflect",
                    options = new[]
                    {
                        Opt("apologize", "Apologize", Low, Yield, "Own it, and mean it.", "You apologised, and {THEM} could see you meant it.",
                            Move(Player, "THEM", 10), Ease("THEM", Player, 15)),
                        Opt("deflect", "Deflect", Medium, Calculated, "It was the game. You would have done the same.", "You talked your way round it. {THEM} was not convinced.",
                            Move(Player, "THEM", -2)),
                        Opt("escalate", "Escalate", High, Bold, "Turn it back on {THEM}.", "It turned into a shouting match, and now the house knows about it too.",
                            Move(Player, "THEM", -15), Grudge("THEM", Player, 20), Receipt("THEM", Player, StoryReceipts.Argued)),
                    },
                },
            },
        };

        /// <summary>A broken word the player was on the wrong end of: the player raises it.</summary>
        private static ArcTemplate TheReckoningYours() => new ArcTemplate
        {
            id = "the-reckoning-yours", lane = StoryLanes.Moment, eyebrow = "The Reckoning", title = "You Broke Your Word",
            origin = "web:BetrayalFlagContext.tsx:31-78, told from the wronged side; the hook native (M5)",
            rulesVersion = StoryRules.Grudges, oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1, urgent = true,
            roles = new[] { Role("THEM") },
            cast = c => c.talkingTo == null ? null : Bind().With("THEM", c.talkingTo).Headlining(c.talkingTo),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "you-raise-it", surface = StorySurfaces.Conversation, title = "You Broke Your Word",
                    summary = "You finally have a houseguest who broke their word to you on their own.",
                    text = "It is the first time you have had {THEM} to yourself since {THEM} went back on the deal. {THEM} knows exactly why you are standing there.",
                    lapse = "let-go",
                    options = new[]
                    {
                        Opt("call-out", "Call it out", Medium, Candid, "Make {THEM} own it.", "{THEM} owned it, and promised to make it right this week.",
                            Move(Player, "THEM", 5), Promise("THEM", Player, PromiseKind.Safety))
                            .Checked(55, "THEM", "{THEM} would not own it, and resented being asked.", Move(Player, "THEM", -8), Grudge("THEM", Player, 30)),
                        Opt("let-go", "Let it go", Low, Yield, "Let it go. You will remember it anyway.", "You let it go. You did not forget it.",
                            Move(Player, "THEM", -5), Receipt(Player, "THEM", StoryReceipts.SoldOut)),
                        Opt("use-it", "Use it", Medium, Calculated, "Say nothing now, and hold it over {THEM} later.", "You said nothing, and {THEM} knows you are keeping count.",
                            Hook(Player, "THEM"), Move(Player, "THEM", -2))
                            .LockUnless("Strategic").From(StoryRules.Bonds),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-agenda (21 G3)

        private static ArcTemplate TheAgenda() => new ArcTemplate
        {
            id = "the-agenda", lane = StoryLanes.Game, eyebrow = "Head of Household", title = "The Agenda",
            origin = "web:npc-social-behavior.ts inferIntent (assess_threat, reveal 0.20); the options native",
            // The house has no free time between the Head of Household competition and the
            // nominations, so the HoH comes to you: the nomination interview as an approach.
            rulesVersion = StoryRules.Grudges, oncePerHeadliner = false, cooldownWeeks = 1,
            startAnchors = new[] { StoryAnchors.HohCrowned },
            roles = new[] { Role("HOH") },
            cast = c =>
            {
                string hoh = NpcHoh(c);
                if (hoh == null || c.state.nominees.Count > 0) return null;
                return StoryPeople.Intent(c.state, hoh) == StoryPeople.Intents.AssessThreat ? Bind().With("HOH", hoh).Headlining(hoh) : null;
            },
            weight = (c, b) => 15,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "sizing-up", surface = StorySurfaces.Approach, venue = HohRoom, closes = StoryAnchors.NomsSet,
                    title = "The Agenda", summary = "The Head of Household calls you up and asks a lot of questions.",
                    text = "{HOH} calls you up to the HoH room and is very friendly about it, and asks a lot of questions: who you are close to, what you think of the week, what you would do in {HOH.their} shoes.",
                    lapse = "keep-light",
                    options = new[]
                    {
                        Opt("read-them", "Read them", Low, Calculated, "Work out what this conversation is really for.",
                            "It clicked halfway through: this is a nomination interview, and you are the one being interviewed.",
                            Memory(Player, "HOH", "The Head of Household was sizing me up for the block."))
                            .Checked(StoryPeople.Intents.RevealChance(StoryPeople.Intents.AssessThreat), "HOH", "You could not get a read on {HOH}.")
                            .LockUnless("Intuitive", "Analytical"),
                        Opt("talk-down", "Talk yourself down", Low, Yield, "Make yourself small: no threat, no plan.", "You played it small. {HOH} seemed to relax.",
                            Move(Player, "HOH", 3)),
                        Opt("pitch-bigger", "Pitch a bigger target", Medium, Calculated, "Point {HOH} at somebody who is more of a threat than you.",
                            "{HOH} went quiet and thoughtful, and the quiet was about somebody else.",
                            View("HOH", Pick, -10))
                            .Picks((c, y) => Npcs(c).Where(x => x.id != y.Role("HOH")).Select(x => x.id))
                            .Checked(45, "HOH", "{HOH} did not like being told who to put up.", Move(Player, "HOH", -3)),
                        Opt("promise-vote", "Promise your vote", Medium, Warm, "Offer {HOH} a safety pact: you keep {HOH.them} safe, {HOH} keeps you safe.",
                            "You and {HOH} agreed to keep each other off the block.", Deal(Player, "HOH", DealKind.SafetyAgreement)),
                        Lapse("keep-light", "Keep it light", "Answer the questions and give nothing away.", "You kept it light."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- caught-talking (21 §5)

        private static ArcTemplate CaughtTalking() => new ArcTemplate
        {
            id = "caught-talking", lane = StoryLanes.Moment, eyebrow = "Gossip", title = "Behind Your Back",
            origin = "web:npc-social-behavior.ts:759-890 (gossip-discovered: Confront -15 / Slide -5 / Gossip back -10)",
            rulesVersion = StoryRules.Grudges, oncePerHeadliner = false, cooldownWeeks = 2,
            roles = new[] { Role("GOSSIP"), Role("TELLER") },
            cast = c =>
            {
                if (c.talkingTo == null) return null;
                string teller = c.about != null && c.about != P(c) ? c.about : Warmest(c, x => x.id != c.talkingTo);
                return teller == null ? null : Bind().With("GOSSIP", c.talkingTo).With("TELLER", teller).Headlining(c.talkingTo);
            },
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "word-gets-back", surface = StorySurfaces.Approach, venue = Hallway,
                    title = "Behind Your Back", summary = "A houseguest has been talking about you behind your back.",
                    text = "{TELLER} pulls you aside. \"I probably shouldn't tell you this, but {GOSSIP} has been talking about you. A lot.\"",
                    lapse = "let-slide",
                    options = new[]
                    {
                        Opt("confront", "Confront them", High, Bold, "Go straight to {GOSSIP}.", "You went straight to {GOSSIP}. It got loud.",
                            Move(Player, "GOSSIP", -15), Receipt("GOSSIP", Player, StoryReceipts.Argued)),
                        Opt("let-slide", "Let it slide", Low, Yield, "Rise above it.", "You let it slide.", Move(Player, "GOSSIP", -5)),
                        Opt("gossip-back", "Gossip back", High, Hardball, "Two can play at that.", "You gave {GOSSIP} a taste of it, and {GOSSIP} knows exactly where it came from.",
                            Move(Player, "GOSSIP", -10), Grudge("GOSSIP", Player, 20))
                            .LockUnless("Sneaky", "Manipulative", "Deceptive"),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- final-plea (21 §5)

        private static ArcTemplate FinalPlea() => new ArcTemplate
        {
            id = "final-plea", lane = StoryLanes.Game, eyebrow = "The Block", title = "Final Plea",
            origin = "web:the Final Plea room act (roomActions.ts, +8) and phase-event-system.ts:290-324; the deal native (21 D-G)",
            rulesVersion = StoryRules.Grudges, oncePerHeadliner = false, cooldownWeeks = 1, urgent = true,
            startAnchors = new[] { StoryAnchors.EvictionEve },
            roles = new[] { Role("VOTER"), Optional("HOH") },
            cast = c =>
            {
                if (!PlayerNominated(c)) return null;
                var voter = EpisodeEngine.Voters(c.state).Where(v => !v.isPlayer)
                    .OrderBy(v => Math.Abs(c.Score(v.id, P(c)))).ThenBy(v => v.id, StringComparer.Ordinal).FirstOrDefault();
                return voter == null ? null : Bind().With("VOTER", voter.id).With("HOH", NpcHoh(c)).Headlining(voter.id);
            },
            weight = (c, b) => 25,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "plea", surface = StorySurfaces.Conversation, venue = Bedroom,
                    title = "Final Plea", summary = "Your last chance to change a vote before the house decides.",
                    text = "Tonight the house decides. {VOTER} is the vote you are least sure of, and {VOTER} has agreed to hear you out.",
                    lapse = "say-nothing",
                    options = new[]
                    {
                        Opt("make-case", "Make your case", Medium, Candid, "Tell {VOTER} why you should stay.", "You made your case. {VOTER} listened to every word.",
                            Move(Player, "VOTER", 8)),
                        Opt("offer-deal", "Offer a deal", Medium, Calculated, "Your vote next week for {VOTER}'s vote tonight.", "{VOTER} shook on it: {VOTER}'s vote to keep you tonight.",
                            Deal(Player, "VOTER", DealKind.VoteSave, Player), Move(Player, "VOTER", 3))
                            .Checked(50, "VOTER", "{VOTER} would not be bought.", Move(Player, "VOTER", -4)),
                        Opt("call-out-hoh", "Call out the HoH", High, Bold, "Say out loud what {HOH} did to put you here.",
                            "You called {HOH} out in front of the house. Some of them respected it.", Move(Player, "HOH", -10), Grudge("HOH", Player, 30))
                            .Extra((c, y) => Npcs(c).Where(x => x.id != y.Role("HOH") && Personality.Of(x).Honest >= 1)
                                .Select(x => Receipt("@" + x.id, Player, StoryReceipts.HeardOut)))
                            .LockUnless("Confrontational").Needs((c, y) => y.Role("HOH") != null, "The Head of Household is you"),
                        Lapse("say-nothing", "Say nothing", "Let your game speak for itself.", "You let your game speak for itself."),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- vent-session (21 §5)

        private static ArcTemplate VentSession() => new ArcTemplate
        {
            id = "vent-session", lane = StoryLanes.Conflict, eyebrow = "Venting", title = "Vent Session",
            origin = "web:src/systems/dialogue-tree-engine.ts:121-147 (vent_about tree); the target agreement native",
            rulesVersion = StoryRules.Grudges, oncePerHeadliner = false, cooldownWeeks = 1,
            conversationTopics = new[] { EpisodeCommandKind.VentAbout },
            roles = new[] { Role("LISTENER"), Role("TARGET") },
            cast = c =>
            {
                if (c.talkingTo == null || c.about == null || c.about == P(c) || c.Find(c.about)?.isPlayer != false) return null;
                return Bind().With("LISTENER", c.talkingTo).With("TARGET", c.about).Headlining(c.talkingTo);
            },
            weight = (c, b) => c.Score(b.Get("LISTENER"), b.Get("TARGET")) <= 10 ? 30 : 10,
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "vent", surface = StorySurfaces.Conversation, title = "Vent Session",
                    summary = "Venting about a houseguest turns into something more.",
                    text = "\"Ugh, don't even get me started.\" {LISTENER} leans in. \"I've noticed some sketchy things from {TARGET} too.\"",
                    lapse = "drop-it",
                    options = new[]
                    {
                        Opt("cant-trust", "They can't be trusted", High, Bold, "Double down on the criticism.", "You doubled down, and {LISTENER} agreed with every word.",
                            Move(Player, "LISTENER", 4), SocialBonus(1)),
                        Opt("overreacting", "Maybe I'm overreacting", Low, Yield, "Walk it back a little.", "You walked it back.",
                            Move(Player, "LISTENER", 2)),
                        Opt("do-something", "Should we do something?", Medium, Calculated, "Propose doing something about {TARGET}, together.",
                            "{LISTENER} lowered {LISTENER.their} voice. \"Between you and me, I've been thinking the same.\"",
                            Move(Player, "LISTENER", 5), SocialBonus(2)).Then("the-plan"),
                        Lapse("drop-it", "Drop it", "Change the subject.", "You dropped it."),
                    },
                },
                new BeatTemplate
                {
                    id = "the-plan", surface = StorySurfaces.Conversation, title = "Between You and Me",
                    summary = "Two houseguests agree to do something about a third.",
                    text = "\"I hear you,\" {LISTENER} says. \"So what do we do about {TARGET}?\"",
                    lapse = "leave-it",
                    options = new[]
                    {
                        Opt("our-secret", "Our secret", Low, Warm, "Keep it between the two of you, and agree on {TARGET}.",
                            "You and {LISTENER} agreed: {TARGET} is the target, and nobody else needs to know.",
                            Move(Player, "LISTENER", 3), Deal(Player, "LISTENER", DealKind.TargetAgreement, "TARGET")),
                        Opt("rally-others", "Let's rally others", High, Bold, "Widen the circle against {TARGET}.",
                            "You and {LISTENER} started widening the circle. {TARGET} will feel it by the weekend.",
                            Move(Player, "LISTENER", 2), SocialBonus(1), Grudge("LISTENER", "TARGET", 30, GrudgeCauses.PileOn)),
                        Lapse("leave-it", "Leave it there", "Say no more.", "You left it there."),
                    },
                },
            },
        };
    }
}
