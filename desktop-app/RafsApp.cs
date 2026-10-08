// RAFS desktop demonstration interface. MIT license; see ../LICENSE.
// RAFS interface and comparison demonstration by Haris K (Haris757518).
// Runs the unmodified fsearch CLI by Hadi Cahyadi.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

internal sealed class SearchRequest
{
    public string Folder, Pattern;
    public int Method, Depth, Threads;
    public bool Content, CaseSensitive;
}

internal sealed class SearchReport
{
    public string Output, Error, EngineTime;
    public int ExitCode;
    public long TotalMilliseconds;
    public readonly List<string> Paths = new List<string>();
}

internal sealed class ComparisonRequest
{
    public SearchRequest Search;
}

internal sealed class ComparisonReport
{
    public SearchRequest Request;
    public SearchReport Matches;
    public readonly List<long> Sequential = new List<long>(), Parallel = new List<long>();
    public double SequentialMedian { get { return Median(Sequential); } }
    public double ParallelMedian { get { return Median(Parallel); } }
    private static double Median(List<long> values) { var sorted = values.OrderBy(v => v).ToArray(); return sorted[sorted.Length / 2]; }
}

internal static class Benchmark
{
    public static ComparisonReport Run(SearchRequest request, Action<Process> started, Func<bool> stopped)
    {
        var report = new ComparisonReport { Request = request };
        HashSet<string> expected = null;
        // Warm up both engines, then alternate run order to reduce cache/order bias.
        for (int round = -1; round < 5; round++) {
            foreach (int method in round % 2 == 0 ? new[] { 2, 1 } : new[] { 1, 2 }) {
                if (stopped()) throw new OperationCanceledException();
                var run = new SearchRequest { Folder = request.Folder, Pattern = request.Pattern, Depth = request.Depth,
                    Method = method, Threads = method == 1 ? request.Threads : 1,
                    Content = request.Content, CaseSensitive = request.CaseSensitive };
                var result = Engine.Run(run, started);
                if (stopped()) throw new OperationCanceledException();
                if (result.ExitCode != 0 && !(result.ExitCode == 1 && String.IsNullOrWhiteSpace(result.Error) && result.Paths.Count == 0))
                    throw new InvalidOperationException("Comparison search failed: " + result.Error);
                var paths = new HashSet<string>(result.Paths, StringComparer.OrdinalIgnoreCase);
                if (expected == null) expected = paths;
                else if (!expected.SetEquals(paths)) throw new InvalidOperationException("The methods returned different files. Results may have changed during the run; no comparison will be shown.");
                report.Matches = result;
                if (round >= 0) (method == 2 ? report.Sequential : report.Parallel).Add(result.TotalMilliseconds);
            }
        }
        return report;
    }
}

internal static class Engine
{
    public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Binary = Path.Combine(Root, "target", "release", "fsearch.exe");

    // Windows process argument quoting, including quotes and trailing backslashes.
    public static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value) {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character); slashes = 0;
        }
        result.Append('\\', slashes * 2); result.Append('"');
        return result.ToString();
    }

    public static SearchReport Run(SearchRequest request, Action<Process> started)
    {
        if (!File.Exists(Binary)) throw new FileNotFoundException("The Rust search engine is missing. Rebuild the baseline first.", Binary);
        if (!Directory.Exists(request.Folder)) throw new DirectoryNotFoundException("Choose an existing search folder.");
        if (String.IsNullOrWhiteSpace(request.Pattern)) throw new ArgumentException("Enter a filename pattern or text to search for.");
        string scratch = Path.Combine(Root, "target", "desktop-runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            // An isolated local override gives every run a fixed, explicit configuration.
            File.WriteAllText(Path.Combine(scratch, "fsearch.toml"), "threads = " + request.Threads + "\n", Encoding.UTF8);
            string arguments = "find " + Quote(request.Pattern) + " -p " + Quote(request.Folder) +
                " -d " + request.Depth + " -m " + request.Method + " -D" +
                (request.Content ? " -f" : "") + (request.CaseSensitive ? " -C" : "");
            var info = new ProcessStartInfo(Binary, arguments) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = scratch,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            info.EnvironmentVariables["NO_COLOR"] = "1";
            info.EnvironmentVariables.Remove("RAYON_NUM_THREADS");
            var output = new StringBuilder();
            var errors = new StringBuilder();
            var clock = Stopwatch.StartNew();
            using (var process = new Process { StartInfo = info })
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) errors.AppendLine(e.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (started != null) started(process);
                process.WaitForExit();
                clock.Stop();
                var report = new SearchReport {
                    Output = output.ToString(), Error = errors.ToString(),
                    ExitCode = process.ExitCode, TotalMilliseconds = clock.ElapsedMilliseconds
                };
                foreach (string line in report.Output.Split('\n'))
                {
                    Match match = Regex.Match(line.TrimEnd('\r'), @"^\d+\. (.+)$");
                    if (match.Success) report.Paths.Add(match.Groups[1].Value);
                }
                Match timing = Regex.Match(report.Output, @"\[(\d+)ms\]");
                report.EngineTime = timing.Success ? timing.Groups[1].Value + " ms" : "n/a";
                return report;
            }
        }
        finally
        {
            // This exact per-run directory contains only our generated configuration.
            try { File.Delete(Path.Combine(scratch, "fsearch.toml")); Directory.Delete(scratch); } catch (IOException) { }
        }
    }
}

internal sealed class RafsForm : Form
{
    private readonly TextBox folder = new TextBox(), pattern = new TextBox();
    private readonly RadioButton method1 = new RadioButton(), method2 = new RadioButton();
    private readonly NumericUpDown depth = new NumericUpDown(), threads = new NumericUpDown();
    private readonly CheckBox content = new CheckBox(), sensitive = new CheckBox();
    private readonly Button search = MakeButton("Search", true), cancel = MakeButton("Cancel", false);
    private readonly Button export = MakeButton("Export CSV", false), reveal = MakeButton("Show in folder", false);
    private readonly Button compare = MakeButton("Compare + graph", true);
    private readonly Label stats = new Label(), status = new Label();
    private readonly DataGridView results = new DataGridView();
    private readonly TextBox details = new TextBox();
    private readonly BackgroundWorker worker = new BackgroundWorker();
    private readonly object processLock = new object();
    private Process activeProcess;
    private bool cancelled;
    private SearchReport lastReport;
    private Panel inputs;
    private readonly TabControl tabs = new TabControl();
    private readonly TabPage comparisonTab = new TabPage("Performance comparison");
    private readonly Chart chart = new Chart();
    private readonly TextBox comparisonDetails = new TextBox();

    public RafsForm()
    {
        Text = "RAFS Search | Desktop Demo";
        ClientSize = new Size(1100, 740);
        MinimumSize = new Size(920, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(244, 247, 251);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(22, 14, 22, 12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(23, 39, 62) };
        header.Controls.Add(new Label { Text = "RAFS / File System Search", ForeColor = Color.White, Font = new Font("Segoe UI", 23, FontStyle.Bold), AutoSize = true, Location = new Point(18, 9) });
        header.Controls.Add(new Label { Text = "By Haris K  |  Desktop demonstration  |  Adaptive features planned", ForeColor = Color.FromArgb(180, 204, 224), AutoSize = true, Location = new Point(21, 54) });
        layout.Controls.Add(header, 0, 0);

        inputs = new Panel { Dock = DockStyle.Fill, Size = new Size(1056, 176) };
        inputs.Controls.Add(LabelAt("Search folder", 0, 10));
        folder.SetBounds(115, 7, 770, 28);
        folder.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        folder.Text = Path.Combine(Engine.Root, "src");
        inputs.Controls.Add(folder);
        var browse = MakeButton("Browse...", false);
        browse.SetBounds(900, 5, 130, 32);
        browse.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        browse.Click += delegate { using (var dialog = new FolderBrowserDialog { Description = "Choose the folder to search", SelectedPath = folder.Text, ShowNewFolderButton = false }) { if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; } };
        inputs.Controls.Add(browse);

        inputs.Controls.Add(LabelAt("Pattern / text", 0, 53));
        pattern.SetBounds(115, 50, 440, 28);
        pattern.Text = "*.rs";
        inputs.Controls.Add(pattern);
        content.Text = "Search file contents"; content.SetBounds(580, 51, 190, 28);
        sensitive.Text = "Case sensitive"; sensitive.SetBounds(790, 51, 160, 28);
        inputs.Controls.Add(content); inputs.Controls.Add(sensitive);

        inputs.Controls.Add(LabelAt("Method", 0, 97));
        var methods = new Panel(); methods.SetBounds(115, 87, 255, 46);
        method1.Text = "1 - WalkDir + Rayon"; method1.Checked = true; method1.SetBounds(0, 0, 255, 23);
        method2.Text = "2 - Recursive DFS"; method2.SetBounds(0, 23, 255, 23);
        methods.Controls.Add(method1); methods.Controls.Add(method2); inputs.Controls.Add(methods);
        inputs.Controls.Add(LabelAt("Depth", 395, 97));
        depth.SetBounds(452, 93, 80, 28); depth.Maximum = 1000; depth.Value = 5;
        inputs.Controls.Add(depth);
        inputs.Controls.Add(LabelAt("Threads", 580, 97));
        threads.SetBounds(660, 93, 80, 28); threads.Maximum = 256; threads.Value = 2;
        inputs.Controls.Add(threads);
        var threadHint = LabelAt("0 = automatic", 756, 97); threadHint.ForeColor = Color.FromArgb(89, 105, 124); inputs.Controls.Add(threadHint);
        method1.CheckedChanged += delegate { threads.Enabled = method1.Checked; threadHint.Text = method1.Checked ? "0 = automatic" : "Sequential method"; };

        search.SetBounds(115, 135, 130, 34); cancel.SetBounds(255, 135, 100, 34); cancel.Enabled = false;
        var demo = MakeButton("Load demo folder", false); demo.SetBounds(375, 135, 175, 34);
        demo.Click += delegate { string fixture = Path.GetFullPath(Path.Combine(Engine.Root, "..", "baseline-verification", "data")); folder.Text = Directory.Exists(fixture) ? fixture : Path.Combine(Engine.Root, "src"); pattern.Text = Directory.Exists(fixture) ? "baseline_*.txt" : "*.rs"; content.Checked = false; sensitive.Checked = false; depth.Value = 5; };
        inputs.Controls.Add(search); inputs.Controls.Add(demo);
        compare.SetBounds(570, 135, 190, 34); inputs.Controls.Add(compare);
        // Cancel stays enabled while the search inputs are disabled.
        inputs.Controls.Add(cancel);
        layout.Controls.Add(inputs, 0, 1);

        var metrics = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        stats.Text = "Ready to search  |  Choose a folder and pattern";
        stats.AutoSize = true; stats.Location = new Point(12, 14); metrics.Controls.Add(stats);
        layout.Controls.Add(metrics, 0, 2);

        tabs.Dock = DockStyle.Fill;
        var listTab = new TabPage("Matching files"); var rawTab = new TabPage("Search output / content matches");
        tabs.TabPages.Add(listTab); tabs.TabPages.Add(rawTab);
        tabs.TabPages.Add(comparisonTab);
        var comparisonLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        comparisonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        comparisonLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        chart.Dock = DockStyle.Fill; chart.BackColor = Color.White;
        var area = new ChartArea("Timing");
        area.AxisY.Title = "Median elapsed time (ms) - lower is better";
        area.AxisY.Minimum = 0; area.AxisY.MajorGrid.LineColor = Color.FromArgb(229, 237, 245);
        area.AxisX.MajorGrid.Enabled = false; area.AxisX.LabelStyle.Font = new Font("Segoe UI", 9);
        chart.ChartAreas.Add(area);
        chart.Titles.Add("Run Compare + graph to measure both methods");
        comparisonDetails.Dock = DockStyle.Fill; comparisonDetails.Multiline = true; comparisonDetails.ReadOnly = true;
        comparisonDetails.ScrollBars = ScrollBars.Vertical; comparisonDetails.BackColor = Color.White;
        comparisonDetails.Text = "Compares the original sequential method with parallel fsearch.\r\nSame folder, pattern, depth and matching settings. One warm-up and five measured runs per method.\r\nTimings include process launch and output collection. Adaptive functionality is not implemented.";
        comparisonLayout.Controls.Add(chart, 0, 0); comparisonLayout.Controls.Add(comparisonDetails, 0, 1);
        comparisonTab.Controls.Add(comparisonLayout);
        results.Dock = DockStyle.Fill; results.ReadOnly = true; results.AllowUserToAddRows = false; results.AllowUserToDeleteRows = false;
        results.RowHeadersVisible = false; results.SelectionMode = DataGridViewSelectionMode.FullRowSelect; results.MultiSelect = false;
        results.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        results.BackgroundColor = Color.White; results.BorderStyle = BorderStyle.None;
        results.EnableHeadersVisualStyles = false;
        results.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(229, 237, 245);
        results.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        results.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 252);
        results.Columns.Add("Name", "Filename"); results.Columns.Add("Path", "Full path");
        results.Columns[0].FillWeight = 26; results.Columns[1].FillWeight = 74;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 43 };
        export.Size = new Size(120, 32); reveal.Size = new Size(155, 32); export.Enabled = false; reveal.Enabled = false;
        toolbar.Controls.Add(reveal); toolbar.Controls.Add(export);
        listTab.Controls.Add(results); listTab.Controls.Add(toolbar);
        details.Dock = DockStyle.Fill; details.Multiline = true; details.ReadOnly = true; details.ScrollBars = ScrollBars.Both; details.WordWrap = false;
        details.Font = new Font("Consolas", 10); details.BackColor = Color.White;
        rawTab.Controls.Add(details); layout.Controls.Add(tabs, 0, 3);
        status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft; status.Text = "Depth 0 searches only the selected folder. Double-click a result to locate it.";
        layout.Controls.Add(status, 0, 4);
        var credit = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(89, 105, 124), Text = "RAFS by Haris K (Haris757518)  |  Original fsearch by Hadi Cahyadi  |  MIT License" };
        layout.Controls.Add(credit, 0, 5);
        AcceptButton = search;
        search.Click += delegate { BeginSearch(); };
        compare.Click += delegate { BeginComparison(); };
        cancel.Click += delegate { CancelSearch(); };
        reveal.Click += delegate { Reveal(); };
        results.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) Reveal(); };
        export.Click += delegate { Export(); };
        worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
            Action<Process> started = delegate(Process p) { lock (processLock) { activeProcess = p; if (cancelled && !p.HasExited) p.Kill(); } };
            var comparison = e.Argument as ComparisonRequest;
            e.Result = comparison == null ? (object)Engine.Run((SearchRequest)e.Argument, started) : Benchmark.Run(comparison.Search, started, delegate { lock (processLock) { return cancelled; } });
        };
        worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
            lock (processLock) { activeProcess = null; }
            SetBusy(false);
            if (cancelled) { status.Text = "Search cancelled. You can start a new search."; stats.Text = "Cancelled"; }
            else if (e.Error != null) { status.Text = e.Error.Message; stats.Text = "Search failed"; }
            else if (e.Result is ComparisonReport) { PresentComparison((ComparisonReport)e.Result); }
            else { Present((SearchReport)e.Result); }
        };
        FormClosing += delegate { CancelSearch(); };
    }

    private static Label LabelAt(string text, int x, int y) { return new Label { Text = text, AutoSize = true, Location = new Point(x, y) }; }
    private static Button MakeButton(string text, bool primary) {
        return new Button { Text = text, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = primary ? Color.FromArgb(15, 118, 110) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(23, 39, 62) };
    }
    private void SetBusy(bool busy) {
        foreach (Control control in inputs.Controls) control.Enabled = !busy;
        cancel.Enabled = busy; threads.Enabled = !busy && method1.Checked;
        export.Enabled = !busy && lastReport != null && lastReport.Paths.Count > 0;
        reveal.Enabled = export.Enabled;
    }
    private void BeginSearch() {
        if (worker.IsBusy) return;
        if (!Directory.Exists(folder.Text)) { status.Text = "Choose an existing folder first."; return; }
        if (String.IsNullOrWhiteSpace(pattern.Text)) { status.Text = "Enter a filename pattern, such as *.txt, or text to find."; return; }
        var request = new SearchRequest { Folder = Path.GetFullPath(folder.Text), Pattern = pattern.Text, Method = method1.Checked ? 1 : 2,
            Depth = (int)depth.Value, Threads = (int)threads.Value, Content = content.Checked, CaseSensitive = sensitive.Checked };
        cancelled = false; lastReport = null; results.Rows.Clear(); details.Clear(); SetBusy(true);
        stats.Text = "Searching...  |  Method " + request.Method + (request.Method == 1 ? "  |  Threads: " + (request.Threads == 0 ? "automatic" : request.Threads.ToString()) : "  |  Sequential");
        status.Text = "Search runs in the background. Results appear when the original engine finishes.";
        worker.RunWorkerAsync(request);
    }
    private void BeginComparison() {
        if (worker.IsBusy) return;
        if (!Directory.Exists(folder.Text) || String.IsNullOrWhiteSpace(pattern.Text)) { status.Text = "Choose an existing folder and enter a search pattern first."; return; }
        var request = new SearchRequest { Folder = Path.GetFullPath(folder.Text), Pattern = pattern.Text,
            Depth = (int)depth.Value, Threads = (int)threads.Value, Content = content.Checked, CaseSensitive = sensitive.Checked };
        cancelled = false; lastReport = null; results.Rows.Clear(); details.Clear(); SetBusy(true);
        chart.Series.Clear(); chart.Titles.Clear(); chart.Titles.Add("Measuring both methods...");
        comparisonDetails.Text = "Running one warm-up and five measured searches per method. Results are checked for equality on every run.";
        tabs.SelectedTab = comparisonTab; stats.Text = "Comparing sequential and parallel search...";
        status.Text = "Please leave the folder unchanged while the comparison runs. Cancel is available.";
        worker.RunWorkerAsync(new ComparisonRequest { Search = request });
    }
    internal void PresentComparison(ComparisonReport report) {
        Present(report.Matches);
        chart.Series.Clear(); chart.Titles.Clear(); chart.Titles.Add("Sequential baseline vs parallel fsearch");
        var series = new Series("Median timing") { ChartType = SeriesChartType.Column, IsValueShownAsLabel = true };
        int normal = series.Points.AddXY("Sequential recursive\nMethod 2 / one worker", report.SequentialMedian);
        int parallel = series.Points.AddXY("Parallel fsearch\nMethod 1 / " + (report.Request.Threads == 0 ? "automatic" : report.Request.Threads.ToString()) + " threads", report.ParallelMedian);
        series.Points[normal].Color = Color.FromArgb(92, 111, 137);
        series.Points[parallel].Color = Color.FromArgb(15, 118, 110);
        chart.Series.Add(series);
        double ratio = report.ParallelMedian > 0 ? report.SequentialMedian / report.ParallelMedian : 0;
        comparisonDetails.Text = "Verified: identical files in every run. " + report.Matches.Paths.Count + " matches.\r\n" +
            "Sequential median: " + report.SequentialMedian + " ms; parallel median: " + report.ParallelMedian + " ms. Ratio (sequential / parallel): " + ratio.ToString("0.00") + "x.\r\n" +
            "5 measured runs each, one warm-up each; alternating order. Total elapsed includes launch and output collection.\r\n" +
            "This compares baseline methods, not Windows Explorer or an adaptive engine. Cache, background activity and small datasets can dominate timings.";
        stats.Text = "Comparison complete  |  Same results verified  |  Lower bars mean faster searches";
        status.Text = "Folder: " + report.Request.Folder + "  |  Pattern: " + report.Request.Pattern + "  |  Depth: " + report.Request.Depth;
        tabs.SelectedTab = comparisonTab;
    }
    private void CancelSearch() {
        lock (processLock) {
            cancelled = true;
            if (activeProcess != null) { try { if (!activeProcess.HasExited) activeProcess.Kill(); } catch (InvalidOperationException) { } }
        }
    }
    internal void Present(SearchReport report) {
        lastReport = report; results.Rows.Clear();
        foreach (string path in report.Paths) results.Rows.Add(Path.GetFileName(path), path);
        details.Text = report.Output + (String.IsNullOrEmpty(report.Error) ? "" : "\r\nMessages:\r\n" + report.Error);
        stats.Text = report.Paths.Count + " matching file(s)  |  Engine: " + report.EngineTime + "  |  Total including launch: " + report.TotalMilliseconds + " ms";
        bool noResults = report.ExitCode == 1 && report.Paths.Count == 0 && String.IsNullOrWhiteSpace(report.Error);
        status.Text = report.ExitCode == 0 ? "Search complete. Select a file to locate it or export the results." : noResults ? "No matches found. Try another pattern or increase depth." : "Search failed. See Search output for details.";
        export.Enabled = report.Paths.Count > 0; reveal.Enabled = export.Enabled;
    }
    internal void PreviewFolder(string path) { folder.Text = path; pattern.Text = "demo_*.txt"; }
    private void Reveal() {
        if (results.CurrentRow == null) return;
        string path = Convert.ToString(results.CurrentRow.Cells[1].Value);
        if (!File.Exists(path)) { status.Text = "This file is no longer available."; return; }
        Process.Start(new ProcessStartInfo("explorer.exe", "/select," + Engine.Quote(path)) { UseShellExecute = true });
    }
    private void Export() {
        if (lastReport == null) return;
        using (var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "rafs-search-results.csv" }) {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllLines(dialog.FileName, new[] { "Filename,Full path" }.Concat(lastReport.Paths.Select(p => Csv(Path.GetFileName(p)) + "," + Csv(p))), new UTF8Encoding(true)); status.Text = "Results exported to " + dialog.FileName; }
            catch (IOException e) { status.Text = "Export failed: " + e.Message; }
            catch (UnauthorizedAccessException e) { status.Text = "Export failed: " + e.Message; }
        }
    }
    private static string Csv(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && args[0] == "--self-test") return SelfTest();
        Application.Run(new RafsForm()); return 0;
    }

    private static int SelfTest()
    {
        string artifactDir = Path.Combine(Engine.Root, "target", "desktop-verification");
        Directory.CreateDirectory(artifactDir);
        var log = new StringBuilder();
        try {
            string fixture = Path.Combine(artifactDir, "fixture with spaces");
            Directory.CreateDirectory(Path.Combine(fixture, "nested"));
            File.WriteAllText(Path.Combine(fixture, "demo_root.txt"), "RAFS demonstration marker");
            File.WriteAllText(Path.Combine(fixture, "nested", "demo_nested.txt"), "RAFS demonstration marker");
            SearchReport preview = null;
            foreach (int method in new[] { 1, 2 }) foreach (int count in new[] { 1, 2 }) {
                var request = new SearchRequest { Folder = fixture, Pattern = "demo_*.txt", Method = method, Depth = 5, Threads = count };
                var report = Engine.Run(request, null);
                if (report.ExitCode != 0 || report.Paths.Count != 2) throw new Exception("Recursive filename search failed: " + report.Error + report.Output);
                log.AppendLine("PASS method " + method + ", configured threads " + count + ": recursive filename search (folder includes spaces)");
                preview = report;
                request.Depth = 0; report = Engine.Run(request, null);
                if (report.ExitCode != 0 || report.Paths.Count != 1) throw new Exception("Depth control failed");
                log.AppendLine("PASS depth 0");
                request.Depth = 5; request.Content = true; request.Pattern = "RAFS demonstration marker";
                report = Engine.Run(request, null);
                if (report.ExitCode != 0 || report.Paths.Count != 2 || !report.Output.Contains("RAFS demonstration marker")) throw new Exception("Content search failed");
                log.AppendLine("PASS content search");
            }
            var missing = Engine.Run(new SearchRequest { Folder = fixture, Pattern = "nonexistent_rafs_match", Method = 1, Depth = 5, Threads = 1 }, null);
            if (missing.ExitCode != 1 || missing.Paths.Count != 0 || !String.IsNullOrWhiteSpace(missing.Error)) throw new Exception("No-match handling failed");
            log.AppendLine("PASS no-match handling");
            var trailing = Engine.Run(new SearchRequest { Folder = fixture + "\\", Pattern = "demo_*.txt", Method = 1, Depth = 5, Threads = 1 }, null);
            if (trailing.ExitCode != 0 || trailing.Paths.Count != 2) throw new Exception("Trailing path separator quoting failed");
            log.AppendLine("PASS trailing path separator");
            var comparison = Benchmark.Run(new SearchRequest { Folder = fixture, Pattern = "demo_*.txt", Depth = 5, Threads = 2 }, null, delegate { return false; });
            if (comparison.Sequential.Count != 5 || comparison.Parallel.Count != 5 || comparison.Matches.Paths.Count != 2) throw new Exception("Comparison failed");
            log.AppendLine("PASS repeated comparison: five runs each, identical results; medians " + comparison.SequentialMedian + " / " + comparison.ParallelMedian + " ms");
            // Render our own form for layout inspection, without automating the desktop.
            using (var form = new RafsForm()) {
                form.PreviewFolder(fixture); form.Present(preview); form.Show(); form.Refresh(); Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height)) {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(Path.Combine(artifactDir, "desktop-preview.png"));
                }
                form.PresentComparison(comparison); form.Refresh(); Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height)) {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(Path.Combine(artifactDir, "comparison-preview.png"));
                }
                form.Close();
            }
            log.AppendLine("PASS desktop form rendered");
            File.WriteAllText(Path.Combine(artifactDir, "checks.txt"), log.ToString()); return 0;
        }
        catch (Exception e) { log.AppendLine("FAIL " + e); File.WriteAllText(Path.Combine(artifactDir, "checks.txt"), log.ToString()); return 1; }
    }
}
