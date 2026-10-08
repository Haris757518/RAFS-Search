# Original fsearch baseline

- Future OS project: **Runtime-Adaptive File System Search Using Workload-Aware Traversal and Concurrency Selection**.
- Original repository: https://github.com/Haris757518/fsearch
- Cloned on: **2026-10-08** (Asia/Calcutta).
- Local path: `D:\OS_Project\RAFS-Search`.
- Upstream baseline: `master`, commit `e74d9d5b72662a23f7ae8a0dde527be3d2517365` (fast-search 1.1.2; full 12-commit history retained).
- Active future-work branch: `rafs-development`, created from the unchanged baseline.
- Original purpose: cross-platform Rust CLI/library for filename/glob search, file-content search, and duplicate detection by content hash, name, or size.
- At initial baseline verification, original MIT LICENSE, Haris K attribution, package/crate names, and source files were unchanged, and this notes file was the only added repository file.

## Original traversal and concurrency

- Method 1 (`-m 1`): `src/searcher.rs:407`, `fast_find`. Rayon processes roots with `par_iter`; each root is traversed with `WalkDir` (`:424`), entries are collected into a vector, then matched using `into_par_iter` (`:442`). It skips excluded directories, does not follow symlinks, respects depth, and sorts results by path.
- Method 2 (`-m 2`): `src/searcher.rs:511`, `recursive_find`, calling `walk_dir` (`:531`). Sequential manual DFS using `std::fs::read_dir`; results are sorted by path. It does not use Rayon for traversal/matching.
- Duplicate detection also uses WalkDir and Rayon in `src/duplicates.rs` (`:372`, `:367`, `:458`).
- Thread configuration: top-level TOML `threads` field in `src/config.rs:39`, default `0`. `src/main.rs:275` configures the global Rayon pool with `ThreadPoolBuilder::num_threads` for positive values. Zero leaves Rayon automatic selection (normally available logical CPUs; Rayon environment settings can affect it). There is no `--threads` CLI argument. Method 2 remains sequential even though CLI startup configures the pool.
- Config loading: `src/config.rs:130` checks `fsearch.toml` in the current working directory, then `dirs::config_dir()/fsearch/config.toml`, then built-in defaults. On Windows the user location is normally under `%APPDATA%`; run `fsearch config path` for the actual path. The root `config.toml` is commented cross-compilation guidance, not the runtime configuration. `fsearch.toml.example` provides runtime examples.
- Baseline caveat: the CLI hard-codes method/depth defaults and derives directory inclusion from `-D`; the config's `default_method`, `default_depth`, and `include_dirs` do not control those CLI options. Use explicit `-m`, `-d`, and `-D`. This behavior was left unchanged.

## Build and run on Windows (PowerShell)

Rust was missing and installed with the official rustup installer, stable MSVC toolchain and default profile. Existing Visual Studio C++ Build Tools were sufficient. Verified versions: `rustc 1.99.0 (b940084d7 2026-09-28)` and `cargo 1.99.0 (5f94df478 2026-08-27)`.

```powershell
Set-Location D:\OS_Project\RAFS-Search
cargo build --release
cargo test
cargo clippy
.\target\release\fsearch.exe find '*.rs' -p .\src -d 5 -m 1
.\target\release\fsearch.exe find '*.rs' -p .\src -d 5 -m 2
.\target\release\fsearch.exe find --help
.\target\release\fsearch.exe config show
.\target\release\fsearch.exe config path
```

Both `target/release/fsearch.exe` and the original alias `target/release/fs.exe` are built. Open a new terminal if Cargo is not yet on PATH; alternatively use `& "$env:USERPROFILE\.cargo\bin\cargo.exe"`.

To select a fixed thread count, create `fsearch.toml` in the working directory containing `threads = 2`, then launch method 1 from that directory. No runtime config was added to the cloned repository.

## Baseline verification

- `cargo build --release`: PASS, original source and lockfile unchanged.
- `cargo test`: PASS, 14 unit tests and 3 documentation tests; zero failures.
- `cargo clippy`: PASS, no source lint warnings. Cargo emits an existing manifest warning because both binaries share `src/main.rs`.
- Harmless fixture: `D:\OS_Project\baseline-verification\data`, with three matching text files at root, nested, and deeper levels plus an unrelated file.
- Methods 1 and 2 with `-d 5`: each found all three expected filenames.
- Methods 1 and 2 with `-d 0`: each found only the root matching file.
- Method 1 with local configs `threads = 1` and `threads = 2`: `config show` confirmed each setting and searches returned all three files successfully. Test config directories are outside the repository at `D:\OS_Project\baseline-verification\threads-1` and `threads-2`.
- Restricted network access initially blocked dependency downloads; allowing network access resolved setup. No source fixes or dependency changes were necessary.

## Source map and likely future modification points

| File | Role / future relevance |
| --- | --- |
| `src/searcher.rs` | SearchOptions, method 1, method 2, traversal, matching and existing search unit tests; primary future traversal-policy integration point. |
| `src/main.rs` | CLI entry, configuration loading, engine dispatch, fixed Rayon pool setup and interrupt handling; future policy/concurrency selection integration. |
| `src/cli.rs` | Clap argument handling, FindArgs, SearchMethod and config subcommands; future user-facing options. |
| `src/config.rs` | Serde/TOML configuration, defaults and persistence; future policy settings. |
| `src/lib.rs` | Public library modules and exports; future public API integration. |
| `Cargo.toml` | Dependencies, original package/library/binary names and release profile. |
| `fsearch.toml.example` | Example runtime settings; update later if settings are extended. |
| `src/duplicates.rs` | Separate duplicate engine using WalkDir/Rayon; not required for filename-search adaptation unless scope expands. |
| `src/output.rs`, `src/colors.rs` | Result formatting and colors. |
| `src/binary.rs`, `src/error.rs` | Binary-content detection and error types. |

**Adaptive workload-aware functionality has NOT yet been implemented.** No DFS/BFS switching, workload profiling, adaptive thread selection, dashboard, CPU monitoring, or I/O monitoring has been added.

## Subsequent desktop demonstration interface (2026-10-08)

At the user's request, a separate Windows desktop interface was added under `desktop-app/` on `rafs-development`, compiled to `RAFS-Search.exe` in the repository root. It launches the existing original `target/release/fsearch.exe` through its CLI. The original Rust source, Cargo manifests/lockfile, LICENSE, and `master` branch remain unchanged. See `desktop-app/README.md` for double-click usage, rebuilding, and integration checks. The interface supports both baseline methods, fixed thread settings, filename/content search, cancellation, results export, and demonstration elapsed times. It does not implement adaptive functionality.

The desktop interface now credits Haris K (Haris757518) for the RAFS additions while retaining upstream attribution in provenance/license files and original sources. A comparison graph measures sequential method 2 versus parallel method 1 using one warm-up and five measured runs each, alternating order and checking identical file-path sets. Median total process elapsed time is graphed. This is a baseline-method comparison, not evidence of an adaptive speedup. RAFS documentation is in `RAFS_README.md` and the main `README.md`; original documentation is preserved in `UPSTREAM_README.md`.
