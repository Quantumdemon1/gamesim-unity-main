using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The pieces a library look wears because a catalog entry is tagged for it (<c>worn-by:</c> then
    /// the look's appearance id), with no edit to the look itself; and the line of LibraryLooks.txt.
    ///
    /// <para>Plain strings in and out, no engine type, so the dotnet subset tests it. With nothing
    /// tagged for a look, <see cref="Apply"/> hands back the look's own wardrobe, the same array.</para>
    /// </summary>
    public static class LookPieces
    {
        public const string WornBy = "worn-by:";
        public const string Over = "over:";

        /// <summary>A catalog item as Apply reads it: its id, its wardrobe slot, its tags and the races it fits.</summary>
        public sealed class Piece
        {
            public string Id, Slot;
            public string[] Tags = Array.Empty<string>();
            public string[] Races = Array.Empty<string>();
            public bool Fits(string race) => Races.Length == 0 || Races.Contains(race);
        }

        /// <summary>
        /// <paramref name="wardrobe"/> with each piece tagged for <paramref name="lookId"/> that fits
        /// <paramref name="race"/> in place of the look's line in its slot and in that slot's twins
        /// (<see cref="WardrobeSlots.Twins"/>), or appended when the look has nothing there. Per slot the
        /// lowest id (ordinal) wins; a second piece or one that does not fit the race gives one warning
        /// for the look and slot. A piece with <c>over:</c> tags is worn only when the wardrobe, once the
        /// other pieces are in, holds an item one of them names (<paramref name="idOf"/> maps a line or
        /// a tag's id to the one id both are compared by); otherwise it is left off without a word.
        /// </summary>
        public static string[] Apply(string lookId, string race, string[] wardrobe, IReadOnlyList<Piece> pieces,
            Func<string, string> slotOf, Func<string, string> idOf, ICollection<string> warnings)
        {
            if (string.IsNullOrEmpty(lookId) || pieces == null || pieces.Count == 0) return wardrobe;
            string tag = WornBy + lookId;
            List<Piece> mine = null;
            foreach (var piece in pieces)
                if (piece?.Tags != null && Array.IndexOf(piece.Tags, tag) >= 0) (mine = mine ?? new List<Piece>()).Add(piece);
            if (mine == null) return wardrobe;
            idOf = idOf ?? (id => id);
            var worn = new List<string>(wardrobe ?? Array.Empty<string>());
            Dress(lookId, race, worn, mine.Where(piece => !IsOuter(piece)), slotOf, warnings);
            var lines = new HashSet<string>(worn.Select(idOf), StringComparer.Ordinal);
            Dress(lookId, race, worn, mine.Where(piece => IsOuter(piece) && piece.Tags.Any(value =>
                value.StartsWith(Over, StringComparison.Ordinal) && lines.Contains(idOf(value.Substring(Over.Length))))), slotOf, warnings);
            return worn.ToArray();
        }

        private static bool IsOuter(Piece piece) => piece.Tags.Any(value => value.StartsWith(Over, StringComparison.Ordinal));

        private static void Dress(string lookId, string race, List<string> worn, IEnumerable<Piece> pieces,
            Func<string, string> slotOf, ICollection<string> warnings)
        {
            foreach (var slot in pieces.GroupBy(piece => piece.Slot ?? string.Empty, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var fitting = slot.Where(piece => piece.Fits(race)).OrderBy(piece => piece.Id, StringComparer.Ordinal).ToList();
                if (fitting.Count != slot.Count() || fitting.Count > 1)
                    warnings?.Add("look '" + lookId + "' has " + slot.Count() + " pieces tagged for its " + slot.Key + " slot, "
                        + fitting.Count + " cut for " + race + "; it wears " + (fitting.Count == 0 ? "none of them" : "'" + fitting[0].Id + "'"));
                if (fitting.Count == 0) continue;
                var place = new[] { slot.Key }.Concat(WardrobeSlots.Twins(slot.Key)).ToArray();
                int at = worn.FindIndex(line => place.Contains(slotOf?.Invoke(line)));
                if (at < 0) { worn.Add(fitting[0].Id); continue; }
                worn[at] = fitting[0].Id;
                for (int i = worn.Count - 1; i > at; i--)
                    if (place.Contains(slotOf?.Invoke(worn[i]))) worn.RemoveAt(i);
            }
        }

        /// <summary>
        /// One line of Assets/Gamesim/Uma/LibraryLooks.txt: the look's id, a tab, its race, then for each
        /// DNA name (sorted ordinal) a tab and name=value, the value in the invariant culture with no
        /// exponent, in the fewest decimals (at most six) that read back to exactly the same float. A value
        /// that would need rounding throws rather than being written. No line end.
        ///
        /// <para>The boundary hash of a look (posted by the lead, compared by Codex's checker): the first 16
        /// hex digits, lower case, of the SHA-256 of this line's UTF-8 bytes, without its line end.</para>
        /// </summary>
        public static string LibraryLine(string id, string race, IEnumerable<KeyValuePair<string, float>> dna)
        {
            var fields = new List<string> { Field(id), Field(race) };
            foreach (var value in dna.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                fields.Add(Field(value.Key) + "=" + Number(value.Value, value.Key));
            return string.Join("\t", fields);
        }

        private static string Field(string text) =>
            string.IsNullOrEmpty(text) || text.IndexOfAny(new[] { '\t', '\r', '\n', '=' }) >= 0
                ? throw new ArgumentException("not a LibraryLooks.txt field: '" + text + "'") : text;

        private static string Number(float value, string name)
        {
            for (int places = 0; places <= 6 && !float.IsNaN(value) && !float.IsInfinity(value); places++)
            {
                string text = value.ToString(places == 0 ? "0" : "0." + new string('#', places), CultureInfo.InvariantCulture);
                if (text == "-0") text = "0";
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var back) && back == value) return text;
            }
            throw new ArgumentException(name + "=" + value.ToString("R", CultureInfo.InvariantCulture) + " needs rounding to be written");
        }
    }
}
