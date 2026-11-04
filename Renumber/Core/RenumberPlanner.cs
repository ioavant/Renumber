using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Renumber.Core
{
    public enum RowState
    {
        /// <summary>Gets a new value.</summary>
        Change,

        /// <summary>Already has the right value, or keeps a number it legitimately holds.</summary>
        Unchanged,

        /// <summary>Has a value in some other format; the "only empty" modes leave it alone.</summary>
        Kept,

        /// <summary>Cannot be written: no such parameter, read-only, borrowed by another user.</summary>
        Skipped,
    }

    public sealed class PlanRow
    {
        public int Index { get; set; }
        public long Id { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string Level { get; set; }
        public string Current { get; set; }
        public string New { get; set; }
        public string Status { get; set; }
        public RowState State { get; set; }

        /// <summary>The new value repeats one an element outside this run keeps.</summary>
        public bool Conflict { get; set; }
    }

    public sealed class RenumberPlan
    {
        public RenumberPlan(ParameterKey parameter)
        {
            Parameter = parameter;
        }

        public ParameterKey Parameter { get; }
        public List<PlanRow> Rows { get; } = new List<PlanRow>();

        public IEnumerable<PlanRow> Changes => Rows.Where(r => r.State == RowState.Change);
        public int ChangeCount => Rows.Count(r => r.State == RowState.Change);
        public int SkippedCount => Rows.Count(r => r.State == RowState.Skipped);
        public int ConflictCount => Rows.Count(r => r.Conflict);

        /// <summary>Changes that replace a value someone typed, as opposed to filling an empty one.</summary>
        public int OverwriteCount => Changes.Count(r => !string.IsNullOrWhiteSpace(r.Current));
    }

    /// <summary>
    /// Works out the new value of every element in scope without touching the model —
    /// the window shows the result as a preview, <see cref="RenumberWriter"/> writes it.
    /// </summary>
    public static class RenumberPlanner
    {
        /// <summary>
        /// How close two elements must be along the cross axis to count as one row (or
        /// column) when ordering by location: 1 m, in feet. Equipment in a row is never
        /// placed on exactly the same coordinate.
        /// </summary>
        private const double RowTolerance = 1.0 / 0.3048;

        private sealed class Item
        {
            public FamilyInstance Element;
            public string Family;
            public Parameter Parameter;
            public string Value;
            public int? Number;
            public int? SourceNumber;
            public bool Duplicate;
            public bool NotPicked;
            public bool Target;
            public string SkipReason;
            public Level Level;
            public XYZ Point;

            public long Id => Element.Id.GetValue();
            public double Elevation => Level?.Elevation ?? double.MaxValue;
        }

        /// <param name="instances">Every numberable instance of the rule's category.</param>
        /// <param name="picked">
        /// Rows the user selected in the preview, or null for all of them. Unpicked rows
        /// stay in the plan, unchanged, and keep their numbers like any element outside
        /// the scope — so the picked ones are numbered around them.
        /// </param>
        public static RenumberPlan Build(ScopeSource source, NumberingRule rule, NumberFormat format,
                                         IReadOnlyList<FamilyInstance> instances, HashSet<long> picked = null)
        {
            Document doc = source.Document;
            var excluded = new HashSet<string>(rule.ExcludedFamilies ?? new List<string>(), StringComparer.Ordinal);
            HashSet<long> scope = source.IdsIn(rule.Scope);

            // The whole category, not only the chosen families: Revit checks Mark for
            // duplicates per category, so a number held by a family that is not being
            // numbered is still taken.
            NumberFormat oldFormat = rule.Mode == NumberingMode.Reformat
                ? NumberFormat.ForReading(rule.SourcePrefix, rule.SourceSuffix)
                : null;
            List<Item> all = instances.Select(i => Read(i, rule.Parameter, format, oldFormat)).ToList();

            // When a number repeats, the oldest element keeps it.
            var holders = new HashSet<int>();
            foreach (Item item in all.Where(i => i.Number != null).OrderBy(i => i.Id))
            {
                if (!holders.Add(item.Number.Value))
                    item.Duplicate = true;
            }

            List<Item> candidates = all
                .Where(i => !excluded.Contains(i.Family) && (scope == null || scope.Contains(i.Id)))
                .ToList();

            foreach (Item item in candidates)
            {
                item.Level = LevelOf(doc, item.Element);
                item.Point = PointOf(item.Element);
                item.SkipReason = WhyNotWritable(doc, item, rule.Parameter);
                item.NotPicked = picked != null && !picked.Contains(item.Id);
                item.Target = item.SkipReason == null
                              && !item.NotPicked
                              && (oldFormat != null
                                  ? item.SourceNumber != null
                                  : rule.Mode == NumberingMode.RenumberAll
                                    || string.IsNullOrWhiteSpace(item.Value)
                                    || (rule.FixDuplicates && item.Duplicate));
            }

            candidates = Order(candidates, rule.Order).ToList();

            var kept = new Dictionary<int, Item>();
            foreach (Item item in all.Where(i => !i.Target && i.Number != null).OrderBy(i => i.Id))
            {
                if (!kept.ContainsKey(item.Number.Value))
                    kept[item.Number.Value] = item;
            }

            var plan = new RenumberPlan(rule.Parameter);
            if (oldFormat != null)
            {
                Reformat(plan, candidates, kept, format, excluded, scope);
                return plan;
            }

            var allocator = new NumberAllocator(format, kept.Keys, rule.Mode);

            foreach (Item item in candidates)
            {
                var row = NewRow(plan, item);

                if (item.SkipReason != null)
                {
                    row.State = RowState.Skipped;
                    row.Status = item.SkipReason;
                }
                else if (item.NotPicked)
                {
                    NotPicked(row);
                }
                else if (item.Target)
                {
                    int number = allocator.Next();
                    row.New = format.Format(number);

                    if (string.Equals(row.New, row.Current, StringComparison.Ordinal))
                    {
                        row.State = RowState.Unchanged;
                        row.Status = "already numbered";
                    }
                    else
                    {
                        row.State = RowState.Change;
                        row.Status = string.IsNullOrWhiteSpace(item.Value) ? "new number"
                                   : item.Duplicate && rule.Mode != NumberingMode.RenumberAll ? "duplicate, renumbered"
                                   : "renumbered";
                    }

                    // Only possible when renumbering everything: the other modes never hand
                    // out a kept number.
                    if (kept.TryGetValue(number, out Item other))
                    {
                        row.Conflict = true;
                        row.Status += $" — {format.Format(number)} is also on {other.Id} ({Why(other, excluded, scope)})";
                    }
                }
                else if (item.Number != null)
                {
                    row.New = row.Current;
                    row.State = RowState.Unchanged;
                    row.Status = "keeps its number";
                }
                else
                {
                    row.New = row.Current;
                    row.State = RowState.Kept;
                    row.Status = "other format, left as is";
                }

                plan.Rows.Add(row);
            }

            return plan;
        }

        /// <summary>
        /// Format change: every value in the old format keeps its number and takes the new
        /// spelling. Nothing is renumbered, so a number that was already repeated stays
        /// repeated — flagged, so it can be fixed with one of the other modes afterwards.
        /// </summary>
        private static void Reformat(RenumberPlan plan, List<Item> candidates, Dictionary<int, Item> kept,
                                     NumberFormat format, HashSet<string> excluded, HashSet<long> scope)
        {
            var first = new Dictionary<int, Item>();
            foreach (Item item in candidates.Where(i => i.Target).OrderBy(i => i.Id))
            {
                if (!first.ContainsKey(item.SourceNumber.Value))
                    first[item.SourceNumber.Value] = item;
            }

            foreach (Item item in candidates)
            {
                var row = NewRow(plan, item);

                if (item.SkipReason != null)
                {
                    row.State = RowState.Skipped;
                    row.Status = item.SkipReason;
                }
                else if (item.NotPicked)
                {
                    NotPicked(row);
                }
                else if (item.Target)
                {
                    int number = item.SourceNumber.Value;
                    row.New = format.Format(number);
                    bool same = string.Equals(row.New, row.Current, StringComparison.Ordinal);
                    row.State = same ? RowState.Unchanged : RowState.Change;
                    row.Status = same ? "already in the new format" : "format changed";

                    if (kept.TryGetValue(number, out Item other))
                    {
                        row.Conflict = true;
                        row.Status += $" — {row.New} is also on {other.Id} ({Why(other, excluded, scope)})";
                    }
                    else if (first[number] != item)
                    {
                        row.Conflict = true;
                        row.Status += $" — duplicate of {first[number].Id}, stays a duplicate";
                    }
                }
                else
                {
                    row.New = row.Current;
                    row.State = item.Number != null ? RowState.Unchanged : RowState.Kept;
                    row.Status = item.Number != null ? "already in the new format"
                               : string.IsNullOrWhiteSpace(item.Value) ? "empty, left as is"
                               : "not in the old format, left as is";
                }

                plan.Rows.Add(row);
            }
        }

        private static void NotPicked(PlanRow row)
        {
            row.New = row.Current;
            row.State = RowState.Unchanged;
            row.Status = "not selected";
        }

        private static PlanRow NewRow(RenumberPlan plan, Item item)
        {
            return new PlanRow
            {
                Index = plan.Rows.Count + 1,
                Id = item.Id,
                Family = item.Family,
                Type = item.Element.Symbol?.Name ?? "",
                Level = item.Level?.Name ?? "—",
                Current = item.Value ?? "",
            };
        }

        private static Item Read(FamilyInstance instance, ParameterKey key, NumberFormat format, NumberFormat oldFormat)
        {
            Parameter parameter = key?.Find(instance);
            string value = parameter != null && parameter.StorageType == StorageType.String
                ? parameter.AsString()
                : null;

            return new Item
            {
                Element = instance,
                Family = ModelQuery.FamilyName(instance),
                Parameter = parameter,
                Value = value,
                Number = format.TryParse(value, out int number) ? number : (int?)null,
                SourceNumber = oldFormat != null && oldFormat.TryParse(value, out int old) ? old : (int?)null,
            };
        }

        private static string WhyNotWritable(Document doc, Item item, ParameterKey key)
        {
            if (item.Parameter == null)
                return $"has no '{key?.Name}' parameter";
            if (item.Parameter.StorageType != StorageType.String)
                return $"'{key.Name}' is not a text parameter here";
            if (item.Parameter.IsReadOnly)
                return $"'{key.Name}' is read-only";

            if (doc.IsWorkshared
                && WorksharingUtils.GetCheckoutStatus(doc, item.Element.Id, out string owner) == CheckoutStatus.OwnedByOtherUser)
                return $"borrowed by {owner}";

            return null;
        }

        private static string Why(Item other, HashSet<string> excluded, HashSet<long> scope)
        {
            if (excluded.Contains(other.Family))
                return "family not selected";
            if (scope != null && !scope.Contains(other.Id))
                return "outside the scope";
            if (other.NotPicked)
                return "not selected";
            return other.SkipReason != null ? "not writable" : "keeps its value";
        }

        private static IEnumerable<Item> Order(List<Item> items, NumberingOrder order)
        {
            switch (order)
            {
                case NumberingOrder.CurrentNumber:
                    return items
                        .OrderBy(i => i.Number == null ? 1 : 0)
                        .ThenBy(i => i.Number ?? 0)
                        .ThenBy(i => i.Id);

                case NumberingOrder.LevelRows:
                    return items
                        .OrderBy(i => i.Elevation)
                        .ThenByDescending(i => Band(i.Point.Y))
                        .ThenBy(i => i.Point.X)
                        .ThenBy(i => i.Id);

                case NumberingOrder.LevelColumns:
                    return items
                        .OrderBy(i => i.Elevation)
                        .ThenBy(i => Band(i.Point.X))
                        .ThenByDescending(i => i.Point.Y)
                        .ThenBy(i => i.Id);

                default:
                    return items.OrderBy(i => i.Id);
            }
        }

        private static double Band(double coordinate) => Math.Round(coordinate / RowTolerance);

        private static Level LevelOf(Document doc, Element element)
        {
            ElementId id = element.LevelId;
            if (id == null || id == ElementId.InvalidElementId)
            {
                // Face-hosted and work-plane-based instances leave LevelId empty but
                // still report a level through these.
                Parameter parameter = element.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM)
                                      ?? element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (parameter != null && parameter.StorageType == StorageType.ElementId)
                    id = parameter.AsElementId();
            }
            return id == null ? null : doc.GetElement(id) as Level;
        }

        private static XYZ PointOf(Element element)
        {
            if (element.Location is LocationPoint point)
                return point.Point;
            if (element.Location is LocationCurve curve)
                return curve.Curve.Evaluate(0.5, true);

            BoundingBoxXYZ box = element.get_BoundingBox(null);
            return box != null ? (box.Min + box.Max) / 2 : XYZ.Zero;
        }
    }
}
