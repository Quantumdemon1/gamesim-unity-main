using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>The saved garment recipe crosses real UI and persistence boundaries without depending on UMA.</summary>
    public sealed class CharacterGarmentLifecyclePlayModeTests
    {
        private const string Knit = "gamesim.knit.crew.a", Vest = "gamesim.vest.competition.a";
        private static readonly string[] Outfits = { "Everyday", "Competition", "Formal", "Sleepwear", "Swimwear" };
        private GameObject owner, actor;
        private CharacterStudioPreview preview;
        private RecordingProvider provider;
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "GamesimGarmentLifecycle-" + Guid.NewGuid().ToString("N"));
            provider = new RecordingProvider();
            CharacterBodySource.Register(provider);
            preview = CharacterStudioPreview.Create("Garment lifecycle studio");
            preview.BuildTimeoutSeconds = 8f;
            provider.Studio = preview.transform;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CharacterBodySource.Unregister(provider);
            CharacterPortraits.Release();
            if (preview != null) Object.Destroy(preview.gameObject);
            if (owner != null) Object.Destroy(owner);
            if (actor != null) Object.Destroy(actor);
            yield return null;
            string resolved = Path.GetFullPath(root);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimGarmentLifecycle-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        [UnityTest]
        public IEnumerator PreviewLibraryCustomCastAndDurableSeasonRetainFiveIndependentOutfits()
        {
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = "Garment lifecycle guest";
            draft.Appearance = FiveOutfits();
            string appearanceKey = draft.Appearance.ContentKey();
            preview.Show(draft.Appearance);
            yield return Until(() => !preview.IsBuilding, "Initial garment preview");
            Assert.That(preview.CompletedKey, Is.EqualTo(appearanceKey));
            Assert.That(provider.StudioRequests.Single().Request.Appearance.ContentKey(), Is.EqualTo(appearanceKey));

            var library = new CharacterProfileStore(Path.Combine(root, "Profiles"));
            var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), draft);
            Assert.That(library.Save(profile, out var error), Is.True, error);
            string profilePath = Path.Combine(library.DirectoryPath, profile.id + ".json");
            byte[] profileBytes = File.ReadAllBytes(profilePath);
            Assert.That(library.TryLoad(profile.id, out var loadedProfile, out error), Is.True, error);
            Assert.That(loadedProfile.contestant.appearance.ContentKey(), Is.EqualTo(appearanceKey));

            owner = new GameObject("Garment cast setup");
            var cast = CastSelect.Attach(owner);
            cast.ConfigureProfiles(library);
            SeasonBuilder.Choice choice = null;
            cast.Show(value => choice = value, () => { }, (_, __) => Assert.Fail("Adding an NPC must not reopen the player creator."));
            cast.SetDraft(loadedProfile.ToDraft());
            Click(cast, "Cast slots");
            Click(cast, "Add Garment lifecycle guest to the cast");
            Click(cast, "Add Garment lifecycle guest to the cast");
            Click(cast, CastSelect.StartCaption);
            Assert.That(choice, Is.Not.Null);
            Assert.That(choice.CustomHouseguests, Has.Count.EqualTo(2));
            Assert.That(choice.CustomHouseguests[0].contestant.appearance, Is.Not.SameAs(choice.CustomHouseguests[1].contestant.appearance));

            var season = SeasonBuilder.Create(choice, 0x67a4c387u);
            CharacterAppearanceSnapshots.Materialize(season);
            var save = new EpisodeSaveStore(Path.Combine(root, "Season.json"));
            save.Save(season);
            Assert.That(save.TryLoad(out var restored, out error), Is.True, error);
            var people = new[] { restored.Find(ContentCatalog.PlayerId), restored.Find("custom-1"), restored.Find("custom-2") };
            foreach (var person in people)
                Assert.That(person.appearance.ContentKey(), Is.EqualTo(appearanceKey), person.id);
            string seasonBefore = JsonUtility.ToJson(restored);
            actor = new GameObject("Garment projection actor");
            foreach (string outfit in Outfits)
            {
                var projected = CharacterOutfits.ForContext(people[1], outfit);
                CharacterPresentation.Attach(actor, projected, Color.magenta);
                yield return null;
                var request = provider.Requests.Last(record => record.Request.ContestantId == "custom-1").Request;
                Assert.That(request.Purpose, Is.EqualTo(CharacterBuildPurpose.Gameplay));
                Assert.That(request.Appearance.activeOutfit, Is.EqualTo(outfit));
                Assert.That(Chest(request.Appearance, outfit), Is.EqualTo(outfit == "Competition" ? Vest : Knit));
                Assert.That(AppearanceEditing.TryFabric(request.Appearance.outfits.Single(set => set.id == outfit), "Chest", out var dye), Is.True);
                Assert.That(dye, Is.EqualTo(Dye(Array.IndexOf(Outfits, outfit))));
            }
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(seasonBefore), "Presentation cannot consume RNG, events, or rewrite saved outfit selections.");
            AppearanceEditing.SetColor(people[1].appearance, "Hair", Color.red);
            people[1].appearance.outfits[0].wardrobe.Single(worn => worn.slot == "Chest").itemId = Vest;
            Assert.That(people[0].appearance.ContentKey(), Is.EqualTo(appearanceKey));
            Assert.That(people[2].appearance.ContentKey(), Is.EqualTo(appearanceKey));
            Assert.That(File.ReadAllBytes(profilePath), Is.EqualTo(profileBytes), "Editing one season instance cannot rewrite its reusable library profile.");
        }

        [UnityTest]
        public IEnumerator RapidOutfitAndDyeEditsPublishOnlyTheNewestOwnedSnapshot()
        {
            var original = FiveOutfits();
            string originalKey = original.ContentKey();
            preview.Show(original);
            yield return Until(() => !preview.IsBuilding, "Initial preview");
            provider.CompleteImmediately = false;
            var pending = original.Clone();
            AppearanceEditing.SetFabric(pending, "Chest", Color.red);
            preview.Show(pending);
            yield return Until(() => provider.StudioRequests.Count() == 2, "Pending garment build");
            var stale = provider.StudioRequests.Last();

            var final = pending.Clone();
            final.activeOutfit = "Competition";
            AppearanceEditing.SetFabric(final, "Chest", Color.blue);
            preview.Show(final);
            AppearanceEditing.SetFabric(final, "Chest", Color.green);
            preview.Show(final); // Coalesce another fabric edit before the debounce completes.
            var submitted = final.Clone();
            string finalKey = submitted.ContentKey();
            AppearanceEditing.SetFabric(final, "Chest", Color.yellow); // Caller mutation after submission is not a preview edit.
            stale.State.Ready = true;
            Assert.That(preview.CompletedKey, Is.EqualTo(originalKey), "The previous completed pixels remain while the new garment is built.");
            yield return Until(() => provider.StudioRequests.Count() == 3, "Coalesced final garment build");
            Assert.That(preview.IsBuilding, Is.True);
            Assert.That(preview.CompletedKey, Is.EqualTo(originalKey), "A late ready flag from the retired garment cannot publish an intermediate outfit.");
            var latest = provider.StudioRequests.Last();
            Assert.That(latest.Request.Appearance.ContentKey(), Is.EqualTo(finalKey));
            Assert.That(latest.Request.Revision, Is.GreaterThan(stale.Request.Revision));
            Assert.That(Chest(latest.Request.Appearance, "Competition"), Is.EqualTo(Vest));
            Assert.That(AppearanceEditing.TryFabric(latest.Request.Appearance.outfits.Single(set => set.id == "Competition"), "Chest", out var tint), Is.True);
            Assert.That(tint, Is.EqualTo(Color.green));
            latest.State.Ready = true;
            yield return Until(() => !preview.IsBuilding, "Latest garment publication");
            Assert.That(preview.CompletedKey, Is.EqualTo(finalKey));
            Assert.That(original.ContentKey(), Is.EqualTo(originalKey), "Rapid edits cannot change the saved baseline or its other outfits.");
            preview.Show(submitted);
            yield return null;
            Assert.That(provider.StudioRequests.Count(), Is.EqualTo(3), "Showing the same completed recipe does not rebuild it.");
        }

        private static CharacterAppearance FiveOutfits()
        {
            var template = CastTemplates.Find("emma-brown");
            var appearance = new CharacterAppearance { provider = "acceptance", bodyId = "Human Female 3.0", presetId = template.Id,
                fallbackId = CharacterPresentation.AppearanceId(CastTemplates.ToContestant(template, false), template.Id) };
            AppearanceEditing.SetColor(appearance, "Hair", new Color(.2f, .3f, .4f));
            for (int i = 0; i < Outfits.Length; i++)
            {
                appearance.outfits.Add(new CharacterOutfit { id = Outfits[i], wardrobe = new List<AppearanceWardrobe>
                    { new AppearanceWardrobe { slot = "Chest", itemId = Outfits[i] == "Competition" ? Vest : Knit } } });
                appearance.activeOutfit = Outfits[i];
                AppearanceEditing.SetFabric(appearance, "Chest", Dye(i));
            }
            appearance.activeOutfit = "Everyday";
            return appearance;
        }

        private static Color Dye(int index) => new Color(.15f + index * .12f, .65f - index * .08f, .2f + index * .1f);
        private static string Chest(CharacterAppearance appearance, string outfit) => appearance.outfits.Single(set => set.id == outfit).wardrobe.Single(worn => worn.slot == "Chest").itemId;
        private static void Click(Component view, string caption)
        {
            var buttons = view.GetComponentsInChildren<Button>().Where(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>()?.text == caption).ToArray();
            Assert.That(buttons, Has.Length.EqualTo(1), caption);
            Assert.That(buttons[0].IsInteractable(), Is.True, caption);
            buttons[0].onClick.Invoke();
        }
        private static IEnumerator Until(Func<bool> done, string phase)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (!done() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(done(), Is.True, phase + " did not complete within eight seconds.");
        }

        private sealed class BuildRecord
        {
            public CharacterBodyRequest Request;
            public CharacterBodyBuildState State;
            public bool InStudio;
        }
        private sealed class RecordingProvider : IModularCharacterBodyProvider
        {
            public readonly List<BuildRecord> Requests = new List<BuildRecord>();
            public IEnumerable<BuildRecord> StudioRequests => Requests.Where(record => record.InStudio);
            public Transform Studio;
            public bool CompleteImmediately = true;
            public ICharacterAppearanceCatalog Catalog { get; } = new SnapshotCatalog();
            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color badge, out CharacterBody body)
            {
                var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.transform.SetParent(parent, false);
                var state = root.AddComponent<CharacterBodyBuildState>();
                state.Ready = CompleteImmediately; state.Revision = request.Revision;
                Requests.Add(new BuildRecord { Request = request, State = state, InStudio = Studio != null && parent.IsChildOf(Studio) });
                body = new CharacterBody(root, null, false);
                return true;
            }
            public bool TryCreate(string id, Transform parent, Color badge, out CharacterBody body)
            { body = default; throw new InvalidOperationException("Appearance must cross the provider boundary explicitly."); }
            public void SetWardrobeColor(in CharacterBody body, Color badge) { }
        }
        private sealed class SnapshotCatalog : ICharacterAppearanceCatalog
        {
            public IReadOnlyList<AppearanceBodyOption> Bodies { get; } = new[] { new AppearanceBodyOption("Human Female 3.0", "Body A") };
            public IReadOnlyList<AppearanceControl> Controls { get; } = Array.Empty<AppearanceControl>();
            public IReadOnlyList<AppearanceItem> Items { get; } = new[]
            {
                new AppearanceItem { Id = Knit, Label = "Crew-neck knit", Slot = "Chest" },
                new AppearanceItem { Id = Vest, Label = "Competition vest", Slot = "Chest" },
            };
            public CharacterAppearance Materialize(CharacterAppearance source) => source.Clone();
            public CharacterAppearance ChangeBody(CharacterAppearance source, string bodyId) => throw new NotSupportedException("Real body substitution is covered by the UMA garment fixture.");
        }
    }
}
