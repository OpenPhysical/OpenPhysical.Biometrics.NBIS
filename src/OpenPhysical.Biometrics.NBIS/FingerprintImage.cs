// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

/// <summary>An immutable grayscale fingerprint capture with explicit dimensions and resolution.</summary>
/// <remarks>The image owns a copy of the supplied pixels. It can be reused across comparisons
/// and shared between callers. Pixel data remains biometric information for the lifetime of the image.</remarks>
public sealed class FingerprintImage
{
    private readonly byte[] pixels;

    /// <summary>Copies and validates a tightly packed 500 ppi grayscale image.</summary>
    /// <param name="pixels">Top-left-origin, row-major grayscale bytes, exactly width times height.
    /// Zero is black and 255 is white. The supplied buffer remains caller-owned.</param>
    /// <param name="width">Image width from 64 through 2048 pixels inclusive.</param>
    /// <param name="height">Image height from 64 through 2048 pixels inclusive.</param>
    /// <param name="dotsPerInch">Explicit capture resolution, 500 pixels per inch.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension or resolution is outside the supported range.</exception>
    /// <exception cref="ArgumentException">Pixel count differs from width times height.</exception>
    public FingerprintImage(ReadOnlySpan<byte> pixels, int width, int height, int dotsPerInch)
    {
        Validate(pixels, width, height, dotsPerInch);
        this.pixels = pixels.ToArray();
        this.Width = width;
        this.Height = height;
        this.DotsPerInch = dotsPerInch;
    }

    /// <summary>Gets a read-only view of the owned grayscale pixels.</summary>
    /// <value>Tightly packed rows starting at the top-left pixel.</value>
    public ReadOnlySpan<byte> Pixels => this.pixels;

    /// <summary>Gets the capture width.</summary>
    /// <value>Width in pixels, from 64 through 2048 inclusive.</value>
    public int Width { get; }

    /// <summary>Gets the capture height.</summary>
    /// <value>Height in pixels, from 64 through 2048 inclusive.</value>
    public int Height { get; }

    /// <summary>Gets the explicit capture resolution.</summary>
    /// <value>500 pixels per inch.</value>
    public int DotsPerInch { get; }

    internal static void Validate(ReadOnlySpan<byte> pixels, int width, int height, int dotsPerInch)
    {
        if (width is < 64 or > 2048)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Width must be between 64 and 2048."
            );
        if (height is < 64 or > 2048)
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "Height must be between 64 and 2048."
            );
        if (dotsPerInch != 500)
            throw new ArgumentOutOfRangeException(
                nameof(dotsPerInch),
                "Capture resolution must be 500 ppi."
            );
        if (pixels.Length != width * height)
            throw new ArgumentException(
                "Expected tightly packed width times height grayscale pixels.",
                nameof(pixels)
            );
    }
}
