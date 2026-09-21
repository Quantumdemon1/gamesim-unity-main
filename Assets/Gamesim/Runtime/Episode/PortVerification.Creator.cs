using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>Bounded actual-UI creator workload. Captures are evidence for a separate visual review.</summary>
    public sealed partial class PortVerification
    {
        private bool verifyCreator;
        private CreatorVerificationReport creatorReport;
        private double creatorDeadline;
        private const int CreatorWorkloadSeconds = 480;

        private IEnumerator RunCreatorVerification(EpisodeDirector director)
        {
            creatorReport = new CreatorVerificationReport
            {
                startedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                processor = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                systemMemoryMB = SystemInfo.systemMemorySize, graphicsMemoryMB = SystemInfo.graphicsMemorySize,
                visualStatus = "Pending inspection of captured frames",
                workload = "Actual preset selection, both bodies at supported slider bounds in front/profile views, restored appearance, pointer slider input, six resolution/text combinations, isolated library save/load, repeated custom cast and NPC-only edit, new season, save/reload, isolated portrait. Automated checks do not establish visual quality or human usability.",
            };
            creatorReport.workloadLimitSeconds = CreatorWorkloadSeconds;
            creatorDeadline = Time.realtimeSinceStartupAsDouble + CreatorWorkloadSeconds;
            var stack = new Stack<IEnumerator>();
            stack.Push(CreatorRoute(director));
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                bool moved;
                try
                {
                    if (Time.realtimeSinceStartupAsDouble >= creatorDeadline) throw new TimeoutException("Creator verification exceeded its " + CreatorWorkloadSeconds + "-second bound.");
                    moved = current.MoveNext();
                }
                catch (Exception error) { creatorReport.errors.Add(error.ToString()); break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current.Current;
            }
            foreach (var unfinished in stack) (unfinished as IDisposable)?.Dispose();
            creatorReport.errors.AddRange(errors);
            creatorReport.finishedUtc = DateTime.UtcNow.ToString("O");
            creatorReport.status = creatorReport.errors.Count == 0 && creatorReport.saveReloadAppearanceEqual
                && creatorReport.pointerSliderChanged && creatorReport.previewHouseAppearanceEqual
                && creatorReport.isolatedLibrary && creatorReport.librarySaveLoadEqual && creatorReport.customCastRepeatedAndEdited
                && creatorReport.playerPreservedDuringNpcEdit && creatorReport.libraryUnaffectedByCastEdit && creatorReport.customCastSaveReloadEqual
                && creatorReport.bodyBoundsCaptures == 8 && creatorReport.bodyBoundsAppearanceRestored
                && creatorReport.resolutionCaptures == 6 ? "Automated checks passed; visual review required" : "Failed";
            File.WriteAllText(Path.Combine(outputDirectory, "creator-verification.json"), JsonUtility.ToJson(creatorReport, true));
            Debug.Log("Gamesim creator verification: " + creatorReport.status);
            Application.Quit(creatorReport.status == "Failed" ? 4 : 0);
        }

        private IEnumerator CreatorRoute(EpisodeDirector director)
        {
            CreatorRequire(!Application.isBatchMode && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
                "Creator visual QA needs a graphical standalone window, without -batchmode/-nographics.");
            seasonDirector = director;
            yield return SkipOpening();
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            double splashDeadline = Time.realtimeSinceStartupAsDouble + 15;
            while (!SplashScreen.isFinished && Time.realtimeSinceStartupAsDouble < splashDeadline) yield return null;
            director.OpenMainMenu();
            yield return null;
            yield return CreatorClick(MainMenu.NewSeasonCaption);
            yield return CreatorClick("Emma Brown");
            yield return CreatorClick(CharacterCreator.CustomiseCaption);
            var creator = director.GetComponentInChildren<CharacterCreator>(true);
            var castSelect = director.GetComponentInChildren<CastSelect>(true);
            CreatorRequire(creator != null && creator.IsShowing && creator.Draft.SourceTemplateId == "emma-brown",
                "The actual selected Emma card must reach the appearance-only creator.");
            var profiles = creator.ProfileStore;
            creatorReport.libraryDirectory = profiles.DirectoryPath;
            creatorReport.isolatedLibrary = ReferenceEquals(profiles, castSelect?.ProfileStore)
                && string.Equals(profiles.DirectoryPath, Path.GetFullPath(Path.Combine(outputDirectory, "Houseguests")), StringComparison.OrdinalIgnoreCase);
            CreatorRequire(creatorReport.isolatedLibrary, "Creator and cast must share the library under the explicit isolated save root.");
            var catalog = (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
            CreatorRequire(catalog != null, "This workload requires the installed modular character provider.");
            creatorReport.bodyCount = catalog.Bodies.Count; creatorReport.wardrobeCount = catalog.Items.Count;
            creatorReport.controlCount = catalog.Controls.Count;
            creator.FontScale = 1f;
            var gameplay = creator.Draft.Stats.Clone();
            uint randomBefore = director.Snapshot.randomState;
            yield return WaitCreatorPreview(creator, "preset");
            yield return CaptureCreatorBodyBounds(creator, catalog);

            var slider = creator.GetComponentsInChildren<Slider>().FirstOrDefault();
            CreatorRequire(slider != null, "The body category must expose at least one real supported DNA slider.");
            slider.Select();
            yield return null; yield return null; // SetupScrollFocus reveals this actual control before its pointer check.
            var rect = slider.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            float oldValue = slider.value;
            var point = Vector3.Lerp(corners[0], corners[3], oldValue < (slider.minValue + slider.maxValue) * .5f ? .8f : .2f);
            point.y = (corners[0].y + corners[1].y) * .5f;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, point), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            CreatorRequire(hits.Any(hit => hit.gameObject == slider.gameObject || hit.gameObject.transform.IsChildOf(slider.transform)),
                "The DNA slider must be reachable by an actual pointer raycast.");
            pointer.pointerCurrentRaycast = hits.First(hit => hit.gameObject == slider.gameObject || hit.gameObject.transform.IsChildOf(slider.transform));
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            creatorReport.pointerSliderChanged = !Mathf.Approximately(slider.value, oldValue);
            CreatorRequire(creatorReport.pointerSliderChanged, "Pointer input did not change the DNA slider.");
            yield return WaitCreatorPreview(creator, "pointer body edit");
            yield return CreatorClick("Hair");
            yield return CreatorClick("Next hair");
            yield return WaitCreatorPreview(creator, "hair change");
            yield return CreatorClick("Colors");
            yield return CreatorClick("Hair 9");
            yield return WaitCreatorPreview(creator, "hair color");
            yield return CreatorClick("Clothing");
            yield return CreatorClick("Next chest");
            yield return WaitCreatorPreview(creator, "outfit change");
            string editedKey = creator.Draft.Appearance.ContentKey();
            yield return CreatorClick("Undo");
            yield return WaitCreatorPreview(creator, "undo");
            CreatorRequire(creator.Draft.Appearance.ContentKey() != editedKey, "Undo must restore the earlier wardrobe choice.");
            yield return CreatorClick("Redo");
            yield return WaitCreatorPreview(creator, "redo");
            CreatorRequire(creator.Draft.Appearance.ContentKey() == editedKey, "Redo must restore the edited wardrobe exactly.");
            CreatorRequire(WebTraits.StatNames.All(stat => WebTraits.Get(gameplay, stat) == WebTraits.Get(creator.Draft.Stats, stat)),
                "Cosmetic controls must preserve the preset's gameplay build.");
            CreatorRequire(director.Snapshot.randomState == randomBefore, "Creator cosmetics must not consume season RNG.");
            creatorReport.cosmeticsPreservedGameplayAndRng = true;

            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
                foreach (bool large in new[] { false, true })
                {
                    Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                    creator.FontScale = large ? 1.2f : 1f;
                    for (int frame = 0; frame < 20; frame++) yield return null;
                    var capture = new CreatorCapture { requestedWidth = size.x, requestedHeight = size.y,
                        actualWidth = Screen.width, actualHeight = Screen.height, largerText = large };
                    CreatorRequire(Screen.width == size.x && Screen.height == size.y,
                        "The requested " + size.x + "x" + size.y + " capture actually rendered " + Screen.width + "x" + Screen.height + ".");
                    foreach (string caption in new[] { "Appearance", "Identity", "Personality", "My Houseguests", "Review", CharacterCreator.StartCaption, CharacterCreator.BackCaption })
                    {
                        var control = CreatorButton(caption);
                        CreatorRequire(control != null && CreatorRectVisible(control.GetComponent<RectTransform>()),
                            caption + " is outside the viewport at " + Screen.width + "x" + Screen.height + (large ? " larger text" : " standard text"));
                    }
                    capture.fixedControlsInsideViewport = true;
                    creatorReport.captures.Add(capture);
                    yield return CaptureCreator("creator-" + size.x + "x" + size.y + (large ? "-large" : "-normal"), capture);
                    creatorReport.resolutionCaptures++;
                }
            creator.FontScale = 1f;
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            yield return CreatorClick("Identity");
            var nameField = creator.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Name field");
            string profileName = "Creator QA " + Guid.NewGuid().ToString("N").Substring(0, 8);
            nameField.text = profileName;
            var authored = creator.Draft.Appearance.Clone();
            yield return CreatorClick("My Houseguests");
            yield return CreatorClick("Save this houseguest");
            var savedProfile = profiles.List().SingleOrDefault(profile => profile.name == profileName);
            CreatorRequire(savedProfile != null && profiles.ReadErrors.Count == 0, "The actual library Save action must produce a valid isolated profile.");
            creatorReport.libraryProfileId = savedProfile.id;
            CreatorRequire(File.Exists(Path.Combine(profiles.DirectoryPath, savedProfile.id + ".json")), "The profile file must exist under the configured library.");
            yield return CreatorClick("Identity");
            creator.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Name field").text = "Unsaved draft change";
            yield return CreatorClick("My Houseguests");
            yield return CreatorClick(profileName); // Load the saved library row, discarding the unsaved name.
            creatorReport.librarySaveLoadEqual = creator.Draft.Name == profileName && creator.Draft.Appearance.ContentKey() == authored.ContentKey();
            CreatorRequire(creatorReport.librarySaveLoadEqual, "Library Load must restore both identity and every appearance field.");
            yield return WaitCreatorPreview(creator, "library load");
            yield return CreatorClick(CharacterCreator.BackCaption);
            yield return CreatorClick("My Houseguests");
            yield return CreatorClick("Choose " + profileName); // Select the saved profile from the cast screen too.
            CreatorRequire(creator.Draft.Name == profileName && creator.Draft.Appearance.ContentKey() == authored.ContentKey(),
                "Cast selection must use the same saved profile library.");
            yield return CreatorClick(CharacterCreator.BackCaption);
            yield return CreatorClick("Cast slots");
            yield return CreatorClick("Add " + profileName + " to the cast");
            yield return CreatorClick("Add " + profileName + " to the cast");
            yield return CreatorClick("Edit slot 1");
            CreatorRequire(CreatorButton(CharacterCreator.ApplySlotCaption) != null && CreatorButton(CharacterCreator.StartCaption) == null,
                "An NPC edit must apply to its slot instead of starting a season.");
            yield return CreatorClick("Identity");
            string editedNpcName = profileName + " A";
            creator.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Name field").text = editedNpcName;
            yield return CreatorClick("Appearance");
            yield return CreatorClick("Hair");
            yield return CreatorClick("Next hair");
            yield return WaitCreatorPreview(creator, "custom NPC edit");
            string editedNpcAppearance = creator.Draft.Appearance.ContentKey();
            CreatorRequire(editedNpcAppearance != authored.ContentKey(), "The NPC-only edit must visibly change its saved appearance.");
            yield return CreatorClick(CharacterCreator.ApplySlotCaption);
            CreatorRequire(castSelect.IsShowing && !creator.IsShowing, "Applying the NPC edit must return directly to cast setup.");
            yield return CaptureCreator("creator-custom-cast-slots", null);
            CreatorRequire(profiles.TryLoad(savedProfile.id, out var unchangedProfile, out var profileError), profileError);
            creatorReport.libraryUnaffectedByCastEdit = unchangedProfile.name == profileName
                && unchangedProfile.contestant.appearance.ContentKey() == authored.ContentKey();
            CreatorRequire(creatorReport.libraryUnaffectedByCastEdit, "A proposed NPC slot must own its edit without rewriting the library profile.");
            yield return CreatorClick("Resume setup");
            creatorReport.playerPreservedDuringNpcEdit = creator.Draft.Name == profileName
                && creator.Draft.Appearance.ContentKey() == authored.ContentKey();
            CreatorRequire(creatorReport.playerPreservedDuringNpcEdit, "The player must retain their own identity and look after editing a repeated NPC.");
            yield return CreatorClick("Review");
            yield return CaptureCreator("creator-season-review", null);
            string previousSession = director.Snapshot.sessionId;
            yield return CreatorClick(CharacterCreator.StartCaption);
            CreatorRequire(director.Snapshot.sessionId != previousSession && !creator.IsShowing, "Starting must install a new safely saved season.");
            creatorReport.seed = director.Snapshot.seed;
            creatorReport.sessionId = director.Snapshot.sessionId;
            yield return SkipOpening();
            director.CloseMainMenu(); director.ClosePanels();
            var player = director.Snapshot.Find(director.Snapshot.playerId);
            var npcOne = director.Snapshot.Find("custom-1");
            var npcTwo = director.Snapshot.Find("custom-2");
            creatorReport.customCastRepeatedAndEdited = npcOne != null && npcTwo != null
                && npcOne.name == editedNpcName && npcTwo.name == profileName && player.name == profileName
                && npcOne.id != npcTwo.id && npcOne.id != player.id && npcTwo.id != player.id
                && npcOne.appearance.ContentKey() == editedNpcAppearance && npcTwo.appearance.ContentKey() == authored.ContentKey();
            CreatorRequire(creatorReport.customCastRepeatedAndEdited, "Repeated profiles must become distinct contestants, with the NPC edit confined to its chosen slot.");
            creatorReport.previewHouseAppearanceEqual = player.appearance.ContentKey() == authored.ContentKey();
            CreatorRequire(creatorReport.previewHouseAppearanceEqual, "The house must receive the exact creator appearance snapshot.");
            double bodyDeadline = Time.realtimeSinceStartupAsDouble + 50;
            CharacterPresentation body = null;
            while (Time.realtimeSinceStartupAsDouble < bodyDeadline)
            {
                body = FindObjectsByType<CharacterPresentation>(FindObjectsSortMode.None).FirstOrDefault(item => item.CharacterId == player.id);
                var progress = body == null ? null : body.GetComponentInChildren<CharacterBodyBuildState>();
                if (body != null && progress != null && progress.Ready) break;
                yield return null;
            }
            CreatorRequire(body != null && body.AppearanceKey == authored.ContentKey()
                && body.GetComponentInChildren<CharacterBodyBuildState>()?.Ready == true,
                "Gameplay body must complete the saved appearance revision.");
            director.FollowHouseguest(player.id);
            for (int frame = 0; frame < 30; frame++) yield return null;
            yield return CaptureCreator("creator-in-house", null);
            Texture portrait = null;
            double portraitStart = Time.realtimeSinceStartupAsDouble;
            while (portrait == null && Time.realtimeSinceStartupAsDouble - portraitStart < 50)
            { portrait = CharacterPortraits.Get(player); yield return null; }
            CreatorRequire(portrait != null, "The saved appearance must produce an isolated portrait.");
            creatorReport.portraitBuildMilliseconds = (Time.realtimeSinceStartupAsDouble - portraitStart) * 1000;
            SaveCreatorTexture(portrait, Path.Combine(outputDirectory, "creator-portrait.png"));
            director.OpenSettings();
            yield return CreatorClick("Save now  [F5]");
            string savedAppearance = director.Snapshot.Find(player.id).appearance.ContentKey();
            string savedPath = director.SavePath;
            CreatorRequire(Path.GetFullPath(savedPath).StartsWith(Path.GetFullPath(outputDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                "The creator workload must save only inside its isolated root.");
            yield return CreatorClick("Reload current slot");
            creatorReport.customCastSaveReloadEqual = director.Snapshot.Find("custom-1")?.appearance.ContentKey() == editedNpcAppearance
                && director.Snapshot.Find("custom-2")?.appearance.ContentKey() == authored.ContentKey()
                && director.Snapshot.Find("custom-1")?.name == editedNpcName && director.Snapshot.Find("custom-2")?.name == profileName;
            CreatorRequire(creatorReport.customCastSaveReloadEqual, "Reload must preserve both distinct custom cast instances and their exact appearance choices.");
            creatorReport.saveReloadAppearanceEqual = director.SavePath == savedPath
                && director.Snapshot.Find(player.id).appearance.ContentKey() == savedAppearance
                && director.StatusMessage.StartsWith("Local episode loaded and validated.", StringComparison.Ordinal);
            CreatorRequire(creatorReport.saveReloadAppearanceEqual, "Actual save/reload must retain and validate all appearance fields.");
            double reloadDeadline = Time.realtimeSinceStartupAsDouble + 50;
            CharacterPresentation reloadedBody = null;
            while (Time.realtimeSinceStartupAsDouble < reloadDeadline)
            {
                reloadedBody = FindObjectsByType<CharacterPresentation>(FindObjectsSortMode.None).FirstOrDefault(item => item.CharacterId == player.id);
                if (reloadedBody != null && reloadedBody.GetComponentInChildren<CharacterBodyBuildState>()?.Ready == true) break;
                yield return null;
            }
            CreatorRequire(reloadedBody != null && reloadedBody.GetComponentInChildren<CharacterBodyBuildState>()?.Ready == true,
                "Reloaded body must finish before its verification capture.");
            yield return CaptureCreator("creator-after-reload", null);
        }

        private IEnumerator CaptureCreatorBodyBounds(CharacterCreator creator, ICharacterAppearanceCatalog catalog)
        {
            CreatorRequire(catalog.Bodies.Count == 2, "The supported-body acceptance workload expects both installed human bases.");
            string original = creator.Draft.Appearance.ContentKey();
            yield return CreatorClick("Body");
            for (int bodyIndex = 0; bodyIndex < catalog.Bodies.Count; bodyIndex++)
            {
                var body = catalog.Bodies[bodyIndex];
                yield return CreatorClick(body.Label);
                CreatorRequire(creator.Draft.Appearance.bodyId == body.Id, "The body selector must apply " + body.Id + ".");
                var controls = catalog.Controls.Where(control => control.Category == "Body" && control.Fits(body.Id)).ToArray();
                CreatorRequire(controls.Length > 0, body.Id + " must expose supported body proportions.");
                foreach (bool maximum in new[] { false, true })
                {
                    var sample = new CreatorBodyBoundsCapture { bodyId = body.Id, bodyLabel = body.Label,
                        boundary = maximum ? "maximum" : "minimum" };
                    double started = Time.realtimeSinceStartupAsDouble;
                    foreach (var control in controls)
                    {
                        var slider = creator.GetComponentsInChildren<Slider>().SingleOrDefault(value => value.name == control.Label + " slider");
                        CreatorRequire(slider != null && slider.IsInteractable(), "Missing supported proportion control: " + body.Id + " / " + control.Id);
                        CreatorRequire(Mathf.Approximately(slider.minValue, control.Minimum) && Mathf.Approximately(slider.maxValue, control.Maximum),
                            "The UI and catalog must agree about tested proportion bounds: " + control.Id);
                        slider.Select(); yield return null;
                        // Drive the real slider and its normal onValueChanged path. Never invent a wider DNA range.
                        float tested = maximum ? slider.maxValue : slider.minValue;
                        slider.value = tested;
                        var saved = creator.Draft.Appearance.dna.SingleOrDefault(value => value.id == control.Id);
                        CreatorRequire(saved != null && Mathf.Approximately(saved.value, tested),
                            "The actual control did not apply its supported " + sample.boundary + ": " + control.Id);
                        sample.controls.Add(new CreatorControlBound { id = control.Id, minimum = slider.minValue,
                            maximum = slider.maxValue, testedValue = tested });
                    }
                    yield return WaitCreatorPreview(creator, body.Id + " " + sample.boundary + " body proportions");
                    sample.observedBuildMilliseconds = (Time.realtimeSinceStartupAsDouble - started) * 1000;
                    sample.editToCompletedBuildMilliseconds = creator.StudioPreview.LastBuildMilliseconds;
                    sample.previewStatus = creator.StudioPreview.Status;
                    string prefix = "creator-body-" + (bodyIndex + 1) + "-" + sample.boundary;
                    yield return CreatorClick("Front");
                    yield return CaptureCreator(prefix + "-front", null);
                    sample.frontPath = Path.Combine(outputDirectory, prefix + "-front.png");
                    creatorReport.bodyBoundsCaptures++;
                    yield return CreatorClick("Side");
                    yield return CaptureCreator(prefix + "-profile", null);
                    sample.profilePath = Path.Combine(outputDirectory, prefix + "-profile.png");
                    creatorReport.bodyBoundsCaptures++;
                    creatorReport.bodyBounds.Add(sample);
                }
            }
            yield return CreatorClick("Reset look");
            yield return CreatorClick("Front");
            yield return WaitCreatorPreview(creator, "restore after body bounds");
            creatorReport.bodyBoundsAppearanceRestored = creator.Draft.Appearance.ContentKey() == original;
            CreatorRequire(creatorReport.bodyBoundsAppearanceRestored,
                "Bounds captures must restore the exact starting appearance through Reset look before the saved-profile journey.");
        }

        private IEnumerator WaitCreatorPreview(CharacterCreator creator, string label)
        {
            double started = Time.realtimeSinceStartupAsDouble;
            string key = creator.Draft.Appearance.ContentKey();
            while (Time.realtimeSinceStartupAsDouble - started < 50)
            {
                var preview = creator.StudioPreview;
                if (preview != null && !preview.IsBuilding && preview.CompletedKey == key)
                {
                    creatorReport.builds.Add(new CreatorBuildTiming { operation = label,
                        observedWaitMilliseconds = (Time.realtimeSinceStartupAsDouble - started) * 1000,
                        editToCompletedBuildMilliseconds = preview.LastBuildMilliseconds, status = preview.Status });
                    CreatorRequire(!preview.CanRetry, "UMA preview unexpectedly used a fallback for " + label + ": " + preview.Status);
                    yield break;
                }
                yield return null;
            }
            throw new TimeoutException("The preview did not finish " + label + " within 50 seconds.");
        }

        private static Button CreatorButton(string caption) => FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(button => button.IsActive() && button.IsInteractable() && (button.name == caption
                || button.GetComponentsInChildren<TMP_Text>().Any(label => label.text == caption)));

        private IEnumerator CreatorClick(string caption)
        {
            var button = CreatorButton(caption);
            CreatorRequire(button != null, "No active creator action: " + caption);
            button.Select(); yield return null;
            button = CreatorButton(caption);
            CreatorRequire(button != null, "The creator action disappeared after focus: " + caption);
            button.onClick.Invoke();
            yield return null; yield return null;
        }

        private IEnumerator CaptureCreator(string name, CreatorCapture capture)
        {
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < 5; i++) yield return null;
            string path = Path.Combine(outputDirectory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            CreatorRequire(File.Exists(path) && new FileInfo(path).Length > 0, "Capture was not written: " + name);
            if (capture != null) capture.path = path;
            creatorReport.images.Add(path);
        }

        private static bool CreatorRectVisible(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return corners.All(corner =>
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
                return point.x >= -1f && point.y >= -1f && point.x <= Screen.width + 1f && point.y <= Screen.height + 1f;
            });
        }

        private static void SaveCreatorTexture(Texture texture, string path)
        {
            var temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, temporary); RenderTexture.active = temporary;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); Destroy(pixels); }
        }

        private static void CreatorRequire(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [Serializable] private sealed class CreatorBuildTiming
        { public string operation, status; public double observedWaitMilliseconds, editToCompletedBuildMilliseconds; }
        [Serializable] private sealed class CreatorControlBound
        { public string id; public float minimum, maximum, testedValue; }
        [Serializable] private sealed class CreatorBodyBoundsCapture
        {
            public string bodyId, bodyLabel, boundary, previewStatus, frontPath, profilePath;
            public double observedBuildMilliseconds, editToCompletedBuildMilliseconds;
            public List<CreatorControlBound> controls = new List<CreatorControlBound>();
        }
        [Serializable] private sealed class CreatorCapture
        {
            public string path; public int requestedWidth, requestedHeight, actualWidth, actualHeight;
            public bool largerText, fixedControlsInsideViewport;
        }
        [Serializable] private sealed class CreatorVerificationReport
        {
            public string status, visualStatus, startedUtc, finishedUtc, workload, processor, gpu, unityVersion, sessionId;
            public string libraryDirectory, libraryProfileId;
            public int systemMemoryMB, graphicsMemoryMB, bodyCount, wardrobeCount, controlCount, resolutionCaptures, bodyBoundsCaptures, workloadLimitSeconds;
            public uint seed;
            public bool pointerSliderChanged, cosmeticsPreservedGameplayAndRng, previewHouseAppearanceEqual, saveReloadAppearanceEqual;
            public bool isolatedLibrary, librarySaveLoadEqual, customCastRepeatedAndEdited, playerPreservedDuringNpcEdit,
                libraryUnaffectedByCastEdit, customCastSaveReloadEqual, bodyBoundsAppearanceRestored;
            public double portraitBuildMilliseconds;
            public List<CreatorBuildTiming> builds = new List<CreatorBuildTiming>();
            public List<CreatorBodyBoundsCapture> bodyBounds = new List<CreatorBodyBoundsCapture>();
            public List<CreatorCapture> captures = new List<CreatorCapture>();
            public List<string> images = new List<string>(), errors = new List<string>();
        }
    }
}
