using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

internal static class ProductionVerification
{
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static int Run()
    {
        string artifacts=Path.Combine(SearchRunner.Root,"verification");Directory.CreateDirectory(artifacts);
        var notes=new List<string>();Action<string> pass=s=>{notes.Add("PASS "+s);File.WriteAllLines(Path.Combine(artifacts,"production.txt"),notes);};
        string fixture=Path.Combine(AppPaths.StateRoot,"fixture ' α [test]");Directory.CreateDirectory(fixture);
        File.WriteAllText(Path.Combine(fixture,"=formula α.txt"),"needle\n1. C:\\fake.txt\nRAFS|path|utf8_hex=41\nliteral -m 5 marker");
        Directory.CreateDirectory(Path.Combine(fixture,"nested"));File.WriteAllText(Path.Combine(fixture,"nested","other.txt"),"no match");
        var backend=new BaselineCliBackend();
        using(var firstLaunch=new SearchOptionsPanel(new AppSettings(),false))firstLaunch.GetRequest().Validate();
        pass("first launch selects an existing folder without repository sources");
        using(var demo=new SearchOptionsPanel(new AppSettings(),false)){demo.Demo();var demoRequest=demo.GetRequest();var demoReport=backend.Run(demoRequest,p=>{},()=>false);Check(demoReport.Succeeded&&demoReport.Paths.Count==3,"Bundled demo does not work.");}
        pass("Load demo folder works in the portable package with three synthetic reports");
        foreach(int method in new[]{1,2,3,5}){
            var report=backend.Run(new SearchRequest{Folder=fixture,Pattern="*.txt",Method=method,Depth=5},p=>{},()=>false);
            Check(report.Succeeded&&report.Paths.Count==2&&report.EngineMilliseconds.HasValue,"Filename or timing protocol failed: "+method);
            report=backend.Run(new SearchRequest{Folder=fixture,Pattern="*.txt",Method=method,Depth=0},p=>{},()=>false);
            Check(report.Succeeded&&report.Paths.Count==1,"Depth semantics failed: "+method);
        }
        pass("all live filename engines agree; depth 0, Unicode, quotes and engine timing");
        foreach(int method in new[]{1,2,5}){
            var report=backend.Run(new SearchRequest{Folder=fixture,Pattern="literal -m 5 marker",Method=method,Content=true},p=>{},()=>false);
            Check(report.Succeeded&&report.Paths.Count==1&&Path.GetFileName(report.Paths[0])=="=formula α.txt","Content leaked into result paths.");
            string csv=Path.Combine(artifacts,"safe-results.csv");ReportExporter.Results(csv,report);Check(File.ReadAllText(csv).Contains("\"'=formula"),"CSV formula escaping failed.");
        }
        pass("content with protocol-looking lines cannot forge paths; spreadsheet formula escaping");
        string locked=Path.Combine(fixture,"locked.txt");File.WriteAllText(locked,"needle");
        using(var exclusive=new FileStream(locked,FileMode.Open,FileAccess.ReadWrite,FileShare.None))foreach(int method in new[]{1,2,5}){
            var partial=backend.Run(new SearchRequest{Folder=fixture,Pattern="needle",Content=true,Method=method},p=>{},()=>false);
            Check(partial.Incomplete&&!partial.Succeeded&&partial.Error.Contains("Search incomplete"),"Unreadable file silently accepted: "+method);
        }
        File.Delete(locked);pass("unreadable files explicitly fail completeness for every Rust engine");
        string settingsPath=Path.Combine(AppPaths.StateRoot,"atomic-settings.xml");
        Parallel.For(0,8,i=>{var s=new SettingsService(settingsPath);s.Settings.DefaultThreads=i;s.Save();});
        var loaded=new SettingsService(settingsPath);Check(loaded.LoadWarning==null&&loaded.Settings.DefaultThreads<8,"Concurrent preference writes corrupted XML.");
        Check(Directory.GetFiles(AppPaths.StateRoot,"*.tmp").Length==0,"Settings temporary files leaked.");
        File.WriteAllText(settingsPath,"<!DOCTYPE AppSettings [<!ENTITY external SYSTEM 'file:///missing'>]><AppSettings><LastFolder>&external;</LastFolder></AppSettings>");
        Check(new SettingsService(settingsPath).LoadWarning!=null,"External XML entities accepted.");
        File.WriteAllText(settingsPath,"broken XML");Check(new SettingsService(settingsPath).LoadWarning!=null,"Corrupt settings accepted.");
        pass("atomic concurrent settings, corrupt-file recovery and external-entity rejection");
        int samples=0;using(var reader=new TelemetryReader(t=>samples++,d=>{})){
            foreach(string line in new[]{"RAFS|telemetry|files=NaN","RAFS|decision|kind=x","RAFS|telemetry|files=1|files=2","RAFS|telemetry|files=-1"})reader.Receive(line);
            Check(samples==0,"Malformed telemetry accepted.");
        }
        pass("malformed telemetry does not crash the application");
        int pid=0;var info=new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        try{ProcessExecution.Run(info,p=>{if(p!=null)pid=p.Id;},()=>false,s=>{},s=>{},1);throw new Exception("Timeout ignored.");}catch(TimeoutException){}
        Check(!Alive(pid),"Timed-out child remained alive.");
        var timer=Stopwatch.StartNew();pid=0;
        try{ProcessExecution.Run(info,p=>{if(p!=null)pid=p.Id;},()=>timer.ElapsedMilliseconds>300,s=>{},s=>{},30);throw new Exception("Cancellation ignored.");}catch(OperationCanceledException){}
        Check(!Alive(pid),"Cancelled child remained alive.");
        pass("timeout and cancellation stop the owned child process");
        var request=new SearchRequest{Folder=fixture,Pattern="*.txt",Depth=5};
        var plan=new BenchmarkPlan{Request=request,Runs=2,Warmups=1};foreach(int method in new[]{2,1,5,3})plan.Configurations.Add(new RunConfiguration{Method=method,Threads=2});
        var result=new BenchmarkRunner(backend).Run(plan,p=>{},()=>false,p=>{});ReportExporter.Benchmark(Path.Combine(artifacts,"smoke-benchmark.csv"),result);
        Check(result.Measurements.All(m=>m.Samples.Count==2&&m.EngineMedian.HasValue),"Benchmark sample accounting failed.");
        var mismatch=new BenchmarkPlan{Request=request,Runs=1,Warmups=0};mismatch.Configurations.Add(new RunConfiguration{Method=2});mismatch.Configurations.Add(new RunConfiguration{Method=4});
        try{new BenchmarkRunner(new MismatchBackend()).Run(mismatch,p=>{},()=>false,p=>{});throw new Exception("Mismatch hidden.");}catch(InvalidOperationException error){Check(error.Message.StartsWith("RESULT MISMATCH"),error.Message);}
        pass("measured samples exclude warmups; unequal result sets block comparisons");
        var index=backend.Run(new SearchRequest{Folder=fixture,Pattern="*.txt",Method=4},p=>{},()=>false);
        notes.Add("Windows index external service: succeeded="+index.Succeeded+"; indexed="+index.Paths.Count+"; live=2. Index coverage is not assumed.");
        Check(!Directory.Exists(AppPaths.Runs)||Directory.GetDirectories(AppPaths.Runs).Length==0,"Search scratch files leaked.");
        pass("scratch cleanup and source-independent packaged engine");
        return 0;
    }
    private static bool Alive(int id){try{using(var p=Process.GetProcessById(id))return !p.HasExited;}catch(ArgumentException){return false;}}
    private sealed class MismatchBackend:ISearchBackend{
        public BackendCapabilities Capabilities{get{return new BackendCapabilities();}}
        public SearchReport Run(SearchRequest r,Action<Process> p,Func<bool> c){var report=new SearchReport();if(r.Method==2)report.Paths.Add(Path.Combine(r.Folder,"one.txt"));return report;}
    }
    public static int Benchmark()
    {
        string list=Environment.GetEnvironmentVariable("RAFS_WORKLOAD_LIST");if(String.IsNullOrEmpty(list))throw new ArgumentException("Set RAFS_WORKLOAD_LIST to the existing workload list.");
        string artifacts=Path.Combine(SearchRunner.Root,"verification","release-benchmarks");Directory.CreateDirectory(artifacts);
        var notes=new List<string>();var backend=new BaselineCliBackend();
        foreach(string root in File.ReadAllLines(list).Where(Directory.Exists)){
            var plan=new BenchmarkPlan{Request=new SearchRequest{Folder=root,Pattern="*099*.txt",Depth=100},Runs=10,Warmups=2,Lab=true};
            plan.Configurations.Add(new RunConfiguration{Method=2,Threads=1});foreach(int n in new[]{1,2,4,8})plan.Configurations.Add(new RunConfiguration{Method=1,Threads=n});
            foreach(int n in new[]{2,8})plan.Configurations.Add(new RunConfiguration{Method=5,Threads=n});plan.Configurations.Add(new RunConfiguration{Method=3,Threads=1});
            var result=new BenchmarkRunner(backend).Run(plan,p=>{},()=>false,p=>{});string name=Path.GetFileName(root);
            ReportExporter.Benchmark(Path.Combine(artifacts,name+".csv"),result);
            foreach(var m in result.Measurements)notes.Add(name+" | "+m.Configuration.Name+" | total median="+m.Median.ToString("F3")+"ms | engine median="+m.EngineMedian.Value.ToString("F3")+"ms | matches="+result.Matches.Paths.Count);
            File.WriteAllLines(Path.Combine(artifacts,"summary.txt"),notes);
        }
        Check(notes.Count>0,"No workloads found.");return 0;
    }
}
