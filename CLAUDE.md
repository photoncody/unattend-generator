# UnattendHost — working rules

PLAN.md is the design brief. These rules apply to every change:

- **Never edit anything under `external/`.** Upstream is consumed read-only. If upstream behavior
  must change, do it in a post-processor (`src/UnattendHost.Core/PostProcessing`, ordered in
  `PostProcessorChain`).
- **No new NuGet packages** beyond what upstream itself requires plus the test framework, unless
  `docs/dependencies.md` gets a written justification first. Prefer the BCL.
- **No external network resources at runtime or in the browser.** No CDN scripts, fonts or
  stylesheets; no inline scripts (the CSP forbids them). Everything is served from the container.
- **Lock dependencies.** `RestorePackagesWithLockFile` is on; commit `packages.lock.json`. Docker
  base images are pinned by digest.
- **Golden-file tests must pass before committing.** If a change intentionally alters output,
  regenerate baselines in the same commit (`UNATTENDHOST_UPDATE_BASELINES=1 dotnet test --solution UnattendHost.slnx`)
  and say so in the commit message.
- **Don't guess at the upstream API.** Read `external/unattend-generator/*.cs` and `modifier/`
  before writing mapping code. Upstream types are referenced as `U.` in `ConfigurationMapper`.
- Keep the code boring and readable. Someone will audit this by reading it.

Practical notes:

- Test: `dotnet test --solution UnattendHost.slnx` (xUnit v3 on Microsoft.Testing.Platform, opted in via `global.json`).
- `src/Directory.Build.props` holds shared settings; it is deliberately not at the repo root so it
  does not apply to the upstream project.
- `HostConfig` is the preset format. Adding a property: give it upstream's default, map it in
  `ConfigurationMapper`, add it to `GeneratorForm` and the page, and make sure existing baselines
  do not change.
