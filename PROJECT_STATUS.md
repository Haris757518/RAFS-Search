# RAFS implementation status — 23 October 2026

RAFS now includes bounded hybrid strategy selection and throughput-informed concurrency control. See the [research report](research/REPORT.md), [algorithm design](research/DESIGN.md) and [frozen protocol](research/PROTOCOL.md). The October 22 measurements describe the previous scheduler, not this hybrid.

## Implemented

- Existing filename/text-content search, depth constraints, cancellation, comparison, generated workloads, read-only duplicates, CSV exports, themes and settings remain available.
- RAFS Adaptive selects sequential DFS, fixed WalkDir + Rayon or dynamic DFS/BFS after observing at most eight directories and 256 entries with a soft two-millisecond budget. Dense flat work can use existing file parallelism.
- Dynamic scheduling uses frontier size, branching, files per directory, throughput and scheduling cost. A bounded pool enforces concurrency. Dwell periods, three-batch regression confirmation and a retained phase ceiling limit oscillation.
- Existing Live Monitor records selected strategy, reasons, worker/traversal changes, actual scan counters and measured policy overhead. Sequential/fixed queue metrics are unavailable because those engines do not expose a frontier. OS samples may be reused between updates and carry timestamps in research exports.
- The old adaptive search body remains available as a research baseline. A separate harness compares DFS, fixed 1/2/4/8, legacy adaptive, hybrid and PowerShell with three warmups and ten measured repetitions per configuration/dataset. All runs require independently checked equal path sets. Windows Search Index is separate.
- Per-user installer, portable ZIP, atomic settings, process cleanup/timeouts, bounded preview, incomplete-scan detection and CSV formula escaping remain. Desktop version is 1.1.0; upstream Rust package version remains 1.1.2.
- Original author Hadi Cahyadi's copyright and attribution are restored in LICENSE, AUTHORS.md, source headers and upstream documentation. Haris K is credited for RAFS extensions. Git history is retained.

## Limits

No adaptive method is guaranteed to be fastest for every query. Profiling, telemetry, pool startup and output handling cost time. The report compares against the fastest measured fixed configuration and discloses losses. Warm-cache results on one Windows machine do not establish cold-cache, network-share or multi-machine performance.

The profile budget is checked between OS calls, so a blocking call can exceed 2 ms. The engine commits to one strategy per invocation and adjusts scheduling within dynamic mode; it does not migrate running scans among engines. Comparable density is a feedback heuristic, not causal proof of concurrency benefit.

Content search reads text/code and skips binaries; PDF/Office text extraction is not implemented. Research checks content-result paths; Rust regressions also check context. Result collections scale with matches. Concurrent filesystem changes can invalidate static ground truth; inaccessible paths block valid comparison charts.

PowerShell/index support filenames. The index can omit unindexed/stale files and is not live File Explorer traversal. Unequal index coverage is never ranked with live scans. Index settings are unchanged.

CPU accounting is quantized. Sampled peak memory can miss brief peaks. Active-task events do not establish continuous utilization. Physical monitor/DPI transitions, screen readers and broad client-machine field testing remain manual validation areas.

The release remains unsigned as requested. Signing hooks support a future real certificate; no trusted-signing claim is made.

## Reproduce

Use cargo test/fmt/strict Clippy, desktop-app/package.ps1, verify-release.ps1, build-installer.ps1 and verify-installer.ps1. Follow [research/README.md](research/README.md) for the full experiment. Original academic documents in D:\VIT are untouched; local copies are read-only inputs and excluded from Git and release packages.