using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Gamesim.Simulation
{
    /// <summary>
    /// E3's transient choice of a memory the player actually knows. No saved field, arbitrary
    /// command-supplied prose, hidden ballot or NPC-owned memory becomes sharing authority.
    /// References bind one exact record, recipient and revision; they are not credentials.
    /// </summary>
    public sealed class InformationShareChoice
    {
        public const string ReferencePrefix = "memory-v1:", ReceiptPrefix = "Heard from you: ";
        public const int ReferenceLimit = 80, MemoryLimit = 2000;

        public sealed class Option
        {
            public string Reference { get; }
            public string SubjectId { get; }
            public string Text { get; }
            public string ReceivedText { get; }
            public int Week { get; }
            public bool Shortened => ReceiptPrefix.Length + Text.Length > MemoryLimit;
            internal Option(string reference, MemoryState memory)
            {
                Reference = reference; SubjectId = memory.subjectId; Text = memory.text;
                Week = memory.week; ReceivedText = Receipt(memory);
            }
        }

        private readonly string session, player;
        private readonly int revision;
        private readonly EpisodePhase phase;
        public string RecipientId { get; }
        public IReadOnlyList<Option> Options { get; }

        private InformationShareChoice(EpisodeState state, string recipient)
        {
            session = state.sessionId; player = state.playerId; revision = state.revision;
            phase = state.phase; RecipientId = recipient;
            var known = new HashSet<MemoryState>(KnownBallots.PlayerMemories(state));
            var options = new List<Option>();
            // Newest first. Original indices and content remain intact, even for equal texts.
            for (int i = state.memories.Count - 1; i >= 0; i--)
                if (Eligible(state.memories[i], recipient, known))
                    options.Add(new Option(ReferenceFor(state, recipient, i), state.memories[i]));
            Options = Array.AsReadOnly(options.ToArray());
        }

        public static bool Available(EpisodeState state, string recipient) => EpisodeEngine.EconomyRulesOn(state)
            && state.Find(state.playerId)?.status == ContestantStatus.Active
            && recipient != state.playerId && state.Find(recipient)?.status == ContestantStatus.Active
            && EpisodeEngine.ConversationWindowRefusal(state, recipient, EpisodeCommandKind.ShareInformation) == null
            && EpisodeEngine.SocialActionsSpent(state) < EpisodeEngine.SocialActionBudget(state);

        public static InformationShareChoice Open(EpisodeState state, string recipient) =>
            Available(state, recipient) ? new InformationShareChoice(state, recipient) : null;

        public bool IsCurrent(EpisodeState state) => Available(state, RecipientId)
            && state.sessionId == session && state.playerId == player && state.revision == revision && state.phase == phase;

        public bool CanChoose(EpisodeState state, string reference) => IsCurrent(state)
            && Options.Any(option => option.Reference == reference)
            && TryResolve(state, RecipientId, reference, out _);

        private static bool Eligible(MemoryState memory, string recipient, HashSet<MemoryState> known) =>
            memory != null && memory.subjectId != recipient && known.Contains(memory);

        /// <summary>Resolve against current authoritative state, never accept caller-supplied text.</summary>
        public static bool TryResolve(EpisodeState state, string recipient, string reference, out MemoryState memory)
        {
            memory = null;
            if (!Available(state, recipient) || string.IsNullOrEmpty(reference) || reference.Length > ReferenceLimit
                || !reference.StartsWith(ReferencePrefix, StringComparison.Ordinal)) return false;
            int split = reference.IndexOf(':', ReferencePrefix.Length);
            if (split < 0 || !int.TryParse(reference.Substring(ReferencePrefix.Length, split - ReferencePrefix.Length),
                    NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || index < 0 || index >= state.memories.Count) return false;
            var candidate = state.memories[index];
            if (!Eligible(candidate, recipient, new HashSet<MemoryState>(KnownBallots.PlayerMemories(state)))
                || !string.Equals(reference, ReferenceFor(state, recipient, index), StringComparison.Ordinal)) return false;
            memory = candidate.Clone();
            return true;
        }

        private static string ReferenceFor(EpisodeState state, string recipient, int index)
        {
            var memory = state.memories[index];
            var value = new StringBuilder();
            void Add(string part)
            {
                value.Append((part?.Length ?? -1).ToString(CultureInfo.InvariantCulture)).Append(':').Append(part);
            }
            Add(state.sessionId); Add(state.playerId); Add(recipient);
            Add(state.revision.ToString(CultureInfo.InvariantCulture)); Add(((int)state.phase).ToString(CultureInfo.InvariantCulture));
            Add(index.ToString(CultureInfo.InvariantCulture)); Add(memory.ownerId); Add(memory.subjectId);
            Add(memory.week.ToString(CultureInfo.InvariantCulture)); Add(memory.isPrivate ? "1" : "0"); Add(memory.text);
            // Exact UTF-16 code units, including malformed surrogates in an old record. Encoding
            // replacement fallbacks must not let two different texts have the same reference.
            var bytes = new byte[value.Length * 2];
            for (int i = 0; i < value.Length; i++) { bytes[2 * i] = (byte)value[i]; bytes[2 * i + 1] = (byte)(value[i] >> 8); }
            using (var hash = SHA256.Create())
                return ReferencePrefix + index.ToString(CultureInfo.InvariantCulture) + ":"
                    + BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>The exact bounded receipt previewed before confirmation. The source is never changed.</summary>
        public static string Receipt(MemoryState memory)
        {
            string text = memory.text;
            int room = MemoryLimit - ReceiptPrefix.Length;
            if (text.Length <= room) return ReceiptPrefix + text;
            int length = room - 1;
            if (length > 0 && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length])) length--;
            return ReceiptPrefix + text.Substring(0, length) + "…";
        }
    }
}
