using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class RunConfiguration
{
    public int Method, Threads;
    public string Name { get { return Method == 5 ? "RAFS Adaptive / " + (Threads==0?"settings cap":Threads.ToString()+" max workers") : Method == 4 ? "Windows Search Index (Explorer backend)" : Method == 3 ? "Traditional PowerShell Search" : Method == 2 ? "Original fsearch Method 2 / sequential" : "Original fsearch Method 1 / " + (Threads == 0 ? "automatic" : Threads.ToString()) + " threads"; } }
}
internal sealed class BenchmarkPlan
{
    public SearchRequest Request;
    public int Runs = 5;
    public int Warmups = 1;
    public readonly List<RunConfiguration> Configurations = new List<RunConfiguration>();
    public bool Lab;
}
internal sealed class BenchmarkMeasurement
{
    public RunConfiguration Configuration;
    public readonly List<double> Samples = new List<double>();
    public readonly List<double?> EngineSamples = new List<double?>();
    public double? EngineMedian {get{var values=EngineSamples.Where(v=>v.HasValue).Select(v=>v.Value).OrderBy(v=>v).ToArray();if(values.Length!=Samples.Count||values.Length==0)return null;int n=values.Length;return n%2==1?values[n/2]:(values[n/2-1]+values[n/2])/2;}}
    public double Median {
        get { var sorted = Samples.OrderBy(x => x).ToArray(); int n = sorted.Length; return n == 0 ? 0 : n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2; }
    }
    public double Minimum { get { return Samples.Count == 0 ? 0 : Samples.Min(); } }
    public double Maximum { get { return Samples.Count == 0 ? 0 : Samples.Max(); } }
    public double StdDev { get { if (Samples.Count == 0) return 0; double mean = Samples.Average(); return Math.Sqrt(Samples.Sum(v => (v - mean) * (v - mean)) / Samples.Count); } }
}
internal sealed class BenchmarkResult
{
    public BenchmarkPlan Plan;
    public SearchReport Matches;
    public readonly List<BenchmarkMeasurement> Measurements = new List<BenchmarkMeasurement>();
    public DateTime CompletedAt;
    public string OperatingSystem = Environment.OSVersion.ToString();
    public int LogicalProcessors = Environment.ProcessorCount;
}
internal sealed class RunProgress
{
    public string Phase;
    public SearchRequest Request;
    public int Completed, Total;
    public int Test, Tests;
}
