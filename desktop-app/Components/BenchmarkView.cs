using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
internal sealed class BenchmarkView : TableLayoutPanel
{
    private readonly StatusBadge correctness = new StatusBadge();
    private readonly Chart chart = new Chart();
    private readonly DataGridView table = new DataGridView();
    private readonly Label details = Theme.Label("No measurements yet. Run a comparison to collect real timings.", true);
    public BenchmarkResult Result { get; private set; }
    public BenchmarkView()
    {
        Dock = DockStyle.Fill; ColumnCount = 1; RowCount = 4; Margin = new Padding(0);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        RowStyles.Add(new RowStyle(SizeType.AutoSize)); RowStyles.Add(new RowStyle(SizeType.Percent, 60)); RowStyles.Add(new RowStyle(SizeType.Percent, 40)); RowStyles.Add(new RowStyle(SizeType.AutoSize));
        correctness.Set("Not measured", false); Controls.Add(correctness, 0, 0);
        chart.Dock = DockStyle.Fill; chart.BackColor = Theme.Surface;
        var area = new ChartArea("Median"); area.AxisY.Minimum = 0; area.AxisY.Title = "Elapsed milliseconds"; area.AxisY.MajorGrid.LineColor = Theme.Border; area.AxisX.MajorGrid.Enabled = false;
        area.AxisX.LabelStyle.Font = Theme.Ui(10); area.AxisY.LabelStyle.Font = Theme.Mono(10); chart.ChartAreas.Add(area);
        chart.Titles.Add(new Title("Median Search Time", Docking.Top, Theme.Ui(12, FontStyle.Bold), Theme.Text));
        chart.Titles.Add(new Title("Lower is better", Docking.Top, Theme.Ui(10), Theme.Muted));
        Controls.Add(chart, 0, 1);
        table.Dock = DockStyle.Fill; table.ReadOnly = true; table.AllowUserToAddRows = false; table.RowHeadersVisible = false; table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        table.BackgroundColor = Theme.Surface; table.BorderStyle = BorderStyle.None; table.SelectionMode = DataGridViewSelectionMode.FullRowSelect; table.MultiSelect = false; Theme.StyleGrid(table);
        foreach (string name in new[] { "Strategy", "Threads", "Median", "Min", "Max", "Std Dev", "Matches", "Correct", "Relative Speed" }) table.Columns.Add(name, name);
        table.Columns[0].FillWeight = 160;
        table.Columns[0].MinimumWidth = 145; table.Columns[1].MinimumWidth = 100;
        foreach (DataGridViewColumn column in table.Columns) { column.SortMode = DataGridViewColumnSortMode.NotSortable; column.MinimumWidth = Math.Max(column.MinimumWidth, 70); }
        Controls.Add(table, 0, 2); details.MaximumSize = new Size(1500, 0); Controls.Add(details, 0, 3);
        SizeChanged += delegate { details.MaximumSize = new Size(Math.Max(1, ClientSize.Width - details.Margin.Horizontal), 0); };
    }
    public void Clear(string message) { Result = null; chart.Series.Clear(); table.Rows.Clear(); correctness.Set(message, false); details.Text = "Timings include process launch and output collection. Adaptive strategies are unavailable."; }
    public void Error(string error) { chart.Series.Clear(); table.Rows.Clear(); correctness.Set(error.StartsWith("RESULT MISMATCH") ? "✕ RESULT MISMATCH" : "Comparison failed", true); details.Text = error; }
    public void Present(BenchmarkResult result)
    {
        Result = result; table.Rows.Clear(); chart.Series.Clear(); correctness.Set("✓ IDENTICAL RESULTS · " + result.Matches.Paths.Count + " matches", false);
        var series = new Series("Median") { ChartType = SeriesChartType.Bar, IsValueShownAsLabel = true };
        double baseline = result.Measurements[0].Median;
        foreach (var measurement in result.Measurements) {
            double ratio = measurement.Median > 0 ? baseline / measurement.Median : 0;
            var configuration = measurement.Configuration;
            string prefix = result.Plan.Lab ? "" : (series.Points.Count == 0 ? "A · " : "B · ");
            int index = series.Points.AddXY(prefix + configuration.Name, measurement.Median);
            series.Points[index].Color = index == 0 ? Color.FromArgb(110, 126, 140) : Theme.Accent;
            series.Points[index].Label = measurement.Median.ToString("0.0") + " ms";
            table.Rows.Add(prefix + (configuration.Method == 2 ? "Recursive DFS" : "WalkDir + Rayon"), configuration.Method == 2 ? "1 (sequential)" : configuration.Threads == 0 ? "Auto" : configuration.Threads.ToString(),
                Theme.Milliseconds(measurement.Median), Theme.Milliseconds(measurement.Minimum), Theme.Milliseconds(measurement.Maximum), Theme.Milliseconds(measurement.StdDev), result.Matches.Paths.Count, "Yes", ratio.ToString("0.00") + "×");
        }
        chart.Series.Add(series);
        details.Text = result.Plan.Warmups + " warm-up(s) + " + result.Plan.Runs + " measured runs per configuration, rotating order. Relative speed uses the first configuration as baseline. Std Dev is population deviation. Process overhead and cache state affect timings.";
    }
}
