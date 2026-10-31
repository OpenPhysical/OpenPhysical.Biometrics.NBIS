// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS.Examples;

using OpenPhysical.Biometrics.NBIS.Fixtures;

internal static class Sd04Examples
{
    internal static int Run(string directory)
    {
        IFingerprintEngine engine = new NbisFingerprintEngine();
        // The manifest identifies two impressions of each finger and records
        // their dimensions, resolution, and hashes. Use it to preserve pairing.
        var fixtures = new Sd04Fixtures(directory);
        // Finish one pair before loading the next to bound image memory use.
        foreach (var pair in fixtures.Pairs)
        {
            var first = pair.First.Load();
            var second = pair.Second.Load();
            // Select extracts each impression; ToArray executes the sequence once.
            // Keep these templates for repeated matching instead of extracting
            // the same image every time a query is enumerated.
            FingerprintImage[] captures = [first, second];
            var templates = captures.Select(engine.Extract).ToArray();
            var firstTemplate = templates[0];
            var secondTemplate = templates[1];
            var expected = engine.Match(firstTemplate, secondTemplate);

            // Demonstrate the convenience overloads with the same probe/gallery
            // order. Each image operand triggers extraction during that call.
            int[] scores =
            [
                engine.Match(first, second),
                engine.Match(first, secondTemplate),
                engine.Match(firstTemplate, second),
                engine.Match(firstTemplate, secondTemplate),
            ];
            // A JSON round trip lets a stored template join the same workflow.
            var restored = FingerprintTemplateJson.Deserialize(
                FingerprintTemplateJson.Serialize(firstTemplate)
            );
            // Any expresses the consistency check directly and stops on a mismatch.
            // Matching policy uses a calibrated threshold supplied by the application.
            if (
                scores.Any(score => score != expected)
                || engine.Match(restored, secondTemplate) != expected
            )
                throw new InvalidOperationException("SD04 workflow results differed.");
        }
        Console.WriteLine(
            "Compared 10 SD04 pairs across five classes using image, template, LINQ, and JSON workflows."
        );
        return 0;
    }
}
