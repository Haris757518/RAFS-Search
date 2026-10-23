using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal sealed class BenchmarkRunner
{
    private readonly ISearchBackend backend;
    public BenchmarkRunner(ISearchBackend backend) { this.backend = backend; }
    public BenchmarkResult Run(BenchmarkPlan plan, Action<Process> processChanged, Func<bool> cancelled, Action<RunProgress> progress)
    {
        if (plan.Runs < 1 || plan.Runs > 15 || plan.Warmups < 0 || plan.Warmups > 5 || plan.Configurations.Count < (plan.Lab ? 1 : 2)) throw new ArgumentException("Choose 1–15 measured runs, 0–5 warm-ups and at least one lab configuration (two for Compare).");
        plan.Request.Validate();
        if(plan.Request.Content && plan.Configurations.Any(c=>c.Method==3||c.Method==4))throw new ArgumentException("PowerShell and Windows Search Index comparison baselines support filename patterns only.");
        var result = new BenchmarkResult { Plan = plan };
        foreach (var configuration in plan.Configurations) result.Measurements.Add(new BenchmarkMeasurement { Configuration = configuration });
        HashSet<string> expected = null;
        int completed = 0, total = (plan.Runs + plan.Warmups) * plan.Configurations.Count;
        for (int round = -plan.Warmups; round < plan.Runs; round++) {
            // Rotate order each round. One real warm-up per configuration.
            for (int i = 0; i < plan.Configurations.Count; i++) {
                if (cancelled()) throw new OperationCanceledException();
                int index = (i + round + plan.Warmups) % plan.Configurations.Count;
                var measurement = result.Measurements[index];
                var request = plan.Request.Copy();
                request.Method = measurement.Configuration.Method;
                request.LegacyAdaptive=measurement.Configuration.LegacyAdaptive;
                request.Threads = request.Method != 1 ? 1 : measurement.Configuration.Threads;
                if(request.Method==5 && measurement.Configuration.Threads>0)request.MaximumWorkers=measurement.Configuration.Threads;
                progress(new RunProgress { Phase = round < 0 ? "Warm-up " + (round + plan.Warmups + 1) + " of " + plan.Warmups : "Run " + (round + 1) + " of " + plan.Runs, Request = request, Completed = completed, Total = total, Test = index + 1, Tests = plan.Configurations.Count });
                var report = backend.Run(request, processChanged, cancelled);
                if (!report.Succeeded) throw new InvalidOperationException("Comparison failed: " + report.Error);
                // Windows paths: full paths, consistent separators, case-insensitive deduplication and ordering.
                var normalized = report.Paths.Select(p => Path.GetFullPath(p).Replace('/', '\\')).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                report.Paths.Clear(); report.Paths.AddRange(normalized);
                var paths = new HashSet<string>(normalized, StringComparer.OrdinalIgnoreCase);
                if (expected == null) expected = paths;
                else if (!expected.SetEquals(paths)) throw new InvalidOperationException("RESULT MISMATCH: baseline has " + expected.Count + " files; " + measurement.Configuration.Name + " has " + paths.Count + ". " + (plan.Configurations.Any(c => c.Method == 4) ? "Windows Search Index may omit unindexed files or contain stale entries. " : "The folder may have changed. ") + "No graph will be presented.");
                result.Matches = report;
                if (round >= 0) {measurement.Samples.Add(report.TotalMilliseconds);measurement.EngineSamples.Add(report.EngineMilliseconds);}
                completed++;
            }
        }
        result.CompletedAt = DateTime.Now;
        progress(new RunProgress { Phase = "Complete", Request = plan.Request, Completed = total, Total = total });
        return result;
    }
}
