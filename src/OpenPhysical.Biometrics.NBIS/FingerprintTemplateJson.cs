// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Versioned JSON interchange for NBIS templates, independent of PIV/CBEFF encoding.</summary>
public static class FingerprintTemplateJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Serializes a validated template. The returned JSON contains sensitive biometric information.</summary>
    /// <param name="template">Template to serialize.</param>
    /// <returns>A version 1 nominal 500 ppi NBIS XYT document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> is null.</exception>
    /// <remarks>The JSON contains biometric information and supplies neither encryption nor credential authenticity.</remarks>
    public static string Serialize(FingerprintTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return JsonSerializer.Serialize(
            new Document(
                1,
                template.Width,
                template.Height,
                500,
                template.Provenance,
                template.Minutiae.ToArray()
            ),
            Options
        );
    }

    /// <summary>Reads a version 1 NBIS template with explicit resolution and validates every minutia.</summary>
    /// <param name="json">A JSON document of at most 1,000,000 characters.</param>
    /// <returns>A validated immutable template.</returns>
    /// <exception cref="ArgumentNullException">The JSON or a required template field is null.</exception>
    /// <exception cref="ArgumentException">The document is oversized or its template values are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An image dimension is outside 1 through 2048 inclusive.</exception>
    /// <exception cref="JsonException">JSON is malformed, contains unknown fields, or does not describe a template.</exception>
    /// <exception cref="NotSupportedException">The schema version is not 1 or explicit resolution is not 500 ppi.</exception>
    public static FingerprintTemplate Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 1_000_000)
            throw new ArgumentException("Template document is too large.", nameof(json));
        var document =
            JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new JsonException("Template is null.");
        if (document.SchemaVersion != 1 || document.DotsPerInch != 500)
            throw new NotSupportedException(
                "Expected NBIS template schema 1 with explicit nominal 500 ppi resolution."
            );
        return new FingerprintTemplate(
            document.Width,
            document.Height,
            document.Minutiae,
            document.Provenance
        );
    }

    private sealed record Document(
        int SchemaVersion,
        int Width,
        int Height,
        int DotsPerInch,
        string Provenance,
        FingerprintMinutia[] Minutiae
    );
}
