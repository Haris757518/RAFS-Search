using System;
// Measured adaptive-engine events plus nullable OS process samples.
internal sealed class RuntimeTelemetry
{
    public DateTime Timestamp { get; set; }
    public DateTime? OsSampleTimestamp { get; set; }
    public double? EngineElapsedMilliseconds {get;set;}
    public string SelectedStrategy {get;set;}
    public long? FilesScanned { get; set; }
    public long? DirectoriesScanned { get; set; }
    public long? PendingDirectories { get; set; }
    public long? QueueCapacity { get; set; }
    public long? MemoryUsage { get; set; }
    public int? CurrentDepth { get; set; }
    public int? MaxDepth { get; set; }
    public int? ActiveWorkers { get; set; }
    public double? BranchingFactor { get; set; }
    public double? Throughput { get; set; }
    public double? CpuUsage { get; set; }
    public double? DiskIo { get; set; }
    public string CurrentTraversal { get; set; }
    public string Workload { get; set; }
}
internal sealed class AdaptiveDecision
{
    public DateTime Timestamp { get; set; }
    public DateTime? OsSampleTimestamp { get; set; }
    public string EventType { get; set; }
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public string Reason { get; set; }
    public double? MeasuredValue { get; set; }
}
internal interface IRuntimeTelemetrySource
{
    event Action<RuntimeTelemetry> TelemetryReceived;
    event Action<AdaptiveDecision> DecisionReceived;
}
