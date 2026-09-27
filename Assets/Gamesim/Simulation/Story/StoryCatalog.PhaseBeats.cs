using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;
using static Gamesim.Simulation.HouseEventRisk;
using static Gamesim.Simulation.Personality.Approach;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The web's phase beats (<c>phase-event-system.ts</c>): one card at a boundary of the week, at
    /// the web's chance and with the web's numbers, in Big Brother's words rather than the web's
    /// leftover Survivor ones. Where a choice is a commitment the plan gives it a consumer:
    /// "You have my vote." is a <c>vote_save</c> deal the reveal settles, and "What are you
    /// offering?" comes back with an offer (21 §4, G7).
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> PhaseBeats()
        {
            yield return TheLobby();
            yield return TensionsRise();
            yield return VetoDilemma();
            yield return TheFallout();
            yield return FinalPleas();
            yield return LastMinuteCampaign();
            yield return HouseDrama();
        }

        /// <summary>A recurring one-card moment: rests a week, and anybody may headline it again.</summary>
        private static ArcTemplate PhaseMoment(string id, string title, string origin, string anchor, double webChance,
            ArcRole[] roles, Func<StoryContext, ArcBinding> cast, BeatTemplate beat, int minActive = 3) => new ArcTemplate
        {
            id = id, lane = StoryLanes.Moment, eyebrow = "This Week", title = title, origin = origin,
            rulesVersion = StoryRules.Grudges, minActive = minActive, startAnchors = new[] { anchor },
            oncePerHeadliner = false, cooldownWeeks = 1, pairCooldownWeeks = 1,
            roles = roles, cast = cast, weight = (c, b) => Web(webChance), beats = new[] { beat },
        };

        private static OptionTemplate NotNow(string outcome = "You let the moment pass.") =>
            Lapse("not-now", "Not now", "Let the moment pass.", outcome);

        // ---------------------------------------------------------------- the lobby

        private static ArcTemplate TheLobby() => PhaseMoment("the-lobby", "The Lobby",
            "web:src/systems/phase-event-system.ts:226-254 (pre-Nomination, 0.5)", StoryAnchors.HohCrowned, 0.5,
            new[] { Role("LOBBYIST"), Role("HOH") },
            c =>
            {
                string hoh = NpcHoh(c);
                var lobbyist = hoh == null ? null : Keyed(c, "lobbyist", Npcs(c).Where(x => x.id != hoh).ToList());
                return lobbyist == null ? null : Bind().With("LOBBYIST", lobbyist.id).With("HOH", hoh).Headlining(lobbyist.id);
            },
            new BeatTemplate
            {
                id = "lobby", surface = StorySurfaces.Approach, venue = Hallway, closes = StoryAnchors.NomsSet,
                title = "The Lobby", summary = "Somebody wants to talk strategy before nominations.",
                text = "Nominations are coming. {LOBBYIST} catches you in the hallway. \"{HOH} is naming names soon. We need to talk. Now.\"",
                lapse = "not-now",
                options = new[]
                {
                    Opt("listen", "Listen to their pitch", Low, Warm, "Hear who {LOBBYIST} wants on the block.",
                        "{LOBBYIST} made the case with real passion, and you filed every word away.", Move(Player, "LOBBYIST", 3), SocialBonus(2)),
                    Opt("counter-lobby", "Counter-lobby", High, Bold, "Push your own agenda instead.",
                        "You turned the conversation to your targets. {LOBBYIST} came to move you and left moved.", Move(Player, "LOBBYIST", -2)),
                    NotNow(),
                },
            });

        // ---------------------------------------------------------------- tensions rise

        private static ArcTemplate TensionsRise() => PhaseMoment("tensions-rise", "Tensions Rise",
            "web:src/systems/phase-event-system.ts:81-108 (post-Nomination, 0.6)", StoryAnchors.NomsSet, 0.6,
            new[] { Role("CONFIDANT") },
            c =>
            {
                string confidant = Warmest(c, x => x.id != c.state.hohId && !Nominated(c, x.id));
                return confidant == null ? null : Bind().With("CONFIDANT", confidant).Headlining(confidant);
            },
            new BeatTemplate
            {
                id = "tensions", surface = StorySurfaces.Approach, venue = Hallway,
                title = "Tensions Rise", summary = "The nominations are out, and somebody wants to talk about the vote.",
                text = "The nomination ceremony has just ended, and two people are sitting in the chairs nobody wants. {CONFIDANT} pulls you aside in the hallway.",
                lapse = "not-now",
                options = new[]
                {
                    Opt("listen-plan", "Listen to their plan", Low, Calculated, "Hear what {CONFIDANT} has in mind for the vote.",
                        "{CONFIDANT} laid out the vote in a whisper. You nodded along and gave nothing away.", SocialBonus(2)),
                    Opt("share-thoughts", "Share your own thoughts", Medium, Candid, "Say where you stand. It could build trust, or travel.",
                        "You told {CONFIDANT} exactly where you stand. The bond is deeper; the secret is out of your hands.", Move(Player, "CONFIDANT", 5)),
                    NotNow(),
                },
            });

        // ---------------------------------------------------------------- the veto holder's dilemma

        private static ArcTemplate VetoDilemma() => PhaseMoment("veto-dilemma", "The Veto Holder's Dilemma",
            "web:src/systems/phase-event-system.ts:151-176 (post-PoV, 0.5); the veto commitment on a won pitch native",
            StoryAnchors.VetoWon, 0.5,
            new[] { Role("HOLDER") },
            c =>
            {
                string holder = NpcVetoHolder(c);
                return holder == null ? null : Bind().With("HOLDER", holder).Headlining(holder);
            },
            new BeatTemplate
            {
                id = "dilemma", surface = StorySurfaces.Approach, venue = Storage, closes = StoryAnchors.BlockSet,
                title = "The Veto Holder's Dilemma", summary = "The veto holder is weighing their options.",
                text = "{HOLDER} is wearing the Power of Veto around {HOLDER.their} neck. You catch {HOLDER.them} in the storage room, clearly weighing what to do with it.",
                lapse = "respect",
                options = new[]
                {
                    Opt("lobby", "Lobby them to use it", Medium, Hardball, "Push {HOLDER} to use the veto. It could save somebody, or make a mess.",
                        "You made your case, and {HOLDER} heard it. If you are on the block, {HOLDER} has now said {HOLDER.they} would use it on you.",
                        Move(Player, "HOLDER", -3))
                        .Extra((c, y) => PlayerNominated(c) ? new[] { Deal("HOLDER", Player, DealKind.VetoUse) } : Enumerable.Empty<Fx>())
                        .Checked(45, "HOLDER", "{HOLDER} did not appreciate the pressure, and said so.", Move(Player, "HOLDER", -3))
                        .Bonus("Charming", "Social", "Strategic"),
                    Opt("respect", "Respect their decision", Low, Yield, "Don't pressure {HOLDER}. It builds goodwill.",
                        "\"Whatever you decide, I respect it.\" {HOLDER} visibly relaxed.", Move(Player, "HOLDER", 4)),
                },
            });

        // ---------------------------------------------------------------- the fallout

        private static ArcTemplate TheFallout() => PhaseMoment("the-fallout", "The Fallout",
            "web:src/systems/phase-event-system.ts:259-285 (post-VetoMeeting, 0.5)", StoryAnchors.BlockSet, 0.5,
            new[] { Role("NOMINEE") },
            c =>
            {
                var nominee = Keyed(c, "nominee", NpcNominees(c));
                return nominee == null ? null : Bind().With("NOMINEE", nominee.id).Headlining(nominee.id);
            },
            new BeatTemplate
            {
                id = "fallout", surface = StorySurfaces.Scene, venue = Living,
                title = "The Fallout", summary = "The veto meeting has left a nominee barely holding it together.",
                text = "The veto meeting just ended. {NOMINEE} storms through the living room, barely holding it together, and the house watches in tense silence.",
                lapse = "distance",
                options = new[]
                {
                    Opt("comfort", "Offer comfort", Low, Warm, "Catch up with {NOMINEE} and offer support.",
                        "You found {NOMINEE} in the bedroom. \"Hey. I know this is hard.\" It was genuine, and in this game that is rare.",
                        Move(Player, "NOMINEE", 7)),
                    Opt("distance", "Keep your distance", Low, Calculated, "Getting close to a nominee is dangerous.",
                        "You watched from across the room and did not go over. Self-preservation isn't pretty, but it works."),
                },
            });

        // ---------------------------------------------------------------- final pleas

        private static ArcTemplate FinalPleas() => PhaseMoment("final-pleas", "Final Pleas",
            "web:src/systems/phase-event-system.ts:290-324 (pre-Vote, 0.6); the offer native (21 G7)", StoryAnchors.EvictionEve, 0.6,
            new[] { Role("NOMINEE") },
            c =>
            {
                if (!PlayerVotes(c)) return null;
                var nominee = Keyed(c, "desperate", NpcNominees(c));
                return nominee == null ? null : Bind().With("NOMINEE", nominee.id).Headlining(nominee.id);
            },
            new BeatTemplate
            {
                id = "pleas", surface = StorySurfaces.Approach, venue = Bedroom,
                title = "Final Pleas", summary = "A nominee wants to know where your head is at before the vote.",
                text = "Minutes before the vote, {NOMINEE} pulls you aside. The voice is steady; the eyes are not. \"I need to know where your head is at. Right now.\"",
                lapse = "not-now",
                options = new[]
                {
                    Opt("reassure", "\"You have nothing to worry about.\"", Low, Warm, "Reassure {NOMINEE}, whether you mean it or not.",
                        "{NOMINEE} exhaled. \"Thank you. I won't forget this.\"", Move(Player, "NOMINEE", 6)),
                    Opt("house", "\"I have to vote with the house.\"", Medium, Candid, "The honest answer, even if it hurts.",
                        "{NOMINEE} nodded slowly. \"I understand.\" The hurt was plain, and {NOMINEE} will remember it, from the house or from the jury.",
                        Move(Player, "NOMINEE", -5)),
                    Opt("offering", "\"What are you offering?\"", High, Hardball, "Use the desperation as leverage.",
                        "{NOMINEE} hesitated, then started pitching. If {NOMINEE} survives the night, you are safe next week.",
                        SocialBonus(3), Deal("NOMINEE", Player, DealKind.SafetyAgreement)),
                    NotNow(),
                },
            });

        // ---------------------------------------------------------------- last-minute campaign

        private static ArcTemplate LastMinuteCampaign() => PhaseMoment("last-minute-campaign", "Last-Minute Campaign",
            "web:src/systems/phase-event-system.ts:113-146 (pre-Eviction, 0.65); the vote_save deal completes web intent (21 D-G)",
            StoryAnchors.EvictionEve, 0.65,
            new[] { Role("NOMINEE") },
            c =>
            {
                if (!PlayerVotes(c)) return null;
                var nominee = NpcNominees(c).FirstOrDefault();
                return nominee == null ? null : Bind().With("NOMINEE", nominee.id).Headlining(nominee.id);
            },
            new BeatTemplate
            {
                id = "campaign", surface = StorySurfaces.Approach, venue = Kitchen,
                title = "Last-Minute Campaign", summary = "A nominee comes to you for your vote.",
                text = "It's eviction night. {NOMINEE} comes to find you, and the desperation is right there. \"I need your vote. Please.\"",
                lapse = "undecided",
                options = new[]
                {
                    Opt("my-vote", "\"You have my vote.\"", Medium, Warm, "Promise to keep {NOMINEE}. The vote reveal will show whether you meant it.",
                        "{NOMINEE}'s face flooded with relief. You have made a promise; whether you keep it is another matter.",
                        Move(Player, "NOMINEE", 8), Deal(Player, "NOMINEE", DealKind.VoteSave, "NOMINEE")),
                    Opt("undecided", "\"I haven't decided yet.\"", Low, Calculated, "Keep your options open.",
                        "{NOMINEE}'s hope faltered. \"I understand.\" The hesitation will not be forgotten.", Move(Player, "NOMINEE", -2)),
                    Opt("cant", "\"I'm sorry, I can't.\"", High, Candid, "Be honest. It hurts now and earns respect later.",
                        "{NOMINEE} stared at you for a long moment. \"At least you're honest.\"", Move(Player, "NOMINEE", -10)),
                },
            });

        // ---------------------------------------------------------------- house drama

        private static ArcTemplate HouseDrama() => PhaseMoment("house-drama", "House Drama",
            "web:src/systems/phase-event-system.ts:329-370 (mid-Social, 0.4); the accusation's cost between them native",
            StoryAnchors.EvictionNight, 0.4,
            new[] { Role("ACCUSER"), Role("ACCUSED") },
            c =>
            {
                var npcs = Npcs(c);
                if (npcs.Count < 3) return null;
                var accuser = Keyed(c, "accuser", npcs);
                var accused = Keyed(c, "accused", npcs.Where(x => x.id != accuser.id).ToList());
                return Bind().With("ACCUSER", accuser.id).With("ACCUSED", accused.id).Headlining(accuser.id);
            },
            new BeatTemplate
            {
                id = "drama", surface = StorySurfaces.Scene, venue = Kitchen,
                title = "House Drama", summary = "Somebody just called somebody out in front of the house.",
                text = "{ACCUSER} just called {ACCUSED} out in the kitchen: \"Everyone knows you've been playing both sides!\" The house freezes, and every eye turns to you.",
                lapse = "stay-out",
                options = new[]
                {
                    Opt("back-accuser", "Back the accuser", High, Bold, "Side with {ACCUSER}. Risky, but it shows strength.",
                        "\"They're right.\" {ACCUSED}'s face fell; {ACCUSER} looked vindicated. You have drawn a very public line.",
                        Move(Player, "ACCUSER", 5), Told("ACCUSED", Player, -3, null), Move("ACCUSER", "ACCUSED", -4)),
                    Opt("defend-accused", "Defend the accused", Medium, Warm, "Stand up for {ACCUSED}. It earns loyalty.",
                        "\"That's not fair.\" {ACCUSED} looked at you with real gratitude; {ACCUSER} fumed. A friend and an enemy, in one sentence.",
                        Move(Player, "ACCUSED", 8), Told("ACCUSER", Player, -3, null), Move("ACCUSER", "ACCUSED", -4)),
                    Opt("stay-out", "Stay out of it", Low, Calculated, "Not your fight. Watch and learn.",
                        "You stepped back and let it play out. Wisdom or cowardice, depending on who you ask.",
                        SocialBonus(1), Move("ACCUSER", "ACCUSED", -4)),
                },
            });
    }
}
