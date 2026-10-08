// Literal vocabulary retained from the committed f456 Frozen24 snapshot SHA256 f2f9574d18745eb609e30c4281795af5d059bf84d28831517406668474a17a14.
// No live future enum or string catalog is consulted.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen25Data;

namespace Gamesim.Persistence
{
    /// <summary>Private schema-24 vocabularies copied from 6ea7d2ec; future public tables cannot widen them.</summary>
    internal static partial class FrozenV25Validator
    {
        // Pinned 6ea7d2ec: DealState.cs
        private static class DealKind
            {
                public const string TargetAgreement = "target_agreement";
                public const string SafetyAgreement = "safety_agreement";
                public const string VoteTogether = "vote_together";
                public const string VoteSave = "vote_save";
                public const string VoteEvict = "vote_evict";
                public const string VetoUse = "veto_use";
                public const string InformationSharing = "information_sharing";
                public const string FinalTwo = "final_two";
                public const string Partnership = "partnership";
                public const string AllianceInvite = "alliance_invite";

                /// <summary>
                /// Native, not the reference's (ACTIONS-DEALS-ALLIANCES-PLAN C9): two houseguests take each other
                /// to the final three. It binds what a safety pact binds - neither puts the other up, at the
                /// nominations or as the veto's replacement, which breaks it - from the week it is struck until
                /// the house is down to three, and it is weighed where a safety pact is: in a Head of
                /// Household's reluctance to nominate the partner and in a ballot on them. It is kept when the
                /// two of them reach the final three together, and ends, blaming nobody, with whichever of them
                /// leaves the house first (X4). A kept one is an obligation in the final Head of Household's
                /// choice at half a final two's (<see cref="EpisodeEngine.FinalChoiceTerms"/>). Put only from
                /// the final six to the final four, and only under the commitment rules: a season without them
                /// never holds one, and validation refuses one there.
                /// </summary>
                public const string FinalThree = "final_three";

                public static readonly string[] All =
                {
                    TargetAgreement, SafetyAgreement, VoteTogether, VoteSave, VoteEvict,
                    VetoUse, InformationSharing, FinalTwo, Partnership, AllianceInvite,
                    FinalThree,
                };

                public static bool IsKnown(string type) => type != null && Array.IndexOf(All, type) >= 0;

                /// <summary>Whether this kind exists only under the commitment rules (<see cref="EpisodeEngine.CommitmentRulesOn"/>): the final three deal.</summary>
                public static bool CommitmentRulesOnly(string type) => type == FinalThree;

                /// <summary>Whether this kind of deal is about a third houseguest rather than the pair.</summary>
                public static bool NamesATarget(string type) =>
                    type == TargetAgreement || type == VoteEvict || type == VoteSave;

                /// <summary>
                /// What the reference's <c>DEAL_TYPE_INFO</c> gives each type as its default trust weight.
                /// </summary>
                public static string DefaultTrust(string type)
                {
                    switch (type)
                    {
                        case VetoUse:
                        case FinalTwo: return DealTrust.Critical;
                        case TargetAgreement:
                        case SafetyAgreement:
                        case AllianceInvite:
                        // The safety pact's weight: it is broken by the same act, a nomination.
                        case FinalThree: return DealTrust.High;
                        case InformationSharing: return DealTrust.Low;
                        default: return DealTrust.Medium;
                    }
                }

                /// <summary>
                /// The title the reference shows, used for the log line a deal writes - and, lowered, for
                /// the caption that proposes one ("Propose a voting bloc"). One departure from the
                /// reference's spelling: its "Voting Block" is a voting bloc (UI-UX-PASS-PLAN D0, the play
                /// sweep's row 31); <see cref="Titles"/> keeps the old spelling for the lines written under it.
                /// </summary>
                public static string Title(string type)
                {
                    switch (type)
                    {
                        case TargetAgreement: return "Target Agreement";
                        case SafetyAgreement: return "Safety Pact";
                        case VoteTogether: return "Voting Bloc";
                        case VoteSave: return "Vote to Save";
                        case VoteEvict: return "Vote to Evict";
                        case VetoUse: return "Veto Commitment";
                        case InformationSharing: return "Information Sharing";
                        case FinalTwo: return "Final Two Deal";
                        case AllianceInvite: return "Alliance Invitation";
                        case FinalThree: return "Final Three Deal";
                        default: return "Partnership";
                    }
                }

                /// <summary>
                /// Every spelling a deal's title has been written under, the current one first. A line of
                /// record - a ledger entry, a memory - is matched by rebuilding the engine's own sentence
                /// from the title (<see cref="YourWeek.ReadDeal"/>, <see cref="KnownBallots.TellsAnUnknownBallot"/>),
                /// so a reader tries each spelling: a season that settled a "voting block" before the word
                /// was corrected still reads as what it was.
                /// </summary>
                public static IEnumerable<string> Titles(string type)
                {
                    yield return Title(type);
                    if (type == VoteTogether) yield return "Voting Block";
                }
            }

        // Pinned 6ea7d2ec: DealState.cs
        private static class DealStatus
            {
                public const string Proposed = "proposed";
                public const string Accepted = "accepted";
                public const string Active = "active";
                public const string Fulfilled = "fulfilled";
                public const string Broken = "broken";
                public const string Declined = "declined";
                public const string Expired = "expired";

                public static readonly string[] All =
                    { Proposed, Accepted, Active, Fulfilled, Broken, Declined, Expired };

                public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;

                /// <summary>Whether the deal still binds anybody.</summary>
                public static bool Binds(string status) => status == Proposed || status == Accepted || status == Active;
            }

        // Pinned 6ea7d2ec: DealState.cs
        private static class DealTrust
            {
                public const string Low = "low", Medium = "medium", High = "high", Critical = "critical";
                public static readonly string[] All = { Low, Medium, High, Critical };
                public static bool IsKnown(string trust) => trust != null && Array.IndexOf(All, trust) >= 0;

                /// <summary>
                /// What keeping or breaking a deal of this weight is worth on the ledger.
                ///
                /// <para>The reference's own <c>impactMultiplier</c> in <c>applyDealOutcome</c>: low 1,
                /// medium 1.5, high 2, critical 3. Applied to <see cref="DealResolution.FulfilledBase"/> and
                /// <see cref="DealResolution.BrokenBase"/>, a broken veto commitment moves −45 and a broken
                /// information swap −15 — which is why <c>trustImpact</c> exists at all, and why walking
                /// away from the block costs three times what walking away from gossip does.</para>
                /// </summary>
                public static double Weight(string trust)
                {
                    switch (trust)
                    {
                        case Critical: return 3;
                        case High: return 2;
                        case Medium: return 1.5;
                        default: return 1;
                    }
                }

                /// <summary>
                /// One step heavier than <paramref name="trust"/>: low to medium, medium to high, high to
                /// critical. Critical is the heaviest there is and stays critical; anything unknown reads as
                /// low, as <see cref="Weight"/> reads it, and goes to medium. What an offer the player
                /// accepted stakes when it breaks, under the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN
                /// C1, decision 15; <see cref="DealResolution.BreachWeight"/>).
                /// </summary>
                public static string Heavier(string trust)
                {
                    switch (trust)
                    {
                        case Critical:
                        case High: return Critical;
                        case Medium: return High;
                        default: return Medium;
                    }
                }
            }

        // Pinned 6ea7d2ec: HouseEventState.cs
        private static class HouseEventKind
            {
                /// <summary>Something the phase itself throws up — the reference's <c>phase-event-system</c>.</summary>
                public const string Phase = "phase";

                /// <summary>A situation in the house at large — <c>house-event-system</c>.</summary>
                public const string House = "house";

                /// <summary>Two houseguests in the same room — <c>proximity-event-system</c>.</summary>
                public const string Proximity = "proximity";

                /// <summary>Background colour with no decision attached — <c>ambient-event-system</c>.</summary>
                public const string Ambient = "ambient";

                /// <summary>Something the season's own state made inevitable — <c>emergent-event-system</c>.</summary>
                public const string Emergent = "emergent";

                /// <summary>The week going wrong — <c>mid-week-crisis-system</c>.</summary>
                public const string Crisis = "crisis";

                /// <summary>
                /// A beat of a story cycle (schema 14). Answered by <c>ProgressStoryline</c> with an option
                /// id, never by <c>ResolveHouseEvent</c>'s label match.
                /// </summary>
                public const string Story = "story";

                /// <summary>
                /// Something the player did in the house: HOUSE-LIFE-PLAN's queued kind, appended in the same
                /// schema-14 bundle so the format needs one migration rather than two.
                /// </summary>
                public const string Activity = "activity";

                // Append only: an older build rejects a save holding a kind it does not know, which is the
                // honest answer; reordering would make it reject kinds it did know.
                public static readonly string[] All = { Phase, House, Proximity, Ambient, Emergent, Crisis, Story, Activity };
                public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;

                /// <summary>
                /// Whether this kind asks the player anything.
                ///
                /// <para>An ambient event is told, not answered. Giving it choices would make the house feel
                /// like a questionnaire, which is the failure mode the reference avoids by having a kind
                /// that only ever narrates.</para>
                /// </summary>
                public static bool Asks(string kind) => kind != Ambient;
            }

        // Pinned 6ea7d2ec: HouseEventState.cs
        private static class HouseEventRisk
            {
                public const string Low = "low", Medium = "medium", High = "high";
                public static readonly string[] All = { Low, Medium, High };
                public static bool IsKnown(string risk) => risk != null && Array.IndexOf(All, risk) >= 0;
            }

        // Pinned 6ea7d2ec: StorylineState.cs
        private static class StorylineStatus
            {
                public const string Active = "active";
                public const string Completed = "completed";

                /// <summary>
                /// Left behind rather than finished.
                ///
                /// <para>The reference drops a storyline the player has ignored for three weeks. A thread
                /// nobody picked up is not a thread that failed, and recording it as abandoned rather than
                /// completed keeps the cooldown honest about what actually happened.</para>
                /// </summary>
                public const string Abandoned = "abandoned";

                public static readonly string[] All = { Active, Completed, Abandoned };
                public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
                public static bool Running(string status) => status == Active;
            }

        // Pinned 6ea7d2ec: StrategyRules.cs
        private static class LobbyAsk
            {
                /// <summary>To the Head of Household: leave me off the block (or out of a saved nominee's place).</summary>
                public const string Spare = "spare";
                /// <summary>To the Head of Household: put this houseguest up.</summary>
                public const string Target = "target";
                /// <summary>To the veto holder: use it on this nominee.</summary>
                public const string Save = "save";
                /// <summary>To the veto holder: keep the nominations the same.</summary>
                public const string Keep = "keep";
                /// <summary>To a voter, from the block: vote to keep me (STRATEGY-LOOP-PLAN.md §3).</summary>
                public const string Vote = "vote";

                public static readonly string[] All = { Spare, Target, Save, Keep, Vote };
                public static bool IsKnown(string ask) => ask != null && Array.IndexOf(All, ask) >= 0;

                /// <summary>A plea travels in a command's text as "ask/approach".</summary>
                public static string Encode(string ask, string approach) => ask + "/" + approach;

                public static bool TryDecode(string text, out string ask, out string approach)
                {
                    ask = approach = null;
                    var parts = (text ?? string.Empty).Trim().Split('/');
                    if (parts.Length != 2 || !IsKnown(parts[0]) || !LobbyApproach.IsKnown(parts[1])) return false;
                    ask = parts[0]; approach = parts[1];
                    return true;
                }
            }

        // Pinned 6ea7d2ec: StrategyRules.cs
        private static class LobbyApproach
            {
                public const string Emotional = "emotional", Strategic = "strategic", Deal = "deal", Pressure = "pressure";
                public static readonly string[] All = { Emotional, Strategic, Deal, Pressure };
                public static bool IsKnown(string approach) => approach != null && Array.IndexOf(All, approach) >= 0;

                /// <summary>Each card's base chance, in percent. The reference's numbers.</summary>
                public static double Base(string approach) =>
                    approach == Emotional ? 55 : approach == Strategic ? 50 : approach == Deal ? 60 : approach == Pressure ? 35 : 0;

                private static readonly Dictionary<string, (string[] suits, string[] grates)> Fits = new Dictionary<string, (string[], string[])>
                {
                    [Emotional] = (new[] { "Emotional", "Loyal", "Social", "Charming" }, new[] { "Strategic", "Analytical", "Manipulative", "Stubborn" }),
                    [Strategic] = (new[] { "Strategic", "Analytical", "Flexible", "Intuitive" }, new[] { "Emotional", "Impulsive", "Stubborn" }),
                    [Deal] = (new[] { "Strategic", "Flexible", "Charming", "Manipulative" }, new[] { "Loyal", "Stubborn", "Confrontational" }),
                    [Pressure] = (new[] { "Impulsive", "Emotional", "Flexible" }, new[] { "Stubborn", "Confrontational", "Strategic", "Analytical" }),
                };

                /// <summary>Fifteen for each trait the card suits and ten off for each it grates on. The reference's lists.</summary>
                public static double TraitFit(IEnumerable<string> traits, string approach)
                {
                    if (!Fits.TryGetValue(approach ?? string.Empty, out var fit)) return 0;
                    double total = 0;
                    foreach (string trait in traits ?? Enumerable.Empty<string>())
                    {
                        if (fit.suits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase))) total += 15;
                        if (fit.grates.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase))) total -= 10;
                    }
                    return total;
                }
            }

        // Pinned 6ea7d2ec: StrategyRules.cs
        private static class LobbyResponse
            {
                public const string Receptive = "receptive", Open = "open", Skeptical = "skeptical", Hostile = "hostile";
                public static readonly string[] All = { Receptive, Open, Skeptical, Hostile };
                public static bool IsKnown(string response) => response != null && Array.IndexOf(All, response) >= 0;
            }

        // Pinned 6ea7d2ec: SeasonLedger.cs
        private static class ClaimSource
            {
                public const string Told = "told", Overheard = "overheard", Ally = "ally", Read = "read";
                /// <summary>A juror's view of the player as they left, kept for the verdict.</summary>
                public const string Juror = "juror";
                /// <summary>A read that missed: the attempt is on the record (once a week is once a week), and nothing was learned.</summary>
                public const string Missed = "missed";
                /// <summary>A voter asked straight who kept it to themselves: the week's ask, on the record, and nothing learned.</summary>
                public const string Deflected = "deflected";
                public static readonly string[] All = { Told, Overheard, Ally, Read, Juror, Missed, Deflected };
                /// <summary>Whether a standing row of this source taught the player nothing: an attempt on the record, not a standing.</summary>
                public static bool IsAttempt(string source) => source == Missed || source == Deflected;
                public static bool IsKnown(string source) => source != null && Array.IndexOf(All, source) >= 0;
            }

        // Pinned 6ea7d2ec: SeasonLedger.cs
        private static class ClaimStatus
            {
                public const string Open = "open", Kept = "kept", Lied = "lied";
                public static readonly string[] All = { Open, Kept, Lied };
                public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
            }

        // Pinned 6ea7d2ec: SeasonLedger.cs
        private static class OpportunityKinds
            {
                public const string Play = "play", Lobby = "lobby", Plea = "plea", Deal = "deal", Alliance = "alliance",
                    Vote = "vote", Comp = "comp", Read = "read";
                public static readonly string[] All = { Play, Lobby, Plea, Deal, Alliance, Vote, Comp, Read };
                public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
            }

        // Pinned 6ea7d2ec: SeasonLedger.cs
        private static class OpportunityResponse
            {
                public const string Taken = "taken", Declined = "declined", Ignored = "ignored", Expired = "expired";
                public static readonly string[] All = { Taken, Declined, Ignored, Expired };
                public static bool IsKnown(string response) => response != null && Array.IndexOf(All, response) >= 0;
            }

        // Pinned 6ea7d2ec: SeasonLedger.cs
        private static class OpportunityOutcome
            {
                public const string Won = "won", Part = "part", Lost = "lost", NotApplicable = "na";
                public static readonly string[] All = { Won, Part, Lost, NotApplicable };
                public static bool IsKnown(string outcome) => outcome != null && Array.IndexOf(All, outcome) >= 0;
            }

        // Pinned 6ea7d2ec: SeasonLedger.cs
        private static class PayoffCurrency
            {
                public const string Trust = "trust", Intel = "intel", Alliance = "alliance", Power = "power";
                public static readonly string[] All = { Trust, Intel, Alliance, Power };
                public static bool IsKnown(string currency) => currency != null && Array.IndexOf(All, currency) >= 0;
            }

        // Pinned 6ea7d2ec: SeasonLedger.cs
        private static class CompetitionEntry
            {
                public const string Played = "played", Assist = "assist", Simulated = "simulated", Thrown = "thrown", Watched = "watched";
                public static readonly string[] All = { Played, Assist, Simulated, Thrown, Watched };
                public static bool IsKnown(string entry) => entry != null && Array.IndexOf(All, entry) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class StoryRules
            {
                /// <summary>Beats, the spine, receipts, odds and the phase beats (M1).</summary>
                public const int Spine = 1;
                /// <summary>Grudges and what reads them (M2).</summary>
                public const int Grudges = 2;
                /// <summary>Staging, the backdoor plan and the Ringer (M3).</summary>
                public const int Staging = 3;
                /// <summary>Lore, rapport and goals (M4).</summary>
                public const int Lore = 4;
                /// <summary>Bonds, showmances, knowledge and hooks (M5).</summary>
                public const int Bonds = 5;
                /// <summary>Production: conduct, Have-Nots and removal (M6).</summary>
                public const int Production = 6;
                /// <summary>Plays: arcs with a goal you win or lose, and the receipts that say what they changed (plan 30).</summary>
                public const int Plays = 7;
                /// <summary>
                /// Reach (plan 30 P4): the fixes that let arcs a season could never cast come round - a beat
                /// postponed at eviction night tries again, NPC pairs count as blocs, allies whisper, the
                /// block names the nominee still in the house.
                /// </summary>
                public const int Reach = 8;
                /// <summary>Threads (plan 31): two or three season-long stories seeded from the cast, told in chapters.</summary>
                public const int Threads = 9;

                /// <summary>What a season switched on today plays under.</summary>
                public const int Current = Threads;

                public const int Lenient = 0, Standard = 1;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class StoryLanes
            {
                public const string Personal = "personal", Conflict = "conflict", Game = "game", Production = "production";
                public const string Moment = "moment", Play = "play", Thread = "thread";
                public const int MaxPlays = 3;
                /// <summary>A season's threads (plan 31): the bond, the rivalry and the numbers, one of each at most.</summary>
                public const int MaxThreads = 3;
                public static readonly string[] All = { Personal, Conflict, Game, Production, Moment, Play, Thread };
                public static bool IsKnown(string lane) => lane != null && Array.IndexOf(All, lane) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class StoryAnchors
            {
                public const string HohCrowned = "hoh-crowned";
                public const string NomsSet = "noms-set";
                public const string VetoWon = "veto-won";
                public const string BlockSet = "block-set";
                public const string EvictionEve = "eviction-eve";
                public const string EvictionNight = "eviction-night";
                public const string SocialClose = "social-close";
                public const string Conversation = "conversation";

                public static readonly string[] All =
                    { HohCrowned, NomsSet, VetoWon, BlockSet, EvictionEve, EvictionNight, SocialClose, Conversation };

                public static bool IsKnown(string anchor) => anchor != null && Array.IndexOf(All, anchor) >= 0;

                /// <summary>The order the anchors come round in a week, for "the next anchor after this one".</summary>
                public static int Order(string anchor)
                {
                    switch (anchor)
                    {
                        case HohCrowned: return 0;
                        case NomsSet: return 1;
                        case VetoWon: return 2;
                        case BlockSet: return 3;
                        case EvictionEve: return 4;
                        case EvictionNight: return 5;
                        case SocialClose: return 6;
                        default: return -1;
                    }
                }
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class StorySurfaces
            {
                /// <summary>The house acts it out in a room and the player may step in.</summary>
                public const string Scene = "scene";
                /// <summary>A houseguest comes to the player.</summary>
                public const string Approach = "approach";
                /// <summary>It happens inside a conversation the player opened.</summary>
                public const string Conversation = "conversation";
                /// <summary>The Diary Room calls.</summary>
                public const string Summons = "summons";
                /// <summary>Three or more gathered in one room.</summary>
                public const string Meeting = "meeting";
                /// <summary>Nothing for the player to answer: an NPC holds the choice.</summary>
                public const string Npc = "npc";

                public static readonly string[] All = { Scene, Approach, Conversation, Summons, Meeting, Npc };
                public static bool IsKnown(string surface) => surface != null && Array.IndexOf(All, surface) >= 0;
                public static bool Asks(string surface) => surface != Npc;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class StoryResults
            {
                public const string Plain = "plain", Success = "success", Backfire = "backfire", Lapsed = "lapsed", Npc = "npc";
                public static readonly string[] All = { Plain, Success, Backfire, Lapsed, Npc };
                public static bool IsKnown(string result) => result != null && Array.IndexOf(All, result) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class BondKinds
            {
                public const string RideOrDie = "ride-or-die", Confidant = "confidant", Nemesis = "nemesis", Showmance = "showmance";
                public static readonly string[] All = { RideOrDie, Confidant, Nemesis, Showmance };
                public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class BondStatus
            {
                /// <summary>Formed, and only the two of them know it.</summary>
                public const string Private = "private";
                /// <summary>Formed, and the house knows.</summary>
                public const string Public = "public";
                /// <summary>One of them has left the house; the bond still reads on the jury.</summary>
                public const string Apart = "apart";
                public const string Ended = "ended";
                /// <summary>Ended by a betrayal, which the jury reads differently from a quiet end.</summary>
                public const string Betrayed = "betrayed";
                public static readonly string[] All = { Private, Public, Apart, Ended, Betrayed };
                public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
                public static bool Holds(string status) => status == Private || status == Public || status == Apart;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class FactVisibility
            {
                public const string Private = "private", Whispered = "whispered", Known = "known", Public = "public";
                public static readonly string[] All = { Private, Whispered, Known, Public };
                public static bool IsKnown(string visibility) => visibility != null && Array.IndexOf(All, visibility) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class FactKinds
            {
                public const string Alliance = "alliance", Couple = "couple", BrokenWord = "broken-word", Strike = "strike";
                public const string Outcome = "outcome", Secret = "secret", Plan = "plan", Conversation = "conversation";
                public static readonly string[] All = { Alliance, Couple, BrokenWord, Strike, Outcome, Secret, Plan, Conversation };
                public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class GrudgeCauses
            {
                public const string Nominated = "nominated", Replacement = "replacement", ReplacementVeto = "replacement-veto";
                public const string AllianceBetrayed = "alliance-betrayed", DealBroken = "deal-broken", PromiseBroken = "promise-broken";
                public const string OathBroken = "oath-broken", LieDiscovered = "lie-discovered", VotedAgainst = "voted-against";
                public const string Story = "story", PileOn = "pile-on";
                public static readonly string[] All =
                {
                    Nominated, Replacement, ReplacementVeto, AllianceBetrayed, DealBroken, PromiseBroken,
                    OathBroken, LieDiscovered, VotedAgainst, Story, PileOn,
                };
                public static bool IsKnown(string cause) => cause != null && Array.IndexOf(All, cause) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryState.cs
        private static class LoreSources
            {
                public const string Authored = "authored", Legacy = "legacy", Derived = "derived";
                public static readonly string[] All = { Authored, Legacy, Derived };
                public static bool IsKnown(string source) => source != null && Array.IndexOf(All, source) >= 0;
            }

        // Pinned 6ea7d2ec: Story/StoryEffects.cs
        private static class StoryEffects
            {
                /// <summary>A placeholder for the houseguest a pick-a-person option's command names.</summary>
                public const string Picked = "{PICK}";

                /// <summary>Scores both ways. Through the player's arcs when the player is in it, the NPC ledger otherwise.</summary>
                public const string Move = "move";
                /// <summary>One houseguest's private view of another. Read by every evaluator that reads a score.</summary>
                public const string View = "view";
                /// <summary>A one-way ledger mark of type story:*. Read by <see cref="ThreatAssessment.TrustScore"/>, and cited by receipts.</summary>
                public const string Receipt = "receipt";
                /// <summary>A grudge. Read by the nomination and veto sorts, the vote, the jury story term and <c>Confront</c>.</summary>
                public const string Grudge = "grudge";
                /// <summary>A grudge made smaller, or forgiven.</summary>
                public const string Ease = "grudge-ease";
                /// <summary>A sticky bond formed. Read by the vote, the veto save, the nomination sort and the jury.</summary>
                public const string Bond = "bond";
                /// <summary>A bond ended; <see cref="StoryEffectState.type"/> is "betrayed" or "ended".</summary>
                public const string BondEnd = "bond-end";
                /// <summary>An alliance formed. Read by the vote's and the jury's alliance loyalty, the blocs and threat.</summary>
                public const string Alliance = "alliance";
                /// <summary>An alliance between the named pair ended.</summary>
                public const string AllianceEnd = "alliance-end";
                /// <summary>A promise given. Settled by the engine's own promise rules.</summary>
                public const string Promise = "promise";
                /// <summary>A deal struck, active at once. Read by <c>DealObligation</c> by pair or by target, and settled by the engine.</summary>
                public const string Deal = "deal";
                /// <summary>A fact with its knowers. Read by knowledge-gated threat and by lines that may mention it.</summary>
                public const string Fact = "fact";
                /// <summary>A fact made public, or a new knower added.</summary>
                public const string Spread = "fact-spread";
                /// <summary>A lore fact the player learns. Read by the odds, the option gates and the jury hint.</summary>
                public const string Reveal = "reveal";
                /// <summary>A favour owed. Spent into a promise or a deal.</summary>
                public const string Hook = "hook";
                /// <summary>A modifier on someone's competition score or action budget, for some weeks.</summary>
                public const string Modifier = "modifier";
                /// <summary>A stress step. Read by competitions, Volatility and the odds.</summary>
                public const string Stress = "stress";
                /// <summary>A production strike. Read by the conduct ladder.</summary>
                public const string Strike = "strike";
                /// <summary>A Have-Not week. Read by the action budget, NPC turns and stress.</summary>
                public const string HaveNot = "have-not";
                /// <summary>A private memory, from a whitelisted phrasing. Read by the vote's memory factor.</summary>
                public const string Memory = "memory";
                /// <summary>A broken word raised at the next conversation.</summary>
                public const string Reckoning = "reckoning";
                /// <summary>Production removes someone as the social window closes.</summary>
                public const string Expel = "expel";
                /// <summary>A number on the story cycle: heat, secrecy, exposure.</summary>
                public const string Var = "var";
                /// <summary>The NPC HoH's plan for this week's backdoor.</summary>
                public const string Backdoor = "backdoor";
                /// <summary>Somebody learns something the player did or said: a one-way cooling and a receipt.</summary>
                public const string Told = "told";
                /// <summary>
                /// The legacy events' trust mark, both ways on the ledger (<c>house_event_trust</c>). Read by
                /// <see cref="ThreatAssessment.TrustScore"/>. Only the ported legacy templates use it.
                /// </summary>
                public const string Trust = "trust";
                /// <summary>
                /// The web's phase-event bonus: <see cref="EpisodeState.phaseEventCompBonus"/> (type
                /// "competition"), read by the player's competition score, or
                /// <see cref="EpisodeState.phaseEventSocialBonus"/> (type "social"), stored as the web stores it.
                /// </summary>
                public const string PhaseBonus = "phase-bonus";
                /// <summary>A favour called in: the holder's unspent hook over somebody is spent.</summary>
                public const string HookSpend = "hook-spend";
                /// <summary>
                /// Everybody who counts the player their closest in the house, except the one named, marks
                /// being passed over (<c>story:snubbed</c>): "they noticed who you picked".
                /// </summary>
                public const string Snub = "snub";
                /// <summary>
                /// Somebody is cut out of the alliance they share with the named member. The rest hold the
                /// web's alliance-betrayed eighty against them; an alliance of two simply ends.
                /// </summary>
                public const string AllianceLeave = "alliance-leave";
                /// <summary>Somebody argued with production: their current strike needs a fourth clean week to clear.</summary>
                public const string PushBack = "push-back";

                public static readonly string[] All =
                {
                    Move, View, Receipt, Grudge, Ease, Bond, BondEnd, Alliance, AllianceEnd, Promise, Deal,
                    Fact, Spread, Reveal, Hook, Modifier, Stress, Strike, HaveNot, Memory, Reckoning, Expel,
                    Var, Backdoor, Told, Trust, PhaseBonus, HookSpend, Snub, AllianceLeave, PushBack,
                };

                public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
            }

        private static class Personality
        {
        public static class Approach
                {
                    public const string Warm = "warm", Calculated = "calculated", Bold = "bold", Candid = "candid";
                    public const string Playful = "playful", Yield = "yield", Hardball = "hardball";
                    public static readonly string[] All = { Warm, Calculated, Bold, Candid, Playful, Yield, Hardball };
                    public static bool IsKnown(string approach) => approach != null && Array.IndexOf(All, approach) >= 0;
                }
        }
    }
}
