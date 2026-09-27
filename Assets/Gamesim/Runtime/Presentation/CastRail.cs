using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The permanent cast strip along the bottom of the frame: every houseguest's face, name, mood
    /// and standing, visible in every frame of the episode.
    ///
    /// <para>Before this, the cast was legible only as 1.4%-of-frame silhouettes on the set, and the
    /// HUD named people in prose — so "Jamie Roberts is the replacement nominee" asked the player to
    /// remember which of six tiny figures that was. A broadcast never does that: the faces are
    /// always on screen and the badges carry the state. This is the single change that most affects
    /// how every other frame reads, which is why it is permanent chrome rather than a panel someone
    /// has to open.</para>
    ///
    /// <para>V2 (VISUAL-TARGET.md §4, mockup-01 and -10) makes each entry a <em>portrait chip</em>
    /// rather than a bare portrait: the mockups' glass ground with a cyan hairline, the face, a mood
    /// face glyph drawn in the mood's own colour, and the mood as a single word underneath. The
    /// glyph and the word say the same thing twice on purpose — a coloured face is not a state a
    /// screen reader can announce, so the word carries it and the glyph decorates it.</para>
    ///
    /// <para>It was a two-wide column down the LEFT edge, and moving it to a row along the bottom
    /// is the change the rest of the HUD was waiting on. The mockups spend the left gutter on a
    /// navigation rail, not on twelve faces, and twelve faces in a 184-px column is also why the
    /// chips had to shrink below the text size the player asked for. A row spends the one part of
    /// the frame nothing else wants - the band above the lower third - and hands the gutter back.
    /// The band is narrow, so the strip stops short of the right column rather than running under
    /// it; see <see cref="Bottom"/>.</para>
    ///
    /// <para>Each chip also says where the PLAYER stands with that houseguest - Allied, Friendly,
    /// Wary or Hostile - in a dark pill of its own at the ring's foot, beside the role pill when
    /// there is one. A five-week ally and a stranger used to be the same grey chip. Every other
    /// channel keeps its one meaning: the ring is the role, the border is the camera, the word
    /// under the name is the houseguest's mood. Neutral says nothing, so the first frame of a
    /// season - every score at its opening value - is the strip it always was.</para>
    ///
    /// <para>What the tag reads is the player's own record: their outbound score and the alliances
    /// they are in, through <see cref="RelationshipWeb.KindOf"/> - never a houseguest's private
    /// view of the player, and never anything between two houseguests. One known leak sits under
    /// that record and is not this strip's to fix: an act an NPC initiates moves the player's
    /// outbound score by the reciprocal draw. It already shows in the web and the conversation
    /// header; the strip makes it more visible, not new. (The other - a weekly settle dissolving the
    /// player's alliance on the partner's private score, without a word - is gone: the player's
    /// alliance now sours only on the player's own reading, and its ending is told.)</para>
    ///
    /// <para>Rebuilt from committed state on each HUD render and never animated per frame: the rail
    /// shows what the simulation holds and holds no opinion of its own.</para>
    /// </summary>
    public static class CastRail
    {
        public const string RootName = "Cast rail";
        /// <summary>
        /// One chip's footprint.
        ///
        /// <para>Set by the tightest frame the strip has to survive: twelve chips at the larger text
        /// size, on a 4:3 canvas, which the scaler reports as 1385 units wide rather than 1600. That
        /// is 1357 between the margins and 1320 of chips, and it is the number that decides this
        /// one - a wider chip cancels the player's text preference at a full house, which is the
        /// accessibility guarantee the strip had as a column and has to keep as a row.</para>
        /// </summary>
        private const float EntryWidth = 88f;

        /// <summary>Names the chip's parts carry, so a test can find them without guessing.</summary>
        public const string ChipName = "Chip";
        public const string MoodGlyphName = "Mood";
        public const string MoodWordName = "Mood word";
        public const string BadgeName = "Badge";
        public const string StandingTagName = "Standing tag";
        public const string StandingWordName = "Standing word";

        // The pills at the ring's foot. The role badge always had these numbers inline.
        private const float BadgeWidth = 46f;
        private const float BadgeHeight = 15f;
        private const float BadgeLift = 11f;
        private const float PairGap = 2f;
        /// <summary>
        /// How far the standing tag's ground leans from ink toward the standing's colour. Pinned by
        /// <c>CastRailStandingTag_ItsWordIsReadableOnEveryGroundTheStripCanGiveIt</c>: at .55, Paper
        /// on the Friendship ground falls to 4.27:1, under what body copy needs.
        /// </summary>
        private const float StandingTint = .45f;

        // The chip is one line deeper than the bare portrait it replaces — name, then mood — and the
        // depth comes out of the face rather than out of the column: the rail has to end above the
        // lower third at the default house of eight, and it did so at exactly this height before.
        private const float Portrait = 42f;
        private const float RingPadding = 3f;
        private const float EntryHeight = 96f;
        // Keep the actual hit rectangles apart as well as their inset card graphics. Abutting
        // fractional edges can overlap after the canvas transform, especially with larger text.
        private const float EntryGap = 4f;
        private const int ChipRadius = 10;

        /// <summary>
        /// The strip's own band, in the canvas's reference units, measured off the canvas floor.
        ///
        /// <para>The strip is the bottom-most thing in the frame and it runs the whole width of it.
        /// That is the only arrangement that fits: the controls box and the right column of cards
        /// both hang down into this corner and neither can move up - the vibe card already ends
        /// fifteen units above the expanded controls box on the canvas the tests measure - so a
        /// strip that tried to share their band would have to stop a third of the frame short, and
        /// twelve chips in the two thirds left over cannot honour the larger text size. Under them
        /// it is full width, and everything that used to live on the floor moved up one band: the
        /// status caption to 118, and the caption stops short of the controls box in x instead.</para>
        /// </summary>
        public const float Bottom = 14f;
        public const float Height = EntryHeight;
        /// <summary>The margin at both ends. The icon rail uses the same one down the left.</summary>
        public const float SideMargin = 14f;
        /// <summary>
        /// How small the rail may draw itself to fit the column it has.
        ///
        /// <para>Eight chips fit at full size, which is the default house; a bigger cast does not,
        /// and a rail whose last houseguest slides under the lower third is worse than one drawn a
        /// little smaller. So the whole chip scales — portrait, name and mood together — down to
        /// this floor, and past it the rail stops shrinking and runs long instead. A rail nobody can
        /// read is not an improvement on a rail that overflows.</para>
        /// </summary>
        private const float MinimumFit = 0.72f;
        // The chip is inset inside the entry so consecutive chips read as separate cards rather
        // than as one long column, and so the glow on the player's chip has somewhere to go.
        private const float ChipInsetX = 2f;
        private const float ChipTop = 2f;
        private const float ChipBottom = 4f;

        /// <summary>
        /// The mockups' landscape chip (mockup-01, -12): a photo down the left of the card and the
        /// words beside it. Used whenever the house fits the band at the player's text size; a full
        /// house at the larger size does not, and keeps the narrow chip, whose whole reason for
        /// being narrow is that twelve of them honour the text preference on a 4:3 canvas.
        /// </summary>
        private const float WideEntryWidth = 144f;
        private const float WideGap = 6f;
        private const float WidePhotoWidth = 62f;
        private const float WidePhotoHeight = 86f;
        private const float WideText = 74f;

        /// <summary>The strip's full-width glass, and the quote card at its far end.</summary>
        public const string StripName = "Cast strip";
        public const string QuoteName = "Strip quote";
        private const float QuoteWidth = 290f;

        /// <summary>How a houseguest is standing right now, and the colour that says so.</summary>
        private readonly struct Standing
        {
            public readonly string Badge;
            public readonly Color Colour;
            public readonly bool Dim;

            public Standing(string badge, Color colour, bool dim)
            {
                Badge = badge; Colour = colour; Dim = dim;
            }
        }

        /// <summary>
        /// Builds the rail under <paramref name="parent"/> and returns it.
        ///
        /// <para><paramref name="portrait"/> is injected rather than resolved here so the rail need
        /// not know how personas map to art — the HUD already owns that mapping, and a second copy
        /// of it is how the two would drift apart.</para>
        /// </summary>
        /// <summary>
        /// While a competition is being played, who is in it (mockup-05's entrant strip): each
        /// chip says Competing or Sitting out in place of its mood. Null the rest of the time. It
        /// is the engine's own field, and it claims no score for anybody - the houseguests' scores
        /// do not exist until the result commits.
        /// </summary>
        public static System.Collections.Generic.ICollection<string> CompetitionField;

        /// <summary>
        /// The player's own progress while the game is played - the one competitor whose progress
        /// exists before the result, read every frame onto their chip. Null the rest of the time.
        /// </summary>
        public static System.Func<string> PlayerProgress;

        /// <summary>The player's chip's live progress word, so a test can find it.</summary>
        public const string ProgressWordName = "Progress word";

        /// <summary>Hands the player's status word to the live progress source while a game is played.</summary>
        private static void Live(TMP_Text word, bool isPlayer)
        {
            if (!isPlayer || CompetitionField == null || PlayerProgress == null) return;
            word.name = ProgressWordName;
            word.gameObject.AddComponent<LiveText>().Source = PlayerProgress;
            string now = PlayerProgress();
            if (!string.IsNullOrEmpty(now)) word.text = now;
            word.color = UiTheme.Glow;
        }

        public static RectTransform Build(
            Transform parent, EpisodeState state, float fontScale, TMP_FontAsset font,
            System.Func<string, Texture> portrait, System.Action<string> onSelect = null,
            string followedId = null)
        {
            var order = Order(state);
            bool wide = Wide(parent as RectTransform, order.Count, fontScale);
            float scale = wide ? fontScale : Fit(parent as RectTransform, order.Count, fontScale);

            // The strip's glass first, so it draws behind the chips: a sibling of the rail rather
            // than its child, because a chip is whatever the rail's children are.
            Ground(parent, scale);

            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(SideMargin, Bottom);
            float width = (wide ? WideRailWidth(order.Count) : RailWidth(order.Count)) * scale;
            root.sizeDelta = new Vector2(width, EntryHeight * scale);

            for (int index = 0; index < order.Count; index++)
            {
                if (wide) WideEntry(root, state, order[index], index, scale, font, portrait, onSelect, followedId);
                else Entry(root, state, order[index], index, scale, font, portrait, onSelect, followedId);
            }

            // The mockups end the strip on a line of the show's own voice, where there is room for
            // it past the last chip.
            var canvas = parent as RectTransform;
            float spare = canvas != null ? canvas.rect.width - 2f * SideMargin - width : 0f;
            if (spare >= (QuoteWidth + 16f) * scale) Quote(parent, scale);
            return root;
        }

        /// <summary>Whether the house fits the band as landscape chips at the player's text size.</summary>
        private static bool Wide(RectTransform canvas, int count, float fontScale)
        {
            if (canvas == null || count <= 0) return false;
            float available = canvas.rect.width - SideMargin * 2f;
            return available > 0f && WideRailWidth(count) * fontScale <= available;
        }

        private static float WideRailWidth(int count) => Mathf.Max(0f, count * (WideEntryWidth + WideGap) - WideGap);

        /// <summary>The strip's glass: the whole width of the frame, a little proud of the chips.</summary>
        private static void Ground(Transform parent, float scale)
        {
            var ground = HudPrimitives.Fill(StripName, parent,
                new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .88f), 12);
            ground.anchorMin = new Vector2(0f, 0f); ground.anchorMax = new Vector2(1f, 0f);
            ground.pivot = new Vector2(.5f, 0f);
            ground.offsetMin = new Vector2(SideMargin - 6f, Bottom - 6f);
            ground.offsetMax = new Vector2(-(SideMargin - 6f), Bottom + EntryHeight * scale + 6f);
            ground.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(ground, 12, UiTheme.Edge(UiTheme.Emphasis.Resting));
        }

        /// <summary>The mockups' quote card at the strip's right-hand end.</summary>
        private static void Quote(Transform parent, float scale)
        {
            var card = HudPrimitives.Fill(QuoteName, parent, UiTheme.CardFill, ChipRadius);
            card.anchorMin = card.anchorMax = new Vector2(1f, 0f);
            card.pivot = new Vector2(1f, 0f);
            card.anchoredPosition = new Vector2(-SideMargin, Bottom + ChipBottom * scale);
            card.sizeDelta = new Vector2(QuoteWidth * scale, (EntryHeight - ChipTop - ChipBottom) * scale);
            card.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(card, ChipRadius, UiTheme.Edge(UiTheme.Emphasis.Resting));

            var mark = HudPrimitives.Label("Quote mark", card, Mathf.Round(44f * scale), UiTheme.Heading, TextAlignmentOptions.TopLeft);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) mark.font = bold;
            mark.text = "\u201C";
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0f, 1f);
            mark.rectTransform.pivot = new Vector2(0f, 1f);
            mark.rectTransform.anchoredPosition = new Vector2(12f * scale, -2f * scale);
            // Inter's line at 44 is 53 tall: a box shorter than that clips the mark it holds.
            mark.rectTransform.sizeDelta = new Vector2(44f * scale, 58f * scale);
            mark.enableAutoSizing = true; mark.fontSizeMax = mark.fontSize; mark.fontSizeMin = Mathf.Min(24f, mark.fontSize);

            var line = HudPrimitives.Label("Quote line", card, Mathf.Round(14f * scale),
                new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .88f), TextAlignmentOptions.MidlineLeft);
            line.fontStyle = FontStyles.Italic;
            line.text = Localisation.Text("Same house.\nDifferent stories.\nWho will you become?");
            line.enableAutoSizing = true;
            line.fontSizeMax = line.fontSize;
            line.fontSizeMin = Mathf.Min(10f, line.fontSize);
            line.rectTransform.anchorMin = Vector2.zero; line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.offsetMin = new Vector2(58f * scale, 8f * scale);
            line.rectTransform.offsetMax = new Vector2(-14f * scale, -8f * scale);
        }

        /// <summary>
        /// The scale a house of <paramref name="count"/> chips fits the band at: never larger than
        /// the player's text preference, and never below <see cref="MinimumFit"/>.
        ///
        /// <para>The fit is against WIDTH now rather than height, which is the whole of what moving
        /// the strip changed here. The floor is absolute rather than a fraction of the preference,
        /// so asking for larger text can shrink the strip back toward its resting size to keep the
        /// house on screen but can never push it smaller than a player who asked for nothing
        /// would get.</para>
        ///
        /// <para>A canvas whose rect is not laid out yet - before the first layout pass, or in a
        /// fixture with no screen - reports zero width; that gives the preference back unchanged
        /// rather than collapsing the strip to nothing.</para>
        /// </summary>
        private static float Fit(RectTransform canvas, int count, float fontScale)
        {
            if (canvas == null || count <= 0) return fontScale;
            float available = canvas.rect.width - SideMargin * 2f;
            if (available <= 0f) return fontScale;
            float floor = Mathf.Min(fontScale, MinimumFit);
            return Mathf.Clamp(Mathf.Min(fontScale, available / RailWidth(count)), floor, fontScale);
        }

        private static float RailWidth(int count) => Mathf.Max(0f, count * (EntryWidth + EntryGap) - EntryGap);

        /// <summary>
        /// The player first, then everyone still playing, then the evicted in the order they left.
        ///
        /// <para>A rail that reshuffled every week would cost the player the spatial memory that
        /// makes it worth having, so only an eviction moves anyone. Left to right now rather than
        /// top to bottom, which is the reading order of the row it became.</para>
        /// </summary>
        private static List<ContestantState> Order(EpisodeState state)
        {
            var active = new List<ContestantState>();
            var gone = new List<ContestantState>();
            ContestantState player = null;

            foreach (var actor in state.contestants)
            {
                if (actor.isPlayer || actor.id == state.playerId) { player = actor; continue; }
                if (actor.status == ContestantStatus.Active) active.Add(actor); else gone.Add(actor);
            }

            var order = new List<ContestantState>();
            if (player != null) order.Add(player);
            order.AddRange(active);
            order.AddRange(gone);
            return order;
        }

        /// <summary>A Have-Not's badge on the rail. A caption: tests and screen readers find it by these words.</summary>
        public const string HaveNotBadge = "HAVE-NOT";

        /// <summary>The badge word the rail already shows, expressed as a portrait mark.</summary>
        private static HudPrimitives.RoleMark MarkFor(string badge)
        {
            switch (badge)
            {
                case "HOH": return HudPrimitives.RoleMark.HeadOfHousehold;
                case "VETO": return HudPrimitives.RoleMark.VetoHolder;
                case "NOM": return HudPrimitives.RoleMark.Nominee;
                default: return HudPrimitives.RoleMark.None;
            }
        }

        /// <summary>
        /// Whether a houseguest is out of the house, and so drawn dimmed with their mood replaced by
        /// the fact of it. A winner and a runner-up are not active either, and they are not out: the
        /// finale's two are the whole point of the rail on its last night. Public because the test
        /// that pins the dimming has to ask the same question rather than guess at it - they drifted
        /// apart once already.
        /// </summary>
        public static bool IsOut(ContestantState actor) => actor != null
            && actor.status != ContestantStatus.Active
            && actor.status != ContestantStatus.Winner
            && actor.status != ContestantStatus.RunnerUp;

        /// <summary>
        /// Where the player stands with <paramref name="actor"/>, as the strip shows it.
        ///
        /// <para><see cref="RelationshipWeb.Kind.Neutral"/> doubles as "the strip shows nothing":
        /// for the player's own chip, for anyone no longer in the house, and for a houseguest the
        /// player has no reading of yet. Neutral is also the resting MOOD of nearly everyone, so a
        /// tag for it would make most chips read "Neutral / Neutral".</para>
        /// </summary>
        public static RelationshipWeb.Kind StandingOf(EpisodeState state, ContestantState actor)
        {
            if (state == null || actor == null) return RelationshipWeb.Kind.Neutral;
            if (actor.isPlayer || actor.id == state.playerId) return RelationshipWeb.Kind.Neutral;
            if (actor.status != ContestantStatus.Active) return RelationshipWeb.Kind.Neutral;
            return RelationshipWeb.KindOf(state, actor.id);
        }

        /// <summary>
        /// The standing tag's ground: the web's own colour for that reading, sunk toward ink, and
        /// opaque because the pill sits over the face.
        ///
        /// <para>The inverse of a role pill on purpose. A role is a saturated fill with a dark
        /// capital word; a standing is a dark fill with a light title-case word, so "Allied" alone
        /// in the slot cannot be mistaken for a role.</para>
        /// </summary>
        public static Color StandingGround(RelationshipWeb.Kind kind)
        {
            var ground = Color.Lerp(UiTheme.Ink, RelationshipWeb.StandingColour(kind), StandingTint);
            ground.a = 1f;
            return ground;
        }

        private static Standing Read(EpisodeState state, ContestantState actor)
        {
            if (actor.status == ContestantStatus.Winner) return new Standing("WINNER", UiTheme.Gold, false);
            if (actor.status == ContestantStatus.RunnerUp) return new Standing("FINAL 2", UiTheme.Accent, false);
            if (IsOut(actor)) return new Standing("OUT", UiTheme.Muted, true);
            if (actor.id == state.hohId) return new Standing("HOH", UiTheme.Gold, false);
            if (actor.id == state.vetoHolderId) return new Standing("VETO", UiTheme.Gold, false);
            if (state.nominees != null && state.nominees.Contains(actor.id)) return new Standing("NOM", UiTheme.Danger, false);
            // Below the week's powers and the block, above the player's own YOU: a Have-Not is
            // this week's news about them.
            if (HaveNots.Is(state, actor.id)) return new Standing(HaveNotBadge, UiTheme.Warning, false);
            if (actor.isPlayer || actor.id == state.playerId) return new Standing("YOU", UiTheme.Accent, false);
            return new Standing(null, UiTheme.Outline, false);
        }

        /// <summary>
        /// The one word a chip shows for a mood. Engine moods are already single words — Happy,
        /// Content, Neutral, Upset, Angry — so this only fills the gap when a save carries none,
        /// and routes the result through the localisation sink like every other string on screen.
        /// </summary>
        private static string MoodWord(ContestantState actor, Standing standing)
        {
            if (standing.Dim) return Localisation.Text("Evicted");
            if (CompetitionField != null)
                return Localisation.Text(CompetitionField.Contains(actor.id) ? "Competing" : "Sitting out");
            string mood = actor.mood;
            if (string.IsNullOrEmpty(mood)) return Localisation.Text("Neutral");
            // Defensive: a word is what fits in a 100px chip, so anything composed is cut at the
            // first space rather than allowed to clip.
            int space = mood.IndexOf(' ');
            if (space > 0) mood = mood.Substring(0, space);
            return Localisation.Text(mood);
        }

        private static RectTransform Entry(
            RectTransform root, EpisodeState state, ContestantState actor, int index, float scale,
            TMP_FontAsset font, System.Func<string, Texture> portrait, System.Action<string> onSelect,
            string followedId = null)
        {
            var standing = Read(state, actor);
            bool isPlayer = actor.isPlayer || actor.id == state.playerId;
            var moodColour = standing.Dim ? UiTheme.Muted : RelationshipWeb.MoodColour(actor.mood);

            var entry = new GameObject(actor.name, typeof(RectTransform)).GetComponent<RectTransform>();
            entry.SetParent(root, false);
            entry.anchorMin = new Vector2(0f, 1f);
            entry.anchorMax = new Vector2(0f, 1f);
            entry.pivot = new Vector2(0f, 1f);
            entry.anchoredPosition = new Vector2(index * (EntryWidth + EntryGap) * scale, 0f);
            // Both axes scale. While the strip was a column only the height did, because the column
            // was a fixed 184 wide whatever the chips did; in a row a chip that keeps its width
            // while its neighbour's position shrinks walks straight over it.
            entry.sizeDelta = new Vector2((EntryWidth - EntryGap) * scale, EntryHeight * scale);

            // The mockups' card: the night ground at 85 %, a cyan hairline, corners at the theme's
            // radius. The player's chip is the one that also carries the glow, so "which of these
            // is me" is answered from the far edge of the frame.
            var chip = HudPrimitives.Fill(ChipName, entry, UiTheme.GlassFill, ChipRadius);
            chip.anchorMin = Vector2.zero; chip.anchorMax = Vector2.one;
            chip.offsetMin = new Vector2(ChipInsetX, ChipBottom * scale);
            chip.offsetMax = new Vector2(-ChipInsetX, -ChipTop * scale);
            // Cyan says "the camera is on this one", and nothing else in the rail says it. It used
            // to say "this one is you", permanently, on a chip whose role word is already the
            // word YOU - so the rail lit one houseguest in every frame of the game for a fact the
            // rail was already stating, and had nothing left to mark the one being followed with.
            // Every other chip is structure: a seam, dimmed further when the houseguest is out.
            bool followed = !string.IsNullOrEmpty(followedId) && actor.id == followedId;
            var seam = UiTheme.Edge(UiTheme.Emphasis.Resting);
            UiTheme.AddBorder(chip, ChipRadius, followed
                ? UiTheme.Edge(UiTheme.Emphasis.Active)
                : new Color(seam.r, seam.g, seam.b, standing.Dim ? seam.a * .6f : seam.a));

            // The ring is the status: a coloured disc showing through as a rim around the face. Every
            // row below it is measured from the ring's own foot, so the chip stacks rather than
            // relying on offsets that would each have to be retuned if the portrait ever changed.
            float ring = (Portrait + RingPadding * 2f) * scale;
            float top = 6f * scale;
            var rim = Disc("Ring", entry, standing.Colour);
            rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
            rim.anchoredPosition = new Vector2(0f, -top);
            rim.sizeDelta = new Vector2(ring, ring);

            // The role badge the web build pins to a portrait: a target on a nominee, a crown on
            // the Head of Household. It repeats what the chip below already says in words, so a
            // reader who cannot see the shape loses nothing.
            HudPrimitives.AddRoleMark(rim, MarkFor(standing.Badge), ring);
            // The mood face takes the opposite shoulder, so the two marks can never collide.
            MoodFace(rim, actor.mood, moodColour, ring);

            // A circular mask over the portrait render. The face texture is square, so without this
            // the cast reads as a row of tiles rather than as a row of people.
            var frame = Disc("Frame", entry, standing.Dim ? new Color(1f, 1f, 1f, .45f) : Color.white);
            frame.anchorMin = new Vector2(.5f, 1f); frame.anchorMax = new Vector2(.5f, 1f); frame.pivot = new Vector2(.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, -(top + RingPadding * scale));
            frame.sizeDelta = new Vector2(Portrait * scale, Portrait * scale);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var face = portrait != null ? portrait(actor.id) : null;
            if (face != null)
            {
                var raw = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                raw.rectTransform.SetParent(frame, false);
                raw.rectTransform.anchorMin = Vector2.zero;
                raw.rectTransform.anchorMax = Vector2.one;
                raw.rectTransform.offsetMin = Vector2.zero;
                raw.rectTransform.offsetMax = Vector2.zero;
                raw.texture = face;
                raw.raycastTarget = false;
                raw.color = standing.Dim ? new Color(.6f, .65f, .7f, 1f) : Color.white;
            }
            else
            {
                // No authored art for this persona: the initial on the surface tone. Still a
                // face-shaped slot, so a missing portrait cannot knock the rail out of alignment.
                var fill = Disc("Initial", frame, UiTheme.SurfaceRaised);
                fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
                fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                var glyph = Label(frame, string.IsNullOrEmpty(actor.name) ? "?" : actor.name.Substring(0, 1),
                    19, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Center);
                glyph.rectTransform.anchorMin = Vector2.zero;
                glyph.rectTransform.anchorMax = Vector2.one;
                glyph.rectTransform.offsetMin = Vector2.zero;
                glyph.rectTransform.offsetMax = Vector2.zero;
            }

            // Given name only. Surnames double the width of the rail, and a player refers to these
            // people the way the house does.
            string given = actor.name ?? string.Empty;
            int nameSpace = given.IndexOf(' ');
            if (nameSpace > 0) given = given.Substring(0, nameSpace);

            var name = Label(entry, given, 12, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Top);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(.5f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(0f, -(top + ring + 6f * scale));
            name.rectTransform.sizeDelta = new Vector2(-8f, 16f * scale);

            // The mood in a word, in the mood's colour, under the name. The mockups put it exactly
            // here, and it is the half of the mood a screen reader can actually read out.
            var mood = Label(entry, MoodWord(actor, standing), 11, moodColour, scale, font, TextAlignmentOptions.Top);
            if (CompetitionField != null && !standing.Dim) mood.color = CompetitionField.Contains(actor.id) ? UiTheme.Accent : UiTheme.Muted;
            mood.name = MoodWordName;
            Live(mood, isPlayer);
            mood.rectTransform.anchorMin = new Vector2(0f, 1f);
            mood.rectTransform.anchorMax = new Vector2(1f, 1f);
            mood.rectTransform.pivot = new Vector2(.5f, 1f);
            mood.rectTransform.anchoredPosition = new Vector2(0f, -(top + ring + 23f * scale));
            mood.rectTransform.sizeDelta = new Vector2(-8f, 14f * scale);

            RectTransform badgeChip = null;
            TMP_Text badge = null;
            if (!string.IsNullOrEmpty(standing.Badge))
            {
                // The badge sits over the bottom of the rim rather than beside it, so the rail stays
                // one column wide however many people are holding something this week.
                badgeChip = new GameObject(BadgeName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                badgeChip.SetParent(entry, false);
                badgeChip.anchorMin = new Vector2(.5f, 1f); badgeChip.anchorMax = new Vector2(.5f, 1f); badgeChip.pivot = new Vector2(.5f, 1f);
                badgeChip.anchoredPosition = new Vector2(0f, -(top + ring - BadgeLift * scale));
                badgeChip.sizeDelta = new Vector2(BadgeWidth * scale, BadgeHeight * scale);
                var chipImage = badgeChip.GetComponent<Image>();
                UiTheme.Style(chipImage, standing.Colour, 4);
                chipImage.raycastTarget = false;

                badge = Label(badgeChip, standing.Badge, 10, UiTheme.Ink, scale, font, TextAlignmentOptions.Center);
                badge.rectTransform.anchorMin = Vector2.zero;
                badge.rectTransform.anchorMax = Vector2.one;
                badge.rectTransform.offsetMin = Vector2.zero;
                badge.rectTransform.offsetMax = Vector2.zero;
            }

            // Where the player stands with this houseguest, in the same row as the role and in
            // words. Parented to the entry rather than to the badge: they are two facts that happen
            // to share a row, and the tests that read a chip's role read the Badge alone.
            RectTransform tag = null;
            TMP_Text word = null;
            var kind = StandingOf(state, actor);
            if (kind != RelationshipWeb.Kind.Neutral)
            {
                tag = new GameObject(StandingTagName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                tag.SetParent(entry, false);
                tag.anchorMin = new Vector2(.5f, 1f); tag.anchorMax = new Vector2(.5f, 1f); tag.pivot = new Vector2(.5f, 1f);
                tag.anchoredPosition = new Vector2(0f, -(top + ring - BadgeLift * scale));
                tag.sizeDelta = new Vector2(BadgeWidth * scale, BadgeHeight * scale);
                var tagImage = tag.GetComponent<Image>();
                UiTheme.Style(tagImage, StandingGround(kind), 4);
                tagImage.raycastTarget = false;

                word = Label(tag, Localisation.Text(RelationshipWeb.StandingWord(kind)), 10, UiTheme.Paper,
                    scale, font, TextAlignmentOptions.Center);
                word.name = StandingWordName;
                word.rectTransform.anchorMin = Vector2.zero;
                word.rectTransform.anchorMax = Vector2.one;
                word.rectTransform.offsetMin = Vector2.zero;
                word.rectTransform.offsetMax = Vector2.zero;
            }
            StatusRow(entry, badgeChip, badge, tag, word, scale);

            // A portrait is a button: click it and the camera follows that houseguest. The button
            // carries the name chip as its caption, so the keyboard and a screen reader find it the
            // way they find every other control. The hit graphic is the last child rather than the
            // entry's own Image, so the press wash reads over the glass instead of behind it.
            if (onSelect == null) return entry;
            string id = actor.id;
            var hit = HudPrimitives.Fill("Press", entry, new Color(1f, 1f, 1f, 0f), ChipRadius);
            hit.anchorMin = Vector2.zero; hit.anchorMax = Vector2.one;
            hit.offsetMin = new Vector2(ChipInsetX, ChipBottom * scale);
            hit.offsetMax = new Vector2(-ChipInsetX, -ChipTop * scale);
            var hitImage = hit.GetComponent<Image>();
            hitImage.raycastTarget = true;
            var button = entry.gameObject.AddComponent<Button>();
            button.targetGraphic = hitImage;
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(1f, 1f, 1f, 0.25f);
            button.colors = colours;
            button.onClick.AddListener(() => onSelect(id));
            return entry;
        }

        /// <summary>
        /// The landscape chip: the houseguest's photo down the left, and beside it the mood face,
        /// the name, a bar for where the player stands, the mood in a word and the standing tag.
        ///
        /// <para>Every part the narrow chip names, it names the same way - the glass is
        /// <see cref="ChipName"/>, the mood face <see cref="MoodGlyphName"/> with its "Face", the
        /// word <see cref="MoodWordName"/>, the role <see cref="BadgeName"/>, the standing
        /// <see cref="StandingTagName"/> - so everything that reads a chip reads this one.</para>
        /// </summary>
        private static RectTransform WideEntry(
            RectTransform root, EpisodeState state, ContestantState actor, int index, float scale,
            TMP_FontAsset font, System.Func<string, Texture> portrait, System.Action<string> onSelect,
            string followedId)
        {
            var standing = Read(state, actor);
            bool isPlayer = actor.isPlayer || actor.id == state.playerId;
            var moodColour = standing.Dim ? UiTheme.Muted : RelationshipWeb.MoodColour(actor.mood);

            var entry = new GameObject(actor.name, typeof(RectTransform)).GetComponent<RectTransform>();
            entry.SetParent(root, false);
            entry.anchorMin = entry.anchorMax = new Vector2(0f, 1f);
            entry.pivot = new Vector2(0f, 1f);
            entry.anchoredPosition = new Vector2(index * (WideEntryWidth + WideGap) * scale, 0f);
            entry.sizeDelta = new Vector2(WideEntryWidth * scale, EntryHeight * scale);

            // A card on the strip's glass: lifted a step, with a seam - and the followed one lit and
            // glowing, the one channel that says "the camera is on this one".
            var chip = HudPrimitives.Fill(ChipName, entry, UiTheme.CardFill, ChipRadius);
            chip.anchorMin = Vector2.zero; chip.anchorMax = Vector2.one;
            chip.offsetMin = new Vector2(0f, ChipBottom * scale);
            chip.offsetMax = new Vector2(0f, -ChipTop * scale);
            bool followed = !string.IsNullOrEmpty(followedId) && actor.id == followedId;
            var seam = UiTheme.Edge(UiTheme.Emphasis.Resting);
            if (followed) UiTheme.AddGlow(chip, ChipRadius);
            UiTheme.AddBorder(chip, ChipRadius, followed
                ? UiTheme.Edge(UiTheme.Emphasis.Active)
                : new Color(seam.r, seam.g, seam.b, standing.Dim ? seam.a * .6f : seam.a));

            // The photo: the portrait cropped to a head-and-shoulders column with rounded corners,
            // as the mockups crop every face in the strip.
            float photoTop = (ChipTop + 4f) * scale;
            var frame = HudPrimitives.Fill("Frame", entry, UiTheme.SurfaceRaised, 8);
            frame.anchorMin = frame.anchorMax = new Vector2(0f, 1f);
            frame.pivot = new Vector2(0f, 1f);
            frame.anchoredPosition = new Vector2(5f * scale, -photoTop);
            frame.sizeDelta = new Vector2(WidePhotoWidth * scale, WidePhotoHeight * scale);
            frame.GetComponent<Image>().raycastTarget = false;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var face = portrait != null ? portrait(actor.id) : null;
            if (face != null)
            {
                var raw = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                raw.rectTransform.SetParent(frame, false);
                raw.rectTransform.anchorMin = Vector2.zero; raw.rectTransform.anchorMax = Vector2.one;
                raw.rectTransform.offsetMin = Vector2.zero; raw.rectTransform.offsetMax = Vector2.zero;
                raw.texture = face;
                // The render is square; the column is not, so take its middle rather than squash it.
                float share = WidePhotoWidth / WidePhotoHeight;
                raw.uvRect = new Rect((1f - share) * .5f, 0f, share, 1f);
                raw.raycastTarget = false;
                raw.color = standing.Dim ? new Color(.6f, .65f, .7f, 1f) : Color.white;
            }
            else
            {
                var glyph = Label(frame, string.IsNullOrEmpty(actor.name) ? "?" : actor.name.Substring(0, 1),
                    22, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Center);
                glyph.rectTransform.anchorMin = Vector2.zero; glyph.rectTransform.anchorMax = Vector2.one;
                glyph.rectTransform.offsetMin = Vector2.zero; glyph.rectTransform.offsetMax = Vector2.zero;
            }

            float x = WideText * scale;
            float column = (WideEntryWidth - WideText - 6f) * scale;

            // The mood face at the head of the column, in the mood's own colour.
            MoodFace(entry, actor.mood, moodColour, new Vector2(x, -(ChipTop + 6f) * scale), 22f * scale);

            // A role, when there is one, rides on the photo's foot, as a lower third on a face.
            RectTransform badgeChip = null;
            if (!string.IsNullOrEmpty(standing.Badge))
            {
                badgeChip = new GameObject(BadgeName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                badgeChip.SetParent(entry, false);
                badgeChip.anchorMin = badgeChip.anchorMax = new Vector2(0f, 1f);
                badgeChip.pivot = new Vector2(.5f, 0f);
                badgeChip.anchoredPosition = new Vector2((5f + WidePhotoWidth * .5f) * scale, -(photoTop + WidePhotoHeight * scale) + 4f * scale);
                badgeChip.sizeDelta = new Vector2(BadgeWidth * scale, BadgeHeight * scale);
                var chipImage = badgeChip.GetComponent<Image>();
                UiTheme.Style(chipImage, standing.Colour, 4);
                chipImage.raycastTarget = false;
                var badge = Label(badgeChip, standing.Badge, 10, UiTheme.Ink, scale, font, TextAlignmentOptions.Center);
                badge.rectTransform.anchorMin = Vector2.zero; badge.rectTransform.anchorMax = Vector2.one;
                badge.rectTransform.offsetMin = Vector2.zero; badge.rectTransform.offsetMax = Vector2.zero;
            }

            string given = actor.name ?? string.Empty;
            int nameSpace = given.IndexOf(' ');
            if (nameSpace > 0) given = given.Substring(0, nameSpace);
            var name = Label(entry, given, 14, standing.Dim ? UiTheme.Muted : UiTheme.Paper, scale, font, TextAlignmentOptions.Left);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            Fit(name, 10f);
            Place(name.rectTransform, x, (ChipTop + 31f) * scale, column, 18f * scale);

            // Where the player stands with them, as a bar in the web's colour for that reading. The
            // player's own chip is the full accent: the one relationship that is not a reading.
            var kind = StandingOf(state, actor);
            float fill = isPlayer ? 1f
                : Mathf.Clamp01((float)(state.Score(state.playerId, actor.id) + 100.0) / 200f);
            var tint = isPlayer ? UiTheme.Accent
                : kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind);
            var track = HudPrimitives.Fill("Standing track", entry,
                new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 2);
            Place(track, x, (ChipTop + 51f) * scale, column, 4f * scale);
            track.GetComponent<Image>().raycastTarget = false;
            if (!standing.Dim && fill > 0f)
            {
                var bar = HudPrimitives.Fill("Standing bar", entry, tint, 2);
                Place(bar, x, (ChipTop + 51f) * scale, column * fill, 4f * scale);
                bar.GetComponent<Image>().raycastTarget = false;
            }

            var mood = Label(entry, MoodWord(actor, standing), 12, moodColour, scale, font, TextAlignmentOptions.Left);
            if (CompetitionField != null && !standing.Dim) mood.color = CompetitionField.Contains(actor.id) ? UiTheme.Accent : UiTheme.Muted;
            mood.name = MoodWordName;
            Live(mood, isPlayer);
            Fit(mood, 9f);
            Place(mood.rectTransform, x, (ChipTop + 58f) * scale, column, 16f * scale);

            if (kind != RelationshipWeb.Kind.Neutral)
            {
                var tag = new GameObject(StandingTagName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                tag.SetParent(entry, false);
                var tagImage = tag.GetComponent<Image>();
                UiTheme.Style(tagImage, StandingGround(kind), 4);
                tagImage.raycastTarget = false;
                var word = Label(tag, Localisation.Text(RelationshipWeb.StandingWord(kind)), 10, UiTheme.Paper,
                    scale, font, TextAlignmentOptions.Center);
                word.name = StandingWordName;
                word.rectTransform.anchorMin = Vector2.zero; word.rectTransform.anchorMax = Vector2.one;
                word.rectTransform.offsetMin = Vector2.zero; word.rectTransform.offsetMax = Vector2.zero;
                float measured = word.GetPreferredValues(word.text).x + 8f * scale;
                Place(tag, x, (ChipTop + 76f) * scale, Mathf.Min(column, Mathf.Max(BadgeWidth * scale, measured)), BadgeHeight * scale);
            }

            if (onSelect == null) return entry;
            string id = actor.id;
            var hit = HudPrimitives.Fill("Press", entry, new Color(1f, 1f, 1f, 0f), ChipRadius);
            hit.anchorMin = Vector2.zero; hit.anchorMax = Vector2.one;
            hit.offsetMin = new Vector2(0f, ChipBottom * scale);
            hit.offsetMax = new Vector2(0f, -ChipTop * scale);
            var hitImage = hit.GetComponent<Image>();
            hitImage.raycastTarget = true;
            var button = entry.gameObject.AddComponent<Button>();
            button.targetGraphic = hitImage;
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.10f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(1f, 1f, 1f, 0.22f);
            button.colors = colours;
            button.onClick.AddListener(() => onSelect(id));
            return entry;
        }

        /// <summary>Top-left placement inside an entry, in the entry's own units.</summary>
        private static void Place(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Lets a label shrink to a floor rather than clip.</summary>
        private static void Fit(TMP_Text label, float floor)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(floor, label.fontSize);
        }

        /// <summary>
        /// Lays out the ring's foot: the role pill, the standing tag, or both side by side.
        ///
        /// <para>This is the only band on the chip with any horizontal room - the name and the mood
        /// word own the rows below it and there is no height for a third - so when a houseguest
        /// holds a role AND the player has a reading of them, the two pills share it, each fitted to
        /// its measured word and centred as a pair. A role alone keeps the badge it always had. A
        /// tag alone takes the badge's slot, widened only if its word needs it, so a longer
        /// translation grows the pill rather than truncating inside it.</para>
        ///
        /// <para>Everything is kept clear of the chip's BORDER, not just inside its glass: the edge
        /// is drawn on the glass's own rect, and an opaque pill across it would cut through the
        /// cyan that says the camera is on this houseguest. The border does not scale, so neither
        /// does the reserve. The one pair that cannot fit at its scaled size - VETO beside Friendly
        /// at a scale of about three quarters, where the font rounds 7.5 up to 8 - drops both words
        /// one point, to the size the next scale down already draws them at.</para>
        /// </summary>
        private static void StatusRow(
            RectTransform entry, RectTransform role, TMP_Text roleWord, RectTransform tag, TMP_Text tagWord, float scale)
        {
            float inner = entry.sizeDelta.x - 2f * ChipInsetX - 2f * (UiTheme.BorderThickness + 1f);
            if (tag == null) return;
            if (role == null)
            {
                float measured = tagWord.GetPreferredValues(tagWord.text).x;
                float solo = Mathf.Min(inner, Mathf.Max(BadgeWidth * scale, measured + 4f * scale));
                tag.sizeDelta = new Vector2(solo, tag.sizeDelta.y);
                return;
            }

            float gap = PairGap * scale;
            float roleText = roleWord.GetPreferredValues(roleWord.text).x;
            float tagText = tagWord.GetPreferredValues(tagWord.text).x;
            if (roleText + tagText + 4f * scale + gap > inner)
            {
                roleWord.fontSize -= 1f;
                tagWord.fontSize -= 1f;
                roleText = roleWord.GetPreferredValues(roleWord.text).x;
                tagText = tagWord.GetPreferredValues(tagWord.text).x;
            }
            float pad = Mathf.Clamp((inner - roleText - tagText - gap) / 4f, 1f * scale, 4f * scale);
            float roleWidth = roleText + 2f * pad;
            float tagWidth = tagText + 2f * pad;
            float total = roleWidth + gap + tagWidth;
            role.sizeDelta = new Vector2(roleWidth, role.sizeDelta.y);
            role.anchoredPosition = new Vector2(-total / 2f + roleWidth / 2f, role.anchoredPosition.y);
            tag.sizeDelta = new Vector2(tagWidth, tag.sizeDelta.y);
            tag.anchoredPosition = new Vector2(total / 2f - tagWidth / 2f, tag.anchoredPosition.y);
        }

        /// <summary>
        /// The mood face on the portrait's upper-left shoulder, drawn in the mood's own colour.
        ///
        /// <para>The role mark owns the upper-right, so the two never overlap however a week goes.
        /// A clone that has not generated the glyph set gets a plain coloured pip instead, which is
        /// the same information at lower fidelity rather than an empty square.</para>
        /// </summary>
        private static void MoodFace(RectTransform rim, string mood, Color colour, float diameter)
        {
            if (rim == null) return;
            float size = Mathf.Max(13f, diameter * 0.33f);
            MoodFace(rim, mood, colour, new Vector2(size * 0.28f, -size * 0.28f), size, true);
        }

        /// <summary>The mood face at a given spot in <paramref name="parent"/>.</summary>
        private static void MoodFace(RectTransform parent, string mood, Color colour, Vector2 at, float size,
            bool centred = false)
        {
            var badge = Disc(MoodGlyphName, parent, UiTheme.Ink);
            badge.anchorMin = new Vector2(0f, 1f);
            badge.anchorMax = new Vector2(0f, 1f);
            badge.pivot = centred ? new Vector2(.5f, .5f) : new Vector2(0f, 1f);
            badge.anchoredPosition = at;
            badge.sizeDelta = new Vector2(size, size);

            var glyph = UiTheme.Icon(RelationshipWeb.MoodIcon(mood)) ?? UiTheme.Icon("mood-neutral");
            if (glyph == null)
            {
                var pip = Disc("Face", badge, colour);
                pip.anchorMin = new Vector2(.5f, .5f); pip.anchorMax = new Vector2(.5f, .5f);
                pip.pivot = new Vector2(.5f, .5f);
                pip.anchoredPosition = Vector2.zero;
                pip.sizeDelta = new Vector2(size * .55f, size * .55f);
                return;
            }

            var art = new GameObject("Face", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            art.SetParent(badge, false);
            art.anchorMin = new Vector2(.5f, .5f); art.anchorMax = new Vector2(.5f, .5f);
            art.pivot = new Vector2(.5f, .5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(size * .82f, size * .82f);
            var image = art.GetComponent<Image>();
            image.sprite = glyph;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static RectTransform Disc(string name, Transform parent, Color colour)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = rect.GetComponent<Image>();
            image.sprite = UiTheme.Circle();
            image.type = Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text Label(
            Transform parent, string value, int size, Color colour, float scale, TMP_FontAsset font,
            TextAlignmentOptions alignment)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            if (font != null) text.font = font;
            text.fontSize = Mathf.RoundToInt(size * scale);
            text.color = colour;
            text.text = value;
            text.richText = false;
            text.raycastTarget = false;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }
    }
}
