using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>What a houseguest does at a piece of furniture. Appended to, never reordered.</summary>
    public enum HouseFurnitureActivity { PrepareSnack, SitAtTable, Rest, Sleep, Swim, Soak, Cook, Dance }

    /// <summary>Which command a click on a prop is a second route to.</summary>
    public enum HousePropClick { None, Activity, Diary, Station }

    /// <summary>Only authored, real props enter this catalog. No runtime placeholder furniture.</summary>
    public static class HouseFurniture
    {
        public const string KitchenAnchor="kitchen-counter-activity";
        public const string StoveAnchor="kitchen-stove-activity";
        public const string BedPrefix="bed-";
        public const string PoolAnchor="pool-swim";
        public const string HotTubAnchor="hot-tub-activity";
        public const string DanceAnchor="living-dance-activity";
        public const string LoungeAnchor="living-lounge-rest";
        public const string LoungerAnchor="yard-lounger-chat";

        public const string SleepCaption="Lie down", SwimCaption="Take a swim", SoakCaption="Soak in the hot tub",
            CookCaption="Cook a meal", DanceCaption="Dance to the music";

        public static IEnumerable<HouseInteractionAnchor> InScene(Scene scene)
            => HouseInteractionAnchors.InScene(scene).Where(anchor=>anchor.isActiveAndEnabled && TryDescribe(anchor,out _,out _));

        /// <summary>
        /// The places the houseguests' own routine may take them between conversations - the
        /// counter, the dining chairs and the loungers, and nothing the player's list has grown
        /// since. Kept apart from <see cref="TryDescribe"/> on purpose: adding a bed to what the
        /// player can do must not quietly send the cast to bed.
        /// </summary>
        public static bool Ambient(HouseInteractionAnchor anchor)
            => TryDescribe(anchor,out var activity,out _) && (activity==HouseFurnitureActivity.PrepareSnack
                || activity==HouseFurnitureActivity.SitAtTable || activity==HouseFurnitureActivity.Rest);

        /// <summary>
        /// Whether a click on the prop starts this at once, with no menu between the click and the
        /// walk. The newer verbs do: one prop, one thing to do there. The first three keep the
        /// House Activities menu they have always opened.
        /// </summary>
        public static bool StartsOnClick(HouseFurnitureActivity activity)
            => activity==HouseFurnitureActivity.Sleep || activity==HouseFurnitureActivity.Swim || activity==HouseFurnitureActivity.Soak
               || activity==HouseFurnitureActivity.Cook || activity==HouseFurnitureActivity.Dance;

        /// <summary>
        /// Whether this lasts until the player moves rather than for a set time: nobody wants to be
        /// woken on a timer, or to be got out of the pool.
        /// </summary>
        public static bool UntilMoved(HouseFurnitureActivity activity)
            => activity==HouseFurnitureActivity.Sleep || activity==HouseFurnitureActivity.Swim
               || activity==HouseFurnitureActivity.Soak || activity==HouseFurnitureActivity.Dance;

        /// <summary>
        /// Whether a venue's places are for company: two people at once, each on their own lease.
        /// The hot tub's two seats are; a conversation venue's two chairs are one meeting's.
        /// </summary>
        public static bool SeatsCompany(string venueId) => venueId==HotTubAnchor || venueId==LoungeAnchor || venueId==LoungerAnchor;

        /// <summary>Furniture shared cushion by cushion, also measured against overlapping chat places.</summary>
        public static bool IndependentRest(HouseInteractionAnchor anchor)
            => anchor!=null && (anchor.VenueId==LoungeAnchor || anchor.VenueId==LoungerAnchor);

        /// <summary>The E prompt, and the status line, while the player is doing this.</summary>
        public static string StopPrompt(HouseFurnitureActivity activity)
        {
            switch(activity)
            {
                case HouseFurnitureActivity.Sleep:return "E  ·  Get up";
                case HouseFurnitureActivity.Swim:return "E  ·  Get out of the pool";
                case HouseFurnitureActivity.Soak:return "E  ·  Get out of the hot tub";
                case HouseFurnitureActivity.Cook:return "E  ·  Stop cooking";
                case HouseFurnitureActivity.Dance:return "E  ·  Stop dancing";
                default:return "E  ·  Finish activity";
            }
        }

        public static bool TryDescribe(HouseInteractionAnchor anchor,out HouseFurnitureActivity activity,out string caption)
        {
            activity=default;caption=null;
            if(anchor==null)return false;
            switch(anchor.VenueId)
            {
                case KitchenAnchor:activity=HouseFurnitureActivity.PrepareSnack;caption="Prepare a snack";return true;
                case "kitchen-table-chat":activity=HouseFurnitureActivity.SitAtTable;caption="Sit at the dining table";return anchor.Seated;
                case LoungerAnchor:activity=HouseFurnitureActivity.Rest;caption="Rest on a lounger";return anchor.Seated;
                case LoungeAnchor:activity=HouseFurnitureActivity.Rest;caption="Rest on a couch";return anchor.Seated;
                case StoveAnchor:activity=HouseFurnitureActivity.Cook;caption=CookCaption;return true;
                case PoolAnchor:activity=HouseFurnitureActivity.Swim;caption=SwimCaption;return anchor.Pose==HouseAnchorPose.Float;
                case HotTubAnchor:activity=HouseFurnitureActivity.Soak;caption=SoakCaption;return anchor.Seated;
                case DanceAnchor:activity=HouseFurnitureActivity.Dance;caption=DanceCaption;return true;
                default:
                    if(anchor.VenueId!=null && anchor.VenueId.StartsWith(BedPrefix,System.StringComparison.Ordinal)
                        && anchor.Pose==HouseAnchorPose.Lie)
                    {activity=HouseFurnitureActivity.Sleep;caption=SleepCaption;return true;}
                    return false;
            }
        }

        /// <summary>
        /// What clicking this prop should do.
        ///
        /// <para>Deliberately a wider list than <see cref="TryDescribe"/>, and deliberately a
        /// separate one. <see cref="TryDescribe"/> is the catalogue of places a houseguest can be
        /// posed at, and it drives the ambient routine that walks cooling-down NPCs to the kitchen
        /// counter and the loungers - widening <em>that</em> would quietly change where the cast
        /// spends its idle time. Clicking is a different question, and the diary chair is the case
        /// that proves it: nobody idles there, and it is the one prop in the house every player
        /// tries to click.</para>
        ///
        /// <para>A click is always a second route to a command that already has a caption. Clicking
        /// the diary chair does what the Diary shortcut does - walks you there and says so. It does
        /// not open the diary, because entering still requires physically reaching the room.</para>
        /// </summary>
        public static bool TryClick(HouseInteractionAnchor anchor,out HousePropClick what,out string caption)
        {
            what=HousePropClick.None;caption=null;
            if(anchor==null || !anchor.isActiveAndEnabled)return false;
            if(TryDescribe(anchor,out _,out caption)){what=HousePropClick.Activity;return true;}
            if(anchor.VenueId==HouseInteractionAnchors.DiaryVenue)
            {what=HousePropClick.Diary;caption="Go to the diary room";return true;}
            // The episode screen was left out of this list once, on the grounds that it is a
            // staging mark the director walks the cast to rather than a thing you operate. That was
            // wrong in a way only playing shows: the proximity prompt ranks any nearby houseguest
            // above the screen, so somebody idling beside it took the only other route as well and
            // the screen could not be opened at all. The caption is the one the HUD button already
            // uses, and it runs the same command.
            if(anchor.VenueId==HouseInteractionAnchors.EpisodeDestination)
            {what=HousePropClick.Station;caption="Go to episode screen";return true;}
            return false;
        }

        public static HouseInteractionAnchor AtProp(Scene scene,Transform clicked)
            => HouseInteractionAnchors.InScene(scene).FirstOrDefault(anchor=>
                TryClick(anchor,out _,out _) && anchor.transform.parent!=null && clicked.IsChildOf(anchor.transform.parent));

        /// <summary>
        /// The anchor on the clicked prop nearest where it was hit. One prop can carry several -
        /// the kitchen run has the counter and the stove, the hot tub two seats - and a click on
        /// the hob means the stove, not whichever anchor the hierarchy lists first.
        /// </summary>
        public static HouseInteractionAnchor AtProp(Scene scene,Transform clicked,Vector3 point)
            => HouseInteractionAnchors.InScene(scene).Where(anchor=>
                    TryClick(anchor,out _,out _) && anchor.transform.parent!=null && clicked.IsChildOf(anchor.transform.parent))
                .OrderBy(anchor=>Flat(anchor.Position-point)).FirstOrDefault();

        private static float Flat(Vector3 offset){offset.y=0;return offset.sqrMagnitude;}

        /// <summary>Explicit authoring repair of the old default, preserving custom approach edits.</summary>
        public static int AuthorLoungerApproaches(Scene scene)
        {
            int repaired=0;
            foreach(var anchor in HouseInteractionAnchors.InScene(scene))
            {
                if(anchor.VenueId!="yard-lounger-chat" || anchor.RoomId!="Yard" || !anchor.Seated)continue;
                // Two historical offsets to repair, not one: the original .55 back, and the .40 back
                // that replaced it. Both point along the lounger's 1.90 m LENGTH, so both stand
                // inside the prop - the .40 one measures 0.00 m of clearance from the lounger's own
                // footprint. A bake erodes by the agent radius and would take the floor out from
                // under whoever was walking to it.
                if((anchor.Approach-anchor.transform.TransformPoint(Vector3.back*.55f)).sqrMagnitude>.000001f
                    && (anchor.Approach-anchor.transform.TransformPoint(Vector3.back*.40f)).sqrMagnitude>.000001f)continue;
                // Sideways: 0.95 m clears the 0.65 m width with room for the agent, where clearing
                // the length would be a 1.60 m hike to sit down.
                anchor.Configure(anchor.VenueId,anchor.RoomId,anchor.Slot,true,Vector3.left*.95f);
                repaired++;
            }
            return repaired;
        }

        /// <summary>Called solely by explicit scene authoring, never by runtime discovery.</summary>
        public static string AuthorKitchenAnchor(Scene scene)
        {
            if(HouseInteractionAnchors.TryFind(scene,KitchenAnchor,0,out _))return null;
            if(HouseInteractionAnchors.InScene(scene).Any(anchor=>anchor.VenueId==KitchenAnchor))
                return "The existing kitchen activity anchor is duplicated or disabled; repair it without replacing its identity.";
            var all=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).ToArray();
            var counters=all.Where(item=>item.name=="bb_set_kitchenrun").ToArray();
            var floor=all.FirstOrDefault(item=>item.name=="Kitchen floor")?.GetComponent<BoxCollider>();
            if(counters.Length!=1 || floor==null)return "Kitchen activity requires one authored bb_set_kitchenrun and Kitchen floor; no substitute was created.";
            var renderers=counters[0].GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)return "The kitchen counter has no visible geometry.";
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            var approach=new Vector3(bounds.center.x,floor.bounds.max.y,bounds.min.z-.8f);
            HouseInteractionAnchor.Create(counters[0],KitchenAnchor,"Kitchen",0,approach,0,false);
            return null;
        }
    }
}
