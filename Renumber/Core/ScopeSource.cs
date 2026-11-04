using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Renumber.Core
{
    /// <summary>
    /// The document plus what was active and selected when the command started. The
    /// window is modal, so neither can change while it is open.
    /// </summary>
    public sealed class ScopeSource
    {
        private HashSet<long> _inView;

        public Document Document { get; }

        /// <summary>Null when the active view cannot list its elements (a sheet, the browser, …).</summary>
        public View ActiveView { get; }

        public HashSet<long> Selection { get; }

        private ScopeSource(Document document, View activeView, HashSet<long> selection)
        {
            Document = document;
            ActiveView = activeView;
            Selection = selection;
        }

        public static ScopeSource Capture(UIDocument uidoc)
        {
            Document doc = uidoc.Document;

            View view = uidoc.ActiveView;
            if (view != null && view.IsTemplate)
                view = null;
            if (view != null)
            {
                try
                {
                    new FilteredElementCollector(doc, view.Id).Dispose();
                }
                catch (Exception)
                {
                    view = null;
                }
            }

            var selection = new HashSet<long>(uidoc.Selection.GetElementIds().Select(id => id.GetValue()));
            return new ScopeSource(doc, view, selection);
        }

        /// <summary>The element ids the scope allows, or null for "no restriction".</summary>
        public HashSet<long> IdsIn(ElementScope scope)
        {
            switch (scope)
            {
                case ElementScope.ActiveView:
                    if (ActiveView == null)
                        return new HashSet<long>();
                    return _inView ?? (_inView = new HashSet<long>(
                        new FilteredElementCollector(Document, ActiveView.Id)
                            .OfClass(typeof(FamilyInstance))
                            .ToElementIds()
                            .Select(id => id.GetValue())));

                case ElementScope.Selection:
                    return Selection;

                default:
                    return null;
            }
        }
    }
}
