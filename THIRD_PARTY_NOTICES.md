# Third-Party Notices

Third-party software and documents retain their own rights, licenses, and notices.
The project GPL grant and any separately negotiated commercial license cover only
rights controlled by the applicable copyright holder.

## NIST NBIS and reference fixtures

`native/nbis/upstream/` is an unmodified subset of NIST NBIS 5.0.0. Original source
notices remain intact, and `native/nbis/NOTICE.txt` contains NIST's public-domain
notice and disclaimer. OpenPhysical does not claim copyright in that original source.

Public NIST test material in `tests/fixtures/` has source attribution and hashes in
`provenance.json`. The grayscale fixture is a documented extraction from a NIST
reference test record, not a local credential dump. These sources remain subject
to the original NIST notices. OpenPhysical does not claim NIST validation,
certification, sponsorship, or endorsement.

Provenance: [NBIS source and build adaptations](native/nbis/PROVENANCE.md).

## NIST Special Database 4

`tests/fixtures/sd04/` contains a curated collection from Craig I. Watson's
NIST Special Database 4, with original PNGs, losslessly decoded grayscale pixels,
and outputs generated using original NBIS reference tools. The dataset retains
its NIST data terms, separately from the license on OpenPhysical-authored code.
See the [dataset attribution and notice](tests/fixtures/sd04/NIST-DATA-NOTICE.txt)
and [fixture provenance](tests/fixtures/sd04/manifest.json).

## µnit, test-only

`tests/native/munit/` contains the pinned µnit test framework by Evan Nemerson,
under the MIT license. Its complete notice is retained in
[tests/native/munit/COPYING](tests/native/munit/COPYING). It is not linked into staged
runtime builds or shipped as a runtime package dependency.

## Managed runtime dependencies

Microsoft.Extensions.Logging.Abstractions and
Microsoft.Extensions.DependencyInjection.Abstractions retain their MIT terms:
https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

Additional build/test dependencies, including NUnit, CSharpier, and StyleCop analyzers,
are recorded in project/tool manifests and retain their own terms. Their inclusion
in development does not relicense their source or transfer their copyrights.

Pillow 12.2.0 is a fixture-import development dependency under MIT-CMU terms. It
decodes the source SD04 PNG files; ordinary builds and runtime packages consume
the committed grayscale fixtures independently of Pillow.

## Bounded Contribution Policy

[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) is adapted from Bounded Contribution Policy
version 2.0.3, https://github.com/OpenPhysical/BoundedContributionPolicy, distributed
under the MIT License. The adopted policy remains under those terms. Its license is:

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Contributor agreement template

[CONTRIBUTOR_ASSIGNMENT.md](CONTRIBUTOR_ASSIGNMENT.md) retains its attribution to
the Harmony Contributor Agreement Template 1.0, licensed under Creative Commons
Attribution 3.0 Unported: https://creativecommons.org/licenses/by/3.0/.
Template: https://www.harmonyagreements.org/docs/ha-combined-v1.
