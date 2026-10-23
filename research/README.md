# Reproduce the hybrid evaluation

Read [PROTOCOL.md](PROTOCOL.md) before interpreting results and [DESIGN.md](DESIGN.md) for pseudocode, flowchart and literature. [REPORT.md](REPORT.md) contains the completed experiment and limitations. The public `results/` files contain dataset aliases and path-set hashes, not document names or contents.

## Build and inputs

Build a release engine with `cargo build --release --locked`. Build the desktop runner into `target/hybrid-stage` with `desktop-app/build.ps1 -OutputDirectory target/hybrid-stage`; copy the release fsearch.exe into that directory's `engine/` subfolder. A package created with `desktop-app/package.ps1` also works; pass its directory to `-PackageDirectory`.

Generate Deep, Wide, Mixed and Small Files workloads with 20,000 files each using the existing Benchmark Lab generator. Write their four absolute directory paths, one per line, to a UTF-8 text file. The harness verifies all four inputs have exactly 20,000 `.txt` files. Dataset manifests are additional files, and are scanned consistently by every engine. Pass that text file to `-WorkloadList`. The original experiment used existing October 12 synthetic trees; their manifest and topology counts are recorded in the report. Generated corpus files are not committed.

The harness creates fresh 12,000-file flat and 128-file content fixtures inside the output directory. It reads actual `src/` and `desktop-app/` folders. If `D:\RAFS_Demo\01_Academic_Search` exists, it also reads its PDF filenames; otherwise the optional academic dataset is omitted. Academic original documents are never needed. Do not use a folder undergoing active modification.

## Execute

```powershell
.\research\run-benchmarks.ps1 -PackageDirectory .\target\hybrid-stage `
    -WorkloadList .\target\my-workloads.txt -OutputDirectory .\target\research-new
```

Use a new output directory. The script records the Git revision and engine hash, removes smoke-mode settings, and runs three warmups followed by ten measured rounds with rotating configuration order. Keep other builds, applications and tests idle. OS background activity is still an uncontrolled limitation. Do not compare a partial run as if it were complete. `COMPLETE.txt` is written only after all path checks pass. The settings directory may be isolated with the existing `RAFS_STATE_DIR` environment override.

Outputs include samples.csv (including warmups), datasets.csv, decisions.csv, telemetry.csv, metadata.txt and a separate index observation. Process timing includes launch, output handling and adaptive instrumentation. The index observation is never part of the live traversal ranking.

## Analyze

Use Python 3.14 with the versions in requirements.txt, or install compatible wheels in a virtual environment. The analyzer also recognizes dependencies installed in this project's ignored `target/research-python` directory.

```powershell
python -m pip install -r research/requirements.txt
python research/analyze.py target/research-new target/research-analysis
```

The analyzer rejects missing repetitions or unequal correctness hashes, summarizes all configurations, selects the fastest fixed median for each dataset and generates PNG/SVG figures. It uses paired round indices, 10,000 bootstrap resamples and seed 20261023. Baseline selection on the same data creates selection bias; ten repetitions on one machine are limited evidence. See the report rather than treating confidence intervals as a universal guarantee.

Only explicitly reviewed CSVs, hardware metadata without user/machine identifiers, and graphs should be copied into `research/results`. Do not upload fixtures, matched documents, filenames, settings, logs or screenshots containing private paths.
