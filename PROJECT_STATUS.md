# RAFS implementation status — 22 October 2026

The current release includes a per-user Windows installer with upgrade/uninstall checks, signing hooks, bundled synthetic demo data, keyboard/accessibility checks, simulated scaling tests and optimized adaptive directory identity handling. See RELEASE_VALIDATION_2026-10-22.md for measured results and external release gates. The user approved a public draft PR; its remote CI status is tracked on GitHub. Trusted signing and physical device testing are not marked complete.

RAFS now includes runtime-adaptive directory scheduling alongside the original fsearch baselines. This document supersedes the earlier Review I implementation descriptions; those documents describe the earlier baseline-only milestone.

## Available features

- Search: original Method 1 and Method 2, RAFS Adaptive, Traditional PowerShell, and Windows Search Index. Filename patterns, plain substring matching in the fsearch engines, text content matching, depth limits, cancellation, results and CSV export.
- Adaptive Rust engine: observed directory frontier and branching select DFS/BFS queue order and active batch concurrency. One fixed Rayon pool enforces the physical thread cap; batch chunks enforce active concurrency. Overlapping roots preserve shallower depth coverage. Original Method 1 and Method 2 traversal algorithms are preserved, with explicit I/O warnings added.
- Live Monitor: actual files and directories scanned, frontier length and allocation capacity, observed depth and branching, running directory tasks, measured throughput, engine process CPU and working-set memory, process I/O byte rate, and policy decisions. No fabricated metrics. Baseline engines do not emit adaptive workload counters.
- Compare: all five strategies selectable independently, including same-method worker comparisons. Warm-ups, rotating execution order, repeated measurements, median/min/max/population standard deviation, normalized Windows path equality, cancellation and CSV export.
- Benchmark Lab: Method 1 / 1, 2, 4, 8, Auto; Method 2; RAFS Adaptive; optional PowerShell and index configurations. Synthetic Deep, Wide, Mixed and Small Files generators produce 100–50,000 files in unique local folders. Generation runs in the background with cancellation. Partial datasets are retained and marked cancelled.
- Duplicates: original content/hash, filename and size grouping engine, depth and filename filters, raw group summary, duplicate paths and CSV export. No delete function.
- Settings: startup method, depth, manual threads, adaptive worker cap, repetition count, remembered folder/query, diagnostic logging, and light/dark theme. Theme and startup defaults take effect at next launch.

## Adaptive policy and measurement limits

The engine observes the queue each batch. Branching below 2 favors DFS; a broader frontier favors BFS, except when pending work exceeds 64 times the worker cap, when DFS reduces frontier pressure. Fewer than four pending directories use one worker; otherwise concurrency is capped by the configured limit and approximately one worker per four queued directories. This is an explicit heuristic, not a trained model or a universal optimum. It does not promise to outperform the original methods.

Telemetry is an opt-in structured `RAFS|...` stderr stream; regular CLI stdout remains readable. It is throttled to about ten periodic snapshots per second plus completion and decision events. Throughput is cumulative scanned files / elapsed engine time. Scan counts include nonmatching files and the workload manifest. Pending/capacity count queued directory entries; directory scheduling tasks differ from CPU cores and from file matches. Workload labels are heuristic classifications from observed frontier size and depth. CPU is normalized over logical CPUs, memory is the process working set, and process I/O includes cached reads and writes: it is not physical disk throughput. Fields remain unavailable if the process has exited before an OS sample.

Benchmark timings include process launch, output handling, sorting and, for Adaptive, telemetry overhead. PowerShell startup can dominate small workloads. Windows Search queries a prebuilt index through a PowerShell host; it does not measure the Explorer UI or a live crawl. Its matching index may omit unindexed/stale files; a mismatch prevents publishing a performance graph. No indexing settings are changed automatically. Different native wildcard/exclusion/hidden-file behavior can also produce a legitimate baseline mismatch; it is reported rather than concealed.

Content search reads ordinary text and code, skipping binaries. No PDF/Office text extraction is claimed. A policy change is an observed scheduling decision, not proof of a speedup. Content benchmarks compare matched file paths; they do not compare every matched line. Duplicate name/size groups are candidates; only content/hash mode checks content identity.

## Production hardening

See PRODUCTION_GUIDE.md for portable packaging and remaining release limits. Settings and generated workloads now use LocalAppData, with a test-state override. Preferences are validated, DTDs rejected, writes serialized across app instances and replacement atomic. Search children have cancellation and configurable timeouts. Typed UTF-8 hex path events prevent content from being mistaken for results. Unreadable files produce an incomplete-search error and block comparison. Content matching streams bounded lines; result collections still scale with actual matches. CSV exports escape formula prefixes. Engine and total timings are separate and their scopes disclosed. Windows builds enable .NET long-path handling.

The release verification mode runs from the portable package without repository source files. It checks quoted/Unicode paths, depth, protocol-like content, exclusive-lock failures, settings races, malformed telemetry, process cleanup, benchmark accounting and mismatch suppression. Windows CI now includes the development branch and packaged verification; remote CI has not yet run.

## Build and verification

```powershell
Set-Location D:\OS_Project\RAFS-Search
cargo build --release --offline
.\desktop-app\build.ps1
cargo test --offline
cargo clippy --offline --all-targets -- -D warnings
$check = Start-Process .\RAFS-Search.exe -ArgumentList '--self-test' -PassThru -Wait
$check.ExitCode
```

For the larger controlled experiment, add `--large-workloads` to the self-test arguments. It creates four 20,000-file workloads, runs all live strategies against ground truth, verifies content results, checks telemetry counts against filesystem enumeration, runs the eight-configuration benchmark matrix, and renders native application pages. It does not touch `D:\VIT` or replace existing datasets.

Results, actual timing samples, workload paths and UI captures are under `target\desktop-verification\features`. `target\desktop-verification\checks.txt` records the full desktop suite. `src\adaptive.rs` has regression tests for topologies, depth, content, invalid roots and cancellation. Original Rust unit and documentation tests remain.

`--feature-test` runs the focused new-feature suite with 100-file datasets and writes to `target\feature-verification`. The raw-output preview is bounded to 2,097,152 characters while all result paths are retained; a regression check exercises a 100,000-line matching file. This prevents content-output volume from consuming hundreds of megabytes in the app.

Physical multi-monitor/DPI switching requires a manual Windows display check. Indexed comparison requires a folder actually present in the Windows index. The application does not promise perfection across inaccessible directories, arbitrary network shares, concurrent filesystem modifications or unavailable Windows Search services.

## Ownership and safety

The MIT license, upstream author attribution, original history and `master` baseline are preserved. Development remains on `rafs-development`. Existing local modifications are retained; nothing is committed, pushed or published automatically. Academic originals in `D:\VIT` remain read-only. New performance workloads contain generated public-domain synthetic text, no private documents or downloaded content.
