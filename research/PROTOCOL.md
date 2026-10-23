# Frozen hybrid evaluation protocol

Freeze the selector thresholds and engine/source hashes before the full sweep. The October 22 dynamic algorithm remains in `src/adaptive_legacy.rs`; only shared event type additions are permitted, not changes to its search body. Original public DFS and WalkDir/Rayon baselines remain available. Optional hybrid observers count their actual work; their cost stays in hybrid timings. All configurations receive identical OS resource polling.

Compare DFS, fixed Rayon 1/2/4/8, October-22 adaptive cap 8, hybrid cap 8 and PowerShell. PowerShell is omitted only for content matching, which it does not implement here. The Windows index is observed separately and never included in the fastest-live ranking.

Use three warmups and ten measured repetitions, rotate configuration order every round, and preserve every invocation including warmups. Ground truth is an independent .NET filesystem enumeration (and independent line matching for the generated content corpus). Reject failed/incomplete scans and any unequal result set. Store counts and normalized set hashes; do not publish document contents or matched academic filenames.

Development workloads: the existing 20,000-file deep, wide, mixed and flat trees. Holdout workloads: a new 12,000-file flat tree, a 128-file text-content tree (~61 KiB per file), actual Rust source, actual desktop source and academic copies if present. Holdouts are not evidence of arbitrary population-wide generalization. Tiny repository/academic folders test overhead as well as correctness. Original documents in D:\VIT are never modified.

Terminology clarification: these secondary templates were exercised in the pre-freeze smoke test. They are called holdouts by the harness but are not blinded unseen datasets; the report must disclose this limitation.

Record total process elapsed time, Rust/PowerShell internal time, cumulative process CPU milliseconds, sampled peak working set and monitoring cost. Poll OS resources every 10 ms for every configuration. Working set is a sampled lower bound; short processes can peak between samples. Windows CPU accounting is quantized and can report zero for very short work. Report sampling, policy, pool startup and scheduling costs separately; include them in hybrid elapsed time. Profiling is capped at eight directories/256 entries and has a soft 2 ms budget checked between filesystem calls; a blocking OS call can exceed that time.

Compare hybrid against the lowest-median fixed strategy (DFS or fixed 1/2/4/8), not only PowerShell. Report all wins/losses, per-round ratios, medians, dispersion and bootstrap intervals with a fixed seed. These are warm-cache, single-machine process measurements; startup and output handling matter. Selecting the fastest baseline from the same experiment introduces selection bias. Intervals across ten repetitions do not establish multi-machine significance or a universal speedup.

For adaptation evidence, retain the actual strategy/worker/traversal events and their elapsed times, reasons and overhead. The hybrid commits to an engine once per search; it never restarts a completed prefix to retrospectively pick the fastest outcome. Dynamic mode uses throughput feedback and hysteresis within that engine. Queue pressure can override a dwell period for capacity protection.
