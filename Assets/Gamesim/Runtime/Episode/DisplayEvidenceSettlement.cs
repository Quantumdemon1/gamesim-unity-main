using System;

namespace Gamesim.Episode
{
    /// <summary>
    /// A screen request is asynchronous. Evidence waits for the requested screen to be observed
    /// and quiet; a bounded timeout reports the actual screen instead of asserting the request won.
    /// Unity-free so deferred observations, superseding requests and timeouts are reproducible.
    /// </summary>
    public sealed class DisplayEvidenceSettlement
    {
        public enum Observation { Waiting, Settled, TimedOut }
        public const double MinimumSeconds = 1;
        public const double QuietSeconds = .25;
        public const double TimeoutSeconds = 10;

        public bool Pending { get; private set; }
        public string Expected { get; private set; }
        public double Deadline { get; private set; }
        private double began;
        private double quietSince;
        private string observed;

        public void Begin(double now, string expected, string current)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) throw new ArgumentOutOfRangeException(nameof(now));
            Expected = expected ?? throw new ArgumentNullException(nameof(expected));
            observed = current ?? throw new ArgumentNullException(nameof(current));
            began = quietSince = now;
            Deadline = now + TimeoutSeconds;
            Pending = true;
        }

        public Observation Observe(double now, string actual)
        {
            if (!Pending) return Observation.Waiting;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < began) throw new ArgumentOutOfRangeException(nameof(now));
            if (actual == null) throw new ArgumentNullException(nameof(actual));
            if (!string.Equals(observed, actual, StringComparison.Ordinal))
            {
                observed = actual;
                quietSince = now;
            }
            // A matching size is not proof that a monitor operation whose deadline elapsed
            // completed. Deadline wins even when the requested screen happened to be unchanged.
            if (now >= Deadline)
            {
                Pending = false;
                return Observation.TimedOut;
            }
            if (now - began >= MinimumSeconds && now - quietSince >= QuietSeconds &&
                string.Equals(Expected, actual, StringComparison.Ordinal))
            {
                Pending = false;
                return Observation.Settled;
            }
            return Observation.Waiting;
        }
    }
}
