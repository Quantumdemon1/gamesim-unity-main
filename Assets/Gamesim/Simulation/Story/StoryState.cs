using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Everything the story system persists that is not already a house event or a storyline.
    ///
    /// <para>Schema 14. The design is <c>20-story-arcs-brainstorm.md</c> §2.3 with the Big Brother
    /// addendum's <see cref="reckonings"/>: one bundle, so the save format pays for one migration
    /// rather than six. Every record here is a plain list of small classes, because the save
    /// format checks stored objects field for field and has no notion of a dictionary or an array.
    /// </para>
    ///
    /// <para><b>Off by default.</b> <see cref="rulesStartWeek"/> is zero on a state nobody has
    /// switched on, which is every state a test builds by hand or through the season builders.
    /// The director switches it on for every season it starts, and the v13→v14 migration switches
    /// it on from the week after the save's — the same boundary every earlier system used, and for
    /// the same reason: a season already under way is not handed a system it was not played under.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class StoryWorldState
    {
        /// <summary>The first week the story system runs in this season, or zero for never.</summary>
        public int rulesStartWeek;

        /// <summary>
        /// How much of the story system this season plays under, one step per milestone
        /// (<see cref="StoryRules"/>). A later milestone's rules switch on by raising this, never by
        /// changing what an earlier version meant.
        /// </summary>
        public int rulesVersion;

        /// <summary>How hard production comes down on conduct: <see cref="StoryRules.Lenient"/> or <see cref="StoryRules.Standard"/>.</summary>
        public int productionStrictness = StoryRules.Standard;

        /// <summary>Whether this season tells romance storylines at all.</summary>
        public bool romanceStorylines = true;

        /// <summary>Who holds what against whom. Directional, and written without a roll.</summary>
        public List<GrudgeState> grudges = new List<GrudgeState>();

        /// <summary>Who knows what: alliances, couples, broken words, strikes and story outcomes.</summary>
        public List<HouseFactState> facts = new List<HouseFactState>();

        /// <summary>Sticky relationships that change only through beats: ride-or-die, confidant, nemesis, showmance.</summary>
        public List<BondState> bonds = new List<BondState>();

        /// <summary>Favours owed, one use each.</summary>
        public List<HookState> hooks = new List<HookState>();

        /// <summary>The player's time with each houseguest: rapport and this week's one-on-ones.</summary>
        public List<ContactState> contacts = new List<ContactState>();

        /// <summary>Which lore each houseguest was cast with, snapshotted when the season began.</summary>
        public List<LoreCastState> lore = new List<LoreCastState>();

        /// <summary>Lore facts the player has learned.</summary>
        public List<string> knownFacts = new List<string>();

        /// <summary>Production's record: strikes, clean weeks and competition sit-outs.</summary>
        public List<ConductState> conduct = new List<ConductState>();

        /// <summary>Who production removed, and when: exit order for the career ledger.</summary>
        public List<RemovalState> removals = new List<RemovalState>();

        /// <summary>This week's Have-Nots. Cleared when the week turns.</summary>

        /// <summary>Somebody production is about to remove, carried out as the social window closes.</summary>
        public string pendingRemovalId;

        /// <summary>Template, pair and headliner cooldowns.</summary>
        public List<StoryCooldownState> cooldowns = new List<StoryCooldownState>();

        /// <summary>
        /// Broken words waiting to be raised the next time the two of them talk.
        ///
        /// <para>The web's betrayal flag (<c>BetrayalFlagContext.tsx</c>), which lives in React state
        /// and is lost on a reload. Persisted here, because "the next conversation is about what
        /// happened" is the point of it.</para>
        /// </summary>
        public List<ReckoningState> reckonings = new List<ReckoningState>();

        public StoryWorldState Clone()
        {
            var copy = (StoryWorldState)MemberwiseClone();
            copy.grudges = grudges.Select(x => x.Clone()).ToList();
            copy.facts = facts.Select(x => x.Clone()).ToList();
            copy.bonds = bonds.Select(x => x.Clone()).ToList();
            copy.hooks = hooks.Select(x => x.Clone()).ToList();
            copy.contacts = contacts.Select(x => x.Clone()).ToList();
            copy.lore = lore.Select(x => x.Clone()).ToList();
            copy.knownFacts = new List<string>(knownFacts);
            copy.conduct = conduct.Select(x => x.Clone()).ToList();
            copy.removals = removals.Select(x => x.Clone()).ToList();
            copy.cooldowns = cooldowns.Select(x => x.Clone()).ToList();
            copy.reckonings = reckonings.Select(x => x.Clone()).ToList();
            return copy;
        }
    }

    /// <summary>One houseguest's grudge against another. Directional: holding one says nothing about the other way.</summary>
    [Serializable]
    public sealed class GrudgeState
    {
        public string holderId, targetId, cause;
        public double severity;
        public int originWeek, count;
        public GrudgeState Clone() => (GrudgeState)MemberwiseClone();
    }

    /// <summary>Something that happened, and who knows it.</summary>
    [Serializable]
    public sealed class HouseFactState
    {
        public string id, kind, actorId, subjectId, refId, visibility;
        public int week;
        public List<string> knowers = new List<string>();
        public HouseFactState Clone()
        {
            var copy = (HouseFactState)MemberwiseClone();
            copy.knowers = new List<string>(knowers);
            return copy;
        }
    }

    /// <summary>A relationship that no longer wobbles with the score.</summary>
    [Serializable]
    public sealed class BondState
    {
        public string id, kind, aId, bId, status, cycleId;
        public int sinceWeek, endedWeek;
        public BondState Clone() => (BondState)MemberwiseClone();
    }

    /// <summary>A favour owed: the holder can call it in once.</summary>
    [Serializable]
    public sealed class HookState
    {
        public string id, holderId, overId, factId;
        public int week;
        public bool spent;
        public HookState Clone() => (HookState)MemberwiseClone();
    }

    /// <summary>The player's time with one houseguest.</summary>
    [Serializable]
    public sealed class ContactState
    {
        public string npcId;
        public int rapport, weekCount, lastWeek;
        public ContactState Clone() => (ContactState)MemberwiseClone();
    }

    /// <summary>The lore a houseguest was cast with. Snapshotted, so a later catalogue never changes a running season.</summary>
    [Serializable]
    public sealed class LoreCastState
    {
        public string contestantId, sheetKey, source;
        public List<string> factIds = new List<string>();
        public LoreCastState Clone()
        {
            var copy = (LoreCastState)MemberwiseClone();
            copy.factIds = new List<string>(factIds);
            return copy;
        }
    }

    /// <summary>Production's record of one houseguest.</summary>
    [Serializable]
    public sealed class ConductState
    {
        public string contestantId;
        public int strikes, lastStrikeWeek, cleanWeeks, sitsOutWeek;

        /// <summary>1 after pushing back in the Diary Room: the current strike needs a fourth clean week to clear.</summary>
        public int pushedBack;

        public List<string> reasons = new List<string>();
        public ConductState Clone()
        {
            var copy = (ConductState)MemberwiseClone();
            copy.reasons = new List<string>(reasons);
            return copy;
        }
    }

    /// <summary>Somebody production removed from the house.</summary>
    [Serializable]
    public sealed class RemovalState
    {
        public string contestantId, reasonId;
        public int week;
        public RemovalState Clone() => (RemovalState)MemberwiseClone();
    }

    /// <summary>Something that may not happen again until a given week.</summary>
    [Serializable]
    public sealed class StoryCooldownState
    {
        public string key;
        public int untilWeek;
        public StoryCooldownState Clone() => (StoryCooldownState)MemberwiseClone();
    }

    /// <summary>A broken word between the player and a houseguest, waiting for their next conversation.</summary>
    [Serializable]
    public sealed class ReckoningState
    {
        public string npcId, cause;
        public bool betrayerIsPlayer;
        public int week;
        public ReckoningState Clone() => (ReckoningState)MemberwiseClone();
    }

    /// <summary>Who plays a part in a story cycle or a beat.</summary>
    [Serializable]
    public sealed class StoryRoleState
    {
        public string role, contestantId;
        public StoryRoleState Clone() => (StoryRoleState)MemberwiseClone();
    }

    /// <summary>One step a story cycle has taken.</summary>
    [Serializable]
    public sealed class StoryStepState
    {
        public string beatId, optionId, result;
        public int week, logSequence;
        public StoryStepState Clone() => (StoryStepState)MemberwiseClone();
    }

    /// <summary>A number a story cycle keeps: heat, secrecy, exposure, a plan.</summary>
    [Serializable]
    public sealed class StoryVarState
    {
        public string key;
        public int value;
        public StoryVarState Clone() => (StoryVarState)MemberwiseClone();
    }

    /// <summary>
    /// One typed consequence, stored on a choice when the beat is drawn.
    ///
    /// <para>Resolved to people at draw time, like the legacy events' impacts, so a beat answered
    /// weeks later moves the people it was always about. The one exception is
    /// <see cref="StoryEffects.Picked"/>: an option that asks the player to choose somebody stores
    /// that placeholder and the engine substitutes the id the command carried.</para>
    /// </summary>
    [Serializable]
    public sealed class StoryEffectState
    {
        public string kind, fromId, toId, thirdId, type, text;
        public double amount;
        public int weeks;
        public StoryEffectState Clone() => (StoryEffectState)MemberwiseClone();
    }

    /// <summary>Where the story system is up to, one version per milestone.</summary>
    public static class StoryRules
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

        /// <summary>What a season switched on today plays under.</summary>
        public const int Current = Production;

        public const int Lenient = 0, Standard = 1;
    }

    /// <summary>
    /// The lanes an arc runs in. At most one arc per lane at a time; production sits outside.
    /// <see cref="Moment"/> is the one-beat lane: the web's phase beats, the ported legacy
    /// situations and the Ringer's set pieces, which answer in one card and never hold a lane the
    /// longer arcs need.
    /// </summary>
    public static class StoryLanes
    {
        public const string Personal = "personal", Conflict = "conflict", Game = "game", Production = "production";
        public const string Moment = "moment";
        public static readonly string[] All = { Personal, Conflict, Game, Production, Moment };
        public static bool IsKnown(string lane) => lane != null && Array.IndexOf(All, lane) >= 0;
    }

    /// <summary>
    /// The moments in a Big Brother week at which the story system pulses.
    ///
    /// <para>The web's phase-event keys (<c>phase-event-system.ts</c>) under Unity's phase names:
    /// post-HoH, post-nomination, post-veto, post-veto-meeting, pre-vote and week start. Two more are
    /// this port's own: the conversation, which is the web's other beat trigger
    /// (<c>storyline-trigger-utils.ts</c>), and the close of the social window, where a week's
    /// leftover beats lapse and production carries out a removal.</para>
    /// </summary>
    public static class StoryAnchors
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

    /// <summary>How a beat reaches the player. A presentation hint; the rules never read it.</summary>
    public static class StorySurfaces
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

    /// <summary>
    /// Where a beat is staged: a presentation hint the rules never read. The house maps each venue
    /// to one of its rooms; a venue it has no room for is staged nowhere, and its card waits at the
    /// episode screen like any other.
    /// </summary>
    public static class StoryVenues
    {
        public const string Kitchen = "kitchen", Living = "living-room", Yard = "backyard", Bedroom = "bedroom";
        public const string Storage = "storage-room", HohRoom = "hoh-room", DiaryRoom = "diary-room", Hallway = "hallway";
        public const string Bathroom = "bathroom", Table = "dining-table";

        public static readonly string[] All = { Kitchen, Living, Yard, Bedroom, Storage, HohRoom, DiaryRoom, Hallway, Bathroom, Table };
        public static bool IsKnown(string venue) => venue != null && Array.IndexOf(All, venue) >= 0;
    }

    /// <summary>How a step of a story cycle came out.</summary>
    public static class StoryResults
    {
        public const string Plain = "plain", Success = "success", Backfire = "backfire", Lapsed = "lapsed", Npc = "npc";
        public static readonly string[] All = { Plain, Success, Backfire, Lapsed, Npc };
        public static bool IsKnown(string result) => result != null && Array.IndexOf(All, result) >= 0;
    }

    /// <summary>The sticky relationships.</summary>
    public static class BondKinds
    {
        public const string RideOrDie = "ride-or-die", Confidant = "confidant", Nemesis = "nemesis", Showmance = "showmance";
        public static readonly string[] All = { RideOrDie, Confidant, Nemesis, Showmance };
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    /// <summary>Where a bond stands.</summary>
    public static class BondStatus
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

    /// <summary>Who knows a fact.</summary>
    public static class FactVisibility
    {
        public const string Private = "private", Whispered = "whispered", Known = "known", Public = "public";
        public static readonly string[] All = { Private, Whispered, Known, Public };
        public static bool IsKnown(string visibility) => visibility != null && Array.IndexOf(All, visibility) >= 0;
    }

    /// <summary>Kinds of fact.</summary>
    public static class FactKinds
    {
        public const string Alliance = "alliance", Couple = "couple", BrokenWord = "broken-word", Strike = "strike";
        public const string Outcome = "outcome", Secret = "secret", Plan = "plan", Conversation = "conversation";
        public static readonly string[] All = { Alliance, Couple, BrokenWord, Strike, Outcome, Secret, Plan, Conversation };
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    /// <summary>Why a grudge is held.</summary>
    public static class GrudgeCauses
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

    /// <summary>Where a lore sheet came from.</summary>
    public static class LoreSources
    {
        public const string Authored = "authored", Legacy = "legacy", Derived = "derived";
        public static readonly string[] All = { Authored, Legacy, Derived };
        public static bool IsKnown(string source) => source != null && Array.IndexOf(All, source) >= 0;
    }
}
