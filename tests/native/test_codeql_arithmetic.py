#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-2.0-or-later
"""Compile allocation expressions from the shipped sources at large dimensions."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "native/nbis/upstream/mindtct/src/lib/mindtct"

class ArithmeticRegression(unittest.TestCase):
    def test_allocation_products_exceed_signed_int_without_overflow(self):
        cases = [("binar.c", "bdata", "bw", "bh", 1),
                 ("maps.c", "pmap", "iw", "ih", 4),
                 ("maps.c", "omap", "mw", "mh", 4),
                 ("quality.c", "QualMap", "map_w", "map_h", 4)]
        for filename, target, width, height, size in cases:
            source = (SOURCE / filename).read_text()
            expressions = re.findall(r"\b" + target + r"\s*=\s*\([^;]*?\)malloc\(([^;]+)\);", source)
            self.assertTrue(expressions, filename)
            for expression in expressions:
                with self.subTest(file=filename, expression=expression), tempfile.TemporaryDirectory() as folder:
                    c = Path(folder) / "size.c"
                    c.write_text("#include <stddef.h>\n#include <stdint.h>\nint main(void) {\n"
                                 + f"volatile int {width}=65536, {height}=65536;\n"
                                 + f"size_t result = {expression};\n"
                                 + f"return result != UINT64_C({65536 * 65536 * size});\n}}\n")
                    exe = Path(folder) / "size"
                    subprocess.run(["cc", "-fsanitize=undefined", "-fno-sanitize-recover=all", str(c), "-o", str(exe)], check=True)
                    subprocess.run([str(exe)], check=True)

    def test_direction_map_copy_uses_the_full_allocation_size(self):
        source = (SOURCE / "maps.c").read_text()
        expression = re.search(r"memcpy\(direction_map, omap, (.+)\);", source).group(1)
        with tempfile.TemporaryDirectory() as folder:
            c = Path(folder) / "copy.c"
            # Intercept memcpy so the real count expression is exercised without
            # allocating or touching a 16 GiB buffer.
            c.write_text("#include <stddef.h>\n#include <stdint.h>\n"
                         "static size_t copied;\n"
                         "static void *record_copy(void *d, const void *s, size_t n) { copied=n; return d; }\n"
                         "#define memcpy record_copy\n"
                         "int main(void) { volatile int mw=65536, mh=65536;\n"
                         "int *direction_map=0, *omap=0;\n"
                         + f"memcpy(direction_map, omap, {expression});\n"
                         "return copied != UINT64_C(17179869184);}\n")
            exe = Path(folder) / "copy"
            subprocess.run(["cc", "-fsanitize=undefined", "-fno-sanitize-recover=all", str(c), "-o", str(exe)], check=True)
            subprocess.run([str(exe)], check=True)

if __name__ == "__main__":
    unittest.main()
