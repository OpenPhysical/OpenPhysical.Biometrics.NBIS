# SPDX-License-Identifier: GPL-2.0-or-later
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("source_provenance", ROOT / "scripts/source_provenance.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class ProvenanceTests(unittest.TestCase):
    def test_original_receipt_and_corrected_bytes_are_both_required(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory)
            (source / "upstream").mkdir()
            file = source / "upstream/example.c"
            original = b"int old;\n"
            corrected = b"int corrected;\n"
            file.write_bytes(corrected)
            (source / "upstream-sha256.json").write_text(json.dumps({"example.c": hashlib.sha256(original).hexdigest()}))
            corrections = {"example.c": {"sha256": hashlib.sha256(corrected).hexdigest(), "hunks": [
                {"line": 0, "before": [original.decode()], "after": [corrected.decode()]}]}}
            manifest = source / "source-corrections.json"
            manifest.write_text(json.dumps(corrections))
            module.verify(source)
            file.write_bytes(corrected + b"unexpected\n")
            with self.assertRaises(ValueError): module.verify(source)
            file.write_bytes(corrected)
            corrections["example.c"]["hunks"][0]["before"] = ["forged original\n"]
            manifest.write_text(json.dumps(corrections))
            with self.assertRaises(ValueError): module.verify(source)

if __name__ == "__main__": unittest.main()
