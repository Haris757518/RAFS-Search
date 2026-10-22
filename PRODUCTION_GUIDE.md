# RAFS Search 1.0 — Windows release

Extract the entire ZIP, then open `RAFS-Search.exe`. Keep the `engine` directory beside it. Windows x64 and .NET Framework 4.8 are required. The supplied engine is x64; the desktop host is AnyCPU. No Rust toolchain is needed to use the package.

Alternatively, run the `-Setup.exe` installer. It installs for the current user without administrator privileges, creates a Start menu entry and registers an uninstaller. Run a newer installer to update. Uninstall removes installed application files and retains preferences, generated workloads, exports and files it does not own. Verify the companion SHA-256 checksum when transferring a build. The four bundled synthetic demo files contain no academic documents; "Load demo folder" selects three report matches.

Settings, temporary search configuration, diagnostics and generated benchmark datasets use `%LOCALAPPDATA%\RAFS-Search`. Application files can therefore live in a folder without write access. A legacy development settings file is migrated when available. `RAFS_STATE_DIR` overrides the state directory for testing.

## Search and comparison

- Method 1: original WalkDir/Rayon parallel traversal; configurable threads.
- Method 2: original recursive sequential traversal.
- RAFS Adaptive: bounded worker pool; adjusts traversal order and concurrent batches from the observed directory frontier. The worker cap is a maximum, not a promise that every worker will be busy.
- Traditional PowerShell: live filename traversal using Windows PowerShell without profiles.
- Windows Search Index: queries the same indexing service used by Explorer. It does not measure the Explorer window. Unindexed folders, stale entries and service availability can change results. Index settings are never modified automatically.

Compare and Benchmark Lab require identical normalized result paths for every configuration and every run, including warmups. A failed or incomplete scan or unequal results stops the comparison. Result-path equality checks filename coverage; it does not establish equivalence of every matching content line. PowerShell and Windows index are filename-only baselines.

Total time includes process startup, execution and reading output. Engine time is reported separately: Rust measures traversal/content matching before printing results; PowerShell measures its search pipeline including its output. These internal timings have different scopes. Use total medians for user-visible comparisons. Run multiple repetitions on the same unchanged folder, with warmups, and retain the CSV. Rotated execution order reduces order bias, but these are warm-cache desktop measurements, not a controlled cold-cache experiment. Adaptive search need not win every workload.

## Reliability and limits

Cancel stops the owned search process. Settings control the per-search timeout (default 300 seconds, maximum 3600). Preferences use atomic replacement and serialized writes. Invalid preferences recover to defaults. Unreadable live-search entries are reported as incomplete and cannot enter comparisons.

The raw-output preview is limited to about 2 million characters. Matching paths and matching content lines are retained in memory; very broad searches can still use substantial memory. Content search processes bounded lines and skips binary/non-UTF-8 content and lines beyond the configured length. It searches text bytes, not extracted PDF, DOCX, OCR or archive contents. Filename search works for those file types. Searches do not follow descendant symbolic links; directory permissions and filesystem changes affect coverage. Matching/glob details can differ across Windows indexing and live methods, so comparisons verify actual results.

Duplicate search is read-only. CSV exports escape spreadsheet-formula prefixes. Searches, exports and generated workloads remain local. Diagnostics can contain local paths; review them before sharing.

The current release is unsigned because no publisher code-signing certificate is available. Build scripts support signing the app, engine and installer with a real certificate and timestamp service. Do not substitute a self-signed certificate and claim public publisher trust. Software is not downloaded or replaced automatically; updates use a user-selected installer. Remote release hosting and CI require publishing the reviewed source/release to GitHub. Physical monitor changes, screen-reader usability, clean client Windows machines and network-share tests still require those environments. Local verification is evidence for the tested build and machine, not a guarantee that software is perfect on every system.

Keyboard navigation: Alt+1 through Alt+6 open Search, Live Monitor, Compare, Benchmark Lab, Duplicates and Settings. Ctrl+F selects the search query, Ctrl+L selects the folder, Ctrl+Enter starts a search from Search, and Escape cancels running work. Input fields and the results grid expose accessible names. The layout suite simulates 100%, 125% and 150% scaling; this does not substitute for physical mixed-monitor or screen-reader testing.

## Verification and development

The package includes a source-independent verification mode: `RAFS-Search.exe --production-test`. It creates isolated state in the temporary directory and a `verification` folder beside the executable, so this test mode needs a writable extraction directory. Normal use writes only to the state directory unless you choose an export destination. `--feature-test` additionally exercises all six pages and writes screenshots. Verification uses isolated preferences and does not change academic source files. Generated deep workloads adapt their depth to keep paths below 240 characters for the legacy PowerShell baseline; excessively long state paths are rejected before generation with instructions to choose a shorter state directory.

From the repository: run `cargo test`, `cargo fmt --all -- --check`, `cargo clippy --all-targets --all-features -- -D warnings`, and `desktop-app\package.ps1`. Run `desktop-app\verify-release.ps1` for production, feature, layout/accessibility and one-minute repeated-search checks. To build Setup, run `desktop-app\bootstrap-installer-tools.ps1` once, then `desktop-app\build-installer.ps1`. The compiler download is pinned and requires a valid publisher signature. `desktop-app\verify-installer.ps1` checks clean install, production checks, upgrade and uninstall in a fresh project test directory; it refuses to overwrite an existing RAFS installation registration.

The Windows CI matrix covers Windows Server 2022 and 2025 hosts, and Rust CI covers Linux, macOS and Windows. It packages and verifies the application and installer, and retains logs/screenshots. These hosted server tests do not claim client Windows 10/11 field coverage. CI has not been run remotely until these changes are published.

Signing: pass `-CertificateThumbprint` and `-TimestampServer` to `package.ps1` to sign the app and engine before ZIP creation; pass the same options to `build-installer.ps1` for the installer. Scripts reject missing private keys, expired certificates, wrong key usage and invalid signatures. Rebuild/checksum artifacts after signing. Certificates and private keys are never included in the ZIP or repository.

SHA-256 files detect accidental package changes; they are not an authenticated publisher signature.
