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

        /// <summary>
        /// The active alliance two people share, or null. Under the leak rules it is the one pact the two
        /// of them name to the player (<see cref="Knowledge.PactOfPair"/>): one the player does not yet know
        /// of before one they do, so a play about a secret pact is about that pact and not a known one of
        /// the same two people.
        /// </summary>
        private static AllianceState AllianceOf(StoryContext c, string a, string b)
        {
            if (a == null || b == null) return null;
            if (AllianceLeaks.On(c.state))
            {
                var pact = Knowledge.PactOfPair(c.state, a, b, P(c));
                return pact != null && pact.active ? pact : null;
            }
            return c.state.alliances.Where(x => x.active && x.members.Contains(a) && x.members.Contains(b))
                .OrderBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
        }

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
                            .Checked(50, "INSIDER", "{INSIDER} laughed it off, and by dinner {PARTNER} knew you had been asking.",
                                Told("PARTNER", Player, -4, StoryReceipts.TooNosy))
                            .Then(Waits),
                        Opt("watch-them", "Watch who they talk to", Low, Calculated,
                            "Spend some of your time watching who {INSIDER} and {PARTNER} huddle with.",
                            "You watched long enough to see who else they huddle with.",
                            LeakAlliance("INSIDER", "PARTNER", Player))
                            .Checked(50, "PARTNER", "{PARTNER} caught you watching, and they went quiet around you.",
                                Move(Player, "PARTNER", -3))
                            .CostsAction().Then(Waits),
                        Opt("over-a-game", "Get it out of them over a game", Low, Playful,
                            "Deal {INSIDER} into a card game and let the talk wander where you want it.",
                            "Three hands in, {INSIDER} let slip who else is in it.",
                            LeakAlliance("INSIDER", "PARTNER", Player))
                            .Checked(50, "INSIDER", "{INSIDER} saw where the questions were going and folded.",
                                Move(Player, "INSIDER", -2))
                            .Then(Waits),
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
            origin = "native: plan 30 §2, the Power play: the Head of Household's bottom two by the ranking the nominations use; lost the moment you are named",
            rulesVersion = StoryRules.Plays, startAnchors = new[] { StoryAnchors.HohCrowned },
            oncePerHeadliner = false, cooldownWeeks = 1,
            roles = new[] { Role("HOH"), Optional("ALLY") },
            cast = c =>
            {
                string hoh = NpcHoh(c);
                // Really at risk: the Head of Household's own ranking, the one the nominations use.
                if (hoh == null || c.state.nominees.Count > 0) return null;
                var ranked = EpisodeEngine.NominationCandidates(c.state)
                    .OrderBy(x => EpisodeEngine.NominationWeight(c.state, hoh, x.id)).ToList();
                if (!ranked.Take(2).Any(x => x.isPlayer)) return null;
                // Someone with nothing against you whom the Head of Household has nothing against: the
                // one who could put in a word. How well it goes is the check's business, not the cast's.
                string ally = Npcs(c).Where(x => x.id != hoh && c.Score(x.id, P(c)) >= 0 && c.Score(hoh, x.id) >= 0)
                    .OrderByDescending(x => c.Score(hoh, x.id) + c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal)
                    .FirstOrDefault()?.id;
                // Where the windows play, the Head of Household's own ear is the lobby's. With nobody
                // to work through there is nothing to do here but wait, and waiting is not a play.
                if (ally == null && StrategyRules.Apply(c.state)) return null;
                return Bind().With("HOH", hoh).With("ALLY", ally).Headlining(hoh);
            },
            weight = (c, b) => 40,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Power, deadline = StoryAnchors.BlockSet,
                goal = "Stay off {HOH}'s block all week.",
                // Off the block once the veto meeting is over; named at any point - the nominations or
                // a backdoor - and it is lost there and then, whatever the veto does after.
                progress = (c, x) => PlayProgress.Done(c.state.vetoResolved && c.state.nominees.Count > 0 && !c.state.nominees.Contains(P(c))),
                failed = (c, x) => c.state.nominees.Contains(P(c)),
                wonOutcome = "{HOH}'s week ended without you on the block.",
                lostOutcome = "{HOH} put you on the block.",
            },
            beats = new[]
            {
                PlayOffer("Stay Off the Block", Hallway, StoryAnchors.NomsSet, "angles",
                    "The house is counting votes, and the talk in the hallway is that {HOH} has you in {HOH.their} bottom two. Nominations are coming."),
                new BeatTemplate
                {
                    id = "angles", surface = StorySurfaces.Approach, venue = Hallway, closes = StoryAnchors.NomsSet,
                    title = "Stay Off the Block", summary = "You work on staying off the block.",
                    // Every way through lifts you the same distance in the Head of Household's eyes; which
                    // one lands depends on who you are asking. Pointing at somebody else would not do:
                    // from the very bottom, one name dropping past you still leaves you in the two.
                    text = "{HOH} decides the nominations. What do you do about being in {HOH.their} bottom two? Anything that changes {HOH.their} mind counts.",
                    lapse = "lay-low",
                    options = new[]
                    {
                        Opt("get-a-voucher", "Get someone to vouch for you", Low, Warm,
                            "Ask {ALLY} to put in a word for you with {HOH}.",
                            "{ALLY} put in a word for you with {HOH}.",
                            View("HOH", Player, 20))
                            .Checked(50, "ALLY", "{ALLY} would rather not stick {ALLY.their} neck out, and {HOH} noticed you asking.",
                                View("HOH", Player, -4))
                            .ShowIf((c, x) => x.Role("ALLY") != null).Then(Waits),
                        Opt("ask-them-straight", "Ask them to go to bat for you", Medium, Candid,
                            "Tell {ALLY} straight out that you are in trouble, and ask {ALLY.them} to take it to {HOH}.",
                            "{ALLY} went to {HOH} that night and made your case.",
                            View("HOH", Player, 20))
                            .Checked(50, "ALLY", "{ALLY} said {ALLY.they} would think about it, and did not.",
                                Move(Player, "ALLY", -2))
                            .ShowIf((c, x) => x.Role("ALLY") != null).Then(Waits),
                        Opt("play-you-down", "Have them play you down", Medium, Calculated,
                            "Get {ALLY} to tell {HOH}, as if in passing, that you are no threat to {HOH.them}.",
                            "{ALLY} made you sound harmless, and {HOH} seemed to believe it.",
                            View("HOH", Player, 20))
                            .Checked(50, "ALLY", "{ALLY} said it too plainly, and {HOH} could tell whose idea it was.",
                                View("HOH", Player, -4))
                            .ShowIf((c, x) => x.Role("ALLY") != null).Then(Waits),
                        Opt("make-your-case", "Make your case", Medium, Bold,
                            "Tell {HOH} to {HOH.their} face why you are more use to {HOH.them} in the house than out of it.",
                            "{HOH} heard you out, and the case landed.",
                            View("HOH", Player, 20))
                            .Checked(50, "HOH", "{HOH} did not like being told how to play {HOH.their} week.",
                                View("HOH", Player, -6))
                            // Where the windows play, the lobby is the way to the Head of Household's ear.
                            .ShowIf((c, x) => !StrategyRules.Apply(c.state)).Then(Waits),
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
                var friends = Among(c).Where(x => c.Score(x.id, P(c)) >= 10 && !PromisedSafe(c, x.id))
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
                        Opt("ask-for-it", "Ask for their word", Medium, Candid,
                            "Ask {FRIEND} to promise to keep you safe.",
                            "{FRIEND} gave you {FRIEND.their} word.",
                            Promise("FRIEND", Player, PromiseKind.Safety))
                            .Checked(50, "FRIEND", "{FRIEND} went vague on you, and it stung a little.",
                                Move(Player, "FRIEND", -3))
                            .Then(Waits),
                        Opt("do-a-favour", "Do something for them first", Low, Warm,
                            "Help {FRIEND} with something first, then ask.",
                            "You helped {FRIEND} out, and when you asked, {FRIEND.they} gave you {FRIEND.their} word.",
                            Move(Player, "FRIEND", 4), Promise("FRIEND", Player, PromiseKind.Safety))
                            .Checked(50, "FRIEND", "{FRIEND} appreciated the help but would not promise anything yet.",
                                Move(Player, "FRIEND", 2))
                            .CostsAction().Then(Waits),
                        Opt("trade-promises", "Trade promises", Medium, Calculated,
                            "Offer {FRIEND} your safety for {FRIEND.theirs}.",
                            "You shook on it: you keep each other safe.",
                            Promise("FRIEND", Player, PromiseKind.Safety), Promise(Player, "FRIEND", PromiseKind.Safety))
                            .Checked(50, "FRIEND", "{FRIEND} took it as a sign you are worried, and backed off.",
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
            && ((UnifiedCommitments.SafetyAuthorityOn(c.state) ? CommitmentReferences.Promises(c.state) : c.state.promises)
                .Any(p => p.status == PromiseStatus.Active && p.fromId == id && p.toId == P(c)
                    && (p.kind == PromiseKind.Safety || p.kind == PromiseKind.Vote))
                || (UnifiedCommitments.SafetyAuthorityOn(c.state) ? CommitmentReferences.Deals(c.state) : c.state.deals)
                    .Any(d => DealStatus.Binds(d.status) && d.type == DealKind.SafetyAgreement
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
                var rival = Among(c).Where(x => c.Grudge(x.id, P(c)) >= 40)
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
                SettleItTalk("approach", null, "again",
                    "{RIVAL} is sitting alone on the far couch. How do you start?"),
                SettleItTalk("again", StoryAnchors.EvictionEve, Waits,
                    "The vote is tomorrow, and {RIVAL} is still keeping {RIVAL.their} distance. One more try?"),
            },
        };

        /// <summary>
        /// A conversation with somebody holding a grudge: each way in lands differently with different
        /// people, and any that lands takes half a nomination's grudge with it. A grudge that big
        /// takes two.
        /// </summary>
        private static BeatTemplate SettleItTalk(string id, string anchor, string next, string text) => new BeatTemplate
        {
            id = id, anchor = anchor, surface = StorySurfaces.Approach, venue = Living, closes = StoryAnchors.EvictionNight,
            title = "Settle It", summary = "You try to settle a grudge.", text = text,
            lapse = "let-it-ride",
            options = new[]
            {
                Opt("apologise", "Apologise", Low, Yield,
                    "Tell {RIVAL} you are sorry, and mean it.",
                    "{RIVAL} accepted the apology, slowly.",
                    Ease("RIVAL", Player, 50), Move(Player, "RIVAL", 2))
                    .Checked(50, "RIVAL", "{RIVAL} took the apology as weakness.",
                        Grudge("RIVAL", Player, 10))
                    .Then(next),
                Opt("clear-the-air", "Clear the air", Medium, Hardball,
                    "Have it out with {RIVAL}, all of it, once.",
                    "It got loud, and then it got better. {RIVAL} let most of it go.",
                    Ease("RIVAL", Player, 50), Move(Player, "RIVAL", 2))
                    .Checked(50, "RIVAL", "It got loud and stayed loud.",
                        Grudge("RIVAL", Player, 15))
                    .Then(next),
                Opt("cook-for-them", "Cook them dinner", Low, Warm,
                    "Make {RIVAL}'s favourite and bring it over.",
                    "{RIVAL} ate every bite, and some of the grudge went with it.",
                    Ease("RIVAL", Player, 50), Move(Player, "RIVAL", 2))
                    .Checked(50, "RIVAL", "{RIVAL} ate it without a word.",
                        Move(Player, "RIVAL", 1))
                    .CostsAction().Then(next),
                Lapse("let-it-ride", "Let it ride", "Give it time.", "You gave it time.").Then(Waits),
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
                // Only for a player without numbers: no alliance of three or more yet. A pact of two
                // with either of them is not numbers; bringing both into one is the play.
                if (c.state.alliances.Any(a => a.active && a.members.Contains(P(c)) && a.members.Count >= 3)) return null;
                var warm = Among(c).Where(x => c.Score(x.id, P(c)) >= 12)
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
                goal = "Bring {FIRST} and {SECOND} into one alliance with you before the next eviction.",
                // All three in one alliance: a pact with one of them already, or two separate pacts, is
                // where the play starts, not a part of the way.
                progress = (c, x) => PlayProgress.Done(c.state.alliances.Any(a => a.active && a.members.Contains(P(c))
                    && a.members.Contains(x.Role("FIRST")) && a.members.Contains(x.Role("SECOND")))),
                wonOutcome = "You, {FIRST} and {SECOND} are an alliance now.",
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
                            .Checked(50, "FIRST", "{SECOND} thought it was too soon, and the moment passed.",
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
                var strangers = Among(c).Where(x => c.Score(P(c), x.id) >= 0 && !c.Real(x.id)
                                                   && Lore.Learned(c.state, x.id).Count == 0 && Lore.FactsOf(c.state, x.id).Count() >= 3)
                    .OrderByDescending(x => c.Score(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).Take(3).ToList();
                var subject = Keyed(c, "know-them", strangers);
                return subject == null ? null : Bind().With("SUBJECT", subject.id).Headlining(subject.id);
            },
            weight = (c, b) => 15,
            play = new PlayTemplate
            {
                currency = PlayCurrencies.Intel, deadline = StoryAnchors.EvictionNight,
                goal = "Learn three things about {SUBJECT} before the next eviction.",
                // Anything learned counts: this play's questions, or an ordinary conversation.
                progress = (c, x) => new PlayProgress(Lore.Learned(c.state, x.Role("SUBJECT")).Count, 3),
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

        /// <summary>
        /// A chance to learn about somebody: each approach reads differently with different people,
        /// and any that lands teaches two things - what was asked about, or failing that whatever
        /// else there is to know.
        /// </summary>
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
                    RevealMore("SUBJECT", Lore.Facets.Home), RevealMore("SUBJECT", Lore.Facets.Origin))
                    .Checked(50, "SUBJECT", "{SUBJECT} changed the subject.", Move(Player, "SUBJECT", -1))
                    .Then(next),
                Opt("share-first", "Share something of yours first", Low, Candid,
                    "Tell {SUBJECT} something real about yourself, and see what comes back.",
                    "You went first, and {SUBJECT} met you halfway.",
                    RevealMore("SUBJECT", Lore.Facets.Comfort), RevealMore("SUBJECT", Lore.Facets.Work))
                    .Checked(50, "SUBJECT", "{SUBJECT} listened, and kept {SUBJECT.their} own story to {SUBJECT.themselves}.")
                    .Then(next),
                Opt("talk-game", "Talk about why they came", Medium, Calculated,
                    "Ask {SUBJECT} what {SUBJECT.they} want out of this game.",
                    "{SUBJECT} told you what {SUBJECT.they} came here for.",
                    RevealMore("SUBJECT", Lore.Facets.Goal), RevealMore("SUBJECT", Lore.Facets.Respects))
                    .Checked(50, "SUBJECT", "{SUBJECT} did not like being asked, and said so.", Move(Player, "SUBJECT", -3))
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
                            .Checked(50, "NOMINEE", "You tried, but {NOMINEE} never heard about it.", Move(Player, "NOMINEE", 1))
                            .CostsAction().Then(Waits),
                        Opt("stand-up-for-them", "Stand up for them", Medium, Bold,
                            "Say it in front of the whole house: {NOMINEE} deserves to stay.",
                            "You said it where everybody could hear, and {NOMINEE} will remember who did.",
                            Hook(Player, "NOMINEE"), Move(Player, "NOMINEE", 2))
                            .Checked(50, "NOMINEE", "The house went quiet, and {NOMINEE} looked more embarrassed than grateful.",
                                Move(Player, "NOMINEE", -1))
                            .Then(Waits),
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
                var target = Among(c).OrderByDescending(x => x.hohWins + x.vetoWins).ThenByDescending(x => x.stats.competition)
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
                        Opt("tell-them", "Tell them what was said", High, Candid,
                            "Tell {PAWN} what {TARGET} has been saying about {PAWN.them}.",
                            "{PAWN} went very quiet, then very angry.",
                            Grudge("PAWN", "TARGET", 45, GrudgeCauses.Story))
                            .Checked(50, "PAWN", "{PAWN} went straight to {TARGET}, and {TARGET} knows exactly who started it.",
                                Grudge("TARGET", Player, 30, GrudgeCauses.Story), Move(Player, "PAWN", -4))
                            .Then(Waits),
                        Opt("plant-a-seed", "Plant a seed", Medium, Calculated,
                            "Wonder aloud whether {TARGET} really has {PAWN}'s back, and leave it there.",
                            "{PAWN} did not say anything, but {PAWN.they} watched {TARGET} differently after, and it grew.",
                            Grudge("PAWN", "TARGET", 45, GrudgeCauses.Story))
                            .Checked(50, "PAWN", "{PAWN} shrugged it off.", Move(Player, "PAWN", -1))
                            .Then(Waits),
                        Opt("joke-about-it", "Joke about it until it stings", Medium, Playful,
                            "Make {TARGET}'s last win the running joke, and let {PAWN} wonder why {TARGET} is laughing too.",
                            "The joke stopped being funny to {PAWN} somewhere around the third time.",
                            Grudge("PAWN", "TARGET", 45, GrudgeCauses.Story))
                            .Checked(50, "PAWN", "{PAWN} laughed along and thought nothing more of it.", Move(Player, "PAWN", -1))
                            .Then(Waits),
                        Lapse("leave-them", "Leave them be", "Not today.", "You left them to it.").Then(Waits),
                    },
                },
            },
        };
    }
}
