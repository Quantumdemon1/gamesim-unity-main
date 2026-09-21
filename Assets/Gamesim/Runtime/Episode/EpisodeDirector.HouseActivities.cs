using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private bool houseActivitiesOpen;
        private HouseInteractionAnchor selectedFurniture;
        private HouseActivityLease playerActivity;
        private HousePlayerController furnitureInput;
        private float nextAmbientActivity;
        private int ambientActivityIndex;
        public bool IsHouseActivityOpen => houseActivitiesOpen;
        public bool IsPlayerHouseActivityActive => playerActivity!=null && !playerActivity.Released;

        public void OpenHouseActivities()
        {
            if(!IsReady || blockedRecovery || !playerIsActive || challengeActive)return;
            PauseNpcSocialForPanel();ClosePanels();houseActivitiesOpen=true;
            player.SetInputEnabled(false);Render();
        }

        private void SelectHouseFurniture(HouseInteractionAnchor anchor)
        {
            if(anchor==null || anchor.gameObject.scene!=gameObject.scene)return;
            if(!HouseFurniture.TryClick(anchor,out var what,out _))return;
            // The world click is a second route to a command that already has a caption, never a
            // replacement for one. Clicking the diary chair walks you there exactly as the Diary
            // shortcut does; it does not open the diary, because entering still means arriving.
            if(what==HousePropClick.Diary){GoToDiary();return;}
            if(what==HousePropClick.Station){GoToStation();return;}
            OpenHouseActivities();
            if(houseActivitiesOpen){selectedFurniture=anchor;Render();}
        }

        private void RenderHouseActivities()
        {
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Diary);
            hud.PanelTitle("HOUSE ACTIVITIES","Use the house between decisions. These activities do not spend actions or change needs, relationships or competition bonuses.");
            if(!NpcSocialState.IsEligible(projected))
            {hud.Paragraph("House activities are available during free time and campaigning.");return;}
            if(IsPlayerHouseActivityActive)
            {
                HouseFurniture.TryDescribe(playerActivity.Anchor,out _,out var caption);
                bool reached=npcMeetings!=null && npcMeetings.ActivityArrived(playerActivity);
                hud.Paragraph((reached ? "At the furniture: " : "Walking to the furniture: ")+caption);
                hud.Action("Finish activity",FinishPlayerHouseActivity);return;
            }
            var anchors=HouseFurniture.InScene(gameObject.scene).OrderBy(anchor=>anchor==selectedFurniture?0:1)
                .ThenBy(anchor=>(anchor.Approach-player.transform.position).sqrMagnitude).ToArray();
            if(anchors.Length==0){hud.Paragraph("This house has no available authored activity places.");return;}
            foreach(var anchor in anchors)
            {
                HouseFurniture.TryDescribe(anchor,out _,out var caption);
                bool available=npcMeetings!=null && npcMeetings.ActivityAnchorAvailable(anchor);
                hud.Action(caption+" · "+anchor.RoomId+" "+(anchor.Slot+1),()=>StartPlayerHouseActivity(anchor)).interactable=available;
                if(!available)hud.Paragraph("This place is occupied or house movement is unavailable.");
            }
        }

        private void StartPlayerHouseActivity(HouseInteractionAnchor anchor)
        {
            if(!houseActivitiesOpen || blockedRecovery || npcMeetings==null || IsPlayerHouseActivityActive
                || !HouseFurniture.TryDescribe(anchor,out var kind,out _))return;
            if(!npcMeetings.TryReservePlayerActivity(player,anchor,out var lease,out var reason))
            {message=reason;Render();return;}
            playerActivity=lease;
            BeginFurniturePose(player.gameObject,lease,kind,18f);
            Render();
        }

        private void BeginFurniturePose(GameObject actor,HouseActivityLease lease,HouseFurnitureActivity kind,float seconds)
        {
            var owner=npcMeetings;
            var pose=actor.GetComponent<HouseFurniturePose>() ?? actor.AddComponent<HouseFurniturePose>();
            pose.Begin(lease.Anchor,kind,seconds,()=>owner!=null && owner.ActivityValid(lease),
                ()=>owner!=null && owner.ActivityArrived(lease),()=>
                {
                    owner?.ReleaseActivity(lease);
                    if(ReferenceEquals(playerActivity,lease))playerActivity=null;
                    if(this!=null && isActiveAndEnabled && IsReady && houseActivitiesOpen)Render();
                },()=>owner!=null && owner.ActivityPaused(lease));
            if(!pose.Active || !owner.RegisterActivityPresentation(lease,pose)){owner.ReleaseActivity(lease);if(ReferenceEquals(playerActivity,lease))playerActivity=null;}
        }

        public void FinishPlayerHouseActivity()
        {
            var pose=player!=null?player.GetComponent<HouseFurniturePose>():null;
            if(pose!=null && pose.Active)pose.RequestFinish();
            else if(playerActivity!=null){npcMeetings?.ReleaseActivity(playerActivity);playerActivity=null;}
        }

        private void CloseHouseActivities(bool immediately=false)
        {
            houseActivitiesOpen=false;selectedFurniture=null;
            if(immediately)
            {if(playerActivity!=null)npcMeetings?.ReleaseActivity(playerActivity);playerActivity=null;}
            else FinishPlayerHouseActivity();
        }

        private void TickHouseActivities()
        {
            if(furnitureInput!=player)
            {
                if(furnitureInput!=null)furnitureInput.FurnitureSelected-=SelectHouseFurniture;
                furnitureInput=player;if(furnitureInput!=null)furnitureInput.FurnitureSelected+=SelectHouseFurniture;
            }
            if(npcMeetings==null)return;
            if(!NpcSocialState.IsEligible(projected))
            {npcMeetings.ReleaseActivities();return;}
            if(playerActivity!=null && !npcMeetings.ActivityValid(playerActivity))
            {npcMeetings.ReleaseActivity(playerActivity);playerActivity=null;if(houseActivitiesOpen)Render();}
            if(!NpcCanAdvance || Time.unscaledTime<nextAmbientActivity)return;
            nextAmbientActivity=Time.unscaledTime+12f;
            // Only actors already cooling down from conversations enter optional visual routines.
            // No saved deadline, random draw, topic, relationship or need is changed by this choice.
            var cooling=projected.npcSocial.cooldowns.Where(row=>row.untilTick>projected.npcSocial.clockTick+2)
                .Select(row=>row.npcId).OrderBy(id=>id).ToArray();
            var places=HouseFurniture.InScene(gameObject.scene).OrderBy(anchor=>anchor.VenueId).ThenBy(anchor=>anchor.Slot).ToArray();
            if(places.Length==0)return;
            foreach(var id in cooling)
            {
                if(npcMeetings.TryGetActivity(id,out _))continue;
                var npc=housemates.FirstOrDefault(actor=>actor!=null && actor.Id==id);
                if(npc==null)continue;
                for(int i=0;i<places.Length;i++)
                {
                    var anchor=places[(ambientActivityIndex+i)%places.Length];
                    if(Vector3.Distance(npc.transform.position,anchor.Approach)>8f)continue;
                    if(!npcMeetings.TryReserveActivity(id,anchor,out var lease,out _))continue;
                    HouseFurniture.TryDescribe(anchor,out var kind,out _);
                    BeginFurniturePose(npc.gameObject,lease,kind,8f);ambientActivityIndex=(ambientActivityIndex+i+1)%places.Length;
                    return;
                }
            }
        }

        private void DisposeHouseActivities()
        {
            CloseHouseActivities(true);npcMeetings?.ReleaseActivities();
            if(furnitureInput!=null)furnitureInput.FurnitureSelected-=SelectHouseFurniture;
            furnitureInput=null;nextAmbientActivity=0;ambientActivityIndex=0;
        }
    }
}
