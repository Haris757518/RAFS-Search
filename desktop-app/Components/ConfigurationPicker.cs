using System.Windows.Forms;
internal sealed class ConfigurationPicker : SectionPanel
{
    private readonly ComboBox method = new ComboBox();
    private readonly NumericUpDown threads;
    public ConfigurationPicker(string title, int defaultMethod, int count) : base(title)
    {
        Theme.MethodCombo(method);
        method.DropDownStyle = ComboBoxStyle.DropDownList; method.Items.AddRange(new object[] { "Method 1 — WalkDir + Rayon", "Method 2 — Recursive DFS" }); method.SelectedIndex = defaultMethod - 1;
        threads = Theme.Number(count, 256); method.SelectedIndexChanged += delegate { threads.Enabled = method.SelectedIndex == 0; }; threads.Enabled = defaultMethod == 1;
        Theme.Row(this, Theme.Field("Method", method), false); Theme.Row(this, Theme.Field("Threads (0 = automatic)", threads), false);
        Theme.Row(this, Theme.Label("BFS / Hybrid / Adaptive: Planned", true), false);
    }
    public RunConfiguration GetConfiguration() { return new RunConfiguration { Method = method.SelectedIndex + 1, Threads = method.SelectedIndex == 1 ? 1 : (int)threads.Value }; }
}
