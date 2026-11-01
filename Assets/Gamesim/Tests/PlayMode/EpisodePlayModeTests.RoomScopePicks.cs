using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Clicks and hover (ROOM-SCOPE-PLAN §3.2 f, D13, RS2b): while the scope is a room, a piece of
    /// furniture whose room the main camera leaves out cannot be clicked or hovered. Its collision proxy
    /// is drawn as its prop is (<see cref="HouseScopePick.OwnerOf"/>), so the click and the hover's one
    /// pick pass over it to whatever stands behind - the floor, which walks the player there and says
    /// nothing - and drawn again it takes them as it always has. The colliders never move: only what the
    /// camera draws decides. Every source is still off, so each test holds the scope on.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A click aimed at a piece of furniture in a room the view leaves out passes over it to the floor
        /// behind it and walks the player there, opening nothing; with the view on the furniture's own
        /// room, or the whole house, the same click lands on it and opens it.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AClickPassesThroughHiddenFurnitureToTheFloor()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            yield return null;
            Physics.SyncTransforms();
            var props = Rs2bClickableProps();
            Assert.That(props, Is.Not.Empty, "The house has clickable furniture with a collision proxy, its renderers on its room's layer from load.");
            Rs2bProp prop = null;
            Ray ray = default;
            RaycastHit floor = default;
            foreach (var candidate in props.OrderBy(each => each.Room == RoomLayers.IndexOf("Yard") ? 1 : 0))
                if (Rs2bRayThrough(candidate, out ray, out floor)) { prop = candidate; break; }
            Assert.That(prop, Is.Not.Null, "A ray that meets a clickable prop's proxy first and the bare floor next, among " + string.Join(", ", props));
            int far = Rs4FarRoom(scope, prop.Proxy.bounds.center, floor.point);
            ScopePlace(player.transform, ScopeOpenFloor(scope, far), 2f);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(Rs2bFirstTwo(ray, prop, out var first, out var next) && first.collider == prop.Proxy && next.collider == floor.collider, Is.True,
                "With the player in the " + RoomLayers.RoomIds[far] + ", the ray still meets " + prop + " first and the floor next.");
            Assert.That(player.TryMeasureRoute(floor.point, out _), Is.True, "The floor behind it is reachable from the " + RoomLayers.RoomIds[far] + ".");
            Assert.That(player.InputEnabled, Is.True, "The player's clicks are on.");
            Assert.That(HouseScopePick.OwnerOf(prop.Proxy), Is.SameAs(prop.Root), "A furniture proxy's owner is the prop it hangs under.");

            var scene = director.gameObject.scene;
            var eye = cameraRig.ViewCamera;
            int propBit = 1 << (RoomLayers.First + prop.Room);
            var screen = (Vector2)eye.WorldToScreenPoint(floor.point);
            var owner = new object();
            int opened = 0;
            HouseInteractionAnchor openedAnchor = null;
            Action<HouseInteractionAnchor> count = selected => { opened++; openedAnchor = selected; };
            player.FurnitureSelected += count;
            cameraRig.SetReducedMotion(true);
            try
            {
                // The whole house, nothing held: the click's pick is Physics.Raycast's own hit.
                Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "Nothing held: the whole house.");
                Assert.That(player.TryPointAt(ray, out var whole) && whole.collider == prop.Proxy, Is.True,
                    "With the whole house drawn, the click lands on " + prop + " as it always has.");

                // Hidden: the view on a room well away from it.
                cameraRig.MoveTo(Rs4Shot(scope, RoomLayers.RoomIds[far]));
                director.HoldRoomScope(owner);
                yield return null; yield return null;
                Physics.SyncTransforms();
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(far)), "The view is the " + RoomLayers.RoomIds[far] + ".");
                Assert.That(eye.cullingMask & propBit, Is.Zero, "The main camera leaves the " + RoomLayers.RoomIds[prop.Room] + " out.");
                Assert.That(Physics.Raycast(ray, out var raw, 500f, HouseLayers.Pick, QueryTriggerInteraction.Ignore) && raw.collider == prop.Proxy, Is.True,
                    "Its collider is where it was: the scope moves no collider (§3.3).");
                Assert.That(HouseScopePick.Draws(eye, prop.Proxy), Is.False, "The main camera does not draw what it belongs to.");
                Assert.That(player.TryPointAt(ray, out var through), Is.True, "The click's pick finds something behind it.");
                Assert.That(through.collider, Is.SameAs(floor.collider), "It passes over " + prop + " to the floor behind it (D13): " + through.collider.name + ".");
                Assert.That(HouseScopePick.Draws(eye, through.collider), Is.True, "which the camera draws: the floor is the shell's.");
                Assert.That(HouseFurniture.AtProp(scene, through.transform, through.point), Is.Null, "and the floor is no furniture.");
                player.DispatchClick(ray, screen);
                Assert.That(opened, Is.Zero, "Clicking it opens nothing.");
                Assert.That(player.Agent.hasPath, Is.True, "The click on the floor behind it walks the player.");
                Assert.That(FlatDistance(player.Agent.pathEndPosition, through.point), Is.LessThan(.5f),
                    "to where it landed: " + FlatDistance(player.Agent.pathEndPosition, through.point).ToString("0.00") + " m off it.");
                // Stopped and put back, and the camera lets them go.
                player.StopHere();
                ScopePlace(player.transform, ScopeOpenFloor(scope, far), 2f);
                cameraRig.ClearSubject();

                // Drawn: the view on its own room. The same ray lands on it, and the click opens it.
                cameraRig.MoveTo(Rs4Shot(scope, RoomLayers.RoomIds[prop.Room]));
                yield return null; yield return null;
                Physics.SyncTransforms();
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(prop.Room)), "The view is the " + RoomLayers.RoomIds[prop.Room] + ".");
                Assert.That(eye.cullingMask & propBit, Is.Not.Zero, "The main camera draws it.");
                Assert.That(HouseScopePick.Draws(eye, prop.Proxy), Is.True);
                Assert.That(player.TryPointAt(ray, out var picked) && picked.collider == prop.Proxy, Is.True, "Drawn, the same ray lands on " + prop + ".");
                var anchor = HouseFurniture.AtProp(scene, picked.transform, picked.point);
                Assert.That(anchor, Is.Not.Null, "and names what a click on it does.");
                player.DispatchClick(ray, screen);
                Assert.That(opened, Is.EqualTo(1), "The click opens it.");
                Assert.That(openedAnchor, Is.SameAs(anchor), "the furniture under the click: " + anchor.VenueId + ".");
            }
            finally
            {
                player.FurnitureSelected -= count;
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                cameraRig.SetReducedMotion(false);
            }
        }

        /// <summary>
        /// The hover says nothing over a piece of furniture whose room the view leaves out, and says what a
        /// click does again once it is drawn - with neither the pointer nor the camera moving, since the
        /// room scope changing what the camera draws changes what the pointer is over: once for each change,
        /// whether the mask changes or, under a room view, a piece of the prop goes to the shell with the
        /// mask as it was, and never while nothing changes.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_HoverSkipsHiddenFurniture()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            yield return null;
            Physics.SyncTransforms();
            var props = Rs2bClickableProps();
            Assert.That(props, Is.Not.Empty, "The house has clickable furniture with a collision proxy, its renderers on its room's layer from load.");
            var camera = cameraRig.ViewCamera;
            var owner = new object();
            var mouse = InputSystem.AddDevice<Mouse>();
            HouseInteractionAnchor heard = null;
            int hovers = 0;
            Action<HouseInteractionAnchor, Vector2> listen = (hovered, at) => { heard = hovered; hovers++; };
            player.FurnitureHovered += listen;
            cameraRig.SetReducedMotion(true);
            Renderer shellPiece = null;
            int shellPieceLayer = 0;
            try
            {
                // A free move onto a room beside a piece of clickable furniture, the piece still in frame,
                // under a point the HUD leaves alone, its proxy the first thing the pointer's ray meets and the
                // bare floor the next.
                Rs2bProp prop = null;
                int beside = -1;
                Vector2 screen = default;
                RaycastHit floor = default;
                HouseInteractionAnchor named = null;
                var tried = new List<string>();
                foreach (var candidate in props)
                {
                    foreach (var focus in Rs2bFociBeside(scope, candidate))
                    {
                        foreach (float distance in new[] { 12f, 16f })
                        {
                            cameraRig.MoveTo(focus.Point, distance);
                            yield return null; yield return null;
                            Physics.SyncTransforms();
                            if (cameraRig.Rooms != RoomSet.Of(focus.Room)) continue;
                            if (!Rs2bPointAt(camera, candidate, out screen, out floor, out named)) continue;
                            prop = candidate; beside = focus.Room;
                            break;
                        }
                        if (prop != null) break;
                    }
                    if (prop != null) break;
                    tried.Add(candidate.ToString());
                }
                Assert.That(prop, Is.Not.Null, "A free move beside a clickable prop with the prop under a clear pointer, among " + string.Join(", ", tried));
                Assert.That(HouseFurniture.TryClick(named, out _, out var caption), Is.True);
                int propBit = 1 << (RoomLayers.First + prop.Room);

                // The whole house, nothing held: the hover says what a click does.
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null; yield return null;
                var tip = director.GetComponentsInChildren<RectTransform>(true).LastOrDefault(rect => rect.name == EpisodeTravelBeacons.FurnitureTipName);
                Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "Nothing held: the whole house.");
                Assert.That(heard, Is.SameAs(named), "Over " + prop + ", the hover names it.");
                Assert.That(tip != null && tip.gameObject.activeInHierarchy, Is.True, "and says what a click does.");
                Assert.That(tip.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo(caption), "In the click's own words.");
                // Nothing moving and every room drawn: the hover does not pick again.
                var view = camera.worldToCameraMatrix;
                int before = hovers;
                yield return null; yield return null;
                Assert.That(camera.worldToCameraMatrix == view, Is.True, "The camera is still.");
                Assert.That(hovers, Is.EqualTo(before), "Nothing changed under a still pointer over the whole house: no new pick.");

                // Held: the view is the room beside it, which leaves its room out. Nothing moves.
                before = hovers;
                director.HoldRoomScope(owner);
                yield return null; yield return null;
                Physics.SyncTransforms();
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(beside)), "The view is the " + RoomLayers.RoomIds[beside] + ".");
                Assert.That(camera.cullingMask & propBit, Is.Zero, "The main camera leaves the " + RoomLayers.RoomIds[prop.Room] + " out.");
                Assert.That(camera.worldToCameraMatrix == view, Is.True, "The camera has not moved.");
                Assert.That(Vector2.Distance(mouse.position.ReadValue(), screen), Is.LessThan(.01f), "Nor has the pointer.");
                Assert.That(hovers - before, Is.EqualTo(1), "The hover picked again all the same, once: what the camera draws changed.");
                Assert.That(heard, Is.Null, "It passes over " + prop + ": no furniture under the pointer (D13).");
                Assert.That(tip.gameObject.activeInHierarchy, Is.False, "and the words are gone.");
                Assert.That(player.TryPointAt(HousePlayerController.ScreenRay(camera, screen), out var under), Is.True);
                Assert.That(under.collider, Is.SameAs(floor.collider), "The hover's pick is the floor behind it, where a click there walks: " + under.collider.name + ".");
                // Under a room view the hover looks again every frame, and says nothing new while nothing changes.
                before = hovers;
                yield return null; yield return null;
                Assert.That(hovers, Is.EqualTo(before), "Held, with nothing changing under the pointer: no new word.");

                // A piece of the prop gone to the shell (layer 0, where RS2a sends a placed piece that moves),
                // the mask as it was: drawn in every view, so the prop is under the pointer again.
                int heldMask = camera.cullingMask;
                shellPiece = prop.Root.GetComponentsInChildren<Renderer>().First(renderer => renderer.enabled);
                shellPieceLayer = shellPiece.gameObject.layer;
                before = hovers;
                shellPiece.gameObject.layer = 0;
                yield return null; yield return null;
                Assert.That(camera.cullingMask, Is.EqualTo(heldMask), "The mask has not changed.");
                Assert.That(camera.worldToCameraMatrix == view, Is.True, "Nor has the camera.");
                Assert.That(hovers - before, Is.EqualTo(1), "With a still pointer, a still camera and the same mask, the hover picked again once: "
                    + shellPiece.name + " is drawn in every view now.");
                Assert.That(heard, Is.SameAs(named), "and names " + prop + " again.");
                Assert.That(tip.gameObject.activeInHierarchy, Is.True, "The words are back.");
                // Back on its room's layer, the prop is hidden again.
                before = hovers;
                shellPiece.gameObject.layer = shellPieceLayer;
                shellPiece = null;
                yield return null; yield return null;
                Assert.That(hovers - before, Is.EqualTo(1), "Hidden again, the hover picked again once.");
                Assert.That(heard, Is.Null, "and names nothing.");
                Assert.That(tip.gameObject.activeInHierarchy, Is.False, "The words are gone.");

                // Released: the whole house, and the words come back where the pointer still is.
                before = hovers;
                director.Release(owner);
                yield return null; yield return null;
                Assert.That(director.ViewedRooms.IsWholeHouse, Is.True);
                Assert.That(camera.worldToCameraMatrix == view, Is.True, "The camera has not moved.");
                Assert.That(hovers - before, Is.EqualTo(1), "Drawn again, the hover picks again, once.");
                Assert.That(heard, Is.SameAs(named), "and names it.");
                Assert.That(tip.gameObject.activeInHierarchy, Is.True, "The words are back.");
                Assert.That(tip.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo(caption));
                before = hovers;
                yield return null; yield return null;
                Assert.That(hovers, Is.EqualTo(before), "The whole house again and nothing moving: no new pick.");
            }
            finally
            {
                if (shellPiece != null) shellPiece.gameObject.layer = shellPieceLayer;
                player.FurnitureHovered -= listen;
                director.Release(owner);
                cameraRig.SetReducedMotion(false);
                InputSystem.RemoveDevice(mouse);
            }
        }

        /// <summary>A clickable piece of furniture: an anchor a click on it acts on, its prop, the prop's collision proxy and the room whose layer its renderers stand on.</summary>
        private sealed class Rs2bProp
        {
            public HouseInteractionAnchor Anchor;
            public Transform Root;
            public Collider Proxy;
            public int Room;
            public override string ToString() => Root.name + " (" + Anchor.VenueId + ", the " + RoomLayers.RoomIds[Room] + ")";
        }

        /// <summary>
        /// The house's clickable furniture: every prop carrying an anchor a click acts on
        /// (<see cref="HouseFurniture.TryClick"/>) with a collision proxy of its own on the furniture
        /// layer, and every renderer it has drawing on one room's layer, where RS2a put it at load. One
        /// entry a prop.
        /// </summary>
        private List<Rs2bProp> Rs2bClickableProps()
        {
            var found = new List<Rs2bProp>();
            foreach (var anchor in HouseInteractionAnchors.InScene(director.gameObject.scene))
            {
                if (anchor == null || !HouseFurniture.TryClick(anchor, out _, out _)) continue;
                var root = anchor.transform.parent;
                if (root == null || found.Any(prop => prop.Root == root)) continue;
                var proxy = root.GetComponentsInChildren<Collider>().FirstOrDefault(collider => collider.enabled && !collider.isTrigger
                    && collider.transform.parent == root && collider.gameObject.layer == HouseLayers.Furniture);
                if (proxy == null) continue;
                var layers = root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled)
                    .Select(renderer => renderer.gameObject.layer).Distinct().ToArray();
                if (layers.Length != 1 || !RoomLayers.IsRoomLayer(layers[0])) continue;
                found.Add(new Rs2bProp { Anchor = anchor, Root = root, Proxy = proxy, Room = layers[0] - RoomLayers.First });
            }
            return found;
        }

        /// <summary>
        /// A click's ray that meets a prop's collision proxy first and the bare floor next: from above and
        /// behind, through the proxy just under its top, down to the floor a little past its edge, where the
        /// NavMesh is, so a click that passes over the prop walks rather than opening anything.
        /// </summary>
        private bool Rs2bRayThrough(Rs2bProp prop, out Ray ray, out RaycastHit floor)
        {
            ray = default;
            floor = default;
            var box = prop.Proxy.bounds;
            var inside = new Vector3(box.center.x, box.max.y - .05f, box.center.z);
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                var along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                foreach (float past in new[] { .8f, 1.3f })
                {
                    float reach = Mathf.Abs(along.x) * box.extents.x + Mathf.Abs(along.z) * box.extents.z + past;
                    var wanted = new Vector3(box.center.x, box.min.y, box.center.z) + along * reach;
                    if (!NavMesh.SamplePosition(wanted, out var walkable, .3f, player.Agent.areaMask)) continue;
                    var heading = (walkable.position - Vector3.up * .1f - inside).normalized;
                    var candidate = new Ray(inside - heading * 4f, heading);
                    if (!Rs2bFirstTwo(candidate, prop, out var first, out var next) || first.collider != prop.Proxy) continue;
                    if (next.collider.GetComponentInParent<HouseWalkable>() == null) continue;
                    // Where the NavMesh is, out of the prop's footprint: a click there walks to it.
                    if (!NavMesh.SamplePosition(next.point, out var landing, .3f, player.Agent.areaMask)
                        || FlatDistance(landing.position, next.point) > .3f) continue;
                    ray = candidate;
                    floor = next;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The first two things a ray meets that matter to a pick: its nearest hit, and the nearest after it
        /// that is not the prop's own. False when it meets fewer.
        /// </summary>
        private static bool Rs2bFirstTwo(Ray ray, Rs2bProp prop, out RaycastHit first, out RaycastHit next)
        {
            first = default;
            next = default;
            var hits = Physics.RaycastAll(ray, 500f, HouseLayers.Pick, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance).ToArray();
            if (hits.Length < 2) return false;
            first = hits[0];
            next = hits.Skip(1).FirstOrDefault(hit => !hit.collider.transform.IsChildOf(prop.Root));
            return next.collider != null;
        }

        /// <summary>
        /// Free-move foci in the rooms beside a prop: on each other floor within five metres of it, the
        /// floor's point nearest the prop, three quarters of a metre and two metres in towards its middle.
        /// </summary>
        private static IEnumerable<(Vector3 Point, int Room)> Rs2bFociBeside(HouseRoomScope scope, Rs2bProp prop)
        {
            var at = prop.Proxy.bounds.center;
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                if (room == prop.Room || !scope.Has(room)) continue;
                var box = scope.Box(room);
                if (HouseWhoIsWhere.OutsideBy(box, at) > 5f) continue;
                var nearest = new Vector2(Mathf.Clamp(at.x, box.xMin, box.xMax), Mathf.Clamp(at.z, box.yMin, box.yMax));
                var inward = box.center - nearest;
                if (inward.sqrMagnitude < 1e-4f) continue;
                inward.Normalize();
                foreach (float depth in new[] { .75f, 2f })
                {
                    var point = nearest + inward * depth;
                    yield return (new Vector3(point.x, 0f, point.y), room);
                }
            }
        }

        /// <summary>
        /// A screen point over a prop where the HUD takes nothing, well inside the frame: its collision proxy
        /// the first thing the pointer's ray meets and the bare floor the next, and the pick there naming
        /// one of the prop's anchors, or one a click on it opens.
        /// </summary>
        private bool Rs2bPointAt(Camera camera, Rs2bProp prop, out Vector2 screen, out RaycastHit floor, out HouseInteractionAnchor anchor)
        {
            screen = default;
            floor = default;
            anchor = null;
            var box = prop.Proxy.bounds;
            var frame = camera.pixelRect;
            foreach (var offset in new[] { Vector2.zero, new Vector2(.4f, 0f), new Vector2(-.4f, 0f), new Vector2(0f, .4f), new Vector2(0f, -.4f) })
            {
                var target = new Vector3(box.center.x + offset.x * box.extents.x, box.max.y - .05f, box.center.z + offset.y * box.extents.z);
                var seen = camera.WorldToScreenPoint(target);
                if (seen.z <= 0f) continue;
                var point = new Vector2(seen.x, seen.y);
                if (point.x < frame.xMin + 40f || point.x > frame.xMax - 40f || point.y < frame.yMin + 40f || point.y > frame.yMax - 40f) continue;
                if (EventSystem.current != null)
                {
                    var ui = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, ui);
                    if (ui.Count > 0) continue;
                }
                var ray = HousePlayerController.ScreenRay(camera, point);
                if (!Rs2bFirstTwo(ray, prop, out var first, out var next) || first.collider != prop.Proxy) continue;
                if (next.collider.GetComponentInParent<HouseWalkable>() == null) continue;
                var named = HouseFurniture.AtProp(director.gameObject.scene, first.transform, first.point);
                if (named == null) continue;
                screen = point;
                floor = next;
                anchor = named;
                return true;
            }
            return false;
        }
    }
}
