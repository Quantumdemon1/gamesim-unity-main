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

        /// <summary>
        /// Whether the player's activity was begun from the house itself - a click on the bed, the
        /// pool, the stove - rather than from the House Activities menu. Those run in free roam:
        /// nothing is frozen, the camera is the player's, the notebook opens over them, and a click
        /// anywhere in the house or E is how they end.
        /// </summary>
        private bool playerActivityInHouse;
        private HouseFurnitureActivity playerActivityKind;
        private Ray? clickAfterActivity;
        private Vector2 clickAfterActivityScreen;

        /// <summary>What the player is doing at a piece of furniture, while they are.</summary>
        public HouseFurnitureActivity? PlayerActivity => IsPlayerHouseActivityActive ? playerActivityKind : (HouseFurnitureActivity?)null;

        private static readonly Color PlayerPalette = new Color(0.4f, 0.88f, 0.76f);

        /// <summary>The set of clothes an activity calls for: swimwear in the water, nightwear in bed.</summary>
        private static string ActivityWardrobe(HouseFurnitureActivity? kind)
            => kind == HouseFurnitureActivity.Swim || kind == HouseFurnitureActivity.Soak ? CharacterOutfits.Swimwear
                : kind == HouseFurnitureActivity.Sleep ? CharacterOutfits.Sleepwear : null;

        /// <summary>
        /// The player as they should look now: the phase's clothes, or the activity's. A look saved
        /// before outfits existed names only a preset, with nothing to take off; the body provider
        /// fills in what that preset wears first, on a copy, so the activity has something to strip.
        /// </summary>
        private ContestantState PlayerDressed(EpisodeState state)
        {
            var me = state.Find(state.playerId);
            var pose = player != null ? player.GetComponent<HouseFurniturePose>() : null;
            string context = pose != null && pose.Ending ? null : ActivityWardrobe(PlayerActivity);
            if (context == null || me == null) return CharacterOutfits.ForPhase(me, state.phase);
            return CharacterOutfits.ForContext(WithWardrobe(me), context);
        }

        /// <summary>
        /// A houseguest whose look lists what they wear, so a change has something to take off. A
        /// preset look - or none saved at all - is filled in on a copy, resolved exactly as a new
        /// season's snapshot resolves it (<see cref="CharacterAppearanceSnapshots"/>): their own
        /// preset first, the trait recipe behind it. Keyed on the recipe alone, a houseguest with no
        /// saved look came out of the change as somebody else.
        /// </summary>
        private static ContestantState WithWardrobe(ContestantState person)
        {
            if (person.appearance?.outfits != null && person.appearance.outfits.Count > 0) return person;
            if (!(CharacterBodySource.Provider is IModularCharacterBodyProvider modular) || modular.Catalog == null) return person;
            var recipe = person.appearance?.Clone() ?? CharacterAppearance.Preset(person.sourceTemplateId ?? person.id);
            recipe.fallbackId = CharacterPresentation.AppearanceId(person, ContentCatalog.CanonicalId(person.id));
            var materialized = modular.Catalog.Materialize(recipe);
            if (materialized == null) return person;
            var copy = person.Clone();
            copy.appearance = materialized;
            return copy;
        }

        /// <summary>
        /// Puts the player in the clothes the moment calls for. An activity's change - and the
        /// change back - is made behind the body the player can see, so nobody vanishes on the way
        /// to the pool; anything else attaches as it always has.
        /// </summary>
        private void DressPlayer(EpisodeState state)
        {
            // Not while the director is being torn down: a new body built during an unload is a
            // body nobody will ever see, and a build that outlives its scene.
            if (player == null || state == null || !isActiveAndEnabled) return;
            var presentation = player.GetComponent<CharacterPresentation>();
            bool changing = ActivityWardrobe(PlayerActivity) != null || (presentation != null && presentation.IsChangingOutfit)
                || (presentation != null && presentation.AppearanceSnapshot?.activeOutfit is string worn
                    && (worn == CharacterOutfits.Swimwear || worn == CharacterOutfits.Sleepwear));
            var dressed = PlayerDressed(state);
            (changing ? CharacterPresentation.Dress(player.gameObject, dressed, PlayerPalette)
                : CharacterPresentation.Attach(player.gameObject, dressed, PlayerPalette))?.SetReducedMotion(reducedMotion);
        }

        /// <summary>The newer verbs, one row each in the menu, in this order.</summary>
        private static readonly HouseFurnitureActivity[] InHouseVerbs =
        {
            HouseFurnitureActivity.Sleep, HouseFurnitureActivity.Swim, HouseFurnitureActivity.Soak,
            HouseFurnitureActivity.Cook, HouseFurnitureActivity.Dance,
        };

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
            // One prop, one thing to do there: the bed, the pool, the hot tub and the stove start
            // on the click. The counter, the table and the loungers keep the menu they have always
            // opened.
            if(HouseFurniture.TryDescribe(anchor,out var kind,out _) && HouseFurniture.StartsOnClick(kind))
            {StartActivityInHouse(anchor,kind);return;}
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
            var anchors=HouseFurniture.InScene(gameObject.scene)
                .Where(anchor=>HouseFurniture.TryDescribe(anchor,out var kind,out _) && !HouseFurniture.StartsOnClick(kind))
                .OrderBy(anchor=>anchor==selectedFurniture?0:1)
                .ThenBy(anchor=>(anchor.Approach-player.transform.position).sqrMagnitude).ToArray();
            var verbs=InHouseVerbs.Select(verb=>(verb,place:NearestPlaceFor(verb))).Where(row=>row.place!=null).ToArray();
            if(anchors.Length==0 && verbs.Length==0){hud.Paragraph("This house has no available authored activity places.");return;}
            foreach(var anchor in anchors)
            {
                HouseFurniture.TryDescribe(anchor,out _,out var caption);
                bool available=npcMeetings!=null && npcMeetings.ActivityAnchorAvailable(anchor);
                hud.Action(caption+" · "+anchor.RoomId+" "+(anchor.Slot+1),()=>StartPlayerHouseActivity(anchor)).interactable=available;
                if(!available)hud.Paragraph("This place is occupied or house movement is unavailable.");
            }
            // One row a verb, to the nearest free place for it: five beds would be five rows that
            // all say "Lie down". These start in the house, as a click on the furniture does.
            foreach(var (verb,place) in verbs)
            {
                HouseFurniture.TryDescribe(place,out _,out var caption);
                hud.Action(caption,()=>StartActivityInHouse(place,verb));
            }
        }

        /// <summary>The nearest place the player may do this now, or null when there is none free.</summary>
        private HouseInteractionAnchor NearestPlaceFor(HouseFurnitureActivity verb)
            => player==null || npcMeetings==null ? null : HouseFurniture.InScene(gameObject.scene)
                .Where(anchor=>HouseFurniture.TryDescribe(anchor,out var kind,out _) && kind==verb
                    && npcMeetings.ActivityAnchorAvailable(anchor) && MayUse(anchor,out _))
                .OrderBy(anchor=>(anchor.Approach-player.transform.position).sqrMagnitude).FirstOrDefault();

        /// <summary>The house's own rules about who uses what: the HoH suite's bed is the Head of Household's.</summary>
        private bool MayUse(HouseInteractionAnchor anchor,out string reason)
        {
            reason=null;
            if(anchor!=null && anchor.RoomId=="HoH" && projected!=null && projected.hohId!=projected.playerId)
            {reason="Only the Head of Household sleeps in the HoH suite.";return false;}
            return true;
        }

        /// <summary>
        /// Starts an activity from the house: goes there - a warp's dip from across the house, a
        /// run or a walk nearer - and does it until told otherwise, with nothing frozen. A click on
        /// a place already taken tries the prop's other places first: the hot tub has two seats.
        /// </summary>
        public void StartActivityInHouse(HouseInteractionAnchor anchor,HouseFurnitureActivity kind)
        {
            if(!IsReady || blockedRecovery || !playerIsActive || challengeActive || npcMeetings==null || player==null || anchor==null)return;
            if(!NpcSocialState.IsEligible(projected))
            {message="House activities are available during free time and campaigning.";Render();return;}
            if(!MayUse(anchor,out var refusal)){message=refusal;Render();return;}
            CancelTravel();ClosePanels();EndDiaryVisit(true);CloseHouseActivities(true);
            if(!npcMeetings.ActivityAnchorAvailable(anchor))
                anchor=HouseInteractionAnchors.InScene(gameObject.scene).FirstOrDefault(other=>other.VenueId==anchor.VenueId
                    && other.transform.parent==anchor.transform.parent && npcMeetings.ActivityAnchorAvailable(other)) ?? anchor;
            // Asked before the warp: a player put across the house for a place that then turns out
            // to be taken is a player moved for nothing.
            if(!npcMeetings.PlayerActivityPossible(player,anchor,out var refused)){message=refused;Render();return;}
            // Whatever walk the player was on ends here. The activity's move keeps the walk it
            // interrupted and resumes it on release, which is right for a panel opened mid-walk
            // and wrong for getting out of bed: the player would set off after the old errand.
            player.StopHere();
            if(player.TryMeasureRoute(anchor.Approach,out float metres) && metres>HousePlayerController.WarpRouteMetres
                && player.TryWarpTo(anchor.Approach))
            {cameraRig?.CutTo(player.transform);BeginTravelDip();}
            if(!npcMeetings.TryReservePlayerActivity(player,anchor,out var lease,out var reason))
            {message=reason;Render();return;}
            playerActivity=lease;playerActivityInHouse=true;playerActivityKind=kind;
            BeginFurniturePose(player.gameObject,lease,kind,HouseFurniture.UntilMoved(kind) ? float.PositiveInfinity : 14f);
            if(!IsPlayerHouseActivityActive){message="That place is not reachable from here.";Render();return;}
            // Changed on the way there, behind the body walking to it.
            DressPlayer(projected);
            // In close enough to see what they are doing: from the far view a swimmer is a dot.
            cameraRig?.FocusSubject(player.transform);
            message=ActivityStatus(kind);
            Render();
        }

        /// <summary>
        /// What a click on the furniture under the pointer would do, said before it is made: "Take a
        /// swim" over the pool, "Go to the diary room" over the diary chair. The words are the
        /// click's own caption.
        /// </summary>
        private void HoverFurniture(HouseInteractionAnchor anchor,Vector2 screen)
        {
            string caption=null;
            if(anchor!=null && HouseIsTheView && playerIsActive && HouseFurniture.TryClick(anchor,out _,out var said))caption=said;
            if(caption!=null && travelBeacons==null)TickTravelBeacons();
            travelBeacons?.ShowFurnitureTip(caption,screen,largeText ? 1.2f : 1f);
        }

        private static string ActivityStatus(HouseFurnitureActivity kind)
        {
            switch(kind)
            {
                case HouseFurnitureActivity.Sleep:return "Off to bed  ·  E or a click to get up";
                case HouseFurnitureActivity.Swim:return "Going for a swim  ·  E or a click to get out";
                case HouseFurnitureActivity.Soak:return "Into the hot tub  ·  E or a click to get out";
                case HouseFurnitureActivity.Cook:return "Cooking a meal  ·  E or a click to stop";
                case HouseFurnitureActivity.Dance:return "Dancing  ·  E or a click to stop";
                default:return "At the furniture  ·  E to finish";
            }
        }

        /// <summary>
        /// A click on the house while the player is busy at a piece of furniture: they get up the
        /// way they got down, and the same click then does what it would have done - walks them,
        /// talks to somebody, starts something else.
        /// </summary>
        private void InterruptActivityInHouse(Ray ray,Vector2 screen)
        {
            if(!IsPlayerHouseActivityActive || !playerActivityInHouse || houseActivitiesOpen)return;
            clickAfterActivity=ray;clickAfterActivityScreen=screen;
            FinishPlayerHouseActivity();
        }

        private void StartPlayerHouseActivity(HouseInteractionAnchor anchor)
        {
            if(!houseActivitiesOpen || blockedRecovery || npcMeetings==null || IsPlayerHouseActivityActive
                || !HouseFurniture.TryDescribe(anchor,out var kind,out _))return;
            if(!npcMeetings.TryReservePlayerActivity(player,anchor,out var lease,out var reason))
            {message=reason;Render();return;}
            playerActivity=lease;playerActivityInHouse=false;playerActivityKind=kind;
            BeginFurniturePose(player.gameObject,lease,kind,18f);
            Render();
        }

        private void BeginFurniturePose(GameObject actor,HouseActivityLease lease,HouseFurnitureActivity kind,float seconds)
        {
            var owner=npcMeetings;
            // Explicitly: a missing component is Unity's fake null in the editor, which ?? keeps.
            var pose=actor.GetComponent<HouseFurniturePose>();
            if(pose==null)pose=actor.AddComponent<HouseFurniturePose>();
            pose.Begin(lease.Anchor,kind,seconds,()=>owner!=null && owner.ActivityValid(lease),
                ()=>owner!=null && owner.ActivityArrived(lease),()=>
                {
                    owner?.ReleaseActivity(lease);
                    if(ReferenceEquals(playerActivity,lease))
                    {
                        playerActivity=null;
                        if(playerActivityInHouse && this!=null && isActiveAndEnabled && IsReady)
                        {
                            playerActivityInHouse=false;
                            // Back into the day's clothes, behind the body getting up.
                            DressPlayer(projected);
                            if(message==ActivityStatus(playerActivityKind))message="";
                            // The click that got them up, now that they are up.
                            var click=clickAfterActivity;clickAfterActivity=null;
                            if(click.HasValue && player!=null)player.DispatchClick(click.Value,clickAfterActivityScreen);
                            if(!houseActivitiesOpen)Render();
                        }
                    }
                    if(this!=null && isActiveAndEnabled && IsReady && houseActivitiesOpen)Render();
                },()=>owner!=null && owner.ActivityPaused(lease));
            if(!pose.Active || !owner.RegisterActivityPresentation(lease,pose)){owner.ReleaseActivity(lease);if(ReferenceEquals(playerActivity,lease))playerActivity=null;}
        }

        public void FinishPlayerHouseActivity()
        {
            var pose=player!=null?player.GetComponent<HouseFurniturePose>():null;
            if(pose!=null && pose.Active)
            {
                pose.RequestFinish();
                // Out of the swimwear as they climb out, not after: a swimwear body still being
                // made would otherwise be swapped in once they were already out, and changed back.
                if(playerActivityInHouse)DressPlayer(projected);
            }
            else if(playerActivity!=null){npcMeetings?.ReleaseActivity(playerActivity);playerActivity=null;}
        }

        private void CloseHouseActivities(bool immediately=false)
        {
            bool wasOpen=houseActivitiesOpen;
            houseActivitiesOpen=false;selectedFurniture=null;
            if(immediately)
            {
                bool inHouse=playerActivityInHouse && playerActivity!=null;
                if(playerActivity!=null)npcMeetings?.ReleaseActivity(playerActivity);
                playerActivity=null;playerActivityInHouse=false;clickAfterActivity=null;
                // Sent somewhere mid-swim: out of the swimwear on the way.
                if(inHouse)DressPlayer(projected);
            }
            // Closing the menu ends what the menu began. Something begun in the house is not the
            // menu's to end: the notebook, the settings or Escape over a sleeping player leave
            // them asleep.
            else if(!playerActivityInHouse)FinishPlayerHouseActivity();
        }

        private void TickHouseActivities()
        {
            if(furnitureInput!=player)
            {
                if(furnitureInput!=null)
                {
                    furnitureInput.FurnitureSelected-=SelectHouseFurniture;
                    furnitureInput.HouseguestSelected-=SelectHouseguest;
                    furnitureInput.DestinationChosen-=CancelTravel;
                    furnitureInput.ActivityInterruptRequested-=InterruptActivityInHouse;
                    furnitureInput.FurnitureHovered-=HoverFurniture;
                }
                furnitureInput=player;
                if(furnitureInput!=null)
                {
                    furnitureInput.FurnitureSelected+=SelectHouseFurniture;
                    furnitureInput.HouseguestSelected+=SelectHouseguest;
                    // Clicking the floor is the plainest statement that the player wants to be
                    // somewhere else. An errand that outlived it dragged them back.
                    furnitureInput.DestinationChosen+=CancelTravel;
                    furnitureInput.ActivityInterruptRequested+=InterruptActivityInHouse;
                    furnitureInput.FurnitureHovered+=HoverFurniture;
                }
            }
            TickWalkToHouseguest();
            if(npcMeetings==null)return;
            if(!NpcSocialState.IsEligible(projected))
            {npcMeetings.ReleaseActivities();return;}
            if(playerActivity!=null && !npcMeetings.ActivityValid(playerActivity))
            {
                bool inHouse=playerActivityInHouse;
                npcMeetings.ReleaseActivity(playerActivity);playerActivity=null;playerActivityInHouse=false;
                if(inHouse)DressPlayer(projected);
                if(houseActivitiesOpen)Render();
            }
            if(!NpcCanAdvance || Time.unscaledTime<nextAmbientActivity)return;
            nextAmbientActivity=Time.unscaledTime+12f;
            // Only actors already cooling down from conversations enter optional visual routines.
            // No saved deadline, random draw, topic, relationship or need is changed by this choice.
            var cooling=projected.npcSocial.cooldowns.Where(row=>row.untilTick>projected.npcSocial.clockTick+2)
                .Select(row=>row.npcId).OrderBy(id=>id).ToArray();
            // The houseguests' own list: the counter, the table and the loungers. The player's has
            // grown beds, the pool and the stove, and nothing here should send the cast to bed.
            var places=HouseFurniture.InScene(gameObject.scene).Where(HouseFurniture.Ambient)
                .OrderBy(anchor=>anchor.VenueId).ThenBy(anchor=>anchor.Slot).ToArray();
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
            if(furnitureInput!=null)
            {
                furnitureInput.FurnitureSelected-=SelectHouseFurniture;
                furnitureInput.HouseguestSelected-=SelectHouseguest;
                furnitureInput.DestinationChosen-=CancelTravel;
                furnitureInput.ActivityInterruptRequested-=InterruptActivityInHouse;
                furnitureInput.FurnitureHovered-=HoverFurniture;
            }
            furnitureInput=null;nextAmbientActivity=0;ambientActivityIndex=0;headingToNpcId=null;
        }
    }
}
