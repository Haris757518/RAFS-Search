# RAFS Windows desktop application

Double-click `D:\OS_Project\RAFS-Search\RAFS-Search.exe`. Keep `RAFS-Search.exe.config` beside it, and keep `target/release/fsearch.exe` and `LICENSE` in the project folder.

## Five pages

- Search: filename/glob and literal content search, case sensitivity, both methods, depth, threads, cancellation, result grid, raw content output, CSV and Show in Folder. Method 2 is sequential; depth 0 searches only the selected folder.
- Live Monitor: real shared session state, query, folder, configured method/depth/threads, stopwatch elapsed time and final matches. Active workers and workload metrics remain --. Throughput and adaptive decisions have explicit empty states. No sample values are plotted.
- Compare: independent A/B configurations, including same-method thread comparisons, optional warm-ups and 1–15 measured runs. Execution order rotates each round. Every path set is validated against the first configuration. The zero-based horizontal chart shows actual median process times. Minimum, maximum, population standard deviation, matches and relative speed are included. A mismatch suppresses the graph.
- Benchmark Lab: custom folder; selectable method 2 and method 1 with 1, 2, 4, 8 or automatic / 0 threads. All six configurations are initially selected. The first selected configuration is the correctness and relative-speed reference (method 2 when selected). Real progress, cancellation and CSV export work. Dataset generators are planned and generate nothing.
- Settings / About: saved next-launch defaults, future maximum-worker setting, optional real application diagnostic logging, light theme/system fonts, provenance, View License and Open Project Folder. Adaptive mode and runtime statistics are unavailable.

Switching pages retains inputs/results and does not start or cancel operations. Scroll the main content area to reach lower sections at smaller sizes.

## Architecture

Program starts AppShell, which owns five persistent page instances and SearchSession. The session coordinates background work, UI-thread completion, cancellation, timestamps and completed reports. Pages observe the shared state and never launch engine processes directly. ISearchBackend/BaselineCliBackend, SearchRunner and BenchmarkRunner isolate engine invocation and experiment scheduling. Reusable components provide layout, cards, charts, result grids and configuration selection.

Each invocation uses a unique `target/desktop-runs/<id>/fsearch.toml` containing its thread setting. The working directory is isolated; its config is removed after completion/cancellation. User-global fsearch configuration is not written. stdout/stderr are captured as UTF-8. Total timings include process launch and output collection; the CLI separately reports integer-millisecond engine time.

RuntimeTelemetry, AdaptiveDecision and IRuntimeTelemetrySource are integration APIs only. Nullable metrics represent unavailable values. The baseline backend declares no live telemetry/adaptive capability and emits no such events.

Settings are stored in `target/desktop-settings.xml`. Optional logging records actual application transitions in `target/desktop-diagnostics.log`. Defaults apply at next launch; logging applies immediately. Maximum Workers is stored only for future integration.

## Build and verification

```powershell
Set-Location D:\OS_Project\RAFS-Search
cargo build --release
.\desktop-app\build.ps1
.\RAFS-Search.exe

$check = Start-Process .\RAFS-Search.exe -ArgumentList '--self-test' -PassThru -Wait
$check.ExitCode
Get-Content .\target\desktop-verification\checks.txt

.\target\release\fsearch.exe find '*.rs' -p .\src -d 5 -m 1
.\target\release\fsearch.exe find '*.rs' -p .\src -d 5 -m 2
```

The script recursively compiles C# with the installed Windows compiler, embeds the DPI manifest and copies the .NET configuration beside the executable. No Cargo GUI dependency is added. The app targets .NET Framework 4.8, opts into PerMonitorV2 and uses WinForms DPI scaling. Installed Segoe UI Variable / Segoe UI is used for UI; Cascadia Mono / Consolas for metrics/logs. No fonts are bundled. Physical 125%/150% monitor switching requires manual verification. DrawToBitmap may omit native combo text and scrolled content; use the actual app for final visual review.

Real regression checks cover src/*.rs, both methods, 1/2/4/8/0 threads, depth 0, content, case sensitivity, invalid directory, no matches, quoted arguments, cancellation, configurable comparison, changed-result detection, six-way benchmarks, actual CSV data, settings and all-page navigation/resizing. Outputs are under `target/desktop-verification`.

## Limits and next step

No adaptive traversal, workload profiling, adaptive workers, CPU/I/O monitoring, dark theme, dataset generator or duplicate-search GUI is implemented. These are comparisons of baseline configurations; no adaptive speedup is claimed. Small workloads may be dominated by launch overhead and OS caches. Final results arrive only when the CLI exits. Content matches appear in raw output. Numbered CLI path parsing is retained; future integration should use machine-readable events.

Next task: Phase 1 Rust instrumentation emitting real files/directories scanned, depth, queue and throughput events through an opt-in structured stream while retaining the existing CLI contract. No instrumentation or adaptive policies have been implemented during this phase.

RAFS's desktop application is developed by Haris K. The original fsearch engine is by Hadi Cahyadi / cumulus13. Its source attribution and MIT notice are retained. Git history was not rewritten during this refactor.