using System;
using System.Windows.Forms;
internal sealed class ComparePage : AppPage
{
    private readonly SearchSession session;
    private readonly SearchOptionsPanel options;
    private readonly ConfigurationPicker configA, configB;
    private readonly NumericUpDown depth, runs, warmups;
    private readonly Button start = Theme.Button("Run Comparison", true), cancel = Theme.Button("Cancel"), export = Theme.Button("Export CSV");
    private readonly BenchmarkView view = new BenchmarkView();
    private readonly Label status = Theme.Label("Ready to compare.", true);
    private BenchmarkResult shown;
    private bool wasBusy;
    public ComparePage(SearchSession session, AppSettings settings, Func<SearchRequest> searchSettings) : base("Strategy Comparison", "Compare filesystem search configurations under the same workload.")
    {
        this.session = session;
        var input = new SectionPanel(""); options = new SearchOptionsPanel(settings, false); Theme.Row(input, options, false);
        depth = Theme.Number(settings.DefaultDepth, 1000); runs = Theme.Number(settings.ComparisonRuns, 15, 1); warmups = Theme.Number(1, 5);
        Theme.Row(input, Theme.Flow(Theme.Field("Maximum Depth", depth), Theme.Field("Warm-up Runs", warmups), Theme.Field("Measured Runs", runs)), false);
        var configs = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        configs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); configs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        configA = new ConfigurationPicker("Configuration A", 2, 1); configB = new ConfigurationPicker("Configuration B", 1, settings.DefaultThreads);
        configs.Controls.Add(configA, 0, 0); configs.Controls.Add(configB, 1, 0); Theme.Row(input, configs, false);
        var useSearch = Theme.Button("Use Search settings"); useSearch.Click += delegate { try { var request = searchSettings(); options.SetRequest(request); depth.Value = request.Depth; } catch (Exception e) { status.Text = e.Message; } };
        Theme.Row(input, Theme.Flow(start, cancel, useSearch, export), false); Theme.Row(Body, input, false);
        Theme.Row(Body, view, true); status.MaximumSize = new System.Drawing.Size(1500, 0); Theme.Row(Body, status, false);
        start.Click += delegate {
            try { var plan = new BenchmarkPlan { Request = options.GetRequest(), Runs = (int)runs.Value, Warmups = (int)warmups.Value }; plan.Request.Depth = (int)depth.Value; plan.Configurations.Add(configA.GetConfiguration()); plan.Configurations.Add(configB.GetConfiguration()); view.Clear("Measuring..."); session.StartBenchmark(plan); }
            catch (Exception e) { status.Text = e.Message; }
        };
        cancel.Click += delegate { session.Cancel(); }; export.Click += delegate { Export(); };
    }
    private void Export() { if (view.Result == null) return; using (var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "rafs-comparison.csv" }) if (dialog.ShowDialog(FindForm()) == DialogResult.OK) { try { ReportExporter.Benchmark(dialog.FileName, view.Result); status.Text = "Measured timing samples exported."; } catch (Exception e) { status.Text = e.Message; } } }
    public override void RefreshSession(SearchSession current)
    {
        options.Enabled = configA.Enabled = configB.Enabled = depth.Enabled = runs.Enabled = warmups.Enabled = start.Enabled = !current.IsBusy;
        cancel.Enabled = current.IsBusy && current.State != SessionState.Cancelling; export.Enabled = !current.IsBusy && view.Result != null;
        if (current.Comparison != null && shown != current.Comparison) { shown = current.Comparison; view.Present(shown); }
        if (current.Operation == "Compare") {
            status.Text = current.IsBusy && current.Progress != null ? "Configuration " + current.Progress.Test + " of " + current.Progress.Tests + " · " + current.Progress.Request.MethodName + " · " + current.Progress.Phase : current.Message;
            if (wasBusy && current.State == SessionState.Failed) view.Error(current.Message);
            if (wasBusy && current.State == SessionState.Cancelled) view.Clear("Comparison cancelled — incomplete timings discarded");
        }
        wasBusy = current.IsBusy;
    }
}
