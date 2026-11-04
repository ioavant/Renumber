using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Renumber.Core;
using Renumber.Forms;

namespace Renumber.Commands
{
    /// <summary>Opens the numbering window.</summary>
    /// <remarks>
    /// Public, like every command type here, so that reflection-based loaders — the
    /// Add-in Manager in particular, which is how this gets tested — are guaranteed
    /// to find it.
    /// </remarks>
    [Transaction(TransactionMode.Manual)]
    public class RenumberCommand : IExternalCommand
    {
        private const string Title = "Renumber";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                return Run(commandData.Application);
            }
            catch (RenumberException expected)
            {
                TaskDialog.Show(Title, expected.Message);
                return Result.Cancelled;
            }
            catch (Exception unexpected)
            {
                // Revit shows this to the user and logs it in the journal.
                message = unexpected.Message;
                return Result.Failed;
            }
        }

        private static Result Run(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null)
                throw new RenumberException("No project is open.");

            Document doc = uidoc.Document;
            if (doc.IsFamilyDocument)
                throw new RenumberException("Renumber works in a project, not in the Family Editor.");

            var window = new RenumberWindow(ScopeSource.Capture(uidoc), SettingsStore.Load());
            new WindowInteropHelper(window).Owner = app.MainWindowHandle;

            // Modal: the window writes to the model from its own button handlers, which is
            // only legal while this command is still on the stack.
            window.ShowDialog();

            // Succeeded even when nothing was written: Cancelled would make Revit undo
            // what the window did commit.
            return Result.Succeeded;
        }
    }
}
