using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal static class Verification
{
    private static readonly StringBuilder log = new StringBuilder();
    private static string artifacts;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); log.AppendLine("PASS " + message); File.WriteAllText(Path.Combine(artifacts, "checks.txt"), log.ToString()); }
    private static SearchReport Run(SearchRequest request) { return new BaselineCliBackend().Run(request, delegate { }, delegate { return false; }); }
    private static void Wait(SearchSession session) {
        var timeout = Stopwatch.StartNew();
        while (session.IsBusy && timeout.Elapsed.TotalSeconds < 20) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
        Application.DoEvents(); Check(!session.IsBusy, "session completes without freezing the UI");
    }
    private static void Capture(AppShell app, string name, int width, int height) {
        app.ClientSize = new Size(width, height); app.ShowPage(name); app.PerformLayout(); app.Refresh(); Application.DoEvents();
        using (var bitmap = new Bitmap(app.Width, app.Height)) { app.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(artifacts, name.Replace(" ", "-").ToLowerInvariant() + "-" + width + ".png")); }
        Check(app.CurrentPage == name, name + " navigation at " + width + " × " + height);
        var viewport = FindViewport(app);
        if (viewport != null && viewport.VerticalScroll.Visible) {
            viewport.AutoScrollPosition = new Point(0, viewport.DisplayRectangle.Height);
            Application.DoEvents();
            using (var bitmap = new Bitmap(app.Width, app.Height)) { app.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(artifacts, name.Replace(" ", "-").ToLowerInvariant() + "-" + width + "-bottom.png")); }
            Check(viewport.AutoScrollPosition.Y < 0, name + " lower sections accessible by scrolling");
            viewport.AutoScrollPosition = Point.Empty;
        }
    }
    private static Panel FindViewport(Control parent) { foreach (Control child in parent.Controls) { var panel = child as Panel; if (panel != null && panel.AutoScroll && panel.VerticalScroll.Visible) return panel; var nested = FindViewport(child); if (nested != null) return nested; } return null; }
    public static int Run()
    {
        artifacts = Path.Combine(SearchRunner.Root, "target", "desktop-verification"); Directory.CreateDirectory(artifacts);
        try {
            string fixture = Path.Combine(artifacts, "fixture with spaces"); Directory.CreateDirectory(Path.Combine(fixture, "nested"));
            File.WriteAllText(Path.Combine(fixture, "demo_root.txt"), "RAFS demonstration marker\nLiteral \"quoted\" text\n");
            File.WriteAllText(Path.Combine(fixture, "nested", "demo_nested.txt"), "RAFS demonstration marker");
            File.WriteAllText(Path.Combine(fixture, "CASE.TXT"), "Case-sensitive demonstration");
            SearchReport previous = null;
            foreach (int method in new[] { 1, 2 }) foreach (int count in new[] { 1, 2, 4, 8, 0 }) {
                var request = new SearchRequest { Folder = fixture, Pattern = "demo_*.txt", Method = method, Depth = 5, Threads = count };
                var report = Run(request); Check(report.Succeeded && report.Paths.Count == 2, "method " + method + " / threads " + count + " recursive filename search");
                if (previous != null) Check(new HashSet<string>(previous.Paths).SetEquals(report.Paths), "result-set equality");
                previous = report;
                request.Depth = 0; Check(Run(request).Paths.Count == 1, "depth 0");
                request.Depth = 5; request.Content = true; request.Pattern = "RAFS demonstration marker";
                report = Run(request); Check(report.Succeeded && report.Paths.Count == 2 && report.Output.Contains("RAFS demonstration marker"), "content search");
            }
            Check(Run(new SearchRequest { Folder = fixture + "\\", Pattern = "demo_*.txt" }).Paths.Count == 2, "spaces and trailing separator quoting");
            Check(Run(new SearchRequest { Folder = fixture, Pattern = "Literal \"quoted\" text", Content = true }).Paths.Count == 1, "quoted content argument");
            Check(Run(new SearchRequest { Folder = fixture, Pattern = "CASE.TXT", CaseSensitive = true }).Paths.Count == 1, "case-sensitive matching");
            Check(Run(new SearchRequest { Folder = fixture, Pattern = "case.txt", CaseSensitive = true }).Paths.Count == 0, "case-sensitive negative match");
            Check(Run(new SearchRequest { Folder = fixture, Pattern = "CASE.TXT", CaseSensitive = true, Method = 2 }).Paths.Count == 1, "method 2 case-sensitive match");
            bool cancelled = false, launched = false;
            try {
                new BaselineCliBackend().Run(new SearchRequest { Folder = fixture, Pattern = "*" }, delegate(Process process) { if (process != null) { launched = true; cancelled = true; SearchRunner.TryStop(process); } }, delegate { return cancelled; });
                throw new Exception("Cancellation was ignored");
            } catch (OperationCanceledException) { Check(launched, "cancellation after the real CLI process launches"); }
            Check(Run(new SearchRequest { Folder = fixture, Pattern = "does-not-exist" }).Succeeded, "no-match exit code is handled as completed");
            try { Run(new SearchRequest { Folder = Path.Combine(fixture, "missing"), Pattern = "*" }); throw new Exception("Invalid folder accepted"); } catch (ArgumentException) { log.AppendLine("PASS invalid directory validation"); }
            var a = Run(new SearchRequest { Folder = Path.Combine(SearchRunner.Root, "src"), Pattern = "*.rs", Method = 1 });
            var b = Run(new SearchRequest { Folder = Path.Combine(SearchRunner.Root, "src"), Pattern = "*.rs", Method = 2 });
            Check(a.Succeeded && b.Succeeded && a.Paths.Count == 10 && new HashSet<string>(a.Paths).SetEquals(b.Paths), "real src/*.rs baseline regression: 10 identical files");
            var plan = new BenchmarkPlan { Request = new SearchRequest { Folder = fixture, Pattern = "demo_*.txt" }, Runs = 3, Warmups = 1 };
            plan.Configurations.Add(new RunConfiguration { Method = 1, Threads = 1 }); plan.Configurations.Add(new RunConfiguration { Method = 1, Threads = 2 });
            var comparison = new BenchmarkRunner(new BaselineCliBackend()).Run(plan, delegate { }, delegate { return false; }, delegate { });
            Check(comparison.Measurements.Count == 2 && comparison.Measurements.All(m => m.Samples.Count == 3), "configurable A/B comparison including same-method thread comparison");
            string changedFile = Path.Combine(fixture, "demo_changed.txt");
            try {
                new BenchmarkRunner(new BaselineCliBackend()).Run(plan, delegate { }, delegate { return false; }, delegate(RunProgress progress) { if (progress.Completed == 1) File.WriteAllText(changedFile, "changed workload"); });
                throw new Exception("Changed result set was accepted");
            } catch (InvalidOperationException error) { Check(error.Message.StartsWith("RESULT MISMATCH"), "real filesystem change triggers correctness mismatch and suppresses comparison"); }
            finally { if (File.Exists(changedFile)) File.Delete(changedFile); }
            var lab = new BenchmarkPlan { Request = plan.Request.Copy(), Runs = 2, Warmups = 1, Lab = true };
            lab.Configurations.Add(new RunConfiguration { Method = 2, Threads = 1 });
            foreach (int count in new[] { 1, 2, 4, 8, 0 }) lab.Configurations.Add(new RunConfiguration { Method = 1, Threads = count });
            var benchmark = new BenchmarkRunner(new BaselineCliBackend()).Run(lab, delegate { }, delegate { return false; }, delegate { });
            Check(benchmark.Measurements.Count == 6 && benchmark.Measurements.All(m => m.Samples.Count == 2) && benchmark.Matches.Paths.Count == 2, "real six-configuration benchmark matrix");
            ReportExporter.Results(Path.Combine(artifacts, "results.csv"), a);
            ReportExporter.Benchmark(Path.Combine(artifacts, "benchmark.csv"), benchmark);
            Check(File.ReadAllLines(Path.Combine(artifacts, "results.csv")).Length == 11 && File.ReadAllLines(Path.Combine(artifacts, "benchmark.csv")).Length == 13, "CSV results and measured timing sample export");
            var settings = new SettingsService(Path.Combine(artifacts, "settings-test.xml"));
            settings.Settings.DefaultDepth = 8; settings.Settings.MaximumWorkers = 12; settings.Save();
            var restored = new SettingsService(settings.FilePath); Check(restored.Settings.DefaultDepth == 8 && restored.Settings.MaximumWorkers == 12, "persisted defaults and future worker limit");
            using (var app = new AppShell(new BaselineCliBackend(), settings)) {
                app.Show(); Application.DoEvents();
                app.Search.Options.SetRequest(plan.Request);
                app.Session.StartSearch(plan.Request); app.ShowPage("Live Monitor");
                Check(app.Session.State != SessionState.Cancelled, "navigation does not cancel or duplicate active search");
                Wait(app.Session); app.ShowPage("Search");
                Check(app.Session.State == SessionState.Completed && app.Search.Grid.MatchCount == 2 && app.Search.Options.Query.Text == plan.Request.Pattern, "Search results and inputs retained across page navigation");
                Check(app.Session.Telemetry == null && app.Session.Decisions.Count == 0 && !app.Session.Capabilities.Adaptive, "no fabricated adaptive metrics or events");
                app.Session.StartSearch(plan.Request); app.Session.Cancel(); Wait(app.Session); Check(app.Session.State == SessionState.Cancelled, "real search cancellation");
                app.Session.StartBenchmark(plan); app.ShowPage("Live Monitor"); Wait(app.Session); Check(app.Session.Comparison != null, "session comparison in background");
                app.Session.StartBenchmark(lab); app.Session.Cancel(); Wait(app.Session); Check(app.Session.State == SessionState.Cancelled && app.Session.LabResult == null, "benchmark cancellation discards partial results");
                app.Session.StartBenchmark(lab); Wait(app.Session); Check(app.Session.LabResult != null, "session Benchmark Lab completion");
                app.Session.StartSearch(plan.Request); Wait(app.Session);
                foreach (string page in new[] { "Search", "Live Monitor", "Compare", "Benchmark Lab", "Settings" }) { Capture(app, page, 1200, 750); Capture(app, page, 1440, 900); }
                app.Close();
            }
            Check(File.Exists(Path.Combine(SearchRunner.Root, "LICENSE")), "View License target exists");
            Check(!Directory.GetDirectories(Path.Combine(SearchRunner.Root, "target", "desktop-runs")).Any(), "isolated per-run configurations cleaned after completion and cancellation");
            if (Environment.GetCommandLineArgs().Contains("--shell-actions")) {
                DesktopActions.ViewLicense(); Check(true, "Windows accepted View License in Notepad");
                DesktopActions.Reveal(Path.Combine(SearchRunner.Root, "src", "searcher.rs")); Check(true, "Windows accepted Show in Folder for src/searcher.rs");
                DesktopActions.OpenFolder(); Check(true, "Windows accepted Open Project Folder");
            }
            log.AppendLine("DPI configuration: PerMonitorV2 manifest/config and .NET Framework 4.8 target. Physical 125%/150% monitor switching requires a manual Windows display test.");
            File.WriteAllText(Path.Combine(artifacts, "checks.txt"), log.ToString()); return 0;
        } catch (Exception error) { log.AppendLine("FAIL " + error); File.WriteAllText(Path.Combine(artifacts, "checks.txt"), log.ToString()); return 1; }
    }
}
