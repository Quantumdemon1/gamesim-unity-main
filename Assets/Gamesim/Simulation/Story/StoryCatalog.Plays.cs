using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Plays (plan 30 §2): arcs with a goal the player can win or lose, one per currency to start.
    /// Each opens with an offer the player takes on or turns down. Its step's options are reads:
    /// which works depends on who it is aimed at, and the odds shown are the odds used. The goal
    /// is checked against the season, so the windows' lobby or a conversation can win it too.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> PlayArcs()
        {
            yield return TheSecretAlliance();
            yield return StayOffTheBlock();
            yield return TheirWord();
            yield return SettleIt();
            yield return BuildTheNumbers();
            yield return KnowThemPlay();
            yield return TheFavour();
            yield return StirThePot();
        }

        /// <summary>
        /// A play's offer: what it is, and a choice to take it on or not. Turning it down ends the
        /// cycle as declined, and the first refusal may come round again next week (plan 30 D2).
        /// </summary>
        private static BeatTemplate PlayOffer(string title, string venue, string deadline, string firstStep, string text,
            params string[] alternates) => new BeatTemplate
        {
            id = "offer", surface = StorySurfaces.Approach, venue = venue, closes = deadline,
            title = title, summary = "A play is on offer.", text = text, alternates = alternates,
            lapse = PlayOptions.NotNow,
            options = new[]
            {
                Opt(PlayOptions.TakeItOn, PlayOptions.TakeItOnLabel, "Go after it.", "You decided to go after it.").Then(firstStep),
                Lapse(PlayOptions.NotNow, PlayOptions.NotNowLabel, "Leave it for now.", "You left it for now.")
                    .Then("end:" + PlayEndings.Declined),
            },
        };

        /// <summary>The active alliance two people share, or null.</summary>
        private static AllianceState AllianceOf(StoryContext c, string a, string b) =>
            a == null || b == null ? null
                : c.state.alliances.Where(x => x.active && x.members.Contains(a) && x.members.Contains(b))
                    .OrderBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();

        // ---------------------------------------------------------------- Intel: the secret alliance

        private static ArcTemplate TheSecretAlliance() => new ArcTemplate
        {
            id = "the-secret-alliance", lane = StoryLanes.Play, eyebrow = "Intel", title = "The Secret Alliance",
            origin = "native: plan 30 §2, the Intel play: an alliance the player can see the shape of but not who is in it",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.HohCrowned, StoryAnchors.BlockSet },
            oncePerHeadliner = false, cooldownWeeks = 3,
            roles = new[] { Role("INSIDER"), Role("PARTNER") },
            cast = c =>
            {
                // An alliance the player is not in and cannot see, with two members still in the house.
                var hidden = c.state.alliances
                    .Where(a => a.active && !a.members.Contains(P(c))
                                && a.members.Count(m => c.Find(m)?.status == ContestantStatus.Active) >= 2
                                && !Knowledge.AllianceVisibleTo(c.state, a, P(c)))
                    .OrderBy(a => a.id, StringComparer.Ordinal).ToList();
                var alliance = Keyed(c, "secret-alliance", hidden);
                if (alliance == null) return null;
                var members = alliance.members.Where(m => c.Find(m)?.status == ContestantStatus.Active)
                    .OrderByDescending(m => c.Score(P(c), m)).ThenBy(m => m, StringComparer.Ordinal).ToList();
                // The one the player is closest to is the one worth asking.
                return Bind().With("INSIDER", members[0]).With("PARTNER", members[1]).Headlining(members[0]);
            },
            weight = (c, b) => 20,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Intel, deadline = StoryAnchors.EvictionNight,
                goal = "Find out who {INSIDER} is working with before eviction night.",
                progress = (c, x) => PlayProgress.Done(AllianceOf(c, x.Role("INSIDER"), x.Role("PARTNER")) is AllianceState a
                                                       && Knowledge.AllianceVisibleTo(c.state, a, P(c))),
                wonOutcome = "You found out who {INSIDER} is working with.",
                lostOutcome = "{INSIDER} kept the alliance hidden from you.",
            },
            beats = new[]
            {
                PlayOffer("The Secret Alliance", Kitchen, StoryAnchors.EvictionNight, "dig",
                    "Conversations keep stopping when you walk into the kitchen. {INSIDER} and {PARTNER} are working together, and it isn't only the two of them.",
                    "{INSIDER} and {PARTNER} keep ending up in the same corner of the house, and the whispering stops when you get close."),
                new BeatTemplate
                {
                    id = "dig", surface = StorySurfaces.Approach, venue = Kitchen, closes = StoryAnchors.EvictionNight,
                    title = "The Secret Alliance", summary = "You try to find out who is in an alliance.",
                    text = "{INSIDER} and {PARTNER} are keeping something from you. How do you find out who else is in it?",
                    lapse = "leave-it",
                    options = new[]
                    {
                        Opt("ask-straight", "Ask them straight out", Medium, Candid,
                            "Ask {INSIDER} directly who else is in it.",
                            "{INSIDER} looked at you for a long moment, then told you who else is in it.",
                            LeakAlliance("INSIDER", "PARTNER", Player))
                            .Checked(40, "INSIDER", "{INSIDER} laughed it off, and by dinner {PARTNER} knew you had been asking.",
                                Told("PARTNER", Player, -4, StoryReceipts.TooNosy))
                            .Then(Waits),
                        Opt("watch-them", "Watch who they talk to", Low, Calculated,
                            "Spend some of your time watching who {INSIDER} and {PARTNER} huddle with.",
                            "You watched long enough to see who else they huddle with.",
                            LeakAlliance("INSIDER", "PARTNER", Player))
                            .Checked(55, "PARTNER", "{PARTNER} caught you watching, and they went quiet around you.",
                                Move(Player, "PARTNER", -3))
                            .CostsAction().Then(Waits),
                        Opt("trade-what-you-know", "Trade something you know", Low, Calculated,
                            "Offer {PARTNER} what you know about another alliance in exchange for names.",
                            "{PARTNER} traded names for what you knew.",
                            LeakAlliance("INSIDER", "PARTNER", Player), Move(Player, "PARTNER", 2))
                            .Needs((c, x) => c.state.alliances.Any(a => a.active && !a.members.Contains(P(c))
                                                                        && !(a.members.Contains(x.Role("INSIDER")) && a.members.Contains(x.Role("PARTNER")))
                                                                        && Knowledge.AllianceVisibleTo(c.state, a, P(c))),
                                "Needs another alliance you know about")
                            .Then(Waits),
                        Lapse("leave-it", "Leave it for now", "Let them keep their secret for now.", "You let it be for now.").Then(Waits),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- Power: stay off the block

        private static ArcTemplate StayOffTheBlock() => new ArcTemplate
        {
            id = "stay-off-the-block", lane = StoryLanes.Play, eyebrow = "Power", title = "Stay Off the Block",
            origin = "native: plan 30 §2, the Power play: the Head of Household's bottom two, read as hoh-room reads it; decided at the final block",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.HohCrowned },
            oncePerHeadliner = false, cooldownWeeks = 1,
            roles = new[] { Role("HOH"), Optional("ALLY"), Optional("TARGET") },
            cast = c =>
            {
                string hoh = NpcHoh(c);
                if (hoh == null || c.state.nominees.Count > 0 || !InBottomTwo(c, hoh)) return null;
                // Someone the Head of Household listens to who likes you: the one who could put in a word.
                string ally = Npcs(c).Where(x => x.id != hoh && c.Score(x.id, P(c)) >= 5 && c.Score(hoh, x.id) >= 10)
                    .OrderByDescending(x => c.Score(hoh, x.id) + c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal)
                    .FirstOrDefault()?.id;
                // Someone the Head of Household would rather see go: a bigger target to point at.
                string target = Npcs(c).Where(x => x.id != hoh && x.id != ally)
                    .OrderBy(x => c.Score(hoh, x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()?.id;
                return Bind().With("HOH", hoh).With("ALLY", ally).With("TARGET", target).Headlining(hoh);
            },
            weight = (c, b) => 40,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Power, deadline = StoryAnchors.BlockSet,
                goal = "Be off {HOH}'s block when the veto meeting is over.",
                // Only the final block counts: a backdoor or the veto can still change the first names.
                progress = (c, x) => PlayProgress.Done(c.state.vetoResolved && c.state.nominees.Count > 0 && !c.state.nominees.Contains(P(c))),
                wonOutcome = "You made it through {HOH}'s week off the block.",
                lostOutcome = "{HOH}'s week ended with you on the block.",
            },
            beats = new[]
            {
                PlayOffer("Stay Off the Block", Hallway, StoryAnchors.NomsSet, "angles",
                    "The house is counting votes, and the talk in the hallway is that {HOH} has you in {HOH.their} bottom two. Nominations are coming."),
                new BeatTemplate
                {
                    id = "angles", surface = StorySurfaces.Approach, venue = Hallway, closes = StoryAnchors.NomsSet,
                    title = "Stay Off the Block", summary = "You work on staying off the block.",
                    text = "{HOH} decides the nominations. What do you do about being in {HOH.their} bottom two? Anything that changes {HOH.their} mind counts.",
                    lapse = "lay-low",
                    options = new[]
                    {
                        Opt("get-a-voucher", "Get someone to vouch for you", Low, Warm,
                            "Ask {ALLY} to put in a word for you with {HOH}.",
                            "{ALLY} put in a word for you with {HOH}.",
                            View("HOH", Player, 12), Move(Player, "ALLY", 2))
                            .Checked(55, "ALLY", "{ALLY} would rather not stick {ALLY.their} neck out, and {HOH} noticed you asking.",
                                View("HOH", Player, -4))
                            .ShowIf((c, x) => x.Role("ALLY") != null).Then(Waits),
                        Opt("point-elsewhere", "Point them at a bigger target", Medium, Calculated,
                            "Make the case to {HOH} that {TARGET} is the bigger threat.",
                            "{HOH} heard you out, and the case against {TARGET} landed.",
                            View("HOH", "TARGET", -15))
                            .Checked(45, "HOH", "{HOH} did not like being told who to nominate, and {TARGET} heard about it.",
                                View("HOH", Player, -6), Grudge("TARGET", Player, 25))
                            // Where the windows play, the lobby is the way to the Head of Household's ear.
                            .ShowIf((c, x) => x.Role("TARGET") != null && !StrategyRules.Apply(c.state)).Then(Waits),
                        Opt("promise-safety", "Offer them your safety", Medium, Warm,
                            "Promise {HOH} you will keep {HOH.them} safe next week if {HOH.they} keep you safe now.",
                            "{HOH} took the deal.",
                            Deal("HOH", Player, DealKind.SafetyAgreement))
                            .Checked(50, "HOH", "{HOH} would not commit, and now {HOH.they} know how worried you are.",
                                View("HOH", Player, -3))
                            .ShowIf((c, x) => !StrategyRules.Apply(c.state)).Then(Waits),
                        Lapse("lay-low", "Lay low", "Keep your head down and hope it passes.", "You kept your head down.").Then(Waits),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- Trust: their word

        private static ArcTemplate TheirWord() => new ArcTemplate
        {
            id = "their-word", lane = StoryLanes.Play, eyebrow = "Trust", title = "Their Word",
            origin = "native: plan 30 §2, the Trust play: a friendly houseguest's promise, through the engine's own promise rules",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.EvictionNight },
            oncePerHeadliner = false, cooldownWeeks = 2,
            roles = new[] { Role("FRIEND") },
            cast = c =>
            {
                // Someone who likes you and has promised you nothing yet this week.
                var friends = Npcs(c).Where(x => c.Score(x.id, P(c)) >= 10 && !PromisedSafe(c, x.id))
                    .OrderByDescending(x => c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).Take(3).ToList();
                var friend = Keyed(c, "their-word", friends);
                return friend == null ? null : Bind().With("FRIEND", friend.id).Headlining(friend.id);
            },
            weight = (c, b) => 20,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Trust, deadline = StoryAnchors.EvictionNight,
                goal = "Get {FRIEND}'s word that {FRIEND.they} will keep you safe.",
                progress = (c, x) => PlayProgress.Done(PromisedSafe(c, x.Role("FRIEND"))),
                wonOutcome = "{FRIEND} gave you {FRIEND.their} word.",
                lostOutcome = "{FRIEND} never quite promised you anything.",
            },
            beats = new[]
            {
                PlayOffer("Their Word", Yard, StoryAnchors.EvictionNight, "ask",
                    "{FRIEND} has been easy company all week. With a new vote coming, easy company is worth turning into a promise."),
                new BeatTemplate
                {
                    id = "ask", surface = StorySurfaces.Approach, venue = Yard, closes = StoryAnchors.EvictionNight,
                    title = "Their Word", summary = "You try to get a promise of safety.",
                    text = "{FRIEND} likes you. The question is whether {FRIEND.they} will say so where it counts.",
                    lapse = "leave-it",
                    options = new[]
                    {
                        Opt("ask-for-it", "Ask for their word", Medium, Warm,
                            "Ask {FRIEND} to promise to keep you safe.",
                            "{FRIEND} gave you {FRIEND.their} word.",
                            Promise("FRIEND", Player, PromiseKind.Safety))
                            .Checked(45, "FRIEND", "{FRIEND} went vague on you, and it stung a little.",
                                Move(Player, "FRIEND", -3))
                            .Then(Waits),
                        Opt("do-a-favour", "Do something for them first", Low, Warm,
                            "Help {FRIEND} with something first, then ask.",
                            "You helped {FRIEND} out, and when you asked, {FRIEND.they} gave you {FRIEND.their} word.",
                            Move(Player, "FRIEND", 4), Promise("FRIEND", Player, PromiseKind.Safety))
                            .Checked(65, "FRIEND", "{FRIEND} appreciated the help but would not promise anything yet.",
                                Move(Player, "FRIEND", 2))
                            .CostsAction().Then(Waits),
                        Opt("trade-promises", "Trade promises", Medium, Calculated,
                            "Offer {FRIEND} your safety for {FRIEND.theirs}.",
                            "You shook on it: you keep each other safe.",
                            Promise("FRIEND", Player, PromiseKind.Safety), Promise(Player, "FRIEND", PromiseKind.Safety))
                            .Checked(55, "FRIEND", "{FRIEND} took it as a sign you are worried, and backed off.",
                                Move(Player, "FRIEND", -2))
                            .Then(Waits),
                        Lapse("leave-it", "Leave it for now", "Let it stay easy company for now.", "You left it for now.").Then(Waits),
                    },
                },
            },
        };

        /// <summary>Whether somebody has promised the player safety, or struck a safety agreement with them, that still holds.</summary>
        private static bool PromisedSafe(StoryContext c, string id) =>
            id != null
            && (c.state.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == id && p.toId == P(c)
                                          && (p.kind == PromiseKind.Safety || p.kind == PromiseKind.Vote))
                || c.state.deals.Any(d => DealStatus.Binds(d.status) && d.type == DealKind.SafetyAgreement
                                          && ((d.proposerId == id && d.recipientId == P(c)) || (d.proposerId == P(c) && d.recipientId == id))));

        // ---------------------------------------------------------------- Trust: settle it

        private static ArcTemplate SettleIt() => new ArcTemplate
        {
            id = "settle-it", lane = StoryLanes.Play, eyebrow = "Trust", title = "Settle It",
            origin = "native: plan 30 §2, the Trust play against a grudge: grudges read by nominations, votes and the jury",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.EvictionNight },
            oncePerHeadliner = false, cooldownWeeks = 3,
            roles = new[] { Role("RIVAL") },
            cast = c =>
            {
                // The houseguest holding the heaviest grudge against the player.
                var rival = Npcs(c).Where(x => c.Grudge(x.id, P(c)) >= 40)
                    .OrderByDescending(x => c.Grudge(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return rival == null ? null : Bind().With("RIVAL", rival.id).Headlining(rival.id);
            },
            weight = (c, b) => 25,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Trust, deadline = StoryAnchors.EvictionNight,
                goal = "Get {RIVAL} to let it go before the next eviction.",
                // Settled below 20; cooler below 40, where the grudge first counted.
                progress = (c, x) =>
                {
                    double grudge = c.Grudge(x.Role("RIVAL"), P(c));
                    return new PlayProgress(grudge < 20 ? 2 : grudge < 40 ? 1 : 0, 2);
                },
                wonOutcome = "{RIVAL} let it go.",
                partOutcome = "It is cooler between you and {RIVAL}, but it is not settled.",
                lostOutcome = "{RIVAL} is still holding it against you.",
                lost = new[] { Grudge("RIVAL", Player, 10) },
            },
            beats = new[]
            {
                PlayOffer("Settle It", Living, StoryAnchors.EvictionNight, "approach",
                    "{RIVAL} still will not sit on the same couch as you. A grudge like that turns into a vote."),
                new BeatTemplate
                {
                    id = "approach", surface = StorySurfaces.Approach, venue = Living, closes = StoryAnchors.EvictionNight,
                    title = "Settle It", summary = "You try to settle a grudge.",
                    text = "{RIVAL} is sitting alone on the far couch. How do you start?",
                    lapse = "let-it-ride",
                    options = new[]
                    {
                        Opt("apologise", "Apologise", Low, Yield,
                            "Tell {RIVAL} you are sorry, and mean it.",
                            "{RIVAL} accepted the apology, slowly.",
                            Ease("RIVAL", Player, 30), Move(Player, "RIVAL", 3))
                            .Checked(50, "RIVAL", "{RIVAL} took the apology as weakness.",
                                Grudge("RIVAL", Player, 10))
                            .Then(Waits),
                        Opt("clear-the-air", "Clear the air", Medium, Hardball,
                            "Have it out with {RIVAL}, all of it, once.",
                            "It got loud, and then it got better. {RIVAL} let most of it go.",
                            Ease("RIVAL", Player, 45))
                            .Checked(40, "RIVAL", "It got loud and stayed loud.",
                                Grudge("RIVAL", Player, 15))
                            .Then(Waits),
                        Opt("cook-for-them", "Cook them dinner", Low, Warm,
                            "Make {RIVAL}'s favourite and bring it over.",
                            "{RIVAL} ate every bite, and some of the grudge went with it.",
                            Ease("RIVAL", Player, 25), Move(Player, "RIVAL", 4))
                            .Checked(60, "RIVAL", "{RIVAL} ate it without a word.",
                                Move(Player, "RIVAL", 1))
                            .CostsAction().Then(Waits),
                        Lapse("let-it-ride", "Let it ride", "Give it time.", "You gave it time.").Then(Waits),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- Alliance: build the numbers

        private static ArcTemplate BuildTheNumbers() => new ArcTemplate
        {
            id = "build-the-numbers", lane = StoryLanes.Play, eyebrow = "Alliance", title = "Build the Numbers",
            origin = "native: plan 30 §2, the Alliance play: three votes the player can steer",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.EvictionNight },
            oncePerHeadliner = false, cooldownWeeks = 3, minActive = 5,
            roles = new[] { Role("FIRST"), Role("SECOND") },
            cast = c =>
            {
                // Only for a player without numbers: no alliance of three or more yet.
                if (c.state.alliances.Any(a => a.active && a.members.Contains(P(c)) && a.members.Count >= 3)) return null;
                var warm = Npcs(c).Where(x => c.Score(x.id, P(c)) >= 12 && !c.state.Allied(P(c), x.id))
                    .OrderByDescending(x => c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
                // The warmest pair who can stand each other.
                for (int i = 0; i < warm.Count; i++)
                    for (int j = i + 1; j < warm.Count; j++)
                        if (Mutual(c, warm[i].id, warm[j].id) >= 0)
                            return Bind().With("FIRST", warm[i].id).With("SECOND", warm[j].id).Headlining(warm[i].id);
                return null;
            },
            weight = (c, b) => 20,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Alliance, deadline = StoryAnchors.EvictionNight,
                goal = "Bring {FIRST} and {SECOND} into an alliance with you before the next eviction.",
                progress = (c, x) => new PlayProgress(new[] { x.Role("FIRST"), x.Role("SECOND") }
                    .Count(id => id != null && c.state.Allied(P(c), id)), 2),
                wonOutcome = "You, {FIRST} and {SECOND} are an alliance now.",
                partOutcome = "You have one of them, but not both.",
                lostOutcome = "The three of you never came together.",
            },
            beats = new[]
            {
                PlayOffer("Build the Numbers", Bedroom, StoryAnchors.EvictionNight, "pitch",
                    "{FIRST} and {SECOND} both trust you, and neither of them trusts the house. Three votes that move together are hard to beat."),
                new BeatTemplate
                {
                    id = "pitch", surface = StorySurfaces.Approach, venue = Bedroom, closes = StoryAnchors.EvictionNight,
                    title = "Build the Numbers", summary = "You try to bring two houseguests into an alliance.",
                    text = "You have {FIRST} and {SECOND} alone in the bedroom. Do you ask now?",
                    lapse = "leave-it",
                    options = new[]
                    {
                        Opt("pitch-now", "Pitch the three of you", Medium, Bold,
                            "Put it to {FIRST} and {SECOND} together: the three of you, voting as one.",
                            "{FIRST} looked at {SECOND}, and {SECOND} nodded. The three of you are in it together.",
                            Alliance(Player, "FIRST", "SECOND"))
                            .Checked(45, "FIRST", "{SECOND} thought it was too soon, and the moment passed.",
                                Move(Player, "SECOND", -3))
                            .Then(Waits, "pitch-again"),
                        Opt("win-them-over", "Win them over first", Low, Warm,
                            "Spend time with both of them first, then ask later in the week.",
                            "You spent the evening with {FIRST} and {SECOND}. Later in the week you can ask.",
                            Move(Player, "FIRST", 3), Move(Player, "SECOND", 3))
                            .CostsAction().Then("pitch-again"),
                        Lapse("leave-it", "Leave it for now", "Wait for a better moment.", "You waited.").Then(Waits),
                    },
                },
                new BeatTemplate
                {
                    id = "pitch-again", anchor = StoryAnchors.BlockSet, surface = StorySurfaces.Approach, venue = Bedroom,
                    closes = StoryAnchors.EvictionNight,
                    title = "Build the Numbers", summary = "A second chance to bring two houseguests into an alliance.",
                    text = "The week has settled, and {FIRST} and {SECOND} are both still in your corner. Now?",
                    lapse = "leave-it",
                    options = new[]
                    {
                        Opt("pitch-now", "Pitch the three of you", Medium, Bold,
                            "Put it to {FIRST} and {SECOND} together.",
                            "{FIRST} and {SECOND} were both in. The three of you are an alliance.",
                            Alliance(Player, "FIRST", "SECOND"))
                            .Checked(60, "FIRST", "{SECOND} still was not ready, and said so.",
                                Move(Player, "SECOND", -2))
                            .Then(Waits),
                        Lapse("leave-it", "Leave it for now", "Wait for a better moment.", "You waited.").Then(Waits),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- Intel: know them

        private static ArcTemplate KnowThemPlay() => new ArcTemplate
        {
            id = "know-them", lane = StoryLanes.Play, eyebrow = "Intel", title = "Know Them",
            origin = "native: plan 30 §2, the Intel play about a person: lore learned makes every later read of them clearer",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet },
            oncePerHeadliner = false, cooldownWeeks = 2,
            roles = new[] { Role("SUBJECT") },
            cast = c =>
            {
                // Somebody the player gets on with and knows nothing about yet, with things to learn.
                var strangers = Npcs(c).Where(x => c.Score(P(c), x.id) >= 0 && !c.Real(x.id)
                                                   && Lore.Learned(c.state, x.id).Count == 0 && Lore.FactsOf(c.state, x.id).Count() >= 2)
                    .OrderByDescending(x => c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).Take(3).ToList();
                var subject = Keyed(c, "know-them", strangers);
                return subject == null ? null : Bind().With("SUBJECT", subject.id).Headlining(subject.id);
            },
            weight = (c, b) => 15,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Intel, deadline = StoryAnchors.EvictionNight,
                goal = "Learn two things about {SUBJECT} before the next eviction.",
                // Anything learned counts: this play's questions, or an ordinary conversation.
                progress = (c, x) => new PlayProgress(Lore.Learned(c.state, x.Role("SUBJECT")).Count, 2),
                wonOutcome = "You know {SUBJECT} a lot better than you did.",
                partOutcome = "You learned something about {SUBJECT}, but not much.",
                lostOutcome = "{SUBJECT} stayed a closed book.",
                won = new[] { Move(Player, "SUBJECT", 2) },
            },
            beats = new[]
            {
                PlayOffer("Know Them", Kitchen, StoryAnchors.EvictionNight, "talk",
                    "You have lived with {SUBJECT} for weeks and you could not say where {SUBJECT.they} grew up. Knowing people is how you read them."),
                KnowThemTalk("talk", null, "again"),
                KnowThemTalk("again", StoryAnchors.EvictionEve, Waits),
            },
        };

        /// <summary>A chance to learn about somebody: each approach reads differently with different people.</summary>
        private static BeatTemplate KnowThemTalk(string id, string anchor, string next) => new BeatTemplate
        {
            id = id, anchor = anchor, surface = StorySurfaces.Approach, venue = Kitchen, closes = StoryAnchors.EvictionNight,
            title = "Know Them", summary = "You try to get to know a houseguest.",
            text = "{SUBJECT} is making coffee and nobody else is around. How do you get {SUBJECT.them} talking?",
            lapse = "leave-it",
            options = new[]
            {
                Opt("ask-about-home", "Ask about home", Low, Warm,
                    "Ask {SUBJECT} about home and the people waiting there.",
                    "{SUBJECT} talked about home for a long time.",
                    Reveal("SUBJECT", Lore.Facets.Home), Reveal("SUBJECT", Lore.Facets.Origin))
                    .Checked(55, "SUBJECT", "{SUBJECT} changed the subject.", Move(Player, "SUBJECT", -1))
                    .Then(next),
                Opt("share-first", "Share something of yours first", Low, Candid,
                    "Tell {SUBJECT} something real about yourself, and see what comes back.",
                    "You went first, and {SUBJECT} met you halfway.",
                    Reveal("SUBJECT", Lore.Facets.Comfort), Reveal("SUBJECT", Lore.Facets.Work), Move(Player, "SUBJECT", 2))
                    .Checked(60, "SUBJECT", "{SUBJECT} listened, and kept {SUBJECT.their} own story to {SUBJECT.themselves}.")
                    .Then(next),
                Opt("talk-game", "Talk about why they came", Medium, Calculated,
                    "Ask {SUBJECT} what {SUBJECT.they} want out of this game.",
                    "{SUBJECT} told you what {SUBJECT.they} came here for.",
                    Reveal("SUBJECT", Lore.Facets.Goal), Reveal("SUBJECT", Lore.Facets.Respects))
                    .Checked(45, "SUBJECT", "{SUBJECT} did not like being asked, and said so.", Move(Player, "SUBJECT", -3))
                    .Then(next),
                Lapse("leave-it", "Leave it for now", "Let {SUBJECT} drink {SUBJECT.their} coffee in peace.", "You left it for now.").Then(Waits),
            },
        };

        // ---------------------------------------------------------------- Power: the favour

        private static ArcTemplate TheFavour() => new ArcTemplate
        {
            id = "the-favour", lane = StoryLanes.Play, eyebrow = "Power", title = "The Favour",
            origin = "native: plan 30 §2, the Power play that banks leverage: a hook the player can call in later",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.BlockSet },
            oncePerHeadliner = false, cooldownWeeks = 2,
            roles = new[] { Role("NOMINEE") },
            cast = c =>
            {
                // A nominee who is not the player, does not dislike the player, and owes them nothing yet.
                var nominees = NpcNominees(c).Where(x => c.Score(x.id, P(c)) >= 0 && !Hooks.Has(c.state, P(c), x.id))
                    .OrderByDescending(x => c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
                var nominee = Keyed(c, "the-favour", nominees);
                return nominee == null ? null : Bind().With("NOMINEE", nominee.id).Headlining(nominee.id);
            },
            weight = (c, b) => 20,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Power, deadline = StoryAnchors.EvictionNight,
                goal = "Help {NOMINEE} through the vote so that {NOMINEE.they} owe you one.",
                progress = (c, x) => PlayProgress.Done(Hooks.Has(c.state, P(c), x.Role("NOMINEE"))),
                wonOutcome = "{NOMINEE} owes you one, and knows it.",
                lostOutcome = "{NOMINEE} owes you nothing.",
            },
            beats = new[]
            {
                PlayOffer("The Favour", Storage, StoryAnchors.EvictionNight, "help",
                    "{NOMINEE} is on the block and scared. People remember who stood with them when it counted."),
                new BeatTemplate
                {
                    id = "help", surface = StorySurfaces.Approach, venue = Storage, closes = StoryAnchors.EvictionNight,
                    title = "The Favour", summary = "You help a nominee, for a favour later.",
                    text = "{NOMINEE} catches you in the storage room. \"I need votes. Can you help me?\"",
                    lapse = "stay-out",
                    options = new[]
                    {
                        Opt("campaign-for-them", "Campaign for them", Medium, Warm,
                            "Spend your time this week talking the house round for {NOMINEE}.",
                            "You worked the house for {NOMINEE}, and {NOMINEE.they} saw you do it.",
                            Hook(Player, "NOMINEE"), Move(Player, "NOMINEE", 3))
                            .Checked(55, "NOMINEE", "You tried, but {NOMINEE} never heard about it.", Move(Player, "NOMINEE", 1))
                            .CostsAction().Then(Waits),
                        Opt("cover-for-them", "Cover for them", Medium, Calculated,
                            "Tell the house the thing {NOMINEE} is being blamed for was not {NOMINEE.their} doing.",
                            "You took the heat for {NOMINEE}, and {NOMINEE.they} will not forget it.",
                            Hook(Player, "NOMINEE"))
                            .Checked(50, "NOMINEE", "Nobody believed you, and {NOMINEE} was embarrassed you tried.",
                                Move(Player, "NOMINEE", -2))
                            .Then(Waits),
                        Lapse("stay-out", "Stay out of it", "It is not your fight.", "You stayed out of it.").Then(Waits),
                    },
                },
            },
        };

        // ---------------------------------------------------------------- Power: stir the pot

        private static ArcTemplate StirThePot() => new ArcTemplate
        {
            id = "stir-the-pot", lane = StoryLanes.Play, eyebrow = "Power", title = "Stir the Pot",
            origin = "native: plan 30 §2, the Power play of turning two houseguests on each other; grudges weigh in nominations and votes",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.EvictionNight, StoryAnchors.BlockSet },
            oncePerHeadliner = false, cooldownWeeks = 3, minActive = 5,
            roles = new[] { Role("PAWN"), Role("TARGET") },
            cast = c =>
            {
                // The target: the player's strongest competitor. The pawn: somebody who trusts the
                // player and has no quarrel with the target yet.
                var target = Npcs(c).OrderByDescending(x => x.hohWins + x.vetoWins).ThenByDescending(x => x.stats.competition)
                    .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                if (target == null) return null;
                var pawn = Npcs(c).Where(x => x.id != target.id && c.Score(x.id, P(c)) >= 5 && c.Grudge(x.id, target.id) < 20)
                    .OrderByDescending(x => c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return pawn == null ? null : Bind().With("PAWN", pawn.id).With("TARGET", target.id).Headlining(pawn.id);
            },
            weight = (c, b) => 15,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Power, deadline = StoryAnchors.EvictionNight,
                goal = "Turn {PAWN} against {TARGET} before the next eviction.",
                // A grudge of 40 is one the pawn acts on; 20 is a crack.
                progress = (c, x) =>
                {
                    double grudge = c.Grudge(x.Role("PAWN"), x.Role("TARGET"));
                    return new PlayProgress(grudge >= 40 ? 2 : grudge >= 20 ? 1 : 0, 2);
                },
                wonOutcome = "{PAWN} and {TARGET} are not speaking.",
                partOutcome = "There is a crack between {PAWN} and {TARGET} now.",
                lostOutcome = "{PAWN} and {TARGET} are as close as ever.",
            },
            beats = new[]
            {
                PlayOffer("Stir the Pot", Yard, StoryAnchors.EvictionNight, "stir",
                    "{TARGET} keeps winning, and {PAWN} still thinks {TARGET} is a friend. That could change."),
                new BeatTemplate
                {
                    id = "stir", surface = StorySurfaces.Approach, venue = Yard, closes = StoryAnchors.EvictionNight,
                    title = "Stir the Pot", summary = "You try to turn one houseguest against another.",
                    text = "{PAWN} is on the hammock, alone. {TARGET} is across the yard, laughing with the house.",
                    lapse = "leave-them",
                    options = new[]
                    {
                        Opt("tell-them", "Tell them what was said", High, Calculated,
                            "Tell {PAWN} what {TARGET} has been saying about {PAWN.them}.",
                            "{PAWN} went very quiet, then very angry.",
                            Grudge("PAWN", "TARGET", 45, GrudgeCauses.Story))
                            .Checked(50, "PAWN", "{PAWN} went straight to {TARGET}, and {TARGET} knows exactly who started it.",
                                Grudge("TARGET", Player, 30, GrudgeCauses.Story), Move(Player, "PAWN", -4))
                            .Then(Waits),
                        Opt("plant-a-seed", "Plant a seed", Medium, Calculated,
                            "Wonder aloud whether {TARGET} really has {PAWN}'s back, and leave it there.",
                            "{PAWN} did not say anything, but {PAWN.they} watched {TARGET} differently after.",
                            Grudge("PAWN", "TARGET", 25, GrudgeCauses.Story))
                            .Checked(65, "PAWN", "{PAWN} shrugged it off.", Move(Player, "PAWN", -1))
                            .Then(Waits),
                        Lapse("leave-them", "Leave them be", "Not today.", "You left them to it.").Then(Waits),
                    },
                },
            },
        };
    }
}
