# RAFS desktop demonstration

Double-click **RAFS-Search.exe** in the repository root. It opens a Windows desktop interface without a terminal. Keep it in this folder: it runs the original `target/release/fsearch.exe` search engine.

1. Click **Load demo folder** (uses the bundled harmless `demo-data` folder), or **Browse...** to choose a folder.
2. Enter a filename pattern such as `*.txt`, `*.pdf`, or `report`.
3. Choose method 1 (WalkDir + Rayon) or method 2 (recursive DFS).
4. Choose depth (0 searches only the selected folder). For method 1, choose a fixed thread count; 0 means automatic. Method 2 is sequential.
5. Click **Search**. Results appear when the original engine finishes; the interface remains responsive. **Cancel** stops the search process.
6. Double-click a result to select it in File Explorer, or use **Export CSV**.

For content search, check **Search file contents** and enter literal text. Read matching lines in the **Search output / content matches** tab. Engine elapsed time and total time including process launch are displayed separately; these are demonstration timings, not controlled benchmarks.

## Compare search methods

Click **Compare + graph** to compare the original sequential recursive method against parallel fsearch on the selected folder and pattern. Choose the parallel thread count before starting. The app warms up both methods, alternates execution order, measures five runs each, checks equal file-path sets on every run, and graphs median elapsed milliseconds including process launch/output collection. Lower bars mean faster searches. Either method may win; small datasets can be dominated by launch overhead. This is not Windows Explorer or an adaptive-engine benchmark.

The interface and comparison demonstration are by **Haris K ([Haris757518](https://github.com/Haris757518))**. Original engine attribution remains **Hadi Cahyadi / cumulus13**.

## Build

From the repository root in PowerShell:

```powershell
cargo build --release
.\desktop-app\build.ps1
```

The interface is a small C#/.NET Framework Windows Forms application using the compiler already installed with Windows. No GUI dependencies were added to Cargo. The Rust engine, CLI, original package names, MIT LICENSE and author attribution remain unchanged. A future adaptive engine can be exposed through this interface later.

The GUI creates an isolated configuration under `target/desktop-runs` for each search and deletes it afterward. This makes the selected thread count explicit without modifying your user configuration. Other runtime settings use the original defaults. Filename and content searches are supported; duplicate detection remains available through the original CLI.

## Verification

```powershell
$check = Start-Process .\RAFS-Search.exe -ArgumentList '--self-test' -PassThru -Wait
$check.ExitCode
Get-Content .\target\desktop-verification\checks.txt
```

The integration check exercises both methods, 1/2 fixed threads for method 1, recursive search, depth 0, content search, no matches, a folder containing spaces, and trailing path separators. It also tests the repeated comparison and renders the form and graph to `target/desktop-verification/desktop-preview.png` and `comparison-preview.png` for layout inspection.

All GUI changes are on `rafs-development`. The original `master` baseline is retained. **No adaptive workload profiling, DFS/BFS switching, adaptive thread control, CPU/I/O monitoring, or dashboard has been implemented.**
