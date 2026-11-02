# Changelog

Changes to OpenPhysical.Biometrics.NBIS are recorded here, starting from the
imported NIST NBIS 5.0.0 sources. Work awaiting release belongs under
**Unreleased**. When publishing a release, move those entries under its version
and release date, and keep an Unreleased section for subsequent work.
Releases use [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html);
see the [compatibility policy](CONTRIBUTING.md#versioning-and-compatibility).

## Unreleased

## 1.0.1 - 2026-11-01

- Publish portable symbol packages and normalize CI build paths (#7).

- Fix CodeQL arithmetic and native correctness findings, retaining verifiable
  original NIST source receipts and adding overflow regression coverage.

## 1.0.0 - 2026-11-01

- First stable library and CLI release, with the compatibility contract described
  in CONTRIBUTING.md.
- Publish both packages automatically to the OpenPhysical NuGet organization when
  a GitHub release is published, after all runtime and package-consumer checks pass.
  Authentication uses NuGet trusted publishing without stored API keys.
- Run feature-branch CI on pull requests only, avoiding duplicate push checks.

## 0.1.0-beta.1 - 2026-10-31

### Release review fixes

- Clarify the arena allocation-failure boundary and defensive process-exit
  adaptations, including the exact branches exercised by the failure tests.

- Check every SD04 managed minutia field and independently recorded MINDTCT type, with paired workflow scores tied to the reference oracle (#1).
- Exercise actual Unix output-directory permission failures separately from existing-path CLI failures (#2).
- Maintain supported runtimes, native filenames, and CI runners in one manifest shared by the resolver, build scripts, release checks, and CI (#4).

### Added

- Explicit native fixture coverage checks retain all 360 ordered different-finger
  comparisons, including nonzero non-mate scores, alongside paired impressions.

- CI captures managed line/branch coverage and GCC native coverage from separate
  test builds, alongside the runtime, sanitizer, and package-consumer checks.

- Added a managed interop check against an independently captured SD04 reference
  vector, alongside the complete native extraction and matching oracle suite.

- Expanded the runnable examples with plain-English workflow comments and LINQ
  template reuse that extracts each capture once for repeated comparisons.

- Added 20 attributed SD04 images covering ten paired fingerprints across all five
  pattern classes, lossless raw pixels, verified source hashes, and independent
  NBIS extraction and 400-score comparison baselines.
- Added native SD04 parity checks, managed image/template workflow and concurrency
  coverage, and a runnable `--sd04` example using the same fixture catalog.
- In-memory C ABI and .NET API for MINDTCT V2 extraction and Bozorth3 matching
  of 8-bit grayscale images, with immutable templates and raw similarity scores
  for image/image, image/template, and template/template comparisons.
- `FingerprintImage` captures that keep pixels, dimensions, and resolution together.
- Versioned JSON templates, Microsoft logging and dependency injection, and the
  `nbis` command for extraction and matching. Library logs carry
  operation status and native status codes.
- Serialized native calls, per-call allocation cleanup, and wiping of native
  payload memory. Generated build adaptations leave the vendored NIST sources unchanged.
- Native packages for Windows x64, Linux x64/ARM64, and macOS x64/ARM64 on .NET 10.
- µnit parity and safety tests, NUnit managed tests, sanitizer builds, provenance,
  and the contribution and security policies.
- CSharpier formatting and mandatory public API documentation, with warnings
  treated as errors.

### Changed

- Canonical INCITS 378 parsing, PIV profile validation, and selected-view conversion
  now live in OpenPhysical.Biometrics.Fingerprints, shared with issuance and inspection.
  Use `biotool convert` for FMR-to-JSON conversion and `nbis` for extraction/matching.

### Fixed

- Windows native checks use µnit's in-process invocation, matching its supported command-line options.

- Isolated package consumers and CLI installation use an explicit NuGet configuration, preserving feed URLs across Windows shell environments.

- MINDTCT `alloc_minutiae` and `realloc_minutiae` terminate the process when
  allocation fails. Generated replacements return status errors, free a partial
  `alloc_minutiae` result, and keep the previous `realloc_minutiae` buffer until
  the new allocation succeeds.
- Bozorth `qsort_decreasing` keeps its sort stack for the process lifetime, so a
  failed sort changes the next comparison. The generated sort resets that stack
  on entry.
- NBIS diagnostic prints can include minutia coordinates. Those prints are
  suppressed at compile time, and callers receive status codes.
