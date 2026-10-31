#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
# Copyright (c) 2026 Mistial Developer

"""Consume the locally packed OpenPhysical.Biometrics.NBIS package outside the checkout."""
import argparse
import os
import subprocess
import tempfile
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

from runtime_manifest import load_runtimes

ROOT = Path(__file__).resolve().parents[1]
fixture_names = {path.name for path in (ROOT / "tests/fixtures").rglob("*")
                 if path.suffix.lower() in {".png", ".gray8", ".gray", ".xyt", ".min", ".bin"}}

def assert_no_fixture_assets(names):
    assert not any(Path(name).name in fixture_names or "sd04/" in name.lower()
                   or "fixtures/" in name.lower() for name in names), "Test fixture assets must not enter runtime packages"

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--version", default=ET.parse(ROOT / "Directory.Build.props").findtext(".//Version"))
parser.add_argument("--require-all-runtimes", action="store_true", help="Require every supported native asset in the complete release package")
args = parser.parse_args()
package = ROOT / "artifacts/packages" / f"OpenPhysical.Biometrics.NBIS.{args.version}.nupkg"
with zipfile.ZipFile(package) as archive:
    names = archive.namelist()
    assert_no_fixture_assets(names)
    if args.require_all_runtimes:
        for runtime in load_runtimes():
            assert f"runtimes/{runtime['rid']}/native/{runtime['file']}" in names, runtime['rid']
    assert "lib/net10.0/OpenPhysical.Biometrics.NBIS.dll" in names
    assert "lib/net10.0/OpenPhysical.Biometrics.NBIS.xml" in names
    assert "licenses/NOTICE.txt" in names
    assert all(name in names for name in ["LICENSE", "NOTICE", "COMMERCIAL-LICENSING.md", "THIRD_PARTY_NOTICES.md"])
    metadata = ET.fromstring(archive.read("OpenPhysical.Biometrics.NBIS.nuspec"))
    license_element = metadata.find("{*}metadata/{*}license")
    assert license_element is not None and license_element.attrib["type"] == "expression"
    assert license_element.text == "GPL-2.0-or-later"
    # Consumers receive self-contained XML tooltips, including concrete engine methods.
    documentation = ET.fromstring(archive.read("lib/net10.0/OpenPhysical.Biometrics.NBIS.xml"))
    for member in documentation.findall("./members/member"):
        summary = member.find("summary")
        assert summary is not None and "".join(summary.itertext()).strip(), member.attrib
        assert member.find("inheritdoc") is None, member.attrib
        name = member.attrib["name"]
        if name.startswith("M:") and "(" in name:
            signature = name[name.index("(") + 1:-1]
            depth = 0
            parameters = 1 if signature else 0
            for character in signature:
                if character in "{[": depth += 1
                elif character in "}]": depth -= 1
                elif character == "," and depth == 0: parameters += 1
            assert len(member.findall("param")) == parameters, member.attrib
            assert all("".join(param.itertext()).strip() for param in member.findall("param")), member.attrib
    assert not any("Fixtures/" in n or n.endswith(".bin") for n in names)
with tempfile.TemporaryDirectory(prefix="openphysical-nbis-package-") as directory:
    work = Path(directory)
    (work / "Smoke.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><PackageReference Include="OpenPhysical.Biometrics.NBIS" Version="{args.version}" /></ItemGroup></Project>''')
    (work / "Program.cs").write_text('''using OpenPhysical.Biometrics.NBIS;
var engine = new NbisFingerprintEngine();
try {
 var image = new FingerprintImage(Enumerable.Repeat((byte)255, 128*128).ToArray(),128,128,500);
 var t = engine.Extract(image);
 if(new[] { engine.Match(t,t), engine.Match(image,image), engine.Match(t,image), engine.Match(image,t) }.Any(score => score != 0)) return 2;
 if(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name!.Contains("Onnx"))) return 3;
 Console.WriteLine("Packaged consumer passed without ONNX."); return 0;
} catch(DllNotFoundException) { Console.WriteLine("Missing runtime detected."); return 42; }
''')
    # Use an isolated package cache so repeated tests cannot silently consume an old build.
    env = dict(os.environ, NUGET_PACKAGES=str(work / "packages"))
    # Keep feed URLs in XML: Windows shells can reinterpret URL command arguments as paths.
    configuration = ET.Element("configuration")
    sources = ET.SubElement(configuration, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="local", value=str(package.parent))
    ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
    config = work / "NuGet.Config"
    ET.ElementTree(configuration).write(config, encoding="utf-8", xml_declaration=True)
    subprocess.run(["dotnet", "restore", str(work / "Smoke.csproj"), "--configfile", str(config)], check=True, env=env)
    subprocess.run(["dotnet", "build", str(work / "Smoke.csproj"), "--no-restore", "-c", "Release"], check=True, env=env)
    assembly = work / "bin/Release/net10.0/Smoke.dll"
    subprocess.run(["dotnet", str(assembly)], check=True, env=env)
    natives = [p for p in assembly.parent.rglob("*openphysical_nbis*") if p.is_file()]
    assert natives, "Native runtime was not copied from the package"
    for p in natives:
        p.rename(p.with_name(p.name + ".unavailable"))
    result = subprocess.run(["dotnet", str(assembly)], env=env)
    assert result.returncode == 42, "Expected clear missing-runtime failure"
    cli_package = package.parent / f"OpenPhysical.Biometrics.NBIS.Cli.{args.version}.nupkg"
    if cli_package.exists():
        with zipfile.ZipFile(cli_package) as cli_archive:
            assert_no_fixture_assets(cli_archive.namelist())
            cli_metadata = ET.fromstring(cli_archive.read("OpenPhysical.Biometrics.NBIS.Cli.nuspec"))
            cli_license = cli_metadata.find("{*}metadata/{*}license")
            assert cli_license is not None and cli_license.text == "GPL-2.0-or-later"
            assert all(name in cli_archive.namelist() for name in ["LICENSE", "NOTICE", "COMMERCIAL-LICENSING.md", "THIRD_PARTY_NOTICES.md"])
        tools = work / "tools"
        subprocess.run(["dotnet", "tool", "install", "OpenPhysical.Biometrics.NBIS.Cli", "--tool-path", str(tools), "--configfile", str(config), "--version", args.version], check=True, env=env)
        tool = tools / ("nbis.exe" if os.name == "nt" else "nbis")
        subprocess.run([str(tool), "--help"], check=True, env=env)
        output = work / "probe.json"
        subprocess.run([str(tool), "extract", str(ROOT / "tests/fixtures/g001t2u.gray"), str(output), "--width", "800", "--height", "768", "--dpi", "500"], check=True, env=env)
        score = subprocess.run([str(tool), "match", str(output), str(output)], check=True, capture_output=True, text=True, env=env)
        assert int(score.stdout.strip()) > 0
        print("Installed CLI extraction and matching passed.")
print("Package contents, external consumption, and missing-runtime checks passed.")
