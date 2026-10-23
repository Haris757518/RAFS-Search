# RAFS Windows desktop application — 1.1

The existing six-page application now uses hybrid adaptive search. See [PROJECT_STATUS.md](../PROJECT_STATUS.md), the [research report](../research/REPORT.md) and [production guide](../PRODUCTION_GUIDE.md) for current behavior and measured limits. Earlier Review I documents describe historical milestones.

Search supports the original fixed parallel and sequential methods, RAFS Adaptive, PowerShell and Windows Search Index. Adaptive selects among sequential DFS, fixed WalkDir + Rayon and dynamic DFS/BFS after a bounded sample. No extra GUI method is needed. The Live Monitor records selection reasons, actual scan counters, worker/traversal changes and measured overhead. Unavailable frontier metrics remain blank for sequential/fixed scans.

Compare and Benchmark Lab use repeated, rotating-order runs and reject unequal result paths. The dedicated research command adds legacy adaptive, CPU/memory samples, three warmups and ten measured runs; see research/README.md. Indexed and live search have different coverage and are interpreted separately.

The Duplicates page is read-only. Settings use LocalAppData with validated atomic writes; diagnostics can contain local paths. Text-content search does not extract PDF/Office contents. Adaptive is not guaranteed to be fastest.

For development, run cargo build --release and desktop-app/build.ps1. For distribution, use package.ps1 and build-installer.ps1. The portable package includes its engine and synthetic demo files; it does not include academic files or generated benchmarks. verify-release.ps1 and verify-installer.ps1 exercise packaged functionality and installer ownership. Physical DPI transitions, screen readers and broad field testing remain manual validation areas.

The executable version is 1.1.0 and remains unsigned by request. Hadi Cahyadi / cumulus13 is the original fsearch author; Haris K implements RAFS extensions. See LICENSE and AUTHORS.md.