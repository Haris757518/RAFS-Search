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
internal sealed class BaselineCliBackend : ISearchBackend, IRuntimeTelemetrySource
{
    public event Action<RuntimeTelemetry> TelemetryReceived;
    public event Action<AdaptiveDecision> DecisionReceived;
    public BackendCapabilities Capabilities { get { return new BackendCapabilities(true, true); } }
    public SearchReport Run(SearchRequest request, Action<Process> processChanged, Func<bool> cancelled) {
        if (request.DuplicateMode==null && request.Method == 4) return WindowsIndexSearchRunner.Run(request, processChanged, cancelled);
        if (request.DuplicateMode==null && request.Method == 3) return PowerShellSearchRunner.Run(request, processChanged, cancelled);
        using (var telemetry = new TelemetryReader(delegate(RuntimeTelemetry data) { var handler=TelemetryReceived; if(handler!=null)handler(data); }, delegate(AdaptiveDecision data) { var handler=DecisionReceived; if(handler!=null)handler(data); }))
            return SearchRunner.Run(request, delegate(Process process) { telemetry.SetProcess(process);processChanged(process); }, cancelled, request.Method==5 ? (Action<string>)telemetry.Receive : null);
    }
}
internal static class SearchRunner
{
    public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Binary = File.Exists(Path.Combine(Root,"engine","fsearch.exe")) ? Path.Combine(Root,"engine","fsearch.exe") : Path.Combine(Root,"target","release","fsearch.exe");

    private static readonly Regex DuplicatePathLine = new Regex(@"^\s*[◉○]\s+(.+)$",RegexOptions.Compiled);

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
    public static SearchReport Run(SearchRequest request, Action<Process> processChanged, Func<bool> cancelled, Action<string> eventReceived = null)
    {
        request.Validate();
        if (!File.Exists(Binary)) throw new FileNotFoundException("The search engine is missing. Extract the complete RAFS release ZIP, keeping the engine folder beside the app. For a source checkout, follow the build guide.", Binary);
        if (cancelled()) throw new OperationCanceledException();
        string scratch = Path.Combine(AppPaths.Runs, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try {
            File.WriteAllText(Path.Combine(scratch, "fsearch.toml"), "threads = " + request.Threads + "\n", new UTF8Encoding(false));
            string args = "find " + Quote(request.Pattern) + " -p " + Quote(request.Folder) + " -d " + request.Depth + " -m " + (request.Method==5?(request.LegacyAdaptive?4:3):request.Method) + " -D" +
                (request.Content ? " -f" : "") + (request.CaseSensitive ? " -C" : "") + " --report-events" + (request.Method == 5 ? " --max-workers " + request.MaximumWorkers + " --telemetry" : "");
            if(request.DuplicateMode!=null)args="dup -p "+Quote(request.Folder)+" -d "+request.Depth+" --mode "+request.DuplicateMode+" -i "+Quote(request.Pattern);
            var info = new ProcessStartInfo(Binary, args) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = scratch,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            info.EnvironmentVariables["NO_COLOR"] = "1";
            info.EnvironmentVariables["RAFS_REPORT_WARNINGS"] = "1";
            info.EnvironmentVariables.Remove("RAYON_NUM_THREADS");
            var output = new StringBuilder(); var errors = new StringBuilder();
            var paths=new System.Collections.Generic.List<string>(); bool truncated=false,incomplete=false; double? engine=null;
            var metrics=new System.Collections.Generic.Dictionary<string,double>();string selected="";
            var report=ProcessExecution.Run(info,processChanged,cancelled,
                delegate(string line) {
                    if(output.Length+line.Length<2*1024*1024)output.AppendLine(line);else truncated=true;
                    if(request.DuplicateMode!=null){var match=DuplicatePathLine.Match(line);if(match.Success)paths.Add(match.Groups[1].Value);}
                },
                delegate(string line) {
                    const string pathPrefix="RAFS|path|utf8_hex=";
                    const string timingPrefix="RAFS|timing|engine_ms=";
                    if(line.StartsWith(pathPrefix,StringComparison.Ordinal)) {
                        string hex=line.Substring(pathPrefix.Length);if(hex.Length%2!=0)throw new FormatException("Malformed path event.");
                        var bytes=new byte[hex.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Byte.Parse(hex.Substring(i*2,2),System.Globalization.NumberStyles.HexNumber);
                        paths.Add(new UTF8Encoding(false,true).GetString(bytes));
                    } else if(line.StartsWith(timingPrefix,StringComparison.Ordinal)) {
                        double value;if(!Double.TryParse(line.Substring(timingPrefix.Length),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value)||value<0)throw new FormatException("Invalid engine timing.");engine=value;
                    } else if(line.StartsWith("RAFS|overhead|",StringComparison.Ordinal)){
                        foreach(string field in line.Split('|')){int separator=field.IndexOf('=');if(separator>0){double value;if(!Double.TryParse(field.Substring(separator+1),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value)||value<0)throw new FormatException("Invalid policy overhead.");metrics[field.Substring(0,separator)]=value;}}
                        if(eventReceived!=null)eventReceived(line);
                    } else if(line.StartsWith("RAFS|warning|",StringComparison.Ordinal)){incomplete=true;if(errors.Length<256*1024)errors.AppendLine(line.Substring(13));}
                    else if(line.StartsWith("RAFS|",StringComparison.Ordinal)) {
                        if(line.StartsWith("RAFS|decision|kind=Strategy|",StringComparison.Ordinal)){int a=line.IndexOf("|new=",StringComparison.Ordinal);int b=line.IndexOf("|reason=",StringComparison.Ordinal);if(a>=0&&b>a)selected=line.Substring(a+5,b-a-5);}
                        if(eventReceived!=null)eventReceived(line);
                    }
                    else if(errors.Length<256*1024)errors.AppendLine(line);
                },request.TimeoutSeconds);
            if(truncated)output.AppendLine("[Raw output preview limited to 2,097,152 characters. All result paths are retained in Results / CSV.]");
            report.Output=output.ToString();report.Error=(incomplete?"Search incomplete: some entries could not be read.\r\n":"")+errors.ToString();report.Paths.AddRange(paths);report.EngineMilliseconds=engine;report.Incomplete=incomplete;
            report.SelectedStrategy=selected;
            if(metrics.ContainsKey("sample_ms"))report.SamplingMilliseconds=metrics["sample_ms"];
            if(metrics.ContainsKey("decision_ms"))report.DecisionMilliseconds=metrics["decision_ms"];
            if(metrics.ContainsKey("pool_ms"))report.PoolMilliseconds=metrics["pool_ms"];
            if(metrics.ContainsKey("scheduling_ms"))report.SchedulingMilliseconds=metrics["scheduling_ms"];
            Match timing=Regex.Match(report.Output,@"\[(\d+)ms\]");
            report.EngineTime=engine.HasValue?engine.Value.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" ms":timing.Success?timing.Groups[1].Value+" ms":"--";
            return report;
        } finally {
            try { File.Delete(Path.Combine(scratch, "fsearch.toml")); Directory.Delete(scratch); } catch (IOException) { } catch(UnauthorizedAccessException){}
        }
    }
    public static void TryStop(Process process)
    {
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
    }
}
