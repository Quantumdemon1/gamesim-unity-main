using System.Collections.Generic;

namespace Gamesim.Simulation
{
    public static class TalkStanceTable
    {
        public static readonly IReadOnlyDictionary<string, RomanceStance> ByTemplate = new Dictionary<string, RomanceStance>
        {
            { "dan-gheesling", RomanceStance.HereToPlay },
            { "dr-will-kirby", RomanceStance.Undecided },
            { "derrick-levasseur", RomanceStance.HereToPlay },
            { "janelle-pierzina", RomanceStance.HereToPlay },
            { "cody-calafiore", RomanceStance.HereToPlay },
            { "danielle-reyes", RomanceStance.HereToPlay },
            { "vanessa-rousso", RomanceStance.HereToPlay },
            { "tyler-crispen", RomanceStance.HereToPlay },
            { "chelsie-baham", RomanceStance.Undecided },
            { "rachel-reilly", RomanceStance.HereToPlay },
            { "xavier-prather", RomanceStance.HereToPlay },
            { "jun-song", RomanceStance.Undecided },
        };
    }
}
