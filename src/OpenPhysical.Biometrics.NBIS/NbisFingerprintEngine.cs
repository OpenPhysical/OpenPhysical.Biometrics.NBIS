// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Provides in-memory MINDTCT V2 extraction and Bozorth3 matching using NBIS 5.0.0.</summary>
/// <remarks>Native loading is deferred until the first operation. Calls are serialized across all instances.
/// This class accepts Microsoft logging abstractions. Logged values are operation status and native status codes.
/// A failed native initialization is cached; fix deployment and restart the process before retrying.</remarks>
public sealed class NbisFingerprintEngine : IFingerprintEngine
{
    private const string Library = "openphysical_nbis";
    private static readonly Lazy<bool> Initialized = new(Initialize);

    private readonly ILogger<NbisFingerprintEngine> logger;

    /// <summary>Creates an engine with optional Microsoft.Extensions.Logging diagnostics.</summary>
    /// <param name="logger">Structured logger; the default discards diagnostics. Logged values are operation status and native status codes.</param>
    public NbisFingerprintEngine(ILogger<NbisFingerprintEngine>? logger = null)
    {
        this.logger = logger ?? NullLogger<NbisFingerprintEngine>.Instance;
    }

    private void EnsureInitialized()
    {
        try
        {
            _ = Initialized.Value;
        }
        catch (Exception ex)
            when (ex
                    is DllNotFoundException
                        or BadImageFormatException
                        or EntryPointNotFoundException
                        or InvalidOperationException
                        or PlatformNotSupportedException
            )
        {
            this.logger.LogError(
                new EventId(1200, "NativeInitializationFailed"),
                "NBIS native initialization failed."
            );
            throw;
        }
    }

    /// <summary>Extracts MINDTCT V2 minutiae from an uncompressed 8-bit grayscale fingerprint image.</summary>
    /// <param name="pixels">Tightly packed rows from the top left, exactly <paramref name="width"/> times
    /// <paramref name="height"/> bytes. Zero is black and 255 is white. The caller's buffer is not modified.</param>
    /// <param name="width">Image width in pixels, from 64 through 2048 inclusive.</param>
    /// <param name="height">Image height in pixels, from 64 through 2048 inclusive.</param>
    /// <param name="dotsPerInch">Explicit image resolution; must be 500. No metadata inference or resampling occurs.</param>
    /// <returns>An immutable, nominal 500 ppi template in NIST XYT coordinates. A blank image may produce no minutiae.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is outside its supported range, or the resolution is not 500 ppi.</exception>
    /// <exception cref="ArgumentException">The pixel buffer length does not match the dimensions.</exception>
    /// <exception cref="PlatformNotSupportedException">The current operating system or process architecture is unsupported.</exception>
    /// <exception cref="DllNotFoundException">The native runtime asset is missing or cannot be loaded.</exception>
    /// <exception cref="BadImageFormatException">The native asset is incompatible with the process.</exception>
    /// <exception cref="EntryPointNotFoundException">The native asset does not expose the required entry points.</exception>
    /// <exception cref="InvalidOperationException">The native ABI is incompatible, extraction fails, or more than 1000 minutiae are detected.</exception>
    /// <remarks>The resulting Y coordinate is image height minus the source row. Angles are degrees counterclockwise
    /// from east, pointing away from ridge endings. Native calls are serialized across all engine instances.</remarks>
    /// <example><code>
    /// IFingerprintEngine engine = new NbisFingerprintEngine();
    /// FingerprintTemplate probe = engine.Extract(gray8Pixels, width, height, dotsPerInch: 500);
    /// </code></example>
    public FingerprintTemplate Extract(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int dotsPerInch
    )
    {
        FingerprintImage.Validate(pixels, width, height, dotsPerInch);
        this.EnsureInitialized();
        var input = pixels.ToArray();
        var output = new NativeMinutia[1000];
        try
        {
            var status = ExtractNative(
                input,
                input.Length,
                width,
                height,
                dotsPerInch,
                output,
                output.Length,
                out var count,
                out var error
            );
            if (status != 0)
            {
                this.logger.LogError(
                    new EventId(1201, "ExtractionFailed"),
                    "NBIS extraction failed with status {Status} and engine code {EngineCode}.",
                    status,
                    error
                );
                throw new InvalidOperationException(
                    $"NBIS extraction failed (status {status}, engine code {error})."
                );
            }

            this.logger.LogDebug(
                new EventId(1000, "ExtractionCompleted"),
                "NBIS extraction completed."
            );
            return new FingerprintTemplate(
                width,
                height,
                output.Take(count).Select(m => m.ToMinutia()),
                "NBIS 5.0.0 MINDTCT V2"
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            Array.Clear(output);
        }
    }

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
    /// Applications calibrate their own thresholds. Native calls are serialized; neither input template is modified.</remarks>
    /// <example><code>
    /// int rawScore = engine.Match(probe, reference);
    /// </code></example>
    public int Match(FingerprintTemplate probe, FingerprintTemplate gallery)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(gallery);
        this.EnsureInitialized();
        var p = probe.Minutiae.Select(NativeMinutia.FromMinutia).ToArray();
        var g = gallery.Minutiae.Select(NativeMinutia.FromMinutia).ToArray();
        try
        {
            var status = MatchNative(p, p.Length, g, g.Length, out var score);
            if (status != 0)
            {
                this.logger.LogError(
                    new EventId(1202, "MatchingFailed"),
                    "NBIS matching failed with status {Status}.",
                    status
                );
                throw new InvalidOperationException($"NBIS matching failed (status {status}).");
            }

            this.logger.LogDebug(
                new EventId(1001, "MatchingCompleted"),
                "NBIS matching completed."
            );
            return score;
        }
        finally
        {
            Array.Clear(p);
            Array.Clear(g);
        }
    }

    private static bool Initialize()
    {
        NativeLibrary.SetDllImportResolver(typeof(NbisFingerprintEngine).Assembly, Resolve);
        if (AbiVersion() != 1)
            throw new InvalidOperationException(
                "Unsupported OpenPhysical NBIS native ABI; rebuild the native assets."
            );
        return true;
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Library)
            return IntPtr.Zero;
        var os =
            OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsLinux() ? "linux"
            : OperatingSystem.IsMacOS() ? "osx"
            : "unsupported";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "unsupported",
        };
        var rid = $"{os}-{arch}";
        using var manifest =
            assembly.GetManifestResourceStream("OpenPhysical.Biometrics.NBIS.runtimes.props")
            ?? throw new InvalidOperationException("The NBIS runtime manifest is missing.");
        var runtime = System
            .Xml.Linq.XDocument.Load(manifest)
            .Descendants("NbisRuntime")
            .SingleOrDefault(item => (string?)item.Attribute("Include") == rid);
        if (runtime is null)
            throw new PlatformNotSupportedException($"NBIS has no supported runtime for {rid}.");
        var file =
            runtime.Element("NativeFile")?.Value
            ?? throw new InvalidOperationException(
                "The NBIS runtime manifest has no native filename."
            );
        var directory = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
        foreach (
            var path in new[]
            {
                Path.Combine(directory, "runtimes", rid, "native", file),
                Path.Combine(directory, file),
            }
        )
            if (File.Exists(path))
                return NativeLibrary.Load(path);
        throw new DllNotFoundException(
            $"NBIS native asset for {rid} is missing. Build native/nbis and include its runtime asset in the application output."
        );
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeMinutia
    {
        public int X,
            Y,
            Angle,
            Quality,
            Type;

        internal readonly FingerprintMinutia ToMinutia() =>
            new(this.X, this.Y, this.Angle, this.Quality, this.Type);

        internal static NativeMinutia FromMinutia(FingerprintMinutia m) =>
            new()
            {
                X = m.X,
                Y = m.Y,
                Angle = m.Angle,
                Quality = m.Quality,
                Type = m.Type,
            };
    }

    [DllImport(
        Library,
        EntryPoint = "op_nbis_abi_version",
        CallingConvention = CallingConvention.Cdecl
    )]
    private static extern int AbiVersion();

    [DllImport(
        Library,
        EntryPoint = "op_nbis_extract",
        CallingConvention = CallingConvention.Cdecl
    )]
    private static extern int ExtractNative(
        [In] byte[] pixels,
        int length,
        int width,
        int height,
        int dpi,
        [Out] NativeMinutia[] output,
        int capacity,
        out int count,
        out int error
    );

    [DllImport(Library, EntryPoint = "op_nbis_match", CallingConvention = CallingConvention.Cdecl)]
    private static extern int MatchNative(
        [In] NativeMinutia[] probe,
        int probeCount,
        [In] NativeMinutia[] gallery,
        int galleryCount,
        out int score
    );
}
