using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Things to do in the house: lie down on a bed, swim, soak in the hot tub, cook at the stove,
    /// dance in the living room. Presentation only - none of it spends an action or moves a score -
    /// and nothing freezes while it happens: the house goes on, and a click or E gets up.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private HouseInteractionAnchor[] PlacesFor(HouseFurnitureActivity verb)
            => HouseFurniture.InScene(player.gameObject.scene)
                .Where(anchor => HouseFurniture.TryDescribe(anchor, out var kind, out _) && kind == verb).ToArray();

        /// <summary>Starts an activity from the house and waits for the body to be doing it.</summary>
        private IEnumerator BeginInHouse(HouseInteractionAnchor anchor, HouseFurnitureActivity verb)
        {
            director.ClosePanels();
            yield return null;
            WarpPlayer(OnFootFrom(anchor.Approach, 2f, 8f));
            player.Agent.speed = 20; player.Agent.acceleration = 100;
            // Reduced motion keeps the stove and the dance floor still, and this is about the motion.
            player.GetComponent<CharacterPresentation>().SetReducedMotion(false);
            director.StartActivityInHouse(anchor, verb);
            Assert.That(director.PlayerActivity, Is.EqualTo(verb), "The activity should have begun. Status: " + director.StatusMessage);
            var pose = player.GetComponent<HouseFurniturePose>();
            float deadline = Time.realtimeSinceStartup + 15f;
            var seat = player.GetComponent<HouseSeatPresentation>();
            while (Time.realtimeSinceStartup < deadline
                   && !(pose != null && pose.IsPerforming && (!anchor.Posed || (seat != null && seat.Settled))))
            {
                yield return null;
                seat = player.GetComponent<HouseSeatPresentation>();
            }
            Assert.That(pose != null && pose.IsPerforming, Is.True, verb + " never began at " + anchor.name + ".");
            // Long enough for the pose to fit and the animator to cross into the state: a third of
            // a second of cross-fade is a few hundred batchmode frames, so this waits on the clock.
            float settle = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < settle) yield return null;
        }

        private Animator PlayerAnimator() => player.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.isHuman && a.isActiveAndEnabled);

        [UnityTest]
        public IEnumerator HouseLife_EveryPlaceStandsOnTheFloorOfItsRoom()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(HouseRoomQuery.TryCreate(player.gameObject.scene, out var rooms, out var why), Is.True, why);
            var expected = new Dictionary<HouseFurnitureActivity, int>
            {
                [HouseFurnitureActivity.Sleep] = 8, [HouseFurnitureActivity.Swim] = 1, [HouseFurnitureActivity.Soak] = 2,
                [HouseFurnitureActivity.Cook] = 1, [HouseFurnitureActivity.Dance] = 1,
            };
            var failures = new List<string>();
            foreach (var entry in expected)
            {
                var places = PlacesFor(entry.Key);
                if (places.Length != entry.Value) failures.Add(entry.Key + ": " + places.Length + " places, expected " + entry.Value);
                foreach (var place in places)
                {
                    if (!rooms.TryLocate(place.Approach, player.Agent.radius, out var room)) failures.Add(place.name + ": approach off every floor");
                    else if (room != place.RoomId) failures.Add(place.name + ": approach in " + room + ", anchor says " + place.RoomId);
                    if (!player.TryMeasureRoute(place.Approach, out _)) failures.Add(place.name + ": no route to it");
                }
            }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            Assert.That(PlacesFor(HouseFurnitureActivity.Sleep).Count(bed => bed.RoomId == "Bedroom"), Is.EqualTo(5),
                "The three singles and the two lower bunks are the bedroom's.");
        }

        /// <summary>The houseguests' own routine never gains a bed, the pool or the stove.</summary>
        [UnityTest]
        public IEnumerator HouseLife_TheHouseguestsKeepToTheirOwnPlaces()
        {
            yield return null;
            var ambient = HouseFurniture.InScene(player.gameObject.scene).Where(HouseFurniture.Ambient)
                .Select(anchor => { HouseFurniture.TryDescribe(anchor, out var kind, out _); return kind; }).Distinct().ToArray();
            Assert.That(ambient, Is.SubsetOf(new[] { HouseFurnitureActivity.PrepareSnack, HouseFurnitureActivity.SitAtTable, HouseFurnitureActivity.Rest }));
            Assert.That(HouseFurniture.InScene(player.gameObject.scene).Any(anchor => !HouseFurniture.Ambient(anchor)), Is.True,
                "The player's list has places the cast's does not.");
        }

        [UnityTest]
        public IEnumerator HouseLife_LyingDownOnABedAndGettingUp()
        {
            // A single bed: a lower bunk sleeps under the top one, out of any shot from above.
            var bed = PlacesFor(HouseFurnitureActivity.Sleep).First(anchor => anchor.transform.parent.name == "bedSingle");
            var before = director.Snapshot;
            var fill = SceneComponents<Light>().Single(light => light.name == bed.RoomId + " fill");
            float lit = fill.intensity;
            yield return BeginInHouse(bed, HouseFurnitureActivity.Sleep);
            float dimming = Time.realtimeSinceStartup + EpisodeDirector.SleepDimSeconds + 1f;
            while (Time.realtimeSinceStartup < dimming && fill.intensity > lit * .5f) yield return null;
            Assert.That(fill.intensity, Is.LessThan(lit * .5f), "The room goes dim while the player sleeps in it.");
            var seat = player.GetComponent<HouseSeatPresentation>();
            Assert.That(seat.Mode, Is.EqualTo(HouseAnchorPose.Lie));
            Assert.That(player.GetComponent<CharacterPresentation>().Activity, Is.EqualTo(CharacterPresentation.BodyActivity.Sleeping));
            var animator = PlayerAnimator();
            Assert.That(animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Sleep"), Is.True, "The body plays the sleep take.");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.That(hips.position.y, Is.EqualTo(bed.SeatContact.y + .1f).Within(.2f), "The hips lie on the mattress.");
            var headboard = bed.Position + Quaternion.Euler(0, bed.Facing, 0) * Vector3.forward;
            Assert.That(Vector3.Distance(head.position, headboard), Is.LessThan(Vector3.Distance(hips.position, headboard)),
                "The head is at the headboard end.");
            Assert.That(director.IsPanelOpen, Is.False, "Nothing is frozen: no panel holds the house.");
            Assert.That(cameraRig.ControlsEnabled, Is.True, "The camera is still the player's.");
            if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-sleep"); }

            director.OpenJournal();
            yield return null;
            // Getting up takes most of a second, so "still asleep" a frame later proves nothing:
            // the question is whether the get-up began.
            Assert.That(seat.IsExiting, Is.False, "Opening the notebook does not wake the player.");
            director.ClosePanels();
            yield return null;
            Assert.That(seat.IsExiting, Is.False, "Nor does closing it.");
            float awake = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < awake) yield return null;
            Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Sleep), "They are still asleep a second later.");

            director.FinishPlayerHouseActivity();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsPlayerHouseActivityActive, Is.False, "Getting up ends it.");
            Assert.That(player.InputEnabled, Is.True, "And the player can move again.");
            float waking = Time.realtimeSinceStartup + EpisodeDirector.SleepDimSeconds + 1f;
            // Until the director has let the light go: it eases most of the way back and then puts
            // the light's own value back, so "nearly as it was" is a frame early.
            while (Time.realtimeSinceStartup < waking && director.DimmedForSleep != null) yield return null;
            Assert.That(director.DimmedForSleep, Is.Null, "The light is let go of once the player is up.");
            Assert.That(fill.intensity, Is.EqualTo(lit), "And the light comes back exactly as it was.");
            AssertPlayerSeasonUnchanged(before, director.Snapshot);
        }

        /// <summary>
        /// Asleep, a click on the floor is the plainest way to say "get up": the player gets up and
        /// the same click takes them there. It used to be swallowed - an activity owns the player's
        /// movement, and the controller read no clicks at all while anything did.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_AClickOnTheFloorGetsUpAndGoesThere()
        {
            var bed = PlacesFor(HouseFurnitureActivity.Sleep).First(anchor => anchor.transform.parent.name == "bedSingle");
            yield return BeginInHouse(bed, HouseFurnitureActivity.Sleep);
            // Hold the shot where it is, so the floor measured is the floor clicked.
            cameraRig.ClearSubject();
            yield return null;
            var rect = cameraRig.ViewCamera.pixelRect;
            Vector2 screen = default; Vector3 target = default; bool found = false;
            for (int ix = 2; ix < 14 && !found; ix++)
                for (int iy = 3; iy < 13 && !found; iy++)
                {
                    var point = new Vector2(rect.xMin + rect.width * ix / 16f, rect.yMin + rect.height * iy / 16f);
                    if (!IsBareFloorAt(point, out var hit)) continue;
                    if (Vector3.Distance(hit.point, player.transform.position) < 2.5f) continue;
                    if (!player.TryMeasureRoute(hit.point, out _)) continue;
                    screen = point; target = hit.point; found = true;
                }
            Assert.That(found, Is.True, "No bare, reachable floor in the shot to click.");

            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
                yield return null;
                yield return ClickAt(mouse, screen);
                float deadline = Time.realtimeSinceStartup + 4f;
                while (director.IsPlayerHouseActivityActive && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.IsPlayerHouseActivityActive, Is.False, "The click got the player up.");
                yield return null;
                Assert.That(player.Agent.hasPath, Is.True, "And set off for where it was clicked.");
                Assert.That(Vector3.Distance(player.Agent.destination, target), Is.LessThan(.8f), "The same click, not a second one.");
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator HouseLife_SwimmingLengthsWithTheRootAtTheSide()
        {
            var pool = PlacesFor(HouseFurnitureActivity.Swim).Single();
            yield return BeginInHouse(pool, HouseFurnitureActivity.Swim);
            var seat = player.GetComponent<HouseSeatPresentation>();
            Assert.That(seat.Mode, Is.EqualTo(HouseAnchorPose.Float));
            Assert.That(cameraRig.DesiredDistance, Is.LessThan(EpisodeTravelBeacons.HiddenBelow),
                "The camera comes in close enough to see the swimmer: from the far view they are a dot.");
            var root = player.transform.position;
            var animator = PlayerAnimator();
            var along = Quaternion.Euler(0, pool.Facing, 0) * Vector3.forward;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float first = Vector3.Dot(hips.position - pool.Position, along);
            bool swam = false, trod = false;
            float travelled = 0f;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && !(swam && trod && travelled > 1f))
            {
                yield return null;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("SwimForward")) swam = true;
                if (state.IsName("SwimIdle")) trod = true;
                travelled = Mathf.Max(travelled, Mathf.Abs(Vector3.Dot(hips.position - pool.Position, along) - first));
            }
            Assert.That(trod, Is.True, "The swimmer treads water between lengths.");
            Assert.That(swam, Is.True, "And swims them.");
            Assert.That(travelled, Is.GreaterThan(1f), "A length goes somewhere.");
            Assert.That(Vector3.Distance(player.transform.position, root), Is.LessThan(.05f), "Only the visual body swims; the root waits at the side.");
            Assert.That(Mathf.Abs(hips.position.y - pool.SeatContact.y), Is.LessThan(.7f), "The swimmer is in the water, not on the deck or under the yard.");
            if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-swim"); }
            director.FinishPlayerHouseActivity();
            yield return null;
        }

        /// <summary>
        /// Into swimwear for the pool and back out after, and the player is never missing while
        /// the new look is made: the old body stands until the new one is ready.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_ChangingForThePoolNeverLeavesThePlayerMissing()
        {
            var pool = PlacesFor(HouseFurnitureActivity.Swim).Single();
            var presentation = player.GetComponent<CharacterPresentation>();
            string everyday = presentation.AppearanceSnapshot?.activeOutfit;
            int missing = 0;
            // Seen, and seen as themselves: a stand-in in somebody else's clothes while UMA
            // rebuilds is the pop this is about, not a body.
            bool Visible()
            {
                var visual = presentation.VisualRoot;
                return visual != null && !presentation.IsBodyAssembling
                    && visual.gameObject.activeInHierarchy && visual.lossyScale.sqrMagnitude > .01f
                    && visual.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy);
            }
            director.ClosePanels();
            yield return null;
            WarpPlayer(OnFootFrom(pool.Approach, 2f, 8f));
            player.GetComponent<CharacterPresentation>().SetReducedMotion(false);
            director.StartActivityInHouse(pool, HouseFurnitureActivity.Swim);
            Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Swim), director.StatusMessage);
            float deadline = Time.realtimeSinceStartup + 20f;
            // Whether the new body takes over in the old one's state is asked where the state holds
            // still (HouseLife_AChangeOfClothesKeepsTheBodyWhereItIs), not on a walk, where Idle
            // and Walk come and go on their own between one frame and the next.
            while (Time.realtimeSinceStartup < deadline && presentation.AppearanceSnapshot?.activeOutfit != CharacterOutfits.Swimwear)
            {
                if (!Visible()) missing++;
                yield return null;
            }
            Assert.That(presentation.AppearanceSnapshot?.activeOutfit, Is.EqualTo(CharacterOutfits.Swimwear),
                "The player changes into swimwear for the pool.");
            Assert.That(missing, Is.Zero, "The player went missing for " + missing + " frames while changing.");
            if (Application.isBatchMode)
            {
                float settle = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < settle) yield return null;
                cameraRig.FocusSubject(player.transform);
                yield return CaptureFraming("house-life-swimwear");
            }

            director.FinishPlayerHouseActivity();
            deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && presentation.AppearanceSnapshot?.activeOutfit == CharacterOutfits.Swimwear)
            {
                if (!Visible()) missing++;
                yield return null;
            }
            Assert.That(presentation.AppearanceSnapshot?.activeOutfit, Is.Not.EqualTo(CharacterOutfits.Swimwear), "And back out of it after.");
            Assert.That(missing, Is.Zero, "The player went missing for " + missing + " frames while changing back.");
            if (everyday != null) Assert.That(presentation.AppearanceSnapshot.activeOutfit, Is.EqualTo(everyday));
        }

        /// <summary>
        /// A change of clothes while seated keeps the body seated: the new body takes over in the
        /// state and at the moment the old one was in, rather than standing up out of Idle for a
        /// frame and sitting back down. Asked with the player settled in the hot tub, where the
        /// state is known, rather than on a walk, where Idle and Walk come and go on their own.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_AChangeOfClothesKeepsTheBodyWhereItIs()
        {
            var seats = PlacesFor(HouseFurnitureActivity.Soak);
            yield return BeginInHouse(seats[0], HouseFurnitureActivity.Soak);
            var presentation = player.GetComponent<CharacterPresentation>();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && presentation.IsChangingOutfit) yield return null;
            Assert.That(PlayerAnimator().GetCurrentAnimatorStateInfo(0).IsName("SitIdle"), Is.True, "Settled in the tub.");
            // Sat a while, so a body that sat down by itself behind the old one would be seconds
            // behind it in the loop, not level with it by chance.
            float sit = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < sit) yield return null;

            // The notebook open over the tub holds the house still: without it, the house's next
            // commit re-dresses the player for the tub and cancels the test's change half made.
            // The player stays in the water under it.
            director.OpenJournal();
            yield return null;
            Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Soak), "Still in the tub under the notebook.");

            // Back into the day's clothes, from the test, while they sit.
            var shownBody = presentation.VisualRoot;
            var state = director.Snapshot;
            CharacterPresentation.Dress(player.gameObject, CharacterOutfits.ForPhase(state.Find(state.playerId), state.phase), Color.white);
            Assert.That(presentation.IsChangingOutfit, Is.True, "A new look is being made behind the body.");
            float lastShown = 0f;
            deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && presentation.IsChangingOutfit)
            {
                lastShown = PlayerAnimator().GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return null;
            }
            Assert.That(presentation.IsChangingOutfit, Is.False, "It was made.");
            Assert.That(presentation.VisualRoot, Is.Not.SameAs(shownBody), "The new body took over.");
            var taken = PlayerAnimator().GetCurrentAnimatorStateInfo(0);
            Assert.That(taken.IsName("SitIdle"), Is.True, "The new body sits, from the frame it takes over.");
            Assert.That(PlayerAnimator().IsInTransition(0), Is.False, "Hearing the old body's cues, it is not on its way up.");
            Assert.That(taken.normalizedTime, Is.EqualTo(lastShown).Within(.05f),
                "And at the moment in the loop the old one had reached.");
            director.ClosePanels();
            director.FinishPlayerHouseActivity();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HouseLife_SoakingInTheHotTub()
        {
            var seats = PlacesFor(HouseFurnitureActivity.Soak);
            Assert.That(seats.Length, Is.EqualTo(2), "The hot tub seats two.");
            yield return BeginInHouse(seats[0], HouseFurnitureActivity.Soak);
            var animator = PlayerAnimator();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("SitIdle"), Is.True, "Soaking is sitting, in the water.");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(hips.position.y, Is.EqualTo(HouseActivityAnchors.TubSeat).Within(.25f), "On the tub's seat ring.");
            if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-soak"); }
            director.FinishPlayerHouseActivity();
            yield return null;
        }

        /// <summary>A click on the hob is the stove's; a click on the middle of the run is still the counter's.</summary>
        [UnityTest]
        public IEnumerator HouseLife_CookingAtTheStove()
        {
            var stove = PlacesFor(HouseFurnitureActivity.Cook).Single();
            Assert.That(HouseInteractionAnchors.TryFind(player.gameObject.scene, HouseFurniture.KitchenAnchor, 0, out var counter), Is.True);
            var run = stove.transform.parent;
            var hob = stove.Position + Quaternion.Euler(0, stove.Facing, 0) * Vector3.forward * 1.2f;
            Assert.That(HouseFurniture.AtProp(player.gameObject.scene, run, hob), Is.SameAs(stove), "A click on the hob cooks.");
            var middle = counter.Position + Quaternion.Euler(0, counter.Facing, 0) * Vector3.forward * 1.2f;
            Assert.That(HouseFurniture.AtProp(player.gameObject.scene, run, middle), Is.SameAs(counter), "A click by the sink still makes a snack.");

            var before = director.Snapshot;
            yield return BeginInHouse(stove, HouseFurnitureActivity.Cook);
            var animator = PlayerAnimator();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Cook"), Is.True, "The body works at the stove.");
            if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-cook"); }
            director.FinishPlayerHouseActivity();
            yield return null;
            Assert.That(director.IsPlayerHouseActivityActive, Is.False);
            AssertPlayerSeasonUnchanged(before, director.Snapshot);
        }

        /// <summary>Dancing in the middle of the living room, from the House Activities menu's own row.</summary>
        [UnityTest]
        public IEnumerator HouseLife_DancingInTheLivingRoom()
        {
            var floor = PlacesFor(HouseFurnitureActivity.Dance).Single();
            Assert.That(floor.RoomId, Is.EqualTo("Living"));
            director.OpenHouseActivities();
            yield return null;
            Assert.That(FindButton(HouseFurniture.DanceCaption), Is.Not.Null, "The menu has one row for it.");
            yield return BeginInHouse(floor, HouseFurnitureActivity.Dance);
            var animator = PlayerAnimator();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dance"), Is.True, "The body dances.");
            Assert.That(director.IsPanelOpen, Is.False, "Begun, it runs in the house, not behind the menu.");
            if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-dance"); }
            director.FinishPlayerHouseActivity();
            yield return null;
            Assert.That(director.IsPlayerHouseActivityActive, Is.False);
        }

        /// <summary>The HoH suite's bed is the Head of Household's, and nobody else lies down on it.</summary>
        [UnityTest]
        public IEnumerator HouseLife_TheHoHBedIsTheHeadOfHouseholds()
        {
            yield return null;
            var hohBed = PlacesFor(HouseFurnitureActivity.Sleep).SingleOrDefault(anchor => anchor.RoomId == "HoH");
            Assert.That(hohBed, Is.Not.Null, "The HoH suite has its bed.");
            Assume.That(director.Snapshot.hohId, Is.Not.EqualTo(director.Snapshot.playerId), "This fixture's player is not HoH.");
            director.StartActivityInHouse(hohBed, HouseFurnitureActivity.Sleep);
            Assert.That(director.PlayerActivity, Is.Null);
            Assert.That(director.StatusMessage, Does.Contain("Head of Household"));
        }

        /// <summary>
        /// Nothing about an activity reaches the season: the player's scores with everybody, their
        /// actions and preparation, the week and the phase. The houseguests' own clock goes on
        /// meanwhile - nothing is frozen - so their business with each other is not compared.
        /// </summary>
        private static void AssertPlayerSeasonUnchanged(Gamesim.Simulation.EpisodeState before, Gamesim.Simulation.EpisodeState after)
        {
            Assert.That(after.phase, Is.EqualTo(before.phase));
            Assert.That(after.week, Is.EqualTo(before.week));
            Assert.That(after.playerStudyBonus, Is.EqualTo(before.playerStudyBonus));
            Assert.That(Gamesim.Simulation.EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(Gamesim.Simulation.EpisodeEngine.SocialActionsSpent(before)));
            foreach (var other in before.contestants.Where(c => c.id != before.playerId))
                Assert.That(after.Score(after.playerId, other.id), Is.EqualTo(before.Score(before.playerId, other.id)), other.id);
        }
    }
}
