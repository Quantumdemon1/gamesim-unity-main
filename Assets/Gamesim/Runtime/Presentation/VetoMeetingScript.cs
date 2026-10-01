using System.Collections.Generic;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What the veto meeting's card says on the living room's screen
    /// (<see cref="CeremonyTakeover.PlayVetoMeeting"/>): the meeting as
    /// <see cref="VetoMeetingRead"/> reads it from the committed state and the block before the
    /// commit, and a face for everybody it names.
    ///
    /// <para>Built by the caller, already within what the player is entitled to see, as the
    /// takeover's subjects are: the card learns nothing about the cast on its own. Nothing in it is
    /// saved; it lives as long as the card does.</para>
    /// </summary>
    public sealed class VetoMeetingScript
    {
        /// <summary>One face the card can show.</summary>
        public readonly struct Face
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Texture Portrait;
            public readonly ContestantState Character;

            public Face(string id, string name, Texture portrait, ContestantState character = null)
            {
                Id = id; Name = name; Portrait = portrait; Character = character?.Clone();
            }
        }

        private readonly Dictionary<string, Face> faces = new Dictionary<string, Face>();

        /// <summary>The meeting, read: who is in it and the card's lines.</summary>
        public VetoMeetingRead.Meeting Meeting { get; }

        public VetoMeetingScript(VetoMeetingRead.Meeting meeting, IEnumerable<Face> people)
        {
            Meeting = meeting;
            if (people == null) return;
            foreach (var face in people)
                if (!string.IsNullOrEmpty(face.Id)) faces[face.Id] = face;
        }

        /// <summary>The face for <paramref name="id"/>, when the caller gave one.</summary>
        public bool TryFace(string id, out Face face)
        {
            face = default;
            return id != null && faces.TryGetValue(id, out face);
        }

        /// <summary>
        /// Whether the card can tell this meeting: a block before it and a block after it, each
        /// with a face. A meeting it cannot tell plays as the generic card on the HUD instead, so
        /// the beat is never silent.
        /// </summary>
        public bool Playable
        {
            get
            {
                if (Meeting == null || Meeting.blockBefore.Count == 0 || Meeting.finalBlock.Count == 0) return false;
                foreach (var id in Meeting.blockBefore) if (!faces.ContainsKey(id)) return false;
                foreach (var id in Meeting.finalBlock) if (!faces.ContainsKey(id)) return false;
                return true;
            }
        }
    }
}
