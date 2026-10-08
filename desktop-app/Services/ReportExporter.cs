using System;
using System.IO;
using System.Linq;
using System.Text;

internal static class ReportExporter
{
    private static string Csv(string text) { return "\"" + text.Replace("\"", "\"\"") + "\""; }
    public static string FileType(string path) { string extension = Path.GetExtension(path); return String.IsNullOrEmpty(extension) ? "File" : extension.TrimStart('.').ToUpperInvariant() + " file"; }
    public static void Results(string file, SearchReport report) {
        File.WriteAllLines(file, new[] { "Filename,Full Path,Type" }.Concat(report.Paths.Select(p => Csv(Path.GetFileName(p)) + "," + Csv(p) + "," + Csv(FileType(p)))), new UTF8Encoding(true));
    }
    public static void Benchmark(string file, BenchmarkResult result) {
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("Folder,Pattern,Depth,Content,CaseSensitive,Strategy,Threads,Run,TotalMilliseconds,MedianMilliseconds,MinimumMilliseconds,MaximumMilliseconds,StdDevMilliseconds,Matches,Correct,RelativeSpeed");
        foreach (var measurement in result.Measurements) for (int i = 0; i < measurement.Samples.Count; i++)
            lines.Add(Csv(result.Plan.Request.Folder) + "," + Csv(result.Plan.Request.Pattern) + "," + result.Plan.Request.Depth + "," +
                result.Plan.Request.Content + "," + result.Plan.Request.CaseSensitive + "," + Csv(measurement.Configuration.Name) + "," + (measurement.Configuration.Method == 2 ? "1" : measurement.Configuration.Threads.ToString()) + "," + (i + 1) + "," +
                measurement.Samples[i].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                Number(measurement.Median) + "," + Number(measurement.Minimum) + "," + Number(measurement.Maximum) + "," + Number(measurement.StdDev) + "," + result.Matches.Paths.Count + ",True," + Number(measurement.Median > 0 ? result.Measurements[0].Median / measurement.Median : 0));
        File.WriteAllLines(file, lines, new UTF8Encoding(true));
    }
    private static string Number(double value) { return value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture); }
}
