using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(244, 246, 248), Surface = Color.White, Text = Color.FromArgb(30, 43, 55),
        Muted = Color.FromArgb(92, 106, 118), Accent = Color.FromArgb(16, 117, 108), Border = Color.FromArgb(220, 227, 232), Error = Color.FromArgb(162, 45, 45);
    private static readonly string[] Fonts = new InstalledFontCollection().Families.Select(f => f.Name).ToArray();
    public static readonly string UiFamily = Fonts.Contains("Segoe UI Variable") ? "Segoe UI Variable" : Fonts.Contains("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
    public static readonly string MonoFamily = Fonts.Contains("Cascadia Mono") ? "Cascadia Mono" : "Consolas";
    public static Font Ui(float size = 10, FontStyle style = FontStyle.Regular) { return new Font(UiFamily, size, style); }
    public static Font Mono(float size = 10) { return new Font(MonoFamily, size); }
    public static Label Label(string text, bool muted = false) { return new Label { Text = text, AutoSize = true, ForeColor = muted ? Muted : Text, Margin = new Padding(0, 3, 12, 6) }; }
    public static Button Button(string text, bool primary = false) {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14, 6, 14, 6), Margin = new Padding(0, 4, 10, 4),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Surface, ForeColor = primary ? Color.White : Text, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderColor = primary ? Accent : Border; return button;
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
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0, 0, 16, 6) };
        panel.Controls.Add(Label(name), 0, 0); input.Dock = DockStyle.Fill; panel.Controls.Add(input, 0, 1); return panel;
    }
    public static string Milliseconds(double value) { return value.ToString("0.0") + " ms"; }
    public static void MethodCombo(ComboBox combo) {
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.DrawItem += delegate(object sender, DrawItemEventArgs e) {
            e.DrawBackground();
            if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, combo.Items[e.Index].ToString(), combo.Font, e.Bounds, combo.Enabled ? Text : Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
    }
}
internal abstract class AppPage : UserControl
{
    protected readonly TableLayoutPanel Body;
    protected AppPage(string title, string subtitle)
    {
        Dock = DockStyle.Fill; BackColor = Theme.Background; ForeColor = Theme.Text; Font = Theme.Ui(); Padding = new Padding(24, 20, 24, 16);
        var root = Theme.Stack();
        var header = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
        var heading = Theme.Label(title); heading.Font = Theme.Ui(22, FontStyle.Bold);
        header.Controls.Add(heading); header.Controls.Add(Theme.Label(subtitle, true));
        Theme.Row(root, header, false);
        root.RowStyles[0] = new RowStyle(SizeType.Absolute, 100);
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
        Body = Theme.Stack(); Body.Dock = DockStyle.Top;
        int minimum = title == "Strategy Comparison" ? 1080 : title == "Benchmark Lab" ? 1100 : title == "Adaptive Engine Monitor" ? 850 : title == "Search" ? 760 : 1000;
        viewport.Resize += delegate {
            Body.Height = Math.Max(minimum, viewport.ClientSize.Height);
        };
        Body.Height = minimum; viewport.Controls.Add(Body); Theme.Row(root, viewport, true); Controls.Add(root);
    }
    public abstract void RefreshSession(SearchSession session);
}
