// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NBIS.Examples;

if (args.Length != 4 && args is not ["--sd04", _])
{
    Console.WriteLine("Examples <first.gray8> <second.gray8> <width> <height>");
    Console.WriteLine("Examples --sd04 <fixture-directory>");
    return 2;
}

try
{
    if (args is ["--sd04", var fixtureDirectory])
        return Sd04Examples.Run(fixtureDirectory);
    // Both files contain tightly packed, 8-bit grayscale pixels at 500 ppi.
    // This example accepts one width and height for captures of the same size.
    var width = int.Parse(args[2], CultureInfo.InvariantCulture);
    var height = int.Parse(args[3], CultureInfo.InvariantCulture);
    var firstCapture = LoadCapture(args[0], width, height);
    var secondCapture = LoadCapture(args[1], width, height);
    IFingerprintEngine engine = new NbisFingerprintEngine();

    // Use the image overload for a single comparison. It extracts both captures
    // before matching their minutiae, so callers can start with reader output.
    int imageToImage = engine.Match(firstCapture, secondCapture);

    // For repeated comparisons, extract each capture once and keep its template.
    // Select describes the work; ToImmutableArray executes it now and stores the
    // results. Enumerating templates later reuses those results.
    FingerprintImage[] captures = [firstCapture, secondCapture];
    var templates = captures.Select(engine.Extract).ToImmutableArray();
    var firstTemplate = templates[0];
    var secondTemplate = templates[1];
    int templateToTemplate = engine.Match(firstTemplate, secondTemplate);
    // Mixed overloads extract only the image operand. Keep the first capture
    // as the probe and the second as the gallery in every comparison.
    int imageToTemplate = engine.Match(firstCapture, secondTemplate);
    int templateToImage = engine.Match(firstTemplate, secondCapture);

    // Compare the stored templates with a shared reference. Materializing the
    // scores executes the comparisons once, ready for application policy to use.
    var scores = templates
        .Select(template => engine.Match(template, secondTemplate))
        .ToImmutableArray();

    // JSON preserves a template for later reuse. Here it stays in memory; an
    // application would store it according to its biometric data policy.
    var json = FingerprintTemplateJson.Serialize(firstTemplate);
    var restoredTemplate = FingerprintTemplateJson.Deserialize(json);
    int restoredScore = engine.Match(restoredTemplate, secondTemplate);

    // These routes compare the same ordered pair, so their scores should agree.
    // Any stops at the first discrepancy. This checks workflow consistency;
    // the application chooses its calibrated acceptance threshold separately.
    if (
        new[]
        {
            templateToTemplate,
            imageToTemplate,
            templateToImage,
            restoredScore,
            scores[0],
        }.Any(score => score != imageToImage)
    )
        throw new InvalidOperationException("Workflow results differed.");

    Console.WriteLine("Image, template, JSON, and LINQ workflows completed successfully.");
    return 0;
}
catch (Exception error)
    when (error
            is ArgumentException
                or FormatException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or InvalidOperationException
                or DllNotFoundException
                or BadImageFormatException
                or EntryPointNotFoundException
                or OverflowException
                or System.Text.Json.JsonException
    )
{
    // Report an actionable status while keeping paths and biometric data private.
    Console.Error.WriteLine(
        "Example failed. Check capture dimensions, input format, and native runtime installation."
    );
    return 1;
}

static FingerprintImage LoadCapture(string path, int width, int height)
{
    var pixels = File.ReadAllBytes(path);
    try
    {
        return new FingerprintImage(pixels, width, height, dotsPerInch: 500);
    }
    finally
    {
        // FingerprintImage owns a copy. Clear the temporary file buffer once
        // construction completes, including when validation fails.
        CryptographicOperations.ZeroMemory(pixels);
    }
}
