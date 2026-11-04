using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Renumber.Forms
{
    /// <summary>
    /// The look shared with the other Vixeldorf add-ins: white grid, alternating rows,
    /// light border.
    /// </summary>
    internal static class UiStyle
    {
        public static readonly Brush Line = new SolidColorBrush(Color.FromRgb(200, 200, 200));
        public static readonly Brush Alternate = new SolidColorBrush(Color.FromRgb(245, 250, 255));
        public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(120, 120, 120));
        public static readonly Brush Strong = new SolidColorBrush(Color.FromRgb(80, 80, 80));

        public static readonly Brush Good = new SolidColorBrush(Color.FromRgb(0, 120, 60));
        public static readonly Brush Doubtful = new SolidColorBrush(Color.FromRgb(190, 110, 0));
        public static readonly Brush Missing = new SolidColorBrush(Color.FromRgb(180, 60, 60));

        public static DataGrid Grid()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                CanUserReorderColumns = false,
                IsReadOnly = true,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = Alternate,
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                HorizontalGridLinesBrush = Line,
                VerticalGridLinesBrush = Line,
                FontSize = 12,
                ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader,
            };
        }

        /// <summary>
        /// A text column whose content is trimmed rather than clipped, with the full
        /// value on the tooltip. Optionally coloured from a brush property of the row.
        /// </summary>
        public static DataGridTextColumn Column(string header, string path, double width,
                                                bool star = false, string brushPath = null, bool bold = false)
        {
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path),
                Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(width),
                MinWidth = star ? 200 : 0,
            };

            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(path)));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2)));
            if (brushPath != null)
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding(brushPath)));
            if (bold)
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            column.ElementStyle = style;

            return column;
        }

        public static TextBlock Header(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = Strong,
                Margin = new Thickness(0, 12, 0, 6),
            };
        }

        public static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Foreground = Muted, Margin = new Thickness(0, 4, 0, 2) };
        }
    }
}
