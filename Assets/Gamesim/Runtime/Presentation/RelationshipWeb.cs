using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The notebook's Network section as mockup-07 draws it: the house as a web around the player,
    /// an edge per relationship coloured by what kind of relationship it is, and a glass column
    /// beside the web that reads one houseguest — the player by default, or whichever portrait was
    /// pressed — as key allies, top rivals and what they know.
    ///
    /// <para>Strictly the player's own perspective, as the graph it replaces was. The engine stores
    /// relationships directionally and the two directions routinely disagree, so every edge here is
    /// the player's outbound score, and the column never reads an NPC's feelings, their private
    /// alliances or a promise the player is not party to. Selecting a houseguest changes which
    /// facts about the player's own record are in view; it does not open theirs.</para>
    ///
    /// <para>The selection lives here rather than in the director because it is presentation
    /// state: it survives the HUD's per-render rebuild, and it resets on its own when the season
    /// changes or the chosen houseguest leaves the house.</para>
    /// </summary>
    public static class RelationshipWeb
    {
        public const string RootName = "Relationship web";
        public const string GraphName = "Relationship graph";
        public const string ColumnName = "Relationship column";
        public const string LegendName = "Relationship legend";
        public const string EdgeName = "Edge";
        public const string DetailsScrollName = "Relationship detail scroll";

        public const string TitleCopy = "RELATIONSHIP WEB";
        public const string StrapCopy = "See how everyone in the house really connects.";
        public const string PerspectiveCopy = "Your perspective. Another housemate may feel differently.";
        public const string AlliesHeading = "Key allies";
        public const string RivalsHeading = "Top rivals";
        public const string SecretsHeading = "Secrets known";
        public const string BetweenHeading = "Between you";
        public const string KnownHeading = "What you know";
        public const string HistoryHeading = "Recent history";

        /// <summary>The score at which the player's own reading becomes a friendship.</summary>
        public const double FriendThreshold = 15d;
        /// <summary>Below this the player distrusts them; below <see cref="RivalThreshold"/> it is a rivalry.</summary>
        public const double DistrustThreshold = -15d;
        public const double RivalThreshold = -40d;
        /// <summary>The strongest word in either direction wants more than the threshold.</summary>
        public const double StrongThreshold = 40d;

        /// <summary>How many allies, rivals or secrets the column lists before it stops.</summary>
        public const int ListLimit = 3;

        private const float ColumnWidth = 250f;
        private const float Gap = 14f;
        private const float NodeSize = 46f;
        private const float PlayerSize = 62f;
        private const float RingWidth = 3f;
        private const float ChipDrop = 26f;
        private const float TitleHeight = 56f;
        private const float LegendHeight = 100f;

        /// <summary>What the player's own record says a relationship is.</summary>
        public enum Kind { Neutral, Friendship, Alliance, Rivalry, Distrust }
        public enum Filter { All, Allies, Friends, Tension }

        // ------------------------------------------------------------------ selection

        private static string selectedSession, selectedId;
        private static string filterSession;
        private static Filter filter;
        public static Filter CurrentFilter => filter;

        public static void SetFilter(EpisodeState state,Filter value)
        {
            filterSession=state?.sessionId; filter=value;
            var selected=SelectedFor(state);
            if(selected!=null && selected.id!=state.playerId && !MatchesFilter(state,selected.id,value))Select(state,state.playerId);
        }

        public static List<ContestantState> FilteredOthers(EpisodeState state)
        {
            var activeFilter=filterSession==state?.sessionId ? filter : Filter.All;
            return Others(state).Where(actor=>MatchesFilter(state,actor.id,activeFilter)).ToList();
        }

        private static bool MatchesFilter(EpisodeState state,string id,Filter value)
        {
            var kind=KindOf(state,id);
            return value==Filter.All || value==Filter.Allies && kind==Kind.Alliance
                || value==Filter.Friends && kind==Kind.Friendship
                || value==Filter.Tension && (kind==Kind.Rivalry || kind==Kind.Distrust);
        }

        /// <summary>The houseguest whose relationships the column reads, or null for the player.</summary>
        public static string Selected => selectedId;

        public static void Select(EpisodeState state, string id)
        {
            selectedSession = state?.sessionId;
            selectedId = id;
        }

        public static void ClearSelection()
        {
            selectedSession = null;
            selectedId = null;
            filterSession=null; filter=Filter.All;
        }

        /// <summary>
        /// The houseguest the column reads: the selection while it is still in this season's house,
        /// and the player otherwise. A selection from another season, or of someone since evicted,
        /// falls back rather than lingering.
        /// </summary>
        public static ContestantState SelectedFor(EpisodeState state)
        {
            if (state == null) return null;
            if (!string.IsNullOrEmpty(selectedId) && selectedSession == state.sessionId)
            {
                var chosen = state.Find(selectedId);
                if (chosen != null && chosen.status == ContestantStatus.Active) return chosen;
            }
            return state.Find(state.playerId);
        }

        // ------------------------------------------------------------------ the reading

        /// <summary>The GameObject name of a houseguest's node: their name, decorated, never changed.</summary>
        public static string NodeName(string name) => (name ?? string.Empty) + " · relationships";

        /// <summary>The player's own outbound reading of a houseguest, as a kind of edge.</summary>
        public static Kind KindOf(EpisodeState state, string otherId)
        {
            if (state == null || string.IsNullOrEmpty(otherId) || otherId == state.playerId) return Kind.Neutral;
            if (state.Allied(state.playerId, otherId)) return Kind.Alliance;
            double score = state.Score(state.playerId, otherId);
            if (score >= FriendThreshold) return Kind.Friendship;
            if (score <= RivalThreshold) return Kind.Rivalry;
            if (score <= DistrustThreshold) return Kind.Distrust;
            return Kind.Neutral;
        }

        /// <summary>Everyone still in the house other than the player, in cast order.</summary>
        public static List<ContestantState> Others(EpisodeState state)
        {
            var others = new List<ContestantState>();
            if (state == null) return others;
            foreach (var actor in state.contestants)
            {
                if (actor.isPlayer || actor.id == state.playerId) continue;
                if (actor.status != ContestantStatus.Active) continue;
                others.Add(actor);
            }
            return others;
        }

        /// <summary>The houseguests the player reads as friends or alliance-mates, closest first.</summary>
        public static List<ContestantState> Allies(EpisodeState state)
        {
            return Others(state)
                .Where(actor => { var kind = KindOf(state, actor.id); return kind == Kind.Alliance || kind == Kind.Friendship; })
                .OrderByDescending(actor => state.Score(state.playerId, actor.id))
                .ToList();
        }

        /// <summary>The houseguests the player distrusts, most hostile first.</summary>
        public static List<ContestantState> Rivals(EpisodeState state)
        {
            return Others(state)
                .Where(actor => { var kind = KindOf(state, actor.id); return kind == Kind.Rivalry || kind == Kind.Distrust; })
                .OrderBy(actor => state.Score(state.playerId, actor.id))
                .ToList();
        }

        /// <summary>The one-line reason a houseguest is listed as an ally.</summary>
        public static string AllyWord(EpisodeState state, string otherId)
        {
            if (state.Allied(state.playerId, otherId)) return "Strong alliance";
            return state.Score(state.playerId, otherId) >= StrongThreshold ? "Close friend" : "Friendly";
        }

        /// <summary>The one-line reason a houseguest is listed as a rival.</summary>
        public static string RivalWord(EpisodeState state, string otherId) =>
            state.Score(state.playerId, otherId) <= RivalThreshold ? "High tension" : "Distrust";

        /// <summary>Where the player stands with someone, in one word.</summary>
        public static string StandingWord(Kind kind)
        {
            switch (kind)
            {
                case Kind.Alliance: return "Allied";
                case Kind.Friendship: return "Friendly";
                case Kind.Distrust: return "Wary";
                case Kind.Rivalry: return "Hostile";
                default: return "Neutral";
            }
        }

        /// <summary>The generated glyph for a mood word, in the icon set's vocabulary.</summary>
        public static string MoodIcon(string mood)
        {
            switch ((mood ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "": return "mood-neutral";
                case "upset": return "mood-sad";
                case "content": return "mood-relieved";
                default: return "mood-" + mood.Trim().ToLowerInvariant();
            }
        }

        /// <summary>The colour a mood is drawn in: green when things are good, red when they are not.</summary>
        public static Color MoodColour(string mood)
        {
            switch ((mood ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "happy":
                case "content": return UiTheme.Allied;
                case "neutral": return UiTheme.Joke;
                case "upset": return UiTheme.Warning;
                case "angry": return UiTheme.Conflict;
                default: return UiTheme.Muted;
            }
        }

        private static Color EdgeColour(Kind kind)
        {
            switch (kind)
            {
                case Kind.Alliance: return UiTheme.AccentDeep;
                case Kind.Friendship: return UiTheme.Allied;
                case Kind.Rivalry:
                case Kind.Distrust: return UiTheme.Conflict;
                default: return UiTheme.Muted;
            }
        }

        private static Color RingColour(Kind kind) => kind == Kind.Neutral ? UiTheme.Outline : EdgeColour(kind);

        // ------------------------------------------------------------------ building

        /// <summary>
        /// Builds the section into <paramref name="parent"/>: the web on the left, the column on
        /// the right, sized by their own content so the notebook's scroll takes the taller of the
        /// two. <paramref name="select"/> is raised after a node is pressed and the selection has
        /// been recorded, so the caller can repaint.
        /// </summary>
        public static RectTransform Build(
            Transform parent, EpisodeState state, float scale, TMP_FontAsset font,
            Func<string, Texture> portrait, Action<string> select, float availableHeight = float.PositiveInfinity)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            var row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = Gap * scale;
            row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            if (state == null) return root;
            if(filterSession!=state.sessionId){filterSession=state.sessionId;filter=Filter.All;}

            var focus = SelectedFor(state);
            bool sideLegend = GraphHeight(state,scale) + 6f > availableHeight;
            float height = GraphHeight(state,scale) - (sideLegend ? LegendHeight * scale : 0f);
            Graph(root, state, focus, scale, font, portrait, select, sideLegend);
            Column(root, state, focus, scale, font, portrait, height);
            Filters(root,state,scale,font,select);
            return root;
        }

        private static float Radius(int count, float scale) =>
            Mathf.Min((112f + Mathf.Max(0, count - 6) * 6f) * scale, 140f);

        private static float GraphHeight(EpisodeState state,float scale)
            => TitleHeight*scale + 2f*(Radius(Others(state).Count,scale)+NodeSize*scale*.5f+ChipDrop*scale)
                + 12f*scale + LegendHeight*scale;

        private static void Filters(RectTransform root,EpisodeState state,float scale,TMP_FontAsset font,Action<string> refresh)
        {
            var toolbar=new GameObject("Relationship filters",typeof(RectTransform)).GetComponent<RectTransform>();
            toolbar.SetParent(root,false);toolbar.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
            toolbar.anchorMin=toolbar.anchorMax=Vector2.one;toolbar.pivot=Vector2.one;
            toolbar.anchoredPosition=new Vector2(-((ColumnWidth+Gap)*scale+14f),0);
            toolbar.sizeDelta=new Vector2(308f*scale,28f*scale);
            var values=new[]{Filter.All,Filter.Allies,Filter.Friends,Filter.Tension};
            for(int i=0;i<values.Length;i++)
            {
                var value=values[i];
                var rect=HudPrimitives.Fill("Filter relationships: "+value,toolbar,filter==value ? UiTheme.AccentDeep : UiTheme.Surface,6);
                Place(rect,i*78f*scale,0,74f*scale,28f*scale);
                var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
                button.targetGraphic.raycastTarget=true;
                button.onClick.AddListener(()=>{SetFilter(state,value);refresh?.Invoke(SelectedFor(state)?.id);});
                var label=Text(rect,value.ToString(),12,UiTheme.Paper,UiTheme.Weight.Medium,scale,font,TextAlignmentOptions.Center);
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
                label.rectTransform.offsetMin=Vector2.zero;label.rectTransform.offsetMax=Vector2.zero;
                if (filter == value)
                {
                    var selectedMark = HudPrimitives.Fill("Selected relationship filter",rect,UiTheme.Paper,1);
                    selectedMark.anchorMin = new Vector2(0,0); selectedMark.anchorMax = new Vector2(1,0);
                    selectedMark.pivot = new Vector2(.5f,0); selectedMark.anchoredPosition = new Vector2(0,2f*scale);
                    selectedMark.sizeDelta = new Vector2(-16f*scale,3f*scale);
                    selectedMark.GetComponent<Image>().raycastTarget = false;
                }
            }
        }

        private static Vector2 Ring(int index, int count, float radius)
        {
            if (count <= 0) return Vector2.zero;
            // Top first, clockwise: stable between renders, so the player can build the same
            // spatial memory the cast rail gives them.
            float angle = Mathf.PI * 0.5f - index * (Mathf.PI * 2f / count);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static void Graph(
            RectTransform root, EpisodeState state, ContestantState focus, float scale, TMP_FontAsset font,
            Func<string, Texture> portrait, Action<string> select, bool sideLegend)
        {
            var area = new GameObject(GraphName, typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(root, false);

            var all = Others(state);
            var others = FilteredOthers(state);
            float radius = Radius(all.Count, scale);
            float ringExtent = radius + NodeSize * scale * .5f + ChipDrop * scale;
            float legendTop = TitleHeight * scale + ringExtent * 2f + 12f * scale;
            float height = legendTop + (sideLegend ? 0f : LegendHeight * scale);
            var element = area.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minHeight = height; element.preferredHeight = height;

            var title = Heading(area, TitleCopy, 15, UiTheme.Paper, scale, font);
            Place(title.rectTransform, 4f * scale, -2f * scale, 260f * scale, 22f * scale);
            var strap = Text(area, StrapCopy, 12, UiTheme.Muted, UiTheme.Weight.Regular, scale, font);
            Place(strap.rectTransform, 4f * scale, -34f * scale, 430f * scale, 18f * scale);

            var hub = new GameObject("Hub", typeof(RectTransform)).GetComponent<RectTransform>();
            hub.SetParent(area, false);
            hub.anchorMin = new Vector2(.5f, 1f); hub.anchorMax = new Vector2(.5f, 1f);
            hub.pivot = new Vector2(.5f, .5f);
            hub.anchoredPosition = new Vector2(sideLegend ? -90f * scale : 0f, -(TitleHeight * scale + ringExtent));
            hub.sizeDelta = Vector2.zero;

            // Edges first, so the portraits draw over them.
            for (int i = 0; i < others.Count; i++)
                Edge(hub, Vector2.zero, Ring(all.IndexOf(others[i]), all.Count, radius), KindOf(state, others[i].id),
                    state.Score(state.playerId, others[i].id), scale);

            string focusId = focus != null ? focus.id : null;
            for (int i = 0; i < others.Count; i++)
                Node(hub, state, others[i], Ring(all.IndexOf(others[i]), all.Count, radius), NodeSize * scale, scale, font, portrait,
                    false, others[i].id == focusId, select);

            var player = state.Find(state.playerId);
            if (player != null)
                Node(hub, state, player, Vector2.zero, PlayerSize * scale, scale, font, portrait,
                    true, player.id == focusId, select);

            Legend(area, sideLegend ? TitleHeight * scale : legendTop, scale, font, sideLegend);
        }

        private static void Edge(RectTransform hub, Vector2 from, Vector2 to, Kind kind, double score, float scale)
        {
            var delta = to - from;
            float length = delta.magnitude;
            if (length <= 1f) return;

            // Thickness carries strength; a neutral edge still draws, thin, because "no strong
            // feeling" and "no relationship recorded" are different states.
            float weight = kind == Kind.Neutral
                ? 1.5f * scale
                : Mathf.Lerp(2f, 5f, Mathf.Clamp01((float)(Math.Abs(score) / 60d))) * scale;
            var tint = EdgeColour(kind);
            var colour = new Color(tint.r, tint.g, tint.b, kind == Kind.Neutral ? .35f : .8f);

            var edge = new GameObject(EdgeName, typeof(RectTransform)).GetComponent<RectTransform>();
            edge.SetParent(hub, false);
            edge.anchorMin = new Vector2(.5f, .5f); edge.anchorMax = new Vector2(.5f, .5f);
            edge.pivot = new Vector2(0f, .5f);
            edge.anchoredPosition = from;
            edge.sizeDelta = new Vector2(length, weight);
            edge.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            if (kind == Kind.Distrust)
            {
                // The mockup's dashed line: distrust is a broken connection, drawn as one.
                float dash = 9f * scale, gap = 6f * scale;
                for (float at = 0f; at < length; at += dash + gap)
                    Segment(edge, at, Mathf.Min(dash, length - at), weight, colour);
            }
            else
            {
                Segment(edge, 0f, length, weight, colour);
            }
        }

        private static void Segment(RectTransform edge, float at, float length, float weight, Color colour)
        {
            var bar = HudPrimitives.Fill("Segment", edge, colour, 1);
            bar.anchorMin = new Vector2(0f, .5f); bar.anchorMax = new Vector2(0f, .5f);
            bar.pivot = new Vector2(0f, .5f);
            bar.anchoredPosition = new Vector2(at, 0f);
            bar.sizeDelta = new Vector2(length, weight);
        }

        private static void Node(
            RectTransform hub, EpisodeState state, ContestantState actor, Vector2 position, float diameter,
            float scale, TMP_FontAsset font, Func<string, Texture> portrait, bool isPlayer, bool selected,
            Action<string> select)
        {
            var kind = isPlayer ? Kind.Neutral : KindOf(state, actor.id);
            var ring = isPlayer ? UiTheme.Accent : RingColour(kind);

            // The node is a button so the column can be pointed at anyone. Its GameObject carries
            // the houseguest's name, decorated, which is what the keyboard restore and a screen
            // reader identify it by; the chip below carries the given name the mockup shows.
            float hit = diameter + 14f * scale;
            var holder = new GameObject(NodeName(actor.name), typeof(RectTransform), typeof(Image), typeof(Button))
                .GetComponent<RectTransform>();
            holder.SetParent(hub, false);
            Centre(holder, position, hit, hit);
            var image = holder.GetComponent<Image>();
            image.sprite = UiTheme.Circle();
            image.type = Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var button = holder.GetComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, .12f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(1f, 1f, 1f, .25f);
            button.colors = colours;
            string id = actor.id;
            button.onClick.AddListener(() =>
            {
                Select(state, id);
                select?.Invoke(id);
            });

            if (selected)
            {
                // The mockup's blue halo on the houseguest being read.
                var halo = HudPrimitives.Disc("Halo", holder, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .35f));
                Centre(halo, Vector2.zero, diameter + 18f * scale, diameter + 18f * scale);
            }

            var rim = HudPrimitives.Portrait(holder, portrait != null ? portrait(actor.id) : null, ring, diameter, RingWidth * scale, false);
            Centre(rim, Vector2.zero, rim.sizeDelta.x, rim.sizeDelta.y);
            HudPrimitives.AddRoleMark(rim, MarkFor(state, actor), rim.sizeDelta.x);
            MoodBadge(rim, actor.mood, diameter, scale);

            var chip = NameChip(holder, isPlayer ? "YOU" : GivenName(actor.name),
                isPlayer ? UiTheme.Accent : UiTheme.Paper, hit, scale, font);
            chip.anchorMin = new Vector2(.5f, 0f); chip.anchorMax = new Vector2(.5f, 0f);
            chip.pivot = new Vector2(.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, 4f * scale);
        }

        private static HudPrimitives.RoleMark MarkFor(EpisodeState state, ContestantState actor)
        {
            if (actor.id == state.hohId) return HudPrimitives.RoleMark.HeadOfHousehold;
            if (actor.id == state.vetoHolderId) return HudPrimitives.RoleMark.VetoHolder;
            if (state.nominees != null && state.nominees.Contains(actor.id)) return HudPrimitives.RoleMark.Nominee;
            return HudPrimitives.RoleMark.None;
        }

        /// <summary>The mood face on a portrait's lower shoulder, in the mood's colour.</summary>
        private static void MoodBadge(RectTransform rim, string mood, float diameter, float scale)
        {
            float size = Mathf.Max(14f, diameter * .36f);
            var badge = HudPrimitives.Disc("Mood", rim, UiTheme.Ink);
            badge.anchorMin = new Vector2(1f, 0f); badge.anchorMax = new Vector2(1f, 0f);
            badge.pivot = new Vector2(.5f, .5f);
            badge.anchoredPosition = new Vector2(-size * .3f, size * .3f);
            badge.sizeDelta = new Vector2(size, size);

            var glyph = UiTheme.Icon(MoodIcon(mood)) ?? UiTheme.Icon("mood-neutral");
            if (glyph == null)
            {
                var pip = HudPrimitives.Disc("Face", badge, MoodColour(mood));
                Centre(pip, Vector2.zero, size * .55f, size * .55f);
                return;
            }
            var art = new GameObject("Face", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            art.SetParent(badge, false);
            Centre(art, Vector2.zero, size * .82f, size * .82f);
            var image = art.GetComponent<Image>();
            image.sprite = glyph; image.color = MoodColour(mood);
            image.preserveAspect = true; image.raycastTarget = false;
        }

        private static RectTransform NameChip(Transform parent, string word, Color tint, float availableWidth, float scale, TMP_FontAsset font)
        {
            // A legal custom name may be a 100-character word. Keep its label within its
            // node's reserved width; selecting the named button reveals the full identity.
            float width = Mathf.Min(availableWidth,Mathf.Max(40f, word.Length * 7.4f + 16f) * scale);
            float height = 19f * scale;
            var chip = HudPrimitives.Fill("Name chip", parent, new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, .92f), 7);
            chip.sizeDelta = new Vector2(width, height);
            UiTheme.AddBorder(chip, 7, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .7f));
            var label = Text(chip, word, 12, tint, UiTheme.Weight.Medium, scale, font, TextAlignmentOptions.Center);
            label.enableAutoSizing = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4f, 0f); label.rectTransform.offsetMax = new Vector2(-4f, 0f);
            return chip;
        }

        /// <summary>The key, under the ring: what each line means, and what each mark on a face means.</summary>
        private static void Legend(RectTransform area, float top, float scale, TMP_FontAsset font, bool side)
        {
            var legend = new GameObject(LegendName, typeof(RectTransform)).GetComponent<RectTransform>();
            legend.SetParent(area, false);
            legend.anchorMin = new Vector2(0f, 1f); legend.anchorMax = new Vector2(1f, 1f);
            legend.pivot = new Vector2(.5f, 1f);
            legend.anchoredPosition = new Vector2(0f, -top);
            legend.sizeDelta = new Vector2(0f, LegendHeight * scale);
            if(side)
            {
                legend.anchorMin = legend.anchorMax = Vector2.one; legend.pivot = Vector2.one;
                legend.sizeDelta = new Vector2(180f * scale,280f * scale);
            }

            float cell = 128f * scale, rowHeight = 20f * scale;
            var lines = new[]
            {
                ("Friendship", Kind.Friendship),
                ("Alliance", Kind.Alliance),
                ("Rivalry", Kind.Rivalry),
                ("Distrust", Kind.Distrust),
                ("Neutral", Kind.Neutral),
            };
            for (int i = 0; i < lines.Length; i++)
            {
                float x = 6f * scale + (side ? 0 : i % 3) * cell;
                float y = -(side ? i : i / 3) * rowHeight;
                var (caption, kind) = lines[i];
                var tint = EdgeColour(kind);
                float weight = (kind == Kind.Neutral ? 1.5f : 3f) * scale;
                if (kind == Kind.Distrust)
                {
                    for (float at = 0f; at < 22f; at += 8f)
                    {
                        var dash = HudPrimitives.Fill("Sample", legend, tint, 1);
                        Place(dash, x + at * scale, y - (10f * scale - weight * .5f), 5f * scale, weight);
                    }
                }
                else
                {
                    var sample = HudPrimitives.Fill("Sample", legend, tint, 1);
                    Place(sample, x, y - (10f * scale - weight * .5f), 22f * scale, weight);
                }
                var text = Text(legend, caption, 12, UiTheme.Muted, UiTheme.Weight.Regular, scale, font);
                Place(text.rectTransform, x + 28f * scale, y - 1f * scale, cell - 30f * scale, 18f * scale);
            }

            // The marks a face can carry. Drawn from the same sprites the cast rail pins on.
            var marks = new[]
            {
                ("crown", "HoH", UiTheme.Gold),
                ("veto-token", "Veto", UiTheme.Gold),
                ("target", "Nominee", UiTheme.Danger),
            };
            float markRow = -(side ? 6f : 2f) * rowHeight;
            for (int i = 0; i < marks.Length; i++)
            {
                float x = 6f * scale + (side ? 0 : i) * cell;
                float markY = markRow - (side ? i * rowHeight : 0f);
                var (icon, caption, tint) = marks[i];
                var glyph = UiTheme.Icon(icon);
                if (glyph != null)
                {
                    var art = new GameObject("Key glyph", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    art.SetParent(legend, false);
                    Place(art, x + 4f * scale, markY - 3f * scale, 13f * scale, 13f * scale);
                    var image = art.GetComponent<Image>();
                    image.sprite = glyph; image.color = tint;
                    image.raycastTarget = false; image.preserveAspect = true;
                }
                else
                {
                    var pip = HudPrimitives.Disc("Key", legend, tint);
                    Place(pip, x + 5f * scale, markY - 4f * scale, 10f * scale, 10f * scale);
                }
                var text = Text(legend, caption, 12, UiTheme.Muted, UiTheme.Weight.Regular, scale, font);
                Place(text.rectTransform, x + 28f * scale, markY - 1f * scale, cell - 30f * scale, 18f * scale);
            }

            // The caveat the notebook used to repeat in its title: it belongs with the key.
            var note = Text(legend, PerspectiveCopy, 12, UiTheme.Muted, UiTheme.Weight.Regular, scale, font);
            note.textWrappingMode = TextWrappingModes.Normal;
            note.rectTransform.anchorMin = new Vector2(0f, 1f); note.rectTransform.anchorMax = new Vector2(1f, 1f);
            note.rectTransform.pivot = new Vector2(.5f, 1f);
            note.rectTransform.anchoredPosition = new Vector2(0f, -(side ? 10f : 3f) * rowHeight - 4f * scale);
            note.rectTransform.offsetMin = new Vector2(6f * scale, note.rectTransform.offsetMin.y);
            note.rectTransform.offsetMax = new Vector2(-6f * scale, note.rectTransform.offsetMax.y);
            note.rectTransform.sizeDelta = new Vector2(note.rectTransform.sizeDelta.x, (side ? 72f : 36f) * scale);
        }

        // ------------------------------------------------------------------ the column

        private static void Column(
            RectTransform root, EpisodeState state, ContestantState focus, float scale, TMP_FontAsset font,
            Func<string, Texture> portrait, float height)
        {
            var scrollRoot=HudPrimitives.Fill(DetailsScrollName,root,new Color(0,0,0,0),UiTheme.GlassRadius);
            scrollRoot.GetComponent<Image>().raycastTarget=true;
            var element=scrollRoot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth=element.minWidth=ColumnWidth*scale+14f;
            element.preferredHeight=element.minHeight=height;element.flexibleWidth=0;
            var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();
            var viewport=new GameObject("Details viewport",typeof(RectTransform),typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(scrollRoot,false);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
            viewport.offsetMin=new Vector2(0,36f*scale);viewport.offsetMax=new Vector2(-14,0);
            var column = new GameObject(ColumnName, typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(viewport, false);
            column.anchorMin=new Vector2(0,1);column.anchorMax=Vector2.one;column.pivot=new Vector2(.5f,1);column.sizeDelta=Vector2.zero;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=column;scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;
            scroll.scrollSensitivity=28;scroll.movementType=ScrollRect.MovementType.Clamped;
            var track=HudPrimitives.Fill("Details scrollbar",scrollRoot,UiTheme.Surface,3);
            track.anchorMin=new Vector2(1,0);track.anchorMax=Vector2.one;track.pivot=new Vector2(1,.5f);
            track.offsetMin=new Vector2(-9,36f*scale);track.offsetMax=Vector2.zero;
            var handle=HudPrimitives.Fill("Details handle",track,UiTheme.Accent,3);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.targetGraphic.raycastTarget=true;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            DetailsScrollButton(scrollRoot,scroll,"Read earlier details",0,1,scale,font);
            DetailsScrollButton(scrollRoot,scroll,"Read later details",1,-1,scale,font);
            var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(12f * scale);
            stack.padding = new RectOffset(pad, pad, pad, pad);
            stack.spacing = 6f * scale;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true; stack.childControlHeight = true;
            stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;

            // The glass sits behind the stack, outside the layout, stretched to whatever height the
            // rows come to. Glass() adds its glow and hairline as children of the panel, which is
            // why the panel is not the layout group itself.
            var glass = HudPrimitives.Fill("Glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            UiTheme.AddBorder(glass, UiTheme.GlassRadius, new Color(UiTheme.Hairline.r,UiTheme.Hairline.g,UiTheme.Hairline.b,.3f));
            glass.anchorMin = Vector2.zero; glass.anchorMax = Vector2.one;
            glass.offsetMin = Vector2.zero; glass.offsetMax = Vector2.zero;
            glass.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            glass.SetAsFirstSibling();

            if (focus == null)
            {
                Note(column, "Nothing to read yet.", scale, font);
                return;
            }

            bool isPlayer = focus.isPlayer || focus.id == state.playerId;
            Header(column, state, focus, isPlayer, scale, font, portrait);
            Stats(column, state, focus, isPlayer, scale, font);

            if (isPlayer)
            {
                var allies = Allies(state);
                SectionHeading(column, AlliesHeading, allies.Count, scale, font);
                if (allies.Count == 0) Note(column, "No one yet.", scale, font);
                foreach (var ally in allies.Take(ListLimit))
                    PersonRow(column, ally, AllyWord(state, ally.id), UiTheme.Allied, scale, font, portrait);

                var rivals = Rivals(state);
                SectionHeading(column, RivalsHeading, rivals.Count, scale, font);
                if (rivals.Count == 0) Note(column, "No one yet.", scale, font);
                foreach (var rival in rivals.Take(ListLimit))
                    PersonRow(column, rival, RivalWord(state, rival.id), UiTheme.Conflict, scale, font, portrait);

                var secrets = state.memories.Where(m => m.ownerId == state.playerId)
                    .OrderByDescending(m => m.week).ToList();
                SectionHeading(column, SecretsHeading, secrets.Count, scale, font);
                if (secrets.Count == 0) Note(column, "Nothing yet.", scale, font);
                foreach (var secret in secrets.Take(ListLimit))
                    TextRow(column, secret.isPrivate ? "eye" : "journal", UiTheme.Strategic, secret.text, scale, font);
            }
            else
            {
                var between = new List<string>();
                foreach (var alliance in state.alliances.Where(a => a.active && a.members.Contains(state.playerId) && a.members.Contains(focus.id)))
                    between.Add(alliance.name + " · " + Localisation.Text("alliance"));
                foreach (var promise in state.promises.Where(p =>
                             (p.fromId == state.playerId && p.toId == focus.id) || (p.fromId == focus.id && p.toId == state.playerId)))
                    between.Add((promise.fromId == state.playerId
                                    ? Localisation.Text("You promised") + " " + PromiseWord(promise.kind)
                                    : GivenName(focus.name) + " " + Localisation.Text("promised you") + " " + PromiseWord(promise.kind))
                                + " · " + promise.status);
                SectionHeading(column, BetweenHeading, between.Count, scale, font);
                if (between.Count == 0) Note(column, "No promises or alliances between you.", scale, font);
                foreach (var line in between.Take(ListLimit))
                    TextRow(column, "handshake", UiTheme.AccentDeep, line, scale, font);

                var known = state.memories.Where(m => m.ownerId == state.playerId && m.subjectId == focus.id)
                    .OrderByDescending(m => m.week).ToList();
                SectionHeading(column, KnownHeading, known.Count, scale, font);
                if (known.Count == 0) Note(column, "Nothing recorded yet.", scale, font);
                foreach (var memory in known.Take(ListLimit))
                    TextRow(column, memory.isPrivate ? "eye" : "journal", UiTheme.Strategic,
                        Localisation.Text("Week") + " " + memory.week + ": " + memory.text, scale, font);

                var record = state.relationships.FirstOrDefault(r => r.fromId == state.playerId && r.toId == focus.id);
                var history = record != null
                    ? record.events.Where(e => !string.IsNullOrEmpty(e.description)).OrderByDescending(e => e.sequence).ToList()
                    : new List<RelationshipEventState>();
                SectionHeading(column, HistoryHeading, history.Count, scale, font);
                if (history.Count == 0) Note(column, "Nothing yet.", scale, font);
                foreach (var entry in history.Take(ListLimit))
                    TextRow(column, entry.impactScore >= 0 ? "heart" : "target",
                        entry.impactScore >= 0 ? UiTheme.Allied : UiTheme.Conflict,
                        Localisation.Text("Week") + " " + entry.week + " · " + entry.description, scale, font);
            }
        }

        private static void DetailsScrollButton(RectTransform parent,ScrollRect scroll,string caption,int slot,int direction,float scale,TMP_FontAsset font)
        {
            var rect=HudPrimitives.Fill(caption,parent,UiTheme.Surface,6);
            rect.anchorMin=rect.anchorMax=new Vector2(slot*.5f,0);rect.pivot=Vector2.zero;
            rect.anchoredPosition=new Vector2(slot==0?0:3f,0);rect.sizeDelta=new Vector2((ColumnWidth*scale+14f)/2f-3f,30f*scale);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.targetGraphic.raycastTarget=true;
            button.onClick.AddListener(()=>{Canvas.ForceUpdateCanvases();float travel=scroll.content.rect.height-scroll.viewport.rect.height;
                if(travel>1){scroll.StopMovement();scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+direction*scroll.viewport.rect.height*.75f/travel);}});
            var label=Text(rect,slot==0?"Scroll up":"Scroll down",12,UiTheme.Paper,UiTheme.Weight.Medium,scale,font,TextAlignmentOptions.Center);
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=Vector2.zero;label.rectTransform.offsetMax=Vector2.zero;
        }

        private static string PromiseWord(PromiseKind kind)
        {
            switch (kind)
            {
                case PromiseKind.Safety: return Localisation.Text("safety");
                case PromiseKind.Vote: return Localisation.Text("a vote");
                case PromiseKind.FinalTwo: return Localisation.Text("a final two");
                case PromiseKind.AllianceLoyalty: return Localisation.Text("alliance loyalty");
                case PromiseKind.Information: return Localisation.Text("information");
                default: return kind.ToString();
            }
        }

        /// <summary>The portrait, the name, who they are, and the mood and status chips.</summary>
        private static void Header(
            RectTransform column, EpisodeState state, ContestantState focus, bool isPlayer, float scale,
            TMP_FontAsset font, Func<string, Texture> portrait)
        {
            var row = Row(column, 84f * scale);
            float face = 56f * scale;
            var kind = isPlayer ? Kind.Neutral : KindOf(state, focus.id);
            var rim = HudPrimitives.Portrait(row, portrait != null ? portrait(focus.id) : null,
                isPlayer ? UiTheme.Accent : RingColour(kind), face, RingWidth * scale, false);
            rim.anchorMin = new Vector2(0f, 1f); rim.anchorMax = new Vector2(0f, 1f);
            rim.pivot = new Vector2(0f, 1f);
            rim.anchoredPosition = new Vector2(0f, 0f);
            HudPrimitives.AddRoleMark(rim, MarkFor(state, focus), rim.sizeDelta.x);

            float left = face + 14f * scale;
            float width = (ColumnWidth - 24f) * scale - left;
            var name = Heading(row, focus.name ?? string.Empty, 17, UiTheme.Paper, scale, font);
            name.characterSpacing = 0f;
            name.enableAutoSizing = false;
            name.textWrappingMode = TextWrappingModes.Normal;
            float nameHeight = Mathf.Max(24f * scale,name.GetPreferredValues(name.text,width,float.PositiveInfinity).y + 2f * scale);
            Place(name.rectTransform, left, -1f * scale, width, nameHeight);
            var rowSize = row.GetComponent<LayoutElement>();
            rowSize.minHeight = rowSize.preferredHeight = nameHeight + 60f * scale;

            string line = isPlayer ? Localisation.Text("You") : CardLine(focus) ?? Localisation.Text("Houseguest");
            var who = Text(row, line, 12, UiTheme.Muted, UiTheme.Weight.Regular, scale, font);
            Fit(who, 9f * scale);
            Place(who.rectTransform, left, -nameHeight - 2f * scale, width, 18f * scale);

            // Mood and status as pills, the way the mockup tags a houseguest. Both restate what
            // the glyphs on the portrait already show, in words.
            float x = left;
            string mood = string.IsNullOrEmpty(focus.mood) ? "Neutral" : focus.mood;
            x += Chip(row, mood, MoodColour(mood), x, -nameHeight - 24f * scale, scale) + 6f * scale;
            string badge = focus.id == state.hohId ? "HOH"
                : focus.id == state.vetoHolderId ? "VETO"
                : state.nominees != null && state.nominees.Contains(focus.id) ? "NOM" : null;
            if (badge != null) Chip(row, badge, badge == "NOM" ? UiTheme.Danger : UiTheme.Gold, x, -nameHeight - 24f * scale, scale);
        }

        /// <summary>Three tiles: counts for the player, the reading for anyone else.</summary>
        private static void Stats(
            RectTransform column, EpisodeState state, ContestantState focus, bool isPlayer, float scale, TMP_FontAsset font)
        {
            var row = Row(column, 66f * scale);
            var rule = HudPrimitives.Fill("Rule", row, UiTheme.Outline, 1);
            rule.anchorMin = new Vector2(0f, 1f); rule.anchorMax = new Vector2(1f, 1f);
            rule.pivot = new Vector2(.5f, 1f);
            rule.anchoredPosition = Vector2.zero;
            rule.sizeDelta = new Vector2(0f, 1f);

            (string icon, string value, string caption, Color tint)[] tiles;
            if (isPlayer)
            {
                tiles = new[]
                {
                    ("handshake", Allies(state).Count.ToString(), "Allies", UiTheme.Allied),
                    ("target", Rivals(state).Count.ToString(), "Rivals", UiTheme.Conflict),
                    ("people", state.alliances.Count(a => a.active && a.members.Contains(state.playerId)).ToString(), "Alliances", UiTheme.AccentDeep),
                };
            }
            else
            {
                var kind = KindOf(state, focus.id);
                tiles = new[]
                {
                    ("heart", state.Score(state.playerId, focus.id).ToString("0"), "Your trust", EdgeColour(kind)),
                    ("eye", Localisation.Text(StandingWord(kind)), "Standing", EdgeColour(kind)),
                    ("people", state.alliances.Count(a => a.active && a.members.Contains(state.playerId) && a.members.Contains(focus.id)).ToString(), "Shared", UiTheme.AccentDeep),
                };
            }

            float width = (ColumnWidth - 24f) * scale / tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                var (icon, value, caption, tint) = tiles[i];
                float x = i * width;
                var glyph = UiTheme.Icon(icon);
                if (glyph != null)
                {
                    var art = new GameObject("Stat glyph", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    art.SetParent(row, false);
                    Place(art, x + width * .5f - 7f * scale, -7f * scale, 14f * scale, 14f * scale);
                    var image = art.GetComponent<Image>();
                    image.sprite = glyph; image.color = tint;
                    image.raycastTarget = false; image.preserveAspect = true;
                }
                var number = Heading(row, value, 16, UiTheme.Paper, scale, font, TextAlignmentOptions.Center);
                number.characterSpacing = 0f;
                Fit(number, 10f * scale);
                Place(number.rectTransform, x, -23f * scale, width, 22f * scale);
                var label = Text(row, caption, 10, UiTheme.Muted, UiTheme.Weight.Regular, scale, font, TextAlignmentOptions.Center);
                Fit(label, 8f * scale);
                Place(label.rectTransform, x, -46f * scale, width, 16f * scale);
            }
        }

        private static void SectionHeading(RectTransform column, string caption, int count, float scale, TMP_FontAsset font)
        {
            var row = Row(column, 28f * scale);
            var heading = Heading(row, caption, 13, UiTheme.Paper, scale, font);
            Fit(heading, 9f * scale);
            Place(heading.rectTransform, 0f, -4f * scale, (ColumnWidth - 24f - 40f) * scale, 20f * scale);
            var tally = Text(row, count.ToString(), 12, UiTheme.Accent, UiTheme.Weight.Medium, scale, font, TextAlignmentOptions.Right);
            tally.rectTransform.anchorMin = new Vector2(1f, 1f); tally.rectTransform.anchorMax = new Vector2(1f, 1f);
            tally.rectTransform.pivot = new Vector2(1f, 1f);
            tally.rectTransform.anchoredPosition = new Vector2(0f, -5f * scale);
            tally.rectTransform.sizeDelta = new Vector2(36f * scale, 18f * scale);
            var rule = HudPrimitives.Fill("Rule", row, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .45f), 1);
            rule.anchorMin = new Vector2(0f, 0f); rule.anchorMax = new Vector2(1f, 0f);
            rule.pivot = new Vector2(.5f, 0f);
            rule.anchoredPosition = Vector2.zero;
            rule.sizeDelta = new Vector2(0f, 1f);
        }

        /// <summary>A face, a name, and one line in a colour: an ally or a rival.</summary>
        private static void PersonRow(
            RectTransform column, ContestantState actor, string word, Color tint, float scale, TMP_FontAsset font,
            Func<string, Texture> portrait)
        {
            var row = Row(column, 40f * scale);
            float face = 30f * scale;
            var rim = HudPrimitives.Portrait(row, portrait != null ? portrait(actor.id) : null, tint, face, 2f * scale, false);
            rim.anchorMin = new Vector2(0f, .5f); rim.anchorMax = new Vector2(0f, .5f);
            rim.pivot = new Vector2(0f, .5f);
            rim.anchoredPosition = new Vector2(0f, 0f);

            float left = face + 12f * scale;
            float width = (ColumnWidth - 24f) * scale - left;
            var name = Text(row, actor.name ?? string.Empty, 13, UiTheme.Paper, UiTheme.Weight.Medium, scale, font);
            Fit(name, 9f * scale);
            Place(name.rectTransform, left, -1f * scale, width, 18f * scale);

            var glyph = UiTheme.Icon(MoodIcon(actor.mood)) ?? UiTheme.Icon("mood-neutral");
            float textLeft = left;
            if (glyph != null)
            {
                var art = new GameObject("Mood", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                art.SetParent(row, false);
                Place(art, left, -20f * scale, 12f * scale, 12f * scale);
                var image = art.GetComponent<Image>();
                image.sprite = glyph; image.color = MoodColour(actor.mood);
                image.raycastTarget = false; image.preserveAspect = true;
                textLeft += 16f * scale;
            }
            var reason = Text(row, word, 11, tint, UiTheme.Weight.Regular, scale, font);
            Fit(reason, 8f * scale);
            Place(reason.rectTransform, textLeft, -19f * scale, width - (textLeft - left), 16f * scale);
        }

        /// <summary>A glyph and a sentence that wraps to as many lines as it needs.</summary>
        private static void TextRow(RectTransform column, string icon, Color tint, string copy, float scale, TMP_FontAsset font)
        {
            var row = new GameObject("Line", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(column, false);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f * scale;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            var glyph = UiTheme.Icon(icon);
            var mark = new GameObject("Glyph", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            mark.SetParent(row, false);
            var markElement = mark.gameObject.AddComponent<LayoutElement>();
            markElement.preferredWidth = 14f * scale; markElement.minWidth = 14f * scale;
            markElement.preferredHeight = 14f * scale; markElement.minHeight = 14f * scale;
            var image = mark.GetComponent<Image>();
            image.raycastTarget = false; image.preserveAspect = true;
            if (glyph != null) { image.sprite = glyph; image.color = tint; }
            else { image.sprite = UiTheme.Circle(); image.color = tint; }

            var text = Text(row, copy, 12, UiTheme.Paper, UiTheme.Weight.Regular, scale, font);
            text.textWrappingMode = TextWrappingModes.Normal;
            var textElement = text.gameObject.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;
        }

        private static void Note(RectTransform column, string copy, float scale, TMP_FontAsset font)
        {
            var text = Text(column, copy, 12, UiTheme.Muted, UiTheme.Weight.Regular, scale, font);
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A fixed-height row in the column, positioned by the stack.</summary>
        private static RectTransform Row(RectTransform column, float height)
        {
            var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(column, false);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height; element.preferredHeight = height;
            return row;
        }

        /// <summary>A pill at a position; returns its width so the next one can follow it.</summary>
        private static float Chip(RectTransform parent, string word, Color tint, float x, float y, float scale)
        {
            string shown = Localisation.Text(word);
            float width = Mathf.Max(44f, shown.Length * 6.8f + 20f) * scale;
            var chip = HudPrimitives.Chip("Chip", parent, shown, tint, width, 22f * scale);
            chip.anchorMin = new Vector2(0f, 1f); chip.anchorMax = new Vector2(0f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            chip.anchoredPosition = new Vector2(x, y);
            var label = chip.GetComponentInChildren<TMP_Text>();
            if (label != null) label.textWrappingMode = TextWrappingModes.NoWrap;
            return width;
        }

        /// <summary>"The Diplomat · 31 · Mediator", or as much of it as the save holds.</summary>
        private static string CardLine(ContestantState actor)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(actor.archetype)) parts.Add(actor.archetype);
            if (actor.age > 0) parts.Add(actor.age.ToString());
            if (!string.IsNullOrEmpty(actor.occupation)) parts.Add(actor.occupation);
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        private static string GivenName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }

        /// <summary>Lets a label shrink to fit its box, so a long name never clips.</summary>
        private static void Fit(TMP_Text label, float minimum)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(minimum, label.fontSize);
        }

        private static TMP_Text Heading(
            Transform parent, string value, int size, Color colour, float scale, TMP_FontAsset font,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = HudPrimitives.Heading("Text", parent, Mathf.RoundToInt(size * scale), colour, alignment);
            if (label.font == null && font != null) label.font = font;
            label.text = Localisation.Text(value);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>Every string on screen goes through the localisation table here.</summary>
        private static TMP_Text Text(
            Transform parent, string value, int size, Color colour, UiTheme.Weight weight, float scale,
            TMP_FontAsset font, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = HudPrimitives.Label("Text", parent, Mathf.RoundToInt(size * scale), colour, alignment);
            var face = UiTheme.Font(weight);
            if (face != null) label.font = face;
            else if (font != null) label.font = font;
            label.text = Localisation.Text(value);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        /// <summary>Top-left anchored placement, so a row reads left to right.</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Centre(RectTransform rect, Vector2 position, float width, float height)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
