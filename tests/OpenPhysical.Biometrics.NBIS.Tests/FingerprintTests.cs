// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS.Tests;

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintTests
{
    private static readonly string Fixtures = Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "Fixtures"
    );
    private readonly IFingerprintEngine engine = new NbisFingerprintEngine();

    [Test]
    public void ManagedExtractionPreservesCallerPixelsAndProducesMatchableTemplate()
    {
        var pixels = File.ReadAllBytes(Path.Combine(Fixtures, "g001t2u.gray"));
        var original = pixels.ToArray();
        var result = this.engine.Extract(pixels, 800, 768, 500);
        Assert.That(result.Minutiae, Is.Not.Empty);
        Assert.That(this.engine.Match(result, result), Is.GreaterThan(0));
        Assert.That(pixels, Is.EqualTo(original));
    }

    [Test]
    public void ConcurrentAndRepeatedCallsRemainDeterministic()
    {
        var p = ReadXyt("a011p_02.xyt");
        var g = ReadXyt("a011r_02.xyt");
        var pixels = File.ReadAllBytes(Path.Combine(Fixtures, "g001t2u.gray"));
        var expected = this.engine.Match(p, g);
        Parallel.For(
            0,
            12,
            i =>
            {
                Assert.That(this.engine.Match(p, g), Is.EqualTo(expected));
                if (i % 4 == 0)
                    Assert.That(this.engine.Extract(pixels, 800, 768, 500).Minutiae, Is.Not.Empty);
            }
        );
    }

    [Test]
    public void InvalidInputsFailBeforeNativeCalls()
    {
        Assert.Throws<ArgumentException>(() => this.engine.Extract(new byte[12], 64, 64, 500));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            this.engine.Extract(new byte[4096], 64, 64, 1000)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            this.engine.Extract([], int.MaxValue, 64, 500)
        );
        Assert.Throws<ArgumentException>(static () =>
            new FingerprintTemplate(100, 100, [new(10, 0, 0, 10)], "test")
        );
        Assert.Throws<ArgumentException>(static () =>
            new FingerprintTemplate(
                100,
                100,
                Enumerable.Repeat(new FingerprintMinutia(10, 10, 0, 10), 1001),
                "test"
            )
        );
    }

    [Test]
    public void ValidationIdentifiesTheInvalidParameter()
    {
        Assert.That(
            Assert
                .Throws<ArgumentOutOfRangeException>(() => this.engine.Extract([], 64, 63, 500))!
                .ParamName,
            Is.EqualTo("height")
        );
        Assert.That(
            Assert
                .Throws<ArgumentOutOfRangeException>(() => this.engine.Extract([], 63, 64, 500))!
                .ParamName,
            Is.EqualTo("width")
        );
        Assert.That(
            Assert
                .Throws<ArgumentOutOfRangeException>(() =>
                    new FingerprintTemplate(100, 0, [], "test")
                )!
                .ParamName,
            Is.EqualTo("height")
        );
        Assert.That(
            Assert
                .Throws<ArgumentOutOfRangeException>(() =>
                    new FingerprintTemplate(0, 100, [], "test")
                )!
                .ParamName,
            Is.EqualTo("width")
        );
    }

    [Test]
    public void RegistrationUsesMicrosoftLoggingAndDoesNotLogBiometricData()
    {
        var logger = new CapturingLogger();
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<NbisFingerprintEngine>>(logger);
        services.AddFingerprintBiometrics();
        services.AddFingerprintBiometrics();
        using var provider = services.BuildServiceProvider();
        var registered = provider.GetRequiredService<IFingerprintEngine>();
        var t = new FingerprintTemplate(128, 128, [], "SENSITIVE-PROVENANCE");
        registered.Extract(new byte[128 * 128], 128, 128, 500);
        registered.Match(t, t);
        Assert.That(
            services.Count(s => s.ServiceType == typeof(IFingerprintEngine)),
            Is.EqualTo(1)
        );
        Assert.That(
            logger.Messages,
            Is.EqualTo(new[] { "NBIS extraction completed.", "NBIS matching completed." })
        );
        Assert.That(logger.Exceptions, Is.All.Null);
        Assert.That(
            typeof(NbisFingerprintEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name),
            Has.None.Contains("Onnx").And.None.Contains("ImageSharp")
        );
    }

    private sealed class CapturingLogger
        : Microsoft.Extensions.Logging.ILogger<NbisFingerprintEngine>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel level,
            Microsoft.Extensions.Logging.EventId id,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            this.Messages.Add(formatter(state, exception));
            this.Exceptions.Add(exception);
        }
    }

    [Test]
    public void JsonRoundTripsAndRejectsUnsupportedSchemaOrResolution()
    {
        var t = new FingerprintTemplate(100, 100, [new(10, 20, 90, 75)], "test");
        var json = FingerprintTemplateJson.Serialize(t);
        var result = FingerprintTemplateJson.Deserialize(json);
        Assert.That(result.Minutiae, Is.EqualTo(t.Minutiae));
        Assert.That(result.Width, Is.EqualTo(t.Width));
        Assert.Throws<NotSupportedException>(() =>
            FingerprintTemplateJson.Deserialize(
                json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2")
            )
        );
        Assert.Throws<NotSupportedException>(() =>
            FingerprintTemplateJson.Deserialize(
                json.Replace("\"DotsPerInch\":500", "\"DotsPerInch\":1000")
            )
        );
        Assert.Throws<ArgumentException>(() =>
            FingerprintTemplateJson.Deserialize(new string(' ', 1_000_001))
        );
    }

    private static FingerprintTemplate ReadXyt(string name) =>
        new(
            2048,
            2048,
            File.ReadLines(Path.Combine(Fixtures, name))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(line =>
                {
                    var values = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .ToArray();
                    return new FingerprintMinutia(values[0], values[1], values[2], values[3]);
                }),
            "NIST NBIS 5.0.0 test fixture"
        );
}
