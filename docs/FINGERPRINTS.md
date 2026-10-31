# OpenPhysical.Biometrics.NBIS API and build guide

MINDTCT V2 extraction and Bozorth3 matching ship in the
`OpenPhysical.Biometrics.NBIS` repository and NuGet package. The C library can
also be built and used on its own.

## Build and test

Requirements: .NET 10 SDK, Python 3, CMake 3.20+, and a C11 compiler. Windows builds
use MSYS2 UCRT64 GCC and Ninja on PATH. No system NBIS installation is required.

```sh
python scripts/build-nbis.py
dotnet test tests/OpenPhysical.Biometrics.NBIS.Tests -c Release
python scripts/build-nbis.py --sanitizer asan
python scripts/build-nbis.py --sanitizer ubsan
ctest --test-dir artifacts/native/osx-arm64-asan --output-on-failure
ctest --test-dir artifacts/native/osx-arm64-ubsan --output-on-failure
```

Replace the sanitizer directory with the current RID. Address and undefined
behavior sanitizers run separately on macOS/Linux. µnit owns NIST algorithm parity,
native input validation, noisy-image safety, and allocation-failure recovery. NUnit
owns managed contracts, serialization, logging/DI, concurrency across the managed
boundary, and marshaling. Linux ASan builds enable leak detection. Sanitized assets stay out of runtime packaging.

Native assets are staged under `artifacts/runtimes/<RID>/native/`. Supported build
targets, native filenames, and CI runners come from the [canonical runtime manifest](../runtimes.props). The resolver, build scripts, package checks, and CI all read this manifest. Linux CI uses
Ubuntu 22.04, defining a glibc 2.35 baseline. macOS builds target macOS 12;
managed applications require macOS 14 or later for .NET 10 and remain subject to
Microsoft's current OS support policy. Windows uses the Universal C Runtime.
CI runs the runtime matrix on each supported platform. A local build checks the
current host.

## Library API

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.NBIS;

services.AddFingerprintBiometrics(); // Registers IFingerprintEngine lazily.

IFingerprintEngine engine = new NbisFingerprintEngine();
var capture = new FingerprintImage(gray8Pixels, width, height, dotsPerInch: 500);
FingerprintTemplate probe = engine.Extract(capture);
int rawScore = engine.Match(probe, referenceTemplate);
```

`FingerprintImage` validates and copies the supplied buffer, keeping its pixels,
dimensions, and resolution together in an immutable capture. `Match` accepts any
image/template combination and preserves probe/gallery order. Image operands are
extracted for that call; template operands are reused directly. Extension methods
make these operations available on `IFingerprintEngine` and `NbisFingerprintEngine`.
The span-based extraction overload remains available for direct buffer processing.
See [workflow examples](WORKFLOWS.md) for compiled examples and LINQ composition.

Input pixels are tightly packed, row-major, top-left-origin, 8-bit grayscale,
black 0 and white 255. Dimensions must be 64 through 2048 inclusive. Supply 500 ppi
explicitly. Decode WSQ, PNG, or JPEG and resample before calling the engine, and
pass the resulting dimensions and resolution. A blank image can return an empty
template. Extraction above 1000 minutiae returns an error.

Templates are immutable. XYT coordinates use `x = sourceX`, `y = imageHeight - sourceY`,
matching NIST output, including its height rather than height-minus-one convention.
Angles are degrees counterclockwise from east, pointing away from ridge endings.
Reliability is rounded to 0 through 100. Minutia type is retained on the template.
Bozorth3 scores coordinates, angle, and quality, and it selects at most the best
150 minutiae using NBIS ordering. Fewer than ten points in either template produces
score zero. Applications calibrate thresholds against their own populations, sensors,
and operating conditions.

## PIV template conversion

[OpenPhysical.Biometrics.Fingerprints](https://github.com/OpenPhysical/OpenPhysical.Biometrics/blob/master/docs/FINGERPRINT-WORKFLOWS.md)
owns INCITS 378 parsing, PIV profile validation, and conversion of a selected
finger/view into this library's `FingerprintTemplate`. This keeps the format
interpretation shared with enrollment, inspection, and middleware.

```csharp
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

var parsed = Incits378Codec.Parse(verifiedFmrBytes);
if (!parsed.IsSuccess)
    throw new FormatException("The fingerprint record has structural errors.");
var reference = parsed.Value!.ToMatchingTemplate(FingerPosition.RightIndex, viewNumber: 0);
int score = engine.Match(probe, reference);
```

Supply the original raw FMR record after your application's authentication step.
The adapter preserves the record's explicit resolution, coordinate conventions,
angle units, minutia types, and quality semantics. Its public documentation and
[workflow guide](https://github.com/OpenPhysical/OpenPhysical.Biometrics/blob/master/docs/FINGERPRINT-WORKFLOWS.md)
explain supported units and selected-view validation.

NIST's C source supplies XYT coordinate helpers used by extraction. The canonical
binary record parser and .NET record conversion belong to OpenPhysical.Biometrics.

## Command line

The standalone `nbis` tool uses explicit raw grayscale input and versioned JSON:

```sh
nbis extract capture.gray8 probe.json --width 800 --height 768 --dpi 500
nbis match probe.json reference.json
```

Output files must not already exist. Matching prints the raw score. Commands
return nonzero on invalid input or a missing native asset. Library exceptions
describe input constraints or native status codes.

`FingerprintTemplateJson` writes schema version 1 documents containing explicit
500 ppi resolution, dimensions, provenance, and minutiae. Readers validate all fields
and reject unsupported versions and resolutions. JSON templates are biometric data.

### JSON template format, version 1

`FingerprintTemplateJson.Serialize` produces UTF-8-compatible JSON text with these
case-sensitive, PascalCase property names. The CLI reads strict UTF-8 and writes
UTF-8 files. This example contains one point:

```json
{
  "SchemaVersion": 1,
  "Width": 800,
  "Height": 768,
  "DotsPerInch": 500,
  "Provenance": "NBIS 5.0.0 MINDTCT V2",
  "Minutiae": [
    { "X": 20, "Y": 600, "Angle": 180, "Quality": 75, "Type": 1 }
  ]
}
```

`SchemaVersion` is 1 and `DotsPerInch` is 500. Width and height are integers from
1 through 2048. `Provenance` is a nonblank algorithm/conversion description.
`Minutiae` is an array of zero through 1000 points. Every point uses integer values:

- `X`: zero through width minus one.
- `Y`: one through height, using the NBIS bottom-left convention described above.
- `Angle`: zero through 359 degrees counterclockwise from east, away from ridge endings.
- `Quality`: zero through 100. Extracted values are rounded MINDTCT reliability;
  converted INCITS zero retains the unreported-quality meaning.
- `Type`: 0 for other or unknown, 1 for ridge ending, or 2 for bifurcation. The writer
  emits it for every point; the reader defaults an omitted type to 0. INCITS projection
  retains the record's type bits, and MINDTCT extraction retains the detected type.

The reader rejects unknown property names, unsupported schema/resolution values,
and invalid template values. The managed string reader accepts at most 1,000,000
characters; CLI input files are bounded to 1,000,000 bytes.

## Packaging

Build each runtime on its corresponding native runner. Release packing requires
all five runtime assets; CI collects them as `nbis-all-runtimes` and creates
complete library and CLI packages as `nbis-packages`. Publication is a separate action.
The library package includes its XML documentation and NIST notice. Runtime
binaries contain the NBIS wrapper.

```sh
dotnet build OpenPhysical.Biometrics.NBIS.sln -c Release
dotnet pack src/OpenPhysical.Biometrics.NBIS -c Release
```

Local package validation may deliberately use just the current host's runtime:

```sh
dotnet pack src/OpenPhysical.Biometrics.NBIS -c Release -p:AllowPartialNbisRuntimes=true
python scripts/test-nbis-package.py
```

The partial-runtime override is only for local verification. The smoke test restores
into an isolated temporary cache, runs a consumer outside this checkout, then checks
missing-native-library behavior in a fresh process. No workspace project reference
can satisfy that consumer accidentally.

## Logging

The wrapper accepts `ILogger<NbisFingerprintEngine>` from
`Microsoft.Extensions.Logging.Abstractions`, defaulting to `NullLogger`. Standard
Microsoft dependency injection registration uses the application's logger when one
is registered. No logging provider is installed by the library.

Debug events 1000/1001 report extraction/matching completion. Error events 1200,
1201, and 1202 report initialization, extraction, and matching failures. Only native
status and error codes are the structured values. Log filtering and destinations
belong to the hosting application.

## Memory, concurrency, and troubleshooting

MINDTCT and Bozorth3 keep their working state in process-wide variables, including
Bozorth's tables and its quality-sort stack. Every managed engine instance therefore
shares one native lock. A second thread waits for that lock. Concurrent matching
uses one process per worker.
The quality sort resets that stack at the start of every sort, so an earlier failed
sort cannot change the next comparison.

No heap pointers escape the C ABI. Native payload memory is wiped with a compiler-visible
`memset`. The call detaches its allocation list and releases the lock before wiping
allocations that are no longer visible to another call. Individual frees that NBIS
makes while it is still running stay inside the lock, using the same wipe. Managed
transient pixel and native buffers are cleared in `finally`. Immutable caller-owned
templates and managed JSON strings remain the caller's responsibility. Source, native ABI, error codes, and
allocation adaptations are documented in `native/nbis/PROVENANCE.md` and `op_nbis.h`.

A missing native library reports the required RID; stage/build its native asset and
copy it with the application, then restart the process to retry initialization. Unsupported architectures report PlatformNotSupportedException.
ABI mismatch requires rebuilding matched managed/native assets. Unsupported resolutions
must be handled explicitly before entering the engine.

## Public API documentation and IntelliSense

Use the pinned CSharpier tool for managed source and project XML. Restore it with
`dotnet tool restore`, format with `dotnet csharpier format .`, and verify with
`dotnet csharpier check .`. Every project also references CSharpier.MsBuild 1.3.0
from `Directory.Build.props`: a Debug build formats that project, and a Release
build fails when it is unformatted. The CLI check covers repository-root project
XML. CSharpier formats managed C# and project XML.

Write XML comments with each public API change. The compiler and pinned StyleCop
documentation analyzers enforce meaningful summaries, parameter/type-parameter
text, and return descriptions. Malformed XML and unresolved `cref` references also
fail builds. All warnings are errors. The build rejects disabled analyzers, disabled
XML generation, and suppressed or downgraded documentation diagnostics. The checks
also run during normal IDE builds; no Python step is required to compile the library.

Concrete engine methods contain full documentation so packaged IntelliSense does
not depend on a consumer resolving `inheritdoc`. Tooltips describe pixel layout,
units, coordinate conventions, native-loading failures, concurrency, exceptions,
and usage examples. The package verification script checks that XML documentation
is present alongside the DLL, every emitted member has a nonempty summary, and
method parameters have descriptions.

`python scripts/verify-documentation-gate.py` performs positive and deliberately
failing builds to prove the gate works. These are build-policy checks, separate from
µnit algorithm tests and NUnit behavior tests.
