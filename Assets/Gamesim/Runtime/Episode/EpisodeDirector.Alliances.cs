using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The notebook's alliances page (ACTIONS-DEALS-ALLIANCES-PLAN V3): the player's own pacts first,
    /// a card each - name, members as faces with the player's own reading under each, when it began
    /// and how, when it ended and why, every call made in it and who followed - then the pacts
    /// between other houseguests the player has evidence of, with that evidence and how sure it is.
    ///
    /// <para>Everything on it is <see cref="AllianceRead"/>'s, which reads only what the player was
    /// part of, saw or was told. A pact the player has no evidence of is not on the page in any form,
    /// and how strong a pact is shows only through the player's own reading and the calls on the
    /// record. Reading the page commits nothing.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The page's empty states: what each half says when it has nothing to show.</summary>
        public const string NoAlliancesCopy = "You have no alliances yet.";
        public const string NoSuspectedCopy = "You don't know of any other alliances.";
        /// <summary>The headings over the page's two halves.</summary>
        public const string YourAlliancesHeading = "YOUR ALLIANCES", OtherAlliancesHeading = "OTHER ALLIANCES YOU KNOW OF";
        /// <summary>The line over the pacts the player knows of: what they are, and the web's mark for them.</summary>
        public const string SuspectedNoteName = "Suspected alliances note";

        private void RenderNotebookAlliances(EpisodeState state)
        {
            var page = AllianceRead.Read(state);
            bool empty = page.yours.Count == 0 && page.suspected.Count == 0;

            hud.PageSectionHeading("Your alliances heading", YourAlliancesHeading, page.yours.Count);
            // The mark exists in every state: it is what the door and the rail scroll to.
            hud.Mark(NotebookSection.Alliances);
            if (page.yours.Count == 0)
                hud.EmptyState(EpisodeHud.NoAlliancesName, empty ? PackArt.KitEmptyPrivate : null, NoAlliancesCopy,
                    "Propose one in a conversation, or say yes when somebody invites you. Each pact you are in, the calls you make in it and who follows them land here.");
            foreach (var pact in page.yours) YourPactCard(state, pact);

            hud.PageSectionHeading("Other alliances heading", OtherAlliancesHeading, page.suspected.Count);
            if (page.suspected.Count == 0)
                hud.EmptyState(EpisodeHud.NoSuspectedName, null, NoSuspectedCopy,
                    "Pacts between other houseguests are private. One shows here only once word of it reaches you.");
            else
                hud.LockNote(SuspectedNoteName, "Only the pacts word has reached you about, and how you heard. On the relationship web a dashed purple line joins those of their members still in the house.");
            foreach (var card in page.suspected) SuspectedPactCard(state, card);

            hud.NotebookFooter("How strong a pact is shows only in your own reading of each member and in who followed your calls. What anyone in it feels is theirs.",
                "House activities", OpenHouseActivities);
        }

        /// <summary>One of the player's pacts, whole.</summary>
        private void YourPactCard(EpisodeState state, AllianceRead.Pact pact)
        {
            string eyebrow = pact.active
                ? "YOUR ALLIANCE" + (pact.formedWeek > 0 ? " · SINCE WEEK " + pact.formedWeek : "")
                : "YOUR ALLIANCE · ENDED" + (pact.endedWeek > 0 ? " IN WEEK " + pact.endedWeek : "");
            var faces = pact.members.Select(member =>
            {
                int asked = member.followed + member.ignored;
                return new EpisodeHud.PactFace
                {
                    Actor = state.Find(member.id), Away = !member.inHouse,
                    Note = member.inHouse ? member.reading : StatusWord(member.status),
                    NoteTint = member.inHouse ? ReadingTint(member.reading) : UiTheme.Muted,
                    Detail = asked > 0 ? "Followed " + member.followed + " of " + asked : null,
                };
            }).ToList();

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(pact.formed)) lines.Add(Dated(pact.formedWeek, pact.formed));
            // Who the player brought in since (ACTIONS-DEALS-ALLIANCES-PLAN C5), each in the week they joined.
            foreach (var join in pact.joined) lines.Add(Dated(join.week, join.text));
            if (!pact.active) lines.Add(Dated(pact.endedWeek, pact.ended));
            foreach (var call in pact.calls) lines.Add(Dated(call.week, CallLine(state, call)));
            // Said only where a call could be made: a standing pact, the levers on, and the player still in the house.
            if (pact.active && pact.calls.Count == 0 && EpisodeEngine.LeverRulesOn(state)
                && state.Find(state.playerId)?.status == ContestantStatus.Active)
                lines.Add(NoCallsYetCopy);
            foreach (var deal in pact.deals) lines.Add(Dated(deal.week, deal.text));
            // Who found out about it, under the leak rules (WAVE-D-NPC-PACTS-PLAN D4): after the calls - and
            // D3's plan line, which slots before these - each in the week the player was told. Lines, not controls.
            foreach (var exposure in pact.exposures) lines.Add(Dated(exposure.week, exposure.text));
            // Last, the risk of word getting out (D4-6), from what the player can reckon for themselves.
            if (!string.IsNullOrEmpty(pact.risk)) lines.Add(pact.risk);
            // Named for the pact's id as well as its name: two pacts can share a name, and a card's
            // name is how a test or a reader finds it. The card shows the name alone.
            hud.PactCard(EpisodeHud.AllianceCardName(pact.name, pact.id), eyebrow, pact.active ? UiTheme.Allied : UiTheme.Muted,
                pact.name, faces, lines);
        }

        /// <summary>A standing pact of the player's with no call in it yet, where calls can be made.</summary>
        public const string NoCallsYetCopy = "You have not called a vote in it yet.";

        /// <summary>A pact between others the player has evidence of: who, how sure, and what they saw or heard.</summary>
        private void SuspectedPactCard(EpisodeState state, AllianceRead.SuspectedPact pact)
        {
            bool open = pact.certainty == FinalistRead.Confirmed;
            var members = pact.memberIds.Select(id => state.Find(id)).Where(actor => actor != null).ToList();
            var faces = members.Select(actor =>
            {
                bool here = actor.status == ContestantStatus.Active;
                string reading = AllianceRead.ReadingWord(state, actor.id);
                return new EpisodeHud.PactFace
                {
                    Actor = actor, Away = !here,
                    Note = here ? reading : StatusWord(actor.status),
                    NoteTint = here ? ReadingTint(reading) : UiTheme.Muted,
                };
            }).ToList();
            var lines = pact.evidence.Select(line => Dated(line.week, line.text)).ToList();
            hud.PactCard(EpisodeHud.SuspectedCardPrefix + string.Join(" & ", members.Select(actor => actor.name)),
                open ? "KNOWN ALLIANCE · OUT IN THE OPEN" : "SUSPECTED ALLIANCE · WORD REACHED YOU",
                open ? UiTheme.Accent : UiTheme.Strategic,
                AllianceRead.Join(members.Select(actor => actor.name).ToList()), faces, lines);
        }

        /// <summary>"Week 3: You called it: evict Casey Wilson. Riley followed; Jo didn't."</summary>
        private static string CallLine(EpisodeState state, AllianceRead.Call call)
        {
            string target = call.targetId == state.playerId ? "you" : state.Find(call.targetId)?.name ?? "somebody";
            List<string> First(IEnumerable<string> ids) => ids.Select(id => FinalistRead.FirstName(state.Find(id)?.name ?? id)).ToList();
            string followed = call.followed.Count == 0 ? "Nobody followed" : AllianceRead.Join(First(call.followed)) + " followed";
            string ignored = call.defected.Count == 0 ? "" : "; " + AllianceRead.Join(First(call.defected)) + " didn't";
            return "You called it: evict " + target + ". " + followed + ignored + ".";
        }

        private static string Dated(int week, string line) => week > 0 ? "Week " + week + " · " + line : line;

        /// <summary>The web's colour for the player's own reading, so a word reads the same on the web and here.</summary>
        private static Color ReadingTint(string reading)
        {
            switch (reading)
            {
                case AllianceRead.Friendly: return RelationshipWeb.StandingColour(RelationshipWeb.Kind.Friendship);
                case AllianceRead.Wary: return RelationshipWeb.StandingColour(RelationshipWeb.Kind.Distrust);
                case AllianceRead.Hostile: return RelationshipWeb.StandingColour(RelationshipWeb.Kind.Rivalry);
                default: return UiTheme.Muted;
            }
        }
    }
}
