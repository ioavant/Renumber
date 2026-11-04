using System;

namespace Renumber.Core
{
    /// <summary>A problem the user can act on: shown as a dialog, not reported as a failure.</summary>
    public class RenumberException : Exception
    {
        public RenumberException(string message) : base(message)
        {
        }
    }
}
