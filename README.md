# OpenPhysical.Biometrics.NBIS

**Extract fingerprint features and compare fingerprints from .NET 10**, using NIST's MINDTCT and Bozorth3 algorithms.

This library takes a grayscale fingerprint image and finds its **minutiae**, the points where ridges end or split. It then compares two sets of minutiae and returns a similarity score. OpenPhysical.Biometrics supplies the INCITS 378-2004 record parsing and selected-view conversion used to compare a fresh capture with a reference obtained from a PIV card.

It is a standalone library and command-line tool built around NIST Biometric Image Software (NBIS) 5.0.0, ready to use in your own .NET application or as part of OpenPhysical middleware.

## Built from NIST's reference code

We started with NIST's original C source code for MINDTCT fingerprint extraction and Bozorth3 matching, then adapted it for use in .NET applications. The fingerprint algorithms still run in C. A C# API lets you pass images and templates directly from your application and receive results in the same process.

Our additions handle input validation, memory cleanup, concurrent calls, native library loading, and .NET integration. Build-time adaptations replace process-termination paths with errors the application can handle and suppress native diagnostics that could expose fingerprint details. The original NIST files are preserved unmodified in the first Git commit, so the starting point is clear and later changes can be reviewed against it.

The [provenance record](native/nbis/PROVENANCE.md) documents the source archive, checksums, and adaptations. The [changelog](CHANGELOG.md) records changes to this library.

## Where it fits in PIV

A Personal Identity Verification (PIV) card can hold fingerprint templates for its holder. In an **off-card comparison**, the application obtains the reference template from the card, captures a fresh fingerprint with a reader, and compares the two on the computer.

OpenPhysical.Biometrics.NBIS supplies the extraction and comparison steps:

1. Your application verifies the card and its biometric data, then unwraps the fingerprint record from its credential container.
2. You choose the finger and view to use from that record.
3. The library extracts minutiae from the fresh image and compares them with the selected reference.
4. Your application decides whether the score meets its tested acceptance policy.

**Higher Bozorth3 scores indicate greater fingerprint similarity.** Your application sets an acceptance threshold based on evaluation with its readers, users, and operating conditions.

## Using the library

The library package is named `OpenPhysical.Biometrics.NBIS`. The separate `OpenPhysical.Biometrics.NBIS.Cli` package provides the `nbis` command. Build the packages from this repository using the instructions below.

```csharp
using OpenPhysical.Biometrics.NBIS;

var engine = new NbisFingerprintEngine();

// A fresh capture: tightly packed, top-left-origin, 8-bit grayscale pixels.
var capture = new FingerprintImage(gray8Pixels, width, height, dotsPerInch: 500);

// Extract a reusable reference from another capture.
var reference = engine.Extract(referenceCapture);

int score = engine.Match(capture, reference);
```

Use the same `Match` method for each workflow:

```csharp
int imageScore = engine.Match(firstCapture, secondCapture);

var firstTemplate = engine.Extract(firstCapture);
var secondTemplate = engine.Extract(secondCapture);
int templateScore = engine.Match(firstTemplate, secondTemplate);
int mixedScore = engine.Match(firstTemplate, secondCapture);
int captureScore = engine.Match(firstCapture, secondTemplate);
```

Images own a copy of their pixels, and templates contain immutable minutiae. Extract once when reusing a capture's template across comparisons. LINQ makes it easy to process a sequence of captures:

```csharp
var templates = captures.Select(engine.Extract).ToArray();
var scores = templates.Select(template => engine.Match(template, reference)).ToArray();
```

The [workflow examples](docs/WORKFLOWS.md) cover all four combinations, PIV references, JSON templates, and dependency injection, with a runnable .NET example project. Extraction and matching operate on data in memory.

For dependency injection, call `services.AddFingerprintBiometrics()` and request `IFingerprintEngine`. Logging uses `Microsoft.Extensions.Logging` with your application's configured providers and reports operation status and error codes. NBIS state is process-global, so every engine instance shares one lock. Parallel matching requires separate processes.

### Input requirements

- Images must be **500 ppi**, 8-bit grayscale, with width and height each between 64 and 2048 pixels. Black is 0 and white is 255.
- Decode WSQ, PNG, or JPEG captures to grayscale pixels before calling the library, and supply the image dimensions and resolution explicitly.
- [OpenPhysical.Biometrics.Fingerprints](https://github.com/OpenPhysical/OpenPhysical.Biometrics/blob/master/docs/FINGERPRINT-WORKFLOWS.md) converts an explicitly selected PIV finger and view into a matching template.
- Matching uses NBIS's best-150 minutia selection.

The [API guide](docs/FINGERPRINTS.md) explains coordinates, angle conventions, quality values, exceptions, parsed-model conversion, and native loading. Public APIs include XML documentation for IntelliSense.

## Command-line use

The CLI works with raw grayscale images and the library's JSON template format:

```sh
nbis extract capture.gray8 captured.json --width 800 --height 768 --dpi 500
nbis match captured.json reference.json
```

Matching prints the raw score. Extraction creates a new output file. Store images and template JSON using your application's biometric data protection policy.

## Building from source

Install the **.NET 10 SDK**, Python 3, CMake 3.20 or later, and a C11 compiler. Windows builds use MSYS2 UCRT64 GCC and Ninja. `global.json` keeps SDK selection within .NET 10 while allowing newer stable feature bands.

```sh
python scripts/build-nbis.py
dotnet build OpenPhysical.Biometrics.NBIS.sln -c Release
dotnet test OpenPhysical.Biometrics.NBIS.sln -c Release
```

The native build script selects the current platform and stages its library for the managed projects. Run .NET builds and tests serially.

Native targets are Windows x64, Linux x64/ARM64, and macOS x64/ARM64. Linux uses a glibc 2.35 baseline. The C library targets macOS 12; managed applications run on operating systems supported by [.NET 10](https://learn.microsoft.com/en-us/dotnet/core/install/macos), currently macOS 14 or later. Full release packages require all five native assets. The CI workflow checks each platform and tests package consumption outside the repository.

See the [build and packaging guide](docs/FINGERPRINTS.md) for complete packages, local package checks, and troubleshooting. The [provenance record](native/nbis/PROVENANCE.md) identifies the official NIST source and documents the build adaptations. The initial Git commit preserves the vendored NIST source unmodified.

## Versioning

Releases follow [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html):
`MAJOR.MINOR.PATCH`. From 1.0 onward, breaking changes increase the major version,
compatible features increase the minor version, and compatible fixes increase
the patch version. During initial `0.x` development, breaking changes increase
the minor version and are called out in the [changelog](CHANGELOG.md).
The library and CLI share a release version, independent of NIST's NBIS version.
See the [compatibility policy](CONTRIBUTING.md#versioning-and-compatibility).

## Testing and contributing

µnit checks native algorithm parity and memory-failure handling. NUnit checks managed contracts, serialization, logging, dependency injection, and interop. Shared NIST fixtures avoid duplicating algorithm tests across the two suites.

The [SD04 fixture collection](tests/fixtures/sd04/README.md) provides 20 real images
from ten paired fingerprints spanning all five pattern classes. Run the same
collection through the examples:

```sh
dotnet run --project examples/OpenPhysical.Biometrics.NBIS.Examples -c Release -- --sd04 tests/fixtures/sd04
```

We captured the reference minutiae and all 400 ordered comparison scores with
the original NBIS 5.0.0 tools, built separately from the bindings. Native tests
check the complete collection against those results. A representative .NET test
checks that the same reference fields and score survive the managed/native
boundary, while the remaining managed tests exercise application workflows.
The fixture manifest records the source checksums and reproduction commands.

The [build and test guide](docs/FINGERPRINTS.md#build-and-test) contains the native
test and sanitizer commands. ASan and UBSan checks run on Linux/macOS; Linux also
enables leak detection. Windows runs plain native and managed tests.
CI also retains managed coverage reports and native GCC line/branch reports.
Run `python scripts/measure-native-coverage.py` on a GCC/Linux development host
to collect native coverage locally in its own test build.

Use the pinned **CSharpier** formatter. Projects reference CSharpier.MsBuild 1.3.0: Debug builds format that project, and Release builds reject unformatted files. Public API documentation is required, and missing documentation or other build warnings fail the build.

```sh
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
python scripts/verify-documentation-gate.py
```

Read the [contribution guide](CONTRIBUTING.md), [contributor copyright assignment](CONTRIBUTOR_ASSIGNMENT.md), [Bounded Contribution Policy](CODE_OF_CONDUCT.md), and [API and build conventions](docs/FINGERPRINTS.md) before contributing.

## Security reports

Report suspected vulnerabilities privately to [security@mistial.dev](mailto:security@mistial.dev). See [SECURITY.md](SECURITY.md) for supported versions and reporting details. Use synthetic or anonymized examples when describing a report.

## Licensing

OpenPhysical-authored code is licensed under **GPL version 2 or any later version** (`GPL-2.0-or-later`). See [LICENSE](LICENSE) and [NOTICE](NOTICE). Organizations needing different terms can request a [separately negotiated commercial license](COMMERCIAL-LICENSING.md).

NIST source retains its public-domain notices, and other third-party materials retain their original terms, including µnit's MIT license. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
