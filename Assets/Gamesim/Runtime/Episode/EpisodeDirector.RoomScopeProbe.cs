using System;

namespace Gamesim.Episode
{
    /// <summary>
    /// The views probe's one hold on the house (ROOM-SCOPE-PLAN §5.3, RS0): the cast stands on fixed
    /// marks, so a before and an after measure the same placement. The probe asks for the house's
    /// people the way the editor's diagnostics do (<see cref="SuspendNpcAutonomyForDiagnostics"/>):
    /// the NPC world stands down and nobody walks until the run ends. A player build has no other
    /// caller - the probe proves it is the views runner - so nothing a player does ever reaches it.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>Stands the NPC world down for the views probe, which then puts every body on its mark.</summary>
        internal void HoldHouseForViewsProbe(PortVerification probe)
        {
            if (probe == null || !probe.IsViewsProbe) throw new InvalidOperationException("Only the views probe holds the house still.");
            npcDiagnosticsSuspended = true;
            DisposeNpcSocialWorld();
        }

        /// <summary>Lets the house's people go again: the world is built anew on the next tick that may have one.</summary>
        internal void ReleaseHouseFromViewsProbe(PortVerification probe)
        {
            if (probe == null || !probe.IsViewsProbe) return;
            npcDiagnosticsSuspended = false;
        }

        /// <summary>Whether the house's NPC world exists and is advancing: what the live pass reports beside its spread.</summary>
        internal bool ViewsProbeNpcWorldAdvancing => npcMeetings != null && NpcCanAdvance;
    }
}
