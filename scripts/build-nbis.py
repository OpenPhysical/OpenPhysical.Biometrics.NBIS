#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
# Copyright (c) 2026 Mistial Developer

"""Build and stage one NBIS runtime from the pinned source tree."""
import argparse
import hashlib
import json
import platform
import subprocess
from pathlib import Path

from runtime_manifest import load_runtimes

ROOT = Path(__file__).resolve().parents[1]
RUNTIMES = load_runtimes()
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--rid", choices=[entry["rid"] for entry in RUNTIMES])
parser.add_argument("--sanitizer", choices=["asan", "ubsan", "both"])
parser.add_argument("--tests", action="store_true", help="Build µnit tests without sanitizers; never stage test-hook binaries")
args = parser.parse_args()
os_name = {"Darwin": "osx", "Linux": "linux", "Windows": "win"}.get(platform.system(), "unsupported")
arch = {"arm64": "arm64", "aarch64": "arm64", "x86_64": "x64", "AMD64": "x64"}.get(platform.machine(), "unsupported")
rid = args.rid or f"{os_name}-{arch}"
if rid not in {entry["rid"] for entry in RUNTIMES}:
    parser.error(f"Unsupported NBIS runtime: {rid}; see runtimes.props.")
if not rid.startswith(os_name + "-") or (rid != f"{os_name}-{arch}" and os_name != "osx"):
    parser.error("Use a native runner for the requested runtime; macOS alone supports cross-building the other architecture.")
source = ROOT / "native/nbis"
for name, expected in json.loads((source / "upstream-sha256.json").read_text()).items():
    if hashlib.sha256((source / "upstream" / name).read_bytes()).hexdigest() != expected:
        raise SystemExit(f"Upstream source checksum mismatch: {name}")
if args.sanitizer or args.tests:
    munit = ROOT / "tests/native/munit"
    for name, expected in json.loads((munit / "provenance.json").read_text())["sha256"].items():
        if hashlib.sha256((munit / name).read_bytes()).hexdigest() != expected:
            raise SystemExit(f"µnit source checksum mismatch: {name}")
build = ROOT / "artifacts/native" / (rid + ("-" + args.sanitizer if args.sanitizer else "-tests" if args.tests else ""))
configure = ["cmake", "-S", str(source), "-B", str(build), "-DCMAKE_BUILD_TYPE=Release", "-DBUILD_TESTING=" + ("ON" if (args.sanitizer or args.tests) else "OFF")]
if os_name == "osx":
    configure += ["-DCMAKE_OSX_ARCHITECTURES=" + ("arm64" if rid.endswith("arm64") else "x86_64"), "-DCMAKE_OSX_DEPLOYMENT_TARGET=12.0"]
if os_name == "win":
    configure += ["-G", "Ninja", "-DCMAKE_C_COMPILER=gcc", "-DCMAKE_SHARED_LINKER_FLAGS=-static-libgcc"]
if args.sanitizer:
    configure += ["-DOP_NBIS_SANITIZERS=" + {"asan": "address", "ubsan": "undefined", "both": "address,undefined"}[args.sanitizer]]
subprocess.run(configure, check=True)
subprocess.run(["cmake", "--build", str(build), "--config", "Release", "--parallel", "4"], check=True)
# Sanitized binaries are test artifacts, never release/runtime assets.
if not (args.sanitizer or args.tests):
    subprocess.run(["cmake", "--install", str(build), "--config", "Release", "--prefix", str(ROOT / "artifacts/runtimes" / rid / "native")], check=True)
print(f"Built {rid}")
