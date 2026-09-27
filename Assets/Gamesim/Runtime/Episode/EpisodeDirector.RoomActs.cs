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
                    if (state.Allied(state.playerId, target)) Act(AllianceMeetCaption, EpisodeCommandKind.AllianceMeet);
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

        /// <summary>The houseguests other than the player standing in a room, by the house's own occupancy read.</summary>
        private List<string> PeopleIn(EpisodeState state, string room) =>
            HouseOccupancy(state).Where(r => r.Name == room && r.Occupants != null)
                .SelectMany(r => r.Occupants).Where(person => !person.IsPlayer && !string.IsNullOrEmpty(person.Id))
                .Select(person => person.Id).Distinct().OrderBy(id => id, System.StringComparer.Ordinal).ToList();
    }
}
