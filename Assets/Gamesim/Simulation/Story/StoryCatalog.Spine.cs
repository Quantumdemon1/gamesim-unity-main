using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// M1, the spine: the arcs that prove the loop - a beat at an anchor, an answer with odds, a
    /// consequence something reads - on the web's own content.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> Spine()
        {
            yield return AfterTheComp();
            yield return TheConfession();
            yield return TheMorningAfter();
        }

        // ---------------------------------------------------------------- after-the-comp

        /// <summary>
        /// Lobbying the new Head of Household before nominations: the web's post-HoH beat (0.7),
        /// with the plan's pitch aimed at a named rival or comp threat. Once staging ships,
        /// <c>hoh-room</c> takes this over whenever the player is on good enough terms to be let up.
        /// </summary>
        private static ArcTemplate AfterTheComp() => new ArcTemplate
        {
            id = "after-the-comp", lane = StoryLanes.Moment, eyebrow = "Head of Household", title = "After the Competition",
            origin = "web:src/systems/phase-event-system.ts:44-76 (post-HoH, 0.7); pitch target native",
            rulesVersion = StoryRules.Spine, startAnchors = new[] { StoryAnchors.HohCrowned },
            oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1,
            roles = new[] { Role("HOH"), Optional("RIVAL"), Optional("THREAT") },
            cast = c =>
            {
                // Where the strategy windows play, a word with the new Head of Household is their
                // lobby - with its approaches, odds and answers - and one conversation needs one way in.
                if (StrategyRules.Apply(c.state)) return null;
                string hoh = NpcHoh(c);
                if (hoh == null) return null;
                string rival = Coldest(c, x => x.id != hoh);
                string threat = Npcs(c).Where(x => x.id != hoh && x.id != rival)
                    .OrderByDescending(x => x.hohWins + x.vetoWins).ThenByDescending(x => x.stats.competition)
                    .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return Bind().With("HOH", hoh).With("RIVAL", rival).With("THREAT", threat).Headlining(hoh);
            },
            // Staging's HoH room is the fuller version of this moment; it takes over when it can.
            weight = (c, b) => c.AtLeast(StoryRules.Staging) && c.Score(P(c), b.Get("HOH")) >= 0 ? 0 : Web(0.7),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "lobby", surface = StorySurfaces.Approach, venue = Kitchen, closes = StoryAnchors.NomsSet,
                    title = "After the Competition",
                    summary = "The new Head of Household is pulling people aside before nominations.",
                    text = "{HOH} has just won Head of Household, and the house is lining up to say congratulations. {HOH} is pulling people aside one at a time. This might be your chance to get into {HOH.their} ear before nominations.",
                    alternates = new[]
                    {
                        "The key is barely in {HOH}'s hand and the HoH room already has a queue outside it. Everyone wants a word before nominations. So could you.",
                        "{HOH} won, and the house has gone very friendly all of a sudden. Nominations are coming, and {HOH} is listening to anyone who gets there first.",
                    },
                    lapse = "lay-low",
                    options = new[]
                    {
                        Opt("congratulate", "Congratulate them", Low, Warm,
                            "Play it safe with genuine praise. {HOH} will remember who was glad for {HOH.them}.",
                            "You found {HOH} in the kitchen and offered warm congratulations. A small thing, but it landed.",
                            Move(Player, "HOH", 3), Receipt("HOH", Player, StoryReceipts.HeardOut)),
                        Opt("pitch-rival", "Pitch your rival", High, Calculated,
                            "Steer {HOH} toward {RIVAL} before the names are said.",
                            "You laid out your case against {RIVAL}. {HOH} went quiet and thoughtful, and the quiet was about {RIVAL}.",
                            Move(Player, "HOH", -2), View("HOH", "RIVAL", -12))
                            .Checked(50, "HOH",
                                "You made your case against {RIVAL}, and by dinner {RIVAL} knew exactly who had made it.",
                                Move(Player, "HOH", -2), Told("RIVAL", Player, -8, StoryReceipts.SoldOut), Grudge("RIVAL", Player, 40))
                            .Bonus("Strategic", "Manipulative", "Charming")
                            .Needs((c, y) => y.Role("RIVAL") != null, "There is nobody to pitch"),
                        Opt("pitch-threat", "Pitch the comp threat", High, Calculated,
                            "Tell {HOH} what everyone already knows: {THREAT} wins things.",
                            "You reminded {HOH} how many competitions {THREAT} could still win. {HOH} did the maths.",
                            Move(Player, "HOH", -2), View("HOH", "THREAT", -12))
                            .Checked(50, "HOH",
                                "You pointed {HOH} at {THREAT}, and {THREAT} heard about it before the ceremony.",
                                Move(Player, "HOH", -2), Told("THREAT", Player, -8, StoryReceipts.SoldOut), Grudge("THREAT", Player, 40))
                            .Bonus("Strategic", "Analytical", "Competitive")
                            .Needs((c, y) => y.Role("THREAT") != null, "There is nobody to pitch"),
                        Lapse("lay-low", "Lay low", "Don't draw attention to yourself. You'll have more in the tank later.",
                            "You slipped away to the backyard while the house queued up to kiss the ring.",
                            SocialBonus(1)),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- the-confession

        /// <summary>
        /// The web's branching "The Confession" (friendship 70+): your ally has been offered a final
        /// two by somebody else. The offerer is a real houseguest the ally is actually tied to, never a
        /// name the text invents. Its payoffs reach the promise, deal, alliance and bond systems.
        /// </summary>
        private static ArcTemplate TheConfession() => new ArcTemplate
        {
            id = "the-confession", lane = StoryLanes.Game, eyebrow = "The Confession", title = "The Confession",
            origin = "web:src/systems/branching-story-system.ts:181-251 (confession); final-two and bond payoffs native",
            rulesVersion = StoryRules.Spine, minWeek = 3, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionNight },
            group = "branching", groupCooldownWeeks = 3,
            roles = new[] { Role("ALLY"), Role("OFFERER") },
            cast = c =>
            {
                var ally = Npcs(c).Where(x => (Arc(c, x.id)?.arcType == "friendship" && Arc(c, x.id).intensity >= 70)
                                              || (c.state.Allied(P(c), x.id) && Mutual(c, P(c), x.id) >= 30))
                    .OrderByDescending(x => Arc(c, x.id)?.intensity ?? 0).ThenByDescending(x => Mutual(c, P(c), x.id))
                    .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                if (ally == null) return null;
                // Somebody the ally is really working with, else the ally's closest other friend.
                var offerer = Npcs(c).Where(x => x.id != ally.id)
                    .OrderByDescending(x => c.state.Allied(ally.id, x.id) || c.state.deals.Any(d => DealStatus.Binds(d.status)
                        && ((d.proposerId == ally.id && d.recipientId == x.id) || (d.proposerId == x.id && d.recipientId == ally.id))))
                    .ThenByDescending(x => c.Score(ally.id, x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return offerer == null ? null : Bind().With("ALLY", ally.id).With("OFFERER", offerer.id).Headlining(ally.id);
            },
            weight = (c, b) => BranchingWeight(c),
            beats = ThreeActs(
                new BeatTemplate
                {
                    id = "discovery", surface = StorySurfaces.Approach, venue = Storage,
                    title = "Act I · Discovery",
                    summary = "An ally has something to confess.",
                    text = "Late at night, {ALLY} pulls you into the storage room, eyes on the door the whole time, and finally comes out with it: {OFFERER} has offered {ALLY.them} a final two, and {ALLY} is thinking about taking it.",
                    lapse = "change-subject",
                    options = new[]
                    {
                        Opt("understanding", "Be understanding", Low, Warm, "\"I get it. This game is brutal. Tell me more.\"",
                            "You let {ALLY} talk it through without flinching.", Move(Player, "ALLY", 5)),
                        Opt("hurt", "Express hurt", Medium, Candid, "\"After everything? I thought we were ride-or-die.\"",
                            "You told {ALLY} it stung, and you let {ALLY.them} see that it did.", Move(Player, "ALLY", -3)),
                        Opt("strategic", "Think strategically", Medium, Calculated, "\"Who else knows? We can use this.\"",
                            "You went straight to what the offer is worth to you.", CompBonus(2)),
                        Lapse("change-subject", "Change the subject", "Not tonight.", "You changed the subject, and {ALLY} let you."),
                    },
                },
                new[]
                {
                    new Act { title = "Act II · Deliberation", text = "{ALLY} is still waiting to hear where this leaves the two of you. The house is asleep, and nobody else knows about {OFFERER}'s offer. How do you play it?",
                        options = new[]
                        {
                            Opt("reaffirm", "Reaffirm your bond", Low, Warm, "Remind {ALLY} why the two of you are stronger than any deal.", "You reminded {ALLY} what the two of you have built.", Move(Player, "ALLY", 8)),
                            Opt("match-offer", "Make a counter-offer", Medium, Calculated, "Top {OFFERER}'s offer and keep {ALLY} with you.", "You put a better offer on the table than {OFFERER} did.", Move(Player, "ALLY", 5), SocialBonus(2)),
                        } },
                    new Act { title = "Act II · Deliberation", text = "{ALLY} did not expect it to land this hard. The hurt is out in the open now. What do you do with it?",
                        options = new[]
                        {
                            Opt("ultimatum", "Give an ultimatum", High, Hardball, "\"It's me or them. Choose now.\"", "You made {ALLY} choose, there and then.", Move(Player, "ALLY", -5)),
                            Opt("forgive", "Forgive and move on", Low, Warm, "\"Let's forget it and focus on winning.\"", "You let it go and turned the talk back to the game.", Move(Player, "ALLY", 3), SocialBonus(1)),
                        } },
                    new Act { title = "Act II · Deliberation", text = "Now you know something {OFFERER} doesn't know you know. {ALLY} is watching you work it out.",
                        options = new[]
                        {
                            Opt("weaponize", "Use it against them", High, Calculated, "Turn {OFFERER}'s offer against {OFFERER}.", "You started working out how to spend what you know about {OFFERER}.", CompBonus(3)),
                            Opt("protect-secret", "Protect the secret", Low, Warm, "Keep {ALLY}'s confidence and earn deeper trust.", "You promised {ALLY} it would go no further.", Move(Player, "ALLY", 10)),
                        } },
                },
                new[]
                {
                    new Act { title = "Act III · Action", text = "{ALLY} is closer than ever. Make it count.",
                        options = new[]
                        {
                            Opt("loyalty-pact", "Swear a loyalty pact", Medium, Warm, "Make it official: ride or die, final two.",
                                "You and {ALLY} shook on it: final two, whatever it takes.",
                                Move(Player, "ALLY", 12), Promise("ALLY", Player, PromiseKind.FinalTwo), Bond(Player, "ALLY", BondKinds.RideOrDie)),
                            Opt("keep-options", "Keep your options open", Low, Calculated, "Agree on the surface, and keep your hands free.", "You said all the right things and promised nothing.", SocialBonus(2), CompBonus(1)),
                        } },
                    new Act { title = "Act III · Action", text = "Your offer is on the table. {ALLY} wants to know how serious it is.",
                        options = new[]
                        {
                            Opt("sweeten", "Sweeten the deal", Medium, Warm, "Offer {ALLY} safety this week as a show of good faith.", "You offered {ALLY} a safety pact, starting now.", Move(Player, "ALLY", 6), SocialBonus(2), Deal(Player, "ALLY", DealKind.SafetyAgreement)),
                            Opt("test-loyalty", "Test their loyalty", High, Calculated, "Feed {ALLY} something false and see whether it reaches {OFFERER}.", "You planted something false and waited to see where it went.", CompBonus(2)),
                        } },
                    new Act { title = "Act III · Action", text = "{ALLY} has heard your ultimatum. Now you find out what it bought you.",
                        options = new[]
                        {
                            Opt("accept-fate", "Accept the outcome", Low, Yield, "\"Whatever happens, I respect your game.\"", "You told {ALLY} you respected the game, whatever came of it.", Move(Player, "ALLY", 3), SocialBonus(1)),
                            Opt("cut-loose", "Cut them loose", High, Hardball, "Start looking for a new ride-or-die.", "You walked away from {ALLY}, and from the alliance.", Move(Player, "ALLY", -8), SocialBonus(3), AllianceEnd(Player, "ALLY")),
                        } },
                    new Act { title = "Act III · Action", text = "You forgave {ALLY}. {OFFERER} is the one who tried to take {ALLY.them}.",
                        options = new[]
                        {
                            Opt("expose-schemer", "Expose the schemer", High, Bold, "Tell {ALLY} exactly what kind of player {OFFERER} is.", "You made sure {ALLY} saw {OFFERER} for what {OFFERER.they} did.", SocialBonus(2), View("ALLY", "OFFERER", -10), Grudge("OFFERER", Player, 40)),
                            Opt("quiet-revenge", "Plot quiet revenge", Medium, Calculated, "Bide your time and put {OFFERER} up later.", "You filed {OFFERER}'s name away for a later week.", CompBonus(3)),
                        } },
                    new Act { title = "Act III · Action", text = "What you know about {OFFERER}'s offer is a card in your hand. When do you play it?",
                        options = new[]
                        {
                            Opt("secure-info", "Keep it for later", Low, Calculated, "Store it for the perfect moment.", "You kept it to yourself, for now.", CompBonus(2), SocialBonus(1)),
                            Opt("leverage-now", "Use it now", High, Hardball, "Spend it this week and shift the balance.", "You spent it at once, and {ALLY} cooled on {OFFERER}.", SocialBonus(3), View("ALLY", "OFFERER", -8)),
                        } },
                    new Act { title = "Act III · Action", text = "{ALLY} trusted you with it, and you kept it. The two of you are alone in the storage room.",
                        options = new[]
                        {
                            Opt("deepen-trust", "Deepen the trust", Medium, Candid, "Share one of your own secrets with {ALLY}.", "You gave {ALLY} a secret of your own to keep.", Move(Player, "ALLY", 10), Bond(Player, "ALLY", BondKinds.Confidant)),
                            Opt("maintain-distance", "Keep your cards close", Low, Calculated, "Appreciate the trust and still keep your cards close.", "You thanked {ALLY} and told {ALLY.them} nothing in return.", CompBonus(2)),
                        } },
                }),
        };

        /// <summary>
        /// The web's branching-story cadence: never before week three, a three-week gap after the
        /// last one, and then 60% ramping by ten points a week to 95% (<c>shouldTriggerBranchingStory</c>).
        /// </summary>
        internal static double BranchingWeight(StoryContext c)
        {
            int last = c.state.storylines.Where(x => Find(x.templateId)?.group == "branching")
                .Select(x => x.week).DefaultIfEmpty(0).Max();
            int gap = c.state.week - last;
            if (c.state.week < 3 || gap < 3) return 0;
            return Web(Math.Min(0.6 + (gap - 3) * 0.1, 0.95));
        }

        // ---------------------------------------------------------------- the-morning-after

        /// <summary>
        /// Reading the room the morning after an eviction: the web's week-start beat (0.55), cast
        /// with whoever the eviction hurt and whoever it relieved. Checking on the house teaches you
        /// how the one who lost somebody handles it (a lore reveal from v4).
        /// </summary>
        private static ArcTemplate TheMorningAfter() => new ArcTemplate
        {
            id = "the-morning-after", lane = StoryLanes.Moment, eyebrow = "The Morning After", title = "The Morning After",
            origin = "web:src/systems/phase-event-system.ts:185-221 (week-start, 0.55); cast from the eviction native",
            rulesVersion = StoryRules.Spine, minWeek = 2, minActive = 4, startAnchors = new[] { StoryAnchors.EvictionNight },
            oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1,
            roles = new[] { Role("QUIET"), Role("UPBEAT") },
            cast = c =>
            {
                string gone = Evicted(c);
                if (gone == null) return null;
                var ranked = Npcs(c).OrderByDescending(x => c.Score(x.id, gone)).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
                if (ranked.Count < 2) return null;
                return Bind().With("QUIET", ranked[0].id).With("UPBEAT", ranked[ranked.Count - 1].id).Headlining(ranked[0].id);
            },
            weight = (c, b) => Web(0.55),
            beats = new[]
            {
                new BeatTemplate
                {
                    id = "morning", surface = StorySurfaces.Scene, venue = Kitchen,
                    title = "The Morning After",
                    summary = "The house is different the morning after an eviction.",
                    text = "The house feels different this morning. Last night's eviction has shifted the energy. {QUIET} is unusually quiet, while {UPBEAT} can barely hide {UPBEAT.their} relief. The game resets, but the scars remain.",
                    alternates = new[]
                    {
                        "Breakfast is subdued. {QUIET} is staring into a cold coffee, and {UPBEAT} is humming at the stove. One chair at the table is empty, and everyone is pretending not to look at it.",
                        "Someone has already moved the evicted houseguest's things. {QUIET} watched them do it. {UPBEAT} is already talking about next week.",
                    },
                    lapse = "stay-in-bed",
                    options = new[]
                    {
                        Opt("check-house", "Check on the house", Low, Warm, "Make the rounds and gauge the new dynamics.",
                            "You spent the morning reading the room. {QUIET} lost more than a vote last night; {UPBEAT} can barely contain the relief.",
                            SocialBonus(2), Reveal("QUIET", Lore.Facets.ConflictStyle)),
                        Opt("rally", "Rally your allies", Medium, Warm, "Now is the time to strengthen your position.",
                            "You pulled your people aside for a morning debrief.", SocialBonus(1))
                            .Extra((c, y) => Npcs(c).Where(x => c.state.Allied(P(c), x.id)).Select(x => Move(Player, "@" + x.id, 2))),
                        Lapse("stay-in-bed", "Stay in bed", "Let others make the first move. Observe from a distance.",
                            "You feigned sleep while the house buzzed. The best intel comes when people think you aren't listening.",
                            CompBonus(1), Stress(Player, -1)),
                    },
                },
            },
        };
    }
}
