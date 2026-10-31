// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

using System.Collections.Immutable;

/// <summary>An immutable, validated NIST XYT template. This is not an INCITS 378 encoded record.</summary>
public sealed class FingerprintTemplate
{
    /// <summary>Creates a nominal 500 ppi template with explicit image dimensions and provenance.</summary>
    /// <param name="width">Original image width, 1 to 2048 pixels.</param>
    /// <param name="height">Original image height, 1 to 2048 pixels.</param>
    /// <param name="minutiae">At most 1000 minutiae; matching selects the best 150 using NBIS ordering.</param>
    /// <param name="provenance">Non-sensitive description of the extraction or conversion algorithm.</param>
    /// <exception cref="ArgumentNullException"><paramref name="minutiae"/> or <paramref name="provenance"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is outside 1 through 2048 inclusive.</exception>
    /// <exception cref="ArgumentException">Provenance is blank, more than 1000 points are supplied, or a point has invalid coordinates, angle, quality, or type.</exception>
    /// <remarks>The sequence is copied into immutable storage and validated. Minutia X must be less than the width;
    /// Y ranges from 1 through the height, matching the NBIS height-minus-row convention. Coordinates are not rescaled.</remarks>
    public FingerprintTemplate(
        int width,
        int height,
        IEnumerable<FingerprintMinutia> minutiae,
        string provenance
    )
    {
        ArgumentNullException.ThrowIfNull(minutiae);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (width is < 1 or > 2048)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Dimensions must be between 1 and 2048."
            );
        if (height is < 1 or > 2048)
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "Height must be between 1 and 2048."
            );
        var points = minutiae.Take(1001).ToImmutableArray();
        if (points.Length > 1000)
            throw new ArgumentException("At most 1000 minutiae are supported.", nameof(minutiae));
        foreach (var m in points)
            if (
                m is null
                || m.X < 0
                || m.X >= width
                || m.Y < 1
                || m.Y > height
                || m.Angle is < 0 or >= 360
                || m.Quality is < 0 or > 100
                || m.Type is < 0 or > 2
            )
                throw new ArgumentException(
                    "Minutia coordinates, angle, quality, or type are invalid.",
                    nameof(minutiae)
                );
        this.Width = width;
        this.Height = height;
        this.Minutiae = points;
        this.Provenance = provenance;
    }

    /// <summary>Gets the image width in pixels.</summary>
    /// <value>The original image width, from 1 through 2048 inclusive.</value>
    public int Width { get; }

    /// <summary>Gets the image height in pixels.</summary>
    /// <value>The original image height, from 1 through 2048 inclusive.</value>
    public int Height { get; }

    /// <summary>Gets the nominal resolution. Other resolutions require an explicit external conversion.</summary>
    /// <value>Always 500 pixels per inch.</value>
    public int DotsPerInch => 500;

    /// <summary>Gets the immutable minutiae.</summary>
    /// <value>Zero through 1000 validated points. An empty collection is permitted.</value>
    public ImmutableArray<FingerprintMinutia> Minutiae { get; }

    /// <summary>Gets the caller-supplied extraction or conversion description.</summary>
    /// <value>A nonblank description. Callers should omit personal identifiers.</value>
    public string Provenance { get; }
}
