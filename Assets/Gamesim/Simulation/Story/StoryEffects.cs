using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The typed consequences a story option can carry.
    ///
    /// <para>Every kind here has a named consumer - something the engine, the vote, the jury, the
    /// nomination sort or the house already reads. "A bonus nothing reads" is the failure this
    /// codebase keeps finding, so the catalogue lint refuses an effect whose kind is not listed,
    /// and each kind's doc says what reads it.</para>
    /// </summary>
    public static class StoryEffects
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
}
