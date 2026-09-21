using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    public enum HouseFurnitureActivity { PrepareSnack, SitAtTable, Rest }

    /// <summary>Which command a click on a prop is a second route to.</summary>
    public enum HousePropClick { None, Activity, Diary }

    /// <summary>Only authored, real props enter this catalog. No runtime placeholder furniture.</summary>
    public static class HouseFurniture
    {
        public const string KitchenAnchor="kitchen-counter-activity";
        public static IEnumerable<HouseInteractionAnchor> InScene(Scene scene)
            => HouseInteractionAnchors.InScene(scene).Where(anchor=>anchor.isActiveAndEnabled && TryDescribe(anchor,out _,out _));

        public static bool TryDescribe(HouseInteractionAnchor anchor,out HouseFurnitureActivity activity,out string caption)
        {
            activity=default;caption=null;
            if(anchor==null)return false;
            switch(anchor.VenueId)
            {
                case KitchenAnchor:activity=HouseFurnitureActivity.PrepareSnack;caption="Prepare a snack";return true;
                case "kitchen-table-chat":activity=HouseFurnitureActivity.SitAtTable;caption="Sit at the dining table";return anchor.Seated;
                case "yard-lounger-chat":activity=HouseFurnitureActivity.Rest;caption="Rest on a lounger";return anchor.Seated;
                default:return false;
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
            return false;
        }

        public static HouseInteractionAnchor AtProp(Scene scene,Transform clicked)
            => HouseInteractionAnchors.InScene(scene).FirstOrDefault(anchor=>
                TryClick(anchor,out _,out _) && anchor.transform.parent!=null && clicked.IsChildOf(anchor.transform.parent));

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
