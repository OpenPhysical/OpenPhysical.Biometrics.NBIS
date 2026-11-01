#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Check that release packages carry matching portable symbols and mapped paths."""
import sys
import zipfile
from pathlib import Path


def verify(directory):
    packages = sorted(Path(directory).glob("*.nupkg"))
    if not packages:
        raise ValueError("No NuGet packages found.")
    for package in packages:
        symbols = package.with_suffix(".snupkg")
        with zipfile.ZipFile(package) as binary, zipfile.ZipFile(symbols) as debug:
            pdbs = {name: debug.read(name) for name in debug.namelist() if name.endswith(".pdb")}
            if not pdbs or any(not data.startswith(b"BSJB") for data in pdbs.values()):
                raise ValueError(f"Missing portable PDBs: {symbols.name}")
            for name in pdbs:
                dll = name[:-4] + ".dll"
                data = binary.read(dll)
                offset = data.find(b"RSDS")
                if offset < 0:
                    raise ValueError(f"Missing CodeView record: {dll}")
                path = data[offset + 24:].split(b"\0", 1)[0]
                if not path.startswith(b"/_/"):
                    raise ValueError(f"Unmapped PDB path in {dll}: {path!r}")
        print(f"Verified symbols and mapped build paths: {package.name}")


if __name__ == "__main__":
    verify(sys.argv[1] if len(sys.argv) > 1 else "artifacts/packages")
