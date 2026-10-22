//! Workload-aware directory scheduling. Baseline methods remain independent.
use crate::error::{FsearchError, FsearchResult};
use crate::searcher::{
    is_excluded_dir, matches_include, name_matches, search_in_file_cancellable, validate_dirs,
    SearchMatch, SearchOptions,
};
use rayon::prelude::*;
use std::collections::{HashMap, HashSet, VecDeque};
use std::fs;
use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
use std::sync::Arc;
use std::time::Instant;

#[derive(Clone, Debug)]
pub struct Snapshot {
    pub files: usize,
    pub directories: usize,
    pub pending: usize,
    pub capacity: usize,
    pub depth: u32,
    pub max_depth: u32,
    pub workers: usize,
    pub branching: f64,
    pub elapsed_ms: f64,
    pub traversal: &'static str,
}
pub enum Event {
    Telemetry(Snapshot),
    Decision {
        kind: &'static str,
        old: String,
        new: String,
        reason: &'static str,
    },
}
struct Batch {
    children: Vec<(PathBuf, PathBuf, u32)>,
    matches: Vec<SearchMatch>,
    files: usize,
    dirs: usize,
    depth: u32,
}

/// Changes DFS/BFS scheduling and batch concurrency using observed pending work.
/// A bounded worker pool is reused; concurrency changes without restarting search.
pub fn adaptive_find(
    opts: &SearchOptions,
    interrupted: Arc<AtomicBool>,
    maximum_workers: usize,
    emit: &(dyn Fn(Event) + Sync),
) -> FsearchResult<Vec<SearchMatch>> {
    validate_dirs(&opts.base_dirs)?;
    let maximum = maximum_workers.clamp(1, 256);
    let pool = rayon::ThreadPoolBuilder::new()
        .num_threads(maximum)
        .build()
        .map_err(|e| FsearchError::Config(e.to_string()))?;
    // Resolve root aliases once. Descendant links are skipped, so directory
    // identities can be derived from their resolved parent without opening
    // every directory a second time on the scheduling thread.
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
    let mut seen_dirs: HashMap<PathBuf, u32> = HashMap::new();
    let mut seen_matches = HashSet::new();
    let mut matches = Vec::new();
    let start = Instant::now();
    let mut files = 0;
    let mut dirs = 0;
    let mut max_depth = 0;
    let mut branching = 0.0;
    let mut workers = 0;
    let mut traversal = "";
    let active = AtomicUsize::new(0);
    while !pending.is_empty() {
        if interrupted.load(Ordering::Relaxed) {
            return Err(FsearchError::Interrupted);
        }
        // Queue pressure favors DFS; a broad frontier otherwise favors BFS.
        let next_traversal = if pending.len() > maximum * 64 || branching < 2.0 {
            "DFS"
        } else {
            "BFS"
        };
        let next_workers = if pending.len() < 4 {
            1
        } else {
            maximum.min(pending.len().div_ceil(4)).max(1)
        };
        if next_traversal != traversal {
            emit(Event::Decision {
                kind: "Traversal",
                old: traversal.into(),
                new: next_traversal.into(),
                reason: "Observed branching and pending-directory pressure",
            });
            traversal = next_traversal;
        }
        if next_workers != workers {
            emit(Event::Decision {
                kind: "Workers",
                old: workers.to_string(),
                new: next_workers.to_string(),
                reason: "Concurrency limited by available directory work and worker cap",
            });
            workers = next_workers;
        }
        let mut batch = Vec::new();
        for _ in 0..workers * 4 {
            let item = if traversal == "BFS" {
                pending.pop_front()
            } else {
                pending.pop_back()
            };
            if let Some((path, identity, depth)) = item {
                if seen_dirs
                    .get(&identity)
                    .map_or(true, |previous| depth < *previous)
                {
                    seen_dirs.insert(identity.clone(), depth);
                    batch.push((path, identity, depth));
                }
            } else {
                break;
            }
        }
        if batch.is_empty() {
            continue;
        }
        let queue_len = pending.len();
        let capacity = pending.capacity();
        let results: Vec<Batch> = pool.install(|| {
            batch
                .par_chunks(4)
                .flat_map_iter(|chunk| {
                    chunk.iter().map(|(path, identity, depth)| {
                        let busy = active.fetch_add(1, Ordering::Relaxed) + 1;
                        emit(Event::Telemetry(Snapshot {
                            files,
                            directories: dirs,
                            pending: queue_len,
                            capacity,
                            depth: *depth,
                            max_depth,
                            workers: busy,
                            branching,
                            elapsed_ms: start.elapsed().as_secs_f64() * 1000.0,
                            traversal,
                        }));
                        let mut result = Batch {
                            children: Vec::new(),
                            matches: Vec::new(),
                            files: 0,
                            dirs: 1,
                            depth: *depth,
                        };
                        match fs::read_dir(path) {
                            Ok(entries) => {
                                for entry in entries {
                                    let entry = match entry {
                                        Ok(entry) => entry,
                                        Err(error) => {
                                            crate::searcher::warn_io(path, &error);
                                            continue;
                                        }
                                    };
                                    if interrupted.load(Ordering::Relaxed) {
                                        break;
                                    }
                                    let ft = match entry.file_type() {
                                        Ok(ft) => ft,
                                        Err(error) => {
                                            crate::searcher::warn_io(&entry.path(), &error);
                                            continue;
                                        }
                                    };
                                    if ft.is_symlink() {
                                        continue;
                                    }
                                    let name = entry.file_name().to_string_lossy().to_string();
                                    let p = entry.path();
                                    if ft.is_dir() {
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
                                        if *depth < opts.max_depth {
                                            result.children.push((
                                                p,
                                                identity.join(entry.file_name()),
                                                depth + 1,
                                            ));
                                        }
                                    } else if ft.is_file() {
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
                                                search_in_file_cancellable(&p, opts, &interrupted);
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
        let child_count: usize = results.iter().map(|r| r.children.len()).sum();
        branching = child_count as f64 / results.len() as f64;
        for result in results {
            files += result.files;
            dirs += result.dirs;
            max_depth = max_depth.max(result.depth);
            pending.extend(result.children);
            for m in result.matches {
                if seen_matches.insert(m.path().to_path_buf()) {
                    matches.push(m);
                }
            }
        }
        emit(Event::Telemetry(Snapshot {
            files,
            directories: dirs,
            pending: pending.len(),
            capacity: pending.capacity(),
            depth: max_depth,
            max_depth,
            workers: 0,
            branching,
            elapsed_ms: start.elapsed().as_secs_f64() * 1000.0,
            traversal,
        }));
    }
    if interrupted.load(Ordering::Relaxed) {
        return Err(FsearchError::Interrupted);
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
    use crate::searcher::recursive_find;
    use std::sync::Mutex;
    #[test]
    fn adaptive_matches_baseline_for_topologies_depth_and_content() {
        let root = tempfile::tempdir().unwrap();
        for n in 0..80 {
            let dir = root.path().join(format!("topic{n}/unit/week"));
            fs::create_dir_all(&dir).unwrap();
            fs::write(dir.join("notes.txt"), "algorithm memory\n").unwrap();
        }
        fs::write(root.path().join("root.txt"), "algorithm").unwrap();
        for content in [false, true] {
            for depth in [0, 1, 3, 10] {
                let opts = SearchOptions::builder(if content { "algorithm" } else { "*.txt" })
                    .base_dir(root.path())
                    .include_dirs(false)
                    .search_in_files(content)
                    .max_depth(depth)
                    .build();
                let flag = Arc::new(AtomicBool::new(false));
                let expected = recursive_find(&opts, flag.clone()).unwrap();
                let events = Mutex::new(Vec::new());
                let result =
                    adaptive_find(&opts, flag, 4, &|e| events.lock().unwrap().push(e)).unwrap();
                assert_eq!(
                    expected.iter().map(|m| m.path()).collect::<Vec<_>>(),
                    result.iter().map(|m| m.path()).collect::<Vec<_>>()
                );
                assert!(events
                    .lock()
                    .unwrap()
                    .iter()
                    .any(|e| matches!(e, Event::Telemetry(_))));
            }
        }
    }
    #[test]
    fn cancels_and_rejects_invalid_root() {
        let root = tempfile::tempdir().unwrap();
        let opts = SearchOptions::builder("*").base_dir(root.path()).build();
        assert!(matches!(
            adaptive_find(&opts, Arc::new(AtomicBool::new(true)), 4, &|_| {}),
            Err(FsearchError::Interrupted)
        ));
        let opts = SearchOptions::builder("*")
            .base_dir(root.path().join("missing"))
            .build();
        assert!(adaptive_find(&opts, Arc::new(AtomicBool::new(false)), 4, &|_| {}).is_err());
    }
    #[test]
    fn overlapping_roots_preserve_shallower_depth_and_deduplicate() {
        let root = tempfile::tempdir().unwrap();
        let parent = root.path().join("parent");
        let child = parent.join("a");
        fs::create_dir_all(child.join("b")).unwrap();
        let file = child.join("b/match.txt");
        fs::write(&file, "memory").unwrap();
        let mut roots = vec![child.clone()];
        for i in 0..3 {
            let p = root.path().join(format!("empty{i}"));
            fs::create_dir(&p).unwrap();
            roots.push(p);
        }
        roots.push(parent);
        let opts = SearchOptions::builder("*.txt")
            .base_dirs(roots)
            .include_dirs(false)
            .max_depth(1)
            .build();
        let result = adaptive_find(&opts, Arc::new(AtomicBool::new(false)), 1, &|_| {}).unwrap();
        assert_eq!(result.len(), 1);
        assert_eq!(result[0].path(), file);
    }
    #[test]
    fn active_work_respects_budget_and_cancel_during_traversal() {
        let root = tempfile::tempdir().unwrap();
        for i in 0..60 {
            let p = root.path().join(format!("d{i}"));
            fs::create_dir(&p).unwrap();
            fs::write(p.join("x.txt"), "x").unwrap();
        }
        let opts = SearchOptions::builder("*.txt")
            .base_dir(root.path())
            .max_depth(10)
            .build();
        let max = AtomicUsize::new(0);
        let result = adaptive_find(&opts, Arc::new(AtomicBool::new(false)), 3, &|event| {
            if let Event::Telemetry(s) = event {
                max.fetch_max(s.workers, Ordering::Relaxed);
            }
        })
        .unwrap();
        assert_eq!(result.len(), 60);
        assert!(max.load(Ordering::Relaxed) <= 3);
        let flag = Arc::new(AtomicBool::new(false));
        let copy = flag.clone();
        let result = adaptive_find(&opts, flag, 3, &|event| {
            if matches!(event, Event::Telemetry(_)) {
                copy.store(true, Ordering::Relaxed);
            }
        });
        assert!(matches!(result, Err(FsearchError::Interrupted)));
    }
    #[cfg(unix)]
    #[test]
    fn symlink_loops_and_outside_targets_are_not_followed() {
        use std::os::unix::fs::symlink;
        let root = tempfile::tempdir().unwrap();
        let outside = tempfile::tempdir().unwrap();
        fs::write(root.path().join("inside.txt"), "inside").unwrap();
        fs::write(outside.path().join("outside.txt"), "outside").unwrap();
        symlink(root.path(), root.path().join("loop")).unwrap();
        symlink(outside.path(), root.path().join("external")).unwrap();
        let opts = SearchOptions::builder("*.txt")
            .base_dir(root.path())
            .include_dirs(false)
            .max_depth(100)
            .build();
        assert_eq!(
            adaptive_find(&opts, Arc::new(AtomicBool::new(false)), 4, &|_| {})
                .unwrap()
                .len(),
            1
        );
    }
}
