using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using PartKind = Gamesim.Presentation.CompetitionApparatusMeshKit.PartKind;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>Borrowed geometry must remain ordinary registered, fitted, live instrument parts.</summary>
    public sealed class CompetitionApparatusMeshKitPlayModeTests
    {
        private readonly List<GameObject> owners = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var owner in owners) if (owner != null) Object.Destroy(owner);
            yield return null;
            yield return null;
            foreach (var asset in assets) if (asset != null) Object.Destroy(asset);
            owners.Clear(); assets.Clear();
            yield return null;
        }

        private CompetitionApparatus Make(string category, CompetitionApparatusMeshKit kit)
        {
            var owner = new GameObject("Authored instrument test anchor");
            owners.Add(owner);
            owner.transform.SetPositionAndRotation(new Vector3(4, 2, 6), Quaternion.Euler(0, 37, 0));
            // Create before first activation, as a real reserved stage does.
            owner.SetActive(false);
            var definition = CompetitionDefinitions.All.First(item => item.Category == category);
            var instrument = CompetitionApparatus.Create(owner.transform, definition, category, "entrant", UiTheme.Gold, kit);
            owner.SetActive(true);
            return instrument;
        }

        private CompetitionApparatusMeshKit Kit(params CompetitionApparatusMeshKit.Entry[] entries)
        {
            var kit = ScriptableObject.CreateInstance<CompetitionApparatusMeshKit>();
            kit.entries = entries; assets.Add(kit); return kit;
        }

        private CompetitionApparatusMeshKit.Entry Entry(CompetitionApparatus.Family family,
            PartKind kind, Vector3 dimensions, Mesh mesh)
            => new CompetitionApparatusMeshKit.Entry { family = family, partKind = kind, referenceDimensions = dimensions, mesh = mesh };

        private static MeshFilter Part(CompetitionApparatus instrument, string name)
            => instrument.GetComponentsInChildren<MeshFilter>(true).Single(part => part.name == name);

        // A chamfered prism with real corner cuts. Tests must not rename the primitive mesh
        // and then infer that an authored part was used from the same object name.
        private Mesh AuthoredMesh()
        {
            Vector2[] ring = { new Vector2(-.35f,-.5f), new Vector2(.35f,-.5f), new Vector2(.5f,-.35f),
                new Vector2(.5f,.35f), new Vector2(.35f,.5f), new Vector2(-.35f,.5f),
                new Vector2(-.5f,.35f), new Vector2(-.5f,-.35f) };
            var vertices = new List<Vector3>();
            for (int layer = 0; layer < 2; layer++)
                foreach (Vector2 point in ring) vertices.Add(new Vector3(point.x, layer == 0 ? -.5f : .5f, point.y));
            var triangles = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                triangles.AddRange(new[] { i, i + 8, next, next, i + 8, next + 8 });
            }
            for (int i = 1; i < 7; i++) triangles.AddRange(new[] { 0, i, i + 1, 8, i + 9, i + 8 });
            var mesh = new Mesh { name = "Authored chamfered instrument test mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateNormals(); assets.Add(mesh); return mesh;
        }

        [UnityTest]
        public IEnumerator Kit_MissingInvalidAndAmbiguousEntriesRetainTheOriginalPartGeometry()
        {
            var mesh = AuthoredMesh();
            var dimensions = new Vector3(.14f, .065f, .15f);
            var good = Entry(CompetitionApparatus.Family.Signals, PartKind.ResponsePad, dimensions, mesh);
            var wrongAspect = Entry(CompetitionApparatus.Family.Signals, PartKind.ResponsePad, new Vector3(.15f,.065f,.14f), mesh);
            var otherFamily = Entry(CompetitionApparatus.Family.PairConsole, PartKind.ResponsePad, dimensions, mesh);
            var undersized = AuthoredMesh();
            undersized.vertices = undersized.vertices.Select(vertex => vertex * .8f).ToArray();
            undersized.RecalculateBounds();
            var offsetInsideEnvelope = AuthoredMesh();
            // Size and containment still pass, so only the pivot check rejects this mesh.
            offsetInsideEnvelope.vertices = offsetInsideEnvelope.vertices.Select(vertex => vertex * .99992f + Vector3.right * .00011f).ToArray();
            offsetInsideEnvelope.RecalculateBounds();
            Assert.That(Mathf.Abs(offsetInsideEnvelope.bounds.size.x - 1f), Is.LessThan(.0001f));
            Assert.That(offsetInsideEnvelope.bounds.max.x, Is.LessThan(.5001f));
            Assert.That(offsetInsideEnvelope.bounds.center.x, Is.GreaterThan(.0001f));
            foreach (var kit in new[] { null, Kit(), Kit(wrongAspect), Kit(otherFamily), Kit(good, good),
                Kit(Entry(good.family,good.partKind,dimensions,undersized)),
                Kit(Entry(good.family,good.partKind,dimensions,offsetInsideEnvelope)) })
            {
                var instrument = Make("Skill", kit);
                var pad = Part(instrument, "Response pad");
                Assert.That(pad.sharedMesh.name, Is.EqualTo("Competition instrument block"));
                Assert.That(pad.sharedMesh.bounds, Is.EqualTo(new Bounds(Vector3.zero, Vector3.one)));
                Assert.That(instrument.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            var partlyAuthored = Make("Skill", Kit(good));
            Assert.That(Part(partlyAuthored, "Response pad").sharedMesh, Is.SameAs(mesh));
            Assert.That(Part(partlyAuthored, "Console housing").sharedMesh.name, Is.EqualTo("Competition instrument block"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Kit_RejectsUnsafeBoundsTopologyAndDimensionsWithoutRequiringReadableAssets()
        {
            var mesh = AuthoredMesh();
            var entry = Entry(CompetitionApparatus.Family.Signals, PartKind.ResponsePad, new Vector3(.14f,.065f,.15f), mesh);
            var kit = Kit(entry);
            Func<bool> resolve = () => kit.TryGetMesh(entry.family, entry.partKind, new Vector3(.14f,.065f,.15f), out _);
            Assert.That(resolve(), Is.True);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one*.8f); Assert.That(resolve(), Is.False, "Contained geometry is insufficient: hand contacts assume the authored unit extent.");
            mesh.bounds = new Bounds(new Vector3(.05f,0,0), Vector3.one*.8f); Assert.That(resolve(), Is.False, "A contained but off-centre mesh violates the part pivot contract.");
            mesh.bounds = new Bounds(new Vector3(.00011f,0,0), Vector3.one*.99992f); Assert.That(resolve(), Is.False, "Size tolerance and containment passing must not bypass the separate pivot tolerance.");
            mesh.bounds = new Bounds(new Vector3(.02f,0,0), Vector3.one); Assert.That(resolve(), Is.False, "Offset geometry exceeds the reserved unit envelope.");
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1,1,1.1f)); Assert.That(resolve(), Is.False);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1,0,1)); Assert.That(resolve(), Is.False);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            entry.referenceDimensions = new Vector3(float.NaN,.065f,.15f); Assert.That(resolve(), Is.False);
            entry.referenceDimensions = new Vector3(.14f,-.065f,.15f); Assert.That(resolve(), Is.False);
            entry.referenceDimensions = new Vector3(.14f,.065f,.15f);
            mesh.subMeshCount = 2; Assert.That(resolve(), Is.False, "One live material cannot silently cover an arbitrary material layout.");
            mesh.subMeshCount = 1;
            mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0); Assert.That(resolve(), Is.False);
            mesh = AuthoredMesh(); entry.mesh = mesh;
            mesh.UploadMeshData(true);
            Assert.That(mesh.isReadable, Is.False);
            Assert.That(resolve(), Is.True, "Imported production assets can discard their CPU vertex copy.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Kit_AuthoredMovingPartsKeepTheirIdentityFitsContactsAndOverlayOwnership()
        {
            var mesh = AuthoredMesh();
            var kit = Kit(
                Entry(CompetitionApparatus.Family.Signals, PartKind.ResponsePad, new Vector3(.14f,.065f,.15f), mesh),
                Entry(CompetitionApparatus.Family.PairConsole, PartKind.MemoryTile, new Vector3(.16f,.13f,.025f), mesh),
                Entry(CompetitionApparatus.Family.GripRig, PartKind.GripHandle, new Vector3(.78f,.045f,.055f), mesh),
                Entry(CompetitionApparatus.Family.GripRig, PartKind.RigUpright, new Vector3(.065f,1.56f,.09f), mesh),
                Entry(CompetitionApparatus.Family.GripRig, PartKind.GripRemaining, new Vector3(.035f,.69f,.025f), mesh),
                Entry(CompetitionApparatus.Family.DiceTray, PartKind.Die, Vector3.one*.17f, mesh),
                Entry(CompetitionApparatus.Family.DiceTray, PartKind.TrayPedestal, new Vector3(.20f,1.10f,.25f), mesh),
                Entry(CompetitionApparatus.Family.WordConsole, PartKind.LetterTile, new Vector3(.125f,.16f,.025f), mesh));
            foreach (string category in new[] { "Skill", "Mental", "Endurance", "Luck", "Social" })
            {
                var baseline = Make(category, null); var authored = Make(category, kit);
                var before = PartsByName(baseline); var after = PartsByName(authored);
                Assert.That(after.Keys, Is.EquivalentTo(before.Keys), "Authored geometry retains every named part and text transform.");
                Assert.That(after.Values.Count(part => part.sharedMesh == mesh), Is.GreaterThan(0));
                foreach (var pair in before)
                {
                    var changed = after[pair.Key];
                    Assert.That(changed.transform.localPosition, Is.EqualTo(pair.Value.transform.localPosition));
                    Assert.That(changed.transform.localRotation, Is.EqualTo(pair.Value.transform.localRotation));
                    Assert.That(changed.transform.localScale, Is.EqualTo(pair.Value.transform.localScale));
                    Assert.That(changed.GetComponent<Renderer>().sharedMaterial.name, Is.EqualTo(pair.Value.GetComponent<Renderer>().sharedMaterial.name));
                }
                var definition = CompetitionDefinitions.All.First(item => item.Category == category);
                var run = new MiniGameRun(CompetitionMiniGames.For(category), 123, 4, definition);
                if (run.Kind == CompetitionMiniGames.Kind.Dice) Assert.That(run.Roll(), Is.True);
                if (run.Kind == CompetitionMiniGames.Kind.Endurance) run.SetHolding(true);
                run.Tick(.225);
                string fingerprint = Fingerprint(run);
                foreach (var instrument in new[] { baseline, authored })
                {
                    var anchor = instrument.transform.parent;
                    var position = anchor.position; var rotation = anchor.rotation;
                    instrument.FitActorClearance(.40f, new Vector3(.13f,0,-.23f));
                    instrument.FitGrip(1.4f,-.19f,.60f,.32f,new Vector3(.13f,0,-.23f));
                    instrument.FitPress(new Vector3(.18f,1.53f,.08f),.62f);
                    instrument.Sync(run,true,false,false,false);
                    Assert.That(instrument.Fits(CompetitionStageFootprint.Station(instrument.Instrument,position,
                        rotation.eulerAngles.y,.40f,1.9f)),Is.True);
                    instrument.SetOverlaysVisible(false);
                    Assert.That(instrument.OverlayRenderers.All(renderer => !renderer.enabled), Is.True);
                    Assert.That(PartsByName(instrument).Values.All(part => part.GetComponent<Renderer>().enabled), Is.True);
                    Assert.That(instrument.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(anchor.position, Is.EqualTo(position)); Assert.That(anchor.rotation, Is.EqualTo(rotation));
                }
                Assert.That(Fingerprint(run), Is.EqualTo(fingerprint));
                Assert.That(authored.HandContact(true), Is.EqualTo(baseline.HandContact(true)));
                Assert.That(authored.HandContact(false), Is.EqualTo(baseline.HandContact(false)));
                foreach (string name in before.Keys)
                {
                    Assert.That(after[name].transform.localPosition, Is.EqualTo(before[name].transform.localPosition), name);
                    Assert.That(after[name].transform.localRotation, Is.EqualTo(before[name].transform.localRotation), name);
                    Assert.That(after[name].transform.localScale, Is.EqualTo(before[name].transform.localScale), name);
                }
                if (category == "Luck")
                {
                    Assert.That(Quaternion.Angle(Quaternion.identity, after["Die 0"].transform.localRotation), Is.GreaterThan(1));
                    authored.Sync(run,true,false,true,false);
                    Assert.That(after["Die 0"].transform.localRotation, Is.EqualTo(Quaternion.identity));
                    authored.Sync(run,true,false,false,true);
                    Assert.That(after["Die 0"].transform.localRotation, Is.EqualTo(Quaternion.identity));
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Kit_AuthoredDiceRemainRegisteredForClearanceYawSweepAndWholeStationContainment()
        {
            var mesh = AuthoredMesh();
            var kit = Kit(Entry(CompetitionApparatus.Family.DiceTray,PartKind.Die,Vector3.one*.17f,mesh));
            var instrument = Make("Luck",kit);
            var anchor = instrument.transform.parent;
            var die = Part(instrument,"Die 0");
            var footprint = CompetitionStageFootprint.Station(instrument.Instrument,anchor.position,anchor.eulerAngles.y,.40f,1.9f);
            var definition = CompetitionDefinitions.All.First(item => item.Category == "Luck");
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Dice,123,4,definition);
            Assert.That(run.Roll(),Is.True);
            Vector3 bodyOffset = new Vector3(.13f,0,-.23f);
            instrument.FitActorClearance(.40f,bodyOffset);
            Vector3 fitted = instrument.transform.localPosition;
            for (int frame = 0; frame < 5; frame++)
            {
                if(frame > 0)run.Tick(.1125);
                instrument.Sync(run,true,false,false,false);
                instrument.FitActorClearance(.40f,bodyOffset);
                Assert.That(instrument.transform.localPosition,Is.EqualTo(fitted));
                foreach(var part in PartsByName(instrument).Values.Where(part => !part.name.Contains("stance rail")))
                    foreach(var vertex in part.sharedMesh.vertices)
                    {
                        Vector3 point = anchor.InverseTransformPoint(part.transform.TransformPoint(vertex))-bodyOffset;
                        Assert.That(point.z,Is.GreaterThanOrEqualTo(.499f),"The borrowed die must participate in the original full-yaw reservation.");
                    }
                Assert.That(instrument.Fits(footprint),Is.True);
            }
            Vector3 originalPosition = die.transform.localPosition;
            die.transform.localPosition += Vector3.right*4;
            Assert.That(instrument.Fits(footprint),Is.False,"A borrowed mesh remains in the complete solid registry; its name is irrelevant.");
            die.transform.localPosition=originalPosition;
            Assert.That(instrument.Fits(footprint),Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Kit_AuthoredFacesRepeatOnlyPublicInformationAndTintTheOriginalRenderers()
        {
            var mesh=AuthoredMesh();
            var kit=Kit(
                Entry(CompetitionApparatus.Family.PairConsole,PartKind.MemoryTile,new Vector3(.16f,.13f,.025f),mesh),
                Entry(CompetitionApparatus.Family.WordConsole,PartKind.LetterTile,new Vector3(.125f,.16f,.025f),mesh));
            var instrument=Make("Mental",kit);
            var definition=CompetitionDefinitions.All.First(item=>item.Category=="Mental");
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Memory,7,4,definition);
            instrument.Sync(run,false,false,false,false);
            Assert.That(instrument.VisibleMemoryFaces,Is.Zero);
            Assert.That(instrument.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name.StartsWith("Memory face")).All(t=>t.text=="?"),Is.True);
            run.Flip(0);string fingerprint=Fingerprint(run);
            instrument.Sync(run,true,false,false,false);
            Assert.That(instrument.VisibleMemoryFaces,Is.EqualTo(1));
            var front=Part(instrument,"Memory tile 0");var back=Part(instrument,"Memory tile 0 audience face");
            Assert.That(front.sharedMesh,Is.SameAs(mesh));Assert.That(back.sharedMesh,Is.SameAs(mesh));
            var frontTint=new MaterialPropertyBlock();var backTint=new MaterialPropertyBlock();
            front.GetComponent<Renderer>().GetPropertyBlock(frontTint);back.GetComponent<Renderer>().GetPropertyBlock(backTint);
            Assert.That(frontTint.GetColor("_BaseColor"),Is.EqualTo(UiTheme.Accent));
            Assert.That(backTint.GetColor("_BaseColor"),Is.EqualTo(frontTint.GetColor("_BaseColor")));
            Assert.That(Fingerprint(run),Is.EqualTo(fingerprint));

            instrument=Make("Social",kit);definition=CompetitionDefinitions.All.First(item=>item.Category=="Social");
            run=new MiniGameRun(CompetitionMiniGames.Kind.Words,123,4,definition);
            instrument.Sync(run,true,false,false,false);
            var letters=instrument.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name.StartsWith("Letter ")).ToArray();
            Assert.That(letters.Any(t=>t.text!="?" && t.text!=""),Is.True);
            fingerprint=Fingerprint(run);instrument.Sync(run,true,false,true,false);
            Assert.That(letters.All(t=>t.text=="?" || t.text==""),Is.True,"Both borrowed console faces obey pause disclosure.");
            Assert.That(Fingerprint(run),Is.EqualTo(fingerprint));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Kit_ReleasingOneApparatusDestroysOnlyItsOwnedResourcesAndLeavesSharedGeometryUsable()
        {
            var mesh=AuthoredMesh();
            var kit=Kit(Entry(CompetitionApparatus.Family.Signals,PartKind.ResponsePad,new Vector3(.14f,.065f,.15f),mesh));
            var first=Make("Skill",kit);var second=Make("Skill",kit);
            var fallback=Part(first,"Console housing").sharedMesh;
            var material=Part(first,"Response pad").GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(first.transform.parent.gameObject);
            yield return null;yield return null;
            Assert.That(fallback==null,Is.True);Assert.That(material==null,Is.True);
            Assert.That(mesh!=null,Is.True,"The kit's imported mesh is borrowed, never stage-owned.");
            Assert.That(kit!=null,Is.True);Assert.That(Part(second,"Response pad").sharedMesh,Is.SameAs(mesh));
            second.FitPress(new Vector3(.18f,1.53f,.08f),.62f);
            Assert.That(Part(second,"Response pad").GetComponent<Renderer>().enabled,Is.True);
            Assert.That(mesh.vertexCount,Is.GreaterThan(8));
        }

        [UnityTest, Explicit("Run after importing all authored FBXs and building the optional kit; absence must fail this production asset gate.")]
        public IEnumerator Kit_InstalledAuthoredAssetsCoverEveryFamilyAndFitTheExistingReservations()
        {
            var kit=Resources.Load<CompetitionApparatusMeshKit>(CompetitionApparatusMeshKit.ResourceName);
            Assert.That(kit,Is.Not.Null,"Run the authored kit builder before this explicit acceptance gate.");
            Assert.That(kit.entries.Select(entry=>(entry.family,entry.partKind)).Distinct().Count(),Is.EqualTo(kit.entries.Length));
            var referenced=new HashSet<Mesh>(kit.entries.Select(entry=>entry.mesh));
            var used=new HashSet<Mesh>();
            foreach(var definition in CompetitionDefinitions.All)
            {
                var instrument=Make(definition.Category,kit);
                var anchor=instrument.transform.parent;
                foreach(var part in PartsByName(instrument).Values)
                {
                    Assert.That(referenced.Contains(part.sharedMesh),Is.True,definition.Id+" / "+part.name+": no primitive fallback in the complete installed kit.");
                    used.Add(part.sharedMesh);
                }
                foreach(float shoulder in new[]{1.25f,1.65f})
                {
                    instrument.FitActorClearance(.40f,new Vector3(.13f,0,-.12f));
                    instrument.FitGrip(shoulder,.04f,.70f,.40f,new Vector3(.13f,0,-.12f));
                    instrument.FitPress(new Vector3(.18f,shoulder,.08f),.62f);
                    var run=new MiniGameRun(CompetitionMiniGames.For(definition.Category),123,4,definition);
                    if(run.Kind==CompetitionMiniGames.Kind.Dice){Assert.That(run.Roll(),Is.True);run.Tick(.225);}
                    instrument.Sync(run,true,false,false,false);
                    Assert.That(instrument.Fits(CompetitionStageFootprint.Station(instrument.Instrument,
                        anchor.position,anchor.eulerAngles.y,.40f,1.9f)),Is.True,definition.Id);
                }
            }
            Assert.That(used,Is.EquivalentTo(referenced),"Every referenced mesh must be exercised by a real family, not merely stored in an unused entry.");
            yield return null;
        }

        private static Dictionary<string,MeshFilter> PartsByName(CompetitionApparatus instrument)
            => instrument.GetComponentsInChildren<MeshFilter>(true).Where(part=>part.transform.parent==instrument.transform && part.GetComponent<TMP_Text>()==null).ToDictionary(part=>part.name);

        private static string Fingerprint(MiniGameRun run)
        {
            var random=(SeededRandom)typeof(MiniGameRun).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(run);
            return string.Join("/",run.Elapsed,run.Finished,run.Score,random.State,run.Meter,run.Held,run.Holding,
                run.Hits,run.Spawned,run.TargetLive,run.TargetDirection,run.MatchedPairs,run.FirstFlip,run.SecondFlip,
                run.RollsUsed,run.RollTotal,run.KeptTotal,run.Word,run.Scrambled,run.Spelled,run.WordsSolved,run.WordPoints);
        }
    }
}
