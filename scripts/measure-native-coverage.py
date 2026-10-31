#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Run the complete native suite with GCC coverage in an isolated test build."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "artifacts/native/coverage"
REPORT = ROOT / "artifacts/coverage"
REPORT.mkdir(parents=True, exist_ok=True)
subprocess.run(["cmake", "-S", str(ROOT / "native/nbis"), "-B", str(BUILD), "-DCMAKE_C_COMPILER=gcc", "-DCMAKE_BUILD_TYPE=Debug", "-DBUILD_TESTING=ON", "-DCMAKE_C_FLAGS=--coverage", "-DCMAKE_SHARED_LINKER_FLAGS=--coverage", "-DCMAKE_EXE_LINKER_FLAGS=--coverage"], check=True)
subprocess.run(["cmake", "--build", str(BUILD), "--parallel", "4"], check=True)
# Clear previous counters so rerunning this command measures this execution alone.
for path in BUILD.rglob("*.gcda"):
    path.unlink()
subprocess.run(["ctest", "--test-dir", str(BUILD), "--output-on-failure"], check=True)
counts = sorted((BUILD / "CMakeFiles/openphysical_nbis.dir").rglob("*.gcda"))
if not counts:
    raise SystemExit("The native suite produced no coverage counters.")
for path in counts:
    subprocess.run(["gcov", "--branch-probabilities", "--branch-counts", "--json-format", str(path)], cwd=REPORT, check=True)
print("Native coverage reports captured.")
