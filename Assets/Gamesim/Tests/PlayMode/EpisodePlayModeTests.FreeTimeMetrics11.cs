using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    // Test-only observation. No allocator, accepted-spec substitution, command or PNG publisher.
    public sealed partial class EpisodePlayModeTests
    {
        [Serializable]
        private sealed class FreeTimeMetric11
        {
            public int schema = 3;
            public string id, status;
            public float scale, canvasWidth, canvasHeight, contentHeight, viewportHeight, boardWidth, boardHeight;
            public float paddingTop, paddingBottom, spacing, postRenderRoomWithReserve2;
            public float heroFloor, cardFloor, photoFloor, moveHeightFloor, moveWidthFloor;
            public float heroHeight, budgetHeight, cardsHeight, movesHeight, budgetCopyFloorPreferredHeight;
            public int readablePerPage, visibleCards, moveRows, labelsObserved, partsObserved, controlsObserved;
            /// <summary>The board's page this receipt observed, of how many, and the sections on it (H, S, T, M).</summary>
            public int page, pages;
            public string sections;
            public string verticalFit, horizontalFit;
            public bool controlChildWidth, controlChildHeight, forceExpandWidth, forceExpandHeight;
            public string[] failures;
            public FreeTimeLabelMetric11[] labels;
            public FreeTimePartMetric11[] parts;
            public FreeTimeControlMetric11[] controls;
            public FreeTimeRequiredSurface11[] requiredSurfaces;
        }

        [Serializable]
        private sealed class FreeTimeLabelMetric11
        {
            public string path, scope, copySha256, font, style, weight, wrapping, overflow;
            public bool active, enabled, autosize, fullGlyphs, truncated, overflowing;
            public bool extraPadding, orthographic, parseControls;
            public string[] fontFeatures;
            public float actualSize, inheritedAlpha, policyMinimum, policyMaximum, requestedPreferredHeight, minimumPreferredHeight;
            public float requestedPreferredWidth, minimumPreferredWidth, characterSpacing, wordSpacing, lineSpacing;
            public float paragraphSpacing, lineSpacingAdjustment, characterWidthAdjustment, wordWrappingRatios;
            public float x, y, width, height;
            public int sourceCharacters, visibleCharacters, maxVisibleCharacters, maxVisibleWords, maxVisibleLines;
            public Vector4 margin;
        }

        [Serializable]
        private sealed class FreeTimePartMetric11
        {
            public string path, role, imageType, sprite;
            public bool active, layoutElement;
            public bool meshAvailable, culled;
            public int vertices;
            public float x, y, width, height, minimumHeight, preferredHeight, colourAlpha;
        }

        [Serializable]
        private sealed class FreeTimeControlMetric11
        {
            public string path;
            public bool active, enabled, interactable;
            public float x, y, width, height;
        }

        private static bool FreeTimeFinite11(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void FreeTimeIssue11(List<string> failures, bool condition, string reason)
        {
            if (condition) return;
            if (failures.Count < 512) failures.Add(reason);
            else if (failures[511] != "FailureRecordCap") failures[511] = "FailureRecordCap";
        }

        private static string FreeTimePath11(Transform part, Transform root)
        {
            var names = new List<string>();
            for (int depth = 0; part != null && depth < 64; depth++, part = part.parent)
            {
                names.Add(part.name);
                if (part == root) return string.Join("/", names.AsEnumerable().Reverse());
            }
            throw new InvalidOperationException("FreeTimeMetric11 requires a bounded owned UI path.");
        }

        private static string FreeTimeCopySha11(string copy)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(copy)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static bool FreeTimeWithin11(Rect outer, Rect inner)
            => FreeTimeFinite11(inner.xMin) && FreeTimeFinite11(inner.yMin) && FreeTimeFinite11(inner.width) && FreeTimeFinite11(inner.height)
                && inner.width > 0f && inner.height > 0f
                && inner.xMin >= outer.xMin - .5f && inner.xMax <= outer.xMax + .5f
                && inner.yMin >= outer.yMin - .5f && inner.yMax <= outer.yMax + .5f;

        private static Rect FreeTimeImageQuad11(UnityEngine.UI.Image image)
        {
            Rect pixels = image.GetPixelAdjustedRect();
            var canvas = image.GetComponentInParent<Canvas>().rootCanvas.transform;
            Vector3 a = canvas.InverseTransformPoint(image.transform.TransformPoint(new Vector3(pixels.xMin, pixels.yMin, 0f)));
            Vector3 b = canvas.InverseTransformPoint(image.transform.TransformPoint(new Vector3(pixels.xMax, pixels.yMax, 0f)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Rect FreeTimeImageMesh11(UnityEngine.UI.Image image, List<string> failures, string path, out int count, out bool available)
        {
            var mesh = image.canvasRenderer.GetMesh(); // Borrowed current geometry; never mutate or destroy it.
            count = mesh != null ? mesh.vertexCount : 0;
            available = mesh != null && mesh.isReadable && count > 0 && count <= 4096;
            FreeTimeIssue11(failures, available, "UnresolvedImageMesh:" + path);
            if (!available) return default;
            var vertices = new List<Vector3>(count);
            mesh.GetVertices(vertices);
            available = vertices.Count == count;
            FreeTimeIssue11(failures, available, "ImageMeshCount:" + path);
            if (!available) return default;
            var canvas = image.GetComponentInParent<Canvas>().rootCanvas.transform;
            float left = float.PositiveInfinity, right = float.NegativeInfinity, bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (var vertex in vertices)
            {
                var point = canvas.InverseTransformPoint(image.transform.TransformPoint(vertex));
                if (!FreeTimeFinite11(point.x) || !FreeTimeFinite11(point.y))
                {
                    available = false;
                    FreeTimeIssue11(failures, false, "NonfiniteImageMesh:" + path);
                    return default;
                }
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        private static Vector2 FreeTimePreferred11(TMP_Text actual, float size)
        {
            var holder = new GameObject("FreeTime metric11 TMP probe", typeof(RectTransform), typeof(TextMeshProUGUI));
            holder.hideFlags = HideFlags.DontSave;
            try
            {
                var probe = holder.GetComponent<TextMeshProUGUI>();
                probe.font = actual.font;
                probe.fontSharedMaterial = actual.fontSharedMaterial;
                probe.fontStyle = actual.fontStyle;
                probe.fontWeight = actual.fontWeight;
                probe.enableAutoSizing = false;
                probe.fontSize = size;
                probe.characterSpacing = actual.characterSpacing;
                probe.wordSpacing = actual.wordSpacing;
                probe.lineSpacing = actual.lineSpacing;
                probe.paragraphSpacing = actual.paragraphSpacing;
                probe.lineSpacingAdjustment = actual.lineSpacingAdjustment;
                probe.characterWidthAdjustment = actual.characterWidthAdjustment;
                probe.wordWrappingRatios = actual.wordWrappingRatios;
                Assert.That(actual.fontFeatures, Is.Not.Null, "Actual font-feature policy must be captured, not replaced with a probe default.");
                probe.fontFeatures = actual.fontFeatures.ToList();
                probe.extraPadding = actual.extraPadding;
                probe.isOrthographic = actual.isOrthographic;
                probe.parseCtrlCharacters = actual.parseCtrlCharacters;
                probe.margin = actual.margin;
                probe.richText = actual.richText;
                probe.textWrappingMode = actual.textWrappingMode;
                probe.overflowMode = TextOverflowModes.Overflow;
                float width = Mathf.Max(0f, actual.rectTransform.rect.width);
                return probe.GetPreferredValues(actual.text ?? string.Empty, width, 0f);
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        private FreeTimeLabelMetric11 FreeTimeObserveLabel11(TMP_Text label, RectTransform board, RectTransform viewport,
            RectTransform panel, List<string> failures)
        {
            bool onBoard = label.transform.IsChildOf(board);
            var directParent = label.transform.parent as RectTransform;
            bool inFooterStrip = !onBoard && directParent != null && directParent.name == EpisodeHud.StrategyStripName;
            string path = FreeTimePath11(label.transform, onBoard ? board : director.transform);
            var box = CanvasRect(label.rectTransform);
            string actualCopy = label.text ?? string.Empty;
            FreeTimeIssue11(failures, actualCopy.Length > 0, "EmptyActualCopy:" + path);
            float inheritedAlpha = label.canvasRenderer.GetInheritedAlpha();
            bool active = label.gameObject.activeInHierarchy && label.enabled && label.color.a > 0f && !label.canvasRenderer.cull
                && FreeTimeFinite11(inheritedAlpha) && inheritedAlpha > 0f;
            FreeTimeIssue11(failures, active, "HiddenCopy:" + path);
            FreeTimeIssue11(failures, label.font != null, "MissingFont:" + path);
            // These fixed fixture copies are plain text. Unknown markup is unresolved, not silently stripped.
            bool plain = !label.richText || (actualCopy.IndexOf('<') < 0 && actualCopy.IndexOf('>') < 0);
            FreeTimeIssue11(failures, plain, "UnresolvedRichCopy:" + path);
            label.ForceMeshUpdate(true);
            var visible = label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(character => character.isVisible).ToArray();
            var expected = actualCopy.Where(character => !char.IsWhiteSpace(character)).ToArray();
            bool complete = expected.Length > 0 && plain && visible.Select(character => character.character).SequenceEqual(expected)
                && !label.isTextOverflowing && !label.isTextTruncated;
            FreeTimeIssue11(failures, complete, "IncompleteCopy:" + path);
            FreeTimeIssue11(failures, FreeTimeWithin11(CanvasRect(panel), box)
                && (!onBoard || (FreeTimeWithin11(CanvasRect(board), box) && FreeTimeWithin11(CanvasRect(viewport), box)))
                && (!inFooterStrip || FreeTimeWithin11(CanvasRect(directParent), box)), "CopyAllocationOutside:" + path);
            // An italic glyph's quad is sheared - its foot leans left of the pen and its head right of
            // the advance, by the font's italic style - so for italic copy the glyph is held to the box
            // its line was wrapped into: from its pen position to its advance, ascender to descender.
            bool italic = (label.fontStyle & FontStyles.Italic) != 0;
            foreach (var glyph in visible)
            {
                Rect drawn = italic ? FreeTimeGlyphAdvance11(label, glyph) : GlyphRect(label, glyph);
                var crossed = new List<string>();
                if (!FreeTimeWithin11(box, drawn)) crossed.Add("box");
                if (!FreeTimeWithin11(CanvasRect(panel), drawn)) crossed.Add("panel");
                if (onBoard && !FreeTimeWithin11(CanvasRect(board), drawn)) crossed.Add("board");
                if (onBoard && !FreeTimeWithin11(CanvasRect(viewport), drawn)) crossed.Add("viewport");
                if (inFooterStrip && !FreeTimeWithin11(CanvasRect(directParent), drawn)) crossed.Add("strip");
                FreeTimeIssue11(failures, glyph.textElement != null && crossed.Count == 0, "GlyphOutside:" + path + "[" + string.Join(",", crossed)
                    + " '" + glyph.character + "' " + drawn.xMin.ToString("0.0", CultureInfo.InvariantCulture) + ".." + drawn.xMax.ToString("0.0", CultureInfo.InvariantCulture)
                    + " in " + box.xMin.ToString("0.0", CultureInfo.InvariantCulture) + ".." + box.xMax.ToString("0.0", CultureInfo.InvariantCulture) + "]");
            }
            float minimum = label.enableAutoSizing ? label.fontSizeMin : label.fontSize;
            float requested = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
            FreeTimeIssue11(failures, FreeTimeFinite11(minimum) && minimum > 0f && FreeTimeFinite11(requested)
                && requested >= minimum && label.fontSize >= minimum - .01f && label.fontSize <= requested + .01f, "TypePolicy:" + path);
            Vector2 want = FreeTimePreferred11(label, requested), least = FreeTimePreferred11(label, minimum);
            FreeTimeIssue11(failures, FreeTimeFinite11(want.x) && FreeTimeFinite11(want.y)
                && FreeTimeFinite11(least.x) && FreeTimeFinite11(least.y), "NonfiniteTMP:" + path);
            return new FreeTimeLabelMetric11
            {
                path = path, scope = onBoard ? "Board" : inFooterStrip ? "PinnedStrip" : "PinnedFooter", copySha256 = FreeTimeCopySha11(actualCopy), font = label.font != null ? label.font.name : "MISSING",
                style = label.fontStyle.ToString(), weight = label.fontWeight.ToString(), wrapping = label.textWrappingMode.ToString(),
                overflow = label.overflowMode.ToString(), active = active, enabled = label.enabled, autosize = label.enableAutoSizing,
                fullGlyphs = complete, truncated = label.isTextTruncated, overflowing = label.isTextOverflowing,
                inheritedAlpha = inheritedAlpha, actualSize = label.fontSize, policyMinimum = minimum, policyMaximum = requested,
                requestedPreferredHeight = want.y, requestedPreferredWidth = want.x,
                minimumPreferredHeight = least.y, minimumPreferredWidth = least.x,
                characterSpacing = label.characterSpacing, wordSpacing = label.wordSpacing, lineSpacing = label.lineSpacing, margin = label.margin,
                paragraphSpacing = label.paragraphSpacing, lineSpacingAdjustment = label.lineSpacingAdjustment,
                characterWidthAdjustment = label.characterWidthAdjustment, wordWrappingRatios = label.wordWrappingRatios,
                fontFeatures = label.fontFeatures != null ? label.fontFeatures.Select(feature => feature.ToString()).ToArray() : new string[0],
                extraPadding = label.extraPadding, orthographic = label.isOrthographic, parseControls = label.parseCtrlCharacters,
                x = box.x, y = box.y, width = box.width, height = box.height,
                sourceCharacters = expected.Length, visibleCharacters = visible.Length,
                maxVisibleCharacters = label.maxVisibleCharacters, maxVisibleWords = label.maxVisibleWords, maxVisibleLines = label.maxVisibleLines,
            };
        }

        /// <summary>A glyph's line box in the canvas's space: from its pen position to its advance, its line's descender to its ascender.</summary>
        private static Rect FreeTimeGlyphAdvance11(TMP_Text label, TMP_CharacterInfo glyph)
        {
            var canvas = label.GetComponentInParent<Canvas>().rootCanvas.transform;
            Vector3 a = canvas.InverseTransformPoint(label.transform.TransformPoint(new Vector3(glyph.origin, glyph.descender, 0f)));
            Vector3 b = canvas.InverseTransformPoint(label.transform.TransformPoint(new Vector3(glyph.xAdvance, glyph.ascender, 0f)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static RectTransform FreeTimeRequiredPart11(RectTransform board, string name, List<string> failures)
        {
            var found = board.GetComponentsInChildren<RectTransform>(true).Where(part => part.name == name).ToArray();
            FreeTimeIssue11(failures, found.Length == 1 && found[0].gameObject.activeInHierarchy, "RequiredPart:" + name);
            return found.Length == 1 ? found[0] : null;
        }

        private static TMP_Text FreeTimeRequiredCopy11(RectTransform scope, string name, string expected, List<string> failures)
        {
            var found = scope.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == name).ToArray();
            if (string.IsNullOrEmpty(expected))
            {
                FreeTimeIssue11(failures, found.Length == 0, "UnexpectedOptionalCopy:" + name);
                return null;
            }
            FreeTimeIssue11(failures, found.Length == 1 && found[0].text == expected && found[0].enabled
                && found[0].gameObject.activeInHierarchy, "RequiredCopy:" + name);
            return found.Length == 1 ? found[0] : null;
        }

        private static void FreeTimeRequiredFrame11(RectTransform host, List<string> failures)
        {
            if (host == null) return; // Its required-part check already refused the missing host.
            var frames = host.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(image => image.transform.parent == host
                && (image.name == "Art" || image.name == EndScreenKit.DrawnFrameName)).ToArray();
            bool framed = frames.Length == 1 && frames[0].enabled && frames[0].gameObject.activeInHierarchy && frames[0].color.a > 0f;
            if (frames.Length == 0)
            {
                // The HUD's glass chrome: the host's own image is its face, and its edge a child "Border".
                var glass = host.GetComponent<UnityEngine.UI.Image>();
                var edges = host.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(image => image.transform.parent == host && image.name == "Border").ToArray();
                framed = glass != null && glass.enabled && glass.color.a > 0f && edges.Length == 1 && edges[0].enabled
                    && edges[0].gameObject.activeInHierarchy && edges[0].color.a > 0f;
            }
            FreeTimeIssue11(failures, framed, "RequiredActualFrame:" + host.name);
        }

        private void FreeTimeRequiredControl11(string caption, List<string> failures)
        {
            bool footer = caption == EpisodeDirector.BeginNextCompetitionCaption || caption == EpisodeDirector.StayInTheHouseCaption;
            var board = ActiveRect(EpisodeHud.FreeTimeBoardName);
            var source = footer ? director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(button => button.gameObject.activeInHierarchy)
                : board.GetComponentsInChildren<UnityEngine.UI.Button>(true).AsEnumerable();
            var found = source.Where(button => button.name == caption).ToArray();
            FreeTimeIssue11(failures, found.Length == 1 && found[0].enabled && found[0].gameObject.activeInHierarchy
                && found[0].GetComponentsInChildren<TMP_Text>(true).Count(label => label.text == caption
                    && label.enabled && label.gameObject.activeInHierarchy) == 1, "RequiredControl:" + caption);
        }


        [Serializable]
        private sealed class FreeTimeRequiredSurface11
        {
            public string role, kind, status, expectedCopySha256;
            public int expected, found;
            public bool required = true;
        }

        private sealed class FreeTimeInventory11
        {
            public readonly List<FreeTimeRequiredSurface11> Required = new List<FreeTimeRequiredSurface11>();
            public readonly List<TMP_Text> Labels = new List<TMP_Text>();
        }

        /// <summary>
        /// Which of the board's four sections the page on screen shows. One page shows all four; at
        /// the largest text a board that cannot fit is dealt onto pages (EpisodeHud.LargestPages.cs),
        /// and a section is required on its page and absent from the others - the walk over the pages
        /// holds that each is on exactly one.
        /// </summary>
        private struct FreeTimeShown11
        {
            public bool Hero, Strip, Talk, Moves;
            public override string ToString() => (Hero ? "H" : "") + (Strip ? "S" : "") + (Talk ? "T" : "") + (Moves ? "M" : "");
        }

        /// <summary>The sections on the page now on screen, by their roots: the hero row, the story strip, the cards' head, the moves' head.</summary>
        private static FreeTimeShown11 FreeTimeShownNow11(RectTransform board)
        {
            bool Has(string name) => board.GetComponentsInChildren<RectTransform>().Any(part => part.name == name && part.parent == board);
            return new FreeTimeShown11 { Hero = Has(EpisodeHud.FreeTimeHeroName), Strip = Has(EpisodeHud.StoryStripName),
                Talk = Has(EpisodeHud.TalkHeadName), Moves = Has(EpisodeHud.MovesHeadName) };
        }

        /// <summary>A board part required on this page, or required absent from it when its section is on another.</summary>
        private static RectTransform FreeTimePagePart11(RectTransform board, string name, bool shown, List<string> failures)
        {
            if (shown) return FreeTimeRequiredPart11(board, name, failures);
            FreeTimeIssue11(failures, !board.GetComponentsInChildren<RectTransform>(true).Any(part => part.name == name && part.gameObject.activeInHierarchy),
                "PartOffItsPage:" + name);
            return null;
        }

        // Source-backed required denominator: it is complete before any active/nonempty label filter.
        // The cards' pages are the suite's literal for the frame (cardPages: how many on each), never
        // the renderer's own arithmetic; the board's page and pages likewise.
        private FreeTimeInventory11 FreeTimeRequiredInventory11(RectTransform board, RectTransform panel, EpisodeState state,
            IList<HouseMap.Room> capturedRooms, string[] everybody, int page, FreeTimeWaiting waiting, List<string> failures,
            int[] cardPages, FreeTimeShown11 shown, int boardPage = 0, int boardPages = 1)
        {
            var inventory = new FreeTimeInventory11();
            float scale = TextSizes.Scale(director.TextSize);
            void Row(string role, string kind, int expected, int found, bool matched, string copy = null, bool requiredEntry = true)
            {
                FreeTimeIssue11(failures, matched, "RequiredSurface:" + role);
                if (inventory.Required.Count < 256)
                    inventory.Required.Add(new FreeTimeRequiredSurface11 { role = role, kind = kind, expected = expected, found = found,
                        status = matched ? (!requiredEntry ? "KNOWN_NONSELECTED_CACHE" : expected == 0 ? "ABSENT_AS_REQUIRED" : "MATCHED") : "NO_FIT", required = requiredEntry,
                        expectedCopySha256 = copy != null ? FreeTimeCopySha11(copy) : null });
                else FreeTimeIssue11(failures, false, "RequiredSurfaceCap");
            }
            RectTransform Part(RectTransform owner, string name, string role, bool required = true)
            {
                var found = owner != null ? owner.GetComponentsInChildren<RectTransform>(true).Where(part => part.name == name).ToArray()
                    : Array.Empty<RectTransform>();
                bool matched = required ? found.Length == 1 && found[0].parent == owner && found[0].gameObject.activeInHierarchy
                    : found.Length == 0;
                Row(role, "RectTransform", required ? 1 : 0, found.Length, matched);
                return found.Length == 1 ? found[0] : null;
            }
            TMP_Text Copy(RectTransform owner, string name, string english, string role, bool required = true, bool localised = true)
            {
                string expected = localised ? Localisation.Text(english) : english;
                var found = owner != null ? owner.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == name).ToArray()
                    : Array.Empty<TMP_Text>();
                bool matched = required ? !string.IsNullOrEmpty(expected) && found.Length == 1 && found[0].transform.parent == owner
                    && found[0].text == expected && found[0].enabled && found[0].gameObject.activeInHierarchy && found[0].color.a > 0f
                    && !found[0].canvasRenderer.cull && FreeTimeFinite11(found[0].canvasRenderer.GetInheritedAlpha())
                    && found[0].canvasRenderer.GetInheritedAlpha() > 0f : found.Length == 0;
                Row(role, "TMP", required ? 1 : 0, found.Length, matched, expected);
                if (required) inventory.Labels.AddRange(found);
                return found.Length == 1 ? found[0] : null;
            }
            RectTransform Control(RectTransform owner, string caption, string role, bool required = true)
            {
                var found = owner != null ? owner.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(button => button.name == caption).ToArray()
                    : Array.Empty<UnityEngine.UI.Button>();
                bool matched = required ? found.Length == 1 && found[0].transform.parent == owner && found[0].enabled
                    && found[0].gameObject.activeInHierarchy : found.Length == 0;
                Row(role, "Button", required ? 1 : 0, found.Length, matched);
                var result = found.Length == 1 ? (RectTransform)found[0].transform : null;
                Copy(result, "Text", caption, role + ".caption", required);
                return result;
            }

            FreeTimeIssue11(failures, state.phase == EpisodePhase.Social && state.Active.Count() == 8, "RequiredEightSocialFixture");
            Row("current.panel", "RectTransform", 1, panel != null ? 1 : 0, panel != null && panel.name == "Episode panel" && panel.gameObject.activeInHierarchy);
            var scroll = Part(panel, "Episode scroll", "current.scroll");
            var viewport = Part(scroll, "Viewport", "current.viewport");
            var content = Part(viewport, "Episode content", "current.content");
            var currentBoard = Part(content, EpisodeHud.FreeTimeBoardName, "current.board");
            FreeTimeIssue11(failures, board == currentBoard && board.IsChildOf(panel), "RequiredCurrentPanelOwnership");
            var hero = Part(board, EpisodeHud.FreeTimeHeroName, "hero", shown.Hero);
            var strip = Part(board, EpisodeHud.StoryStripName, "storyStrip", shown.Strip);
            var cards = Part(board, EpisodeHud.HouseCardsName, "houseCards", shown.Talk);
            var moves = Part(board, EpisodeHud.HouseMovesName, "houseMoves", shown.Moves);
            var budget = Part(board, EpisodeHud.ScreenHeadName, "budget", shown.Hero);
            var talkHead = Part(board, EpisodeHud.TalkHeadName, "talkHead", shown.Talk);
            var movesHead = Part(board, EpisodeHud.MovesHeadName, "movesHead", shown.Moves);
            Copy(talkHead, "Head title", "TALK TO A HOUSEGUEST", "talkHead.title", shown.Talk);
            Copy(movesHead, "Head title", "OTHER WAYS TO SPEND YOUR TIME", "movesHead.title", shown.Moves);
            Copy(talkHead, "Head line", "Choose someone to approach, or select an action below.", "talkHead.subtitle", shown.Talk && scale <= 1.05f);
            Copy(movesHead, "Head line", "Explore the house, make a move, or find something to do.", "movesHead.subtitle", shown.Moves && scale <= 1.05f);
            int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));
            Copy(budget, "Budget eyebrow", "FREE TIME", "budget.eyebrow", shown.Hero);
            Copy(budget, EpisodeHud.ActionsLeftCountName, left.ToString(), "budget.count", shown.Hero);
            Copy(budget, EpisodeHud.ActionsLeftWordName, left == 1 ? "ACTION LEFT" : "ACTIONS LEFT", "budget.word", shown.Hero);
            Copy(budget, EpisodeHud.BudgetRuleName, EpisodeDirector.BudgetRule(state), "budget.rule", shown.Hero);
            Copy(budget, EpisodeHud.BudgetCopyName, EpisodeDirector.FreeTimeCostLine(state), "budget.cost", shown.Hero);
            string unused = EpisodeDirector.UnusedActionsNote(state);
            Copy(budget, EpisodeHud.UnusedActionsNoteName, unused, "budget.unused", shown.Hero && !string.IsNullOrEmpty(unused));
            bool canBuy = WebSocialVocabulary.PurchaseCeiling - state.boughtActionPoints > 0;
            var buys = Part(budget, EpisodeHud.BuyActionsName, "budget.buys", shown.Hero && canBuy);
            if (canBuy && shown.Hero)
            {
                var burn = Control(buys, EpisodeHud.BuyBurnOneCaption, "buy.burn");
                Copy(burn, "Buy price", Mathf.Abs((int)WebSocialVocabulary.BurnOneCost) + " goodwill · choose a housemate", "buy.burn.price");
                var spread = Control(buys, EpisodeHud.BuySpreadCaption, "buy.spread");
                Copy(spread, "Buy price", Mathf.Abs((int)WebSocialVocabulary.SpreadAllCost) + " goodwill with every housemate", "buy.spread.price");
                Copy(budget, EpisodeHud.BoughtOutName, null, "budget.boughtOut", false);
            }
            else if (shown.Hero) Copy(budget, EpisodeHud.BoughtOutName, "You have bought as much time as the house will give you this "
                + (EpisodeEngine.LeverRulesOn(state) ? "week" : "season") + ".", "budget.boughtOut");

            var open = EpisodeEngine.OpenStoryBeats(state);
            var reply = ReplyCards.Pending(state);
            bool hasReply = reply != null, hasBeat = !hasReply && open.Count > 0;
            FreeTimeIssue11(failures, waiting == FreeTimeWaiting.Reply ? hasReply && reply.kind == ReplyCards.Confrontation && open.Count == 0
                : waiting == FreeTimeWaiting.Beat ? !hasReply && open.Count == 2 : !hasReply && open.Count == 0, "RequiredWaitingFixture");
            var plays = EpisodeEngine.Plays(state).Where(play => play.ending == null).ToList();
            var running = plays.FirstOrDefault(play => play.takenOn) ?? plays.FirstOrDefault();
            var playOwner = !hasReply && !hasBeat ? hero : strip;
            bool playShown = !hasReply && !hasBeat ? shown.Hero : shown.Strip;
            var playCard = Part(playOwner, EpisodeHud.CurrentPlayCardName, "currentPlay", playShown);
            if (!playShown) { }
            else if (!hasReply && !hasBeat)
            {
                var cycle = running != null ? state.storylines.FirstOrDefault(item => item.id == running.cycleId) : null;
                var subject = cycle?.cast != null ? cycle.cast.Select(role => state.Find(role.contestantId)).FirstOrDefault(actor => actor != null && !actor.isPlayer) : null;
                Copy(playCard, "Play eyebrow", "CURRENT PLAY" + (running != null ? " · " + PlayCurrencies.Label(running.currency).ToUpperInvariant() : ""), "play.eyebrow");
                Copy(playCard, "Play title", running == null ? "No play in motion." : running.title + (subject != null ? " — " + subject.name : ""), "play.title");
                bool bar = running != null && running.takenOn;
                Copy(playCard, "Play bar label", bar ? PlayCurrencies.Label(running.currency) : null, "play.barLabel", bar);
                Copy(playCard, "Progress count", bar ? running.progress.have + "/" + Mathf.Max(1, running.progress.need) : null, "play.progress", bar);
                string offer = running != null && !bar ? "On offer until " + StoryText.ClosesAt(running.deadline) + "." : null;
                Copy(playCard, "Play status", offer, "play.status", !string.IsNullOrEmpty(offer));
                string goal = running != null ? running.goal : "Plays are offered as the season turns; a Pull brings one to you.";
                Copy(playCard, "Play goal", goal, "play.goal", !string.IsNullOrEmpty(goal));
            }
            else
            {
                Copy(playCard, "Segment label", "PLAY", "play.segmentLabel");
                Copy(playCard, "Segment value", running == null ? "None in motion" : running.title
                    + (running.progress.need > 1 ? " " + running.progress.have + "/" + running.progress.need : running.progress.Met ? " done" : ""), "play.segmentValue");
            }
            var threads = Part(strip, EpisodeHud.ThreadsCardName, "threads", shown.Strip);
            Copy(threads, "Segment label", "THREADS", "threads.label", shown.Strip);
            Copy(threads, "Segment value", FreeTimeExpectedStoryLine11(state), "threads.value", shown.Strip);
            Copy(strip, EpisodeHud.PreparationName, state.playerStudyBonus > 0 ? "Preparation " + state.playerStudyBonus + "/5" : null,
                "preparation", shown.Strip && state.playerStudyBonus > 0);
            bool haveNot = HaveNots.Is(state, state.playerId);
            FreeTimeIssue11(failures, haveNot == (waiting == FreeTimeWaiting.HaveNot), "RequiredHaveNotFixture");
            // The Have-Not line stands under the strip, on the strip's page.
            Copy(board, EpisodeHud.FreeTimeHaveNotName, EpisodeDirector.HaveNotLine, "haveNot", haveNot && shown.Strip);

            var banner = Part(hero, EpisodeHud.StoryBannerName, "banner", hasBeat && shown.Hero);
            var plea = Part(hero, EpisodeHud.FreeTimeReplyName, "reply", hasReply && shown.Hero);
            if (hasBeat && shown.Hero)
            {
                var beat = open[0];
                bool free = !beat.choices.Any(choice => choice.costsAction);
                Copy(banner, "Banner eyebrow", (StoryText.Eyebrow(beat) ?? "Story").ToUpperInvariant() + " · WAITING ON YOU", "beat.eyebrow");
                Copy(banner, "Banner title", StoryText.Title(beat), "beat.title");
                Copy(banner, "Banner narrative", StoryText.Narrative(state, beat), "beat.narrative");
                Copy(banner, "Banner note", (free ? "Answering spends no action." : "Some answers spend an action.")
                    + "  ·  It won't wait past " + StoryText.ClosesAt(beat.closesAnchor) + ".", "beat.note");
                var chip = Part(banner, "Free chip", "beat.freeChip", free);
                Copy(chip, "Word", "Free", "beat.freeWord", free, false);
                Control(banner, EpisodeDirector.AnswerBeatCaption, "beat.answer");
            }
            var waitingBeats = open.Skip(hasBeat ? 1 : 0).ToArray();
            var waitingRow = Part(strip, EpisodeHud.WaitingBeatsName, "waitingBeats", shown.Strip && waitingBeats.Length > 0);
            Copy(waitingRow, "Segment label", "WAITING", "waitingBeats.label", shown.Strip && waitingBeats.Length > 0);
            foreach (var beat in waitingBeats) Control(waitingRow, StoryText.Title(beat), "waitingBeat." + beat.id, shown.Strip);
            if (hasReply && shown.Hero)
            {
                Copy(plea, "Plea eyebrow", EpisodeHud.ReplyCardEyebrow, "reply.eyebrow");
                Copy(plea, "Plea title", ReplyCards.Title(state, reply), "reply.title");
                Copy(plea, "Plea message", ReplyCards.Message(state, reply), "reply.message");
                var next = state.replyCards.Skip(1).FirstOrDefault();
                string count = "1 of " + state.replyCards.Count + (next != null && !string.IsNullOrEmpty(state.Find(next.fromId)?.name)
                    ? " · " + state.Find(next.fromId).name + " next" : "");
                Copy(plea, EpisodeHud.CampaignPleaCountName, count, "reply.count", state.replyCards.Count > 1);
                var grid = Part(plea, EpisodeHud.EventChoicesName, "reply.choices");
                foreach (var answer in ReplyCards.Replies(reply.kind))
                {
                    string caption = EpisodeHud.ReplyCaption(answer.Label), consequence = ReplyCardPayoffs.Description(state, reply, answer);
                    var tiles = grid != null ? grid.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(button => button.name == caption).ToArray()
                        : Array.Empty<UnityEngine.UI.Button>();
                    Row("reply." + answer.Key, "Button", 1, tiles.Length, tiles.Length == 1 && tiles[0].transform.parent == grid
                        && tiles[0].enabled && tiles[0].gameObject.activeInHierarchy);
                    var tile = tiles.Length == 1 ? (RectTransform)tiles[0].transform : null;
                    // Both default-named Text nodes are counted BEFORE copy; source creation order identifies their two roles.
                    var texts = tile != null ? tile.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Text")
                        .OrderBy(label => label.transform.GetSiblingIndex()).ToArray() : Array.Empty<TMP_Text>();
                    string[] expected = { Localisation.Text(caption), Localisation.Text(consequence) };
                    Row("reply." + answer.Key + ".textSlots", "TMPSet", 2, texts.Length, texts.Length == 2);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        var label = texts.Length == 2 ? texts[slot] : null;
                        bool matched = label != null && !string.IsNullOrEmpty(expected[slot]) && label.transform.parent == tile
                            && label.text == expected[slot] && label.enabled && label.gameObject.activeInHierarchy && label.color.a > 0f
                            && !label.canvasRenderer.cull && FreeTimeFinite11(label.canvasRenderer.GetInheritedAlpha()) && label.canvasRenderer.GetInheritedAlpha() > 0f;
                        Row("reply." + answer.Key + (slot == 0 ? ".caption" : ".consequence"), "TMP", 1, label != null ? 1 : 0, matched, expected[slot]);
                    }
                    inventory.Labels.AddRange(texts);
                    string risk = EpisodeHud.RiskTag(answer.Risk);
                    Copy(tile, "Risk word", risk, "reply." + answer.Key + ".risk", !string.IsNullOrEmpty(risk));
                }
            }

            string[] captions = { EpisodeDirector.ExploreTheHouseCaption, EpisodeDirector.OverviewListenCaption,
                EpisodeHud.RallyHouseCaption, EpisodeHud.AirLaundryCaption, EpisodeDirector.DoAnActivityCaption };
            string[] corners = { null, "Works 7 in 10", "Risky", EpisodeDirector.AiringRiskLabel(state), null };
            for (int i = 0; i < captions.Length && shown.Moves; i++)
            {
                var tile = Control(moves, captions[i], "move." + i);
                bool free = i == 0 || i == 4, locked = !free && left <= 0;
                Copy(tile, "Tile foot", free ? EpisodeDirector.TileFree : locked ? EpisodeDirector.TileNoActionsLeft : EpisodeDirector.TileCostsAction, "move." + i + ".cost");
                Copy(tile, "Tile corner", corners[i], "move." + i + ".corner", !locked && !string.IsNullOrEmpty(corners[i]));
                var button = tile != null ? tile.GetComponent<UnityEngine.UI.Button>() : null;
                FreeTimeIssue11(failures, button != null && button.interactable == !locked, "MoveInteractable:" + i);
            }
            if (!shown.Moves) foreach (string caption in captions) Control(board, caption, "move." + caption + ".offItsPage", false);

            // The cards' pages as the suite names them for this frame: how many on each, in order.
            int pages = cardPages != null ? cardPages.Length : 0;
            FreeTimeIssue11(failures, everybody.Length == 7 && everybody.Distinct(StringComparer.Ordinal).Count() == 7 && pages > 0
                && cardPages.Sum() == everybody.Length && cardPages.All(count => count > 0) && page >= 0 && page < pages, "RequiredGenuinePageSet");
            var shownCards = shown.Talk && page >= 0 && page < pages ? everybody.Skip(cardPages.Take(page).Sum()).Take(cardPages[page]).ToArray() : Array.Empty<string>();
            var actualCards = cards != null ? cards.Cast<Transform>().Select(card => card.name).ToArray() : Array.Empty<string>();
            FreeTimeIssue11(failures, actualCards.SequenceEqual(shownCards), "RequiredCardOrderAndMembership");
            var here = capturedRooms.FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            var hereIds = new HashSet<string>((here.Occupants ?? new List<HouseMap.Occupant>()).Where(person => !person.IsPlayer).Select(person => person.Id), StringComparer.Ordinal);
            foreach (string name in shownCards)
            {
                var actor = state.Active.SingleOrDefault(person => !person.isPlayer && "Houseguest · " + person.name == name);
                FreeTimeIssue11(failures, actor != null, "RequiredRealActor:" + name);
                var card = Part(cards, name, "card." + name);
                var photo = Part(card, "Photo", "photo." + name);
                FreeTimeIssue11(failures, card != null && card.rect.width >= 112f * scale - .5f && card.rect.height >= 129f * scale - .5f, "RequiredCardFloor:" + name);
                FreeTimeIssue11(failures, photo != null && photo.rect.width >= 48f * scale - .5f && photo.rect.height >= 48f * scale - .5f, "RequiredPhotoFloor:" + name);
                var faces = photo != null ? photo.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Where(image => image.name == "Face").ToArray()
                    : Array.Empty<UnityEngine.UI.RawImage>();
                Row("photo." + name + ".faceBinding", "RawImageBinding", 1, faces.Length, faces.Length == 1 && faces[0].transform.parent == photo
                    && faces[0].gameObject.activeInHierarchy && faces[0].GetComponents<CharacterPortraitBinding>().Length == 1
                    && faces[0].GetComponent<CharacterPortraitBinding>().enabled);
                var photoImages = photo != null ? photo.GetComponents<UnityEngine.UI.Image>() : Array.Empty<UnityEngine.UI.Image>();
                var photoMasks = photo != null ? photo.GetComponents<UnityEngine.UI.Mask>() : Array.Empty<UnityEngine.UI.Mask>();
                Row("photo." + name + ".surface", "ImageMask", 1, photoImages.Length, photoImages.Length == 1 && photoImages[0].enabled
                    && photoImages[0].color.a > 0f && photoMasks.Length == 1 && photoMasks[0].enabled && photoMasks[0].showMaskGraphic);
                // Face may legitimately await a NoUMA portrait; no provider/getter/texture or private request oracle.
                if (actor == null) continue; // Real-actor refusal is already explicit; no fabricated copy.
                Control(card, actor.name, "actor." + actor.id + ".pick");
                Control(card, EpisodeHud.CastTalkCaption((actor.name ?? "").Split(' ')[0]), "actor." + actor.id + ".talk");
                Copy(card, "Standing", RelationshipWeb.StandingWord(RelationshipWeb.KindOf(state, actor.id)), "actor." + actor.id + ".standing");
                string role = EpisodeHud.RoleFor(state, actor.id, out _);
                if (role == null && hereIds.Contains(actor.id)) role = "HERE";
                var pill = Part(photo, "Role", "actor." + actor.id + ".role", !string.IsNullOrEmpty(role));
                Copy(pill, "Text", role, "actor." + actor.id + ".roleCopy", !string.IsNullOrEmpty(role));
            }
            var pager = Part(board, "Pager", "pager", shown.Talk && pages > 1);
            Copy(pager, EpisodeHud.BoardPageName, "Page " + (page + 1) + " of " + pages, "pager.count", shown.Talk && pages > 1);
            Control(pager, EpisodeHud.CampaignPreviousCaption, "pager.previous", shown.Talk && page > 0);
            Control(pager, EpisodeHud.CampaignNextCaption, "pager.next", shown.Talk && page + 1 < pages);
            // The largest text's pager at the board's foot, when the board pages: where the page is,
            // and a way back and on, each exactly where there is somewhere to go.
            var turner = Part(board, EpisodeHud.LargestPagerName, "boardPager", boardPages > 1);
            Copy(turner, EpisodeHud.LargestPageCountName, "Page " + (boardPage + 1) + " of " + boardPages, "boardPager.count", boardPages > 1);
            Control(turner, EpisodeHud.PreviousPageCaption, "boardPager.previous", boardPages > 1 && boardPage > 0);
            Control(turner, EpisodeHud.NextPageCaption, "boardPager.next", boardPages > 1 && boardPage + 1 < boardPages);

            var primary = Control(panel, EpisodeDirector.BeginNextCompetitionCaption, "footer.primary");
            Copy(primary, EpisodeHud.DrawWayOnHeadlineName, EpisodeDirector.EndFreeTimeHeadline, "footer.primary.headline");
            var secondary = Control(panel, EpisodeDirector.StayInTheHouseCaption, "footer.secondary");
            Copy(secondary, EpisodeHud.SecondaryHeadlineName, EpisodeDirector.ContinueFreeTimeHeadline, "footer.secondary.headline");
            var footer = Part(panel, EpisodeHud.StrategyStripName, "footer.strip");
            string warning = FreeTimeExpectedAdvanceWarning11(state);
            string location = FreeTimeExpectedLocation11(capturedRooms);
            string selectedName = warning != null ? EpisodeDirector.AdvanceWarningName : location != null ? EpisodeHud.LocationCardName : EpisodeHud.FreeTimeTipName;
            string selectedCopy = warning ?? location ?? EpisodeDirector.PhaseRule(state);
            var selected = Copy(footer, selectedName, selectedCopy, "footer.selectedCopy");
            // Resolve both COMPLETE inactive-inclusive sets before recognizing a dormant source slot.
            var alternatives = footer != null ? footer.GetComponentsInChildren<TMP_Text>(true).Where(label => label != selected).ToArray() : Array.Empty<TMP_Text>();
            var cacheNodes = footer != null ? footer.GetComponentsInChildren<Transform>(true)
                .Where(node => node.name == EpisodeHud.UnusedActionsNoteName && (selected == null || node != selected.transform)).ToArray()
                : Array.Empty<Transform>();
            var cache = alternatives.Length == 1 ? alternatives[0] : null;
            bool dormantCache = alternatives.Length == 1 && cacheNodes.Length == 1 && cache != null
                && cache.GetType() == typeof(TextMeshProUGUI) && cache.transform == cacheNodes[0]
                && cache.transform is RectTransform cacheRect && cacheRect == cache.rectTransform
                && cache.transform.parent == footer && cache.GetComponents<TMP_Text>().Length == 1
                && cache.name == EpisodeHud.UnusedActionsNoteName && !cache.gameObject.activeSelf && !cache.gameObject.activeInHierarchy;
            bool cacheSetAdmitted = (alternatives.Length == 0 && cacheNodes.Length == 0) || dormantCache;
            FreeTimeIssue11(failures, cacheSetAdmitted, "FooterCacheSet:TMP=" + alternatives.Length.ToString(CultureInfo.InvariantCulture)
                + ":Named=" + cacheNodes.Length.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < alternatives.Length; index++)
            {
                var label = alternatives[index];
                bool cachedNotice = dormantCache && label == cache;
                Row("footer.cachedAlternative." + index.ToString(CultureInfo.InvariantCulture) + "." + FreeTimePath11(label.transform, footer),
                    "InactiveFooterCache", 0, 1, cachedNotice, null, false);
                if (!cachedNotice) inventory.Labels.Add(label); // Every invalid actual alternative stays refused AND measured.
            }
            for (int index = 0; index < cacheNodes.Length; index++)
            {
                var node = cacheNodes[index];
                if (alternatives.Any(label => label.transform == node)) continue; // Already recorded as its actual TMP component.
                Row("footer.cacheNode." + index.ToString(CultureInfo.InvariantCulture) + "." + FreeTimePath11(node, footer),
                    "WrongKindFooterCache", 0, 1, false, null, false); // No fabricated TMP or glyph for a missing component.
            }
            FreeTimeIssue11(failures, inventory.Labels.Distinct().Count() <= 256, "RequiredLabelCap");
            return inventory;
        }

        private static string FreeTimeExpectedStoryLine11(EpisodeState state)
        {
            var parts = EpisodeEngine.Threads(state).Where(thread => thread.ending == null).Select(thread => thread.label).ToList();
            int playing = state.storylines.Count(item => item.beatId != null && StorylineStatus.Running(item.status)
                && StoryCatalog.Find(item.templateId)?.play == null && item.lane != StoryLanes.Thread
                && !state.houseEvents.Any(beat => !beat.resolved && beat.cycleId == item.id));
            if (playing > 0) parts.Add(playing + (playing == 1 ? " storyline playing out" : " storylines playing out"));
            foreach (var modifier in state.activeModifiers.Where(item => item.weeksLeft > 0 && string.IsNullOrEmpty(item.ownerId)))
                parts.Add(modifier.name + " (" + modifier.weeksLeft + (modifier.weeksLeft == 1 ? " week left)" : " weeks left)"));
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "Nothing in play yet. Threads start from what you do.";
        }

        private static string FreeTimeExpectedAdvanceWarning11(EpisodeState state)
        {
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return null;
            var passing = EpisodeEngine.LapsingOnAdvance(state);
            if (passing.Count == 0) return null;
            string Each(HouseEventState beat)
            {
                var lapse = beat.choices.FirstOrDefault(choice => choice.optionId == beat.lapseOptionId);
                return StoryText.Title(beat) + (lapse != null ? " (" + lapse.label + ")" : "");
            }
            return "Moving on lets " + (passing.Count == 1 ? "1 storyline" : passing.Count + " storylines")
                + " pass: " + string.Join("; ", passing.Select(Each)) + ".";
        }

        private static string FreeTimeExpectedLocation11(IList<HouseMap.Room> capturedRooms)
        {
            var here = capturedRooms.FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return null;
            var others = (here.Occupants ?? new List<HouseMap.Occupant>()).Where(person => !person.IsPlayer).Select(person => (person.Name ?? "").Split(' ')[0]).ToArray();
            string company = others.Length == 0 ? "you have it to yourself" : others.Length
                + (others.Length == 1 ? " houseguest here: " : " houseguests here: ") + string.Join(", ", others);
            return RoomLabels.Name(here.Name) + "  ·  " + company;
        }

        private TMP_Text[] FreeTimeLabels11(RectTransform board)
        {
            var captions = new[] { EpisodeDirector.BeginNextCompetitionCaption, EpisodeDirector.StayInTheHouseCaption };
            return board.GetComponentsInChildren<TMP_Text>(true).Concat(director.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Where(button => button.gameObject.activeInHierarchy && captions.Contains(button.name))
                .SelectMany(button => button.GetComponentsInChildren<TMP_Text>(true))).Distinct().Where(label => !string.IsNullOrEmpty(label.text)).ToArray();
        }

        private FreeTimeMetric11 FreeTimeObserve11(string id, FreeTimeWaiting waiting, int[] cardPages, int boardPage = 0, int boardPages = 1)
        {
            Canvas.ForceUpdateCanvases();
            var failures = new List<string>();
            var board = ActiveRect(EpisodeHud.FreeTimeBoardName);
            var content = ActiveRect("Episode content");
            var panel = ActiveRect("Episode panel");
            Assert.That(board != null && content != null && panel != null, Is.True, "Metric setup must expose the actual board/content/panel.");
            var viewport = (RectTransform)content.parent;
            float scale = TextSizes.Scale(director.TextSize);
            FreeTimeIssue11(failures, director.TextSize == TextSize.Largest, "RequiredLargestSetting");
            var root = (RectTransform)board.GetComponentInParent<Canvas>().rootCanvas.transform;
            var shown = FreeTimeShownNow11(board);
            FreeTimeIssue11(failures, boardPages > 1 || (shown.Hero && shown.Strip && shown.Talk && shown.Moves), "OnePageShowsAllFour");
            var rowNames = new[] { EpisodeHud.FreeTimeHeroName, EpisodeHud.StoryStripName, EpisodeHud.HouseCardsName, EpisodeHud.HouseMovesName };
            var rowShown = new[] { shown.Hero, shown.Strip, shown.Talk, shown.Moves };
            var rows = rowNames.Select((name, i) => FreeTimePagePart11(board, name, rowShown[i], failures)).ToArray();
            var budget = FreeTimePagePart11(board, EpisodeHud.ScreenHeadName, shown.Hero, failures);
            FreeTimeRequiredFrame11(budget, failures);
            var talkHead = FreeTimePagePart11(board, EpisodeHud.TalkHeadName, shown.Talk, failures);
            var movesHead = FreeTimePagePart11(board, EpisodeHud.MovesHeadName, shown.Moves, failures);
            if (talkHead != null) FreeTimeRequiredCopy11(talkHead, "Head title", "TALK TO A HOUSEGUEST", failures);
            if (movesHead != null) FreeTimeRequiredCopy11(movesHead, "Head title", "OTHER WAYS TO SPEND YOUR TIME", failures);
            FreeTimePagePart11(board, EpisodeHud.ThreadsCardName, shown.Strip, failures);
            bool playInHero = waiting == FreeTimeWaiting.Nothing || waiting == FreeTimeWaiting.HaveNot;
            FreeTimePagePart11(board, EpisodeHud.CurrentPlayCardName, playInHero ? shown.Hero : shown.Strip, failures);
            FreeTimeIssue11(failures, content.rect.height <= viewport.rect.height + .5f, "NO_SCROLL_HEIGHT");
            FreeTimeIssue11(failures, FreeTimeWithin11(CanvasRect(viewport), CanvasRect(board))
                && FreeTimeWithin11(CanvasRect(panel), CanvasRect(board)), "BoardOutsideFrame");
            RectTransform rowAbove = null;
            for (int i = 0; i < rows.Length; i++)
                if (rows[i] != null)
                {
                    FreeTimeIssue11(failures, FreeTimeWithin11(CanvasRect(board), CanvasRect(rows[i])), "RowOutside:" + rowNames[i]);
                    if (rowAbove != null)
                        FreeTimeIssue11(failures, CanvasRect(rows[i]).yMax <= CanvasRect(rowAbove).yMin + .5f, "RowOrder:" + rowNames[i]);
                    rowAbove = rows[i];
                }
            if (budget != null && rows[0] != null)
                FreeTimeIssue11(failures, budget.IsChildOf(board) && FreeTimeWithin11(CanvasRect(board), CanvasRect(budget))
                    && Mathf.Abs(CanvasRect(budget).yMax - CanvasRect(rows[0]).yMax) < 1f
                    && CanvasRect(budget).xMin >= CanvasRect(rows[0]).xMax - .5f, "BudgetBesideHero");
            var state = director.Snapshot;
            int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));
            if (shown.Hero)
            {
                FreeTimeRequiredCopy11(board, EpisodeHud.BudgetRuleName, EpisodeDirector.BudgetRule(state), failures);
                FreeTimeRequiredCopy11(board, EpisodeHud.BudgetCopyName, EpisodeDirector.FreeTimeCostLine(state), failures);
                FreeTimeRequiredCopy11(board, EpisodeHud.UnusedActionsNoteName, EpisodeDirector.UnusedActionsNote(state), failures);
                FreeTimeRequiredCopy11(board, "Budget eyebrow", "FREE TIME", failures);
                FreeTimeRequiredCopy11(board, EpisodeHud.ActionsLeftCountName, left.ToString(), failures);
                FreeTimeRequiredCopy11(board, EpisodeHud.ActionsLeftWordName, left == 1 ? "ACTION LEFT" : "ACTIONS LEFT", failures);
            }
            if (rows[0] != null) FreeTimeIssue11(failures, rows[0].rect.height >= 104f * scale - .5f, "HeroFloor");
            var expectedCards = FreeTimeExpectedEightCards11(out var capturedRooms);
            // The first page of cards: as many as the suite names for this frame, never the renderer's arithmetic.
            if (shown.Talk)
                FreeTimeIssue11(failures, cardPages != null && cardPages.Length > 0 && FreeTimeCards().SequenceEqual(expectedCards.Take(cardPages[0])),
                    "RequiredFirstReadableCards");
            if (rows[3] != null) FreeTimeIssue11(failures, rows[3].childCount == 5, "RequiredFiveMoveTiles");
            var haveNot = board.GetComponentsInChildren<RectTransform>(true).Where(part => part.name == EpisodeHud.FreeTimeHaveNotName).ToArray();
            FreeTimeIssue11(failures, haveNot.Length == (waiting == FreeTimeWaiting.HaveNot && shown.Strip ? 1 : 0), "HaveNotPartSet");
            if (waiting == FreeTimeWaiting.HaveNot && shown.Strip) FreeTimeRequiredCopy11(board, EpisodeHud.FreeTimeHaveNotName, EpisodeDirector.HaveNotLine, failures);
            if (waiting == FreeTimeWaiting.Beat && shown.Hero)
            {
                FreeTimeRequiredFrame11(FreeTimeRequiredPart11(board, EpisodeHud.StoryBannerName, failures), failures);
                FreeTimeRequiredCopy11(board, "Banner title", FreeTimeBeatTitles[0], failures);
                FreeTimeRequiredCopy11(board, "Banner narrative", FreeTimeBeat(state, 0).narrative, failures);
                FreeTimeRequiredControl11(EpisodeDirector.AnswerBeatCaption, failures);
            }
            if (waiting == FreeTimeWaiting.Beat && shown.Strip) FreeTimeRequiredControl11(FreeTimeBeatTitles[1], failures);
            if (waiting == FreeTimeWaiting.Reply && shown.Hero)
            {
                var card = ReplyCards.Pending(state);
                Assert.That(card, Is.Not.Null, "The existing reply fixture is required; no synthetic accepted reply.");
                FreeTimeRequiredFrame11(FreeTimeRequiredPart11(board, EpisodeHud.FreeTimeReplyName, failures), failures);
                FreeTimeRequiredCopy11(board, "Plea title", ReplyCards.Title(state, card), failures);
                FreeTimeRequiredCopy11(board, "Plea message", ReplyCards.Message(state, card), failures);
                var grid = FreeTimeRequiredPart11(board, EpisodeHud.EventChoicesName, failures);
                foreach (var reply in ReplyCards.Replies(card.kind))
                {
                    string caption = EpisodeHud.ReplyCaption(reply.Label);
                    FreeTimeRequiredControl11(caption, failures);
                    if (grid == null) continue; // RequiredPart has already recorded an explicit NO_FIT.
                    var tiles = grid.Cast<Transform>().Where(part => part.name == caption).ToArray();
                    FreeTimeIssue11(failures, tiles.Length == 1, "RequiredReplyTile:" + caption);
                    if (tiles.Length != 1) continue;
                    FreeTimeRequiredFrame11((RectTransform)tiles[0], failures);
                    FreeTimeIssue11(failures, ((RectTransform)tiles[0]).rect.height >= 60f * scale - .5f, "ReplyTileFloor:" + caption);
                    var copy = tiles[0].GetComponentsInChildren<TMP_Text>(true).Where(label => label.text == ReplyCardPayoffs.Description(state, card, reply)).ToArray();
                    FreeTimeIssue11(failures, copy.Length == 1 && copy[0].gameObject.activeInHierarchy && copy[0].enabled, "RequiredConsequence:" + caption);
                    FreeTimeRequiredCopy11((RectTransform)tiles[0], "Risk word", EpisodeHud.RiskTag(reply.Risk), failures);
                }
            }
            if (playInHero && shown.Hero)
                FreeTimeRequiredFrame11(FreeTimeRequiredPart11(board, EpisodeHud.CurrentPlayCardName, failures), failures);
            if (shown.Moves)
                foreach (string caption in new[] { EpisodeDirector.ExploreTheHouseCaption, EpisodeDirector.OverviewListenCaption,
                    EpisodeHud.RallyHouseCaption, EpisodeHud.AirLaundryCaption, EpisodeDirector.DoAnActivityCaption })
                    FreeTimeRequiredControl11(caption, failures);
            if (shown.Hero)
                foreach (string caption in new[] { EpisodeHud.BuyBurnOneCaption, EpisodeHud.BuySpreadCaption })
                    FreeTimeRequiredControl11(caption, failures);
            foreach (string caption in new[] { EpisodeDirector.BeginNextCompetitionCaption, EpisodeDirector.StayInTheHouseCaption })
                FreeTimeRequiredControl11(caption, failures);
            FreeTimeIssue11(failures, LiveButtonsCarrying(EpisodeDirector.BeginNextCompetitionCaption) == 1, "OneLiveWayOn");
            var requiredInventory = FreeTimeRequiredInventory11(board, panel, state, capturedRooms, expectedCards, 0, waiting, failures,
                cardPages, shown, boardPage, boardPages);
            var labelSource = requiredInventory.Labels.Concat(FreeTimeLabels11(board)).Distinct().ToArray();
            FreeTimeIssue11(failures, labelSource.Length <= 256, "LabelCap");
            var labels = labelSource.Take(256).Select(label => FreeTimeObserveLabel11(label, board, viewport, panel, failures)).ToArray();
            var partSource = board.GetComponentsInChildren<RectTransform>(true);
            FreeTimeIssue11(failures, partSource.Length <= 512, "PartCap");
            var parts = new List<FreeTimePartMetric11>();
            foreach (var part in partSource.Take(512))
            {
                Rect box = CanvasRect(part);
                var element = part.GetComponent<UnityEngine.UI.LayoutElement>();
                parts.Add(new FreeTimePartMetric11 { path = FreeTimePath11(part, board), role = "RectTransform", active = part.gameObject.activeInHierarchy,
                    x = box.x, y = box.y, width = box.width, height = box.height, layoutElement = element != null,
                    minimumHeight = element != null ? element.minHeight : -1f, preferredHeight = element != null ? element.preferredHeight : -1f });
                if (part.name.StartsWith("Houseguest · ", StringComparison.Ordinal))
                {
                    FreeTimeIssue11(failures, part.rect.width >= 112f * scale - .5f && part.rect.height >= 129f * scale - .5f, "CardFloor");
                    FreeTimeRequiredFrame11(part, failures);
                }
                if (part.name == "Photo")
                    FreeTimeIssue11(failures, part.rect.height >= 48f * scale - .5f && part.rect.width >= 48f * scale - .5f, "PhotoFloor");
                if (rows[3] != null && part.parent == rows[3])
                {
                    FreeTimeIssue11(failures, part.rect.width >= 200f * scale - .5f && part.rect.height >= 72f * scale - .5f, "MoveFloor");
                    FreeTimeRequiredFrame11(part, failures);
                }
                var image = part.GetComponent<UnityEngine.UI.Image>();
                if (image == null) continue;
                Rect quad = FreeTimeImageQuad11(image);
                parts.Add(new FreeTimePartMetric11 { path = FreeTimePath11(part, board), role = "ImageAllocation", active = image.enabled && part.gameObject.activeInHierarchy,
                    x = quad.x, y = quad.y, width = quad.width, height = quad.height, imageType = image.type.ToString(), sprite = image.sprite != null ? image.sprite.name : "NONE",
                    colourAlpha = image.color.a });
                bool frameImage = part.name == "Art" || part.name == EndScreenKit.DrawnFrameName;
                bool requiresMesh = frameImage || image.color.a > 0f;
                if (image.enabled && part.gameObject.activeInHierarchy)
                {
                    string path = FreeTimePath11(part, board);
                    if (requiresMesh)
                    {
                        Rect drawn = FreeTimeImageMesh11(image, failures, path, out int count, out bool available);
                        parts.Add(new FreeTimePartMetric11 { path = path, role = "ImageMesh", active = true, meshAvailable = available, vertices = count,
                            culled = image.canvasRenderer.cull, x = drawn.x, y = drawn.y, width = drawn.width, height = drawn.height });
                        if (available)
                        {
                            var host = part.parent as RectTransform;
                            bool packArt = part.name == "Art";
                            var crossed = new List<string>();
                            if (!FreeTimeWithin11(CanvasRect(viewport), drawn)) crossed.Add("viewport");
                            if (!FreeTimeWithin11(CanvasRect(panel), drawn)) crossed.Add("panel");
                            if (packArt)
                            {
                                if (!FreeTimeWithin11(CanvasRect(part), drawn)) crossed.Add("own");
                                if (host != null && host != board && !FreeTimeWithin11(CanvasRect(board), CanvasRect(host))) crossed.Add("hostOffBoard");
                            }
                            else
                            {
                                if (!FreeTimeWithin11(CanvasRect(board), drawn)) crossed.Add("board");
                                if (host != null && !FreeTimeWithin11(CanvasRect(host), drawn)) crossed.Add("parent");
                            }
                            FreeTimeIssue11(failures, crossed.Count == 0, "ImageMeshOutside:" + path + "[" + string.Join(",", crossed) + "]");
                        }
                        FreeTimeIssue11(failures, !image.canvasRenderer.cull, "CulledRequiredImage:" + path);
                    }
                    else parts.Add(new FreeTimePartMetric11 { path = path, role = "TransparentHitImage", active = true,
                        culled = image.canvasRenderer.cull, colourAlpha = image.color.a });
                }
                if (frameImage)
                    FreeTimeIssue11(failures, image.enabled && part.gameObject.activeInHierarchy && image.color.a > 0f, "HiddenFrame:" + FreeTimePath11(part, board));
            }
            FreeTimeIssue11(failures, parts.Count <= 512, "CombinedPartCap");
            var footerCaptions = new[] { EpisodeDirector.BeginNextCompetitionCaption, EpisodeDirector.StayInTheHouseCaption };
            var buttonSource = board.GetComponentsInChildren<UnityEngine.UI.Button>(true).Concat(
                director.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(button => button.gameObject.activeInHierarchy && footerCaptions.Contains(button.name))).Distinct().ToArray();
            FreeTimeIssue11(failures, buttonSource.Length <= 128, "ControlCap");
            var controls = buttonSource.Take(128).Select(button =>
            {
                Rect box = CanvasRect((RectTransform)button.transform);
                bool onBoard = button.transform.IsChildOf(board);
                FreeTimeIssue11(failures, button.enabled && button.gameObject.activeInHierarchy && FreeTimeWithin11(CanvasRect(panel), box)
                    && (!onBoard || (FreeTimeWithin11(CanvasRect(board), box) && FreeTimeWithin11(CanvasRect(viewport), box))), "ControlOutside:" + button.name);
                return new FreeTimeControlMetric11 { path = FreeTimePath11(button.transform, director.transform), active = button.gameObject.activeInHierarchy,
                    enabled = button.enabled, interactable = button.IsInteractable(), x = box.x, y = box.y, width = box.width, height = box.height };
            }).ToArray();
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            var fitter = content.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            FreeTimeIssue11(failures, layout != null && fitter != null && fitter.verticalFit == UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize, "ContentOwners");
            float spacing = layout != null ? layout.spacing : 0f, top = layout != null ? layout.padding.top : 0f, bottom = layout != null ? layout.padding.bottom : 0f;
            var children = content.Cast<Transform>().Where(part => part.gameObject.activeInHierarchy).OfType<RectTransform>().ToArray();
            float other = children.Where(part => part != board).Sum(part => UnityEngine.UI.LayoutUtility.GetPreferredHeight(part));
            float copyLeast = labels.Where(label => label.path.EndsWith("/" + EpisodeHud.BudgetRuleName, StringComparison.Ordinal)
                || label.path.EndsWith("/" + EpisodeHud.BudgetCopyName, StringComparison.Ordinal)).Sum(label => Mathf.Ceil(label.minimumPreferredHeight) + 2f);
            var buy = FreeTimePagePart11(board, EpisodeHud.BuyActionsName, shown.Hero, failures);
            if (buy != null) FreeTimeIssue11(failures, buy.childCount == 2, "RequiredTwoBuyControls");
            if (buy != null) foreach (Transform button in buy) FreeTimeRequiredFrame11((RectTransform)button, failures);
            return new FreeTimeMetric11
            {
                id = id, status = failures.Count == 0 ? "FIT_OBSERVED" : "NO_FIT", failures = failures.ToArray(),
                scale = scale, canvasWidth = root.rect.width, canvasHeight = root.rect.height, contentHeight = content.rect.height, viewportHeight = viewport.rect.height,
                boardWidth = board.rect.width, boardHeight = board.rect.height, paddingTop = top, paddingBottom = bottom, spacing = spacing,
                postRenderRoomWithReserve2 = viewport.rect.height - top - bottom - Mathf.Max(0, children.Length - 1) * spacing - other - 2f,
                heroFloor = 104f * scale, cardFloor = 129f * scale, photoFloor = 48f * scale, moveHeightFloor = 72f * scale, moveWidthFloor = 200f * scale,
                heroHeight = rows[0] != null ? rows[0].rect.height : -1f, budgetHeight = budget != null ? budget.rect.height : -1f,
                cardsHeight = rows[2] != null ? rows[2].rect.height : -1f, movesHeight = rows[3] != null ? rows[3].rect.height : -1f,
                budgetCopyFloorPreferredHeight = 24f * scale + copyLeast + 4f * scale + 8f * scale + (buy != null ? buy.rect.height : -1f),
                readablePerPage = cardPages != null && cardPages.Length > 0 ? cardPages[0] : -1, page = boardPage + 1, pages = boardPages, sections = shown.ToString(),
                visibleCards = rows[2] != null ? FreeTimeCards().Length : 0, moveRows = rows[3] != null ? rows[3].Cast<Transform>().Select(part => Mathf.RoundToInt(((RectTransform)part).anchoredPosition.y)).Distinct().Count() : -1,
                labelsObserved = labelSource.Length, partsObserved = partSource.Length, controlsObserved = buttonSource.Length,
                verticalFit = fitter != null ? fitter.verticalFit.ToString() : "MISSING", horizontalFit = fitter != null ? fitter.horizontalFit.ToString() : "MISSING",
                controlChildWidth = layout != null && layout.childControlWidth, controlChildHeight = layout != null && layout.childControlHeight,
                forceExpandWidth = layout != null && layout.childForceExpandWidth, forceExpandHeight = layout != null && layout.childForceExpandHeight,
                labels = labels, parts = parts.Take(512).ToArray(), controls = controls, requiredSurfaces = requiredInventory.Required.ToArray(),
            };
        }

        /// <summary>
        /// The board's pages at the largest text in a house of eight, per fixture and frame: the
        /// suite's literals for the owner's contract (EpisodeHud.LargestPages.cs), measured on
        /// 2026-10-15, never the renderer's arithmetic. Two for every fixture on both frames.
        /// </summary>
        private static int FreeTimeLargestPagesAt11(FreeTimeWaiting waiting, string frame) => FreeTimeLargestPages;

        /// <summary>
        /// How many cards each of a house of eight's card pages holds at the largest text on a frame:
        /// the suite's literal - six then one on 4:3, all seven on 16:9 (D20).
        /// </summary>
        private static int[] FreeTimeLargestCardPagesAt11(string frame) => frame == "16x9" ? new[] { 7 } : new[] { 6, 1 };

        /// <summary>The board's four sections by their roots on the board, in reading order, for the walk over the pages.</summary>
        private static readonly string[] FreeTimeSectionRoots11 =
            { EpisodeHud.FreeTimeHeroName, EpisodeHud.StoryStripName, EpisodeHud.TalkHeadName, EpisodeHud.MovesHeadName };

        /// <summary>
        /// One fixture on one frame at the largest text, walked page by page: a receipt for each page,
        /// then the frame's own - FIT_OBSERVED for a board that is one page and passes, PAGED_OBSERVED
        /// for one whose pages are the suite's literal count, each passing for the sections it shows,
        /// every section on exactly one page and in order - else NO_FIT with every reason. A receipt
        /// too large for the log is logged without its per-part rows; its status is the observation's.
        /// </summary>
        private FreeTimeMetric11 FreeTimeObserveFrame11(string id, FreeTimeWaiting waiting, string frame)
        {
            int boardPages = FreeTimeLargestPagesAt11(waiting, frame);
            var cardPages = FreeTimeLargestCardPagesAt11(frame);
            var reasons = new List<string>();
            ShowFirstPage();
            var indicator = LargestPageIndicator();
            FreeTimeIssue11(reasons, indicator.Pages == boardPages, "BoardPageCount:" + indicator.Pages + "!=" + boardPages);
            var pageOf = new Dictionary<string, int>(StringComparer.Ordinal);
            FreeTimeMetric11 first = null;
            for (int page = 0; page < boardPages; page++)
            {
                var observation = FreeTimeObserve11(id + "_p" + (page + 1), waiting, cardPages, page, boardPages);
                first = first ?? observation;
                foreach (string failure in observation.failures) FreeTimeIssue11(reasons, false, "p" + (page + 1) + ":" + failure);
                var board = ActiveRect(EpisodeHud.FreeTimeBoardName);
                foreach (string root in FreeTimeSectionRoots11)
                    if (board != null && board.Cast<Transform>().Any(part => part.name == root && part.gameObject.activeInHierarchy))
                    {
                        FreeTimeIssue11(reasons, !pageOf.ContainsKey(root), "SectionOnTwoPages:" + root);
                        pageOf[root] = page;
                    }
                FreeTimeLogReceipt11("[Gamesim][FreeTimeMetric11Page] ", observation);
                if (page + 1 >= boardPages) break;
                var next = LivePageControl(EpisodeHud.NextPageCaption);
                FreeTimeIssue11(reasons, next != null, "NoWayOn:p" + (page + 1));
                if (next == null) break;
                next.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
            }
            for (int i = 0; i < FreeTimeSectionRoots11.Length; i++)
            {
                FreeTimeIssue11(reasons, pageOf.ContainsKey(FreeTimeSectionRoots11[i]), "SectionOnNoPage:" + FreeTimeSectionRoots11[i]);
                if (i > 0 && pageOf.ContainsKey(FreeTimeSectionRoots11[i]) && pageOf.ContainsKey(FreeTimeSectionRoots11[i - 1]))
                    FreeTimeIssue11(reasons, pageOf[FreeTimeSectionRoots11[i]] >= pageOf[FreeTimeSectionRoots11[i - 1]], "SectionOrder:" + FreeTimeSectionRoots11[i]);
            }
            ShowFirstPage();
            var summary = first ?? new FreeTimeMetric11();
            summary.id = id;
            summary.pages = boardPages;
            summary.failures = reasons.ToArray();
            summary.status = reasons.Count > 0 ? "NO_FIT" : boardPages > 1 ? "PAGED_OBSERVED" : "FIT_OBSERVED";
            return summary;
        }

        /// <summary>Logs a receipt; one past the log's cap is logged without its per-part rows, its status and failures as they are.</summary>
        private static void FreeTimeLogReceipt11(string tag, FreeTimeMetric11 receipt)
        {
            string json = JsonUtility.ToJson(receipt);
            if (Encoding.UTF8.GetByteCount(json) > 65536)
            {
                var labels = receipt.labels; var parts = receipt.parts; var controls = receipt.controls; var surfaces = receipt.requiredSurfaces;
                receipt.labels = null; receipt.parts = null; receipt.controls = null; receipt.requiredSurfaces = null;
                json = JsonUtility.ToJson(receipt);
                receipt.labels = labels; receipt.parts = parts; receipt.controls = controls; receipt.requiredSurfaces = surfaces;
            }
            Debug.Log(tag + json);
        }

        [UnityTest]
        public IEnumerator FreeTimeBoard_Metrics11_LargestEightFixedObservations()
        {
            var observations = new List<FreeTimeMetric11>();
            TextSize original = director.TextSize;
            try
            {
                foreach (var waiting in new[] { FreeTimeWaiting.Nothing, FreeTimeWaiting.Beat, FreeTimeWaiting.Reply, FreeTimeWaiting.HaveNot })
                {
                    yield return InstallFreeTime(8, waiting, waiting == FreeTimeWaiting.Beat ? 2 : 1);
                    yield return ApplyTextSize(TextSize.Largest);
                    Assert.That(director.TextSize, Is.EqualTo(TextSize.Largest), "The real settings must select Largest for every fixed metric case.");
                    yield return OpenStation();
                    yield return null;
                    foreach (var frame in new[] { (Width: 1024, Height: 768, Name: "4x3"), (Width: 1600, Height: 900, Name: "16x9") })
                        yield return AtFrame(frame.Width, frame.Height, () =>
                        {
                            string variant = waiting == FreeTimeWaiting.Beat ? "TwoBeats" : waiting.ToString();
                            var observation = FreeTimeObserveFrame11("FT11_" + variant + "_" + frame.Name, waiting, frame.Name);
                            observations.Add(observation);
                            FreeTimeLogReceipt11("[Gamesim][FreeTimeMetric11] ", observation);
                        });
                    director.ClosePanels();
                }
            }
            finally
            {
                director.ClosePanels();
                director.SetTextSize(original);
            }
            Assert.That(observations.Select(row => row.id), Is.EquivalentTo(new[] {
                "FT11_Nothing_4x3", "FT11_Nothing_16x9", "FT11_TwoBeats_4x3", "FT11_TwoBeats_16x9",
                "FT11_Reply_4x3", "FT11_Reply_16x9", "FT11_HaveNot_4x3", "FT11_HaveNot_16x9" }));
            Assert.That(observations.Select(row => row.id), Is.Unique, "Each real native metric case is observed exactly once.");
            Assert.That(observations.Where(row => row.status != "FIT_OBSERVED" && row.status != "PAGED_OBSERVED")
                    .Select(row => row.id + ": " + string.Join(", ", row.failures.Take(12))), Is.Empty,
                "Every receipt is a fit or the largest text's paging contract; NO_FIT is a retained failed diagnostic, and all eight receipts precede this assertion.");
        }

        private sealed class FreeTimePagerState11
        {
            internal string Json;
            internal uint MainRng, NpcRng;
            internal Dictionary<string, byte[]> Files;
        }

        private FreeTimePagerState11 FreeTimePagerStateNow11()
        {
            var state = director.Snapshot;
            Assert.That(state.npcSocial, Is.Not.Null, "The complete NPC RNG owner is part of pager neutrality.");
            string primary = Path.GetFullPath(director.SavePath);
            string directory = Path.GetDirectoryName(primary);
            Assert.That(directory, Is.EqualTo(Path.GetFullPath(temporaryDirectory)), "Read only this test's exact isolated save directory.");
            Assert.That((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, Is.True, "No redirected isolated save directory.");
            var paths = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(paths.Length, Is.LessThanOrEqualTo(32), "The isolated save custody is finite.");
            Assert.That(paths, Does.Contain(primary), "The committed primary save must exist.");
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                var info = new FileInfo(path);
                Assert.That((info.Attributes & FileAttributes.ReparsePoint) == 0 && info.Length <= 8388608, Is.True, "Ordinary finite isolated save files only.");
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    long length = input.Length;
                    Assert.That(length, Is.LessThanOrEqualTo(8388608L), "Held isolated save cap before allocation.");
                    var bytes = new byte[(int)length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int count = input.Read(bytes, read, Math.Min(65536, bytes.Length - read));
                        Assert.That(count, Is.GreaterThan(0), "A short isolated save read cannot be neutrality proof.");
                        read += count;
                    }
                    Assert.That(input.Length == length && input.ReadByte() == -1, Is.True, "Exact held save length and EOF.");
                    Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, Is.True, "No redirected save at final read.");
                    files.Add(path, bytes);
                }
            }
            return new FreeTimePagerState11 { Json = JsonUtility.ToJson(state), MainRng = state.randomState, NpcRng = state.npcSocial.randomState, Files = files };
        }

        private void FreeTimeAssertPagerNeutral11(FreeTimePagerState11 before, string where)
        {
            var after = FreeTimePagerStateNow11();
            Assert.That(after.Json == before.Json, Is.True, where + ": a real pager leaves the complete authoritative graph unchanged.");
            Assert.That(after.MainRng == before.MainRng && after.NpcRng == before.NpcRng, Is.True, where + ": both actual RNG owners are unchanged.");
            Assert.That(after.Files.Keys, Is.EquivalentTo(before.Files.Keys), where + ": disk membership is unchanged.");
            foreach (var pair in before.Files)
                Assert.That(after.Files[pair.Key].SequenceEqual(pair.Value), Is.True, where + ": every isolated save byte is unchanged.");
        }

        /// <summary>
        /// A house of eight's cards at the largest text, walked page by page by the cards' own pager:
        /// exactly the pages <paramref name="cardPages"/> names (how many cards on each - the suite's
        /// literal for the frame, D20), the seven others each on exactly one in order, every page's
        /// copy and controls whole, and each turn, forward and back, leaving the season, both random
        /// streams and every save byte as they were. At the standard and the larger text the seven
        /// are one page with no pager (FreeTimeBoard_NothingWaiting). A frame passes after each turn,
        /// as a player's next look does, before the board is held to its contract again.
        /// </summary>
        private IEnumerator AssertFreeTimeEightReadablePages11(string where, int[] cardPages)
        {
            var everybody = FreeTimeExpectedEightCards11(out var capturedRooms);
            Assert.That(everybody, Has.Length.EqualTo(7), where + ": all seven real others are required.");
            Assert.That(cardPages.Sum(), Is.EqualTo(7), where + ": the suite's pages hold the seven.");
            int pages = cardPages.Length;
            int largestPages = FreeTimeLargestPages;
            ShowPageWith(EpisodeHud.HouseCardsName);
            var first = FreeTimeCards();
            var seen = new List<string>();
            for (int page = 0; page < pages; page++)
            {
                Canvas.ForceUpdateCanvases();
                AssertFreeTimeBoard(where + ", readable page " + (page + 1), largestPages);
                ShowPageWith(EpisodeHud.HouseCardsName);
                var current = FreeTimeCards();
                Assert.That(current, Is.EqualTo(everybody.Skip(cardPages.Take(page).Sum()).Take(cardPages[page]).ToArray()),
                    where + ", card page " + (page + 1) + ": the suite's cards, in the house's order.");
                FreeTimeAssertEightPageCopy11(where, capturedRooms, everybody, page, cardPages);
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignPreviousCaption) != null, Is.EqualTo(page > 0), "Previous is a real page control exactly when needed.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.CampaignNextCaption) != null, Is.EqualTo(page + 1 < pages), "Next is a real page control exactly when needed.");
                seen.AddRange(current);
                if (page + 1 < pages)
                {
                    var before = FreeTimePagerStateNow11();
                    ButtonWithCaption(EpisodeHud.CampaignNextCaption).onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    FreeTimeAssertPagerNeutral11(before, where + ", Next");
                    yield return null; yield return null;
                }
            }
            Assert.That(seen, Is.Unique, where + ": no actor is repeated across pages.");
            Assert.That(seen, Is.EqualTo(everybody), where + ": all seven real others appear exactly once in order.");
            for (int page = pages - 1; page > 0; page--)
            {
                var before = FreeTimePagerStateNow11();
                ButtonWithCaption(EpisodeHud.CampaignPreviousCaption).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                FreeTimeAssertPagerNeutral11(before, where + ", Previous");
                yield return null; yield return null;
                AssertFreeTimeBoard(where + ", return page " + page, largestPages);
                ShowPageWith(EpisodeHud.HouseCardsName);
                FreeTimeAssertEightPageCopy11(where + ", return page " + page, capturedRooms, everybody, page - 1, cardPages);
            }
            Assert.That(FreeTimeCards(), Is.EqualTo(first), where + ": every real Previous returns to the original page.");
        }

        private string[] FreeTimeExpectedEightCards11(out IList<HouseMap.Room> capturedRooms)
        {
            capturedRooms = director.WhoIsWhere(); // SAME existing public presentation query; footer formatting must reuse this result.
            var here = capturedRooms.FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            var hereIds = new HashSet<string>(here.Occupants != null
                ? here.Occupants.Where(person => !person.IsPlayer).Select(person => person.Id) : Enumerable.Empty<string>(), StringComparer.Ordinal);
            // Exact OthersHereFirst projection: stable model order, with actual here-room actors first.
            return director.Snapshot.Active.Where(actor => !actor.isPlayer).OrderByDescending(actor => hereIds.Contains(actor.id) ? 1 : 0)
                .Select(actor => "Houseguest · " + actor.name).ToArray();
        }

        private void FreeTimeAssertEightPageCopy11(string where, IList<HouseMap.Room> capturedRooms, string[] everybody, int page, int[] cardPages)
        {
            float scale = TextSizes.Scale(director.TextSize);
            var failures = new List<string>();
            var content = ActiveRect("Episode content");
            var viewport = (RectTransform)content.parent;
            var panel = ActiveRect("Episode panel");
            var board = ActiveRect(EpisodeHud.FreeTimeBoardName);
            var state = director.Snapshot;
            var shown = FreeTimeShownNow11(board);
            if (shown.Hero)
            {
                FreeTimeRequiredCopy11(board, EpisodeHud.BudgetRuleName, EpisodeDirector.BudgetRule(state), failures);
                FreeTimeRequiredCopy11(board, EpisodeHud.BudgetCopyName, EpisodeDirector.FreeTimeCostLine(state), failures);
                FreeTimeRequiredCopy11(board, EpisodeHud.UnusedActionsNoteName, EpisodeDirector.UnusedActionsNote(state), failures);
            }
            foreach (Transform card in ActiveRect(EpisodeHud.HouseCardsName))
            {
                var rect = (RectTransform)card;
                Assert.That(rect.rect.width >= 112f * scale - .5f && rect.rect.height >= 129f * scale - .5f, Is.True,
                    where + ": each readable card retains the original scaled minimums.");
                var photos = card.GetComponentsInChildren<RectTransform>(true).Where(part => part.name == "Photo").ToArray();
                Assert.That(photos, Has.Length.EqualTo(1), where + ": no card may omit its photo allocation.");
                Assert.That(photos[0].gameObject.activeInHierarchy && photos[0].rect.height >= 48f * scale - .5f
                    && photos[0].rect.width >= 48f * scale - .5f, Is.True, where + ": the photo retains its original floor.");
                var actor = state.Active.SingleOrDefault(person => "Houseguest · " + person.name == card.name && !person.isPlayer);
                Assert.That(actor, Is.Not.Null, where + ": the card is a real other, not invented layout metadata.");
                FreeTimeRequiredControl11(actor.name, failures);
                FreeTimeRequiredControl11(EpisodeHud.CastTalkCaption((actor.name ?? "").Split(' ')[0]), failures);
                FreeTimeRequiredCopy11((RectTransform)card, "Standing", RelationshipWeb.StandingWord(RelationshipWeb.KindOf(state, actor.id)), failures);
            }
            var boardPage = LargestPageIndicator();
            var requiredInventory = FreeTimeRequiredInventory11(board, panel, state, capturedRooms, everybody, page, FreeTimeWaiting.Nothing, failures,
                cardPages, shown, boardPage.Page - 1, boardPage.Pages);
            var labels = requiredInventory.Labels.Concat(FreeTimeLabels11(board)).Distinct().ToArray();
            Assert.That(labels.Length, Is.LessThanOrEqualTo(256), "The whole current page's copy is finite; excess cannot pass.");
            foreach (var label in labels) FreeTimeObserveLabel11(label, board, viewport, panel, failures);
            Assert.That(failures, Is.Empty, where + ": every page's required copy/control and every nonempty label is complete at unchanged policy.");
        }
    }
}
