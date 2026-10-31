// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

/// <summary>Composes extraction and matching for images and reusable templates.</summary>
/// <remarks>These operations use the supplied engine and its logging, loading, and concurrency
/// behavior. Extract a template once when reusing a capture in several comparisons.</remarks>
public static class FingerprintEngineExtensions
{
    /// <summary>Extracts a reusable minutiae template from an immutable capture.</summary>
    /// <param name="engine">Engine performing extraction.</param>
    /// <param name="image">Validated grayscale capture with explicit 500 ppi resolution.</param>
    /// <returns>An immutable template in NIST XYT coordinates.</returns>
    /// <exception cref="ArgumentNullException">The engine or image is null.</exception>
    /// <exception cref="InvalidOperationException">The engine cannot complete extraction.</exception>
    /// <remarks>Native loading exceptions from the engine propagate to the caller.
    /// The image remains unchanged and can be reused after extraction.</remarks>
    /// <example><code>
    /// FingerprintTemplate template = engine.Extract(capture);
    /// </code></example>
    public static FingerprintTemplate Extract(
        this IFingerprintEngine engine,
        FingerprintImage image
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(image);
        return engine.Extract(image.Pixels, image.Width, image.Height, image.DotsPerInch);
    }

    /// <summary>Extracts both captures and compares their templates in probe/gallery order.</summary>
    /// <param name="engine">Engine performing extraction and matching.</param>
    /// <param name="probe">Grayscale probe capture.</param>
    /// <param name="gallery">Grayscale reference capture.</param>
    /// <returns>The raw Bozorth3 similarity score for the two extracted templates.</returns>
    /// <exception cref="ArgumentNullException">The engine or either image is null.</exception>
    /// <exception cref="InvalidOperationException">Extraction or matching fails.</exception>
    /// <remarks>Each image is extracted once per call. Engine loading exceptions propagate.
    /// Higher scores indicate greater similarity; the application chooses its acceptance threshold.</remarks>
    /// <example><code>
    /// int score = engine.Match(firstCapture, secondCapture);
    /// </code></example>
    public static int Match(
        this IFingerprintEngine engine,
        FingerprintImage probe,
        FingerprintImage gallery
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(gallery);
        return engine.Match(engine.Extract(probe), engine.Extract(gallery));
    }

    /// <summary>Extracts a probe capture and compares it with a reusable reference template.</summary>
    /// <param name="engine">Engine performing extraction and matching.</param>
    /// <param name="probe">Grayscale probe capture.</param>
    /// <param name="gallery">Reference template, including a converted PIV card view.</param>
    /// <returns>The raw Bozorth3 similarity score in probe/gallery order.</returns>
    /// <exception cref="ArgumentNullException">The engine, image, or template is null.</exception>
    /// <exception cref="InvalidOperationException">Extraction or matching fails.</exception>
    /// <remarks>The reference template is reused as supplied. Engine loading exceptions propagate.
    /// The application chooses its acceptance threshold.</remarks>
    /// <example><code>
    /// int score = engine.Match(capture, cardTemplate);
    /// </code></example>
    public static int Match(
        this IFingerprintEngine engine,
        FingerprintImage probe,
        FingerprintTemplate gallery
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(gallery);
        return engine.Match(engine.Extract(probe), gallery);
    }

    /// <summary>Compares a reusable probe template with an extracted reference capture.</summary>
    /// <param name="engine">Engine performing extraction and matching.</param>
    /// <param name="probe">Probe template in NIST XYT coordinates.</param>
    /// <param name="gallery">Grayscale reference capture.</param>
    /// <returns>The raw Bozorth3 similarity score in probe/gallery order.</returns>
    /// <exception cref="ArgumentNullException">The engine, template, or image is null.</exception>
    /// <exception cref="InvalidOperationException">Extraction or matching fails.</exception>
    /// <remarks>The probe template is reused as supplied. Engine loading exceptions propagate.
    /// The application chooses its acceptance threshold.</remarks>
    /// <example><code>
    /// int score = engine.Match(savedTemplate, capture);
    /// </code></example>
    public static int Match(
        this IFingerprintEngine engine,
        FingerprintTemplate probe,
        FingerprintImage gallery
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(gallery);
        return engine.Match(probe, engine.Extract(gallery));
    }
}
