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
        Dock = DockStyle.Fill; ColumnCount = 1; BackColor = Color.FromArgb(235, 240, 243); Padding = new Padding(16, 22, 12, 14);
        var brand = Theme.Label("RAFS"); brand.Font = Theme.Ui(24, FontStyle.Bold); Theme.Row(this, brand, false);
        Theme.Row(this, Theme.Label("Filesystem search", true), false);
        foreach (string name in new[] { "Search", "Live Monitor", "Compare", "Benchmark Lab", "Settings" }) {
            var button = Theme.Button(name); button.TextAlign = ContentAlignment.MiddleLeft; button.Dock = DockStyle.Fill; button.Margin = new Padding(0, 6, 0, 4);
            button.Click += delegate { if (Navigate != null) Navigate(name); }; buttons.Add(name, button); Theme.Row(this, button, false);
        }
        Theme.Row(this, new Panel(), true); Theme.Row(this, Theme.Label("RAFS v0.1", true), false);
        Theme.Row(this, Theme.Label("Manual baseline\nAdaptive: Planned", true), false);
    }
    public void SelectPage(string page) { foreach (var item in buttons) { item.Value.BackColor = item.Key == page ? Theme.Accent : BackColor; item.Value.ForeColor = item.Key == page ? Color.White : Theme.Text; } }
}
