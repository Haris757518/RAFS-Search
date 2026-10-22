using System;
using System.Diagnostics;
using System.IO;
using System.Text;

internal static class PowerShellSearchRunner
{
    private static string Literal(string value) { return "'" + value.Replace("'", "''") + "'"; }
    public static SearchReport Run(SearchRequest request, Action<Process> processChanged, Func<bool> cancelled)
    {
        request.Validate();
        if (request.Content) throw new ArgumentException("Traditional PowerShell Search supports filename patterns only. Uncheck Search file contents.");
        if (cancelled()) throw new OperationCanceledException();
        string pattern=request.Pattern.Contains("*")||request.Pattern.Contains("?")?request.Pattern:"*"+request.Pattern+"*";
        string script = "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false); " +
            "Get-ChildItem -LiteralPath " + Literal(request.Folder) + " -Recurse -File -Depth " + request.Depth + " -Filter " + Literal(pattern) +
            (request.CaseSensitive ? " | Where-Object { $_.Name -clike " + Literal(pattern) + " }" : "") +
            " | ForEach-Object { $_.FullName }";
        return RunScript(script, processChanged, cancelled,request.TimeoutSeconds);
    }
    internal static SearchReport RunScript(string script, Action<Process> processChanged, Func<bool> cancelled,int timeoutSeconds=300)
    {
        if (cancelled()) throw new OperationCanceledException();
        script = "$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; try { $rafsClock=[Diagnostics.Stopwatch]::StartNew(); & { " + script + " }; $rafsClock.Stop(); [Console]::Error.WriteLine('RAFS|timing|engine_ms=' + $rafsClock.Elapsed.TotalMilliseconds.ToString('R',[Globalization.CultureInfo]::InvariantCulture)) } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 2 }";
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        var output = new StringBuilder(); var errors = new StringBuilder();
        var paths=new System.Collections.Generic.List<string>();double? engine=null;bool truncated=false;
        var report=ProcessExecution.Run(info,processChanged,cancelled,
            delegate(string line){paths.Add(Path.GetFullPath(line));if(output.Length+line.Length<2*1024*1024)output.AppendLine(line);else truncated=true;},
            delegate(string line){const string prefix="RAFS|timing|engine_ms=";if(line.StartsWith(prefix,StringComparison.Ordinal)){double value;if(!Double.TryParse(line.Substring(prefix.Length),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)||value<0||Double.IsNaN(value)||Double.IsInfinity(value))throw new FormatException("Invalid PowerShell timing.");engine=value;}else if(errors.Length<256*1024)errors.AppendLine(line);},timeoutSeconds);
        report.Output=output.ToString()+(truncated?"\n[Raw preview truncated; all paths retained.]":"");
        report.Error=errors.ToString();report.Paths.AddRange(paths);report.EngineMilliseconds=engine;
        report.EngineTime=engine.HasValue?engine.Value.ToString("0.000")+" ms":"--";return report;
    }
}
