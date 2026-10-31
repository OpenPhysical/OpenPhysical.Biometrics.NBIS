# Repository conventions

- Default branch: `master`.
- Follow Semantic Versioning 2.0.0 and the compatibility policy in CONTRIBUTING.md. Library and CLI versions stay aligned. Before 1.0, breaking changes require a minor increment; patch releases remain compatible. Do not overwrite published versions.
- Maintain `CHANGELOG.md`: record user-visible changes under Unreleased as work is done; assign a version and date only when releasing. Document native adaptations in the provenance record as well.
- Use the repository-pinned CSharpier tool for C# and project XML: `dotnet tool restore`, `dotnet csharpier format .`, then `dotnet csharpier check .`. Directory.Build.props references CSharpier.MsBuild 1.3.0 for every project. Do not substitute dotnet format or reformat vendored NIST or µnit source.
- Write XML documentation with public API changes, including meaningful summaries, every parameter/type parameter, return values, relevant exceptions, and coordinate/unit/ownership constraints. Keep concrete engine method documentation self-contained for packaged IntelliSense.
- All warnings are errors. Public API documentation analyzers and XML generation are mandatory. Do not suppress CS1591, disable documentation rules, or add blanket pragmas/NoWarn entries.
- Use Microsoft.Extensions.Logging for managed diagnostics. Never log biometric content, provenance, filenames, scores, or exception payloads.
- µnit owns native algorithm parity and native safety. NUnit owns managed contracts, conversion, logging/DI, serialization, and interop. Avoid duplicating native oracle tests in managed code.
- Run .NET builds/tests serially. Preserve upstream hashes and notices. Stage only unsanitized native assets built without test hooks.

- Original project code is GPL-2.0-or-later; keep SPDX headers and project notices. Preserve NIST public-domain, µnit MIT, policy MIT, and agreement-template attribution. Follow CONTRIBUTING.md for contributor-rights coverage; do not claim a CLA service is configured until verified.

- Write examples as readable engineering walkthroughs. Explain intent, ownership, units, and costly operations in plain English. Use LINQ for projections and queries, materialize expensive sequences before reuse, and use loops for side effects.
