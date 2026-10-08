using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class RunConfiguration
{
    public int Method, Threads;
    public string Name { get { return Method == 2 ? "Recursive DFS / sequential" : "WalkDir + Rayon / " + (Threads == 0 ? "automatic" : Threads.ToString()) + " threads"; } }
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
}
internal sealed class RunProgress
{
    public string Phase;
    public SearchRequest Request;
    public int Completed, Total;
    public int Test, Tests;
}
