// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS.Tests;

using System.Collections.Immutable;
using NUnit.Framework;

[TestFixture]
internal sealed class WorkflowTests
{
    [Test]
    public void ImageOwnsItsPixelsAndKeepsCaptureMetadata()
    {
        var pixels = new byte[64 * 64];
        pixels[0] = 11;
        var image = new FingerprintImage(pixels, 64, 64, 500);
        Array.Clear(pixels);
        Assert.Multiple(() =>
        {
            Assert.That(image.Pixels[0], Is.EqualTo(11));
            Assert.That(image.Width, Is.EqualTo(64));
            Assert.That(image.Height, Is.EqualTo(64));
            Assert.That(image.DotsPerInch, Is.EqualTo(500));
        });
    }

    [TestCase(63, 64, 500, "width")]
    [TestCase(64, 2049, 500, "height")]
    [TestCase(64, 64, 1000, "dotsPerInch")]
    public void ImageValidatesMetadata(int width, int height, int dpi, string parameter)
    {
        Assert.That(
            Assert
                .Throws<ArgumentOutOfRangeException>(() =>
                    new FingerprintImage([], width, height, dpi)
                )!
                .ParamName,
            Is.EqualTo(parameter)
        );
    }

    [Test]
    public void ImageRequiresAnExactPixelCount()
    {
        Assert.Throws<ArgumentException>(() => new FingerprintImage(new byte[4095], 64, 64, 500));
    }

    [TestCase(true, true, 2)]
    [TestCase(true, false, 1)]
    [TestCase(false, true, 1)]
    [TestCase(false, false, 0)]
    public void MatchingExtractsOnlyImagesAndPreservesProbeGalleryOrder(
        bool probeIsImage,
        bool galleryIsImage,
        int extractions
    )
    {
        var spy = new RecordingEngine();
        IFingerprintEngine engine = spy;
        var probe = Image(11);
        var gallery = Image(22);
        var probeTemplate = Template(11);
        var galleryTemplate = Template(22);
        var score = (probeIsImage, galleryIsImage) switch
        {
            (true, true) => engine.Match(probe, gallery),
            (true, false) => engine.Match(probe, galleryTemplate),
            (false, true) => engine.Match(probeTemplate, gallery),
            _ => engine.Match(probeTemplate, galleryTemplate),
        };
        Assert.That(score, Is.EqualTo(1122));
        Assert.That(spy.Extractions, Is.EqualTo(extractions));
        Assert.That(spy.Comparisons, Is.EqualTo(1));
        if (!probeIsImage)
            Assert.That(spy.Probe, Is.SameAs(probeTemplate));
        if (!galleryIsImage)
            Assert.That(spy.Gallery, Is.SameAs(galleryTemplate));
    }

    [Test]
    public void NullArgumentsFailBeforeExtraction()
    {
        var spy = new RecordingEngine();
        var image = Image(11);
        var template = Template(22);
        Assert.Throws<ArgumentNullException>(() => spy.Match(image, (FingerprintImage)null!));
        Assert.Throws<ArgumentNullException>(() => spy.Match(image, (FingerprintTemplate)null!));
        Assert.Throws<ArgumentNullException>(() => spy.Match((FingerprintTemplate)null!, image));
        Assert.Throws<ArgumentNullException>(() => spy.Match((FingerprintImage)null!, image));
        Assert.Throws<ArgumentNullException>(() => spy.Match((FingerprintImage)null!, template));
        Assert.Throws<ArgumentNullException>(() => spy.Extract(null!));
        IFingerprintEngine missing = null!;
        Assert.Throws<ArgumentNullException>(() => missing.Extract(image));
        Assert.Throws<ArgumentNullException>(() => missing.Match(image, image));
        Assert.Throws<ArgumentNullException>(() => missing.Match(image, template));
        Assert.Throws<ArgumentNullException>(() => missing.Match(template, image));
        Assert.That(spy.Extractions, Is.Zero);
        Assert.That(spy.Comparisons, Is.Zero);
    }

    [Test]
    public void ExtractionComposesWithLinqAndReusableImmutableTemplates()
    {
        var spy = new RecordingEngine();
        IFingerprintEngine engine = spy;
        FingerprintImage[] captures = [Image(11), Image(22)];
        var templates = captures.Select(engine.Extract).ToImmutableArray();
        Assert.That(spy.Extractions, Is.EqualTo(2));
        var scores = templates.Select(probe => engine.Match(probe, templates[1])).ToArray();
        Assert.That(scores, Is.EqualTo(new[] { 1122, 2222 }));
        Assert.That(spy.Extractions, Is.EqualTo(2));
    }

    [Test]
    public void EngineFailuresPropagateWithoutFurtherOperations()
    {
        var engine = new RecordingEngine
        {
            Failure = new InvalidOperationException("engine failure"),
        };
        var failure = Assert.Throws<InvalidOperationException>(() =>
            engine.Match(Image(11), Image(22))
        );
        Assert.That(failure, Is.SameAs(engine.Failure));
        Assert.That(engine.Extractions, Is.EqualTo(1));
        Assert.That(engine.Comparisons, Is.Zero);
    }

    private static FingerprintImage Image(byte value)
    {
        var pixels = new byte[64 * 64];
        pixels[0] = value;
        return new FingerprintImage(pixels, 64, 64, 500);
    }

    private static FingerprintTemplate Template(int x) =>
        new(64, 64, [new(x, 20, 90, 75)], "workflow test");

    private sealed class RecordingEngine : IFingerprintEngine
    {
        public int Extractions { get; private set; }
        public int Comparisons { get; private set; }
        public FingerprintTemplate? Probe { get; private set; }
        public FingerprintTemplate? Gallery { get; private set; }
        public Exception? Failure { get; init; }

        public FingerprintTemplate Extract(
            ReadOnlySpan<byte> pixels,
            int width,
            int height,
            int dotsPerInch
        )
        {
            this.Extractions++;
            if (this.Failure is not null)
                throw this.Failure;
            Assert.That((width, height, dotsPerInch), Is.EqualTo((64, 64, 500)));
            return Template(pixels[0]);
        }

        public int Match(FingerprintTemplate probe, FingerprintTemplate gallery)
        {
            this.Comparisons++;
            this.Probe = probe;
            this.Gallery = gallery;
            return probe.Minutiae.Single().X * 100 + gallery.Minutiae.Single().X;
        }
    }
}
