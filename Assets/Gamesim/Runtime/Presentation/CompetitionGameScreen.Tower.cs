using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The Ring Tower's board (competition rules 5): the yard's three stacks are the game, so the board is
    /// glass the yard shows through, with the three stacks drawn small along its foot - the same rings, the
    /// same colours - as the controls. Pick a stack to lift its top ring, then the stack to put it on: click
    /// a stack, or walk them with the arrows or the D-pad and press Enter or A, as every board's controls are
    /// walked. The move count and the fewest it can be done in stand in the board's band; the clock is the
    /// frame's own.
    ///
    /// <para>Each stack is a control named and captioned "Stack 1" to "Stack 3" - captions are a contract,
    /// how a test, the season walk and a screen reader find them - with its rings read out under it ("Rings
    /// 4, 3, 2, 1", "Empty"). A refused pick says why in the band and marks the stack it named for a moment;
    /// under reduced motion the mark is a colour, not a movement.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        /// <summary>A stack control's caption. Captions are a contract.</summary>
        public static string StackCaption(int stack) => "Stack " + (stack + 1);

        /// <summary>How long a refused pick marks its stack, in seconds.</summary>
        private const float RefusalMarkSeconds = .7f;

        /// <summary>The tower board's glass: lighter than the endurance board's, since the stacks behind it are the game.</summary>
        private const float TowerBoardAlpha = .22f;

        private readonly Button[] towerStacks = new Button[RingTowerPuzzle.Stacks];
        private readonly Image[] towerFaces = new Image[RingTowerPuzzle.Stacks];
        private readonly Image[] towerPegs = new Image[RingTowerPuzzle.Stacks];
        private readonly TMP_Text[] towerContents = new TMP_Text[RingTowerPuzzle.Stacks];
        private Image[] towerRings;
        private TMP_Text towerChip;
        private float towerRefusalLeft;
        private int towerRefusedStack = -1;

        private bool TowerBoard => run != null && run.Kind == CompetitionMiniGames.Kind.Tower;

        private void BuildTower()
        {
            var ground = playArea.GetComponent<Image>();
            ground.color = new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, TowerBoardAlpha);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            for (int i = 0; i < RingTowerPuzzle.Stacks; i++)
            {
                int stack = i;
                var root = HudPrimitives.Fill(StackCaption(i), playArea, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .55f), 14);
                var face = root.GetComponent<Image>(); face.raycastTarget = true;
                UiTheme.AddBorder(root, 14, UiTheme.Outline);
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                Untinted(button);
                button.onClick.AddListener(() => PickTowerStack(stack));
                var peg = Picture("Peg", root, null, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .55f), false);
                var caption = HudPrimitives.Label("Label", root, 18f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
                caption.text = StackCaption(i); caption.textWrappingMode = TextWrappingModes.NoWrap;
                if (semibold != null) caption.font = semibold;
                Fit(caption, 11);
                var contents = HudPrimitives.Label("Stack contents", root, 14f * FontScale, UiTheme.Muted, TextAlignmentOptions.Center);
                contents.textWrappingMode = TextWrappingModes.NoWrap; Fit(contents, 10);
                var ring = new GameObject("Focus ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                ring.rectTransform.SetParent(root, false); Stretch(ring.rectTransform, -5f, -5f, -5f, -5f);
                ring.raycastTarget = false;
                if (!UiTheme.PackSliced(ring, PackArt.KitCardEdgeFocus, 14f, UiTheme.Paper)) UiTheme.Style(ring, new Color(1f, 1f, 1f, .9f), 14);
                ring.gameObject.SetActive(false);
                root.gameObject.AddComponent<CompetitionCardFocus>().Ring = ring.gameObject;
                root.gameObject.AddComponent<HudPress>().ReducedMotion = ReducedMotion;
                towerStacks[i] = button; towerFaces[i] = face; towerPegs[i] = peg; towerContents[i] = contents;
            }
            int rings = run.Tower != null ? run.Tower.Rings : RingTowerPuzzle.StandardRings;
            towerRings = new Image[rings];
            for (int size = 1; size <= rings; size++)
            {
                var bar = Picture("Ring " + size, playArea, null, CompetitionRingTower.RingColour(size, rings), false);
                UiTheme.Style(bar, CompetitionRingTower.RingColour(size, rings), 6);
                bar.raycastTarget = false;
                towerRings[size - 1] = bar;
            }
            for (int i = 0; i < RingTowerPuzzle.Stacks; i++)
                towerStacks[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = towerStacks[(i + RingTowerPuzzle.Stacks - 1) % RingTowerPuzzle.Stacks],
                    selectOnRight = towerStacks[(i + 1) % RingTowerPuzzle.Stacks], selectOnUp = cancel, selectOnDown = pause };
            towerChip = HudPrimitives.Label("Tower moves", boardChips, 15f * FontScale, UiTheme.Positive, TextAlignmentOptions.MidlineLeft);
            towerChip.textWrappingMode = TextWrappingModes.NoWrap; Fit(towerChip, 11);
            towerRefusalLeft = 0f; towerRefusedStack = -1;
        }

        /// <summary>Where a stack's column stands on the board, in the board's units: along the foot of the field, the yard above it clear.</summary>
        private Rect TowerColumn(int stack)
        {
            var field = PlayField;
            float gap = 18f;
            float width = Mathf.Clamp((field.width - 48f - 2f * gap) / 3f, 120f, 300f);
            float height = Mathf.Clamp(field.height * .46f, 150f, 300f);
            float row = 3f * width + 2f * gap, left = (field.width - row) * .5f;
            float top = field.y + field.height - height - 14f;
            return new Rect(left + stack * (width + gap), top, width, height);
        }

        private void PlaceTower()
        {
            for (int i = 0; i < RingTowerPuzzle.Stacks; i++)
            {
                var column = TowerColumn(i);
                Place((RectTransform)towerStacks[i].transform, column.x, column.y, column.width, column.height);
                var caption = towerStacks[i].GetComponentInChildren<TMP_Text>();
                Place(caption.rectTransform, 8f, column.height - 30f * FontScale - 22f * FontScale, column.width - 16f, 26f * FontScale);
                Place(towerContents[i].rectTransform, 8f, column.height - 24f * FontScale, column.width - 16f, 20f * FontScale);
            }
            Place(towerChip.rectTransform, 0f, 0f, surfaceWidth * .5f, BandHeight - 1f);
            RefreshTower();
        }

        /// <summary>The rings where the attempt has them: each on its stack's column, the lifted one raised over its column.</summary>
        private void RefreshTower()
        {
            var tower = run.Tower;
            if (tower == null || towerRings == null) return;
            for (int i = 0; i < RingTowerPuzzle.Stacks; i++)
            {
                var column = TowerColumn(i);
                float footY = column.height - 30f * FontScale - 26f * FontScale - 6f;
                float ringHeight = Mathf.Clamp((footY - 34f) / (tower.Rings + 1.5f), 10f, 26f);
                float widest = column.width - 28f;
                var pegRect = towerPegs[i].rectTransform;
                Place(pegRect, column.width * .5f - 3f, footY - ringHeight * (tower.Rings + .8f), 6f, ringHeight * (tower.Rings + .8f));
                var rings = tower.Stack(i);
                for (int level = 0; level < rings.Count; level++)
                {
                    int size = rings[level];
                    bool lifted = tower.Lifted == i && level == rings.Count - 1;
                    float w = widest * (.35f + .65f * size / tower.Rings);
                    float y = lifted ? 4f : footY - (level + 1) * (ringHeight + 2f);
                    var bar = towerRings[size - 1].rectTransform;
                    if (bar.parent != towerStacks[i].transform) bar.SetParent(towerStacks[i].transform, false);
                    Place(bar, (column.width - w) * .5f, y, w, ringHeight);
                    towerRings[size - 1].color = lifted ? Color.Lerp(CompetitionRingTower.RingColour(size, tower.Rings), Color.white, .45f)
                        : CompetitionRingTower.RingColour(size, tower.Rings);
                    bar.SetAsLastSibling();
                }
                towerContents[i].text = rings.Count == 0 ? "Empty" : "Rings " + string.Join(", ", rings);
                bool marked = towerRefusalLeft > 0f && towerRefusedStack == i;
                towerFaces[i].color = marked ? new Color(UiTheme.Danger.r, UiTheme.Danger.g, UiTheme.Danger.b, .55f)
                    : tower.Lifted == i ? new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .55f)
                    : new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .55f);
                towerStacks[i].interactable = !run.Finished;
            }
            towerChip.text = "Moves " + tower.Moves + "  ·  fewest " + tower.Minimum;
            status.text = tower.Lifted >= 0 ? "Ring lifted from stack " + (tower.Lifted + 1) : "Moves " + tower.Moves + " of " + tower.Minimum + " at best";
            if (!string.IsNullOrEmpty(run.Feedback)) SetFeedback(run.Feedback);
        }

        /// <summary>A pick from the board: the stack's top ring lifted, or the lifted ring put down. A refusal says why and sounds it.</summary>
        private void PickTowerStack(int stack)
        {
            if (!IsPlaying || run == null || run.Tower == null) return;
            int moves = run.Tower.Moves;
            var refusal = run.PickStack(stack);
            if (refusal == RingTowerPuzzle.Refusal.None)
                Cue(run.Tower.Solved ? HouseAudio.Cue.SocialUp : run.Tower.Moves > moves ? HouseAudio.Cue.Button : HouseAudio.Cue.Hover);
            else if (refusal != RingTowerPuzzle.Refusal.Finished)
            {
                Cue(HouseAudio.Cue.SocialDown);
                towerRefusedStack = stack; towerRefusalLeft = RefusalMarkSeconds;
            }
            Refresh();
        }

        /// <summary>The refusal mark's time: a colour held for a moment, then cleared, on the screen's own clock.</summary>
        private void AdvanceTowerBeats(float delta)
        {
            if (towerRefusalLeft <= 0f) return;
            towerRefusalLeft -= Mathf.Max(0f, delta);
            if (towerRefusalLeft <= 0f) { towerRefusalLeft = 0f; towerRefusedStack = -1; if (run != null) RefreshTower(); }
        }

        /// <summary>The stack the keyboard and the pad start on: the lifted ring's stack, or the first with a ring.</summary>
        private Selectable TowerFocus
        {
            get
            {
                var tower = run?.Tower;
                if (tower == null) return towerStacks[0];
                if (tower.Lifted >= 0) return towerStacks[tower.Lifted];
                for (int i = 0; i < RingTowerPuzzle.Stacks; i++) if (tower.Stack(i).Count > 0) return towerStacks[i];
                return towerStacks[0];
            }
        }
    }
}
