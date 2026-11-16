using UnityEngine;

namespace Gamesim.Presentation
{
    public sealed partial class CompetitionApparatus
    {
        /// <summary>What a staged podium, table or course show's instrument shows (competition staging, slice 3).</summary>
        public enum ShowFace
        {
            /// <summary>The resting faces of another entrant's station: what <see cref="Sync"/> leaves.</summary>
            Ready,
            /// <summary>Podium: a question is up; the keys wait unlit.</summary>
            Question,
            /// <summary>Podium: the answer is locked in, every key lit pale.</summary>
            LockedIn,
            /// <summary>Podium: right, every key mint.</summary>
            Right,
            /// <summary>Podium: wrong and out, every key red.</summary>
            Wrong,
            /// <summary>Table: at work - letters picked one after another, dice tumbling.</summary>
            Working,
            /// <summary>Table: finished, every key mint and the dice landed.</summary>
            Done,
            /// <summary>Table: time called while still working; the dice land, the keys go out.</summary>
            TimeUp,
            /// <summary>Course: the start, every target lit.</summary>
            Go,
            /// <summary>Course: out on the leg; the post waits unlit.</summary>
            Running,
            /// <summary>Course: home and finished, every target mint.</summary>
            Finished,
        }

        /// <summary>The words a show's readout says at each face, in the order of <see cref="ShowFace"/>.</summary>
        public static readonly string[] ShowWords =
        {
            "Ready", "Question", "Locked in", "Right", "Out", "Working", "Done", "Time", "Go", "Running", "Finished",
        };

        /// <summary>The face a show set last; Ready when none has.</summary>
        public ShowFace ShownFace { get; private set; }

        /// <summary>
        /// A staged show's beat on this instrument: the readout's word, the keys lit for the beat, the
        /// dice tumbling while they are thrown. Drawn after <see cref="Sync"/>'s resting faces each
        /// frame, and as presentation only: the committed standings decide every beat; <paramref name="t"/>
        /// (seconds into the beat, unscaled) only moves what is moving. Grip rigs keep slice 1's
        /// <see cref="ShowHold"/>. Under reduced motion nothing turns.
        /// </summary>
        public void ShowBeat(ShowFace face, float t, bool reducedMotion)
        {
            if (Instrument == Family.GripRig) return;
            ShownFace = face;
            // Sync rests the faces again next frame and this draws the beat over them.
            resting = false;
            if (progress != null) SetWord(progress, ShowWords[(int)face]);
            switch (Instrument)
            {
                case Family.DiceTray:
                    for (int i = 0; i < indicators.Count && i < glyphs.Count; i++)
                    {
                        bool tumbling = face == ShowFace.Working && !reducedMotion;
                        indicators[i].transform.localRotation = tumbling ? Quaternion.Euler(0, t * 200f + i * 40f, 0) : Quaternion.identity;
                        TurnLabelsWithDie(i);
                        SetWord(glyphs[i], face == ShowFace.Working ? "…" : "—");
                    }
                    break;
                default:
                    Color? keys = KeysFor(face);
                    int lit = indicators.Count;
                    // At work a letter console picks its letters one after another, and starts again.
                    if (face == ShowFace.Working && Instrument == Family.WordConsole)
                    {
                        keys = UiTheme.Positive;
                        lit = reducedMotion ? 0 : (int)(t * 3f) % (indicators.Count + 1);
                    }
                    for (int i = 0; i < indicators.Count; i++) LightKey(i, i < lit ? keys : null);
                    break;
            }
        }

        private static Color? KeysFor(ShowFace face)
        {
            switch (face)
            {
                case ShowFace.LockedIn: return UiTheme.Accent;
                case ShowFace.Right: case ShowFace.Done: case ShowFace.Go: case ShowFace.Finished: return UiTheme.Positive;
                case ShowFace.Wrong: return UiTheme.Danger;
                default: return null;
            }
        }
    }
}
