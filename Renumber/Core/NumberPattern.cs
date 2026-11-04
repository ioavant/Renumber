using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Renumber.Core
{
    /// <summary>
    /// A numbering format read off existing values: "F02", "F03", "F04" are prefix "F",
    /// two digits, no suffix. Lets the window offer what is already in the model instead
    /// of making the user retype it, and is the "from" side of a format change.
    /// </summary>
    /// <remarks>
    /// Revit-free, like <see cref="NumberFormat"/>. The number is the LAST run of digits
    /// in a value, so "AHU-1-03" is prefix "AHU-1-" and 3.
    /// </remarks>
    public sealed class NumberPattern
    {
        public string Prefix { get; private set; }
        public string Suffix { get; private set; }

        /// <summary>The shortest digit run seen: "F02 … F10, F100" is two digits, padded.</summary>
        public int Width { get; private set; }

        public int Count { get; private set; }
        public int Min { get; private set; }
        public int Max { get; private set; }

        /// <summary>"F##", "AHU-#-A".</summary>
        public string Mask => Prefix + new string('#', Width) + Suffix;

        /// <summary>A start number typed the way the format pads it: width 2 gives "01".</summary>
        public string PaddedStart(int start) => start.ToString(new string('0', Width), CultureInfo.InvariantCulture);

        public override string ToString() => $"{Mask} ×{Count}";

        /// <summary>
        /// Every pattern among the values, most common first. Prefix and suffix compare
        /// case-insensitively, as <see cref="NumberFormat.TryParse"/> does; the spelling
        /// kept is the one seen most often. Values without digits are not numbers and are
        /// left out.
        /// </summary>
        public static List<NumberPattern> Detect(IEnumerable<string> values)
        {
            var groups = new Dictionary<string, List<Split>>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                if (!TrySplit(value, out Split split))
                    continue;

                string key = split.Prefix + "\u0000" + split.Suffix;
                if (!groups.TryGetValue(key, out List<Split> list))
                    groups[key] = list = new List<Split>();
                list.Add(split);
            }

            return groups.Values
                .Select(list => new NumberPattern
                {
                    Prefix = MostCommon(list.Select(s => s.Prefix)),
                    Suffix = MostCommon(list.Select(s => s.Suffix)),
                    Width = list.Min(s => s.Digits.Length),
                    Count = list.Count,
                    Min = list.Min(s => s.Number),
                    Max = list.Max(s => s.Number),
                })
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.Prefix, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private struct Split
        {
            public string Prefix;
            public string Digits;
            public string Suffix;
            public int Number;
        }

        private static bool TrySplit(string value, out Split split)
        {
            split = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            int end = value.Length - 1;
            while (end >= 0 && !IsDigit(value[end]))
                end--;
            if (end < 0)
                return false;

            int start = end;
            while (start > 0 && IsDigit(value[start - 1]))
                start--;

            string digits = value.Substring(start, end - start + 1);
            if (digits.Length > 9)
                return false;

            split = new Split
            {
                Prefix = value.Substring(0, start),
                Digits = digits,
                Suffix = value.Substring(end + 1),
                Number = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture),
            };
            return true;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static string MostCommon(IEnumerable<string> spellings)
        {
            return spellings
                .GroupBy(s => s, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .First().Key;
        }
    }
}
