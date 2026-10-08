using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The room acts (decision D-E) in the conversation panel. The engine models the act and never
    /// the room; the house is what knows where the player is standing, so this is where each act is
    /// offered only in its room - pillow talk in a bedroom, an invitation in the Head of Household's
    /// suite - and where the people who are there to see it are named for the acts that have an
    /// audience. The captions are constants, as every control's is.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string PillowTalkCaption = "Talk into the night";
        public const string CookCaption = "Cook for everyone here";
        public const string InviteUpCaption = "Invite them up to the HoH room";
        public const string PublicDefenseCaption = "Stand up for them in front of the room";
        public const string AllianceMeetCaption = "Go over the plan in private";
        public const string CompPracticeCaption = "Practise for the next competition";
        public const string PlayAGameCaption = "Play a game together";

        /// <summary>
        /// A meeting of a pact the player is in (ACTIONS-DEALS-ALLIANCES-PLAN C6), offered only under the
        /// commitment rules, in any private room. A new caption: under the rules the meeting is what
        /// "Go over the plan in private" commits, and that row, with its caption, is the backyard's
        /// word with one ally in every season without them.
        /// </summary>
        public const string AllianceMeetingCaption = "Hold an alliance meeting";

        /// <summary>The room the player is standing in, as the house last read it: null between rooms.</summary>
        public string PlayerRoom => playerIsActive ? beaconPlayerRoom : null;

        /// <summary>The acts the room the player is standing in offers with this houseguest, as rows under the dial.</summary>
        private void RoomActs(EpisodeState state, ContestantState npc)
        {
            if (!EpisodeEngine.RoomActsOpen(state) || npc == null) return;
            string room = PlayerRoom;
            if (room == null) return;
            string target = npc.id;
            void Act(string caption, EpisodeCommandKind kind, IEnumerable<string> audience = null)
            {
                string text = audience == null ? null : string.Join(" ", audience);
                hud.Tag(hud.Action(caption, () => Commit(state, kind, target, text: text)), Category(kind));
            }
            // Under the commitment rules a pact meets in any private room (C6); the backyard's word with
            // one ally is the same command, so it is not offered beside it.
            bool meetings = EpisodeEngine.CommitmentRulesOn(state);
            if (meetings) AllianceMeetingRow(state, target, room);
            switch (room)
            {
                case "Bedroom":
                    Act(PillowTalkCaption, EpisodeCommandKind.PillowTalk);
                    break;
                case "HoH":
                    if (EpisodeEngine.InviteRefusal(state, target) == null) Act(InviteUpCaption, EpisodeCommandKind.InviteUp);
                    break;
                case "Living":
                {
                    var witnesses = PeopleIn(state, "Living").Where(id => id != target).ToList();
                    if (witnesses.Count > 0) Act(PublicDefenseCaption, EpisodeCommandKind.PublicDefense, witnesses);
                    break;
                }
                case "Yard":
                    if (!meetings && state.Allied(state.playerId, target)) Act(AllianceMeetCaption, EpisodeCommandKind.AllianceMeet);
                    Act(CompPracticeCaption, EpisodeCommandKind.CompPractice);
                    break;
                case "Kitchen":
                    // Cooking feeds the kitchen and the living room beside it, as the web's does.
                    Act(CookCaption, EpisodeCommandKind.Cook,
                        PeopleIn(state, "Kitchen").Concat(PeopleIn(state, "Living")).Where(id => id != target).Distinct());
                    break;
                case "Games":
                    Act(PlayAGameCaption, EpisodeCommandKind.PlayAGame);
                    break;
            }
        }

        /// <summary>
        /// The pact meeting's row (C6): in a room where nobody listens (<see cref="EpisodeEngine.PrivateRooms"/>),
        /// with somebody in a pact of the player's that has not met this week - the oldest such pact, as
        /// the engine holds it (<see cref="EpisodeEngine.MeetingPact"/>), named by the command as a call
        /// names its pact. One row whatever the pacts: once that one has met, the next is offered under
        /// the same caption. Nothing about the houseguest is read to offer it.
        /// </summary>
        private void AllianceMeetingRow(EpisodeState state, string target, string room)
        {
            if (!EpisodeEngine.IsPrivateRoom(room)) return;
            var pact = EpisodeEngine.MeetingPact(state, target);
            if (pact == null) return;
            string pactId = pact.id;
            hud.Tag(hud.Action(AllianceMeetingCaption, () => Commit(state, EpisodeCommandKind.AllianceMeet, target, text: pactId)),
                AllianceMeetingTag(state, pact));
        }

        /// <summary>
        /// The pill on the meeting's row: warmth, and learn as well in a vote week when somebody at it
        /// casts a ballot - by what the player can see; whether they say is theirs. Under the war rooms a
        /// meeting that would be one says so instead (WAVE-D-NPC-PACTS-PLAN D3): warmth, and a plan.
        /// </summary>
        public static string AllianceMeetingTag(EpisodeState state, AllianceState pact) =>
            PactPlans.CouldConvene(state, pact) ? WarmthTag + " · " + PlanTag
                : EpisodeEngine.MeetingCouldTellAVote(state, pact) ? WarmthTag + " · " + LearnTag : WarmthTag;

        /// <summary>The houseguests other than the player standing in a room, by the house's own occupancy read.</summary>
        private List<string> PeopleIn(EpisodeState state, string room) =>
            HouseOccupancy(state).Where(r => r.Name == room && r.Occupants != null)
                .SelectMany(r => r.Occupants).Where(person => !person.IsPlayer && !string.IsNullOrEmpty(person.Id))
                .Select(person => person.Id).Distinct().OrderBy(id => id, System.StringComparer.Ordinal).ToList();
    }
}
