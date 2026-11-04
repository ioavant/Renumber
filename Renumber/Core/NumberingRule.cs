using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Renumber.Core
{
    public enum ElementScope
    {
        WholeProject,
        ActiveView,
        Selection,
    }

    public enum NumberingOrder
    {
        /// <summary>Element id — the order the elements were placed in.</summary>
        CreationOrder,

        /// <summary>By the number already in the parameter; unnumbered ones last. Closes gaps without reshuffling.</summary>
        CurrentNumber,

        /// <summary>Level by level; on each level rows top to bottom, left to right.</summary>
        LevelRows,

        /// <summary>Level by level; on each level columns left to right, top to bottom.</summary>
        LevelColumns,
    }

    /// <summary>
    /// Everything the window asks for, in one serialisable object: what to number, into
    /// which parameter, in what format and order.
    /// </summary>
    /// <remarks>
    /// Saved per user between sessions (<see cref="SettingsStore"/>). The background
    /// updater will need the same object stored in the model instead, so that every
    /// user of a workshared file numbers the same way — which is why this is a plain
    /// DTO and not window state.
    /// </remarks>
    public sealed class NumberingRule
    {
        public long CategoryId { get; set; } = (long)BuiltInCategory.OST_MechanicalEquipment;

        /// <summary>
        /// Families left out. Stored as exclusions rather than a selection so that a
        /// family loaded later is numbered by default instead of silently skipped.
        /// </summary>
        public List<string> ExcludedFamilies { get; set; } = new List<string>();

        public ParameterKey Parameter { get; set; } = ParameterKey.Mark;

        public string Prefix { get; set; } = "F-";
        public string Start { get; set; } = "01";
        public string Step { get; set; } = "1";
        public string Suffix { get; set; } = "";

        public ElementScope Scope { get; set; } = ElementScope.WholeProject;
        public NumberingOrder Order { get; set; } = NumberingOrder.CreationOrder;
        public NumberingMode Mode { get; set; } = NumberingMode.RenumberAll;

        /// <summary>
        /// In the "only empty" modes, also renumber an element whose number repeats one
        /// held by an older element — what a copy-paste usually leaves behind.
        /// </summary>
        public bool FixDuplicates { get; set; } = true;

        /// <summary>The format being replaced in <see cref="NumberingMode.Reformat"/>: "F" in F02 → F-02.</summary>
        public string SourcePrefix { get; set; } = "";
        public string SourceSuffix { get; set; } = "";
    }
}
