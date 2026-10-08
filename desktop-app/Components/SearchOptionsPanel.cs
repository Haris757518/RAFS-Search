using System;
using System.IO;
using System.Windows.Forms;
internal sealed class SearchOptionsPanel : TableLayoutPanel
{
    internal readonly TextBox Query = new TextBox(), Folder = new TextBox();
    internal readonly ComboBox Method = new ComboBox();
    internal readonly NumericUpDown Depth, Threads;
    internal readonly CheckBox Content = new CheckBox { Text = "Search file contents", AutoSize = true }, CaseSensitive = new CheckBox { Text = "Case sensitive", AutoSize = true };
    public SearchOptionsPanel(AppSettings settings, bool manual)
    {
        Dock = DockStyle.Fill; AutoSize = true; ColumnCount = 1; Margin = new Padding(0);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Folder.Text = settings.RememberFolder && Directory.Exists(settings.LastFolder) ? settings.LastFolder : Path.Combine(SearchRunner.Root, Directory.Exists(Path.Combine(SearchRunner.Root, "src")) ? "src" : "demo-data");
        Query.Text = settings.LastPattern;
        var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        fields.Controls.Add(Theme.Field("Query / Pattern", Query), 0, 0);
        var location = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); location.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Folder.Dock = DockStyle.Fill; Folder.Margin = new Padding(0, 4, 10, 0); var browse = Theme.Button("Browse...");
        browse.Click += delegate { using (var dialog = new FolderBrowserDialog { Description = "Choose a search folder", SelectedPath = Folder.Text, ShowNewFolderButton = false }) if (dialog.ShowDialog(FindForm()) == DialogResult.OK) Folder.Text = dialog.SelectedPath; };
        location.Controls.Add(Folder, 0, 0); location.Controls.Add(browse, 1, 0);
        fields.Controls.Add(Theme.Field("Search Location", location), 1, 0); Theme.Row(this, fields, false);
        Theme.Row(this, Theme.Flow(Content, CaseSensitive), false);
        Method.DropDownStyle = ComboBoxStyle.DropDownList; Method.Width = 260;
        Theme.MethodCombo(Method);
        Method.Items.AddRange(new object[] { "Method 1 — WalkDir + Rayon", "Method 2 — Recursive DFS" }); Method.SelectedIndex = settings.DefaultMethod - 1;
        Depth = Theme.Number(settings.DefaultDepth, 1000); Threads = Theme.Number(settings.DefaultThreads, 256);
        Method.SelectedIndexChanged += delegate { Threads.Enabled = Method.SelectedIndex == 0; };
        Threads.Enabled = Method.SelectedIndex == 0;
        if (manual) {
            var modes = Theme.Flow(new RadioButton { Text = "Manual / Research", Checked = true, AutoSize = true }, new RadioButton { Text = "Adaptive (Recommended) — Planned", Enabled = false, AutoSize = true });
            Theme.Row(this, modes, false);
            Theme.Row(this, Theme.Flow(Theme.Field("Traversal / Method", Method), Theme.Field("Threads (0 = auto)", Threads), Theme.Field("Depth", Depth)), false);
            Theme.Row(this, Theme.Label("Depth 0 searches the selected folder only. Method 2 is sequential. Adaptive mode requires the future RAFS controller.", true), false);
        }
    }
    public SearchRequest GetRequest() { var request = new SearchRequest { Folder = Folder.Text, Pattern = Query.Text, Method = Method.SelectedIndex + 1, Depth = (int)Depth.Value, Threads = (int)Threads.Value, Content = Content.Checked, CaseSensitive = CaseSensitive.Checked }; request.Validate(); return request; }
    public void SetRequest(SearchRequest request) { Folder.Text = request.Folder; Query.Text = request.Pattern; Method.SelectedIndex = request.Method - 1; Depth.Value = request.Depth; Threads.Value = request.Threads; Content.Checked = request.Content; CaseSensitive.Checked = request.CaseSensitive; }
    public void Demo() { Folder.Text = Path.Combine(SearchRunner.Root, "demo-data"); Query.Text = "report_*.txt"; Content.Checked = false; CaseSensitive.Checked = false; Depth.Value = 5; }
}
