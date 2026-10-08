using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal sealed class SearchPage : AppPage
{
    private readonly SearchSession session;
    internal readonly SearchOptionsPanel Options;
    internal readonly ResultGrid Grid = new ResultGrid();
    private readonly TextBox output = new TextBox();
    private readonly MetricCard matches = new MetricCard("Matches"), elapsed = new MetricCard("Engine elapsed"), total = new MetricCard("Total including launch");
    private readonly StatusBadge status = new StatusBadge();
    private readonly ProgressBar progress = new ProgressBar { Style = ProgressBarStyle.Marquee, Width = 110, Height = 15 };
    private readonly Button start = Theme.Button("Start Search", true), cancel = Theme.Button("Cancel"), reveal = Theme.Button("Show in Folder"), export = Theme.Button("Export CSV");
    private readonly Label banner = Theme.Label("");
    private int revision = -1;
    public SearchPage(SearchSession session, AppSettings settings) : base("Search", "Search the live filesystem efficiently.")
    {
        this.session = session;
        var input = new SectionPanel(""); Options = new SearchOptionsPanel(settings, true); Theme.Row(input, Options, false);
        var demo = Theme.Button("Load demo folder"); demo.Click += delegate { Options.Demo(); };
        Theme.Row(input, Theme.Flow(start, cancel, demo), false); Theme.Row(Body, input, false);
        Theme.Row(Body, MetricCard.Row(matches, elapsed, total), false);
        banner.ForeColor = Theme.Error; banner.MaximumSize = new System.Drawing.Size(1600, 0); Theme.Row(Body, banner, false);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var files = new TabPage("Results"); var logs = new TabPage("Raw output / content matches");
        var list = Theme.Stack(); Theme.Row(list, Grid, true); Theme.Row(list, Theme.Flow(reveal, export), false); files.Controls.Add(list);
        output.Dock = DockStyle.Fill; output.Multiline = true; output.ReadOnly = true; output.ScrollBars = ScrollBars.Both; output.WordWrap = false; output.Font = Theme.Mono(); output.BackColor = Theme.Surface;
        logs.Controls.Add(output); tabs.TabPages.Add(files); tabs.TabPages.Add(logs); Theme.Row(Body, tabs, true);
        Theme.Row(Body, Theme.Flow(status, progress), false);
        start.Click += delegate { Start(); }; cancel.Click += delegate { session.Cancel(); };
        reveal.Click += delegate { Reveal(); }; Grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) Reveal(); };
        export.Click += delegate { Export(); };
    }
    internal void Start() { banner.Text = ""; try { session.StartSearch(Options.GetRequest()); } catch (Exception e) { if (!(e is ArgumentException || e is InvalidOperationException)) throw; banner.Text = e.Message; } }
    private void Reveal() { try { if (Grid.SelectedPath == null) return; if (!File.Exists(Grid.SelectedPath)) throw new IOException("The selected file is no longer available."); DesktopActions.Reveal(Grid.SelectedPath); banner.Text = ""; } catch (Exception e) { banner.Text = e.Message; } }
    private void Export() {
        if (session.LastSearchReport == null) return;
        using (var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "rafs-search-results.csv" }) if (dialog.ShowDialog(FindForm()) == DialogResult.OK) {
            try { ReportExporter.Results(dialog.FileName, session.LastSearchReport); banner.ForeColor = Theme.Accent; banner.Text = "Results exported."; } catch (Exception e) { banner.ForeColor = Theme.Error; banner.Text = e.Message; }
        }
    }
    public override void RefreshSession(SearchSession current)
    {
        Options.Enabled = !current.IsBusy; start.Enabled = !current.IsBusy; cancel.Enabled = current.IsBusy && current.State != SessionState.Cancelling;
        bool running = current.IsBusy && current.Operation == "Search";
        progress.Visible = running; status.Set(running ? current.Message + "  " + current.Elapsed.TotalSeconds.ToString("0.0") + " s" : current.Operation == "Search" ? current.Message : "Search results retained", current.State == SessionState.Failed && current.Operation == "Search");
        if (revision != current.SearchRevision) {
            revision = current.SearchRevision; Grid.SetReport(current.LastSearchReport);
            var report = current.LastSearchReport;
            output.Text = report == null ? "" : report.Output + (String.IsNullOrWhiteSpace(report.Error) ? "" : "\r\nMessages:\r\n" + report.Error);
            matches.Set(report == null ? "--" : report.Paths.Count.ToString()); elapsed.Set(report == null ? "--" : report.EngineTime);
            total.Set(report == null ? "--" : Theme.Milliseconds(report.TotalMilliseconds), "Process launch + output capture");
        }
        if (current.State == SessionState.Failed && current.Operation == "Search") { banner.ForeColor = Theme.Error; banner.Text = current.Message; }
        reveal.Enabled = export.Enabled = !current.IsBusy && Grid.MatchCount > 0;
    }
}
