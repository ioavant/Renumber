using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Renumber.Core
{
    public sealed class CategoryChoice
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public override string ToString() => $"{Name}  ({Count})";
    }

    public sealed class FamilyChoice
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    public sealed class ParameterChoice
    {
        public ParameterKey Key { get; set; }
        public override string ToString() => Key.Name;
    }

    /// <summary>What the model offers for the window's lists.</summary>
    public static class ModelQuery
    {
        /// <summary>Instances sampled per family when listing parameters — enough to see every family's set.</summary>
        private const int ParameterSampleSize = 20;

        /// <summary>
        /// Model categories that have numberable family instances in this project, with
        /// counts. Only family-based categories: the window picks families, and system
        /// elements (ducts, pipes, walls) have none to pick.
        /// </summary>
        public static List<CategoryChoice> Categories(Document doc)
        {
            var found = new Dictionary<long, CategoryChoice>();

            foreach (FamilyInstance instance in new FilteredElementCollector(doc)
                         .OfClass(typeof(FamilyInstance))
                         .Cast<FamilyInstance>())
            {
                if (!IsNumberable(instance))
                    continue;

                Category category = instance.Category;
                if (category == null || category.CategoryType != CategoryType.Model)
                    continue;

                long id = category.Id.GetValue();
                if (!found.TryGetValue(id, out CategoryChoice choice))
                    found[id] = choice = new CategoryChoice { Id = id, Name = category.Name };
                choice.Count++;
            }

            return found.Values
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static List<FamilyInstance> Instances(Document doc, long categoryId)
        {
            return new FilteredElementCollector(doc)
                .OfCategoryId(ElementIdExtensions.FromValue(categoryId))
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(IsNumberable)
                .ToList();
        }

        public static List<FamilyChoice> Families(IEnumerable<FamilyInstance> instances)
        {
            return instances
                .GroupBy(FamilyName)
                .Select(g => new FamilyChoice { Name = g.Key, Count = g.Count() })
                .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Instance text parameters that can be written. Type parameters are left out on
        /// purpose: one value per type cannot number individual elements.
        /// </summary>
        public static List<ParameterChoice> TextParameters(IEnumerable<FamilyInstance> instances)
        {
            var found = new Dictionary<string, ParameterChoice>();

            foreach (IGrouping<string, FamilyInstance> family in instances.GroupBy(FamilyName))
            {
                foreach (FamilyInstance instance in family.Take(ParameterSampleSize))
                {
                    foreach (Parameter parameter in instance.Parameters)
                    {
                        if (parameter.Definition == null
                            || parameter.StorageType != StorageType.String
                            || parameter.IsReadOnly)
                            continue;

                        ParameterKey key = ParameterKey.From(parameter);
                        if (!found.ContainsKey(key.Id))
                            found[key.Id] = new ParameterChoice { Key = key };
                    }
                }
            }

            // Mark is the default and every model category has it, so it is offered even
            // before the category has instances to sample.
            ParameterKey mark = ParameterKey.Mark;
            if (!found.ContainsKey(mark.Id))
                found[mark.Id] = new ParameterChoice { Key = mark };

            return found.Values
                .OrderBy(c => c.Key.IsMark ? 0 : 1)
                .ThenBy(c => c.Key.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static string FamilyName(FamilyInstance instance) => instance.Symbol?.FamilyName ?? "";

        /// <summary>
        /// Shared components nested in another family are listed by the collector as
        /// instances of their own, but they belong to their host and are not numbered.
        /// </summary>
        private static bool IsNumberable(FamilyInstance instance) => instance.SuperComponent == null;
    }
}
