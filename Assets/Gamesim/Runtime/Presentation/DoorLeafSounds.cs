namespace Gamesim.Presentation
{
    /// <summary>What a step of the front door's leaves sounds like (<see cref="DoorLeafSounds.Between"/>).</summary>
    public enum DoorLeafSound
    {
        /// <summary>Nothing: the leaves stood still, or moved without leaving the frame or landing in it.</summary>
        None,
        /// <summary>The leaves left the frame: the door gave after its rattle and began to swing open.</summary>
        Swing,
        /// <summary>The leaves landed in the frame: the door thudded shut.</summary>
        Shut,
    }

    /// <summary>
    /// When the front door is heard (AU-06): the clunk as its leaves give and begin to swing open, and
    /// the thud as they land shut - at the opening's entrances and as the evicted walk out. The set
    /// (OpeningDoorSet) asks after every step of its leaves, by how open they stood before it and after
    /// it, so the sound comes in the frame the eye sees the door move. Unity-free, so the Unity-free
    /// suite holds it.
    ///
    /// <para>A door that turns back before it shut makes neither sound - its latch never caught - and
    /// neither does a rattle that never gave: the leaves did not leave the frame. Under reduced motion
    /// the leaves jump in one step, so the clunk comes with the open and the thud with the close.</para>
    /// </summary>
    public static class DoorLeafSounds
    {
        /// <summary>
        /// How open the leaves may stand and still read as shut: a hundredth of their swing, under one
        /// degree. The walk out holds its shot on the shut door from the same moment.
        /// </summary>
        public const float ShutBelow = 0.01f;

        /// <summary>Whether leaves this open read as shut.</summary>
        public static bool IsShut(float openness) => openness <= ShutBelow;

        /// <summary>
        /// The sound of the leaves going from <paramref name="before"/> to <paramref name="after"/> in
        /// one step, each 0 shut to 1 wide open: the swing as they leave shut, the thud as they land in
        /// it, and nothing for a step that does neither.
        /// </summary>
        public static DoorLeafSound Between(float before, float after)
        {
            bool wasShut = IsShut(before), isShut = IsShut(after);
            if (wasShut && !isShut) return DoorLeafSound.Swing;
            if (!wasShut && isShut) return DoorLeafSound.Shut;
            return DoorLeafSound.None;
        }
    }
}
