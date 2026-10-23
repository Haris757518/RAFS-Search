using System;
using System.Diagnostics;
using System.Threading;

internal static class ProcessExecution
{
    public static SearchReport Run(ProcessStartInfo info,Action<Process> processChanged,Func<bool> cancelled,
        Action<string> stdout,Action<string> stderr,int timeoutSeconds)
    {
        if(cancelled())throw new OperationCanceledException();
        Exception captureError=null;object gate=new object();
        var clock=Stopwatch.StartNew();
        double? cpu=null;long? peak=null;double sampleCost=0;
        using(var process=new Process{StartInfo=info}) {
            process.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;try{stdout(e.Data);}catch(Exception error){lock(gate){if(captureError==null)captureError=error;}}};
            process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;try{stderr(e.Data);}catch(Exception error){lock(gate){if(captureError==null)captureError=error;}}};
            try {
                process.Start();processChanged(process);
                Action sample=delegate{var sampling=Stopwatch.StartNew();try{process.Refresh();cpu=process.TotalProcessorTime.TotalMilliseconds;long workingSet=process.PeakWorkingSet64;peak=Math.Max(peak.GetValueOrDefault(),workingSet);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}finally{sampleCost+=sampling.Elapsed.TotalMilliseconds;}};
                sample();
                process.BeginOutputReadLine();process.BeginErrorReadLine();
                while(!process.WaitForExit(10)) {
                    sample();
                    if(cancelled())throw new OperationCanceledException();
                    if(clock.Elapsed.TotalSeconds>=timeoutSeconds)throw new TimeoutException("Search exceeded "+timeoutSeconds+" seconds. Narrow the folder or increase the timeout in Settings.");
                    lock(gate){if(captureError!=null)throw new InvalidOperationException("Could not read the search engine output.",captureError);}
                }
                process.WaitForExit();clock.Stop();
                try{cpu=process.TotalProcessorTime.TotalMilliseconds;}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
                if(cancelled())throw new OperationCanceledException();
                if(captureError!=null)throw new InvalidOperationException("Could not read the search engine output.",captureError);
                return new SearchReport{ExitCode=process.ExitCode,TotalMilliseconds=clock.Elapsed.TotalMilliseconds,CpuMilliseconds=cpu,SampledPeakWorkingSetBytes=peak,ResourceSamplingMilliseconds=sampleCost};
            } finally {
                SearchRunner.TryStop(process);
                try {if(!process.HasExited)process.WaitForExit(3000);}catch(InvalidOperationException){}
                processChanged(null);
            }
        }
    }
}
