using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>The follow camera: who the rig rides and how the cast rail, the keys and the pad choose them.</summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The name of the houseguest the camera is following, or null.</summary>
        public string FollowedName
        {
            get
            {
                var subject = cameraRig != null ? cameraRig.FocusedSubject : null;
                if (subject == null || projected == null) return null;
                var npc = subject.GetComponentInParent<HouseNpc>();
                if (npc != null) return projected.Find(npc.Id)?.name;
                return subject.GetComponentInParent<HousePlayerController>() != null ? projected.Find(projected.playerId)?.name : null;
            }
        }

        /// <summary>
        /// Follows a houseguest by id, from the cast rail: the camera rides them until something
        /// else is asked of it. The same id again lets go. Honoured under reduced motion for the
        /// reason click-to-follow is: the viewer asked for this shot.
        /// </summary>
        public void FollowHouseguest(string id)
        {
            if (cameraRig == null || string.IsNullOrEmpty(id)) return;
            var body = BodyFor(id);
            if (body == null) return;
            if (cameraRig.FocusedSubject == body) cameraRig.ClearSubject();
            else cameraRig.FocusSubject(body);
            hud?.ShowFollowing(FollowedName);
            lastFollowed = FollowedName;
        }

        /// <summary>Tab follows the next active houseguest in cast order, Shift+Tab the previous; the player is in the ring.</summary>
        public void FollowNext(bool backwards)
        {
            if (cameraRig == null || projected == null) return;
            var ring = new List<Transform>();
            foreach (var actor in projected.contestants)
            {
                if (actor.status != ContestantStatus.Active) continue;
                var body = BodyFor(actor.id);
                if (body != null) ring.Add(body);
            }
            if (ring.Count == 0) return;
            int at = ring.IndexOf(cameraRig.FocusedSubject);
            int next = at < 0 ? (backwards ? ring.Count - 1 : 0) : (at + (backwards ? ring.Count - 1 : 1)) % ring.Count;
            cameraRig.FocusSubject(ring[next]);
            hud?.ShowFollowing(FollowedName);
            lastFollowed = FollowedName;
        }

        private Transform BodyFor(string id)
        {
            if (housemates != null)
                foreach (var npc in housemates)
                    if (npc != null && npc.Id == id && npc.gameObject.activeInHierarchy) return npc.transform;
            return player != null && projected != null && id == projected.playerId ? player.transform : null;
        }

        /// <summary>
        /// The visual root of a houseguest whose body is generated at runtime, or null.
        ///
        /// <para>Only generated bodies are offered. An authored prefab photographs better from the
        /// portrait rig — isolated, unlit, framed — than it does standing in a dark house, so there
        /// is nothing to gain by capturing it live and a lit-by-the-room portrait to lose.</para>
        /// </summary>
        public Transform LiveBody(string contestantId)
        {
            if (CharacterBodySource.Provider == null) return null;
            var canonical = ContentCatalog.CanonicalId(contestantId);
            foreach (var visual in gameObject.scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CharacterPresentation>(true)))
            {
                if (ContentCatalog.CanonicalId(visual.CharacterId) != canonical) continue;
                return visual.transform.Find("Gamesim Character Visual");
            }
            return null;
        }
    }
}
