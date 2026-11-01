# Releasing

Publishing a GitHub release automatically publishes the library and CLI to
NuGet.org under the OpenPhysical organization. No stored API key is needed.

1. Update the shared version in `Directory.Build.props` and date its changelog
   section. Keep library and CLI versions aligned.
2. Merge the reviewed change after CI passes, then create a signed `v<version>`
   tag at that commit.
3. Publish a GitHub release for that tag. `publish.yml` checks the version,
   runs the complete reusable CI workflow, and publishes its tested packages.
4. Verify the publishing workflow succeeds and both package versions appear on
   NuGet.org with OpenPhysical as owner.

Draft releases and tag pushes do not publish packages. A failed release workflow
can be rerun; existing package versions are skipped and never overwritten.

## Trusted publishing configuration

The NuGet policy is owned by **OpenPhysical** and permits pushing new packages
and versions for exactly `OpenPhysical.Biometrics.NBIS` and
`OpenPhysical.Biometrics.NBIS.Cli`. It trusts GitHub owner `OpenPhysical`, repository
`OpenPhysical.Biometrics.NBIS`, workflow `publish.yml`, and environment
`nuget-production`. Unlist/relist permission is unnecessary.

The matching GitHub environment permits release tags matching `v*` and defines
`NUGET_USER` as the NuGet username of the organization member who created the
policy. This username is not the organization name or an email address. The
member must remain active in the organization for the policy to remain usable.

See [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
