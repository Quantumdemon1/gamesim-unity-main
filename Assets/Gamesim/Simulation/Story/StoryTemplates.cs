using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// One story arc: who it casts, when it may start, and its beats.
    ///
    /// <para>The catalogue is static C# data in the Simulation assembly: append-only, never saved,
    /// every template citing its <see cref="origin"/>. Deliberately <i>not</i> the plan's closed
    /// condition grammar for triggers: triggers and casting are C# predicates over the season
    /// (<see cref="StoryContext"/>), because nothing about a trigger is persisted and a predicate
    /// is testable where it stands. Everything the lint has to see - beats, options, labels,
    /// effects, lapses, origins, exemptions - is plain data.</para>
    ///
    /// <para><b>Numbers in the catalogue are rules.</b> Changing one on an existing template means a
    /// new template id or a <see cref="StoryRules"/> version, and an option is tombstoned rather
    /// than removed: an open beat in a save looks its template up by id.</para>
    /// </summary>
    public sealed class ArcTemplate
    {
        public string id, lane, eyebrow, title;

        /// <summary>"web:path:line" when it ports or completes something the web started, "native: why" otherwise.</summary>
        public string origin;

        public int minWeek = 1, minActive = 3, rulesVersion = StoryRules.Spine;
        public int cooldownWeeks = 4, pairCooldownWeeks = 2;

        /// <summary>
        /// Whether each houseguest headlines this arc at most once a season. True for a story - the
        /// same showmance does not start twice - and false for a recurring moment such as the web's
        /// phase beats, which come round every week to whoever holds the position.
        /// </summary>
        public bool oncePerHeadliner = true;

        /// <summary>
        /// A family of arcs that rest together: the web's five branching stories share one "a story
        /// every three weeks" gap. Null for an arc that rests only by itself.
        /// </summary>
        public string group;
        public int groupCooldownWeeks;

        /// <summary>
        /// Whether the weekly budget may not hold it back: a reckoning the web always plays, a
        /// confrontation or a plea a houseguest brings to you. One open card at a time still applies.
        /// </summary>
        public bool urgent;

        /// <summary>The anchors a new cycle of this arc may open at. Empty: only code opens it.</summary>
        public string[] startAnchors = Array.Empty<string>();

        /// <summary>The conversation commands that may open it (the web's conversation trigger). Null: none.</summary>
        public EpisodeCommandKind[] conversationTopics;

        /// <summary>What it asks of the player; an alumni card plays game arcs only.</summary>
        public StoryPeople.Sensitivity playerNeeds = StoryPeople.Sensitivity.Game;

        public ArcRole[] roles = Array.Empty<ArcRole>();

        /// <summary>Binds the roles, or returns null when the season cannot cast it now.</summary>
        public Func<StoryContext, ArcBinding> cast;

        /// <summary>How much it wants to start, given the binding. Zero or less: not now.</summary>
        public Func<StoryContext, ArcBinding, double> weight;

        /// <summary>The beats; the first is the opener.</summary>
        public BeatTemplate[] beats = Array.Empty<BeatTemplate>();

        /// <summary>
        /// Runs at every anchor while the cycle is open and nothing is scheduled: exposure pulses,
        /// flare-ups, a plan that goes wrong. Returns the id of a beat to fire now, or null.
        /// </summary>
        public Func<StoryContext, StoryCycle, string> pulse;

        /// <summary>Structural exemptions from the castability sweep, each with its reason: "cast:reason".</summary>
        public string[] exemptions = Array.Empty<string>();

        /// <summary>A play's goal, deadline and outcomes (plan 30), or null for an arc that is not a play.</summary>
        public PlayTemplate play;

        public BeatTemplate Beat(string beatId) => beats.FirstOrDefault(b => b.id == beatId);

        /// <summary>The same arc cast another way: the engine's walk-in casts the two people the player found.</summary>
        public ArcTemplate Recast(Func<StoryContext, ArcBinding> recast)
        {
            var copy = (ArcTemplate)MemberwiseClone();
            copy.cast = recast;
            return copy;
        }
    }

    /// <summary>A part in an arc, and what it asks of whoever plays it.</summary>
    public sealed class ArcRole
    {
        public string key;
        public StoryPeople.Sensitivity needs = StoryPeople.Sensitivity.Game;
        public bool optional;

        /// <summary>
        /// Whether the part may be played by somebody who has left the house: the houseguest you
        /// record a goodbye message for. A departed part never ends a cycle by being gone.
        /// </summary>
        public bool departed;

        public ArcRole(string key, StoryPeople.Sensitivity needs = StoryPeople.Sensitivity.Game, bool optional = false)
        { this.key = key; this.needs = needs; this.optional = optional; }
    }

    /// <summary>Who plays each part when a cycle starts. The headliner is who cooldowns rest.</summary>
    public sealed class ArcBinding
    {
        public readonly Dictionary<string, string> roles = new Dictionary<string, string>(StringComparer.Ordinal);
        public string headliner;
        public string Get(string role) => role != null && roles.TryGetValue(role, out var id) ? id : null;
        public ArcBinding With(string role, string id)
        {
            if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(id)) roles[role] = id;
            return this;
        }
        public ArcBinding Headlining(string id) { headliner = id; return this; }
    }

    /// <summary>One beat of an arc.</summary>
    public sealed class BeatTemplate
    {
        public string id;

        /// <summary>The anchor it fires at when it is scheduled as a next beat.</summary>
        public string anchor;

        /// <summary>
        /// For a beat whose anchor is the conversation: the role whose conversation with the player
        /// fires it. The web advances a storyline the next time you talk to somebody in it.
        /// </summary>
        public string talkRole;

        /// <summary>The anchor whose Advance lapses it. Null: the next anchor after the one it fired at.</summary>
        public string closes;

        public string surface = StorySurfaces.Scene;
        public string venue;

        /// <summary>
        /// The ceremony the house marks this beat's answer with (plan §5.1, Fallout): one of
        /// <see cref="StoryLog.Fallout"/>, logged as its own line when the player answers it. Null
        /// for the many beats whose outcome is only a line in the log.
        /// </summary>
        public string fallout;

        /// <summary>A constant, name-free title.</summary>
        public string title;

        /// <summary>A name-free sentence: what is saved, and what an older reader shows.</summary>
        public string summary;

        /// <summary>The narrative, with role tokens; further phrasings by week. The mechanics never vary.</summary>
        public string text;
        public string[] alternates = Array.Empty<string>();

        public OptionTemplate[] options = Array.Empty<OptionTemplate>();

        /// <summary>The option taken when it lapses. Every beat that asks has one; the lint checks.</summary>
        public string lapse;

        /// <summary>Whether it may fire now; when false a scheduled beat waits for the next anchor.</summary>
        public Func<StoryContext, StoryCycle, bool> ready;

        /// <summary>For an NPC-held beat: the option they take (drawn from the story stream by the caller's weights).</summary>
        public Func<StoryContext, StoryCycle, OptionTemplate[], string> npc;

        public OptionTemplate Option(string optionId) => options.FirstOrDefault(o => o.id == optionId);
        public bool NpcHeld => surface == StorySurfaces.Npc;
    }

    /// <summary>One way of answering a beat.</summary>
    public sealed class OptionTemplate
    {
        public string id;

        /// <summary>A fixed caption constant: never composed with a name or a number.</summary>
        public string label;

        /// <summary>With role tokens; rendered at display time.</summary>
        public string description;

        public string risk = HouseEventRisk.Low, glyph, approach;

        /// <summary>Hidden unless the player has one of these traits.</summary>
        public string[] onlyIf;

        /// <summary>Hidden unless this holds when the beat is drawn: an option that only makes sense for some casts.</summary>
        public Func<StoryContext, StoryCycle, bool> showIf;

        /// <summary>Shown as one muted locked line unless the player has one of these traits.</summary>
        public string[] lockUnless;

        /// <summary>Available only when this holds (a known fact, a bond, a hook); otherwise locked with <see cref="needsLabel"/>.</summary>
        public Func<StoryContext, StoryCycle, bool> needs;
        public string needsLabel;

        /// <summary>The base chance of success, or −1 for certain; checked against <see cref="checkRole"/>.</summary>
        public double check = -1;
        public string checkRole;

        /// <summary>Player traits worth the web's +15.</summary>
        public string[] bonusTraits;

        /// <summary>Player traits for which choosing it is out of character: a stress step, and it says so.</summary>
        public string[] against;

        public bool costsAction, conduct;

        /// <summary>For an option that asks the player to name somebody: who may be named.</summary>
        public Func<StoryContext, StoryCycle, IEnumerable<string>> pick;

        public Fx[] effects = Array.Empty<Fx>();
        public Fx[] backfire = Array.Empty<Fx>();

        /// <summary>Consequences worked out when the beat is drawn, from the season as it stands.</summary>
        public Func<StoryContext, StoryCycle, IEnumerable<Fx>> extra;

        /// <summary>A backfire's consequences worked out when the beat is drawn: who saw, who resents it.</summary>
        public Func<StoryContext, StoryCycle, IEnumerable<Fx>> backfireExtra;

        /// <summary>The beat that follows on success, and on a backfire. "end:reason" ends the cycle.</summary>
        public string next, nextOnBackfire;

        /// <summary>
        /// What happened, second person, with role tokens: the log line, the status line and the
        /// recap. A backfire has its own when it reads differently.
        /// </summary>
        public string outcome, backfireOutcome;

        /// <summary>For an NPC-held beat: how much the holder wants this option.</summary>
        public Func<ContestantState, double> ai;
    }

    /// <summary>
    /// One consequence in role terms. <see cref="from"/>, <see cref="to"/> and <see cref="third"/>
    /// name roles, <see cref="Player"/>, <see cref="Pick"/>, or nothing.
    /// </summary>
    public sealed class Fx
    {
        public const string Player = "PLAYER", Pick = "PICK";
        public string kind, from, to, third, type, text;
        public double amount;
        public int weeks;

        public static Fx Move(string from, string to, double amount) => new Fx { kind = StoryEffects.Move, from = from, to = to, amount = amount };
        public static Fx View(string holder, string about, double amount) => new Fx { kind = StoryEffects.View, from = holder, to = about, amount = amount };
        /// <summary>A one-way ledger mark. An amount of zero takes the receipt type's own size.</summary>
        public static Fx Receipt(string holder, string about, string type, double amount = 0, string text = null) =>
            new Fx { kind = StoryEffects.Receipt, from = holder, to = about, type = type, amount = amount, text = text };
        public static Fx Grudge(string holder, string target, double severity, string cause = GrudgeCauses.Story) =>
            new Fx { kind = StoryEffects.Grudge, from = holder, to = target, amount = severity, type = cause };
        public static Fx Ease(string holder, string target, double amount) => new Fx { kind = StoryEffects.Ease, from = holder, to = target, amount = amount };
        public static Fx Bond(string a, string b, string kind, string status = BondStatus.Private) =>
            new Fx { kind = StoryEffects.Bond, from = a, to = b, type = kind, text = status };
        public static Fx BondEnd(string a, string b, string kind, bool betrayed) =>
            new Fx { kind = StoryEffects.BondEnd, from = a, to = b, type = kind, text = betrayed ? BondStatus.Betrayed : BondStatus.Ended };
        public static Fx Alliance(string a, string b, string third = null) => new Fx { kind = StoryEffects.Alliance, from = a, to = b, third = third };
        public static Fx AllianceEnd(string a, string b) => new Fx { kind = StoryEffects.AllianceEnd, from = a, to = b };
        public static Fx Promise(string from, string to, PromiseKind kind) => new Fx { kind = StoryEffects.Promise, from = from, to = to, type = kind.ToString() };
        public static Fx Deal(string proposer, string recipient, string dealType, string target = null) =>
            new Fx { kind = StoryEffects.Deal, from = proposer, to = recipient, third = target, type = dealType };
        public static Fx Fact(string actor, string subject, string factKind, string visibility, string knowersRole = null) =>
            new Fx { kind = StoryEffects.Fact, from = actor, to = subject, type = factKind, text = visibility, third = knowersRole };
        public static Fx Spread(string factKind, string visibility) => new Fx { kind = StoryEffects.Spread, type = factKind, text = visibility };
        public static Fx Reveal(string about, string facet) => new Fx { kind = StoryEffects.Reveal, to = about, type = facet };
        public static Fx Hook(string holder, string over) => new Fx { kind = StoryEffects.Hook, from = holder, to = over };
        public static Fx Modifier(string owner, string modifierId, int weeks) => new Fx { kind = StoryEffects.Modifier, from = owner, type = modifierId, weeks = weeks };
        public static Fx Stress(string who, int steps) => new Fx { kind = StoryEffects.Stress, from = who, amount = steps };
        public static Fx Strike(string who, string reason) => new Fx { kind = StoryEffects.Strike, from = who, type = reason };
        public static Fx HaveNot(string who) => new Fx { kind = StoryEffects.HaveNot, from = who };
        public static Fx Memory(string owner, string about, string text) => new Fx { kind = StoryEffects.Memory, from = owner, to = about, text = text };
        public static Fx Reckoning(string npc, string cause, bool betrayerIsPlayer) =>
            new Fx { kind = StoryEffects.Reckoning, from = npc, type = cause, amount = betrayerIsPlayer ? 1 : 0 };
        public static Fx Var(string key, int delta) => new Fx { kind = StoryEffects.Var, type = key, amount = delta };
        /// <summary>A number about one person, kept as "key:ROLE" - which pawn was told, which side was backed.</summary>
        public static Fx VarFor(string who, string key, int delta) => new Fx { kind = StoryEffects.Var, from = who, type = key, amount = delta };
        /// <summary>The legacy events' trust mark, both ways. Ported templates only.</summary>
        public static Fx Trust(string holder, string about, double amount, string text = null) =>
            new Fx { kind = StoryEffects.Trust, from = holder, to = about, amount = amount, text = text };
        /// <summary>Everyone who counts the player their closest, except whoever is named, marks being passed over.</summary>
        public static Fx Snub(string except) => new Fx { kind = StoryEffects.Snub, from = except };
        /// <summary>Cuts somebody out of the alliance they share with a member.</summary>
        public static Fx AllianceLeave(string leaver, string member) => new Fx { kind = StoryEffects.AllianceLeave, from = leaver, to = member };
        /// <summary>The alliance between two people becomes known to the whole house.</summary>
        public static Fx SpreadAlliance(string a, string b) =>
            new Fx { kind = StoryEffects.Spread, from = a, to = b, type = FactKinds.Alliance, text = FactVisibility.Public };
        /// <summary>One more person finds out about the alliance between two people.</summary>
        public static Fx LeakAlliance(string a, string b, string listener) =>
            new Fx { kind = StoryEffects.Spread, from = a, to = b, third = listener, type = FactKinds.Alliance, text = FactVisibility.Whispered };
        /// <summary>The player argued with production: this strike needs a fourth clean week to clear.</summary>
        public static Fx PushBack(string who) => new Fx { kind = StoryEffects.PushBack, from = who };
        /// <summary>Spends the holder's favour over somebody.</summary>
        public static Fx SpendHook(string holder, string over) => new Fx { kind = StoryEffects.HookSpend, from = holder, to = over };
        /// <summary>The web's phase-event competition bonus: cumulative, read by the player's competition score.</summary>
        public static Fx CompBonus(int points) => new Fx { kind = StoryEffects.PhaseBonus, type = "competition", amount = points };
        /// <summary>The web's phase-event social bonus: cumulative and stored, as the web stores it.</summary>
        public static Fx SocialBonus(int points) => new Fx { kind = StoryEffects.PhaseBonus, type = "social", amount = points };
        /// <summary>Production removes somebody as the social window closes (the third rung).</summary>
        public static Fx Expel(string who) => new Fx { kind = StoryEffects.Expel, from = who };
        public static Fx Told(string listener, string about, double cooling, string receipt) =>
            new Fx { kind = StoryEffects.Told, from = listener, to = about, amount = cooling, type = receipt };
    }

    /// <summary>What a trigger, a caster or a pulse can see: the season, and where in the week it is.</summary>
    public sealed class StoryContext
    {
        public readonly EpisodeState state;
        public readonly string anchor;

        /// <summary>For a conversation trigger: who the player was talking to, and how.</summary>
        public readonly string talkingTo;
        public readonly EpisodeCommandKind? topic;

        /// <summary>For a conversation about somebody - venting, a rumour - who it was about.</summary>
        public readonly string about;

        public StoryContext(EpisodeState state, string anchor, string talkingTo = null, EpisodeCommandKind? topic = null, string about = null)
        { this.state = state; this.anchor = anchor; this.talkingTo = talkingTo; this.topic = topic; this.about = about; }

        public string Player => state.playerId;
        public ContestantState Find(string id) => state.Find(id);
        public double Score(string from, string to) => state.Score(from, to);
        public int Version => state.story?.rulesVersion ?? 0;
        public bool AtLeast(int version) => Version >= version;
        public bool Real(string id) => StoryPeople.IsRealPerson(state, id);
        public List<ContestantState> Npcs => StoryPeople.ActiveNpcs(state);
        public double Grudge(string holder, string target) => Grudges.Severity(state, holder, target);
    }

    /// <summary>A running story cycle, as a beat's predicates see it.</summary>
    public sealed class StoryCycle
    {
        public readonly StorylineState record;
        public readonly ArcTemplate template;
        public StoryCycle(StorylineState record, ArcTemplate template) { this.record = record; this.template = template; }

        public string Id => record.id;
        public string Role(string key) => record.cast.FirstOrDefault(r => r.role == key)?.contestantId;
        public int Var(string key) => record.vars.FirstOrDefault(v => v.key == key)?.value ?? 0;

        public void SetVar(string key, int value)
        {
            var existing = record.vars.FirstOrDefault(v => v.key == key);
            if (existing != null) { existing.value = Math.Max(-1000, Math.Min(1000, value)); return; }
            if (record.vars.Count >= 16) return;
            record.vars.Add(new StoryVarState { key = key, value = Math.Max(-1000, Math.Min(1000, value)) });
        }

        /// <summary>Whether this cycle has already taken an option (any beat).</summary>
        public bool Took(string optionId) => record.path.Any(p => p.optionId == optionId);
        public bool Reached(string beatId) => record.path.Any(p => p.beatId == beatId);
    }
}
