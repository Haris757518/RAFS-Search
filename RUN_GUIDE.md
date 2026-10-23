# RAFS run guide

Current release: 22 October 2026. Use the latest `-Setup.exe` or portable ZIP under `target\releases`. Read RELEASE_VALIDATION_2026-10-22.md for current results; instructions and timings below were recorded for the 12 October build. The wide-folder regression was addressed by deriving child directory identities from resolved roots; current timings still show Method 1 winning on flat files.

Launch `D:\OS_Project\RAFS-Search\RAFS-Search.exe` after closing any older copy. The adaptive engine, live metrics, workload generators, duplicate discovery and dark theme are implemented. See PROJECT_STATUS.md for behavior and measurement limits.

The hardened portable build is under `target\releases`; see PRODUCTION_GUIDE.md for installation/state paths and RELEASE_VALIDATION.md for current ten-run results. Earlier measurements below belong to the earlier implementation. Adaptive search is not always faster: the current wide workload favors Method 1.

## Academic document search

Folder: `D:\RAFS_Demo\01_Academic_Search`; Pattern: `*.pdf`; Depth: **12**; content search OFF. Expect **21 PDFs** from the original demo copies. Methods 1, 2, PowerShell and Adaptive are checked against the same filesystem result set. Secondary filename query: `*Module*` (9 results). Academic originals in D:\VIT remain unchanged.

## Recommended measured comparison

Folder:

```text
D:\OS_Project\RAFS-Search\target\benchmark-data\Wide-20261012-205102-53d0de
```

Pattern: `*099*.txt`; Depth: **100**; content search OFF; case sensitive OFF.

Expect **140 matches**. Open Compare and set A to **Method 1 / 4 threads**, B to **RAFS Adaptive / 8 workers cap**. Use **1 warm-up and 3 measured runs**. Run Comparison and scroll to the measurement table. Use the same folder and query for PowerShell vs Adaptive or Method 2 vs Adaptive.

On Benchmark Lab, select Method 1 counts **1, 2, 4, 8, Auto**, Method 2, Adaptive and optionally PowerShell. Set Settings / Adaptive worker cap to **8**. Leave Windows Search Index unchecked unless the chosen folder is already completely indexed. Use the same depth, warm-up and measured-run settings. All eight live configurations passed identical result checks in every generated topology.

The generated workloads contain synthetic text. They are designed to compare traversal, not presented as real academic documents. Every workload has 20,000 TXT files and one manifest; the query selects 140 files while still traversing the whole tree.

## Recorded medians

| Workload | Method 2 | Method 1 / 4 | Adaptive / cap 8 | PowerShell |
|---|---:|---:|---:|---:|
| Deep | 97.239 ms | 96.153 ms | 87.990 ms | 533.385 ms |
| Wide | 166.192 ms | 145.722 ms | 98.944 ms | 674.697 ms |
| Mixed | 108.107 ms | 99.671 ms | 88.949 ms | 565.857 ms |
| Small Files | 92.410 ms | 93.156 ms | 89.174 ms | 512.222 ms |

These are real end-to-end process measurements from one local experiment, 1 warm-up + 3 measured runs, rotating order. They include launch, output collection, sorting and adaptive telemetry overhead; OS caches/background activity affect them. They are observations, not guaranteed speedups. In the flat Small Files workload, Method 1 with **2 threads (85.784 ms)** beat Adaptive **(89.174 ms)**; do not claim Adaptive is universally fastest. Raw samples, minimum, maximum, standard deviation, correctness and relative speed are saved in `target\desktop-verification\features\*-benchmark.csv`.

## Live monitoring

On Search select **RAFS Adaptive**, use the Mixed workload below, pattern `*.txt`, depth **100**, then click Start Search and open Live Monitor. Expect **20,000 matches**, **20,001 scanned files** (including the nonmatching manifest), **651 scanned directories**, maximum observed directory depth **8** and real scheduling decisions. Workers become **0** at completion; that is correct. Scroll to the counters and throughput/decision panels.

```text
D:\OS_Project\RAFS-Search\target\benchmark-data\Mixed-20261012-210537-5fa1ab
```

For a quick content demonstration use `D:\OS_Project\RAFS-Search\src`, pattern `use`, depth 12, content search ON and Method 1/2/Adaptive. Text/code only; PDF and Office extraction is not implemented. Large content scans are slower than filename scans; avoid scanning all 80,000 generated files live during a short presentation.

## All workload paths

```text
D:\OS_Project\RAFS-Search\target\benchmark-data\Deep-20261012-204933-4bd02f
D:\OS_Project\RAFS-Search\target\benchmark-data\Wide-20261012-205102-53d0de
D:\OS_Project\RAFS-Search\target\benchmark-data\Mixed-20261012-210537-5fa1ab
D:\OS_Project\RAFS-Search\target\benchmark-data\Small-Files-20261012-210704-8baee8
```

Benchmark Lab / Generate workload can create Deep, Wide, Mixed or Small Files topologies with 100–50,000 synthetic files. Each generation gets a unique folder; existing folders are retained. Generation can be cancelled; partial folders are labelled in their manifest.

## Other implemented features

Duplicates: choose a folder, mode **content** for hash identity (or name/size for candidate groups), pattern and depth. Click Find duplicates. Inspect the Groups and summary tab; export paths CSV if needed. This page never deletes files.

Settings: default strategy, search depth, manual threads, adaptive worker cap and run count are saved. Dark theme applies on the next launch. Diagnostic logging is optional.

## Windows Search index

This is a pre-indexed Windows Search database query, not an Explorer-window timer or live crawl. The academic demo query currently has **0 indexed matches vs 21 live matches**. The app reports a mismatch and suppresses the graph rather than publishing an invalid speed comparison. To compare this strategy, choose a folder already fully indexed or manually configure Windows indexing and wait for completion; the app does not change OS indexing settings. PowerShell/index baselines support filename patterns only.

## Verification and fallback

Rust: 16 unit tests and 3 documentation tests pass; Clippy passes. Full native desktop verification plus four large workloads passed. Updated new-feature verification is available with `RAFS-Search.exe --feature-test`. Logs/screenshots are in `target\desktop-verification` and `target\feature-verification`.

If an external baseline fails, use Method 2 / `*.pdf` / academic folder / depth 12. Source-code fallback: `D:\OS_Project\RAFS-Search\src` / `*.rs` / depth 12; **11 files**. For an index mismatch, leave the index configuration off; do not change correctness rules. Physical multi-monitor DPI switching remains a manual display check.
