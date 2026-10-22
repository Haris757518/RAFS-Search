# Release validation — 22 October 2026

## Completed release work

- Windows per-user installer, Start menu entry, upgrade/reinstall and registered uninstall. Installer test verified that files it does not own survive both upgrade and uninstall. Preferences/workloads are outside the installed program directory.
- Portable ZIP now includes four public synthetic demo files; Load demo returns three matching reports. No academic documents are distributed.
- Adaptive directory identity resolution moved from every directory to the roots, retaining derived child identities, skipped descendant links, overlap handling and depth semantics. One bounded Rayon pool remains.
- Accessible names for inputs, navigation and results. Alt+1..6 page navigation, Ctrl+F query, Ctrl+L location, Ctrl+Enter search and Escape cancel.
- Layout/accessibility suite passed simulated 100%, 125% and 150% scaling on all six pages. Captures were inspected. The machine's native DPI was 120. This is not a physical mixed-monitor or screen-reader acceptance test.
- Repeated-search test passed 1,119 filename/content searches in 60.1 seconds with identical 100-file coverage, no scratch-directory leaks and process handles within the configured bound (baseline 294, peak 351). This is a short local repeatability check.
- 21 Rust unit tests, 3 documentation tests, formatting and strict Clippy passed. Documentation compilation required sandbox escalation for temporary executables; it passed after rerunning with permission.
- Signing support accepts an existing code-signing certificate and timestamp service, signs payload before packaging, signs Setup and refreshes checksums. No suitable publisher certificate was available, so supplied artifacts remain unsigned. No fake publisher identity was generated.
- CI now supports manual dispatch, Windows Server 2022/2025 package/UI/install checks and the existing Linux/macOS/Windows Rust checks. Remote execution requires publishing the current source changes; local evidence is not a remote CI result.

## Current performance experiment

Four explicitly validated synthetic datasets, each containing 20,000 text files. Query `*099*.txt`, filename-only, depth 100, case insensitive. Eight configurations, two warmups and ten measured rounds per topology. All 384 invocations agreed on 140 result paths; CSV contains 320 measured samples. Execution order rotates. This is a warm-cache, single-machine experiment.

Total-time medians in milliseconds:

| Workload | Method 2 | Method 1 / 2 threads | Method 1 / 4 threads | Adaptive / cap 8 | PowerShell |
|---|---:|---:|---:|---:|---:|
| Deep | 80.974 | 75.207 | 76.959 | 70.600 | 488.256 |
| Wide | 146.903 | 136.886 | 138.278 | 82.745 | 702.462 |
| Mixed | 93.679 | 87.596 | 89.909 | 55.775 | 546.135 |
| Flat small files | 83.922 | 72.192 | 74.226 | 78.400 | 477.254 |

Adaptive beat Method 1 / 4 threads on deep, wide and mixed here, but lost on flat files. Different thread caps also change performance: adaptive cap 2 had a 69.169 ms deep median. These results do not establish a universal winner or isolate hardware differences from the earlier 12 October experiment. The earlier implementation paid for directory canonicalization on every scheduled directory; this build derives descendant identities from canonical roots. Read raw per-run CSV and variation before making claims.

Total time includes launch/output handling. Rust engine timing excludes result serialization; PowerShell pipeline timing includes output, so engine timings have different scopes. Windows index coverage depends on external indexing state, and mismatches block graphs. Actual Explorer window timing remains outside the user-selected automated index baseline.

## External completion gates

1. A real publisher code-signing certificate/private-key service and its timestamp endpoint are required for trusted signatures.
2. Remote CI must verify the published draft pull request. The user approved source publication on 22 October; its live GitHub status is separate from this local validation report.
3. Physical mixed-monitor DPI switching, screen-reader user acceptance, clean Windows 10/11 clients, a supplied network share and longer field testing need those environments. Hosted Windows Server CI is additional coverage, not a substitute.

These gates are explicitly open. The release can be installed and used locally; it is not represented as fully signed, remotely validated public production distribution.
