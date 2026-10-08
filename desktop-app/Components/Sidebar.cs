using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
internal sealed class Sidebar : TableLayoutPanel
{
    private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    public event Action<string> Navigate;
    public Sidebar()
    {
        Dock = DockStyle.Fill; ColumnCount = 1; BackColor = Theme.Background; Padding = new Padding(8, 14, 8, 10);
        var brand = Theme.Label("RAFS Search"); brand.Font = Theme.Ui(14, FontStyle.Bold); Theme.Row(this, brand, false);
        Theme.Row(this, Theme.Label("v0.1 Engine", true), false);
        foreach (string name in new[] { "Search", "Live Monitor", "Compare", "Benchmark Lab", "Settings" }) {
            var button = Theme.Button(name); button.TextAlign = ContentAlignment.MiddleLeft; button.Dock = DockStyle.Fill; button.Margin = new Padding(0, 2, 0, 2);
            button.Padding = new Padding(30, 3, 8, 3);
            button.Paint += delegate(object sender, PaintEventArgs e) { DrawIcon(e.Graphics, button, name); };
            button.Click += delegate { if (Navigate != null) Navigate(name); }; buttons.Add(name, button); Theme.Row(this, button, false);
        }
        Theme.Row(this, new Panel(), true); Theme.Row(this, Theme.Label("RAFS v0.1", true), false);
        Theme.Row(this, Theme.Label("OS Project", true), false);
    }
    public void SelectPage(string page) { foreach (var item in buttons) { bool selected = item.Key == page; item.Value.BackColor = selected ? Theme.Surface : BackColor; item.Value.ForeColor = selected ? Theme.Accent : Theme.Text; item.Value.FlatAppearance.BorderColor = selected ? Theme.Border : BackColor; item.Value.FlatAppearance.MouseOverBackColor = Color.FromArgb(229, 238, 247); } }
    private static void DrawIcon(Graphics graphics, Button button, string name) {
        float scale = button.DeviceDpi / 96f, x = 8 * scale, y = (button.Height - 16 * scale) / 2f, size = 16 * scale;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var pen = new Pen(button.ForeColor, 1.3f * scale)) {
            if (name == "Search") { graphics.DrawEllipse(pen, x, y, size * .65f, size * .65f); graphics.DrawLine(pen, x + size * .56f, y + size * .56f, x + size, y + size); }
            else if (name == "Live Monitor") { graphics.DrawLines(pen, new[] { new PointF(x, y), new PointF(x, y + size), new PointF(x + size, y + size) }); graphics.DrawLines(pen, new[] { new PointF(x + 3 * scale, y + 11 * scale), new PointF(x + 7 * scale, y + 6 * scale), new PointF(x + 11 * scale, y + 8 * scale), new PointF(x + size, y + scale) }); }
            else if (name == "Compare") { graphics.DrawRectangle(pen, x + 4 * scale, y, size * .65f, size * .75f); graphics.DrawRectangle(pen, x, y + 4 * scale, size * .65f, size * .75f); }
            else if (name == "Benchmark Lab") { graphics.DrawArc(pen, x, y + 3 * scale, size, size, 180, 180); graphics.DrawLine(pen, x + 8 * scale, y + 11 * scale, x + 13 * scale, y + 5 * scale); graphics.DrawLine(pen, x, y + 11 * scale, x + size, y + 11 * scale); }
            else { graphics.DrawEllipse(pen, x + 4 * scale, y + 4 * scale, 8 * scale, 8 * scale); for (int i = 0; i < 8; i++) { double angle = i * Math.PI / 4; graphics.DrawLine(pen, x + size / 2 + (float)Math.Cos(angle) * 6 * scale, y + size / 2 + (float)Math.Sin(angle) * 6 * scale, x + size / 2 + (float)Math.Cos(angle) * 8 * scale, y + size / 2 + (float)Math.Sin(angle) * 8 * scale); } }
        }
        if (button.ForeColor == Theme.Accent) using (var pen = new Pen(Theme.Accent, 2 * scale)) graphics.DrawLine(pen, scale, 4 * scale, scale, button.Height - 4 * scale);
    }
}
