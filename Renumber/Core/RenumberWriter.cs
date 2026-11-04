using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Renumber.Core
{
    public sealed class WriteResult
    {
        public int Written { get; set; }
        public List<string> Failures { get; } = new List<string>();
        public bool RolledBack { get; set; }
    }

    /// <summary>Writes a plan's changes in one transaction — one Undo step for the whole run.</summary>
    public static class RenumberWriter
    {
        public static WriteResult Apply(Document doc, RenumberPlan plan)
        {
            var result = new WriteResult();

            using (var transaction = new Transaction(doc, $"Renumber {plan.Parameter.Name}"))
            {
                transaction.Start();

                foreach (PlanRow row in plan.Changes)
                {
                    Element element = doc.GetElement(new ElementId(row.Id));
                    Parameter parameter = element == null ? null : plan.Parameter.Find(element);
                    try
                    {
                        if (parameter != null && !parameter.IsReadOnly && parameter.Set(row.New))
                            result.Written++;
                        else
                            result.Failures.Add($"{row.Id}: not written");
                    }
                    catch (Exception e)
                    {
                        // One element Revit refuses (a group member, say) must not cost
                        // the rest of the run.
                        result.Failures.Add($"{row.Id}: {e.Message}");
                    }
                }

                if (result.Written == 0)
                {
                    transaction.RollBack();
                }
                else if (transaction.Commit() != TransactionStatus.Committed)
                {
                    result.RolledBack = true;
                    result.Written = 0;
                }
            }

            return result;
        }
    }
}
