using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The web's room acts (decision D-E; 21 §G6, roomActions.ts:38-190), modelled as the act and
    /// never the room. The engine does not know where anybody stands - the player's room is kept
    /// out of the save on purpose - and needs no room, because the act implies it: pillow talk is
    /// the bedroom's, cooking the kitchen's, an invitation the Head of Household's suite. The house
    /// offers each act only in its room and, when an act has an audience, names who is there to see
    /// it. Every act is a social action: it is spent from the week's budget like any conversation.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The bedroom's +6 at its ×1.5 ("trust ×1.5").</summary>
        public const double PillowTalkWarmth = 9;
        /// <summary>"Cooking bonds everyone present": +2 with everyone in the kitchen and the living room.</summary>
        public const double CookWarmth = 2;
        /// <summary>The suite's +9, and the −4 "they noticed who you picked" for the houseguests who rank you warmest.</summary>
        public const double InviteUpWarmth = 9;
        public const int InvitesPerWeek = 2;
        /// <summary>The living room's Public Defense: +6 with the defended, +1 with each witness.</summary>
        public const double DefenseWarmth = 6, DefenseWitnessWarmth = 1;
        /// <summary>The backyard's Alliance Meet, "low suspicion": +4 with an ally.</summary>
        public const double AllianceMeetWarmth = 4;
        /// <summary>The game room's Play a Game: +3.</summary>
        public const double PlayAGameWarmth = 3;
        /// <summary>Practice: the web's +5% on the next competition, to 15% - here a point a session, to three.</summary>
        public const double PracticeBonus = 1, PracticeCap = 3;
        /// <summary>The most people an act's audience may name: the whole house, never more.</summary>
        public const int AudienceLimit = 16;

        /// <summary>Whether this season plays the room acts: from the story system's staging rules on.</summary>
        public static bool RoomActsOpen(EpisodeState s) => StoryAt(s, StoryRules.Staging);

        public static bool IsRoomAct(EpisodeCommandKind kind) =>
            kind == EpisodeCommandKind.PillowTalk || kind == EpisodeCommandKind.Cook || kind == EpisodeCommandKind.InviteUp
            || kind == EpisodeCommandKind.PublicDefense || kind == EpisodeCommandKind.AllianceMeet
            || kind == EpisodeCommandKind.CompPractice || kind == EpisodeCommandKind.PlayAGame;

        /// <summary>
        /// Whether the Head of Household may still invite this houseguest up this week, and why not
        /// when they may not - for the house, which offers the invitation only when it would work.
        /// </summary>
        public static string InviteRefusal(EpisodeState s, string targetId)
        {
            if (s == null || s.hohId != s.playerId) return "Only the Head of Household invites people up.";
            if (s.story.cooldowns.Count(c => c.key.StartsWith(InvitePrefix(s), StringComparison.Ordinal) && c.untilWeek > s.week) >= InvitesPerWeek)
                return "You have invited two people up this week.";
            if (Cooling(s, InvitePrefix(s) + targetId)) return "They have already been up this week.";
            return null;
        }

        private static string InvitePrefix(EpisodeState s) => "invite:" + s.week + ":";

        private static string PillowKey(int week, string npcId) => "pillow:" + week + ":" + npcId;

        /// <summary>Whether the player and this houseguest have had pillow talk this week or last.</summary>
        public static bool PillowTalkedLately(EpisodeState s, string npcId) =>
            s?.story != null && npcId != null && (Cooling(s, PillowKey(s.week, npcId)) || Cooling(s, PillowKey(s.week - 1, npcId)));

        /// <summary>The people an act's audience names: <c>text</c>, ids separated by spaces, each in the house.</summary>
        private static List<ContestantState> ActAudience(EpisodeState s, EpisodeCommand c, string targetId)
        {
            var ids = (c.text ?? string.Empty).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal).ToList();
            Require(ids.Count <= AudienceLimit, "Name only the people who are there.");
            var people = ids.Select(s.Find).ToList();
            Require(people.All(p => p != null && p.status == ContestantStatus.Active && !p.isPlayer && p.id != targetId),
                "Name only houseguests who are there.");
            return people.OrderBy(p => p.id, StringComparer.Ordinal).ToList();
        }

        /// <summary>One room act, after <see cref="Social"/> has checked the phase, the target and the budget.</summary>
        private static void RoomAct(EpisodeState s, ContestantState target, EpisodeCommand c)
        {
            Require(RoomActsOpen(s), "That is not part of this season's rules.");
            switch (c.kind)
            {
                case EpisodeCommandKind.PillowTalk:
                    Converse(s, target, PillowTalkWarmth, "You and " + target.name + " talked in the bedroom until the lights went out.");
                    // The bedroom talk is what the showmance reads (21 §C4): kept a week past this one.
                    Cool(s, PillowKey(s.week, target.id), s.week + 2);
                    break;
                case EpisodeCommandKind.PlayAGame:
                    Converse(s, target, PlayAGameWarmth, "You played a game with " + target.name + " in the game room.");
                    break;
                case EpisodeCommandKind.AllianceMeet:
                    Require(s.Allied(s.playerId, target.id), "A private meeting is for an ally.");
                    Converse(s, target, AllianceMeetWarmth, "You and " + target.name + " went over the plan in the backyard, where nobody listens.");
                    break;
                case EpisodeCommandKind.InviteUp:
                {
                    string refusal = InviteRefusal(s, target.id);
                    Require(refusal == null, refusal);
                    Cool(s, InvitePrefix(s) + target.id, s.week + 1);
                    Converse(s, target, InviteUpWarmth, "You invited " + target.name + " up to the Head of Household room.");
                    // Others noticed who you picked: whoever ranks you warmest of anyone in the house,
                    // and has not been up this week.
                    var invited = new HashSet<string>(s.story.cooldowns.Where(k => k.key.StartsWith(InvitePrefix(s), StringComparison.Ordinal))
                        .Select(k => k.key.Substring(InvitePrefix(s).Length)));
                    foreach (var npc in s.Active.Where(x => !x.isPlayer && !invited.Contains(x.id)).ToList())
                    {
                        var closest = s.Active.Where(o => o.id != npc.id).OrderByDescending(o => s.Score(npc.id, o.id))
                            .ThenBy(o => o.id, StringComparer.Ordinal).FirstOrDefault();
                        if (closest != null && closest.isPlayer)
                            RelationshipLedger.RecordOneWay(s, npc.id, s.playerId, StoryReceipts.Snubbed, StoryReceipts.Impact(StoryReceipts.Snubbed),
                                StoryReceipts.Describe(StoryReceipts.Snubbed, npc.name, true));
                    }
                    break;
                }
                case EpisodeCommandKind.PublicDefense:
                {
                    var witnesses = ActAudience(s, c, target.id);
                    Require(witnesses.Count > 0, "Standing up for somebody needs somebody to see it.");
                    Converse(s, target, DefenseWarmth, "You stood up for " + target.name + " in front of the house.");
                    foreach (var witness in witnesses)
                    {
                        Change(s, s.playerId, witness.id, DefenseWitnessWarmth);
                        Remember(s, witness.id, s.playerId, "Saw you stand up for " + target.name + ".", true);
                    }
                    break;
                }
                case EpisodeCommandKind.Cook:
                {
                    var eating = new[] { target }.Concat(ActAudience(s, c, target.id)).ToList();
                    foreach (var person in eating)
                    {
                        Change(s, s.playerId, person.id, CookWarmth);
                        Remember(s, person.id, s.playerId, "You cooked for the house in week " + s.week + ".", true);
                    }
                    Log(s, "conversation", "You cooked for the house. " + (eating.Count == 1 ? "1 houseguest" : eating.Count + " houseguests")
                        + " ate with you.", new[] { s.playerId }.Concat(eating.Select(p => p.id)).ToArray());
                    break;
                }
                case EpisodeCommandKind.CompPractice:
                    Practise(s, target);
                    break;
                default:
                    throw new RuleException("Unsupported room act.");
            }
        }

        /// <summary>
        /// Practice for the next competition, with somebody to run it with: the "Practised"
        /// modifier, a point a session to three, lasting into next week's competitions.
        /// </summary>
        private static void Practise(EpisodeState s, ContestantState partner)
        {
            var spec = StoryModifierCatalog.Find("comp-practice");
            Require(spec != null, "Practice is not part of this season's rules.");
            var mine = s.activeModifiers.FirstOrDefault(m => m.id == spec.id && string.IsNullOrEmpty(m.ownerId) && m.weeksLeft > 0);
            Require(mine == null || mine.competitionBonus < PracticeCap, "You are as ready as practice can make you.");
            if (mine == null)
            {
                if (s.activeModifiers.Count >= 40) return;
                s.activeModifiers.Add(new StoryModifierState
                {
                    id = spec.id, name = spec.name, description = spec.description, weeksLeft = 2,
                    competitionBonus = PracticeBonus, socialBonus = 0, ownerId = string.Empty,
                });
            }
            else
            {
                mine.competitionBonus = Math.Min(PracticeCap, mine.competitionBonus + PracticeBonus);
                mine.weeksLeft = Math.Max(mine.weeksLeft, 2);
            }
            Log(s, "preparation", "You practised in the backyard with " + partner.name + ". Next competition: "
                + CompetitionSigned(s.activeModifiers.First(m => m.id == spec.id && string.IsNullOrEmpty(m.ownerId)).competitionBonus) + ".",
                s.playerId, partner.id);
        }
    }
}
