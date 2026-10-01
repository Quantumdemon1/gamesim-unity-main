using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The veto's player selection (PACK8-PASS-PLAN B2, the owner's mockup 76), and the draw it makes.
    ///
    /// <para><b>The screen.</b> One row across the frame: who plays by right, the bag, and who is
    /// eligible for the draw, under one line that says how the field is made up. At six houseguests
    /// or fewer everyone plays and the engine draws nothing, so the bag is empty and there is no pool:
    /// the screen showed three chips and "3 drawn from the house" for a draw that never happened. The
    /// way on is still "Continue episode", which makes the draw, wearing the mockup's 'Reveal the
    /// draw' as a headline beside its caption; the footer's strip says what pressing it does.</para>
    ///
    /// <para><b>The reveal.</b> The draw is made by that commit, so anything shown before it would be a
    /// second rules engine. After it, the chips come out of the bag one at a time and turn into the
    /// faces they drew (<see cref="VetoDrawReveal"/>), in a seeded presentation order: the engine holds
    /// the lineup in house order. The card reads the committed lineup and writes nothing. Batch runs
    /// and reduced motion keep the field card it stands in for, so the audited walk and every card
    /// test see what they always saw, and a draw with nobody drawn keeps it too.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private VetoDrawReveal drawReveal;

        /// <summary>For tests: play the draw's reveal even in a batch run, where the field card plays instead, as a stage is asked for.</summary>
        public bool DrawRevealsInBatchRuns { get; set; }

        /// <summary>The draw's reveal card, once a draw has asked for it. A read, for tests.</summary>
        public VetoDrawReveal DrawRevealCard => drawReveal;

        /// <summary>Whether the draw's reveal is on screen: the chrome stands aside for it as it does for the other reveals.</summary>
        private bool VetoDrawRevealing => drawReveal != null && drawReveal.IsPlaying;

        /// <summary>Whether the screen open now is the veto's player selection: laid out for the frame's whole width.</summary>
        private static bool VetoDrawBeat(EpisodeState s) => s != null && s.pendingDiary == null && s.phase == EpisodePhase.VetoSelection;

        /// <summary>
        /// The selection's screen, under the house's status and any story beat: the ceremony's name
        /// small, the one line, and the row. The footer's strip says what the way on does, set before
        /// the row measures what the step has left; a storyline the way on lets pass outranks it.
        /// </summary>
        private void VetoDrawScreen(EpisodeState s)
        {
            var board = VetoDraw.Before(s);
            EpisodeHud.CeremonyFace Face(string id) => id == s.hohId ? new EpisodeHud.CeremonyFace(id, "HOH", UiTheme.Gold)
                : s.nominees.Contains(id) ? new EpisodeHud.CeremonyFace(id, "NOM", UiTheme.Danger)
                : new EpisodeHud.CeremonyFace(id, null, UiTheme.Muted);
            hud.VetoDrawHead("Power of Veto Player Selection", VetoDraw.Line(board));
            hud.PinnedNote(VetoDraw.Footnote(board), null, false);
            hud.VetoDrawBoard(board.ByRight.Select(Face).ToList(), board.Eligible.Select(Face).ToList(), board.ToDraw,
                VetoDraw.PlayingHeading(board), VetoDraw.DrawHeading(board), VetoDraw.ChipsLine(board), VetoDraw.EligibleHeading);
        }

        /// <summary>The way on, dressed as the draw it makes: Pack 8's face and the mockup's words beside the caption.</summary>
        private void VetoDrawWayOn(EpisodeState s, Button wayOn) =>
            hud.DressWayOn(wayOn, VetoDraw.Headline(VetoDraw.Before(s)), PackArt.Pack8RevealButton);

        /// <summary>
        /// Plays the draw's reveal for the commit that made it, when there was a draw and this house
        /// plays it. False - and the caller plays the field card - under reduced motion, in a batch run
        /// not asking, or when everyone played. The chrome stands aside while it plays: the status line
        /// names every drawn houseguest the moment the commit lands.
        /// </summary>
        private bool PlayVetoDrawReveal(EpisodeState committed)
        {
            if (committed == null || reducedMotion || (Application.isBatchMode && !DrawRevealsInBatchRuns)) return false;
            var drawn = VetoDraw.Drawn(committed);
            if (drawn.Count == 0) return false;
            // Unity's fake null: a card destroyed with its scene compares equal to null, and a ?? would not see it.
            if (drawReveal == null) drawReveal = VetoDrawReveal.Attach(gameObject);
            drawReveal.FontScale = largeText ? 1.2f : 1f;
            VetoDrawReveal.Player Entry(string id, string badge)
            {
                var actor = committed.Find(id);
                return new VetoDrawReveal.Player(id, actor != null ? actor.name : id, badge,
                    actor != null ? CharacterPortraits.Get(actor) : null, actor);
            }
            var byRight = new List<VetoDrawReveal.Player>();
            if (committed.vetoPlayers.Contains(committed.hohId)) byRight.Add(Entry(committed.hohId, "HOH"));
            foreach (var id in committed.nominees)
                if (committed.vetoPlayers.Contains(id)) byRight.Add(Entry(id, "NOM"));
            bool played = drawReveal.Play(committed.week, byRight, drawn.Select(id => Entry(id, "DRAWN")).ToList(),
                VetoFieldLine(committed), reducedMotion);
            if (played) HoldHudForReveal();
            return played;
        }

        /// <summary>Takes the draw's reveal down with the other ceremony cards.</summary>
        private void CancelVetoDrawReveal()
        {
            if (drawReveal != null) drawReveal.Cancel();
        }

        /// <summary>The reveal is a scene root, as every ceremony card is: left behind, the next director would attach a second.</summary>
        private void DestroyVetoDrawReveal()
        {
            if (drawReveal != null) { Destroy(drawReveal.gameObject); drawReveal = null; }
        }
    }
}
