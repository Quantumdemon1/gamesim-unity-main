using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The effect router: every story consequence goes through here to a producer the engine
    /// already has, or to a named refactor of one (20 §2.6).
    ///
    /// <para>Three paths for a score. The player in it: <see cref="ChangeKeyed"/>, which feeds the
    /// player's arcs and the oath milestone exactly as any player act does, with its reciprocal
    /// draw taken from the story stream. Two NPCs sharing an act: <see cref="RelationshipLedger.Move"/>
    /// and <see cref="RelationshipLedger.Record"/>, no roll and no arc. One NPC's private view of
    /// another: <see cref="WriteScore"/> one way and <see cref="RelationshipLedger.RecordOneWay"/>.
    /// Nothing here draws from the season's main stream.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// The engine's relationship reducer with its reciprocal draws taken from a keyed story
        /// stream rather than the season's. Player arcs and the 75-point oath milestone behave
        /// exactly as for any player act, at zero main-stream rolls.
        /// </summary>
        internal static void ChangeKeyed(EpisodeState s, string from, string to, double delta, string key,
            string note = null, string eventType = null) =>
            ChangeWithRoll(s, from, to, delta, StoryRandom.Stream(s, key), note, eventType);

        /// <summary>
        /// The player's alliance, formed with the roll injected: the command path passes the
        /// season's own stream and a story passes a keyed one. The body is the old
        /// <c>FormAlliance</c> case, unchanged.
        /// </summary>
        private static void FormAllianceWith(EpisodeState s, ContestantState target, Func<double> nextRoll)
        {
            Require(!s.Allied(s.playerId, target.id), "You already share an active alliance.");
            Require(s.Score(target.id, s.playerId) >= 8, "Build some trust before proposing an alliance.");
            s.alliances.Add(new AllianceState { id = "alliance-" + s.nextSequence, name = "The " + target.name.Split(' ')[0] + " Pact", members = new List<string> { s.playerId, target.id } });
            ChangeWithRoll(s, s.playerId, target.id, 8, nextRoll);
            Log(s, "alliance", "You and " + target.name + " formed a private alliance.", s.playerId, target.id);
        }

        /// <summary>Applies one resolved story effect.</summary>
        private static void ApplyStoryEffect(EpisodeState s, StoryEffectState e, StorylineState cycle, string subjectId,
            string key, int reception, bool backfire)
        {
            if (e == null) return;
            if (e.fromId == StoryEffects.Picked || e.toId == StoryEffects.Picked || e.thirdId == StoryEffects.Picked) return;
            var from = s.Find(e.fromId);
            var to = s.Find(e.toId);
            string effectKey = key + ":" + e.kind + ":" + e.fromId + ":" + e.toId + ":" + e.type;
            switch (e.kind)
            {
                case StoryEffects.Move:
                {
                    if (from == null || to == null || from.id == to.id) return;
                    double delta = e.amount;
                    bool withPlayer = from.isPlayer || to.isPlayer;
                    // How a gain lands with the person it was aimed at: half again when it resonates,
                    // half a loss when it grates. Only between the player and that person.
                    if (withPlayer && reception != 0 && subjectId != null && (from.id == subjectId || to.id == subjectId))
                        delta = Personality.Received(delta, reception);
                    if (!Alive(from) || !Alive(to)) return;
                    if (withPlayer) ChangeKeyed(s, from.id, to.id, delta, effectKey, e.text);
                    else
                    {
                        RelationshipLedger.Move(s, from.id, to.id, delta);
                        RelationshipLedger.Record(s, from.id, to.id, "story", delta, e.text ?? "Something happened between them.");
                    }
                    // The web's rule for a relationship drop, on a story backfire: a fall of eight or
                    // more leaves a grudge of five times the fall.
                    if (backfire && delta <= -8 && StoryAt(s, StoryRules.Grudges))
                        Grudges.FromDrop(s, from.id, to.id, delta);
                    return;
                }
                case StoryEffects.View:
                {
                    // A juror's view still counts - it is 30% of their vote - so a goodbye message
                    // can move it. Nobody else out of the house has a view worth moving.
                    if (from == null || to == null || from.id == to.id || !(Alive(from) || from.status == ContestantStatus.Jury)) return;
                    WriteScore(s, from.id, to.id, e.amount);
                    return;
                }
                case StoryEffects.Receipt:
                {
                    if (from == null || to == null || from.id == to.id) return;
                    if (!StoryReceipts.IsKnown(e.type)) return;
                    double impact = Math.Abs(e.amount) > 0.001 ? e.amount : StoryReceipts.Impact(e.type);
                    // The holder's mark about the other: held about the player, it is something the
                    // player did ("you heard them out"); held by the player, something done to them.
                    RelationshipLedger.RecordOneWay(s, from.id, to.id, e.type, impact,
                        StoryReceipts.Describe(e.type, to.isPlayer ? from.name : to.name, to.isPlayer) ?? e.type);
                    return;
                }
                case StoryEffects.Told:
                {
                    // Somebody finds out something: a one-way cooling and, when named, a receipt.
                    if (from == null || to == null || from.id == to.id || !Alive(from)) return;
                    if (Math.Abs(e.amount) > 0.001) WriteScore(s, from.id, to.id, e.amount);
                    if (StoryReceipts.IsKnown(e.type))
                        RelationshipLedger.RecordOneWay(s, from.id, to.id, e.type, StoryReceipts.Impact(e.type),
                            StoryReceipts.Describe(e.type, to.isPlayer ? from.name : to.name, to.isPlayer) ?? e.type);
                    return;
                }
                case StoryEffects.Grudge:
                    if (StoryAt(s, StoryRules.Grudges) && from != null && to != null)
                        Grudges.Add(s, from.id, to.id, e.amount, GrudgeCauses.IsKnown(e.type) ? e.type : GrudgeCauses.Story);
                    return;
                case StoryEffects.Ease:
                    if (from != null && to != null) Grudges.Ease(s, from.id, to.id, e.amount);
                    return;
                case StoryEffects.Bond:
                    if (StoryAt(s, StoryRules.Bonds) && from != null && to != null)
                        Bonds.Form(s, from.id, to.id, e.type, BondStatus.IsKnown(e.text) ? e.text : BondStatus.Private, cycle?.id);
                    return;
                case StoryEffects.BondEnd:
                    if (from != null && to != null) Bonds.End(s, from.id, to.id, e.type, e.text == BondStatus.Betrayed);
                    return;
                case StoryEffects.Alliance:
                    StoryAlliance(s, from, to, s.Find(e.thirdId), effectKey);
                    return;
                case StoryEffects.AllianceEnd:
                {
                    if (from == null || to == null) return;
                    foreach (var alliance in s.alliances.Where(a => a.active && a.members.Contains(from.id) && a.members.Contains(to.id)).ToList())
                    {
                        alliance.active = false;
                        Log(s, "alliance", alliance.name + " is finished.", alliance.members.ToArray());
                    }
                    return;
                }
                case StoryEffects.Promise:
                    StoryPromise(s, from, to, e.type);
                    return;
                case StoryEffects.Deal:
                    StoryDeal(s, from, to, s.Find(e.thirdId), e.type);
                    return;
                case StoryEffects.Fact:
                    if (StoryAt(s, StoryRules.Bonds))
                        Knowledge.Create(s, e.type, e.fromId, e.toId, e.text, cycle, e.thirdId);
                    return;
                case StoryEffects.Spread:
                    if (!StoryAt(s, StoryRules.Bonds)) return;
                    // A named pair's alliance going public, rather than a fact this cycle made.
                    if (e.type == FactKinds.Alliance && from != null && to != null)
                    {
                        foreach (var alliance in s.alliances.Where(a => a.members.Contains(from.id) && a.members.Contains(to.id)).ToList())
                        {
                            if (Knowledge.Of(s, FactKinds.Alliance, alliance.id) == null) Knowledge.AllianceFormed(s, alliance);
                            var fact = Knowledge.Of(s, FactKinds.Alliance, alliance.id);
                            // Named listener: one more person knows, and it is out as a whisper.
                            if (e.thirdId != null)
                            {
                                Knowledge.AddKnower(s, fact, e.thirdId);
                                Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
                            }
                            else Knowledge.MakeKnown(s, fact, FactVisibility.IsKnown(e.text) ? e.text : FactVisibility.Public);
                        }
                        return;
                    }
                    Knowledge.Publicise(s, cycle, e.type, e.text);
                    return;
                case StoryEffects.Snub:
                    foreach (var npc in s.Active.Where(x => !x.isPlayer && x.id != e.fromId).ToList())
                    {
                        var closest = s.Active.Where(o => o.id != npc.id).OrderByDescending(o => s.Score(npc.id, o.id))
                            .ThenBy(o => o.id, StringComparer.Ordinal).FirstOrDefault();
                        if (closest != null && closest.isPlayer)
                            RelationshipLedger.RecordOneWay(s, npc.id, s.playerId, StoryReceipts.Snubbed, StoryReceipts.Impact(StoryReceipts.Snubbed),
                                StoryReceipts.Describe(StoryReceipts.Snubbed, npc.name, true));
                    }
                    return;
                case StoryEffects.AllianceLeave:
                {
                    if (from == null || to == null || from.id == to.id) return;
                    foreach (var alliance in s.alliances.Where(a => a.active && a.members.Contains(from.id) && a.members.Contains(to.id)).ToList())
                    {
                        var rest = alliance.members.Where(m => m != from.id).ToList();
                        // An alliance of two is simply over; a bigger one closes ranks without them.
                        if (alliance.members.Count <= 2) alliance.active = false;
                        else alliance.members.Remove(from.id);
                        foreach (var member in rest) StoryAllianceLeft(s, member, from.id);
                        Log(s, "alliance", from.name + " is out of " + alliance.name + ".", alliance.members.Concat(new[] { from.id }).Distinct().ToArray());
                    }
                    return;
                }
                case StoryEffects.Reveal:
                {
                    if (to == null || !StoryAt(s, StoryRules.Lore)) return;
                    var fact = Lore.Facet(s, to.id, e.type);
                    if (fact != null && Lore.Learn(s, fact.id))
                        Log(s, "story-lore", "You learned something about " + to.name + ": " + fact.text, s.playerId, to.id);
                    return;
                }
                case StoryEffects.Hook:
                    if (StoryAt(s, StoryRules.Bonds) && from != null && to != null) Hooks.Create(s, from.id, to.id, null);
                    return;
                case StoryEffects.Modifier:
                    StoryModifier(s, from, e.type, e.weeks);
                    return;
                case StoryEffects.Stress:
                    if (from != null && Alive(from)) Personality.AdjustStress(from, (int)Math.Round(e.amount));
                    return;
                case StoryEffects.Strike:
                    if (from != null) ProductionStrike(s, from.id, e.type, cycle);
                    return;
                case StoryEffects.HookSpend:
                    if (from != null && to != null) Hooks.Spend(s, from.id, to.id);
                    return;
                case StoryEffects.PushBack:
                {
                    var row = from == null ? null : Production.For(s, from.id, false);
                    if (row != null && row.strikes > 0) row.pushedBack = 1;
                    return;
                }
                case StoryEffects.HaveNot:
                    if (from != null && StoryAt(s, StoryRules.Production)) Production.MakeHaveNot(s, from.id);
                    return;
                case StoryEffects.Memory:
                    if (from != null && to != null && !string.IsNullOrEmpty(e.text)) Remember(s, from.id, to.id, e.text, true);
                    return;
                case StoryEffects.Reckoning:
                    if (from != null && !from.isPlayer) AddReckoning(s, from.id, e.type ?? "story", e.amount > 0);
                    return;
                case StoryEffects.Expel:
                    if (from != null && StoryAt(s, StoryRules.Production)) Production.Pending(s, from.id);
                    return;
                case StoryEffects.Var:
                    if (cycle != null && !string.IsNullOrEmpty(e.type))
                    {
                        var view = new StoryCycle(cycle, StoryCatalog.Find(cycle.templateId));
                        // A number about somebody is kept under their part - "told:PAWN1" - so a
                        // pick-a-person option can say which of two people it was.
                        string role = e.fromId == null ? null : cycle.cast.FirstOrDefault(r => r.contestantId == e.fromId)?.role;
                        string varKey = role == null ? e.type : e.type + ":" + role;
                        view.SetVar(varKey, view.Var(varKey) + (int)Math.Round(e.amount));
                    }
                    return;
                case StoryEffects.PhaseBonus:
                {
                    // The web's own route for a phase event's bonus: cumulative counters on the state.
                    int points = (int)Math.Round(e.amount);
                    if (points <= 0) return;
                    if (e.type == "competition") s.phaseEventCompBonus = Math.Min(1000, s.phaseEventCompBonus + points);
                    else if (e.type == "social") s.phaseEventSocialBonus = Math.Min(1000, s.phaseEventSocialBonus + points);
                    return;
                }
                case StoryEffects.Trust:
                    if (from == null || to == null || from.id == to.id || !Alive(from)) return;
                    RelationshipLedger.Record(s, from.id, to.id, e.amount > 0 ? "house_event_trust" : "house_event_distrust",
                        e.amount, e.text ?? "A moment in the house.");
                    return;
                case StoryEffects.Backdoor:
                    if (cycle != null) new StoryCycle(cycle, StoryCatalog.Find(cycle.templateId)).SetVar("planned", 1);
                    return;
            }
        }

        private static bool Alive(ContestantState who) => who != null && who.status == ContestantStatus.Active;

        /// <summary>
        /// An alliance a story made. The player's goes through <see cref="FormAllianceWith"/> with a
        /// keyed roll and keeps its trust guard - a player who has not earned it simply does not get
        /// one. An NPC pact goes through <see cref="NpcAlliances.FormFromStory"/>, with no roll.
        /// </summary>
        private static void StoryAlliance(EpisodeState s, ContestantState a, ContestantState b, ContestantState c, string key)
        {
            if (a == null || b == null || !Alive(a) || !Alive(b)) return;
            var members = new List<ContestantState> { a, b };
            if (c != null && Alive(c) && c.id != a.id && c.id != b.id) members.Add(c);
            if (members.Any(m => m.isPlayer) && members.Count == 2)
            {
                var other = members.First(m => !m.isPlayer);
                if (s.Allied(s.playerId, other.id) || s.Score(other.id, s.playerId) < 8) return;
                FormAllianceWith(s, other, StoryRandom.Stream(s, key));
                var formed = s.alliances.Last();
                if (StoryAt(s, StoryRules.Bonds)) Knowledge.AllianceFormed(s, formed);
                return;
            }
            var made = NpcAlliances.FormFromStory(s, members.Select(m => m.id).ToList());
            if (made != null && StoryAt(s, StoryRules.Bonds)) Knowledge.AllianceFormed(s, made);
        }

        /// <summary>A promise a story made, settled by the engine's own promise rules.</summary>
        private static void StoryPromise(EpisodeState s, ContestantState from, ContestantState to, string kindName)
        {
            if (from == null || to == null || from.id == to.id || !Alive(from) || !Alive(to)) return;
            if (!Enum.TryParse(kindName, out PromiseKind kind)) return;
            if (s.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == from.id && p.toId == to.id && p.kind == kind)) return;
            if (s.promises.Count >= 200) return;
            s.promises.Add(new PromiseState
            {
                id = "promise-" + s.nextSequence++, fromId = from.id, toId = to.id, kind = kind, status = PromiseStatus.Active,
                week = s.week, expiresWeek = kind == PromiseKind.FinalTwo ? 0 : kind == PromiseKind.Safety ? s.week + 1 : s.week,
            });
            Remember(s, to.id, from.id, "Made me a " + kind + " promise.", true);
            if (from.isPlayer || to.isPlayer)
                Log(s, "promise", (from.isPlayer ? "You promised " + kind + " to " + to.name : from.name + " promised you " + kind) + ".",
                    from.id, to.id);
        }

        /// <summary>
        /// A deal a story struck, active at once. <c>DealObligation</c> reads a deal by pair or by
        /// target, which is why a vote a story wins is a deal and never a promise: a promise carries
        /// no target, and one made to a voter who is not a nominee moves nothing.
        /// </summary>
        private static void StoryDeal(EpisodeState s, ContestantState a, ContestantState b, ContestantState target, string type)
        {
            if (a == null || b == null || a.id == b.id || !Alive(a) || !Alive(b) || !DealKind.IsKnown(type)) return;
            if (DealKind.NamesATarget(type) && (target == null || !Alive(target))) return;
            if (s.deals.Any(d => DealStatus.Binds(d.status) && d.type == type
                                 && ((d.proposerId == a.id && d.recipientId == b.id) || (d.proposerId == b.id && d.recipientId == a.id))
                                 && d.targetId == target?.id)) return;
            if (s.deals.Count >= 200) return;
            var deal = PlayerDeals.Draft(s, b.id, type, target?.id, "deal-story-" + s.nextSequence++);
            deal.proposerId = a.id;
            deal.recipientId = b.id;
            deal.status = DealStatus.Active;
            s.deals.Add(deal);
            if (a.isPlayer || b.isPlayer)
            {
                var other = a.isPlayer ? b : a;
                Log(s, "deal", "You and " + other.name + " have a " + DealKind.Title(type).ToLowerInvariant() + ".", a.id, b.id);
            }
        }

        /// <summary>A modifier a story left: the catalogue names it, the owner is the player unless the story says otherwise.</summary>
        private static void StoryModifier(EpisodeState s, ContestantState owner, string modifierId, int weeks)
        {
            var spec = StoryModifierCatalog.Find(modifierId);
            if (spec == null || weeks < 1) return;
            string ownerId = owner == null || owner.isPlayer ? string.Empty : owner.id;
            s.activeModifiers.RemoveAll(m => m.id == spec.id && m.ownerId == ownerId);
            if (s.activeModifiers.Count >= 40) return;
            s.activeModifiers.Add(new StoryModifierState
            {
                id = spec.id, name = spec.name, description = spec.description, weeksLeft = Math.Min(20, weeks),
                competitionBonus = spec.competition, socialBonus = spec.social, ownerId = ownerId,
            });
            if (ownerId.Length == 0) Log(s, "storyline-outcome", spec.name + ": " + spec.description, s.playerId);
        }
    }

    /// <summary>The modifiers stories leave behind, by id. Append only: a save names them.</summary>
    public static class StoryModifierCatalog
    {
        public sealed class Spec
        {
            public string id, name, description;
            public double competition, social;
        }

        public static readonly Spec[] All =
        {
            new Spec { id = "rattled", name = "Rattled", description = "A scene went wrong and it is still in your head.", competition = -1 },
            new Spec { id = "fired-up", name = "Fired Up", description = "Somebody gave you a reason to win this week.", competition = 1 },
            new Spec { id = "comp-practice", name = "Practised", description = "Hours in the backyard going over the layout.", competition = 1 },
            new Spec { id = "power_move", name = "Power Move", description = "You showed the house you will not be pushed around.", competition = 2 },
            new Spec { id = "alliance_builder", name = "Alliance Builder", description = "Your social game is strengthening.", social = 10 },
            new Spec { id = "counter_intel", name = "Counter Intelligence", description = "You know something they do not know you know.", competition = 1, social = -5 },
            new Spec { id = "deal_maker", name = "Deal Maker", description = "You bought yourself a week, and everybody knows it.", social = 5 },
            new Spec { id = "clutch_performer", name = "Clutch Performer", description = "Your back is against the wall and it suits you.", competition = 3 },
            new Spec { id = "diary-vent", name = "Got It Off Your Chest", description = "Said it to a camera instead of a houseguest.", social = 0 },
        };

        public static Spec Find(string id) => All.FirstOrDefault(s => s.id == id);
    }
}
