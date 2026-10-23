# RAFS Search 1.1 — release validation, 23 October 2026

The release implements bounded hybrid selection, dynamic concurrency feedback and measured explanations in the existing application. It restores Hadi Cahyadi's original fsearch copyright and attribution, with RAFS extensions credited separately. The [research report](research/REPORT.md) documents algorithm design, experiments and targets not fully achieved.

## Verified source and behavior

- Work began from `rafs-release-hardening-20261022`, source commit `0c1383d`, preserving the existing release functionality.
- Engine/harness policy was frozen before the full sweep at `f26d83de2a6d85c393a1bd481643dda27e75e513`. Final changes add reports, graphs, attribution in the package and updated documentation; they do not retune the measured policy.
- 28 Windows Rust unit tests, three documentation tests, formatting and strict Clippy passed locally. Temporary documentation executables required running outside the restricted sandbox.
- Existing production, feature, layout and one-minute soak verification passed on staged and packaged Windows builds. The packaged soak ran 1,170 filename/content searches in 60.1 seconds with identical 100-file coverage, no scratch leaks and handle counts baseline 316 / peak 339.
- The frozen implementation passed all eleven [GitHub CI jobs](https://github.com/Haris757518/RAFS-Search/actions/runs/39901578724), including Linux/macOS/Windows tests, five platform builds, and Windows Server 2022/2025 package/UI/installer verification. The final documentation/packaging revision is separately checked on [PR #1](https://github.com/Haris757518/RAFS-Search/pull/1).
- Full research: nine datasets, 71 configuration/dataset cells, 213 warmups and 710 measured invocations, all passing independent path-set checks. No failed/outlier runs were silently removed. Windows Search Index returned 0 vs 21 live PDFs and was excluded from live performance ranking.
- Final release executables were copied byte-for-byte from the fully verified portable build. Only README and AUTHORS.md were added/updated during clean final assembly. The final installer independently passed clean per-user install, installed production checks, upgrade and uninstall while preserving an unowned user file.

## Public artifacts

| Artifact | SHA256 |
|---|---|
| RAFS-Search-1.1.0-Windows-x64.zip | `8a08778cab660c4aaffdcf6b66fe157de17e979663a389d3ed725b59c3e71939` |
| RAFS-Search-1.1.0-Windows-x64-Setup.exe | `e54ce64cfde7f8411a4245e13c4f8098909dae1e32fb30cf0c7930427a92ffcc` |
| Desktop executable | `a93cfbe9cba8de00ba55d7b48df08f4a18ffe8ac676e1ab2a5dd0fca3e8c652c` |
| Rust engine | `a47be4010d3d0fe1e1a72f50a38893a18ecc3cde1d59522b834d4ce4367dfb58` |

The ZIP inventory was inspected: executable, configuration, engine, LICENSE, AUTHORS.md, README and four public synthetic demo files only. Test fixtures, academic copies/originals, generated corpora, settings and diagnostics are excluded. Reports publish only dataset aliases, result counts, set hashes, numeric samples and policy explanations. Original academic documents were not modified. The old local root executable is not silently replaced or restarted.

Desktop/installer version is 1.1.0; upstream Rust package version remains 1.1.2. The Windows artifacts are **unsigned**, as requested. Checksums detect file changes but are not an authenticated publisher signature.

## Performance conclusion and limits

Hybrid median process time beat the fastest fixed configuration by 9.4%/40.5%/41.4% on deep/wide/mixed synthetic work and was close on large flat filename scans. These topology gains were already largely present in old adaptive. Flat filename scans did not improve over old adaptive; the flat content case improved over old adaptive but lost to fixed eight. Tiny-folder overhead remains. Wide/mixed gains also cost more CPU. No claim of universal superiority or perfection is made.

Measurements are warm-cache on one Windows machine. Secondary “holdout” templates were seen in the pilot and are not blinded validation data. No cold-cache, energy, large real-corpus, network-share or multi-machine claim follows. Physical display transitions, screen-reader usability and broad client-machine field testing remain manual work. These limitations do not prevent distributing this tested unsigned build, but they constrain its research claims.
