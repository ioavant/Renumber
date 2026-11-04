using System.Collections.Generic;

namespace Renumber.Core
{
    public enum NumberingMode
    {
        /// <summary>Every element in scope gets a fresh number, from the start, in order.</summary>
        RenumberAll,

        /// <summary>Only elements without a number; they continue after the highest one in use.</summary>
        ContinueAfterLast,

        /// <summary>Only elements without a number; numbers freed by deleted elements are reused first.</summary>
        FillGaps,

        /// <summary>
        /// Values in the old format get the same number in the new one: F02 → F-02.
        /// Nothing is renumbered, so this mode never uses the allocator.
        /// </summary>
        Reformat,
    }

    /// <summary>
    /// Hands out numbers on the grid start, start+step, start+2·step, … skipping every
    /// number already taken. The mode decides where on the grid it begins.
    /// </summary>
    /// <remarks>
    /// This is the piece the planned background updater will reuse as is: there the
    /// "elements to number" are the ones just added to the model, and the taken set is
    /// everything else in the category.
    /// </remarks>
    public sealed class NumberAllocator
    {
        private readonly int _step;
        private readonly HashSet<int> _taken;
        private int _next;

        public NumberAllocator(NumberFormat format, IEnumerable<int> taken, NumberingMode mode)
        {
            _step = format.Step;
            _next = format.Start;

            // Renumbering everything means the old numbers are about to disappear, so
            // none of them blocks the sequence.
            _taken = mode == NumberingMode.RenumberAll ? new HashSet<int>() : new HashSet<int>(taken);

            if (mode == NumberingMode.ContinueAfterLast)
            {
                // Numbers below the start belong to some other series (or to a person
                // who typed them by hand) and do not move the starting point.
                bool any = false;
                int last = 0;
                foreach (int n in _taken)
                {
                    if (n >= format.Start && (!any || n > last))
                    {
                        last = n;
                        any = true;
                    }
                }
                if (any)
                    _next = last + _step;
            }
        }

        public int Next()
        {
            while (_taken.Contains(_next))
                _next += _step;

            int number = _next;
            _taken.Add(number);
            _next += _step;
            return number;
        }
    }
}
