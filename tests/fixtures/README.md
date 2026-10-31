# NIST reference fixtures

## NBIS 5.0.0 test material

These are public NIST NBIS 5.0.0 test fixtures, not local credential/card dumps.
`provenance.json` records upstream members and hashes. The grayscale image is
the uncompressed field 14.999 of the NIST Type-14 test record, 800 by 768 pixels.
NIST generated the expected XYT and matching scores. Its software/test-suite
public-domain notice and disclaimer are retained in `native/nbis/NOTICE.txt`.
Fixtures validate regression parity, not operational biometric accuracy.

## Special Database 4

The [SD04 collection](sd04/README.md) adds 20 images from ten paired fingerprints
covering all five pattern classes. It includes original PNGs, losslessly decoded
grayscale pixels, independent NBIS extraction baselines, and 400 ordered comparison
scores. Its own manifest and NIST data notice document attribution, source hashes,
pairing, classifications, and reproducible generation.
