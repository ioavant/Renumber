using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Branding;
using Renumber.Core;
using FamilyInstance = Autodesk.Revit.DB.FamilyInstance;

namespace Renumber.Forms
{
    /// <summary>
    /// The one window: settings on the left, a live preview of every element's new value
    /// on the right, and a single button that writes it.
    /// </summary>
    /// <remarks>
    /// Built in code, without XAML, like the other Vixeldorf windows. Nothing is written
    /// until "Renumber" is pressed; every other control only recalculates the preview.
    /// </remarks>
    internal sealed class RenumberWindow : Window
    {
        private sealed class Choice<T>
        {
            public Choice(T value, string text)
            {
                Value = value;
                Text = text;
            }

            public T Value { get; }
            public string Text { get; }
            public override string ToString() => Text;
        }

        /// <summary>A preview row plus the colour of its status.</summary>
        private sealed class RowView
        {
            public RowView(PlanRow row)
            {
                Row = row;
            }

            public PlanRow Row { get; }
            public int Index => Row.Index;
            public long Id => Row.Id;
            public string Family => Row.Family;
            public string Type => Row.Type;
            public string Level => Row.Level;
            public string Current => Row.Current;
            public string New => Row.New;
            public string Status => Row.Status;

            public Brush StatusBrush =>
                Row.Conflict ? UiStyle.Doubtful
                : Row.State == RowState.Change ? UiStyle.Good
                : Row.State == RowState.Kept ? UiStyle.Doubtful
                : Row.State == RowState.Skipped ? UiStyle.Missing
                : UiStyle.Muted;
        }

        private static readonly Thickness Gap = new Thickness(0, 0, 0, 6);

        private readonly ScopeSource _source;
        private readonly NumberingRule _saved;
        private readonly DispatcherTimer _debounce;

        private List<FamilyInstance> _instances = new List<FamilyInstance>();
        private List<NumberPattern> _found = new List<NumberPattern>();
        private RenumberPlan _plan;
        private bool _loading;

        /// <summary>
        /// Rows selected in the preview. Empty means "all rows"; otherwise only these are
        /// processed. Kept by element id, because every recalculation rebuilds the rows.
        /// </summary>
        private readonly HashSet<long> _picked = new HashSet<long>();

        /// <summary>Set while the grid is refilled, when its selection changes by itself.</summary>
        private bool _refilling;

        private ComboBox _category;
        private StackPanel _families;
        private RadioButton _scopeProject;
        private RadioButton _scopeView;
        private RadioButton _scopeSelection;
        private ComboBox _parameter;
        private TextBox _prefix;
        private TextBox _start;
        private TextBox _step;
        private TextBox _suffix;
        private TextBlock _example;
        private WrapPanel _patterns;
        private ComboBox _order;
        private RadioButton _modeAll;
        private RadioButton _modeContinue;
        private RadioButton _modeGaps;
        private CheckBox _fixDuplicates;
        private RadioButton _modeReformat;
        private StackPanel _sourcePanel;
        private TextBox _sourcePrefix;
        private TextBox _sourceSuffix;
        private DataGrid _grid;
        private TextBlock _summary;
        private Button _unpick;
        private TextBlock _result;
        private Button _apply;

        public RenumberWindow(ScopeSource source, NumberingRule saved)
        {
            _source = source;
            _saved = saved;

            Title = $"{Brand.ProductDisplayName}  —  v{UpdateChecker.CurrentVersion}";
            Width = 1180;
            Height = 740;
            MinWidth = 900;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontSize = 12;
            BrandIcon.Apply(this);

            // Typing "F-" fires a change per key; the preview waits for a pause.
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _debounce.Tick += (s, e) =>
            {
                _debounce.Stop();
                Recalculate();
            };

            Content = BuildLayout();
            Load();

            Closed += (s, e) => SettingsStore.Save(ReadRule());
        }

        // ---------------------------------------------------------------- layout

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(12) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var settings = new ScrollViewer
            {
                Content = BuildSettings(),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 6, 0),
            };
            Grid.SetColumn(settings, 0);
            root.Children.Add(settings);

            UIElement preview = BuildPreview();
            Grid.SetColumn(preview, 2);
            root.Children.Add(preview);

            UIElement footer = BuildFooter();
            Grid.SetRow(footer, 1);
            Grid.SetColumnSpan(footer, 3);
            root.Children.Add(footer);

            UIElement banner = BuildUpdateBanner();
            if (banner == null)
                return root;

            var outer = new DockPanel();
            DockPanel.SetDock(banner, Dock.Top);
            outer.Children.Add(banner);
            outer.Children.Add(root);
            return outer;
        }

        /// <summary>
        /// A newer published version, as a link to its installer; null when there is none.
        /// The check runs in the background from App.OnStartup, so a window opened right
        /// after Revit starts may not show it yet.
        /// </summary>
        private static UIElement BuildUpdateBanner()
        {
            UpdateInfo info = UpdateChecker.AvailableUpdate;
            if (info == null)
                return null;

            string text = $"↑  {Brand.ProductDisplayName} {info.Version} is available — click to download";
            if (!string.IsNullOrEmpty(info.ReleaseNotes))
                text += $"  ({info.ReleaseNotes})";

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(224, 242, 253)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 159, 220)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(12, 12, 12, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 90, 140)),
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            border.MouseLeftButtonUp += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(info.DownloadUrl) { UseShellExecute = true }); }
                catch { /* no browser, or a bad URL — nothing useful to say */ }
            };
            return border;
        }

        private UIElement BuildSettings()
        {
            var panel = new StackPanel();

            // What to number.
            var elementsHeader = UiStyle.Header("Elements");
            elementsHeader.Margin = new Thickness(0, 0, 0, 6);
            panel.Children.Add(elementsHeader);

            panel.Children.Add(UiStyle.Label("Category"));
            _category = new ComboBox { Margin = Gap };
            _category.SelectionChanged += (s, e) =>
            {
                if (_loading) return;
                FillCategory(new HashSet<string>());
                Schedule();
            };
            panel.Children.Add(_category);

            var familiesHeader = new DockPanel { Margin = new Thickness(0, 4, 0, 2) };
            var none = LinkButton("none", () => SetAllFamilies(false));
            var all = LinkButton("all", () => SetAllFamilies(true));
            DockPanel.SetDock(none, Dock.Right);
            DockPanel.SetDock(all, Dock.Right);
            familiesHeader.Children.Add(none);
            familiesHeader.Children.Add(all);
            var familiesLabel = UiStyle.Label("Families");
            familiesLabel.Margin = new Thickness(0);
            familiesHeader.Children.Add(familiesLabel);
            panel.Children.Add(familiesHeader);

            _families = new StackPanel { Margin = new Thickness(4) };
            panel.Children.Add(new Border
            {
                BorderBrush = UiStyle.Line,
                BorderThickness = new Thickness(1),
                Height = 170,
                Margin = Gap,
                Child = new ScrollViewer
                {
                    Content = _families,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                },
            });

            panel.Children.Add(UiStyle.Label("Which elements"));
            _scopeProject = Radio("scope", "Whole project");
            _scopeView = Radio("scope", _source.ActiveView != null
                ? $"Visible in the active view ({_source.ActiveView.Name})"
                : "Visible in the active view (not available here)");
            _scopeView.IsEnabled = _source.ActiveView != null;
            _scopeSelection = Radio("scope", $"Current selection ({_source.Selection.Count} selected)");
            _scopeSelection.IsEnabled = _source.Selection.Count > 0;
            panel.Children.Add(_scopeProject);
            panel.Children.Add(_scopeView);
            panel.Children.Add(_scopeSelection);

            // How to number.
            panel.Children.Add(UiStyle.Header("Numbering"));

            panel.Children.Add(UiStyle.Label("Write to parameter"));
            _parameter = new ComboBox { Margin = Gap };
            _parameter.SelectionChanged += (s, e) => Schedule();
            panel.Children.Add(_parameter);

            var format = new UniformGrid { Columns = 4, Margin = Gap };
            _prefix = Field(format, "Prefix");
            _start = Field(format, "Start");
            _step = Field(format, "Step");
            _suffix = Field(format, "Suffix");
            panel.Children.Add(format);

            _example = new TextBlock { Foreground = UiStyle.Muted, Margin = Gap, TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(_example);

            // Formats already in use among the elements in scope, as links: one click
            // takes "F02, F03, F04" for the format instead of retyping it.
            _patterns = new WrapPanel { Margin = Gap };
            panel.Children.Add(_patterns);

            panel.Children.Add(UiStyle.Label("Order"));
            _order = new ComboBox { Margin = Gap };
            _order.Items.Add(new Choice<NumberingOrder>(NumberingOrder.CreationOrder, "Creation order"));
            _order.Items.Add(new Choice<NumberingOrder>(NumberingOrder.CurrentNumber, "Current number (keeps the sequence, closes gaps)"));
            _order.Items.Add(new Choice<NumberingOrder>(NumberingOrder.LevelRows, "By level, then rows: top to bottom, left to right"));
            _order.Items.Add(new Choice<NumberingOrder>(NumberingOrder.LevelColumns, "By level, then columns: left to right, top to bottom"));
            _order.SelectionChanged += (s, e) => Schedule();
            panel.Children.Add(_order);

            panel.Children.Add(UiStyle.Label("Mode"));
            _modeAll = Radio("mode", "Renumber all — overwrite every value");
            _modeContinue = Radio("mode", "Only empty — continue after the last number");
            _modeGaps = Radio("mode", "Only empty — fill gaps first");
            panel.Children.Add(_modeAll);
            panel.Children.Add(_modeContinue);
            panel.Children.Add(_modeGaps);

            _fixDuplicates = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = "Treat duplicates as empty (the oldest element keeps the number)",
                    TextWrapping = TextWrapping.Wrap,
                },
                Margin = new Thickness(18, 4, 0, 0),
            };
            _fixDuplicates.Checked += (s, e) => Schedule();
            _fixDuplicates.Unchecked += (s, e) => Schedule();
            panel.Children.Add(_fixDuplicates);

            _modeReformat = Radio("mode", "Change format — keep the numbers (F02 → F-02)");
            _modeReformat.Checked += (s, e) =>
            {
                if (_loading || _sourcePrefix.Text.Length > 0 || _sourceSuffix.Text.Length > 0 || _found.Count == 0)
                    return;
                _sourcePrefix.Text = _found[0].Prefix;
                _sourceSuffix.Text = _found[0].Suffix;
            };
            panel.Children.Add(_modeReformat);

            _sourcePanel = new StackPanel { Margin = new Thickness(18, 2, 0, 0) };
            var source = new UniformGrid { Columns = 2, Margin = Gap };
            _sourcePrefix = Field(source, "Old prefix");
            _sourceSuffix = Field(source, "Old suffix");
            _sourcePanel.Children.Add(source);
            _sourcePanel.Children.Add(new TextBlock
            {
                Text = "Values in the old format are rewritten with the prefix and suffix above; " +
                       "Start only sets the digits (01 → two). Other values are not touched.",
                Foreground = UiStyle.Muted,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(_sourcePanel);

            return panel;
        }

        private UIElement BuildPreview()
        {
            var panel = new DockPanel();

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            _unpick = LinkButton("process all rows", () =>
            {
                _picked.Clear();
                Recalculate();
            });
            _unpick.Visibility = Visibility.Collapsed;
            _unpick.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(_unpick, Dock.Right);
            top.Children.Add(_unpick);

            _summary = new TextBlock
            {
                Foreground = UiStyle.Strong,
                TextWrapping = TextWrapping.Wrap,
            };
            top.Children.Add(_summary);
            DockPanel.SetDock(top, Dock.Top);
            panel.Children.Add(top);

            _grid = UiStyle.Grid();
            _grid.SelectionMode = DataGridSelectionMode.Extended;
            _grid.ToolTip = "Select rows (Ctrl / Shift + click) to process only them.";
            _grid.SelectionChanged += (s, e) =>
            {
                if (_refilling || e.OriginalSource != _grid)
                    return;
                _picked.Clear();
                foreach (RowView row in _grid.SelectedItems.OfType<RowView>())
                    _picked.Add(row.Id);
                Schedule();
            };
            _grid.Columns.Add(UiStyle.Column("#", nameof(RowView.Index), 44));
            _grid.Columns.Add(UiStyle.Column("Id", nameof(RowView.Id), 80));
            _grid.Columns.Add(UiStyle.Column("Family", nameof(RowView.Family), 170));
            _grid.Columns.Add(UiStyle.Column("Type", nameof(RowView.Type), 130));
            _grid.Columns.Add(UiStyle.Column("Level", nameof(RowView.Level), 100));
            _grid.Columns.Add(UiStyle.Column("Current", nameof(RowView.Current), 80));
            _grid.Columns.Add(UiStyle.Column("New", nameof(RowView.New), 80, bold: true));
            _grid.Columns.Add(UiStyle.Column("Status", nameof(RowView.Status), 0, star: true,
                                             brushPath: nameof(RowView.StatusBrush)));
            panel.Children.Add(_grid);

            return panel;
        }

        private UIElement BuildFooter()
        {
            var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };

            var close = new Button { Content = "Close", Width = 90, Height = 28, IsCancel = true };
            close.Click += (s, e) => Close();
            _apply = new Button { Content = "Renumber", MinWidth = 130, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            _apply.Click += (s, e) => Apply();

            DockPanel.SetDock(close, Dock.Right);
            DockPanel.SetDock(_apply, Dock.Right);
            footer.Children.Add(close);
            footer.Children.Add(_apply);

            _result = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            footer.Children.Add(_result);

            return footer;
        }

        private RadioButton Radio(string group, string text)
        {
            var radio = new RadioButton { GroupName = group, Content = text, Margin = new Thickness(0, 2, 0, 2) };
            radio.Checked += (s, e) => Schedule();
            return radio;
        }

        private TextBox Field(UniformGrid parent, string label)
        {
            var cell = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            cell.Children.Add(UiStyle.Label(label));
            var box = new TextBox { Height = 24, VerticalContentAlignment = VerticalAlignment.Center };
            box.TextChanged += (s, e) => Schedule();
            cell.Children.Add(box);
            parent.Children.Add(cell);
            return box;
        }

        private static Button LinkButton(string text, Action click)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.SteelBlue,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            button.Click += (s, e) => click();
            return button;
        }

        // ---------------------------------------------------------------- state

        private void Load()
        {
            _loading = true;
            try
            {
                List<CategoryChoice> categories = ModelQuery.Categories(_source.Document);
                _category.ItemsSource = categories;
                _category.SelectedItem =
                    categories.FirstOrDefault(c => c.Id == _saved.CategoryId)
                    ?? categories.FirstOrDefault(c => c.Id == (long)Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment)
                    ?? categories.FirstOrDefault();

                bool sameCategory = (_category.SelectedItem as CategoryChoice)?.Id == _saved.CategoryId;
                FillCategory(sameCategory
                    ? new HashSet<string>(_saved.ExcludedFamilies ?? new List<string>())
                    : new HashSet<string>());

                _prefix.Text = _saved.Prefix;
                _start.Text = _saved.Start;
                _step.Text = _saved.Step;
                _suffix.Text = _saved.Suffix;
                _sourcePrefix.Text = _saved.SourcePrefix ?? "";
                _sourceSuffix.Text = _saved.SourceSuffix ?? "";

                // A selection made before starting the command is the clearest statement
                // of intent there is, so it wins over the remembered scope.
                ElementScope scope = _source.Selection.Count > 0 ? ElementScope.Selection : _saved.Scope;
                if (scope == ElementScope.Selection && !_scopeSelection.IsEnabled
                    || scope == ElementScope.ActiveView && !_scopeView.IsEnabled)
                    scope = ElementScope.WholeProject;
                (scope == ElementScope.Selection ? _scopeSelection
                    : scope == ElementScope.ActiveView ? _scopeView
                    : _scopeProject).IsChecked = true;

                _order.SelectedItem = _order.Items.Cast<Choice<NumberingOrder>>().First(c => c.Value == _saved.Order);

                (_saved.Mode == NumberingMode.ContinueAfterLast ? _modeContinue
                    : _saved.Mode == NumberingMode.FillGaps ? _modeGaps
                    : _saved.Mode == NumberingMode.Reformat ? _modeReformat
                    : _modeAll).IsChecked = true;
                _fixDuplicates.IsChecked = _saved.FixDuplicates;
            }
            finally
            {
                _loading = false;
            }

            Recalculate();
        }

        /// <summary>Re-reads the families and parameters for the chosen category.</summary>
        private void FillCategory(HashSet<string> excluded)
        {
            var category = _category.SelectedItem as CategoryChoice;
            _instances = category == null
                ? new List<FamilyInstance>()
                : ModelQuery.Instances(_source.Document, category.Id);

            _families.Children.Clear();
            foreach (FamilyChoice family in ModelQuery.Families(_instances))
            {
                var box = new CheckBox
                {
                    Content = $"{family.Name}  ({family.Count})",
                    Tag = family.Name,
                    IsChecked = !excluded.Contains(family.Name),
                    Margin = new Thickness(0, 1, 0, 1),
                };
                box.Checked += (s, e) => Schedule();
                box.Unchecked += (s, e) => Schedule();
                _families.Children.Add(box);
            }

            ParameterKey keep = (_parameter.SelectedItem as ParameterChoice)?.Key ?? _saved.Parameter ?? ParameterKey.Mark;
            List<ParameterChoice> parameters = ModelQuery.TextParameters(_instances);
            _parameter.ItemsSource = parameters;
            _parameter.SelectedItem =
                parameters.FirstOrDefault(p => p.Key.Id == keep.Id)
                ?? parameters.FirstOrDefault(p => p.Key.IsMark)
                ?? parameters.FirstOrDefault();
        }

        private void SetAllFamilies(bool check)
        {
            _loading = true;
            try
            {
                foreach (CheckBox box in _families.Children.OfType<CheckBox>())
                    box.IsChecked = check;
            }
            finally
            {
                _loading = false;
            }
            Recalculate();
        }

        private NumberingRule ReadRule()
        {
            return new NumberingRule
            {
                CategoryId = (_category.SelectedItem as CategoryChoice)?.Id ?? _saved.CategoryId,
                ExcludedFamilies = _families.Children.OfType<CheckBox>()
                    .Where(b => b.IsChecked != true)
                    .Select(b => (string)b.Tag)
                    .ToList(),
                Parameter = (_parameter.SelectedItem as ParameterChoice)?.Key,
                Prefix = _prefix.Text,
                Start = _start.Text,
                Step = _step.Text,
                Suffix = _suffix.Text,
                Scope = _scopeSelection.IsChecked == true ? ElementScope.Selection
                      : _scopeView.IsChecked == true ? ElementScope.ActiveView
                      : ElementScope.WholeProject,
                Order = (_order.SelectedItem as Choice<NumberingOrder>)?.Value ?? NumberingOrder.CreationOrder,
                Mode = _modeContinue.IsChecked == true ? NumberingMode.ContinueAfterLast
                     : _modeGaps.IsChecked == true ? NumberingMode.FillGaps
                     : _modeReformat.IsChecked == true ? NumberingMode.Reformat
                     : NumberingMode.RenumberAll,
                FixDuplicates = _fixDuplicates.IsChecked == true,
                SourcePrefix = _sourcePrefix.Text,
                SourceSuffix = _sourceSuffix.Text,
            };
        }

        private void Schedule()
        {
            if (_loading)
                return;
            _debounce.Stop();
            _debounce.Start();
        }

        // ---------------------------------------------------------------- preview

        private void Recalculate()
        {
            _plan = null;
            SetRows(null);

            NumberingRule rule = ReadRule();
            bool reformat = rule.Mode == NumberingMode.Reformat;
            _fixDuplicates.IsEnabled = rule.Mode == NumberingMode.ContinueAfterLast || rule.Mode == NumberingMode.FillGaps;
            _step.IsEnabled = !reformat;
            _sourcePanel.Visibility = reformat ? Visibility.Visible : Visibility.Collapsed;

            if (!NumberFormat.TryCreate(rule.Prefix, rule.Start, rule.Step, rule.Suffix,
                                        out NumberFormat format, out string error))
            {
                _example.Text = error;
                _example.Foreground = UiStyle.Missing;
                _summary.Text = "";
                UpdateApply();
                return;
            }

            _example.Foreground = UiStyle.Muted;
            _example.Text = "e.g. " + string.Join(", ",
                Enumerable.Range(0, 3).Select(i => format.Format(format.Start + i * format.Step))) + " …";

            if (rule.Parameter == null)
            {
                _summary.Text = "Choose a parameter to write to.";
                UpdateApply();
                return;
            }

            try
            {
                _plan = RenumberPlanner.Build(_source, rule, format, _instances, _picked.Count > 0 ? _picked : null);

                // A change of category, families or scope can take picked rows out of the
                // preview. Forget those; if none is left, that is "all rows" again.
                var shown = new HashSet<long>(_plan.Rows.Select(r => r.Id));
                if (_picked.Count > 0 && _picked.RemoveWhere(id => !shown.Contains(id)) > 0)
                    _plan = RenumberPlanner.Build(_source, rule, format, _instances, _picked.Count > 0 ? _picked : null);
            }
            catch (Exception e)
            {
                _summary.Text = "Could not build the preview: " + e.Message;
                _summary.Foreground = UiStyle.Missing;
                UpdateApply();
                return;
            }

            SetRows(_plan.Rows.Select(r => new RowView(r)).ToList());
            ShowPatterns(NumberPattern.Detect(_plan.Rows.Select(r => r.Current)));

            if (reformat)
            {
                PlanRow sample = _plan.Changes.FirstOrDefault();
                _example.Text = sample != null
                    ? $"e.g. {sample.Current} → {sample.New}"
                    : "Nothing in the old format here — pick it from the formats below.";
            }

            _summary.Foreground = UiStyle.Strong;
            _summary.Text = Summary(_plan, _picked.Count);
            UpdateApply();
        }

        /// <summary>Refills the grid and puts the picked rows back into its selection.</summary>
        private void SetRows(List<RowView> rows)
        {
            _refilling = true;
            try
            {
                _grid.ItemsSource = rows;
                if (rows != null && _picked.Count > 0)
                {
                    foreach (RowView row in rows.Where(r => _picked.Contains(r.Id)))
                        _grid.SelectedItems.Add(row);
                }
            }
            finally
            {
                _refilling = false;
            }
            _unpick.Visibility = _picked.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Lists the formats in use; clicking one takes it as the old format or the new one.</summary>
        private void ShowPatterns(List<NumberPattern> found)
        {
            _found = found;
            _patterns.Children.Clear();
            if (found.Count == 0)
                return;

            var label = UiStyle.Label("In use:");
            label.Margin = new Thickness(0, 0, 0, 0);
            _patterns.Children.Add(label);

            foreach (NumberPattern pattern in found.Take(6))
            {
                Button link = LinkButton(pattern.ToString(), () => UsePattern(pattern));
                link.ToolTip = pattern.Min == pattern.Max
                    ? $"{pattern.Count} value(s), number {pattern.Min}"
                    : $"{pattern.Count} value(s), numbers {pattern.Min}–{pattern.Max}. Click to use this format.";
                _patterns.Children.Add(link);
            }
        }

        private void UsePattern(NumberPattern pattern)
        {
            if (_modeReformat.IsChecked == true)
            {
                _sourcePrefix.Text = pattern.Prefix;
                _sourceSuffix.Text = pattern.Suffix;
                return;
            }

            // Keep the start number the user chose, only re-padded to the pattern's width.
            int start = int.TryParse(_start.Text.Trim(), out int typed) ? typed : 1;
            _prefix.Text = pattern.Prefix;
            _suffix.Text = pattern.Suffix;
            _start.Text = pattern.PaddedStart(start);
        }

        private static string Summary(RenumberPlan plan, int picked)
        {
            if (plan.Rows.Count == 0)
                return "Nothing in scope: no elements of the chosen families here.";

            var parts = new List<string> { $"{plan.Rows.Count} element(s) in scope" };
            if (picked > 0)
                parts.Add($"only the {picked} selected row(s) are processed");
            parts.Add($"{plan.ChangeCount} to write");
            if (plan.OverwriteCount > 0)
                parts.Add($"{plan.OverwriteCount} of them replace an existing value");
            if (plan.SkippedCount > 0)
                parts.Add($"{plan.SkippedCount} skipped");
            if (plan.ConflictCount > 0)
                parts.Add($"{plan.ConflictCount} would repeat a number kept elsewhere");
            return string.Join(" · ", parts);
        }

        private void UpdateApply()
        {
            int count = _plan?.ChangeCount ?? 0;
            _apply.IsEnabled = count > 0;
            string label = _picked.Count > 0 ? "Renumber selected" : "Renumber";
            _apply.Content = count > 0 ? $"{label} ({count})" : label;
        }

        // ---------------------------------------------------------------- write

        private void Apply()
        {
            // The preview may be up to one debounce interval behind the controls.
            _debounce.Stop();
            Recalculate();
            if (_plan == null || _plan.ChangeCount == 0)
                return;

            if (_plan.OverwriteCount > 0
                && MessageBox.Show(this,
                       $"{_plan.OverwriteCount} existing value(s) of '{_plan.Parameter.Name}' will be replaced.\n\n" +
                       "The whole run is one Undo step in Revit. Continue?",
                       "Renumber", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            WriteResult result;
            try
            {
                result = RenumberWriter.Apply(_source.Document, _plan);
            }
            catch (Exception e)
            {
                ShowResult("Nothing written: " + e.Message, UiStyle.Missing);
                return;
            }

            SettingsStore.Save(ReadRule());
            Recalculate();

            if (result.RolledBack)
            {
                ShowResult("Revit rolled the change back; nothing was written.", UiStyle.Missing);
            }
            else if (result.Failures.Count > 0)
            {
                ShowResult($"Written: {result.Written}. Failed: {result.Failures.Count} — " +
                           string.Join("; ", result.Failures.Take(5)), UiStyle.Doubtful);
            }
            else
            {
                ShowResult($"Written: {result.Written}.", UiStyle.Good);
            }
        }

        private void ShowResult(string text, Brush brush)
        {
            _result.Text = text;
            _result.Foreground = brush;
        }
    }
}
