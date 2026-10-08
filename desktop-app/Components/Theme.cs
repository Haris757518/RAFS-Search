using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(243, 243, 243), Surface = Color.White, Text = Color.FromArgb(26, 28, 28),
        Muted = Color.FromArgb(80, 88, 100), Accent = Color.FromArgb(0, 120, 212), Border = Color.FromArgb(226, 226, 226), Error = Color.FromArgb(162, 45, 45);
    private static readonly string[] Fonts = new InstalledFontCollection().Families.Select(f => f.Name).ToArray();
    public static readonly string UiFamily = Fonts.Contains("Segoe UI Variable") ? "Segoe UI Variable" : Fonts.Contains("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
    public static readonly string MonoFamily = Fonts.Contains("Cascadia Mono") ? "Cascadia Mono" : "Consolas";
    public static Font Ui(float size = 10, FontStyle style = FontStyle.Regular) { return new Font(UiFamily, size, style); }
    public static Font Mono(float size = 10) { return new Font(MonoFamily, size); }
    public static Label Label(string text, bool muted = false) { return new Label { Text = text, AutoSize = true, ForeColor = muted ? Muted : Text, Margin = new Padding(0, 2, 10, 4) }; }
    public static Button Button(string text, bool primary = false) {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 3, 10, 3), Margin = new Padding(0, 3, 8, 3),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Surface, ForeColor = primary ? Color.White : Text, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 98, 175) : Color.FromArgb(235, 242, 248);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(0, 80, 145) : Color.FromArgb(224, 235, 246); return button;
    }
    public static FlowLayoutPanel Flow(params Control[] controls) { var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0) }; flow.Controls.AddRange(controls); return flow; }
    public static TableLayoutPanel Stack() { var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0), Padding = new Padding(0) }; table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); return table; }
    public static void Row(TableLayoutPanel table, Control control, bool fill) {
        int row = table.RowCount++; table.RowStyles.Add(new RowStyle(fill ? SizeType.Percent : SizeType.AutoSize, fill ? 100 : 0));
        control.Dock = DockStyle.Fill; table.Controls.Add(control, 0, row);
        var label = control as Label;
        if (label != null) table.SizeChanged += delegate { label.MaximumSize = new Size(Math.Max(1, table.ClientSize.Width - table.Padding.Horizontal - label.Margin.Horizontal), 0); };
    }
    public static NumericUpDown Number(int value, int max, int min = 0) { return new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Width = 82, Margin = new Padding(0, 3, 16, 6) }; }
    public static Control Field(string name, Control input) {
        var panel = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0, 0, 16, 4) };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(Label(name), 0, 0); input.Dock = DockStyle.Fill; panel.Controls.Add(input, 0, 1); return panel;
    }
    public static string Milliseconds(double value) { return value.ToString("0.0") + " ms"; }
    public static void MethodCombo(ComboBox combo) {
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.DrawItem += delegate(object sender, DrawItemEventArgs e) {
            e.DrawBackground();
            if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, combo.Items[e.Index].ToString(), combo.Font, e.Bounds, !combo.Enabled ? Muted : (e.State & DrawItemState.Selected) != 0 ? e.ForeColor : Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
    }
    public static void StyleGrid(DataGridView grid) {
        grid.Font = Ui(10); grid.ForeColor = Text; grid.BackgroundColor = Surface;
        grid.GridColor = Border; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Background; grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Background; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.Font = Ui(10, FontStyle.Bold);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 247, 255); grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Padding = new Padding(6, 3, 6, 3);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 252);
        grid.RowTemplate.Height = grid.Font.Height + 10;
        grid.FontChanged += delegate { grid.RowTemplate.Height = grid.Font.Height + 10; };
    }
}
internal abstract class AppPage : UserControl
{
    protected readonly TableLayoutPanel Body;
    protected AppPage(string title, string subtitle)
    {
        AutoScaleMode = AutoScaleMode.Inherit;
        Dock = DockStyle.Fill; BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Ui(); Padding = new Padding(16, 12, 16, 10);
        var root = Theme.Stack();
        var header = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
        var heading = Theme.Label(title); heading.Font = Theme.Ui(17, FontStyle.Bold);
        header.Controls.Add(heading); header.Controls.Add(Theme.Label(subtitle, true));
        Theme.Row(root, header, false);
        root.RowStyles[0] = new RowStyle(SizeType.Absolute, 66);
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
        Body = Theme.Stack(); Body.Dock = DockStyle.Top;
        int minimum = title == "Strategy Comparison" ? 850 : title == "Benchmark Lab" ? 900 : title == "Adaptive Engine Monitor" ? 760 : title == "Search" ? 540 : 800;
        viewport.Resize += delegate {
            Body.Height = Math.Max((int)Math.Ceiling(minimum * DeviceDpi / 96f), viewport.ClientSize.Height);
        };
        Body.Height = minimum; viewport.Controls.Add(Body); Theme.Row(root, viewport, true); Controls.Add(root);
    }
    public abstract void RefreshSession(SearchSession session);
}
