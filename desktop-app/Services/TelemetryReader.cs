using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
internal sealed class TelemetryReader : IDisposable {
    private readonly Action<RuntimeTelemetry> telemetry;
    private readonly Action<AdaptiveDecision> decision;
    private readonly object gate=new object();
    private Process process; private DateTime lastTime; private TimeSpan lastCpu; private ulong lastIo;
    private long peakPending; private long? sampledMemory; private double? sampledCpu,sampledIo;
    private bool observedBroadFrontier;
    private string selectedStrategy="";
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes; }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetProcessIoCounters(IntPtr handle,out IoCounters counters);
    public TelemetryReader(Action<RuntimeTelemetry> telemetry,Action<AdaptiveDecision> decision){this.telemetry=telemetry;this.decision=decision;}
    public void SetProcess(Process value){lock(gate){process=value;lastTime=DateTime.UtcNow;lastCpu=TimeSpan.Zero;lastIo=0;peakPending=0;observedBroadFrontier=false;selectedStrategy="";sampledMemory=null;sampledCpu=null;sampledIo=null;}}
    public void Receive(string line){try{ReceiveChecked(line);}catch(FormatException){}catch(OverflowException){}catch(KeyNotFoundException){} }
    private void ReceiveChecked(string line){
        var fields=new Dictionary<string,string>();var parts=line.Split('|');foreach(string p in parts){int i=p.IndexOf('=');if(i>0){string key=p.Substring(0,i);if(fields.ContainsKey(key))throw new FormatException("Duplicate telemetry field.");fields[key]=p.Substring(i+1);}}
        if(parts.Length<2)return;
        if(parts[1]=="decision"){if(fields["kind"]=="Strategy")selectedStrategy=fields["new"];if(fields["kind"]=="Traversal"&&fields["new"]=="BFS")observedBroadFrontier=true;decision(new AdaptiveDecision{Timestamp=DateTime.Now,EventType=fields["kind"],OldValue=fields["old"],NewValue=fields["new"],Reason=fields["reason"]});return;}
        if(parts[1]=="overhead"){
            double total=0;foreach(string key in new[]{"sample_ms","decision_ms","pool_ms","scheduling_ms"}){double value;if(!Double.TryParse(fields[key],NumberStyles.Float,CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value)||value<0)throw new FormatException("Invalid overhead value.");total+=value;}
            decision(new AdaptiveDecision{Timestamp=DateTime.Now,EventType="Policy overhead",OldValue="",NewValue=total.ToString("F3",CultureInfo.InvariantCulture)+" ms",Reason="Measured sampling="+fields["sample_ms"]+", decisions="+fields["decision_ms"]+", pool startup="+fields["pool_ms"]+", scheduling="+fields["scheduling_ms"]+" ms; included in elapsed time",MeasuredValue=total});return;
        }
        if(parts[1]!="telemetry")return;
        Func<string,double> number=k=>{double value;if(!Double.TryParse(fields[k],NumberStyles.Float,CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value)||value<0)throw new FormatException("Invalid telemetry value.");return value;};
        Func<string,long> counter=k=>{long value;if(!Int64.TryParse(fields[k],NumberStyles.None,CultureInfo.InvariantCulture,out value)||value<0)throw new FormatException("Invalid telemetry counter.");return value;};
        foreach(string key in new[]{"files","dirs","pending","capacity","depth","max_depth","workers"})counter(key);
        if(counter("depth")>Int32.MaxValue||counter("max_depth")>Int32.MaxValue||counter("workers")>256)throw new FormatException("Telemetry range exceeded.");
        var data=new RuntimeTelemetry{Timestamp=DateTime.Now,FilesScanned=(long)number("files"),DirectoriesScanned=(long)number("dirs"),PendingDirectories=(long)number("pending"),QueueCapacity=(long)number("capacity"),CurrentDepth=(int)number("depth"),MaxDepth=(int)number("max_depth"),ActiveWorkers=(int)number("workers"),BranchingFactor=number("branching"),CurrentTraversal=fields["traversal"],Throughput=number("elapsed_ms")>0?number("files")*1000/number("elapsed_ms"):0};
        lock(gate){if(process!=null&&(!sampledMemory.HasValue||(DateTime.UtcNow-lastTime).TotalMilliseconds>=100)){try{process.Refresh();data.MemoryUsage=process.WorkingSet64;DateTime now=DateTime.UtcNow;double seconds=(now-lastTime).TotalSeconds;var cpu=process.TotalProcessorTime;if(seconds>0)data.CpuUsage=Math.Max(0,Math.Min(100,(cpu-lastCpu).TotalSeconds/seconds/Environment.ProcessorCount*100));IoCounters io;if(GetProcessIoCounters(process.Handle,out io)){ulong total=io.ReadBytes+io.WriteBytes;if(seconds>0)data.DiskIo=(total-lastIo)/seconds;lastIo=total;}lastCpu=cpu;lastTime=now;sampledMemory=data.MemoryUsage;sampledCpu=data.CpuUsage;sampledIo=data.DiskIo;}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}}
        data.MemoryUsage=sampledMemory;data.CpuUsage=sampledCpu;data.DiskIo=sampledIo;data.OsSampleTimestamp=sampledMemory.HasValue?(DateTime?)lastTime:null;
        data.EngineElapsedMilliseconds=number("elapsed_ms");data.SelectedStrategy=selectedStrategy;
        if(selectedStrategy!=""&&selectedStrategy!="Adaptive DFS/BFS"){data.PendingDirectories=null;data.QueueCapacity=null;data.BranchingFactor=null;}
        peakPending=Math.Max(peakPending,data.PendingDirectories.GetValueOrDefault());
        bool wide=observedBroadFrontier||peakPending>=64;
        data.Workload=wide && data.MaxDepth<=3 ? "Wide / shallow" : data.MaxDepth>=7 && !wide && peakPending<8 ? "Deep / narrow" : data.MaxDepth==0 && peakPending<2 ? "Flat files" : "Mixed frontier";
        telemetry(data);
    }
    public void Dispose(){SetProcess(null);}
}
