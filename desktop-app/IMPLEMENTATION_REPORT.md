# RAFS desktop refactor report — 2026-10-08

Project: D:\OS_Project\RAFS-Search
Branch: rafs-development
Original repository: https://github.com/cumulus13/fsearch
Derivative application: Haris K
Original Rust engine: Hadi Cahyadi / cumulus13

## Architecture

AppShell owns five persistent pages and one SearchSession. The session stores the request, stopwatch/start/end time, state, cancellation, process reference, results, errors and completed comparisons/benchmarks. It runs work on a BackgroundWorker and marshals completion/progress to the GUI synchronization context. Its displayed states include Idle, Searching, Benchmarking, Cancelling, Completed, Failed and Cancelled. Navigation observes this session and never duplicates searches.

SearchRunner owns CLI argument quoting, isolated per-run TOML configuration, UTF-8 stdout/stderr capture, process exit code, cancellation and total elapsed time. BenchmarkRunner schedules warm-ups and measured iterations in rotating order and validates every result set. Pages never launch fsearch directly. ISearchBackend allows a future instrumented backend to replace BaselineCliBackend. RuntimeTelemetry and AdaptiveDecision plus IRuntimeTelemetrySource prepare event integration without emitting invented events.

## Working pages

Search supports both baseline methods, filename/glob patterns, literal content search, case sensitivity, depth including zero, fixed thread configuration or automatic zero, browse, cancellation, virtual result grid (filename/full path/type), raw output, CSV, and Explorer reveal/double-click. Available timing cards distinguish CLI engine time from total process time. Error and no-result states are handled. The engine still returns complete results at the end; no partial result stream is claimed.

Live Monitor shows actual session status, query/folder, elapsed GUI stopwatch, configured method/depth/threads, and final match count. Active worker count remains --. Files/directories scanned, throughput, pending directories, tree workload, depth, branching factor, queue capacity, CPU, memory and disk I/O remain unavailable/planned. Throughput and decision components have explicit empty-state messages and contain no sample numbers. Future telemetry is nullable and opt-in by backend capability.

Compare accepts independent configurations A and B (method 1 or method 2), including two method-1 thread settings. It performs configured warm-ups and measured runs, alternates A/B order through rotation, compares every path set, and computes median/minimum/maximum/population standard deviation. It shows matches/correctness and relative speed against A. The horizontal graph begins at zero. A real changed-folder regression confirms a mismatch fails the comparison instead of drawing a misleading speedup. Timings include CLI process launch and output collection.

Benchmark Lab supports Custom Folder and a selectable matrix of method 2 sequential and method 1 with 1, 2, 4, 8 or automatic/0 threads. It performs real CLI warm-ups and measured runs, rotates order, reports actual configuration/run progress, supports cancellation, and validates against the first selected configuration (method 2 when included). CSV contains strategy/thread count, actual samples, median/minimum/maximum/stddev, matches, correctness and relative speed. No throughput is invented. Dataset generators are planned and do not run.

Settings stores next-launch manual search defaults, a future maximum-worker limit and optional application diagnostic logging. Logging records real session changes. About identifies Haris K's derivative application and the fsearch baseline, with license/folder actions. Maximum Workers has no effect on current manual threads. Light theme/system fonts are implemented; dark mode is not.

## DPI and fonts

.NET Framework 4.8 target, PerMonitorV2 manifest and WinForms configuration, DPI AutoScaleMode/96-DPI design dimensions, Segoe UI Variable if installed with Segoe UI fallback, Cascadia Mono if installed with Consolas fallback for metrics/logs, no bundled fonts. Docked percentage columns, table/flow layouts and vertical scrolling keep controls/results accessible when resized. Header height is DPI-scaled. The minimum outer window size is 1200 by 750; verification used 1200 by 750 and 1440 by 900 client areas. Physical 125%/150% scaling and moving across monitors have not been verified. DrawToBitmap has native combo-text limitations; direct app inspection confirmed the combo selection is present.

## Baseline preservation

Cargo.toml, LICENSE and the complete Rust source tree match upstream commit e74d9d5b72662a23f7ae8a0dde527be3d2517365. Attribution changes present before this task were restored to upstream values. Original package/crate names and search algorithms are retained. No Git history rewrite, new adaptive algorithm, workload generator, instrumentation or feature implementation in Rust was performed. The existing local branch remains rafs-development. Changes are local and have not been committed or pushed during this refactor.

## Build and real tests

- Rust release build: PASS.
- Windows desktop build: PASS (warnings treated as errors).
- Rust unit tests: 14 PASS; documentation tests: 3 PASS.
- Clippy: PASS. Existing duplicate-bin manifest warning and sandbox hard-link cache fallback warnings remain. Initial documentation tests were blocked by sandbox temporary-executable permissions; the approved rerun passed.
- Both original CLI src/*.rs commands: 10 identical files.
- Desktop checks cover both methods at 1/2/4/8/0 threads, recursion, depth zero, content, case sensitivity, no results, invalid directory validation, spaces/trailing separators/quoted arguments, cancellation after actual CLI launch, shared-session navigation, retained results/inputs, same-method comparison, actual filesystem mismatch, all six benchmark configurations, benchmark cancellation, CSV samples/statistics, saved preferences, all-page resizing/scrolling and no fabricated telemetry.
- Full test results: target/desktop-verification/checks.txt. Final GUI verification: exit code 0, 91 passing checks (including three Windows shell launch checks).
- Windows shell-action checks confirm requests to open the license in Notepad and reveal/open project folders are accepted. Automated checks do not confirm Explorer's final selected item or manually inspect Notepad rendering. Direct UI automation encountered inconsistent focus/geometry and was stopped after recovery failed; no full click-through verification is claimed.

## Known limitations

No Adaptive controller, DFS/BFS switching, workload profiling, adaptive workers, real scanned counters, throughput, queue pressure, CPU/memory/disk monitoring, dark theme, dataset generation or duplicate-search GUI. Method 2 remains sequential. Configured thread counts are not active-worker telemetry. Small workloads are dominated by launch/cache effects; no guarantee of a speedup and no Windows Explorer comparison are made. Final results arrive only after process exit. CLI engine time may round down to zero milliseconds. Numbered path-output parsing and upstream matching/permission behavior remain. Manual multi-monitor/DPI tests and a final physical button/Explorer inspection remain advisable.

## Next backend task — recommended, not implemented

Phase 1 instrumentation: emit real files/directories scanned, depth, pending work/queue information and throughput through a versioned, opt-in machine-readable stream. Wire it through IRuntimeTelemetrySource and validate counters/cancellation/correctness before implementing adaptive traversal or worker policies.

## New source/configuration/documentation files
- desktop-app/app.config
- desktop-app/app.manifest
- desktop-app/AppShell.cs
- desktop-app/Components/BenchmarkView.cs
- desktop-app/Components/ConfigurationPicker.cs
- desktop-app/Components/MetricCard.cs
- desktop-app/Components/ResultGrid.cs
- desktop-app/Components/SearchOptionsPanel.cs
- desktop-app/Components/SectionPanel.cs
- desktop-app/Components/Sidebar.cs
- desktop-app/Components/StatusBadge.cs
- desktop-app/Components/TelemetryViews.cs
- desktop-app/Components/Theme.cs
- desktop-app/IMPLEMENTATION_REPORT.md
- desktop-app/Models/BenchmarkResult.cs
- desktop-app/Models/RuntimeTelemetry.cs
- desktop-app/Models/SearchReport.cs
- desktop-app/Models/SearchRequest.cs
- desktop-app/Models/SearchSession.cs
- desktop-app/Pages/BenchmarkPage.cs
- desktop-app/Pages/ComparePage.cs
- desktop-app/Pages/LiveMonitorPage.cs
- desktop-app/Pages/SearchPage.cs
- desktop-app/Pages/SettingsPage.cs
- desktop-app/Program.cs
- desktop-app/Services/BenchmarkRunner.cs
- desktop-app/Services/DesktopActions.cs
- desktop-app/Services/ReportExporter.cs
- desktop-app/Services/SearchRunner.cs
- desktop-app/Services/SettingsService.cs
- desktop-app/Verification.cs

## Existing files modified
- .gitignore
- AUTHORS.md
- BASELINE_NOTES.md
- Cargo.toml
- LICENSE
- UPSTREAM_README.md
- desktop-app/README.md
- desktop-app/build.ps1
- src/binary.rs
- src/cli.rs
- src/colors.rs
- src/config.rs
- src/error.rs
- src/main.rs
- src/output.rs
- src/searcher.rs

## Existing file removed
- desktop-app/RafsApp.cs — replaced by the separated models/services/components/pages.

## Generated or refreshed outputs
- RAFS-Search.exe
- RAFS-Search.exe.config (new required configuration sidecar)
- target/release/fsearch.exe and fs.exe
- target/desktop-verification/: checks.txt, results.csv, benchmark.csv, settings-test.xml, harmless fixture files and top/bottom page render captures.
