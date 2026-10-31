// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS.Tests;

using System.Text;
using NUnit.Framework;
using OpenPhysical.Biometrics.NBIS.Cli;

[TestFixture]
[NonParallelizable]
internal sealed class CliTests
{
    [Test]
    public void ExistingDirectoryOutputReturnsRedactedError()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "private-capture.gray8");
            File.WriteAllBytes(input, new byte[64 * 64]);
            var (code, output, error) = Run(
                "extract",
                input,
                directory,
                "--width",
                "64",
                "--height",
                "64",
                "--dpi",
                "500"
            );
            Assert.That(code, Is.EqualTo(1));
            Assert.That(output, Is.Empty);
            Assert.That(
                error,
                Does.StartWith("Fingerprint command failed.")
                    .And.Not.Contains(directory)
                    .And.Not.Contains("Exception")
            );
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    [Platform("Linux,MacOsX")]
    public void UnwritableOutputDirectoryReturnsRedactedError()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Uses Unix directory permissions.");
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(directory, "private-output");
        Directory.CreateDirectory(outputDirectory);
        var input = Path.Combine(directory, "private-capture.gray8");
        var outputPath = Path.Combine(outputDirectory, "private-template.json");
        File.WriteAllBytes(input, new byte[64 * 64]);
        try
        {
            File.SetUnixFileMode(outputDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            // A privileged process or permissive filesystem can bypass Unix modes.
            // Such a run cannot demonstrate permission-denial coverage.
            try
            {
                using (var probe = new FileStream(outputPath, FileMode.CreateNew)) { }
                Assert.Ignore(
                    "The runner or filesystem permits writes despite read-only Unix directory permissions."
                );
            }
            catch (UnauthorizedAccessException)
            {
                // The CLI below now exercises a verified output-access denial.
            }
            var (code, output, error) = Run(
                "extract",
                input,
                outputPath,
                "--width",
                "64",
                "--height",
                "64",
                "--dpi",
                "500"
            );
            Assert.Multiple(() =>
            {
                Assert.That(code, Is.EqualTo(1));
                Assert.That(output, Is.Empty);
                Assert.That(
                    error,
                    Does.StartWith("Fingerprint command failed.")
                        .And.Not.Contains(directory)
                        .And.Not.Contains("Exception")
                );
                Assert.That(File.Exists(outputPath), Is.False);
            });
        }
        finally
        {
            File.SetUnixFileMode(
                outputDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
            Directory.Delete(directory, true);
        }
    }

    [Test]
    [Platform("Linux,MacOsX")]
    public void UnreadableFileReturnsRedactedError()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Uses Unix file permissions.");
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "private-template.json");
        File.WriteAllText(
            path,
            FingerprintTemplateJson.Serialize(
                new FingerprintTemplate(64, 64, [], "permission-test")
            )
        );
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.None);
            try
            {
                _ = File.ReadAllBytes(path);
                Assert.Ignore(
                    "The runner or filesystem permits reads despite empty Unix file permissions."
                );
            }
            catch (UnauthorizedAccessException)
            {
                // Valid JSON ensures only the access denial causes this failure.
            }
            var (code, output, error) = Run("match", path, path);
            Assert.That(code, Is.EqualTo(1));
            Assert.That(output, Is.Empty);
            Assert.That(
                error,
                Does.StartWith("Fingerprint command failed.").And.Not.Contains(path)
            );
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void InvalidUtf8IsRejectedBeforeMatching()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "private-template.json");
            var json = FingerprintTemplateJson.Serialize(
                new FingerprintTemplate(100, 100, [], "x")
            );
            var bytes = Encoding.UTF8.GetBytes(json);
            bytes[json.IndexOf("\"x\"", StringComparison.Ordinal) + 1] = 0xff;
            File.WriteAllBytes(path, bytes);
            var (code, output, error) = Run("match", path, path);
            Assert.That(code, Is.EqualTo(1));
            Assert.That(output, Is.Empty);
            Assert.That(
                error,
                Does.StartWith("Fingerprint command failed.").And.Not.Contains(path)
            );
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var code = FingerprintCommands.Run(args);
            return (code, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }
}
