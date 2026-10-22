using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

internal static class SoakVerification
{
    public static int Run()
    {
        string artifacts=Path.Combine(SearchRunner.Root,"verification");Directory.CreateDirectory(artifacts);
        string fixture=Path.Combine(AppPaths.StateRoot,"soak");Directory.CreateDirectory(fixture);
        for(int i=0;i<100;i++){string dir=Path.Combine(fixture,"d"+(i%20));Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"item"+i+".txt"),"soak needle\nother row");}
        var backend=new BaselineCliBackend();var clock=Stopwatch.StartNew();int count=0,baseline=0,maxHandles=0;var notes=new List<string>();
        // Run for at least one minute, rather than claiming long-term field coverage.
        do {
            foreach(int method in new[]{2,1,5}){
                var report=backend.Run(new SearchRequest{Folder=fixture,Pattern=count%2==0?"*.txt":"soak needle",Content=count%2!=0,Method=method,MaximumWorkers=1+count%8,Threads=1+count%4},p=>{},()=>false);
                if(!report.Succeeded||report.Paths.Count!=100||report.Paths.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=100)throw new Exception("Repeated search lost or duplicated results: "+count);
                count++;
            }
            if(count>=15){GC.Collect();GC.WaitForPendingFinalizers();using(var process=Process.GetCurrentProcess()){int handles=process.HandleCount;if(baseline==0)baseline=handles;maxHandles=Math.Max(maxHandles,handles);if(handles>baseline+80)throw new Exception("Process handles grew unexpectedly during repeated searches.");}}
        }while(clock.Elapsed.TotalSeconds<60||count<100);
        if(Directory.Exists(AppPaths.Runs)&&Directory.GetDirectories(AppPaths.Runs).Length!=0)throw new Exception("Repeated searches leaked scratch directories.");
        notes.Add("PASS "+count+" repeated filename/content searches in "+clock.Elapsed.TotalSeconds.ToString("F1")+" seconds, identical 100-file coverage across engines and worker counts.");
        notes.Add("PASS scratch cleanup and process handle bound; baseline="+baseline+", peak="+maxHandles);
        notes.Add("This is a one-minute local repeatability check, not sustained field or multi-machine testing.");File.WriteAllLines(Path.Combine(artifacts,"soak.txt"),notes);return 0;
    }
}
