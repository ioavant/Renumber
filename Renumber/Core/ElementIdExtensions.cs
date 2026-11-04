using System;
using Autodesk.Revit.DB;

namespace Renumber.Core
{
    /// <summary>
    /// ElementId's numeric value across the API change: Revit 2024 added Value (long) and
    /// Revit 2026 removed IntegerValue (int). The net48 assembly serves 2022-2024 and the
    /// net8 one 2025 onward, so the getter is resolved once by reflection — the same
    /// helper as in the other Vixeldorf add-ins.
    /// </summary>
    internal static class ElementIdExtensions
    {
        private static readonly Func<ElementId, long> Getter = BuildGetter();

        private static Func<ElementId, long> BuildGetter()
        {
            // Revit 2024+: property is called "Value" and returns long
            var valueProp = typeof(ElementId).GetProperty("Value");
            if (valueProp != null && valueProp.PropertyType == typeof(long))
                return id => (long)valueProp.GetValue(id, null);

            // Revit 2022-2023: property is called "IntegerValue" and returns int
            var intProp = typeof(ElementId).GetProperty("IntegerValue");
            if (intProp != null)
                return id => (int)intProp.GetValue(id, null);

            throw new InvalidOperationException(
                "ElementId has neither Value nor IntegerValue - unsupported Revit version.");
        }

        public static long GetValue(this ElementId id) => Getter(id);

        /// <summary>ElementId from a stored value: ElementId(long) only exists from Revit 2024 on.</summary>
#if NETFRAMEWORK
        public static ElementId FromValue(long value) => new ElementId((int)value);
#else
        public static ElementId FromValue(long value) => new ElementId(value);
#endif
    }
}
