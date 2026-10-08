using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal interface ISearchBackend
{
    BackendCapabilities Capabilities { get; }
    SearchReport Run(SearchRequest request, Action<Process> processChanged, Func<bool> cancelled);
}
internal sealed class BaselineCliBackend : ISearchBackend
{
    public BackendCapabilities Capabilities { get { return new BackendCapabilities(); } }
    public SearchReport Run(SearchRequest request, Action<Process> processChanged, Func<bool> cancelled) { return SearchRunner.Run(request, processChanged, cancelled); }
}
internal static class SearchRunner
{
    public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Binary = Path.Combine(Root, "target", "release", "fsearch.exe");

    public static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value) {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c); slashes = 0;
        }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }
    public static SearchReport Run(SearchRequest request, Action<Process> processChanged, Func<bool> cancelled)
    {
        request.Validate();
        if (!File.Exists(Binary)) throw new FileNotFoundException("The Rust search engine is missing. Run cargo build --release first.", Binary);
        if (cancelled()) throw new OperationCanceledException();
        string scratch = Path.Combine(Root, "target", "desktop-runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try {
            File.WriteAllText(Path.Combine(scratch, "fsearch.toml"), "threads = " + request.Threads + "\n", new UTF8Encoding(false));
            string args = "find " + Quote(request.Pattern) + " -p " + Quote(request.Folder) + " -d " + request.Depth + " -m " + request.Method + " -D" +
                (request.Content ? " -f" : "") + (request.CaseSensitive ? " -C" : "");
            var info = new ProcessStartInfo(Binary, args) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = scratch,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            info.EnvironmentVariables["NO_COLOR"] = "1";
            info.EnvironmentVariables.Remove("RAYON_NUM_THREADS");
            var output = new StringBuilder(); var errors = new StringBuilder();
            var clock = Stopwatch.StartNew();
            using (var process = new Process { StartInfo = info }) {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) errors.AppendLine(e.Data); };
                try {
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                    processChanged(process);
                    if (cancelled()) { TryStop(process); throw new OperationCanceledException(); }
                    process.WaitForExit(); clock.Stop();
                    if (cancelled()) throw new OperationCanceledException();
                    var report = new SearchReport { Output = output.ToString(), Error = errors.ToString(), ExitCode = process.ExitCode, TotalMilliseconds = clock.Elapsed.TotalMilliseconds };
                    foreach (string line in report.Output.Split('\n')) {
                        Match match = Regex.Match(line.TrimEnd('\r'), @"^\d+\. (.+)$");
                        if (match.Success) report.Paths.Add(match.Groups[1].Value);
                    }
                    Match timing = Regex.Match(report.Output, @"\[(\d+)ms\]");
                    report.EngineTime = timing.Success ? timing.Groups[1].Value + " ms" : "--";
                    return report;
                } finally { processChanged(null); }
            }
        } finally {
            try { File.Delete(Path.Combine(scratch, "fsearch.toml")); Directory.Delete(scratch); } catch (IOException) { }
        }
    }
    public static void TryStop(Process process)
    {
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
    }
}
