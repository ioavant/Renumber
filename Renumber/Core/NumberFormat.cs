using System;
using System.Globalization;

namespace Renumber.Core
{
    /// <summary>
    /// Prefix + zero-padded number + suffix: "F-" and 7 give "F-07". The padding is
    /// taken from how the start number is typed — "01" pads to two digits, "1" does
    /// not pad at all — so the user describes the format by example.
    /// </summary>
    /// <remarks>
    /// Revit-free on purpose, like <see cref="NumberAllocator"/>: both can be exercised
    /// outside Revit.
    /// </remarks>
    public sealed class NumberFormat
    {
        public string Prefix { get; }
        public string Suffix { get; }
        public int Start { get; }
        public int Step { get; }
        public int Width { get; }

        private NumberFormat(string prefix, string suffix, int start, int step, int width)
        {
            Prefix = prefix;
            Suffix = suffix;
            Start = start;
            Step = step;
            Width = width;
        }

        public static bool TryCreate(string prefix, string start, string step, string suffix,
                                     out NumberFormat format, out string error)
        {
            format = null;
            start = (start ?? "").Trim();
            step = (step ?? "").Trim();

            if (!IsDigits(start))
            {
                error = "Start number must be digits, e.g. 01.";
                return false;
            }
            if (start.Length > 9 || !int.TryParse(start, NumberStyles.None, CultureInfo.InvariantCulture, out int first))
            {
                error = "Start number is too long.";
                return false;
            }
            if (!int.TryParse(step, NumberStyles.None, CultureInfo.InvariantCulture, out int increment) || increment < 1)
            {
                error = "Step must be a whole number, 1 or more.";
                return false;
            }

            error = null;
            format = new NumberFormat(prefix ?? "", suffix ?? "", first, increment, start.Length);
            return true;
        }

        /// <summary>
        /// A format used only to read numbers back — the old side of a format change.
        /// Padding does not matter when reading, so no start number is needed.
        /// </summary>
        public static NumberFormat ForReading(string prefix, string suffix)
        {
            return new NumberFormat((prefix ?? "").Trim(), (suffix ?? "").Trim(), 1, 1, 1);
        }

        public string Format(int number)
        {
            return Prefix + number.ToString(new string('0', Width), CultureInfo.InvariantCulture) + Suffix;
        }

        /// <summary>
        /// Reads the number back out of a value in this format. Padding is not required
        /// ("F-7" and "F-07" are both 7) and case is ignored, because to Revit's duplicate
        /// check and to a person reading the drawing those are the same mark.
        /// </summary>
        public bool TryParse(string value, out int number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            if (value.Length <= Prefix.Length + Suffix.Length
                || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                || !value.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
                return false;

            string digits = value.Substring(Prefix.Length, value.Length - Prefix.Length - Suffix.Length);
            return digits.Length <= 9
                && IsDigits(digits)
                && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        private static bool IsDigits(string text)
        {
            if (text.Length == 0)
                return false;
            foreach (char c in text)
                if (c < '0' || c > '9')
                    return false;
            return true;
        }
    }
}
