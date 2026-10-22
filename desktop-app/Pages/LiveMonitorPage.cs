using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
internal sealed class LiveMonitorPage : AppPage
{
    private readonly MetricCard elapsed = new MetricCard("Elapsed Time"), matches = new MetricCard("Matches"), strategy = new MetricCard("Strategy"), workers = new MetricCard("Workers");
    private readonly StatusBadge status = new StatusBadge();
    private readonly Label workload = Theme.Label("No active search.", true);
    private readonly Dictionary<string, Label> values = new Dictionary<string, Label>();
    private readonly ThroughputView throughput = new ThroughputView();
    private readonly DecisionTimeline timeline = new DecisionTimeline();
    private RuntimeTelemetry previous;
    private DateTime? previousStart;
    private int previousRun=-1;
    public LiveMonitorPage() : base("Adaptive Engine Monitor", "Observe filesystem workload and runtime scheduling behaviour.")
    {
        Theme.Row(Body, Theme.Flow(status), false); Theme.Row(Body, MetricCard.Row(elapsed, matches, strategy, workers), false);
        var context = new SectionPanel("Current session — real application state") {AutoSize=false,Height=130}; workload.MaximumSize = new Size(1500, 0); Theme.Row(context, workload, false); Theme.Row(Body, context, false);
        var planned = new SectionPanel("Filesystem workload — measured during adaptive search") {AutoSize=false,Height=220};
        var metrics = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4 };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32)); metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18)); metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32)); metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        int i = 0;
        foreach (string name in new[] { "Files Scanned", "Directories Scanned", "Throughput", "Pending Directories", "Current Depth", "Branching Factor", "Queue Capacity", "CPU Usage (%)", "Memory (bytes)", "Process I/O (bytes/s)", "Tree Workload", "Max Depth" }) {
            var value = Theme.Label("--", true); value.Font = Theme.Mono(); values.Add(name, value);
            metrics.Controls.Add(Theme.Label(name), (i % 2) * 2, i / 2); metrics.Controls.Add(value, (i % 2) * 2 + 1, i / 2); i++;
        }
        Theme.Row(planned, metrics, false); Theme.Row(Body, planned, false);
        var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var graph = new SectionPanel("Throughput Over Time") { AutoSize = false }; Theme.Row(graph, throughput, true);
        var decisions = new SectionPanel("Adaptive Decision Timeline") { AutoSize = false }; Theme.Row(decisions, timeline, true);
        lower.Controls.Add(graph, 0, 0); lower.Controls.Add(decisions, 1, 0); Theme.Row(Body, lower, true);
    }
    public override void RefreshSession(SearchSession session)
    {
        status.Set(session.StatusText, session.State == SessionState.Failed);
        elapsed.Set(session.StartTime.HasValue ? session.Elapsed.TotalSeconds.ToString("0.0") + " s" : "--", "Application session elapsed");
        matches.Set(session.Report == null ? "--" : session.Report.Paths.Count.ToString(), session.Report == null ? "Available on completion" : "Reported by the search engine");
        var request = session.Progress != null && session.IsBusy ? session.Progress.Request : session.Request;
        strategy.Set(session.Telemetry!=null?session.Telemetry.CurrentTraversal:request==null?"--":request.MethodName, request==null?"No active request":request.MethodName);
        workers.Set(session.Telemetry != null && session.Telemetry.ActiveWorkers.HasValue ? session.Telemetry.ActiveWorkers.Value.ToString() : "--", request == null ? "Adaptive workers reported live" : "Configured: " + request.ThreadLabel);
        workload.Text = request == null ? "Select RAFS Adaptive on Search to see live workload counters." : "Query: " + request.Pattern + "\nFolder: " + request.Folder + "\nConfigured depth: " + request.Depth + " · " + session.Operation + (session.Progress == null ? "" : " · Test " + session.Progress.Test + " of " + session.Progress.Tests + " · " + session.Progress.Phase);
        int run=session.Progress!=null&&session.Progress.Completed<session.Progress.Total?session.Progress.Completed:previousRun;
        if (session.StartTime != previousStart || run!=previousRun) { previousStart = session.StartTime; previousRun=run;previous = null; throughput.Reset(); foreach (var item in values) item.Value.Text = "--"; }
        var data = session.Telemetry;
        if (data != null && data != previous) {
            previous = data; throughput.Add(data);
            Put("Files Scanned", data.FilesScanned); Put("Directories Scanned", data.DirectoriesScanned); Put("Throughput", data.Throughput);
            Put("Pending Directories", data.PendingDirectories); Put("Current Depth", data.CurrentDepth); Put("Branching Factor", data.BranchingFactor);
            Put("Queue Capacity", data.QueueCapacity); Put("CPU Usage (%)", data.CpuUsage); Put("Memory (bytes)", data.MemoryUsage); Put("Process I/O (bytes/s)", data.DiskIo); Put("Max Depth", data.MaxDepth);
            Put("Tree Workload",data.Workload);
        }
        timeline.Present(session.Decisions);
    }
    private void Put(string key, object value) { values[key].Text = value == null ? "--" : value is double ? ((double)value).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture); }
}
