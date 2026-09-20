using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    public enum HouseFurnitureActivity { PrepareSnack, SitAtTable, Rest }

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

        public static HouseInteractionAnchor AtProp(Scene scene,Transform clicked)
            => InScene(scene).FirstOrDefault(anchor=>anchor.transform.parent!=null && clicked.IsChildOf(anchor.transform.parent));

        /// <summary>Explicit authoring repair of the old default, preserving custom approach edits.</summary>
        public static int AuthorLoungerApproaches(Scene scene)
        {
            int repaired=0;
            foreach(var anchor in HouseInteractionAnchors.InScene(scene))
            {
                if(anchor.VenueId!="yard-lounger-chat" || anchor.RoomId!="Yard" || !anchor.Seated)continue;
                if((anchor.Approach-anchor.transform.TransformPoint(Vector3.back*.55f)).sqrMagnitude>.000001f)continue;
                // The authored chairs are at z=11. The old z=10.45 approach touches the floor's
                // strict .45m safe inset and overlaps the wall ending at z=10.125 for a .35m body.
                anchor.Configure(anchor.VenueId,anchor.RoomId,anchor.Slot,true,Vector3.back*.40f);
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
