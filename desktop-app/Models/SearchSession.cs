using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

internal enum SessionState { Idle, Running, Cancelling, Completed, Failed, Cancelled }
internal sealed class SearchSession : IDisposable
{
    private readonly ISearchBackend backend;
    private readonly BackgroundWorker worker = new BackgroundWorker();
    private readonly Stopwatch clock = new Stopwatch();
    private readonly object gate = new object();
    private readonly SynchronizationContext context;
    private Process process;
    private volatile bool cancellation, disposed;
    public event EventHandler Changed;
    public SessionState State { get; private set; }
    public string Operation { get; private set; }
    public string Message { get; private set; }
    public SearchRequest Request { get; private set; }
    public SearchReport Report { get; private set; }
    public SearchReport LastSearchReport { get; private set; }
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public RuntimeTelemetry Telemetry { get; private set; }
    public readonly List<AdaptiveDecision> Decisions = new List<AdaptiveDecision>();
    public readonly List<BenchmarkResult> History = new List<BenchmarkResult>();
    public int SearchRevision { get; private set; }
    public BenchmarkResult Comparison { get; private set; }
    public BenchmarkResult LabResult { get; private set; }
    public RunProgress Progress { get; private set; }
    public int Revision { get; private set; }
    public bool IsBusy { get { return State == SessionState.Running || State == SessionState.Cancelling; } }
    public string StatusText { get { return State == SessionState.Running ? Operation == "Search" ? "Searching" : "Benchmarking" : State.ToString(); } }
    public Action<string> Log;
    public TimeSpan Elapsed { get { return clock.Elapsed; } }
    public BackendCapabilities Capabilities { get { return backend.Capabilities; } }

    public SearchSession(ISearchBackend backend)
    {
        this.backend = backend;
        context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        State = SessionState.Idle; Message = "Ready to search"; Operation = "Search";
        var source = backend as IRuntimeTelemetrySource;
        if (source != null && Capabilities.LiveWorkloadTelemetry) {
            source.TelemetryReceived += ReceiveTelemetry;
            source.DecisionReceived += ReceiveDecision;
        }
        worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
            var plan = e.Argument as BenchmarkPlan;
            e.Result = plan == null ? (object)backend.Run((SearchRequest)e.Argument, SetProcess, IsCancelled) :
                new BenchmarkRunner(backend).Run(plan, SetProcess, IsCancelled, UpdateProgress);
        };
        worker.RunWorkerCompleted += Completed;
    }
    public void StartSearch(SearchRequest request)
    {
        if (IsBusy || worker.IsBusy) throw new InvalidOperationException("A search or benchmark is already running.");
        request = request.Copy(); request.Validate();
        LastSearchReport = null; SearchRevision++;
        Begin(request, "Search"); worker.RunWorkerAsync(request);
    }
    public void StartBenchmark(BenchmarkPlan plan)
    {
        // Snapshot all options; UI edits never change a running workload.
        var snapshot = new BenchmarkPlan { Request = plan.Request.Copy(), Runs = plan.Runs, Warmups = plan.Warmups, Lab = plan.Lab };
        foreach (var config in plan.Configurations) snapshot.Configurations.Add(new RunConfiguration { Method = config.Method, Threads = config.Threads });
        snapshot.Request.Validate();
        Begin(snapshot.Request, snapshot.Lab ? "Benchmark Lab" : "Compare");
        if (snapshot.Lab) LabResult = null; else Comparison = null;
        worker.RunWorkerAsync(snapshot);
    }
    private void Begin(SearchRequest request, string operation)
    {
        if (IsBusy || worker.IsBusy) throw new InvalidOperationException("A search or benchmark is already running. Cancel it or wait for completion.");
        cancellation = false; State = SessionState.Running; Operation = operation;
        Request = request; Report = null; Progress = null; Message = operation + " running...";
        StartTime = DateTime.Now; EndTime = null; Telemetry = null; Decisions.Clear();
        clock.Restart(); Revision++; Notify();
    }
    private bool IsCancelled() { return cancellation || disposed; }
    private void SetProcess(Process current)
    {
        lock (gate) { process = current; if (cancellation || disposed) SearchRunner.TryStop(process); }
    }
    private void UpdateProgress(RunProgress current)
    {
        context.Post(delegate(object ignored) { if (!disposed && IsBusy) { Progress = current; Notify(); } }, null);
    }
    public void Cancel()
    {
        if (!IsBusy || cancellation) return;
        cancellation = true;
        State = SessionState.Cancelling;
        lock (gate) { SearchRunner.TryStop(process); }
        Message = "Cancelling..."; Notify();
    }
    private void Completed(object sender, RunWorkerCompletedEventArgs e)
    {
        context.Post(delegate { Finish(e); }, null);
    }
    private void Finish(RunWorkerCompletedEventArgs e)
    {
        if (disposed) return;
        clock.Stop();
        EndTime = DateTime.Now;
        if (cancellation || e.Error is OperationCanceledException) { State = SessionState.Cancelled; Message = "Search cancelled"; }
        else if (e.Error != null) { State = SessionState.Failed; Message = e.Error.Message; }
        else {
            var benchmark = e.Result as BenchmarkResult;
            if (benchmark != null) { Report = benchmark.Matches; History.Add(benchmark); if (benchmark.Plan.Lab) LabResult = benchmark; else Comparison = benchmark; }
            else { Report = (SearchReport)e.Result; LastSearchReport = Report; SearchRevision++; }
            if (Report.Succeeded) { State = SessionState.Completed; Message = Report.Paths.Count == 0 ? "No matches found" : "Completed"; }
            else { State = SessionState.Failed; Message = String.IsNullOrWhiteSpace(Report.Error) ? "The search engine returned exit code " + Report.ExitCode + "." : Report.Error.Trim(); }
        }
        Revision++; Notify();
    }
    private void ReceiveTelemetry(RuntimeTelemetry snapshot) { context.Post(delegate { if (!disposed && IsBusy) { Telemetry = snapshot; Notify(); } }, null); }
    private void ReceiveDecision(AdaptiveDecision decision) { context.Post(delegate { if (!disposed && IsBusy) { Decisions.Add(decision); Notify(); } }, null); }
    private void Notify() { var handler = Changed; if (!disposed && handler != null) handler(this, EventArgs.Empty); if (!disposed && Log != null) Log(StatusText + " | " + Operation + " | " + Message); }
    public void Dispose() {
        Cancel(); disposed = true; worker.RunWorkerCompleted -= Completed; Changed = null;
        var source = backend as IRuntimeTelemetrySource;
        if (source != null) { source.TelemetryReceived -= ReceiveTelemetry; source.DecisionReceived -= ReceiveDecision; }
    }
}
