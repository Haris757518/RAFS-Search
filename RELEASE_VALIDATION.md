# Historical local release validation — 12 October 2026

The current release report is [RELEASE_VALIDATION_2026-10-22.md](RELEASE_VALIDATION_2026-10-22.md). The results below describe the earlier build and are retained for comparison.

Tested on the current Windows machine, branch `rafs-development`. No commits, pushes, signed release or remote CI run are implied.

## Automated evidence

- Rust: 21 unit tests and 3 documentation tests passed. Formatting and Clippy with `-D warnings` passed. Cargo reports the existing two binary targets sharing `src/main.rs`; the local drive also falls back from hard links to copied incremental files.
- Packaged production checks: source-independent engine loading, Unicode/quoted paths, depth 0, typed paths, protocol-looking content, internal timings, CSV formula protection, exclusively locked unreadable files, concurrent atomic settings, XML/DTD rejection, malformed telemetry, timeout/cancel child cleanup, warmup accounting, result-mismatch rejection and scratch cleanup.
- Native UI suite: adaptive counters and decisions, controlled topology workloads, academic result equality, content matching, duplicates, bounded preview, six-page navigation and dark/light rendering. See the final package's feature verification log and screenshots for completion status.

## Repeatable large-workload experiment

Four existing synthetic workloads: Deep, Wide, Mixed and Small Files; 20,000 text files each (80,000 total, plus manifests). Query `*099*.txt`, maximum depth 100, filename-only, case insensitive. Eight configurations, two warmups and ten measured runs per configuration per topology: 384 child searches, 320 measured samples. Every invocation returned the same 140 normalized paths. The execution order rotates each round. This experiment finished before the UI suite ran, so UI rendering did not compete with the measured searches.

Total-time medians in milliseconds:

| Workload | Method 2 | Method 1 / 2 | Method 1 / 4 | Adaptive cap 8 | PowerShell |
|---|---:|---:|---:|---:|---:|
| Deep | 103.934 | 95.999 | 100.889 | 93.980 | 512.364 |
| Wide | 153.682 | 142.579 | 145.972 | 187.623 | 710.176 |
| Mixed | 107.616 | 100.356 | 98.899 | 100.060 | 565.678 |
| Small Files | 81.873 | 75.592 | 76.051 | 73.263 | 491.649 |

Adaptive was slower on the wide workload and slightly slower than Method 1 / 4 on mixed. These are actual measurements, not target values. The bounded-pool scheduling and directory identity checks prioritize resource limits and correctness; this heuristic is not universally optimal. Earlier three-run results describe an older adaptive implementation and must not be substituted for this build.

PowerShell engine medians were 125.244–323.725 ms while totals were 491.649–710.176 ms: process startup and hosting contribute materially. Rust engine timing ends before result serialization; PowerShell pipeline timing includes output, so internal times are not identical measurement scopes. Raw CSV retains all eight configurations, per-run totals, per-run engine times, median/min/max/population standard deviation, OS and processor count.

The Windows index returned 0 of 2 files in the packaged fixture; earlier academic checks returned 0 indexed versus 21 live PDFs. Index coverage is external state. An unequal indexed result set blocks comparison; no speedup is asserted and index settings are untouched. Actual Explorer UI timing is not measured.

## Remaining release requirements

Unsigned portable build; no publisher signing certificate supplied. Physical monitor/DPI changes, screen-reader usability, clean-machine installs, network shares and sustained load testing require broader validation. Match collections grow with actual results. PDF/Office/OCR extraction and authenticated automatic updating are outside this implementation. See PRODUCTION_GUIDE.md for operational limits.
