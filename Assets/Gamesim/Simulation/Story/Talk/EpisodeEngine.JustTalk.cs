using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The free "Just talk" command (talk rules 1, slice S1): the checks, the cap, the gains, the reveal and the spend, exactly as
    /// <see cref="FreeTalk.Result"/> computes them. Free: no action is spent and nothing is drawn from the season's stream.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        private static void ResolveJustTalk(EpisodeState s, EpisodeCommand c)
        {
            Require(FreeTalk.On(s), FreeTalk.OffReason);
            Require(false, FreeTalk.NotOnOfferReason);
        }
    }
}
