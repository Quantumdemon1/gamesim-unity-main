using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>The card your own chip opens, the name a test finds it by.</summary>
        public const string EmoteMenuName = "Emote menu";

        /// <summary>Each move's button: captions are how a control is found, so each is its own.</summary>
        public static string EmoteCaption(EpisodeDirector.Emote kind)
        {
            switch (kind)
            {
                case EpisodeDirector.Emote.Cheer: return "Cheer";
                case EpisodeDirector.Emote.Shrug: return "Shrug";
                case EpisodeDirector.Emote.Celebrate: return "Celebrate";
                case EpisodeDirector.Emote.Pose: return "Strike a pose";
                case EpisodeDirector.Emote.Samba: return "Samba";
                case EpisodeDirector.Emote.HipHop: return "Hip-hop";
                default: return "Wave dance";
            }
        }

        /// <summary>Lets go of a held pose or dance; only on the card while one is held.</summary>
        public const string RelaxCaption = "Relax";

        private const float EmoteMenuWidth = 300f, EmoteMenuHead = 40f;

        /// <summary>
        /// The card of your moves over your own chip (<see cref="EpisodeDirector.MakeEmote"/>): two
        /// to a row, only those your body can make, then letting go of a held one, and the camera's
        /// follow your chip used to be. It takes the keyboard as it opens.
        /// </summary>
        private void EmoteMenu(EpisodeState state)
        {
            var you = state?.Find(state.playerId);
            if (you == null || canvas == null) return;
            var chip = CastChip(you.name);
            if (chip == null) return;

            string first = (you.name ?? "").Split(' ')[0];
            var rows = new List<(string caption, System.Action action)>();
            foreach (EpisodeDirector.Emote kind in System.Enum.GetValues(typeof(EpisodeDirector.Emote)))
                if (director.CanEmote(kind))
                {
                    var chosen = kind;
                    rows.Add((EmoteCaption(kind), () => director.MakeEmote(chosen)));
                }
            if (director.PlayerEmote != null) rows.Add((RelaxCaption, director.Relax));
            bool following = director.FollowedId == state.playerId;
            rows.Add((CastFollowCaption(first, following), () => { director.CloseEmoteMenu(); director.FollowHouseguest(state.playerId); }));

            float scale = FontScale, width = EmoteMenuWidth * scale;
            int lines = (rows.Count + 1) / 2;
            float height = (EmoteMenuHead + lines * (CastMenuRow + CastMenuGap) + 8f) * scale;
            var menu = CardOverChip(EmoteMenuName, chip, width, height);

            var title = FixedText(menu, "Your moves", 17, UiTheme.Heading, new Vector2(14f * scale, -8f * scale), new Vector2(width - 28f * scale, 22f * scale));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;

            float cell = (width - 20f * scale - CastMenuGap * scale) / 2f;
            for (int i = 0; i < rows.Count; i++)
            {
                int column = i % 2, line = i / 2;
                var button = FixedButton(menu, rows[i].caption,
                    new Vector2((10f + column * CastMenuGap) * scale + column * cell, -(EmoteMenuHead + line * (CastMenuRow + CastMenuGap)) * scale),
                    new Vector2(cell, CastMenuRow * scale), rows[i].action);
                var words = button.GetComponentInChildren<TMP_Text>();
                if (words != null) { words.fontSize = 16; words.fontSizeMax = 16; }
            }
            bool onTheCard = false;
            foreach (var row in rows) onTheCard |= row.caption == preferredSelection;
            if (!onTheCard) preferredSelection = rows[0].caption;
            restoreSelection = true;
        }
    }
}
