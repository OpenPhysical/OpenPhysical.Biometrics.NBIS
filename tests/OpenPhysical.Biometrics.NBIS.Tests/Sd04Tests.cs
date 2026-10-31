// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS.Tests;

using System.Globalization;
using System.Security.Cryptography;
using NUnit.Framework;
using OpenPhysical.Biometrics.NBIS.Fixtures;

[TestFixture]
internal sealed class Sd04Tests
{
    private static Sd04Fixtures Fixtures =>
        new(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "sd04"));
    private readonly IFingerprintEngine engine = new NbisFingerprintEngine();

    private static IEnumerable<string> PairIds => Fixtures.Pairs.Select(pair => pair.Id);

    [TestCaseSource(nameof(PairIds))]
    public void RealImpressionsAgreeAcrossManagedWorkflows(string pairId)
    {
        var pair = Fixtures.Pairs.Single(pair => pair.Id == pairId);
        var first = pair.First.Load();
        var second = pair.Second.Load();
        var firstHash = SHA256.HashData(first.Pixels);
        var secondHash = SHA256.HashData(second.Pixels);
        var probe = engine.Extract(first);
        var gallery = engine.Extract(second);
        var originalPoints = probe.Minutiae;
        AssertReferenceMinutiae(probe, pair.First.RawPath);
        AssertReferenceMinutiae(gallery, pair.Second.RawPath);
        var expected = ReferenceScore(pair.First.Id, pair.Second.Id);
        Assert.Multiple(() =>
        {
            Assert.That(engine.Match(first, second), Is.EqualTo(expected));
            Assert.That(engine.Match(first, gallery), Is.EqualTo(expected));
            Assert.That(engine.Match(probe, second), Is.EqualTo(expected));
            Assert.That(engine.Match(probe, gallery), Is.EqualTo(expected));
        });
        var reverse = ReferenceScore(pair.Second.Id, pair.First.Id);
        Assert.That(engine.Match(gallery, probe), Is.EqualTo(reverse));
        Assert.That(engine.Match(second, first), Is.EqualTo(reverse));
        var restored = FingerprintTemplateJson.Deserialize(
            FingerprintTemplateJson.Serialize(probe)
        );
        Assert.That(restored.Minutiae, Is.EqualTo(probe.Minutiae));
        Assert.That(engine.Match(restored, gallery), Is.EqualTo(expected));
        Assert.That(SHA256.HashData(first.Pixels), Is.EqualTo(firstHash));
        Assert.That(SHA256.HashData(second.Pixels), Is.EqualTo(secondHash));
        Assert.That(probe.Minutiae, Is.EqualTo(originalPoints));
    }

    [Test]
    public void PairedTemplatesCanBeReusedAcrossRepeatedAndConcurrentCalls()
    {
        var pairs = Fixtures
            .Pairs.Select(pair =>
                (
                    Probe: engine.Extract(pair.First.Load()),
                    Gallery: engine.Extract(pair.Second.Load()),
                    Expected: ReferenceScore(pair.First.Id, pair.Second.Id)
                )
            )
            .ToArray();
        for (var repetition = 0; repetition < 3; repetition++)
            Parallel.For(
                0,
                pairs.Length,
                index =>
                    Assert.That(
                        engine.Match(pairs[index].Probe, pairs[index].Gallery),
                        Is.EqualTo(pairs[index].Expected)
                    )
            );
    }

    private static int ReferenceScore(string probe, string gallery) =>
        File.ReadLines(
                Path.Combine(Path.GetDirectoryName(Fixtures.Pairs[0].First.RawPath)!, "scores.tsv")
            )
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(columns => columns[0] == probe && columns[1] == gallery)
            .Select(columns => int.Parse(columns[2], CultureInfo.InvariantCulture))
            .Single();

    private static void AssertReferenceMinutiae(FingerprintTemplate template, string rawPath)
    {
        // XYT and type reports come from the independently built, unmodified tools.
        // Full struct checks here protect marshaling; µnit owns the full score matrix.
        var rows = File.ReadLines(Path.ChangeExtension(rawPath, ".xyt"))
            .Select(line =>
                line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
                    .ToArray()
            )
            .ToArray();
        var types = File.ReadLines(Path.ChangeExtension(rawPath, ".min"))
            .Where(line => line.Contains(':'))
            .Select(line =>
                line.Split(':')[4].Trim() switch
                {
                    "RIG" => 1,
                    "BIF" => 2,
                    _ => throw new InvalidDataException("Unknown reference minutia type."),
                }
            )
            .ToArray();
        Assert.That(types.Length, Is.EqualTo(rows.Length));
        var expected = rows.Select(
                (row, index) => new FingerprintMinutia(row[0], row[1], row[2], row[3], types[index])
            )
            .ToArray();
        Assert.That(template.Minutiae, Is.EqualTo(expected));
        var marshaled = template
            .Minutiae.Select(NbisFingerprintEngine.NativeMinutia.FromMinutia)
            .Select(point => point.ToMinutia())
            .ToArray();
        Assert.That(marshaled, Is.EqualTo(expected));
    }
}
