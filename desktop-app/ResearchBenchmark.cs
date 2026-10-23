using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Globalization;

internal static class ResearchBenchmark
{
    private sealed class Dataset {public string Name,Root,Pattern;public bool Content;public string Category;}
    private sealed class Configuration {public string Name;public int Method,Threads=1;public bool Legacy=false;}
    private static string Csv(object value){string text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";return "\""+text.Replace("\"","\"\"")+"\"";}
    private static string Row(params object[] fields){return String.Join(",",fields.Select(Csv));}
    private static string Digest(IEnumerable<string> paths){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(String.Join("\n",paths.Select(p=>Path.GetFullPath(p).ToUpperInvariant()).OrderBy(p=>p,StringComparer.Ordinal))))).Replace("-","").ToLowerInvariant();}
    public static int Run()
    {
        string output=Environment.GetEnvironmentVariable("RAFS_RESEARCH_ROOT");string project=Environment.GetEnvironmentVariable("RAFS_PROJECT_ROOT");string list=Environment.GetEnvironmentVariable("RAFS_WORKLOAD_LIST");
        if(String.IsNullOrWhiteSpace(output)||String.IsNullOrWhiteSpace(project)||String.IsNullOrWhiteSpace(list))throw new ArgumentException("Set RAFS_RESEARCH_ROOT, RAFS_PROJECT_ROOT and RAFS_WORKLOAD_LIST.");
        Directory.CreateDirectory(output);bool smoke=Environment.GetEnvironmentVariable("RAFS_RESEARCH_SMOKE")=="1";int warmups=smoke?0:3,runs=smoke?1:10;
        var datasets=new List<Dataset>();
        foreach(string root in File.ReadAllLines(list).Where(Directory.Exists)){
            int count=Directory.GetFiles(root,"*.txt",SearchOption.AllDirectories).Length;if(count!=20000)throw new Exception("Expected a frozen 20,000-file synthetic workload: "+root);
            datasets.Add(new Dataset{Name=Path.GetFileName(root).Split('-')[0]=="Small"?"Flat-20000":Path.GetFileName(root).Split('-')[0]+"-20000",Root=root,Pattern="*099*.txt",Category="development-synthetic"});
        }
        if(datasets.Count!=4)throw new Exception("Four controlled topology workloads are required.");
        string holdout=Path.Combine(output,"fixtures","flat-12000");Directory.CreateDirectory(holdout);
        for(int i=0;i<12000;i++){string path=Path.Combine(holdout,"holdout_"+i.ToString("D6")+".txt");if(!File.Exists(path))File.WriteAllText(path,"Independent synthetic holdout material, no private content.\n");}
        datasets.Add(new Dataset{Name="Flat-holdout-12000",Root=holdout,Pattern="*099*.txt",Category="holdout-synthetic"});
        string content=Path.Combine(output,"fixtures","content-128");Directory.CreateDirectory(content);
        var payload=new StringBuilder();for(int line=0;line<256;line++)payload.AppendLine(new string('a',240));payload.AppendLine("research needle marker");
        for(int i=0;i<128;i++)File.WriteAllText(Path.Combine(content,"content_"+i.ToString("D3")+".txt"),payload.ToString());
        datasets.Add(new Dataset{Name="Flat-content-128",Root=content,Pattern="research needle marker",Content=true,Category="holdout-synthetic-content"});
        datasets.Add(new Dataset{Name="Real-Rust-source",Root=Path.Combine(project,"src"),Pattern="*.rs",Category="real-repository"});
        datasets.Add(new Dataset{Name="Real-desktop-source",Root=Path.Combine(project,"desktop-app"),Pattern="*.cs",Category="real-repository"});
        string academic=@"D:\RAFS_Demo\01_Academic_Search";if(Directory.Exists(academic))datasets.Add(new Dataset{Name="Real-academic-copies",Root=academic,Pattern="*.pdf",Category="real-academic-copies"});
        var configurations=new List<Configuration>{new Configuration{Name="Sequential DFS",Method=2}};
        foreach(int n in new[]{1,2,4,8})configurations.Add(new Configuration{Name="Fixed / "+n+" threads",Method=1,Threads=n});
        configurations.Add(new Configuration{Name="October-22 adaptive",Method=5,Threads=8,Legacy=true});
        configurations.Add(new Configuration{Name="Hybrid adaptive",Method=5,Threads=8});configurations.Add(new Configuration{Name="PowerShell",Method=3});
        var metadata=new List<string>{"Protocol: "+warmups+" warmups + "+runs+" measured rounds per configuration; rotated order; warm filesystem cache.","OS: "+Environment.OSVersion,"Logical CPUs: "+Environment.ProcessorCount,"Framework: "+Environment.Version,"Source revision: "+Environment.GetEnvironmentVariable("RAFS_SOURCE_SHA"),"Engine SHA256: "+FileHash(SearchRunner.Binary),"Resource sampling: OS cumulative CPU time; OS peak working set polled every 10 ms (sampled lower bound). Sampling cost retained in totals and recorded separately.","PowerShell excluded from content search; index queries recorded separately, not ranked against live traversal."};
        File.WriteAllLines(Path.Combine(output,"metadata.txt"),metadata);
        var backend=new BaselineCliBackend();var decisions=new List<AdaptiveDecision>();var telemetry=new List<RuntimeTelemetry>();object gate=new object();backend.DecisionReceived+=d=>{lock(gate)decisions.Add(d);};backend.TelemetryReceived+=d=>{lock(gate)telemetry.Add(d);};
        using(var raw=new StreamWriter(Path.Combine(output,"samples.csv"),false,new UTF8Encoding(false)))
        using(var trace=new StreamWriter(Path.Combine(output,"decisions.csv"),false,new UTF8Encoding(false)))
        using(var runtime=new StreamWriter(Path.Combine(output,"telemetry.csv"),false,new UTF8Encoding(false)))
        using(var manifest=new StreamWriter(Path.Combine(output,"datasets.csv"),false,new UTF8Encoding(false))){
            raw.WriteLine("Dataset,Category,Query,Content,Configuration,Round,Warmup,TotalMs,EngineMs,CpuMs,SampledPeakWorkingSetBytes,ResourceSamplingMs,SamplingMs,DecisionMs,PoolStartupMs,SchedulingMs,SelectedStrategy,Matches,Correct,PathSetSHA256");
            trace.WriteLine("Dataset,Configuration,Round,Warmup,Timestamp,Event,Old,New,Reason,MeasuredValue");
            runtime.WriteLine("Dataset,Configuration,Round,Warmup,EngineElapsedMs,Files,Directories,Pending,Capacity,MaxDepth,ActiveWorkers,ThroughputFilesPerSecond,CpuPercent,WorkingSetBytes,Traversal,SelectedStrategy,OsSampleTimestamp");
            manifest.WriteLine("Dataset,Category,FileCount,ExpectedMatches,PathSetSHA256");
            foreach(var data in datasets){
                var all=Directory.GetFiles(data.Root,data.Content?"*.txt":data.Pattern,SearchOption.AllDirectories);
                var expected=new HashSet<string>(all.Where(p=>!data.Content||File.ReadLines(p).Any(line=>line.IndexOf(data.Pattern,StringComparison.OrdinalIgnoreCase)>=0)).Select(Path.GetFullPath),StringComparer.OrdinalIgnoreCase);
                string digest=Digest(expected);manifest.WriteLine(Row(data.Name,data.Category,Directory.GetFiles(data.Root,"*",SearchOption.AllDirectories).Length,expected.Count,digest));manifest.Flush();
                var configs=configurations.Where(c=>!data.Content||c.Method!=3).ToArray();
                for(int round=-warmups;round<runs;round++)for(int index=0;index<configs.Length;index++){
                    var config=configs[(index+round+warmups)%configs.Length];lock(gate){decisions.Clear();telemetry.Clear();}
                    var request=new SearchRequest{Folder=data.Root,Pattern=data.Pattern,Content=data.Content,Depth=100,Method=config.Method,Threads=config.Threads,MaximumWorkers=8,LegacyAdaptive=config.Legacy};
                    var report=backend.Run(request,p=>{},()=>false);
                    if(!report.Succeeded||!expected.SetEquals(report.Paths))throw new Exception("Correctness failure: "+data.Name+" / "+config.Name+" expected="+expected.Count+" actual="+report.Paths.Count+" "+report.Error);
                    if(!report.CpuMilliseconds.HasValue||!report.SampledPeakWorkingSetBytes.HasValue||report.SampledPeakWorkingSetBytes.Value<=0)throw new Exception("Resource measurement unavailable: "+data.Name+" / "+config.Name);
                    if(config.Method==5&&!config.Legacy&&(!report.SamplingMilliseconds.HasValue||String.IsNullOrEmpty(report.SelectedStrategy)))throw new Exception("Hybrid explanation/overhead event missing.");
                    raw.WriteLine(Row(data.Name,data.Category,data.Pattern,data.Content,config.Name,round<0?round+warmups+1:round+1,round<0,report.TotalMilliseconds,report.EngineMilliseconds,report.CpuMilliseconds,report.SampledPeakWorkingSetBytes,report.ResourceSamplingMilliseconds,report.SamplingMilliseconds,report.DecisionMilliseconds,report.PoolMilliseconds,report.SchedulingMilliseconds,report.SelectedStrategy,report.Paths.Count,true,digest));raw.Flush();
                    lock(gate)foreach(var d in decisions)trace.WriteLine(Row(data.Name,config.Name,round<0?round+warmups+1:round+1,round<0,d.Timestamp.ToString("O"),d.EventType,d.OldValue,d.NewValue,d.Reason,d.MeasuredValue));trace.Flush();
                    lock(gate)foreach(var t in telemetry)runtime.WriteLine(Row(data.Name,config.Name,round<0?round+warmups+1:round+1,round<0,t.EngineElapsedMilliseconds,t.FilesScanned,t.DirectoriesScanned,t.PendingDirectories,t.QueueCapacity,t.MaxDepth,t.ActiveWorkers,t.Throughput,t.CpuUsage,t.MemoryUsage,t.CurrentTraversal,t.SelectedStrategy,t.OsSampleTimestamp.HasValue?t.OsSampleTimestamp.Value.ToString("O"):""));runtime.Flush();
                    File.WriteAllText(Path.Combine(output,"progress.txt"),data.Name+" | "+config.Name+" | "+(round<0?"warmup "+(round+warmups+1):"measured "+(round+1))+" | "+report.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture)+" ms | "+report.SelectedStrategy);
                }
            }
        }
        var real=datasets.FirstOrDefault(d=>d.Category=="real-academic-copies")??datasets.First(d=>d.Category=="real-repository");
        var indexed=backend.Run(new SearchRequest{Folder=real.Root,Pattern=real.Pattern,Depth=100,Method=4},p=>{},()=>false);
        File.WriteAllText(Path.Combine(output,"index-separate.txt"),"Separate Windows Search Index observation: dataset="+real.Name+", succeeded="+indexed.Succeeded+", indexed matches="+indexed.Paths.Count+", live matches="+Directory.GetFiles(real.Root,real.Pattern,SearchOption.AllDirectories).Length+". Prebuilt index and live traversal are not ranked together. "+indexed.Error);
        File.WriteAllText(Path.Combine(output,"COMPLETE.txt"),"All requested rounds passed independent path-set checks. See samples.csv, decisions.csv, datasets.csv and metadata.txt.");return 0;
    }
    private static string FileHash(string path){using(var file=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
}
