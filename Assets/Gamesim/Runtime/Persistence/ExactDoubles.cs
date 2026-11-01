using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Float literals read back as the doubles the writer wrote them for, whatever runtime parses them
    /// (OLD-SAVE-ACCEPTANCE F9).
    ///
    /// <para>Json.NET's JsonTextReader turns a float literal into a double with the runtime's
    /// double.TryParse. On .NET that is correctly rounded. Unity's Mono parses the way the old .NET
    /// Framework did, which is not: "3.4694441460073", the text the writer gives the double
    /// 0x400bc16beeb33334 and its shortest text, reads back as 0x400bc16beeb33335. The save stores hash
    /// the canonical form of what they read against the checksum of what was written, so one such value
    /// made a save, a load or a backup recovery refuse the file as damaged.</para>
    ///
    /// <para>Mono's writer is not always right either: it gives 0x4019a6e82949a566 the text "6.412995",
    /// whose nearest double is 0x4019a6e82949a565 (measured under the editor's Mono, about one double in
    /// 2000 of some kinds). The nearest double alone would then write "6.4129949999999996" and refuse the
    /// file as surely. What a checksum needs is a value the writer writes as the file's text, so
    /// <see cref="Correct"/> keeps the value Json.NET read when the writer writes it as its literal
    /// (every file that loads today loads exactly as before); otherwise it takes the nearest double if the
    /// writer writes that as the literal, or the double closest to it that the writer does (within 16),
    /// or else the nearest double. On .NET that is always the value read.</para>
    ///
    /// <para><see cref="Correct"/> pairs every number value of the token tree, in document order, with
    /// the number literals of the text. Its scan follows what Json.NET reads here beyond plain JSON:
    /// single- and double-quoted strings with escapes, // and /* */ comments, unquoted names,
    /// true/false/null/undefined, NaN, Infinity and -Infinity, and constructors. When the text and the
    /// tree do not pair one to one, a float literal is not a decimal number or lies far from the value
    /// read, it changes nothing: the text loads exactly as Json.NET read it. NaN and the infinities are
    /// never touched, for validation to refuse as before.</para>
    ///
    /// <para>Unity-free (BCL, System.Numerics and Json.NET only), so the dotnet subset compiles it.
    /// There the runtime reads correctly and this changes nothing; only Unity's Mono shows what it
    /// repairs.</para>
    /// </summary>
    public static class ExactDoubles
    {
        // How far, in doubles, the walk from the runtime's value to the nearest one may go. Mono's
        // misreads are a unit or two in the last place; a pairing gone wrong is far further.
        private const int MaximumSteps = 64;

        // Significant digits kept before the rest folds into one sticky digit. A midpoint between two
        // doubles has at most 767 significant digits, so this keeps every comparison exact.
        private const int KeptDigits = 800;

        // How far from the nearest double the pass looks for one the writer writes as the literal.
        private const int SettleDistance = 16;

        private static readonly double[] ExactPowersOfTen =
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
            1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22
        };

        private static readonly double NegativeZero = BitConverter.Int64BitsToDouble(long.MinValue);

        /// <summary>
        /// Puts in every finite float value of <paramref name="root"/>, which Json.NET read from
        /// <paramref name="text"/>, a double the writer writes as its literal, nearest first (see the
        /// class). Returns how many values changed, or -1 when it left every value as Json.NET read it:
        /// the text and the tree do not pair, a float's literal is not a decimal number, or one lies
        /// more than 64 doubles from the value read. All or nothing.
        /// </summary>
        public static int Correct(JToken root, string text)
        {
            if (root == null || text == null) return -1;
            var literals = new List<Literal>();
            if (!Scan(text, literals)) return -1;
            var numbers = new List<JValue>(literals.Count);
            CollectNumbers(root, numbers);
            if (numbers.Count != literals.Count) return -1;

            List<KeyValuePair<JValue, double>> corrections = null;
            for (int i = 0; i < numbers.Count; i++)
            {
                var literal = literals[i];
                var number = numbers[i];
                if (!(number.Value is double read))
                {
                    // An integer, or a float read as a decimal: nothing to correct, but it must pair.
                    if (literal.Kind != LiteralKind.Number || number.Type == JTokenType.Integer && !IntegerText(text, literal))
                        return -1;
                    continue;
                }
                if (double.IsNaN(read))
                {
                    if (literal.Kind != LiteralKind.NaN) return -1;
                    continue;
                }
                if (double.IsInfinity(read))
                {
                    // An infinity word, or a literal past the largest double that the runtime read as infinite.
                    var word = read > 0 ? LiteralKind.PositiveInfinity : LiteralKind.NegativeInfinity;
                    if (literal.Kind != word && literal.Kind != LiteralKind.Number) return -1;
                    continue;
                }
                if (literal.Kind != LiteralKind.Number) return -1;
                // The writer writes the value read as its own literal: its canonical form is the file's.
                if (Written(read, text, literal)) continue;
                if (!TryParse(text, literal.Start, literal.Length, read, false, out double nearest)) return -1;
                // A finite value is never made infinite: past the largest double it stays as read.
                if (double.IsInfinity(nearest)) continue;
                double settled = Settle(nearest, text, literal);
                if (BitConverter.DoubleToInt64Bits(settled) == BitConverter.DoubleToInt64Bits(read)) continue;
                // A literal far from the value it was paired with is a pairing gone wrong.
                if (Math.Abs(Ordered(settled) - Ordered(read)) > MaximumSteps) return -1;
                (corrections ??= new List<KeyValuePair<JValue, double>>()).Add(new KeyValuePair<JValue, double>(number, settled));
            }

            if (corrections == null) return 0;
            foreach (var correction in corrections) correction.Key.Value = correction.Value;
            return corrections.Count;
        }

        /// <summary>
        /// The double nearest to a decimal literal (an optional '-', digits with an optional point,
        /// an optional exponent), ties to even, the sign of zero kept; infinite past the largest
        /// double. False for any other text.
        /// </summary>
        public static bool TryParse(string literal, out double value)
        {
            value = 0;
            if (literal == null) return false;
            // The runtime's own parse is the walk's start; it fails only past the largest double.
            double start = double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed : double.MaxValue;
            return TryParse(literal, 0, literal.Length, start, false, out value);
        }

        /// <summary>
        /// <see cref="TryParse(string, out double)"/> by the exact walk alone, from
        /// <paramref name="start"/> to the nearest double. False when that is more than 64 doubles away.
        /// </summary>
        public static bool TryParseFrom(string literal, double start, out double value)
        {
            value = 0;
            return literal != null && TryParse(literal, 0, literal.Length, start, true, out value);
        }

        // The double nearest to the literal if the writer writes it as the literal, or else the double
        // closest to it that the writer does write so (within 16), or else the nearest one.
        private static double Settle(double nearest, string text, Literal literal)
        {
            if (Written(nearest, text, literal)) return nearest;
            long place = Ordered(nearest);
            for (int distance = 1; distance <= SettleDistance; distance++)
            {
                double below = FromOrdered(place - distance), above = FromOrdered(place + distance);
                if (!double.IsInfinity(below) && !double.IsNaN(below) && Written(below, text, literal)) return below;
                if (!double.IsInfinity(above) && !double.IsNaN(above) && Written(above, text, literal)) return above;
            }
            return nearest;
        }

        private static bool Written(double value, string text, Literal literal) =>
            SameText(JsonConvert.ToString(value), text, literal.Start, literal.Length);

        private static bool TryParse(string s, int offset, int length, double start, bool walk, out double value)
        {
            value = 0;
            int end = offset + length, i = offset;
            bool negative = i < end && s[i] == '-';
            if (negative) i++;
            int intStart = i;
            while (i < end && IsDigit(s[i])) i++;
            int intEnd = i, fracStart = i, fracEnd = i;
            if (i < end && s[i] == '.')
            {
                fracStart = ++i;
                while (i < end && IsDigit(s[i])) i++;
                fracEnd = i;
            }
            if (intEnd == intStart && fracEnd == fracStart) return false;
            long exponent = 0;
            if (i < end && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                bool negativeExponent = i < end && s[i] == '-';
                if (i < end && (s[i] == '-' || s[i] == '+')) i++;
                int exponentStart = i;
                for (; i < end && IsDigit(s[i]); i++)
                    if (exponent < 1000000000) exponent = exponent * 10 + (s[i] - '0');
                if (i == exponentStart) return false;
                if (negativeExponent) exponent = -exponent;
            }
            if (i != end) return false;

            // The digits run on from the integer part into the fraction; the value is digits x 10^scale.
            int intLength = intEnd - intStart, total = intLength + (fracEnd - fracStart);
            int Digit(int k) => (k < intLength ? s[intStart + k] : s[fracStart + k - intLength]) - '0';
            int first = 0;
            while (first < total && Digit(first) == 0) first++;
            if (first == total)
            {
                value = negative ? NegativeZero : 0.0;
                return true;
            }
            int last = total - 1;
            while (Digit(last) == 0) last--;
            int count = last - first + 1;
            long scale = exponent - (fracEnd - fracStart) + (total - 1 - last);

            // Clinger's fast path: both operands are exact doubles, so one operation rounds correctly.
            if (!walk && count <= 15 && scale >= -22 && scale <= 22)
            {
                long whole = 0;
                for (int k = first; k <= last; k++) whole = whole * 10 + Digit(k);
                double magnitude = scale >= 0 ? whole * ExactPowersOfTen[scale] : whole / ExactPowersOfTen[-scale];
                value = negative ? -magnitude : magnitude;
                return true;
            }
            if (count + scale <= -324)
            {
                // Below 1e-324, under half the smallest double: zero.
                value = negative ? NegativeZero : 0.0;
                return true;
            }
            if (count - 1 + scale >= 309)
            {
                value = negative ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            }
            int kept = Math.Min(count, KeptDigits);
            var chars = new char[count > KeptDigits ? kept + 1 : kept];
            for (int k = 0; k < kept; k++) chars[k] = (char)('0' + Digit(first + k));
            if (count > KeptDigits)
            {
                // Every digit past the kept ones folds into a final 1: more than the kept digits, less than
                // the next kept value, which is all a comparison with a midpoint can tell apart.
                chars[kept] = '1';
                scale += count - KeptDigits - 1;
            }
            var digits = BigInteger.Parse(new string(chars), NumberStyles.None, CultureInfo.InvariantCulture);
            int power = (int)scale;
            var scaledDigits = power >= 0 ? digits * BigInteger.Pow(10, power) : digits;
            var scaledPower = power >= 0 ? BigInteger.One : BigInteger.Pow(10, -power);

            double candidate = Math.Abs(start);
            if (double.IsNaN(candidate)) return false;
            if (double.IsInfinity(candidate)) candidate = double.MaxValue;
            for (int step = 0; step <= MaximumSteps; step++)
            {
                long bits = BitConverter.DoubleToInt64Bits(candidate);
                int biased = (int)(bits >> 52) & 0x7ff;
                long fraction = bits & 0xfffffffffffffL;
                long mantissa = biased == 0 ? fraction : fraction | 1L << 52;
                int binary = biased == 0 ? -1074 : biased - 1075;
                bool odd = (mantissa & 1) != 0;
                // Past the midpoint above, (2m + 1) x 2^(e - 1), or on it with an odd mantissa: up.
                int above = Compare(scaledDigits, scaledPower, 2 * mantissa + 1, binary - 1);
                if (above > 0 || above == 0 && odd)
                {
                    if (candidate == double.MaxValue)
                    {
                        value = negative ? double.NegativeInfinity : double.PositiveInfinity;
                        return true;
                    }
                    candidate = BitConverter.Int64BitsToDouble(bits + 1);
                    continue;
                }
                if (mantissa != 0)
                {
                    // The midpoint below; at a power of two the double below is half as far away.
                    int below = mantissa == 1L << 52 && biased > 1
                        ? Compare(scaledDigits, scaledPower, 4 * mantissa - 1, binary - 2)
                        : Compare(scaledDigits, scaledPower, 2 * mantissa - 1, binary - 1);
                    if (below < 0 || below == 0 && odd)
                    {
                        candidate = BitConverter.Int64BitsToDouble(bits - 1);
                        continue;
                    }
                }
                value = negative ? -candidate : candidate;
                return true;
            }
            return false;
        }

        // The sign of digits x 10^power against n x 2^exponent, as scaledDigits against n x scaledPower,
        // both multiplied out to whole numbers.
        private static int Compare(BigInteger scaledDigits, BigInteger scaledPower, long n, int exponent)
        {
            var left = scaledDigits;
            var right = scaledPower * n;
            if (exponent >= 0) right <<= exponent;
            else left <<= -exponent;
            return left.CompareTo(right);
        }

        // The integer and float values in document order (Json.NET's reader keeps the depth to 64 or its MaxDepth).
        private static void CollectNumbers(JToken token, List<JValue> numbers)
        {
            if (token is JContainer container)
            {
                foreach (var child in container.Children()) CollectNumbers(child, numbers);
                return;
            }
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) numbers.Add((JValue)token);
        }

        // A finite double's place in the order of all doubles, the two zeros together: neighbours differ by one.
        private static long Ordered(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            return bits < 0 ? long.MinValue - bits : bits;
        }

        private static double FromOrdered(long place) => BitConverter.Int64BitsToDouble(place < 0 ? long.MinValue - place : place);

        private static bool SameText(string written, string s, int offset, int length) =>
            written.Length == length && string.CompareOrdinal(written, 0, s, offset, length) == 0;

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        // Json.NET reads a literal as an integer only without a point or an exponent, or in hexadecimal.
        private static bool IntegerText(string text, Literal literal)
        {
            bool hexadecimal = literal.Length > 1 && text[literal.Start] == '0' && (text[literal.Start + 1] == 'x' || text[literal.Start + 1] == 'X');
            for (int i = literal.Start; i < literal.Start + literal.Length; i++)
                if (text[i] == '.' || !hexadecimal && (text[i] == 'e' || text[i] == 'E')) return false;
            return true;
        }

        private enum LiteralKind : byte { Number, NaN, PositiveInfinity, NegativeInfinity }

        private struct Literal
        {
            public int Start, Length;
            public LiteralKind Kind;
        }

        // The number literals of a text Json.NET has read, in document order, names and strings skipped.
        // False for anything it does not follow: then nothing is paired and nothing changes.
        private static bool Scan(string text, List<Literal> literals)
        {
            var containers = new List<bool>(); // true for an object, where a name follows { and ,
            bool expectName = false;
            int i = 0, n = text.Length;
            while (i < n)
            {
                char c = text[i];
                switch (c)
                {
                    case '"':
                    case '\'':
                        if (!SkipString(text, ref i)) return false;
                        expectName = false;
                        continue;
                    case '/':
                        if (!SkipComment(text, ref i)) return false;
                        continue;
                    case '{':
                        containers.Add(true);
                        expectName = true;
                        i++;
                        continue;
                    case '[':
                    case '(':
                        containers.Add(false);
                        expectName = false;
                        i++;
                        continue;
                    case '}':
                    case ']':
                    case ')':
                        if (containers.Count == 0) return false;
                        containers.RemoveAt(containers.Count - 1);
                        expectName = false;
                        i++;
                        continue;
                    case ',':
                        expectName = containers.Count > 0 && containers[containers.Count - 1];
                        i++;
                        continue;
                    case ':':
                        expectName = false;
                        i++;
                        continue;
                }
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }
                int start = i;
                if (expectName)
                {
                    // An unquoted name, in Json.NET's identifier characters.
                    while (i < n && IsNameChar(text[i])) i++;
                    if (i == start) return false;
                    expectName = false;
                    continue;
                }
                if (c == '-' && i + 1 < n && text[i + 1] == 'I')
                {
                    i++;
                    while (i < n && IsNameChar(text[i])) i++;
                    if (i - start != 9 || string.CompareOrdinal(text, start, "-Infinity", 0, 9) != 0) return false;
                    literals.Add(new Literal { Start = start, Length = 9, Kind = LiteralKind.NegativeInfinity });
                    continue;
                }
                if (c == '-' || c == '.' || IsDigit(c))
                {
                    while (i < n && IsNumberChar(text[i])) i++;
                    literals.Add(new Literal { Start = start, Length = i - start, Kind = LiteralKind.Number });
                    continue;
                }
                if (IsNameChar(c))
                {
                    // true, false, null, undefined, new and a constructor's name, or a NaN or Infinity.
                    while (i < n && IsNameChar(text[i])) i++;
                    int length = i - start;
                    if (length == 3 && string.CompareOrdinal(text, start, "NaN", 0, 3) == 0)
                        literals.Add(new Literal { Start = start, Length = 3, Kind = LiteralKind.NaN });
                    else if (length == 8 && string.CompareOrdinal(text, start, "Infinity", 0, 8) == 0)
                        literals.Add(new Literal { Start = start, Length = 8, Kind = LiteralKind.PositiveInfinity });
                    continue;
                }
                return false;
            }
            return containers.Count == 0;
        }

        private static bool SkipString(string text, ref int i)
        {
            char quote = text[i++];
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '\\') i += 2;
                else if (c == quote)
                {
                    i++;
                    return true;
                }
                else i++;
            }
            return false;
        }

        private static bool SkipComment(string text, ref int i)
        {
            if (i + 1 >= text.Length) return false;
            if (text[i + 1] == '*')
            {
                int close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0) return false;
                i = close + 2;
                return true;
            }
            if (text[i + 1] != '/') return false;
            i += 2;
            while (i < text.Length && text[i] != '\r' && text[i] != '\n') i++;
            return true;
        }

        // The characters Json.NET reads into a number before it decides what the number is.
        private static bool IsNumberChar(char c) =>
            IsDigit(c) || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F' || c == 'x' || c == 'X' || c == '.' || c == '-' || c == '+';

        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';
    }
}
