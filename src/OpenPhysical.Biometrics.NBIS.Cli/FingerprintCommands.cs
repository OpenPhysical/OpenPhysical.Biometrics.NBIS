// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS.Cli;

using System.Globalization;
using OpenPhysical.Biometrics.NBIS;

internal static class FingerprintCommands
{
    // Reject corrupt byte sequences before JSON parsing can replace them with different text.
    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, true);

    internal static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                Console.WriteLine(
                    "nbis extract <gray8.raw> <output.json> --width N --height N --dpi 500"
                );
                Console.WriteLine("nbis match <probe.json> <gallery.json>");
                return 0;
            }
            if (args.Length < 3)
                throw new ArgumentException(
                    "Expected a command and two file paths; use nbis --help."
                );
            int Option(string name)
            {
                var index = Array.IndexOf(args, name, 3);
                if (
                    index < 0
                    || index + 1 >= args.Length
                    || !int.TryParse(
                        args[index + 1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var value
                    )
                )
                    throw new ArgumentException($"Required numeric option: {name}.");
                return value;
            }
            void ValidateOptions(params string[] allowed)
            {
                if ((args.Length - 3) % 2 != 0)
                    throw new ArgumentException("Options require a value.");
                var seen = new HashSet<string>();
                for (var i = 3; i < args.Length; i += 2)
                    if (!allowed.Contains(args[i]) || !seen.Add(args[i]))
                        throw new ArgumentException("Unknown or duplicate option.");
            }
            byte[] ReadBounded(string path, int maximum)
            {
                if (new FileInfo(path).Length > maximum)
                    throw new ArgumentException("Input file exceeds the supported limit.");
                return File.ReadAllBytes(path);
            }
            void WriteNew(FingerprintTemplate template)
            {
                using var stream = new FileStream(
                    args[2],
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                );
                using var writer = new StreamWriter(stream);
                writer.Write(FingerprintTemplateJson.Serialize(template));
            }
            switch (args[0])
            {
                case "extract":
                    ValidateOptions("--width", "--height", "--dpi");
                    var pixels = ReadBounded(args[1], 2048 * 2048);
                    try
                    {
                        WriteNew(
                            new NbisFingerprintEngine().Extract(
                                pixels,
                                Option("--width"),
                                Option("--height"),
                                Option("--dpi")
                            )
                        );
                    }
                    finally
                    {
                        System.Security.Cryptography.CryptographicOperations.ZeroMemory(pixels);
                    }
                    break;
                case "match":
                    ValidateOptions();
                    var p = FingerprintTemplateJson.Deserialize(
                        StrictUtf8.GetString(ReadBounded(args[1], 1_000_000))
                    );
                    var g = FingerprintTemplateJson.Deserialize(
                        StrictUtf8.GetString(ReadBounded(args[2], 1_000_000))
                    );
                    Console.WriteLine(
                        new NbisFingerprintEngine()
                            .Match(p, g)
                            .ToString(CultureInfo.InvariantCulture)
                    );
                    break;
                default:
                    throw new ArgumentException("Unknown fingerprint command; use nbis --help.");
            }
            return 0;
        }
        catch (Exception ex)
            when (ex
                    is ArgumentException
                        or FormatException
                        or NotSupportedException
                        or IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or System.Text.Json.JsonException
                        or OverflowException
                        or DllNotFoundException
                        or BadImageFormatException
                        or EntryPointNotFoundException
            )
        {
            // Never echo input data, filenames, or serializer exception details.
            Console.Error.WriteLine(
                "Fingerprint command failed. Check input format, dimensions, resolution, output availability, and native runtime installation."
            );
            return 1;
        }
    }
}
