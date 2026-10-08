using System;
using System.Collections.Generic;
using System.Windows.Forms;
internal sealed class BenchmarkPage : AppPage
{
    private readonly SearchOptionsPanel options;
    private readonly NumericUpDown depth, runs, warmups;
    private readonly CheckBox method1 = new CheckBox { Text = "Method 1 — WalkDir + Rayon", Checked = true, AutoSize = true }, method2 = new CheckBox { Text = "Method 2 — Recursive DFS", Checked = true, AutoSize = true };
    private readonly Dictionary<int, CheckBox> counts = new Dictionary<int, CheckBox>();
    private readonly Button start = Theme.Button("Run Benchmark Matrix", true), cancel = Theme.Button("Cancel"), export = Theme.Button("Export CSV");
    private readonly BenchmarkView view = new BenchmarkView();
    private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill };
    private readonly Label status = Theme.Label("Ready to benchmark a custom folder.", true);
    private BenchmarkResult shown;
    private bool wasBusy;
    public BenchmarkPage(SearchSession session, AppSettings settings) : base("Benchmark Lab", "Run repeatable filesystem-search experiments.")
    {
        var input = new SectionPanel("Workload — Custom Folder"); options = new SearchOptionsPanel(settings, false); Theme.Row(input, options, false);
        Theme.Row(input, Theme.Label("Planned generators: Deep / Narrow · Shallow / Wide · Mixed / Unbalanced · Many Small Files. No synthetic data is generated.", true), false);
        depth = Theme.Number(settings.DefaultDepth, 1000); runs = Theme.Number(settings.ComparisonRuns, 15, 1); warmups = Theme.Number(1, 5);
        var runHeading = Theme.Label("Runs"); runHeading.Font = Theme.Ui(11, System.Drawing.FontStyle.Bold); Theme.Row(input, runHeading, false);
        Theme.Row(input, Theme.Flow(Theme.Field("Maximum Depth", depth), Theme.Field("Warm-up Runs", warmups), Theme.Field("Measured Runs", runs)), false);
        var threads = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        threads.Controls.Add(Theme.Label("Method 1 thread counts:"));
        foreach (int count in new[] { 1, 2, 4, 8, 0 }) { var check = new CheckBox { Text = count == 0 ? "Auto / 0" : count.ToString(), Checked = true, AutoSize = true }; counts.Add(count, check); threads.Controls.Add(check); }
        method1.CheckedChanged += delegate { foreach (var item in counts) item.Value.Enabled = method1.Checked; };
        var configurationHeading = Theme.Label("Configurations"); configurationHeading.Font = Theme.Ui(11, System.Drawing.FontStyle.Bold); Theme.Row(input, configurationHeading, false);
        Theme.Row(input, Theme.Flow(method1, method2), false); Theme.Row(input, threads, false);
        Theme.Row(input, Theme.Flow(start, cancel, export), false); Theme.Row(Body, input, false);
        Theme.Row(Body, view, true); Theme.Row(Body, progress, false); progress.Height = 12; Theme.Row(Body, status, false);
        start.Click += delegate {
            try {
                var plan = new BenchmarkPlan { Request = options.GetRequest(), Runs = (int)runs.Value, Warmups = (int)warmups.Value, Lab = true }; plan.Request.Depth = (int)depth.Value;
                // The first selected configuration is the explicitly chosen correctness/speed reference.
                if (method2.Checked) plan.Configurations.Add(new RunConfiguration { Method = 2, Threads = 1 });
                if (method1.Checked) foreach (int count in new[] { 1, 2, 4, 8, 0 }) if (counts[count].Checked) plan.Configurations.Add(new RunConfiguration { Method = 1, Threads = count });
                if (plan.Configurations.Count == 0) throw new ArgumentException("Select at least one method/thread configuration.");
                view.Clear("Measuring real searches..."); session.StartBenchmark(plan);
            } catch (Exception e) { status.Text = e.Message; }
        };
        cancel.Click += delegate { session.Cancel(); };
        export.Click += delegate {
            if (view.Result == null) return;
            using (var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "rafs-benchmark.csv" }) if (dialog.ShowDialog(FindForm()) == DialogResult.OK) {
                try { ReportExporter.Benchmark(dialog.FileName, view.Result); status.Text = "Actual benchmark samples exported."; } catch (Exception e) { status.Text = e.Message; }
            }
        };
    }
    public override void RefreshSession(SearchSession current)
    {
        options.Enabled = depth.Enabled = runs.Enabled = warmups.Enabled = method1.Enabled = method2.Enabled = start.Enabled = !current.IsBusy;
        foreach (var count in counts) count.Value.Enabled = !current.IsBusy && method1.Checked;
        cancel.Enabled = current.IsBusy && current.State != SessionState.Cancelling; export.Enabled = !current.IsBusy && view.Result != null;
        if (current.LabResult != null && current.LabResult != shown) { shown = current.LabResult; view.Present(shown); }
        if (current.Operation == "Benchmark Lab") {
            if (current.IsBusy && current.Progress != null) { var p = current.Progress; progress.Maximum = Math.Max(1, p.Total); progress.Value = Math.Min(p.Completed, progress.Maximum); status.Text = "Configuration " + p.Test + " of " + p.Tests + " · " + p.Request.MethodName + " · " + p.Request.ThreadLabel + " · " + p.Phase; }
            else { status.Text = current.Message; if (current.State == SessionState.Completed) progress.Value = progress.Maximum; }
            if (wasBusy && current.State == SessionState.Failed) view.Error(current.Message);
            if (wasBusy && current.State == SessionState.Cancelled) view.Clear("Benchmark cancelled — incomplete timings discarded");
        }
        wasBusy = current.IsBusy;
    }
}
