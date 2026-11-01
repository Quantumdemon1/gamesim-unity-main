using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The live house's saves (LIVE-SAVE-PLAN.md): the verification's commit timing log, which records how long
    /// each part of a house commit takes and is switched on only by the verification player.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator LiveSave_TheCommitTimingsRecordEachPartOnlyWhenAsked()
        {
            Assert.That(NpcRead<NpcCommitTimings>("commitTimingForVerification"), Is.Null,
                "Ordinary play and every Editor session leave the timing log off.");
            yield return WaitForNpcRuntimeBinding();
            NpcResetTransientFractions();
            NpcWrite("npcFrameFraction", 1d);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True, director.StatusMessage);
            Assert.That(NpcRead<NpcCommitTimings>("commitTimingForVerification"), Is.Null, "A commit does not switch it on.");

            var timings = new NpcCommitTimings();
            NpcWrite("commitTimingForVerification", timings);
            try
            {
                NpcWrite("npcFrameFraction", 1d);
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True, director.StatusMessage);
                long tickRevision = director.Snapshot.revision;
                int frame = Time.frameCount;
                director.SaveNow();
                var rows = LiveSaveTimingRows(timings);
                Assert.That(rows.Count, Is.EqualTo(2), timings.ToCsv());

                var tick = rows[0];
                Assert.That(tick["kind"], Is.EqualTo("tick"));
                Assert.That(tick["frame"], Is.EqualTo(frame.ToString(CultureInfo.InvariantCulture)));
                Assert.That(tick["revision"], Is.EqualTo(tickRevision.ToString(CultureInfo.InvariantCulture)));
                foreach (var part in new[] { "prepareMs", "persistMs", "engineMs", "projectMs" })
                    Assert.That(double.Parse(tick[part], CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(0), part);
                Assert.That(tick["validateMs"], Is.Empty, "Today's tick has no separate check before its write.");
                Assert.That(tick["cause"], Is.EqualTo("tick"));
                Assert.That(tick["saved"], Is.EqualTo("true"));

                var save = rows[1];
                Assert.That(save["kind"], Is.EqualTo(NpcCommitTimings.WriteKind));
                Assert.That(save["cause"], Is.EqualTo("save"));
                Assert.That(double.Parse(save["persistMs"], CultureInfo.InvariantCulture), Is.GreaterThan(0));
                Assert.That(save["saved"], Is.EqualTo("true"));
                Assert.That(save["prepareMs"], Is.Empty);
            }
            finally { NpcWrite("commitTimingForVerification", null); }
        }

        private static List<Dictionary<string, string>> LiveSaveTimingRows(NpcCommitTimings timings)
        {
            var lines = timings.ToCsv().Split('\n');
            var header = lines[0].Split(',');
            return lines.Skip(1).Where(line => line.Length > 0).Select(line =>
            {
                var fields = line.Split(',');
                Assert.That(fields.Length, Is.EqualTo(header.Length), line);
                return header.Select((name, index) => (name, value: fields[index])).ToDictionary(pair => pair.name, pair => pair.value);
            }).ToList();
        }
    }
}
