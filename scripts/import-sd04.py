#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
# Copyright (c) 2026 Mistial Developer
"""Import the curated SD04 subset and generate independent NBIS reference results."""
import argparse
import hashlib
import importlib.metadata
import json
import subprocess
import tempfile
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SELECTION = [("A", "0005_03", None), ("A", "0070_10", "T"),
             ("L", "0003_10", None), ("L", "0091_10", "T"),
             ("R", "0002_05", None), ("R", "0110_08", "T"),
             ("T", "0021_07", None), ("T", "0008_10", "L"),
             ("W", "0010_01", None), ("W", "0371_05", "R")]

def digest(path, algorithm="sha256"):
    return hashlib.new(algorithm, path.read_bytes()).hexdigest()

def ihead(raw):
    """Lossless NBIS input envelope: original top-left pixels, 8-bit, 500 ppi."""
    number = lambda value: str(value).encode().ljust(8, b"\0")
    header = b"NIST SD04 fixture".ljust(80, b"\0") + b"\0" * 26
    header += b"".join(number(v) for v in [512, 512, 8, 500, 0, 0, 8, 8])
    header += b"00" + number(0) + number(255) + b"0000" + b"\0" * 80 + number(0) + number(0)
    assert len(header) == 288 and len(raw) == 512 * 512
    return number(288) + header + raw

def verify(output):
    assert (output / "README.md").is_file() and (output / "NIST-DATA-NOTICE.txt").is_file()
    manifest = json.loads((output / "manifest.json").read_text())
    assert manifest["schema_version"] == 1
    assert len(manifest["pairs"]) == 10 and len(manifest["images"]) == 20
    assert {p["id"] for p in manifest["pairs"]} == {p[1] for p in SELECTION}
    by_pair = {p["id"]: p for p in manifest["pairs"]}
    for primary, ident, secondary in SELECTION:
        assert by_pair[ident]["primary_class"] == primary
        assert by_pair[ident]["cross_referenced_class"] == secondary
        assert by_pair[ident]["finger_position"] == int(ident[-2:])
    for primary in "ALRTW":
        pairs = [p for p in manifest["pairs"] if p["primary_class"] == primary]
        assert len(pairs) == 2 and sum(p["cross_referenced_class"] is not None for p in pairs) == 1
    images = {item["id"]: item for item in manifest["images"]}
    assert len(images) == 20
    assert {p.name for p in output.glob("*.png")} == {i["png"] for i in images.values()}
    assert {p.name for p in output.glob("*.gray8")} == {i["raw"] for i in images.values()}
    assert {p.name for p in output.glob("*.xyt")} == {i["xyt"] for i in images.values()}
    assert {p.name for p in output.glob("*.min")} == {i["min"] for i in images.values()}
    for pair in manifest["pairs"]:
        for prefix, field in [("f", "first"), ("s", "second")]:
            assert pair[field] == prefix + pair["id"]
            assert images[pair[field]]["pair_id"] == pair["id"]
    for item in images.values():
        assert (item["width"], item["height"], item["dots_per_inch"]) == (512, 512, 500)
        assert (output / item["raw"]).stat().st_size == 512 * 512
        for field in ["png", "raw", "xyt", "min"]:
            assert digest(output / item[field]) == item[field + "_sha256"], (item["id"], field)
        report_rows = [line.split(":") for line in (output / item["min"]).read_text().splitlines() if ":" in line]
        assert len(report_rows) == len((output / item["xyt"]).read_text().splitlines())
        assert all(int(row[0].strip()) == index and row[4].strip() in {"RIG", "BIF"} for index, row in enumerate(report_rows))
        xyt_rows = [list(map(int, line.split())) for line in (output / item["xyt"]).read_text().splitlines()]
        for report, xyt in zip(report_rows, xyt_rows):
            x, y = map(int, report[1].split(","))
            # MINDTCT's MIN report is top-left; its NIST XYT uses the opposite y origin.
            assert (x, item["height"] - y) == tuple(xyt[:2]), (item["id"], report[0], "MIN/XYT row alignment")
        assert digest(output / item["png"], "md5") == item["source_md5"]
    cases = (output / "scores.tsv").read_text().splitlines()
    ordered = [tuple(line.split()[:2]) for line in cases]
    assert len(ordered) == len(set(ordered)) == 400
    assert set(ordered) == {(a, b) for a in images for b in images}
    assert all(len(line.split()) == 3 and int(line.split()[2]) >= 0 for line in cases)
    assert digest(output / "scores.tsv") == manifest["scores_sha256"]
    expected_index = "".join(f"{i['id']} {i['raw']} {i['xyt']} 512 512 500\n" for i in manifest["images"])
    assert (output / "images.tsv").read_text() == expected_index
    assert digest(output / "images.tsv") == manifest["index_sha256"]
    print("Verified 20 SD04 fixtures, five classes, paired identities, hashes, and 400 ordered scores.")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--reference-build", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "tests/fixtures/sd04")
    parser.add_argument("--refresh-reference-minutiae", action="store_true", help="Export independently recorded MINDTCT .min type reports from committed pixels.")
    parser.add_argument("--verify", action="store_true", help="Offline, standard-library-only integrity check")
    args = parser.parse_args()
    output = args.output.resolve()
    if args.verify:
        verify(output)
        return
    if args.refresh_reference_minutiae:
        if args.reference_build is None:
            parser.error("Reference refresh requires --reference-build.")
        reference = args.reference_build.resolve()
        manifest = json.loads((output / "manifest.json").read_text())
        receipt = json.loads((reference / "reference-build.json").read_text())
        assert receipt == manifest["reference"], "Use the recorded unmodified reference tool build."
        assert digest(reference / "bin/mindtct") == receipt["tools"]["mindtct"]["sha256"]
        with tempfile.TemporaryDirectory(prefix="sd04-reference-types-") as directory:
            work = Path(directory)
            for item in manifest["images"]:
                assert digest(output / item["raw"]) == item["raw_sha256"]
                envelope = work / (item["id"] + ".ihead")
                envelope.write_bytes(ihead((output / item["raw"]).read_bytes()))
                subprocess.run([str(reference / "bin/mindtct"), str(envelope), str(work / item["id"])], check=True, capture_output=True)
                assert (work / (item["id"] + ".xyt")).read_text() == (output / item["xyt"]).read_text(), "Reference XYT changed."
                item["min"] = item["id"] + ".min"
                (output / item["min"]).write_text((work / item["min"]).read_text())
                item["min_sha256"] = digest(output / item["min"])
        manifest["reference_minutiae_report"] = "Unmodified MINDTCT .min output; RIG and BIF are independently recorded types in the same row order as .xyt."
        (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
        verify(output)
        return
    if args.source is None or args.reference_build is None:
        parser.error("Import requires --source and --reference-build.")
    from PIL import Image
    assert importlib.metadata.version("Pillow") == "12.2.0", "Use requirements-fixtures.txt"
    source, reference = args.source.resolve(), args.reference_build.resolve()
    receipt = json.loads((reference / "reference-build.json").read_text())
    assert receipt["archive_sha256"] == "0adf8ab0f6b0e4208de50ca00ba21d3d77112ecd66288757ddfed21f6bee92c3"
    for tool in ["mindtct", "bozorth3"]:
        assert digest(reference / "bin" / tool) == receipt["tools"][tool]["sha256"]
    md5 = {}
    for line in (source / "sd04_md5.lst").read_text().splitlines():
        if not line.strip():
            continue
        checksum, name = line.split(maxsplit=1)
        md5[name.removeprefix("sd04/")] = checksum
    output.mkdir(parents=True, exist_ok=True)
    for name in ["README.md", "NIST-DATA-NOTICE.txt"]:
        # Every exported subset travels with the same attribution and reproduction guide.
        notice = ROOT / "tests/fixtures/sd04" / name
        if notice.resolve() != (output / name).resolve():
            (output / name).write_bytes(notice.read_bytes())
    pairs, images = [], []
    with tempfile.TemporaryDirectory(prefix="sd04-reference-") as directory:
        work = Path(directory)
        for primary, ident, secondary in SELECTION:
            pairs.append(dict(id=ident, primary_class=primary, cross_referenced_class=secondary,
                              finger_position=int(ident[-2:]), first="f" + ident, second="s" + ident))
            for prefix in ["f", "s"]:
                name = prefix + ident
                candidates = list(source.glob("png_txt/*/" + name + ".png"))
                assert len(candidates) == 1, name
                png = candidates[0]
                txt = png.with_suffix(".txt")
                for original in [png, txt]:
                    assert digest(original, "md5") == md5[str(original.relative_to(source))]
                fields = dict(line.split(": ", 1) for line in txt.read_text().splitlines() if ": " in line)
                assert fields["Class"] == primary
                classification = fields["History"].split()[1]
                assert classification == primary + (secondary or ""), (name, classification)
                with Image.open(png) as image:
                    assert image.mode == "L" and image.size == (512, 512)
                    raw = image.tobytes()
                # Preserve source PNG bytes; raw pixels are a lossless decoding only.
                (output / (name + ".png")).write_bytes(png.read_bytes())
                (output / (name + ".gray8")).write_bytes(raw)
                assert (output / (name + ".gray8")).read_bytes() == raw
                envelope = work / (name + ".ihead")
                envelope.write_bytes(ihead(raw))
                subprocess.run([str(reference / "bin/mindtct"), str(envelope), str(work / name)], check=True, capture_output=True)
                # Normalize text line endings only, retaining every reference XYT value and row.
                (output / (name + ".xyt")).write_text((work / (name + ".xyt")).read_text())
                (output / (name + ".min")).write_text((work / (name + ".min")).read_text())
                item = dict(id=name, pair_id=ident, impression="first" if prefix == "f" else "second",
                            width=512, height=512, dots_per_inch=500, source_png=str(png.relative_to(source)),
                            source_metadata=str(txt.relative_to(source)), source_md5=digest(png, "md5"),
                            source_metadata_md5=digest(txt, "md5"), source_metadata_sha256=digest(txt),
                            history=fields["History"], png=name + ".png", raw=name + ".gray8", xyt=name + ".xyt", min=name + ".min")
                for field in ["png", "raw", "xyt", "min"]:
                    item[field + "_sha256"] = digest(output / item[field])
                images.append(item)
        scores = []
        for probe in images:
            for gallery in images:
                result = subprocess.check_output([str(reference / "bin/bozorth3"),
                    str(output / probe["xyt"]), str(output / gallery["xyt"])], text=True)
                scores.append(f"{probe['id']} {gallery['id']} {int(result.strip())}\n")
        (output / "scores.tsv").write_text("".join(scores))
    (output / "images.tsv").write_text("".join(f"{i['id']} {i['raw']} {i['xyt']} 512 512 500\n" for i in images))
    manifest = dict(schema_version=1, dataset="NIST Special Database 4",
        author="Craig I. Watson", publisher="National Institute of Standards and Technology",
        citation_url="https://www.nist.gov/publications/nist-special-database-4-nist-8-bit-gray-scale-images-fingerprint-image-groups",
        terms_url="https://www.nist.gov/open/copyright-fair-use-and-licensing-statements-srd-data-software-and-technical-series-publications",
        resolution_source_url="https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication782-1994.pdf",
        resolution_source_page=84, source_pixels_per_millimeter=19.6850,
        transformation_date=date.today().isoformat(), decoder="Pillow 12.2.0",
        transformation="Original PNG bytes retained; lossless 512x512 8-bit grayscale decoding, all rows preserved. Nominal 500 ppi from SD04 documentation; no resampling.",
        source_documents={name: digest(source / name) for name in ["sd04_md5.lst", "readme_sd04.rtf", "Appendixes_sd04.pdf"]},
        reference=receipt, reference_extraction=["mindtct", "<lossless-500-ppi-ihead>", "<output-root>"],
        reference_minutiae_report="Unmodified MINDTCT .min output; RIG and BIF are independently recorded types in the same row order as .xyt.",
        reference_matching=["bozorth3", "<probe.xyt>", "<gallery.xyt>"],
        pairs=pairs, images=images, scores_sha256=digest(output / "scores.tsv"), index_sha256=digest(output / "images.tsv"))
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    verify(output)

if __name__ == "__main__":
    main()
