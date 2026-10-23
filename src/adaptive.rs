//! RAFS hybrid selector and feedback scheduler. RAFS additions: Haris K.
//! Original fixed search engines: Hadi Cahyadi / cumulus13 (MIT).
pub use crate::adaptive_legacy::{Event, Snapshot};
use crate::error::{FsearchError, FsearchResult};
use crate::searcher::{
    fast_find_observed, is_excluded_dir, matches_include, name_matches, recursive_find_observed,
    search_in_file_cancellable, validate_dirs, SearchMatch, SearchObserver, SearchOptions,
};
use rayon::prelude::*;
use std::collections::{HashMap, HashSet, VecDeque};
use std::fs;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, AtomicU64, AtomicUsize, Ordering};
use std::sync::Arc;
use std::time::{Duration, Instant};

#[derive(Default, Debug)]
pub struct Profile {
    pub entries: usize,
    pub files: usize,
    pub children: usize,
    pub directories: usize,
    pub max_branch: usize,
    pub max_depth: u32,
    pub sampled_bytes: u64,
    pub complete: bool,
}
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Strategy {
    Sequential,
    FixedParallel,
    Dynamic,
}
impl Strategy {
    pub fn name(self) -> &'static str {
        match self {
            Self::Sequential => "Sequential DFS",
            Self::FixedParallel => "WalkDir + Rayon",
            Self::Dynamic => "Adaptive DFS/BFS",
        }
    }
}

/// Bounded observation only: sampled entries are not search results and are
/// scanned normally later. Neither sampling truncation nor its ordering affects coverage.
pub fn sample(opts: &SearchOptions, interrupted: &AtomicBool) -> FsearchResult<Profile> {
    let start = Instant::now();
    let mut queue: VecDeque<_> = opts.base_dirs.iter().cloned().map(|p| (p, 0)).collect();
    let mut p = Profile::default();
    while let Some((path, depth)) = queue.pop_front() {
        if interrupted.load(Ordering::Relaxed) {
            return Err(FsearchError::Interrupted);
        }
        if p.directories >= 8 || p.entries >= 256 || start.elapsed() >= Duration::from_millis(2) {
            return Ok(p);
        }
        p.directories += 1;
        p.max_depth = p.max_depth.max(depth);
        let entries = match fs::read_dir(path) {
            Ok(e) => e,
            Err(_) => return Ok(p),
        };
        let mut children = 0;
        for entry in entries {
            if interrupted.load(Ordering::Relaxed) {
                return Err(FsearchError::Interrupted);
            }
            if p.entries >= 256 || start.elapsed() >= Duration::from_millis(2) {
                return Ok(p);
            }
            let entry = match entry {
                Ok(e) => e,
                Err(_) => return Ok(p),
            };
            p.entries += 1;
            let kind = match entry.file_type() {
                Ok(t) => t,
                Err(_) => return Ok(p),
            };
            if kind.is_symlink() {
                continue;
            }
            if kind.is_dir()
                && !is_excluded_dir(&entry.file_name().to_string_lossy(), &opts.exclude_dirs)
            {
                children += 1;
                p.children += 1;
                p.max_branch = p.max_branch.max(children);
                if depth < opts.max_depth {
                    queue.push_back((entry.path(), depth + 1));
                }
            } else if kind.is_file() {
                p.files += 1;
                if opts.search_in_files && p.files <= 8 {
                    if let Ok(m) = entry.metadata() {
                        p.sampled_bytes += m.len();
                    }
                }
            }
        }
    }
    p.complete = true;
    Ok(p)
}

pub fn select(
    opts: &SearchOptions,
    p: &Profile,
    maximum: usize,
) -> (Strategy, usize, &'static str) {
    if opts.base_dirs.len() > 1 {
        return (
            Strategy::Dynamic,
            maximum,
            "Multiple roots: preserve depth-aware overlap scheduling",
        );
    }
    if maximum == 1
        || (p.complete
            && p.entries < 256
            && !(opts.search_in_files && p.files >= 64 && p.sampled_bytes >= 32768))
    {
        return (
            Strategy::Sequential,
            1,
            "Small complete workload or one-worker cap: avoid pool and scheduler overhead",
        );
    }
    if p.children == 0 && p.files >= 128 {
        let threads = maximum.min(if opts.search_in_files && p.sampled_bytes >= 32768 {
            4
        } else {
            2
        });
        return (
            Strategy::FixedParallel,
            threads,
            "Dense sampled file workload: fixed file parallelism avoids per-directory scheduling",
        );
    }
    if opts.search_in_files && p.files >= 64 && p.max_branch <= 2 {
        return (
            Strategy::FixedParallel,
            maximum.min(4),
            "Content-heavy sample with little directory parallelism: use fixed file workers",
        );
    }
    if p.children == 0 {
        return (Strategy::Sequential,1,"No directory parallelism observed before the sample limit: conservative sequential scan");
    }
    (
        Strategy::Dynamic,
        maximum,
        "Directory frontier requires bounded dynamic traversal and concurrency",
    )
}

struct Progress<'a> {
    files: AtomicUsize,
    dirs: AtomicUsize,
    depth: AtomicUsize,
    active: AtomicUsize,
    peak: AtomicUsize,
    last: AtomicU64,
    start: Instant,
    emit: &'a (dyn Fn(Event) + Sync),
    strategy: Strategy,
}
impl Progress<'_> {
    fn snapshot(&self) {
        (self.emit)(Event::Telemetry(Snapshot {
            files: self.files.load(Ordering::Relaxed),
            directories: self.dirs.load(Ordering::Relaxed),
            pending: 0,
            capacity: 0,
            depth: self.depth.load(Ordering::Relaxed) as u32,
            max_depth: self.depth.load(Ordering::Relaxed) as u32,
            workers: self.active.load(Ordering::Relaxed),
            branching: 0.0,
            elapsed_ms: self.start.elapsed().as_secs_f64() * 1000.0,
            traversal: if self.strategy == Strategy::Sequential {
                "DFS"
            } else {
                "Fixed file parallel"
            },
        }));
    }
    fn tick(&self) {
        let active = self.active.load(Ordering::Relaxed);
        if active > self.peak.fetch_max(active, Ordering::Relaxed) {
            self.snapshot();
            return;
        }
        let files = self.files.load(Ordering::Relaxed);
        let dirs = self.dirs.load(Ordering::Relaxed);
        if files & 255 != 0 && dirs & 63 != 0 {
            return;
        }
        let now = self.start.elapsed().as_millis() as u64;
        let last = self.last.load(Ordering::Relaxed);
        if now.saturating_sub(last) >= 100
            && self
                .last
                .compare_exchange(last, now, Ordering::Relaxed, Ordering::Relaxed)
                .is_ok()
        {
            self.snapshot();
        }
    }
}
impl SearchObserver for Progress<'_> {
    fn directory(&self, depth: u32) {
        self.dirs.fetch_add(1, Ordering::Relaxed);
        self.depth.fetch_max(depth as usize, Ordering::Relaxed);
        self.tick();
    }
    fn file_begin(&self, depth: u32) {
        self.files.fetch_add(1, Ordering::Relaxed);
        self.depth.fetch_max(depth as usize, Ordering::Relaxed);
        self.active.fetch_add(1, Ordering::Relaxed);
        self.tick();
    }
    fn file_end(&self) {
        self.active.fetch_sub(1, Ordering::Relaxed);
    }
}

#[derive(Default)]
struct Costs {
    sample: f64,
    decision: f64,
    pool: f64,
    scheduling: f64,
}
fn policy(
    emit: &(dyn Fn(Event) + Sync),
    start: Instant,
    kind: &'static str,
    old: String,
    new: String,
    reason: String,
    workers: usize,
) {
    emit(Event::Policy {
        kind,
        old,
        new,
        reason,
        workers,
        elapsed_ms: start.elapsed().as_secs_f64() * 1000.0,
    });
}
fn milliseconds(start: Instant) -> f64 {
    start.elapsed().as_secs_f64() * 1000.0
}

pub fn adaptive_find(
    opts: &SearchOptions,
    interrupted: Arc<AtomicBool>,
    maximum_workers: usize,
    emit: &(dyn Fn(Event) + Sync),
) -> FsearchResult<Vec<SearchMatch>> {
    if interrupted.load(Ordering::Relaxed) {
        return Err(FsearchError::Interrupted);
    }
    validate_dirs(&opts.base_dirs)?;
    let start = Instant::now();
    let maximum = maximum_workers.clamp(1, 256);
    let mut costs = Costs::default();
    let clock = Instant::now();
    let profile = sample(opts, &interrupted)?;
    costs.sample = milliseconds(clock);
    let clock = Instant::now();
    let (strategy, threads, reason) = select(opts, &profile, maximum);
    policy(
        emit,
        start,
        "Strategy",
        "Unselected".into(),
        strategy.name().into(),
        format!(
            "{}; sampled entries={}, files={}, dirs={}, children={}, max_branch={}, complete={}",
            reason,
            profile.entries,
            profile.files,
            profile.directories,
            profile.children,
            profile.max_branch,
            profile.complete
        ),
        threads,
    );
    costs.decision += milliseconds(clock);
    let result = if strategy == Strategy::Dynamic {
        dynamic(
            opts,
            &interrupted,
            threads,
            emit,
            start,
            &profile,
            &mut costs,
        )
    } else {
        let progress = Progress {
            files: AtomicUsize::new(0),
            dirs: AtomicUsize::new(0),
            depth: AtomicUsize::new(0),
            active: AtomicUsize::new(0),
            peak: AtomicUsize::new(0),
            last: AtomicU64::new(0),
            start,
            emit,
            strategy,
        };
        let result = if strategy == Strategy::Sequential {
            recursive_find_observed(opts, interrupted.clone(), Some(&progress))
        } else {
            let clock = Instant::now();
            let pool = rayon::ThreadPoolBuilder::new()
                .num_threads(threads)
                .build()
                .map_err(|e| FsearchError::Config(e.to_string()))?;
            costs.pool += milliseconds(clock);
            pool.install(|| fast_find_observed(opts, interrupted.clone(), Some(&progress)))
        };
        progress.snapshot();
        result
    };
    emit(Event::Overhead {
        sampling_ms: costs.sample,
        decision_ms: costs.decision,
        pool_ms: costs.pool,
        scheduling_ms: costs.scheduling,
    });
    if interrupted.load(Ordering::Relaxed) {
        return Err(FsearchError::Interrupted);
    }
    result
}

struct Controller {
    workers: usize,
    traversal: &'static str,
    last_change: usize,
    previous_rate: f64,
    previous_density: f64,
    baseline_rate: f64,
    baseline_density: f64,
    degraded: usize,
    ceiling: usize,
}
impl Controller {
    fn new() -> Self {
        Self {
            workers: 0,
            traversal: "",
            last_change: 0,
            previous_rate: 0.0,
            previous_density: 0.0,
            baseline_rate: 0.0,
            baseline_density: 0.0,
            degraded: 0,
            ceiling: usize::MAX,
        }
    }
    fn choose(
        &mut self,
        epoch: usize,
        pending: usize,
        maximum: usize,
        branching: f64,
        density: f64,
        (rate, overhead): (f64, f64),
    ) -> (usize, usize, &'static str, &'static str) {
        let grain = if density < 8.0 && overhead > 0.10 {
            16
        } else {
            4
        };
        let available = maximum.min(pending.div_ceil(grain)).max(1);
        let phase_changed = self.previous_density > 0.0
            && !(0.5..=2.0).contains(&(density / self.previous_density));
        if phase_changed {
            self.ceiling = usize::MAX;
            self.baseline_rate = 0.0;
            self.degraded = 0;
        }
        let comparable =
            self.baseline_density > 0.0 && (0.5..=2.0).contains(&(density / self.baseline_density));
        if comparable && self.baseline_rate > 0.0 && rate > 0.0 && rate < self.baseline_rate * 0.85
        {
            self.degraded += 1;
        } else {
            self.degraded = 0;
        }
        let mut reason = "Available frontier and measured files per directory justify concurrency";
        if self.degraded >= 3 && epoch.saturating_sub(self.last_change) >= 3 {
            self.ceiling = (self.workers / 2).max(1);
            self.baseline_rate = 0.0;
            self.degraded = 0;
            reason = "Three comparable batches lost over 15% throughput; roll back and retain concurrency ceiling for this phase";
        }
        let wanted = available.min(self.ceiling);
        let urgent = available < self.workers || self.workers == 0;
        let first_expansion =
            self.workers == 1 && self.baseline_rate == 0.0 && self.ceiling == usize::MAX;
        if wanted != self.workers
            && (urgent || first_expansion || epoch.saturating_sub(self.last_change) >= 3)
        {
            if wanted > self.workers && self.workers > 0 && density > 0.0 {
                self.baseline_rate = rate;
                self.baseline_density = density;
                self.degraded = 0;
            }
            self.workers = wanted;
            self.last_change = epoch;
        }
        let requested = if pending > maximum * 64 || branching < 1.5 {
            "DFS"
        } else if branching > 3.0 {
            "BFS"
        } else {
            self.traversal
        };
        if requested != self.traversal
            && (self.traversal.is_empty()
                || pending > maximum * 128
                || epoch.saturating_sub(self.last_change) >= 3)
        {
            self.traversal = requested;
            self.last_change = epoch;
        }
        if rate > 0.0 {
            self.previous_rate = if self.previous_rate == 0.0 {
                rate
            } else {
                0.5 * self.previous_rate + 0.5 * rate
            };
        }
        self.previous_density = density;
        (self.workers.min(available), grain, self.traversal, reason)
    }
}
struct Batch {
    children: Vec<(PathBuf, PathBuf, u32)>,
    matches: Vec<SearchMatch>,
    files: usize,
    dirs: usize,
    depth: u32,
}
fn dynamic(
    opts: &SearchOptions,
    interrupted: &Arc<AtomicBool>,
    maximum: usize,
    emit: &(dyn Fn(Event) + Sync),
    start: Instant,
    profile: &Profile,
    costs: &mut Costs,
) -> FsearchResult<Vec<SearchMatch>> {
    let clock = Instant::now();
    let pool = rayon::ThreadPoolBuilder::new()
        .num_threads(maximum)
        .build()
        .map_err(|e| FsearchError::Config(e.to_string()))?;
    costs.pool += milliseconds(clock);
    let mut pending: VecDeque<_> = opts
        .base_dirs
        .iter()
        .map(|p| {
            (
                p.clone(),
                fs::canonicalize(p).unwrap_or_else(|_| p.clone()),
                0,
            )
        })
        .collect();
    let mut seen: HashMap<PathBuf, u32> = HashMap::new();
    let mut seen_matches = HashSet::new();
    let mut matches = Vec::new();
    let mut files = 0;
    let mut dirs = 0;
    let mut depth = 0;
    let mut branch = profile.max_branch as f64;
    let mut density = profile.files as f64 / profile.directories.max(1) as f64;
    let mut rate = 0.0;
    let mut overhead = 0.0;
    let mut control = Controller::new();
    let mut epoch = 0;
    let active = AtomicUsize::new(0);
    while !pending.is_empty() {
        if interrupted.load(Ordering::Relaxed) {
            return Err(FsearchError::Interrupted);
        }
        let scheduling = Instant::now();
        let decision = Instant::now();
        let previous = (control.workers, control.traversal);
        let (workers, grain, traversal, reason) = control.choose(
            epoch,
            pending.len(),
            maximum,
            branch,
            density,
            (rate, overhead),
        );
        if previous.0 != workers {
            policy(emit,start,"Workers",previous.0.to_string(),workers.to_string(),format!("{}; frontier={}, files/dir={:.2}, previous throughput={:.2} work-units/ms, overhead={:.3}",reason,pending.len(),density,rate,overhead),workers);
        }
        if previous.1 != traversal {
            policy(
                emit,
                start,
                "Traversal",
                previous.1.into(),
                traversal.into(),
                format!(
                    "Branching {:.2}, frontier {}; hysteresis 1.5/3 and minimum three-epoch dwell",
                    branch,
                    pending.len()
                ),
                workers,
            );
        }
        let decided = milliseconds(decision);
        costs.decision += decided;
        let mut batch = Vec::new();
        for _ in 0..workers * grain {
            let item = if traversal == "BFS" {
                pending.pop_front()
            } else {
                pending.pop_back()
            };
            if let Some((path, identity, d)) = item {
                if seen.get(&identity).map_or(true, |previous| d < *previous) {
                    seen.insert(identity.clone(), d);
                    batch.push((path, identity, d));
                }
            } else {
                break;
            }
        }
        let planned = milliseconds(scheduling);
        costs.scheduling += (planned - decided).max(0.0);
        if batch.is_empty() {
            epoch += 1;
            continue;
        }
        let queue = pending.len();
        let capacity = pending.capacity();
        let execution = Instant::now();
        let results: Vec<Batch> = pool.install(|| {
            batch
                .par_chunks(grain)
                .flat_map_iter(|chunk| {
                    chunk.iter().map(|(path, identity, d)| {
                        let busy = active.fetch_add(1, Ordering::Relaxed) + 1;
                        emit(Event::Telemetry(Snapshot {
                            files,
                            directories: dirs,
                            pending: queue,
                            capacity,
                            depth: *d,
                            max_depth: depth,
                            workers: busy,
                            branching: branch,
                            elapsed_ms: milliseconds(start),
                            traversal,
                        }));
                        let mut result = Batch {
                            children: Vec::new(),
                            matches: Vec::new(),
                            files: 0,
                            dirs: 1,
                            depth: *d,
                        };
                        match fs::read_dir(path) {
                            Ok(entries) => {
                                for entry in entries {
                                    if interrupted.load(Ordering::Relaxed) {
                                        break;
                                    }
                                    let entry = match entry {
                                        Ok(e) => e,
                                        Err(error) => {
                                            crate::searcher::warn_io(path, &error);
                                            continue;
                                        }
                                    };
                                    let kind = match entry.file_type() {
                                        Ok(t) => t,
                                        Err(error) => {
                                            crate::searcher::warn_io(&entry.path(), &error);
                                            continue;
                                        }
                                    };
                                    if kind.is_symlink() {
                                        continue;
                                    }
                                    let name = entry.file_name().to_string_lossy().to_string();
                                    let p = entry.path();
                                    if kind.is_dir() {
                                        if is_excluded_dir(&name, &opts.exclude_dirs) {
                                            continue;
                                        }
                                        if opts.include_dirs
                                            && !opts.search_in_files
                                            && name_matches(
                                                &name,
                                                &opts.pattern,
                                                opts.case_insensitive,
                                            )
                                        {
                                            result.matches.push(SearchMatch::Path(p.clone()));
                                        }
                                        if *d < opts.max_depth {
                                            result.children.push((
                                                p,
                                                identity.join(entry.file_name()),
                                                d + 1,
                                            ));
                                        }
                                    } else if kind.is_file() {
                                        result.files += 1;
                                        if !matches_include(
                                            &name,
                                            &opts.include_patterns,
                                            opts.case_insensitive,
                                        ) {
                                            continue;
                                        }
                                        if opts.search_in_files {
                                            let lines =
                                                search_in_file_cancellable(&p, opts, interrupted);
                                            if !lines.is_empty() {
                                                result
                                                    .matches
                                                    .push(SearchMatch::Content { path: p, lines });
                                            }
                                        } else if name_matches(
                                            &name,
                                            &opts.pattern,
                                            opts.case_insensitive,
                                        ) {
                                            result.matches.push(SearchMatch::Path(p));
                                        }
                                    }
                                }
                            }
                            Err(error) => crate::searcher::warn_io(path, &error),
                        }
                        active.fetch_sub(1, Ordering::Relaxed);
                        result
                    })
                })
                .collect()
        });
        let execution_ms = milliseconds(execution);
        let merge = Instant::now();
        let scanned_files: usize = results.iter().map(|r| r.files).sum();
        let scanned_dirs: usize = results.iter().map(|r| r.dirs).sum();
        let children: usize = results.iter().map(|r| r.children.len()).sum();
        branch = children as f64 / scanned_dirs.max(1) as f64;
        density = scanned_files as f64 / scanned_dirs.max(1) as f64;
        rate = (scanned_files + scanned_dirs) as f64 / execution_ms.max(0.001);
        for result in results {
            files += result.files;
            dirs += result.dirs;
            depth = depth.max(result.depth);
            pending.extend(result.children);
            for m in result.matches {
                if seen_matches.insert(m.path().to_path_buf()) {
                    matches.push(m);
                }
            }
        }
        let merged = milliseconds(merge);
        costs.scheduling += merged;
        overhead = (planned + merged) / (execution_ms + planned + merged).max(0.001);
        emit(Event::Telemetry(Snapshot {
            files,
            directories: dirs,
            pending: pending.len(),
            capacity: pending.capacity(),
            depth,
            max_depth: depth,
            workers: 0,
            branching: branch,
            elapsed_ms: milliseconds(start),
            traversal,
        }));
        epoch += 1;
    }
    matches.sort_unstable_by(|a, b| a.path().cmp(b.path()));
    if opts.max_results > 0 {
        matches.truncate(opts.max_results);
    }
    Ok(matches)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn hybrid_paths_and_context_match_fixed_engines_at_all_depths() {
        let root = tempfile::tempdir().unwrap();
        for i in 0..80 {
            let path = root.path().join(format!("course{i}/unit/week"));
            fs::create_dir_all(&path).unwrap();
            fs::write(path.join("notes.txt"), "one\nneedle\nthree\n").unwrap();
        }
        fs::write(root.path().join("root.txt"), "needle\n").unwrap();
        for content in [false, true] {
            for depth in [0, 1, 2, 3, 10] {
                for cap in [1, 2, 8] {
                    let opts = SearchOptions::builder(if content { "needle" } else { "*.txt" })
                        .base_dir(root.path())
                        .include_dirs(false)
                        .search_in_files(content)
                        .context_before(1)
                        .context_after(1)
                        .max_depth(depth)
                        .build();
                    let expected =
                        crate::searcher::recursive_find(&opts, Arc::new(AtomicBool::new(false)))
                            .unwrap();
                    let actual =
                        adaptive_find(&opts, Arc::new(AtomicBool::new(false)), cap, &|_| {})
                            .unwrap();
                    assert_eq!(
                        expected.iter().map(|m| m.path()).collect::<Vec<_>>(),
                        actual.iter().map(|m| m.path()).collect::<Vec<_>>()
                    );
                    for (e, a) in expected.iter().zip(&actual) {
                        if let (
                            SearchMatch::Content { lines: e, .. },
                            SearchMatch::Content { lines: a, .. },
                        ) = (e, a)
                        {
                            assert_eq!(e, a);
                        }
                    }
                }
            }
        }
    }
    #[test]
    fn throughput_regression_rolls_back_after_dwell() {
        let mut c = Controller::new();
        assert_eq!(c.choose(0, 4, 8, 0.0, 5.0, (100.0, 0.0)).0, 1);
        assert_eq!(c.choose(3, 32, 8, 0.0, 5.0, (100.0, 0.0)).0, 8);
        assert_eq!(c.choose(4, 32, 8, 0.0, 5.0, (20.0, 0.0)).0, 8);
        assert_eq!(c.choose(5, 32, 8, 0.0, 5.0, (20.0, 0.0)).0, 8);
        let next = c.choose(6, 32, 8, 0.0, 5.0, (20.0, 0.0));
        assert_eq!(next.0, 4);
        for epoch in 7..20 {
            assert_eq!(c.choose(epoch, 32, 8, 0.0, 5.0, (20.0, 0.0)).0, 4);
        }
        assert!(next.3.contains("roll back"));
    }
    #[test]
    fn cancellation_and_active_caps_are_enforced() {
        let root = tempfile::tempdir().unwrap();
        for i in 0..300 {
            fs::write(root.path().join(format!("file{i}.txt")), "needle").unwrap();
        }
        let opts = SearchOptions::builder("*.txt")
            .base_dir(root.path())
            .include_dirs(false)
            .build();
        assert!(matches!(
            adaptive_find(&opts, Arc::new(AtomicBool::new(true)), 4, &|_| {}),
            Err(FsearchError::Interrupted)
        ));
        let busy = AtomicUsize::new(0);
        adaptive_find(&opts, Arc::new(AtomicBool::new(false)), 2, &|e| {
            if let Event::Telemetry(s) = e {
                busy.fetch_max(s.workers, Ordering::Relaxed);
            }
        })
        .unwrap();
        assert!(busy.load(Ordering::Relaxed) <= 2);
        let cancelled = Arc::new(AtomicBool::new(false));
        let flag = cancelled.clone();
        assert!(matches!(
            adaptive_find(&opts, cancelled, 2, &|_| {
                flag.store(true, Ordering::Relaxed);
            }),
            Err(FsearchError::Interrupted)
        ));
    }
    #[cfg(unix)]
    #[test]
    fn dense_flat_selection_does_not_return_symlink_targets() {
        use std::os::unix::fs::symlink;
        let root = tempfile::tempdir().unwrap();
        for i in 0..300 {
            fs::write(root.path().join(format!("f{i}.txt")), "needle").unwrap();
        }
        symlink(root.path().join("f0.txt"), root.path().join("link.txt")).unwrap();
        let opts = SearchOptions::builder("*.txt")
            .base_dir(root.path())
            .include_dirs(false)
            .build();
        for results in [
            crate::searcher::fast_find(&opts, Arc::new(AtomicBool::new(false))).unwrap(),
            adaptive_find(&opts, Arc::new(AtomicBool::new(false)), 4, &|_| {}).unwrap(),
        ] {
            assert_eq!(results.len(), 300);
        }
    }
    #[test]
    fn selector_uses_existing_engines_and_caps_workers() {
        let opts = SearchOptions::builder("*").build();
        let tiny = Profile {
            complete: true,
            entries: 8,
            files: 8,
            ..Profile::default()
        };
        assert_eq!(select(&opts, &tiny, 8).0, Strategy::Sequential);
        let flat = Profile {
            entries: 256,
            files: 256,
            directories: 1,
            ..Profile::default()
        };
        assert_eq!(select(&opts,&flat,8),(Strategy::FixedParallel,2,"Dense sampled file workload: fixed file parallelism avoids per-directory scheduling"));
        let wide = Profile {
            entries: 256,
            children: 256,
            max_branch: 256,
            directories: 1,
            ..Profile::default()
        };
        assert_eq!(select(&opts, &wide, 8).0, Strategy::Dynamic);
        assert_eq!(select(&opts, &flat, 1).0, Strategy::Sequential);
    }
    #[test]
    fn policy_has_dwell_and_reacts_to_pressure() {
        let mut c = Controller::new();
        let initial = c.choose(0, 500, 8, 0.0, 2.0, (0.0, 0.2));
        assert_eq!(initial.0, 8);
        let a = c.choose(1, 500, 8, 8.0, 2.0, (20.0, 0.2));
        assert_eq!(a.2, "DFS");
        let b = c.choose(3, 500, 8, 8.0, 2.0, (20.0, 0.2));
        assert_eq!(b.2, "BFS");
        let d = c.choose(4, 1, 8, 0.0, 2.0, (20.0, 0.2));
        assert_eq!(d.0, 1);
    }
    #[test]
    fn bounded_sampling_and_flat_content_agree_with_ground_truth() {
        let root = tempfile::tempdir().unwrap();
        for i in 0..500 {
            fs::write(root.path().join(format!("f{i}.txt")), "needle\nother").unwrap();
        }
        for content in [false, true] {
            let opts = SearchOptions::builder(if content { "needle" } else { "*.txt" })
                .base_dir(root.path())
                .search_in_files(content)
                .include_dirs(false)
                .build();
            let flag = Arc::new(AtomicBool::new(false));
            let p = sample(&opts, &flag).unwrap();
            assert!(p.entries <= 256);
            assert!(p.directories <= 8);
            let actual = adaptive_find(&opts, flag, 4, &|_| {}).unwrap();
            assert_eq!(actual.len(), 500);
        }
    }
    #[test]
    fn hybrid_preserves_depth_and_overlapping_roots() {
        let root = tempfile::tempdir().unwrap();
        fs::create_dir_all(root.path().join("a/b")).unwrap();
        fs::write(root.path().join("a/b/x.txt"), "needle").unwrap();
        let opts = SearchOptions::builder("*.txt")
            .base_dirs(vec![root.path().join("a"), root.path().to_path_buf()])
            .max_depth(1)
            .include_dirs(false)
            .build();
        assert_eq!(
            adaptive_find(&opts, Arc::new(AtomicBool::new(false)), 4, &|_| {})
                .unwrap()
                .len(),
            1
        );
    }
}
