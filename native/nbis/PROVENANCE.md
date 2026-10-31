# NBIS provenance and build adaptations

Upstream: NIST Biometric Image Software (NBIS), release 5.0.0.

- Official release listing: https://www.nist.gov/itl/iad/btg/products-and-services/image-group-open-source-server-nigos
- Archive: https://nigos.nist.gov/nist/nbis/nbis_v5_0_0.zip
- Archive SHA-256: `0adf8ab0f6b0e4208de50ca00ba21d3d77112ecd66288757ddfed21f6bee92c3`
- Retrieved: 2026-10-31. A browser user-agent was required for the download.
- License/disclaimer: `NOTICE.txt`, also preserved in each original source file.

`upstream/` is an unmodified subset of the official archive. Every file hash is
recorded in `upstream-sha256.json`; the build verifies them before compiling.
Included components are MINDTCT V2, Bozorth3 computation/sorting/drivers/globals,
the Bresenham line utility, and their required headers. The AN2K header template
is copied into the build directory solely to satisfy LFS type declarations.

## Adaptations, separate from upstream

Both the build script and CMake verify the pinned upstream hashes. Each generated
source replacement must match exactly once; a missing or ambiguous pattern fails
configuration before compilation.

`CMakeLists.txt` generates an adapted `minutia.c` in the build directory:

1. `alloc_minutiae` returns -430/-431 instead of calling `exit`; its partially
   allocated structure is freed on the second failure.
2. `realloc_minutiae` preserves its old pointer and allocation count until the
   allocation succeeds, and returns -432 instead of terminating the process.
3. On Windows, the generated timer header uses `time.h` for unused timer
   declarations instead of the unavailable POSIX `sys/times.h`. Timing remains disabled.

`CMakeLists.txt` also generates an adapted `bz_sort.c`. Upstream `qsort_decreasing`
keeps its stack pointer for the process lifetime. The generated file sets
`stack_pointer = stack` on entry. The vendored file stays unchanged.

The -430/-431/-432 returns are defensive generated-source fallbacks that remove
upstream process exits from the linked library. With the forced allocation arena,
`op_malloc` and a failing `op_realloc` unwind immediately and never return NULL.
Those fallback returns and their local cleanup branches are therefore unreachable
in the supported wrapper configuration. Allocation-failure tests exercise the
actual arena boundary and its -10000 engine error; they do not claim coverage of
the unreachable fallback branches.

Every engine allocation is routed through `op_alloc.h` by compiler forced include.
The serialized C call owns an allocation arena. Allocation failure uses `longjmp`
to unwind C frames to the wrapper boundary. The arena reclaims all partially built
objects. Payload bytes are wiped through a volatile `memset` function pointer so
the wipe stays in the generated code. At the end of a call the arena list is detached,
the mutex is released, and the detached blocks are wiped. Frees that occur while
NBIS is still running stay inside the lock. Allocation failure returns status 3
with engine error -10000. Staged runtime builds use the production allocation path.

Upstream Bozorth and MINDTCT state remains process-global. The mutex is required;
separate processes are the parallel execution boundary.

Upstream `fprintf`/`printf` diagnostics are suppressed at compile time because
some paths contain minutia coordinates. Consumers receive status codes only.
`LOG_REPORT`, `TIMER`, and `DEBUG` must remain disabled.

`op_nbis.c` replaces CLI-owned globals and filenames with fixed internal values,
implements the CLI's `bz_prune` preparation in memory, and uses the upstream
quality ordering and XY sorting routines. It retains the default best-150 limit,
ten-minutia minimum, NIST angle normalization, and `bozorth_main` score calculation.
Extraction above 1000 minutiae returns an error. The native ABI exports version,
extraction, and pairwise comparison, and it returns the raw score.

## Validation boundaries

Reference fixture hashes and transformation provenance are under
`tests/fixtures`. Expected XYT fields
and scores come from NIST's test archive.
The CI matrix builds and tests each runtime.
