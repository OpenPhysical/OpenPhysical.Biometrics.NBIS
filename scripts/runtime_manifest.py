#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Read the same runtime manifest used by MSBuild and the managed resolver."""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def load_runtimes():
    """Return validated runtime entries without maintaining a second RID list."""
    entries = [{"rid": item.attrib["Include"], "file": item.findtext("NativeFile"),
                "os": item.findtext("Runner")} for item in ET.parse(ROOT / "runtimes.props").findall(".//NbisRuntime")]
    if not entries or len({entry["rid"] for entry in entries}) != len(entries):
        raise ValueError("Runtime manifest must contain unique runtime identifiers.")
    if any(not entry["os"] or not entry["file"] or Path(entry["file"]).name != entry["file"] for entry in entries):
        raise ValueError("Every runtime must specify a runner and a native filename.")
    return entries


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--matrix", action="store_true", help="Emit the GitHub Actions include matrix.")
    parser.add_argument("--verify-assets", type=Path, help="Require the complete staged runtime collection.")
    args = parser.parse_args()
    entries = load_runtimes()
    if args.verify_assets:
        for entry in entries:
            path = args.verify_assets / entry["rid"] / "native" / entry["file"]
            if not path.is_file():
                raise SystemExit(f"Missing NBIS runtime asset: {path}")
        print(f"Verified {len(entries)} staged NBIS runtimes.")
    else:
        print(json.dumps({"include": [{"os": e["os"], "rid": e["rid"]} for e in entries]}, separators=(",", ":")))


if __name__ == "__main__":
    main()
