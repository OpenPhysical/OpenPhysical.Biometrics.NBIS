# SPDX-License-Identifier: GPL-2.0-or-later
"""Verify corrected source bytes and reconstruct the unchanged NIST receipts."""
import hashlib
import json
from pathlib import Path


def original_bytes(source, relative, corrections):
    data = (source / "upstream" / relative).read_bytes()
    correction = corrections.get(relative)
    if correction is None:
        return data
    if hashlib.sha256(data).hexdigest() != correction["sha256"]:
        raise ValueError(f"Corrected source checksum differs: {relative}")
    lines = data.decode("utf-8").splitlines(keepends=True)
    for hunk in reversed(correction["hunks"]):
        start = hunk["line"]
        end = start + len(hunk["after"])
        if lines[start:end] != hunk["after"]:
            raise ValueError(f"Source correction does not match: {relative}:{start + 1}")
        lines[start:end] = hunk["before"]
    return "".join(lines).encode("utf-8")


def verify(source):
    source = Path(source)
    hashes = json.loads((source / "upstream-sha256.json").read_text())
    corrections = json.loads((source / "source-corrections.json").read_text())
    actual = {p.relative_to(source / "upstream").as_posix()
              for p in (source / "upstream").rglob("*") if p.is_file()}
    if actual != set(hashes) or not set(corrections) <= set(hashes):
        raise ValueError("Source inventory differs from the original manifest.")
    for relative, expected in hashes.items():
        if hashlib.sha256(original_bytes(source, relative, corrections)).hexdigest() != expected:
            raise ValueError(f"Original NIST checksum differs: {relative}")
    print(f"Verified {len(hashes)} NIST receipts and {len(corrections)} corrected files.")


if __name__ == "__main__":
    import sys
    verify(sys.argv[1])
