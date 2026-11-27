using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A houseguest's murmur (AU-16): a soft, wordless voice under each line a conversation shows -
    /// the speaker's own pitch, a beat a word, never words. The line on screen is its picture, so it
    /// carries no news of its own, and it plays under <see cref="SoundChannel.Voice"/>.
    ///
    /// <para>Everything here is decided by the speaker's id, their pronouns and the line itself, never
    /// by the season's random stream: the same houseguest always has the same voice, and the same
    /// line always murmurs the same way, in every session and on every machine. The id's hash is
    /// FNV-1a over its UTF-16 code units (<see cref="Hash"/>), never <c>string.GetHashCode</c>, which
    /// .NET seeds afresh in every process.</para>
    ///
    /// <list type="bullet">
    /// <item>Pitch: a register from the pronouns (lower for he/him, higher for she/her, between for
    /// any other), and within it a place from the id's hash, anywhere in nine semitones.</item>
    /// <item>Length: a syllable a word, <see cref="SyllableSeconds"/> each, at least
    /// <see cref="MinSyllables"/> and at most <see cref="MaxSyllables"/>, so never longer than
    /// <see cref="MaxSeconds"/>; a line with no words has no murmur.</item>
    /// <item>Shape: each syllable rises and falls away to silence; the line drifts down two
    /// semitones, as speech does, and a question lifts at its end.</item>
    /// <item>Level: a witnessed caption's murmur - a pair across the room - plays
    /// <see cref="WitnessedGainDb"/> under a conversation's own.</item>
    /// </list>
    ///
    /// <para>Unity-free, so the subset holds these rules; <c>HouseAudio</c> turns a render into a clip.</para>
    /// </summary>
    public static class Murmur
    {
        /// <summary>One syllable a word, in seconds: its voiced part and the breath after it.</summary>
        public const double SyllableSeconds = 0.12;

        /// <summary>The longest murmur, in seconds: a long line murmurs its first ten words' worth and stops.</summary>
        public const double MaxSeconds = 1.2;

        /// <summary>The most syllables a murmur has: <see cref="MaxSeconds"/> of them.</summary>
        public const int MaxSyllables = 10;

        /// <summary>The fewest a murmur with words has: a one-word line still sounds like speech, not a blip.</summary>
        public const int MinSyllables = 2;

        /// <summary>The share of a syllable that is voiced; the rest is the silence before the next.</summary>
        public const double VoicedShare = 0.8;

        /// <summary>A witnessed caption's murmur against a conversation's own, in decibels: the pair is across the room.</summary>
        public const float WitnessedGainDb = -6f;

        /// <summary>
        /// The murmur's level in the render: a line's loudest 400 ms comes to about -23 dB of full scale,
        /// so at the whole of the Voice volume a murmur sits under the conversation's trust sounds, which
        /// close the same conversation, and over the hover's tick, the quietest sound the house makes
        /// (CueLoudnessTests); the Voice volume's 70 percent sets it 3 dB lower again.
        /// </summary>
        public const double Level = 0.18;

        /// <summary>The breath under the voice, against the voice.</summary>
        private const double BreathMix = 0.15;

        /// <summary>How far a voice may sit above its register's floor, in hundredths of a semitone: nine semitones.</summary>
        public const uint PitchSpan = 900;

        /// <summary>The floor of each register, in hertz.</summary>
        public const double LowFloorHz = 98.0, MiddleFloorHz = 131.0, HighFloorHz = 175.0;

        /// <summary>Where a voice sits: from the houseguest's pronouns.</summary>
        public enum Register { Low, Middle, High }

        /// <summary>One murmur: who, what it says and how it will sound.</summary>
        public sealed class Plan
        {
            public string SpeakerId { get; }
            public int Words { get; }
            public int Syllables { get; }
            public double Seconds { get; }
            public double PitchHz { get; }
            public double Brightness { get; }
            public bool Question { get; }
            public bool Witnessed { get; }
            /// <summary>Its place under the Voice volume, in decibels: 0 for a conversation's line, <see cref="WitnessedGainDb"/> for a witnessed caption.</summary>
            public float GainDb { get; }
            /// <summary>The hash of the speaker and the line, which varies each syllable; never the season's stream.</summary>
            public uint LineHash { get; }

            internal Plan(string speakerId, int words, double pitchHz, double brightness, bool question, bool witnessed, uint lineHash)
            {
                SpeakerId = speakerId;
                Words = words;
                Syllables = SyllablesFor(words);
                Seconds = Syllables * SyllableSeconds;
                PitchHz = pitchHz;
                Brightness = brightness;
                Question = question;
                Witnessed = witnessed;
                GainDb = witnessed ? WitnessedGainDb : 0f;
                LineHash = lineHash;
            }

            public override string ToString() => SpeakerId + ": " + Words + " words, " + Syllables + " syllables, "
                + Seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s at "
                + PitchHz.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " Hz" + (Witnessed ? ", witnessed" : "");
        }

        /// <summary>The murmur for <paramref name="line"/> said by <paramref name="speakerId"/>; quieter when <paramref name="witnessed"/>.</summary>
        public static Plan For(string speakerId, string pronouns, string line, bool witnessed)
        {
            string id = speakerId ?? "";
            return new Plan(id, WordCount(line), PitchOf(id, pronouns), BrightnessOf(id), IsQuestion(line), witnessed,
                Hash(id + "\u001f" + (line ?? "")));
        }

        /// <summary>FNV-1a, 32 bits, over the text's UTF-16 code units, low byte first: the same in every process.</summary>
        public static uint Hash(string text)
        {
            uint hash = 2166136261u;
            if (text == null) return hash;
            unchecked
            {
                foreach (char c in text)
                {
                    hash ^= (uint)(c & 0xFF);
                    hash *= 16777619u;
                    hash ^= (uint)(c >> 8);
                    hash *= 16777619u;
                }
            }
            return hash;
        }

        /// <summary>The register a houseguest's pronouns give: he/him low, she/her high, any other between.</summary>
        public static Register RegisterOf(string pronouns)
        {
            string said = (pronouns ?? "").Trim();
            if (said.StartsWith("she/", StringComparison.OrdinalIgnoreCase)) return Register.High;
            if (said.StartsWith("he/", StringComparison.OrdinalIgnoreCase)) return Register.Low;
            return Register.Middle;
        }

        /// <summary>The lowest pitch of a register, in hertz.</summary>
        public static double FloorOf(Register register) =>
            register == Register.Low ? LowFloorHz : register == Register.High ? HighFloorHz : MiddleFloorHz;

        /// <summary>A houseguest's pitch, in hertz: their register's floor, raised by up to nine semitones by their id's hash.</summary>
        public static double PitchOf(string speakerId, string pronouns)
        {
            double semitones = Hash(speakerId ?? "") % PitchSpan / 100.0;
            return FloorOf(RegisterOf(pronouns)) * Math.Pow(2.0, semitones / 12.0);
        }

        /// <summary>How bright a houseguest's voice is, 0.25 to 0.6: the weight of its second harmonic, from other bits of the id's hash.</summary>
        public static double BrightnessOf(string speakerId) => 0.25 + (Hash(speakerId ?? "") >> 16) % 100u / 100.0 * 0.35;

        /// <summary>The words of a line: the runs between spaces that hold a letter or a digit ("How's" is one, a dash is none).</summary>
        public static int WordCount(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            int words = 0;
            bool inRun = false, counted = false;
            foreach (char c in line)
            {
                if (char.IsWhiteSpace(c)) { inRun = false; continue; }
                if (!inRun) { inRun = true; counted = false; }
                if (!counted && char.IsLetterOrDigit(c)) { words++; counted = true; }
            }
            return words;
        }

        /// <summary>The syllables a line of <paramref name="words"/> words murmurs: none for none, else one a word inside <see cref="MinSyllables"/> to <see cref="MaxSyllables"/>.</summary>
        public static int SyllablesFor(int words) => words <= 0 ? 0 : Math.Max(MinSyllables, Math.Min(MaxSyllables, words));

        /// <summary>How long a line of <paramref name="words"/> words murmurs, in seconds: never more than <see cref="MaxSeconds"/>.</summary>
        public static double SecondsFor(int words) => SyllablesFor(words) * SyllableSeconds;

        /// <summary>Whether a line asks: its last mark, past any closing quote, is a question mark.</summary>
        public static bool IsQuestion(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            for (int at = line.Length - 1; at >= 0; at--)
            {
                char c = line[at];
                if (char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '\u201D' || c == '\u2019') continue;
                return c == '?';
            }
            return false;
        }

        /// <summary>The samples a plan renders to at <paramref name="sampleRate"/>.</summary>
        public static int SampleCount(Plan plan, int sampleRate) =>
            plan == null || plan.Syllables == 0 ? 0 : (int)Math.Ceiling(plan.Seconds * sampleRate - 1e-9);

        /// <summary>The longest render at <paramref name="sampleRate"/>: what a buffer for any murmur holds.</summary>
        public static int MaxSampleCount(int sampleRate) => (int)Math.Ceiling(MaxSeconds * sampleRate - 1e-9);

        /// <summary>A plan rendered into a new buffer of its own length.</summary>
        public static float[] Render(Plan plan, int sampleRate)
        {
            var samples = new float[SampleCount(plan, sampleRate)];
            Render(plan, sampleRate, samples);
            return samples;
        }

        /// <summary>
        /// A plan rendered into <paramref name="into"/> from its start, the rest of it cleared; returns
        /// the samples written. Mono, at <paramref name="sampleRate"/>: a voice of three harmonics and a
        /// breath, each syllable shaped by a squared sine, so the murmur starts and ends in silence.
        /// </summary>
        public static int Render(Plan plan, int sampleRate, float[] into)
        {
            if (into == null) return 0;
            Array.Clear(into, 0, into.Length);
            int count = Math.Min(SampleCount(plan, sampleRate), into.Length);
            if (count <= 0 || sampleRate <= 0) return 0;
            int syllables = plan.Syllables;
            var pitch = new double[syllables];
            var bright = new double[syllables];
            var loud = new double[syllables];
            for (int i = 0; i < syllables; i++)
            {
                // Speech drifts down over a line; each syllable sits a little off it; a question lifts at its end.
                double fall = syllables > 1 ? -2.0 * i / (syllables - 1) : 0.0;
                double wobble = (Unit(plan.LineHash, i, 1) - 0.5) * 3.0;
                double rise = !plan.Question ? 0.0 : i == syllables - 1 ? 4.0 : i == syllables - 2 ? 1.5 : 0.0;
                pitch[i] = plan.PitchHz * Math.Pow(2.0, (fall + wobble + rise) / 12.0);
                // A vowel a syllable: more or less of the second harmonic.
                bright[i] = plan.Brightness * (0.6 + 0.8 * Unit(plan.LineHash, i, 2));
                loud[i] = 0.75 + 0.25 * Unit(plan.LineHash, i, 3);
            }
            double perSyllable = SyllableSeconds * sampleRate, phase = 0.0, breath = 0.0;
            uint noise = plan.LineHash | 1u;
            for (int k = 0; k < count; k++)
            {
                double at = k / perSyllable;
                int i = Math.Min(syllables - 1, (int)at);
                double u = (at - i) / VoicedShare;
                double shape = u < 1.0 ? Math.Sin(Math.PI * u) : 0.0;
                double envelope = shape * shape;
                // The pitch glides from each syllable's centre to the next, with no step to click.
                double centre = at - 0.4;
                double frequency;
                if (centre <= 0.0) frequency = pitch[0];
                else if (centre >= syllables - 1) frequency = pitch[syllables - 1];
                else
                {
                    int a = (int)centre;
                    frequency = pitch[a] + (pitch[a + 1] - pitch[a]) * (centre - a);
                }
                phase += 2.0 * Math.PI * frequency / sampleRate;
                if (phase >= 2.0 * Math.PI) phase -= 2.0 * Math.PI;
                double b = bright[i];
                double voice = (Math.Sin(phase) + b * Math.Sin(2.0 * phase) + 0.12 * Math.Sin(3.0 * phase)) / Math.Sqrt(1.0 + b * b + 0.0144);
                unchecked
                {
                    noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                }
                double white = noise / 4294967295.0 * 2.0 - 1.0;
                breath += (white - breath) * 0.2;
                into[k] = (float)((voice + breath * BreathMix) * envelope * loud[i] * Level);
            }
            return count;
        }

        /// <summary>A number from 0 to 1 for syllable <paramref name="index"/> of a line, from its hash: murmur3's finaliser over the mix.</summary>
        private static double Unit(uint lineHash, int index, uint salt)
        {
            unchecked
            {
                uint x = lineHash ^ ((uint)index * 0x9E3779B9u) ^ (salt * 0x7FEB352Du);
                x ^= x >> 16; x *= 0x85EBCA6Bu;
                x ^= x >> 13; x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return x / 4294967296.0;
            }
        }
    }
}
