using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The scenery drawn on the competition deck, which a station's reservation must clear just as
    /// it clears the colliders <see cref="CompetitionStageFootprint.HasStaticClearance"/> sees.
    ///
    /// <para>Much of the yard has no collider, so physics never saw it: the roped entrance (its arch,
    /// stanchions and ropes are set dressing by design, which EpisodeHouseYardTests pins), the
    /// course's gates, signs and backdrop, the yard display, the flight cases, the hanging lamp, and a
    /// course stack whose fitted proxy was switched off. With only physics to go on, the arena stood
    /// the player's station at x 2.4 on the back row, where the arch's right leg passed through the
    /// rear of every family's instrument.</para>
    ///
    /// <para>So the arena reads what is drawn. Every enabled renderer of the deck's own house (the
    /// floor's root) that stands on the deck - the middle of its bounds over the deck's rectangle and
    /// its top more than <see cref="Rise"/> over the deck - counts with its world bounds, and a
    /// reservation may not meet them. Floor paint (the lanes, the start lines, the neon strips) lies
    /// lower and takes no part. A piece with a fitted proxy counts too: the proxy is fitted to the same
    /// bounds, so physics already refuses the same reservations and counting the drawing changes
    /// nothing, but a proxy that is smaller than its art, or switched off, no longer lets an
    /// instrument through the art. Bounds rather than triangles, because an imported mesh keeps no CPU
    /// copy in a player (isReadable 0); the box is the conservative answer. The arena's own runtime
    /// root (its instruments, marks and sign) never counts, and neither do effects.</para>
    ///
    /// <para>No collider and no NavMesh carving is added: the pieces stay walk-through for the
    /// house's navigation and invisible to its sight queries, exactly as they were authored.</para>
    /// </summary>
    public sealed class CompetitionStageScenery
    {
        /// <summary>Metres over the deck a drawing's top must reach to stand on it rather than lie on it as paint (the neon strips reach .05).</summary>
        public const float Rise=.06f;

        private readonly List<Renderer> pieces=new List<Renderer>();
        private readonly List<Bounds> boxes=new List<Bounds>();

        /// <summary>Everything drawn standing on <paramref name="deck"/> now, except what lies under <paramref name="own"/>.</summary>
        public static CompetitionStageScenery Collect(Collider deck,Transform own)
        {
            var scenery=new CompetitionStageScenery();
            if(deck==null)return scenery;
            var floor=deck.bounds;
            foreach(var renderer in deck.transform.root.GetComponentsInChildren<Renderer>())
            {
                if(!Stands(renderer,floor) || own!=null && renderer.transform.IsChildOf(own))continue;
                scenery.pieces.Add(renderer);scenery.boxes.Add(renderer.bounds);
            }
            return scenery;
        }

        /// <summary>Whether a renderer is drawn standing on the deck whose collider bounds are <paramref name="deck"/>.</summary>
        public static bool Stands(Renderer renderer,Bounds deck)
        {
            if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)return false;
            if(renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)return false;
            var box=renderer.bounds;
            return box.center.x>=deck.min.x && box.center.x<=deck.max.x && box.center.z>=deck.min.z && box.center.z<=deck.max.z
                && box.max.y>deck.max.y+Rise;
        }

        /// <summary>How many drawn pieces stand on the deck.</summary>
        public int Count=>boxes.Count;

        /// <summary>Whether <paramref name="footprint"/> meets none of them.</summary>
        public bool Clears(CompetitionStageFootprint footprint)
        {
            for(int i=0;i<boxes.Count;i++)if(footprint.Meets(boxes[i]))return false;
            return true;
        }

        /// <summary>The names of the pieces <paramref name="footprint"/> meets, for a refusal's log.</summary>
        public string Blockers(CompetitionStageFootprint footprint)
        {
            var met=new List<string>();
            for(int i=0;i<boxes.Count;i++)
                if(footprint.Meets(boxes[i]))met.Add((pieces[i]!=null?pieces[i].name:"destroyed piece")+"@"+boxes[i].center.ToString("F3"));
            return met.Count>0?string.Join(",",met):"none";
        }
    }
}
