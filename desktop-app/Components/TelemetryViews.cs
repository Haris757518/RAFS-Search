using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
internal sealed class ThroughputView : Panel
{
    private readonly Chart chart = new Chart();
    private readonly Label empty = new Label();
    private DateTime? start;
    public ThroughputView()
    {
        Dock = DockStyle.Fill; BackColor = Theme.Surface;
        chart.Dock = DockStyle.Fill; chart.Visible = false;
        var area = new ChartArea("Throughput"); area.AxisX.Title = "Seconds"; area.AxisY.Title = "Files / second"; area.AxisY.Minimum = 0; chart.ChartAreas.Add(area);
        chart.Series.Add(new Series("Files per second") { ChartType = SeriesChartType.Line, Color = Theme.Accent, BorderWidth = 2 });
        Controls.Add(chart);
        empty.Text = "Runtime throughput telemetry will be available after RAFS instrumentation."; empty.Dock = DockStyle.Fill; empty.TextAlign = ContentAlignment.MiddleCenter; empty.Padding = new Padding(14);
        empty.ForeColor = Theme.Muted; empty.BackColor = Theme.Surface; Controls.Add(empty); empty.BringToFront();
    }
    public void Reset() { start = null; chart.Series[0].Points.Clear(); empty.Visible = true; chart.Visible = false; }
    public void Add(RuntimeTelemetry data) {
        if (!data.Throughput.HasValue) return;
        if (!start.HasValue) start = data.Timestamp;
        empty.Visible = false; chart.Visible = true; chart.Series[0].Points.AddXY((data.Timestamp - start.Value).TotalSeconds, data.Throughput.Value);
    }
}
internal sealed class DecisionTimeline : Panel
{
    private readonly ListView list = new ListView();
    private readonly Label empty = new Label();
    public DecisionTimeline()
    {
        Dock = DockStyle.Fill; BackColor = Theme.Surface;
        list.Dock = DockStyle.Fill; list.Visible = false; list.View = View.Details; list.FullRowSelect = true;
        list.Columns.Add("Time", 90); list.Columns.Add("Event", 160); list.Columns.Add("Change", 120); list.Columns.Add("Reason", 360); list.Columns.Add("Measured value", 130);
        Controls.Add(list);
        empty.Text = "Adaptive controller not enabled yet."; empty.Dock = DockStyle.Fill; empty.TextAlign = ContentAlignment.MiddleCenter; empty.Padding = new Padding(14);
        empty.ForeColor = Theme.Muted; Controls.Add(empty); empty.BringToFront();
    }
    public void Present(IList<AdaptiveDecision> decisions) {
        empty.Visible = decisions.Count == 0; list.Visible = decisions.Count > 0;
        if (decisions.Count == list.Items.Count) return;
        list.Items.Clear(); empty.Visible = decisions.Count == 0;
        foreach (var decision in decisions) list.Items.Add(new ListViewItem(new[] { decision.Timestamp.ToString("HH:mm:ss.fff"), decision.EventType, decision.OldValue + " → " + decision.NewValue, decision.Reason, decision.MeasuredValue.HasValue ? decision.MeasuredValue.Value.ToString("0.00") : "--" }));
    }
}
