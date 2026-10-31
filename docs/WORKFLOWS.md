# Fingerprint comparison workflows

Use `FingerprintImage` for a capture and `FingerprintTemplate` for extracted or
converted minutiae. `IFingerprintEngine` provides extraction and matching, and
`Match(probe, gallery)` preserves that order for every combination.

The examples use this namespace:

```csharp
using OpenPhysical.Biometrics.NBIS;
```

## Captured image to captured image

Construct each image from the grayscale pixels returned by your capture or image
decoding code. Supply each capture's dimensions and resolution explicitly:

```csharp
IFingerprintEngine engine = new NbisFingerprintEngine();
var first = new FingerprintImage(firstPixels, firstWidth, firstHeight, dotsPerInch: 500);
var second = new FingerprintImage(secondPixels, secondWidth, secondHeight, dotsPerInch: 500);

int score = engine.Match(first, second);
```

Each image owns a copy of its pixels. You can return a reader's original buffer
to a pool or clear it after construction. Images are immutable and retain their
pixel data for their lifetime. The engine extracts both images and compares the
resulting templates. Higher scores indicate greater similarity.

## Template to template

Extract templates once when reusing captures. Matching existing templates performs
the comparison directly:

```csharp
var firstTemplate = engine.Extract(first);
var secondTemplate = engine.Extract(second);

int score = engine.Match(firstTemplate, secondTemplate);
```

Templates carry image dimensions, immutable minutiae, and provenance. The existing
span overload also accepts tightly packed pixels directly:

```csharp
var template = engine.Extract(gray8Pixels, width, height, dotsPerInch: 500);
```

## Mixed image and template comparisons

Supply the image on either side. Only the image operand needs extraction:

```csharp
int imageToTemplate = engine.Match(first, secondTemplate);
int templateToImage = engine.Match(firstTemplate, second);
```

All overloads return the same raw Bozorth3 score representation. The application
uses its calibrated threshold when making an acceptance decision.

## Fresh capture to PIV card reference

The PIV application verifies the credential and unwraps its raw FMR record. Select
the finger and view explicitly, then compare a capture with that reference:

```csharp
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

var parsed = Incits378Codec.Parse(verifiedFmrBytes);
if (!parsed.IsSuccess)
    throw new FormatException("The fingerprint record has structural errors.");
var reference = parsed.Value!.ToMatchingTemplate(FingerPosition.RightIndex, viewNumber: 0);

int score = engine.Match(first, reference);
```

Keep the reference template for further attempts:

```csharp
var attemptScores = captures
    .Select(capture => engine.Match(capture, reference))
    .ToArray();
```

The [conversion guide](FINGERPRINTS.md#piv-template-conversion) describes resolution,
quality, and parsed-model projection. PIV card views at up to 128 minutiae fit
within the matcher's 150-point selection limit.

## Functional composition with LINQ

Immutable images and templates work naturally with collection expressions, method
groups, and LINQ. Materialize extraction results when you intend to reuse them:

```csharp
using System.Collections.Immutable;

FingerprintImage[] captures = [first, second];
var templates = captures.Select(engine.Extract).ToImmutableArray();
var scores = templates
    .Select(template => engine.Match(template, reference))
    .ToImmutableArray();
```

`Select` describes a projection and executes as the sequence is enumerated.
`ToImmutableArray` runs the extraction pipeline once. Subsequent enumeration uses
the stored templates. These calls execute synchronously. NBIS state is process-global,
so the engine lock admits one native call at a time. Parallel matching requires
separate processes.

Use LINQ to transform captures into templates or templates into scores. Use
`foreach` when processing each pair or performing other side effects. The runnable
examples name the probe and gallery templates explicitly and explain where image
overloads perform extraction, so you can choose the route that suits your workflow.

Minutiae are immutable records. Pattern matching and LINQ are useful for examining
features, for example collecting ridge endings for a visualization:

```csharp
var ridgeEndings = firstTemplate.Minutiae
    .Where(minutia => minutia is { Type: 1 })
    .ToImmutableArray();
```

## Store and reload a template

```csharp
string json = FingerprintTemplateJson.Serialize(firstTemplate);
FingerprintTemplate restored = FingerprintTemplateJson.Deserialize(json);
int score = engine.Match(restored, second);
```

Store the JSON through the application's biometric data storage policy. The
[format guide](FINGERPRINTS.md#json-template-format-version-1) documents the schema.

## Dependency injection

Register the engine with the application's service collection:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddFingerprintBiometrics();
```

Request `IFingerprintEngine` in your constructor. Its image extensions use the
same registered engine and logger:

```csharp
internal sealed class CaptureComparison(IFingerprintEngine engine)
{
    internal int Compare(FingerprintImage capture, FingerprintTemplate reference) =>
        engine.Match(capture, reference);
}
```

## Run the example project

To compare real paired impressions across all five fingerprint classes, use the
attributed [SD04 collection](../tests/fixtures/sd04/README.md):

```sh
python scripts/build-nbis.py
dotnet run --project examples/OpenPhysical.Biometrics.NBIS.Examples -c Release -- --sd04 tests/fixtures/sd04
```

This mode compares ten pairs using all four image/template combinations, LINQ
template reuse, and JSON round trips. The native tests check these images against
independent NBIS reference outputs.

The [example program](../examples/OpenPhysical.Biometrics.NBIS.Examples/Program.cs)
compiles with the solution and runs all four comparison combinations, immutable
LINQ pipelines, and JSON round trips. From the repository root:

```sh
python scripts/build-nbis.py
dotnet run --project examples/OpenPhysical.Biometrics.NBIS.Examples -c Release -- tests/fixtures/g001t2u.gray tests/fixtures/g001t2u.gray 800 768
```

This uses the attributed NIST fixture as both captures and checks that every route
returns the same score. The program reports completion and keeps scores in memory.
The CI runtime matrix runs this example on each platform.

For your own two grayscale captures with the same dimensions, pass their paths
and dimensions. For PIV record preparation and selected-view matching, use the
[OpenPhysical.Biometrics examples](https://github.com/OpenPhysical/OpenPhysical.Biometrics/tree/master/examples).
