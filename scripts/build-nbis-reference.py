#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
# Copyright (c) 2026 Mistial Developer
"""Build original NBIS command-line tools independently for fixture generation."""
import argparse
import hashlib
import json
import platform
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ARCHIVE_SHA256 = "0adf8ab0f6b0e4208de50ca00ba21d3d77112ecd66288757ddfed21f6bee92c3"

def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if platform.system() not in ("Darwin", "Linux"):
        parser.error("Generate reference fixtures on macOS or Linux using the original Unix build.")
    if sha256(args.archive) != ARCHIVE_SHA256:
        parser.error("Official NBIS 5.0.0 archive checksum mismatch.")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    with zipfile.ZipFile(args.archive) as archive:
        for name in archive.namelist():
            destination = (output / name).resolve()
            if not destination.is_relative_to(output):
                raise ValueError("Archive member escapes build directory")
        archive.extractall(output)
        original = {entry.filename: hashlib.sha256(archive.read(entry)).hexdigest()
                    for entry in archive.infolist() if entry.filename.endswith((".c", ".h", ".src"))}
    source = output / "Rel_5.0.0"
    install = output / "install"
    install.mkdir()
    compiler = subprocess.check_output(["cc", "--version"], text=True).splitlines()[0]
    endian = "__NBISLE__" if sys.byteorder == "little" else "__NBISBE__"
    flags = f"-O2 -w -ansi -D_POSIX_SOURCE -D{endian} -fPIC"
    # Modern compilers diagnose old NBIS C constructs more strictly; no source is patched.
    flags += " -Wno-error=implicit-function-declaration -Wno-error=int-conversion"
    flags += " -Wno-error=incompatible-function-pointer-types" if "clang" in compiler.lower() else " -Wno-error=incompatible-pointer-types"
    commands = [["sh", "setup.sh", str(install), "--without-X11", "--STDLIBS"],
                ["make", "config", "CC=cc", "CFLAGS=" + flags],
                ["make", "it", "-j4", "CC=cc", "CFLAGS=" + flags]]
    with (output / "build.log").open("w") as log:
        for command in commands:
            subprocess.run(command, cwd=source, check=True, stdout=log, stderr=subprocess.STDOUT)
    for name, expected in original.items():
        if sha256(output / name) != expected:
            raise ValueError(f"Original NBIS source changed during build: {name}")
    tools = {}
    (output / "bin").mkdir()
    for name in ("mindtct", "bozorth3"):
        destination = output / "bin" / name
        shutil.copy2(source / name / "bin" / name, destination)
        tools[name] = {"sha256": sha256(destination), "version": subprocess.check_output([str(destination), "-version"], text=True).strip()}
    receipt = dict(archive_url="https://nigos.nist.gov/nist/nbis/nbis_v5_0_0.zip",
                   archive_sha256=ARCHIVE_SHA256, source_files_verified=len(original),
                   compiler=compiler, cflags=flags, system=platform.system(), machine=platform.machine(),
                   commands=[[part.replace(str(install), "<install>") for part in command] for command in commands],
                   tools=tools)
    (output / "reference-build.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(f"Built original NBIS reference tools; verified {len(original)} unchanged source files.")

if __name__ == "__main__":
    main()
