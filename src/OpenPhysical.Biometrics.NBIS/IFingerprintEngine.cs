// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

using System.Runtime.InteropServices;

/// <summary>Extracts fingerprint minutiae and compares templates.</summary>
/// <remarks>Implementations consume an explicit pixel resolution and return raw similarity scores.</remarks>
public interface IFingerprintEngine
{
    /// <summary>Extracts MINDTCT V2 minutiae from an uncompressed 8-bit grayscale fingerprint image.</summary>
    /// <param name="pixels">Tightly packed rows from the top left, exactly <paramref name="width"/> times
    /// <paramref name="height"/> bytes. Zero is black and 255 is white. The caller's buffer is not modified.</param>
    /// <param name="width">Image width in pixels, from 64 through 2048 inclusive.</param>
    /// <param name="height">Image height in pixels, from 64 through 2048 inclusive.</param>
    /// <param name="dotsPerInch">Explicit image resolution; must be 500.</param>
    /// <returns>An immutable, nominal 500 ppi template in NIST XYT coordinates. A blank image may produce no minutiae.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is outside its supported range, or the resolution is not 500 ppi.</exception>
    /// <exception cref="ArgumentException">The pixel buffer length does not match the dimensions.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system or process architecture is unsupported.</exception>
    /// <exception cref="DllNotFoundException">The native runtime asset is missing or cannot be loaded.</exception>
    /// <exception cref="BadImageFormatException">The native asset is incompatible with the process.</exception>
    /// <exception cref="EntryPointNotFoundException">The native asset does not expose the required entry points.</exception>
    /// <exception cref="InvalidOperationException">The native ABI is incompatible, extraction fails, or more than 1000 minutiae are detected.</exception>
    /// <remarks>The resulting Y coordinate is image height minus the source row. Angles are degrees counterclockwise
    /// from east, pointing away from ridge endings. NBIS keeps process-global state, so every engine instance
    /// shares one lock. Parallel matching requires separate processes.</remarks>
    /// <example><code>
    /// IFingerprintEngine engine = new NbisFingerprintEngine();
    /// FingerprintTemplate probe = engine.Extract(gray8Pixels, width, height, dotsPerInch: 500);
    /// </code></example>
    FingerprintTemplate Extract(ReadOnlySpan<byte> pixels, int width, int height, int dotsPerInch);

    /// <summary>Compares two nominal 500 ppi templates and returns the raw Bozorth3 similarity score.</summary>
    /// <param name="probe">Validated probe template in NIST XYT coordinates.</param>
    /// <param name="gallery">Validated reference template in NIST XYT coordinates.</param>
    /// <returns>The NBIS similarity score. Fewer than ten minutiae in either template produces zero.</returns>
    /// <exception cref="ArgumentNullException">Either template is <see langword="null"/>.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system or process architecture is unsupported.</exception>
    /// <exception cref="DllNotFoundException">The native runtime asset is missing or cannot be loaded.</exception>
    /// <exception cref="BadImageFormatException">The native asset is incompatible with the process.</exception>
    /// <exception cref="EntryPointNotFoundException">The native asset does not expose the required entry points.</exception>
    /// <exception cref="InvalidOperationException">The native ABI is incompatible or comparison fails.</exception>
    /// <remarks>NBIS retains at most the best 150 minutiae per template using its quality ordering.
    /// Applications calibrate their own thresholds. NBIS keeps process-global state, so every engine
    /// instance shares one lock. Parallel matching uses separate processes. Neither input template is modified.</remarks>
    /// <example><code>
    /// int rawScore = engine.Match(probe, reference);
    /// </code></example>
    int Match(FingerprintTemplate probe, FingerprintTemplate gallery);
}
