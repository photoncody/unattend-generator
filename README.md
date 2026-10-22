# UnattendHost

A self-hosted, auditable web front end for Christoph Schneegans'
[unattend-generator](https://github.com/cschneegans/unattend-generator) library. It generates
`autounattend.xml` files on your own LAN without depending on schneegans.de.

See [PLAN.md](PLAN.md) for the full design. **Status: phase 1 (working core, Windows 10/11 clients).**
Windows Server support, the script review panel, the HTTP API, saved presets and the CLI come in
later phases.

## Upstream version

The library is a git submodule at `external/unattend-generator`, consumed read-only.

| | |
|---|---|
| Pinned commit | `27237f82e9a8498d9c5bab7d398dc34b7682a1e8` |
| Commit date | 2026-10-11 |
| Pinned on | 2026-10-22 |

A test checks that the commit compiled into the build is the one listed here, so update this
table whenever the submodule moves.

## Repository layout

```
external/unattend-generator/  upstream library (submodule, never edited)
src/UnattendHost.Core/        HostConfig model, validation, mapping to upstream, post-processors
src/UnattendHost.Web/         Razor Pages UI
tests/UnattendHost.Tests/     unit, golden-file and web tests
presets/                      example presets (HostConfig JSON)
baselines/                    expected XML for each preset
docker/                       Dockerfile and compose file
docs/                         dependencies, acceptance checklist
```

## Build and test

Requires the .NET 10 SDK (`global.json` pins 10.0.402 or a later feature band) and git.

```bash
git clone --recurse-submodules https://github.com/photoncody/unattend-generator.git
```

```bash
dotnet test --solution UnattendHost.slnx
```

Run the UI locally at http://localhost:5191:

```bash
dotnet run --project src/UnattendHost.Web
```

The tests need the build to see git metadata (they check the upstream commit), so build from a
git clone, not from a source archive.

## Run with Docker

```bash
cd docker && UNATTENDHOST_BIND=192.168.1.10 docker compose up --build -d
```

Replace `192.168.1.10` with the host's LAN address. Without `UNATTENDHOST_BIND` the container
only listens on `127.0.0.1`. The image build:

- refuses to build if the upstream submodule is missing, modified, or not at the commit recorded
  in `HEAD` (override with `--build-arg EXPECTED_UPSTREAM_SHA=<sha>`),
- restores packages in locked mode and runs the whole test suite, so a failing test means no image,
- uses base images pinned by digest and runs as a non-root user on a read-only file system.

The app makes no outbound network calls, so blocking egress for the container is a reasonable
extra hardening step. `GET /healthz` reports the upstream commit and the app version.

## Presets

A preset is a `HostConfig` saved as JSON. The UI can load presets from `presets/` (in the
container: `/app/presets`) or from an uploaded file, and every result page can download the
configuration as a preset. Parsing is strict: unknown properties, misspelled enum values and
nulls are errors, and a preset that fails validation is reported instead of being loaded.

Settings that a preset does not mention take their default, and the defaults match upstream's
`Configuration.Default`. The example presets contain the placeholder password `change-me`;
secret placeholders (`{{secret:NAME}}`) arrive in phase 3.

To add a preset: put `presets/<name>.json` in place (lower-case letters, digits and hyphens),
create its baseline (next section), and commit both.

## Golden-file baselines

Each preset has a matching `baselines/<name>.xml` with the exact expected output. The tests
regenerate every preset and compare byte for byte, so the diff of `baselines/` shows exactly how
the output changed. The only normalization is the upstream commit hash that upstream writes into
the `<Extensions><Build>` element, which is replaced by `UPSTREAM-COMMIT`.

When a change alters output on purpose, rewrite the baselines and commit them with the change:

```bash
UNATTENDHOST_UPDATE_BASELINES=1 dotnet test --solution UnattendHost.slnx
```

## Updating upstream

`tools/bump-upstream.sh` arrives in phase 3. Until then, by hand:

1. `git -C external/unattend-generator fetch` and review `git -C external/unattend-generator log --oneline HEAD..origin/master`.
2. `git -C external/unattend-generator checkout <new-sha>`.
3. Run the tests. Golden-file tests fail if output changed; that is expected.
4. Regenerate the baselines (above) and review `git diff baselines/`.
5. Update the table in "Upstream version", then commit the submodule pointer, baselines and README together.

## Phase 1 verification notes

These items marked **Verify** in PLAN.md were checked against the real upstream source:

- **Target framework.** Upstream multi-targets `net8.0;net9.0;net10.0`. UnattendHost targets
  `net10.0`, the current LTS release (.NET 8 support ends in November 2026).
- **Embedded resources.** All files in upstream's `resource/` folder that the generator loads are
  `EmbeddedResource` items in `UnattendGenerator.csproj`, and `new UnattendGenerator()` works from
  the web app. Covered by `UpstreamSmokeTests`.
- **`Example.cs` and its `Main` method.** No conflict: upstream builds as a class library, where a
  static `Main` is an ordinary method. No MSBuild workaround was needed.
- **Nondeterministic output.** Upstream's C# uses no timestamps, GUIDs or random numbers. The only
  build-dependent value is the upstream commit hash, which is normalized in baselines.
- **Where scripts end up** (input for the phase 2 script review panel). Upstream writes scripts as
  `<File path="...">` elements inside its own `<Extensions>` element and extracts them during the
  specialize pass with a PowerShell command in `RunSynchronous`; short commands go directly into
  `RunSynchronous` (windowsPE, specialize) and `FirstLogonCommands` (oobeSystem). In scripted setup
  mode, `pe.cmd` is written in the windowsPE pass by a series of `cmd.exe /c >>X:\pe.cmd (echo ...)` commands.
- **`ImageInstall`** (input for phase 2 image selection). Upstream never writes an `ImageInstall`
  element. Standard setup picks the edition through `UserData/ProductKey`; scripted setup picks the
  image inside `pe.cmd` with `dism /Apply-Image /Name:` or `/Index:`.

One upstream rule shaped the model: unless `keepSensitiveFiles` is set, upstream deletes the answer
file at first logon and therefore requires automatic logon as an administrator when accounts are
created unattended. `keepSensitiveFiles` is exposed in the accounts section for that reason.
