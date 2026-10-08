using System;
using System.Collections.Generic;

internal sealed class SearchReport
{
    public string Output = "", Error = "", EngineTime = "--";
    public int ExitCode;
    public double TotalMilliseconds;
    public readonly List<string> Paths = new List<string>();
    public bool Succeeded { get { return ExitCode == 0 || (ExitCode == 1 && Paths.Count == 0 && String.IsNullOrWhiteSpace(Error) && Output.Contains("No results found.")); } }
}

internal sealed class BackendCapabilities
{
    public bool Adaptive { get; private set; }
    public bool LiveWorkloadTelemetry { get; private set; }
    public BackendCapabilities(bool adaptive = false, bool telemetry = false) { Adaptive = adaptive; LiveWorkloadTelemetry = telemetry; }
    public string Description { get { return "Baseline CLI: filename/content search, fixed method and fixed thread settings."; } }
}
