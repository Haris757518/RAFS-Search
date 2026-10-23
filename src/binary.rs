// File: src\binary.rs
// Original author: Hadi Cahyadi <cumulus13@gmail.com>
// Date: 2026-05-11
// Description:
// License: MIT

use std::fs::File;
use std::io::Read;
use std::path::Path;

/// Returns `true` when the file looks like binary content.
///
/// The heuristic reads the first `check_bytes` bytes of the file and checks:
/// * whether a null byte (`\0`) is present — classic binary signal
/// * whether the bytes fail UTF-8 decoding
///
/// Any IO error is treated conservatively as "binary" so the file is skipped.
pub fn is_binary(path: &Path, check_bytes: usize) -> bool {
    match probe(path, check_bytes) {
        Ok(value) => value,
        Err(error) => {
            crate::searcher::warn_io(path, &error);
            true
        }
    }
}

fn probe(path: &Path, check_bytes: usize) -> std::io::Result<bool> {
    let mut file = File::open(path)?;

    let mut buf = vec![0u8; check_bytes];
    let n = file.read(&mut buf)?;

    let chunk = &buf[..n];

    // Null byte → almost certainly binary
    if chunk.contains(&0u8) {
        return Ok(true);
    }

    // Non-UTF-8 sequence → treat as binary
    // A fixed-size probe can end inside a valid multi-byte character.
    Ok(match std::str::from_utf8(chunk) {
        Ok(_) => false,
        Err(error) => error.error_len().is_some() || n < check_bytes,
    })
}

#[cfg(test)]
mod tests {
    #[test]
    fn probe_boundary_does_not_reject_valid_utf8() {
        let root = tempfile::tempdir().unwrap();
        let path = root.path().join("unicode.txt");
        std::fs::write(&path, "aα text").unwrap();
        assert!(!super::is_binary(&path, 2));
        std::fs::write(&path, [b'a', 0xce]).unwrap();
        assert!(super::is_binary(&path, 3));
        std::fs::write(&path, [b'a', 0xff, b'b']).unwrap();
        assert!(super::is_binary(&path, 2));
    }
}
