# UnattendHost — Implementation Plan

A self-hosted, auditable web front end for Christoph Schneegans' `unattend-generator` library, with added Windows Server support, saved presets, and an API for automated image builds.

This document is the brief for the implementing session. Read all of it before writing code. Items marked **Verify** are assumptions the planner could not confirm; check them against the real source or real media before relying on them.

---

## 1. Goals and non-goals

### Goals

1. **Independence.** Generate `autounattend.xml` files without depending on schneegans.de being up.
2. **Auditability.** Every line of code that influences the output is either in this repo or in a pinned, reviewed commit of the upstream library. No CDNs, no runtime network calls, minimal dependencies.
3. **Server support.** Windows Server 2022 and 2025 (Core and Desktop Experience, Standard and Datacenter), alongside Windows 10/11.
4. **Presets.** Named, saved configurations that can be loaded in the UI or fetched over HTTP by an automated build pipeline.
5. **Change visibility.** When upstream is bumped, it must be obvious exactly how the generated XML changes for each preset.

### Non-goals

- Pixel-perfect clone of the schneegans.de UI.
- Public internet exposure. This runs on a LAN. Do not add features (user accounts, rate limiting, etc.) that only make sense for public hosting.
- Modifying the upstream library's code. All behavior changes happen in this repo (see §5).

---

## 2. Working rules for the implementing session

Create a `CLAUDE.md` at the repo root containing these rules, then follow them:

- **Never edit anything under `external/`.** Upstream is consumed read-only. If upstream behavior must change, do it in a post-processor (§5.3).
- **No new NuGet packages** beyond what upstream itself requires plus a test framework (xUnit) without a written justification in `docs/dependencies.md`. Prefer the BCL.
- **No external network resources at runtime or in the browser.** No CDN scripts, fonts, or stylesheets. Everything is served from the container.
- **Lock dependencies.** Enable `RestorePackagesWithLockFile` and commit `packages.lock.json`. Pin Docker base images by digest.
- **Golden-file tests must pass** before committing (§7). If a change intentionally alters output, regenerate baselines in the same commit and say so in the commit message.
- **Don't guess at the upstream API.** Read `external/unattend-generator/*.cs` and `modifier/` to learn the actual `Configuration` record and its settings types before writing mapping code.
- Keep the code boring and readable. Someone will audit this by reading it.

---

## 3. Repository layout

```
/
├── CLAUDE.md
├── PLAN.md                        # this file
├── README.md                      # how to build, run, update upstream
├── NOTICE                         # upstream MIT attribution
├── external/
│   └── unattend-generator/        # git submodule, pinned SHA
├── src/
│   ├── UnattendHost.Core/         # wrapper logic: form model, mapping, post-processors, presets, audit
│   ├── UnattendHost.Web/          # ASP.NET Core Razor Pages UI + HTTP API
│   └── UnattendHost.Cli/          # offline generation from a preset file (phase 3)
├── tests/
│   └── UnattendHost.Tests/
├── presets/                       # example/default presets (JSON), checked in
├── baselines/                     # expected XML output per preset, checked in
├── tools/
│   └── bump-upstream.sh           # upstream update workflow (§8)
├── docker/
│   ├── Dockerfile
│   └── compose.yaml
└── docs/
    ├── dependencies.md
    ├── server-support.md
    └── option-applicability.md
```

---

## 4. Upstream integration

- Add `https://github.com/cschneegans/unattend-generator` as a git submodule at `external/unattend-generator`, pinned to the current `master` HEAD at the time of setup. Record the SHA and date in `README.md`.
- Reference it from `UnattendHost.Core` via `<ProjectReference>` to `UnattendGenerator.csproj`.
- **Target framework:** match the `TargetFramework` in upstream's `UnattendGenerator.csproj`. Don't pick a .NET version independently.
- **Verify:** upstream ships embedded resources (the `resource/` folder) that the generator loads at runtime. Confirm they're embedded in the referenced assembly and that `new UnattendGenerator()` works from the web project. Add a smoke test for this.
- **Verify:** upstream's `Example.cs` contains a `Main` method. Confirm it doesn't cause a build conflict when the project is referenced as a library. If it does, solve it with MSBuild properties in our project, not by editing upstream.
- Copy upstream's `LICENSE.txt` content into `NOTICE` with attribution.

Basic usage pattern (from upstream `Example.cs`):

```csharp
UnattendGenerator generator = new();
XmlDocument xml = generator.GenerateXml(Configuration.Default with { /* settings */ });
byte[] bytes = UnattendGenerator.Serialize(xml);
```

Note that `GenerateXml` returns a mutable `XmlDocument` before serialization. This is the extension point for Server support.

---

## 5. Core design (`UnattendHost.Core`)

### 5.1 Pipeline

```
FormModel / Preset JSON
   → Validator
   → ConfigurationMapper      → upstream Configuration
   → UnattendGenerator.GenerateXml
   → Post-processor chain     (XmlDocument → XmlDocument)
   → Serializer
   → AuditReport              (extracted scripts, flags)
```

Expose one entry point, e.g. `GenerationService.Generate(HostConfig config) → GenerationResult { byte[] Xml; AuditReport Audit; IReadOnlyList<string> Warnings; }`. The web UI, the API, the CLI and the tests all call this, so they can't drift apart.

### 5.2 `HostConfig` (our model)

Our own serializable record. It wraps the upstream settings we expose and adds our own fields:

- `TargetOs`: `Windows10 | Windows11 | Server2022 | Server2025`
- `ServerOptions` (only valid when the target is Server; see §6)
- The client-facing settings, mirroring upstream's settings groups

Keep it JSON-serializable with `System.Text.Json`. This is the preset format, so add a `SchemaVersion` field from day one.

**List-type options** (locales, keyboards, geo IDs, bloatware entries, etc.) should be populated from upstream's own data via `generator.Lookup<T>` and the underlying collections, not hardcoded. This gives automatic parity on those lists when upstream is bumped.

### 5.3 Post-processors

Interface: `IXmlPostProcessor { bool AppliesTo(HostConfig c); void Apply(XmlDocument doc, HostConfig c); }`

- Run in a fixed, explicit order defined in one place.
- Each one is small, single-purpose, and individually unit-tested against a fixed input XML.
- Use the unattend namespace (`urn:schemas-microsoft-com:unattend`) correctly. Write a small helper for "find or create component X in pass Y" with the right `processorArchitecture`, `publicKeyToken`, `language` and `versionScope` attributes. Match the attributes upstream already emits.

### 5.4 Option applicability

Create `docs/option-applicability.md` with a table classifying every upstream setting the UI exposes as **Client only**, **Server only**, **Both**, or **Untested on Server**. Back it with a code-level map that the UI and validator use.

- In Server mode, the UI hides Client-only options and shows a warning badge on Untested ones.
- The validator rejects a config that sets a Client-only option with a Server target, so presets can't sneak them in.
- Expected Client-only (**Verify** each against upstream): TPM/Secure Boot/RAM requirement bypass, BypassNRO / Microsoft-account OOBE tweaks, Store-app bloatware removal, Copilot/Widgets/consumer-feature tweaks, device-encryption-for-Home settings, client edition product keys.

---

## 6. Windows Server support

Document all of this in `docs/server-support.md`.

### 6.1 Image selection

Server ISOs contain multiple images in `install.wim`, so the answer file must say which one to install. Implement a post-processor that writes, in the `windowsPE` pass under `Microsoft-Windows-Setup/ImageInstall/OSImage/InstallFrom/MetaData`:

- Either `Key=/IMAGE/INDEX` with a numeric value, or `Key=/IMAGE/NAME` with the image name.
- The UI offers both. Presets should default to `/IMAGE/NAME`, because names survive index reordering across ISO releases.

**Verify:** the exact image NAME strings for 2022 and 2025 media (Core vs Desktop Experience, Standard vs Datacenter). Do not hardcode names from memory. The README should tell the user to run this against their actual ISOs and paste the results into `presets/`:

```powershell
dism /Get-WimInfo /WimFile:D:\sources\install.wim
```

Also check whether upstream already emits an `ImageInstall` block for client installs. If it does, the post-processor must replace it, not duplicate it.

### 6.2 Product keys

- Support three modes: **none** (rely on image selection), **GVLK** (KMS client key), or **custom key**.
- Source GVLKs from Microsoft Learn's "KMS client activation and product keys" page. Put them in a single constants file with the source URL and retrieval date in a comment. Do not rely on model memory for key values.
- **Verify** by test install whether setup still prompts for an edition when an image is selected but no key is given, and adjust `UserData/ProductKey` / `WillShowUI` handling to avoid the prompt.

### 6.3 Server-specific options (Phase 2)

Each of these is its own post-processor:

| Option | Mechanism (**Verify** component names/settings against Microsoft's Unattended Windows Setup Reference) |
|---|---|
| Don't open Server Manager at logon | `Microsoft-Windows-ServerManager-SvrMgrNc` → `DoNotOpenServerManagerAtLogon` (specialize) |
| Disable IE Enhanced Security Config (2022 only) | `Microsoft-Windows-IE-ESC` → `IEHardenAdmin` / `IEHardenUser` |
| Enable Remote Desktop + firewall rule | `Microsoft-Windows-TerminalServices-LocalSessionManager` → `fDenyTSConnections=false`; `Networking-MPSSVC-Svc` → firewall group for Remote Desktop |
| Built-in Administrator password | `oobeSystem` → `UserAccounts/AdministratorPassword` |
| Time zone, computer name | Reuse upstream settings if they behave correctly on Server; otherwise post-process |

**Accounts:** Server OOBE differs from client OOBE. **Verify** by test install that upstream's account-creation output works on Server, and that it doesn't leave setup waiting on a prompt.

### 6.4 Deferred: domain join

`Microsoft-Windows-UnattendedJoin` support is Phase 4. Join credentials end up readable in the XML, so the UI must warn about this, and presets must support secret placeholders (§9.2) for them.

---

## 7. Auditing features

These are core features, not extras.

### 7.1 Script review panel

The generator embeds PowerShell, cmd and registry content in the XML. **Verify** where it puts it by inspecting real output; it may use `RunSynchronous` commands, an embedded file-extraction mechanism, or both.

The `AuditReport` must:
- Extract every embedded script and command into a list (name/path, pass, content).
- Show them in the UI after generation, syntax-highlighted with a small local highlighter or none at all. No CDN.

### 7.2 Heuristic flags

Scan the generated XML and the extracted scripts, and raise warnings for:
- Any URL (`http://`, `https://`, UNC paths) not on an allowlist in config. Upstream may legitimately include its own URL in comments or namespaces; allowlist those specifically.
- Network-fetch patterns: `Invoke-WebRequest`, `iwr`, `Invoke-RestMethod`, `irm`, `DownloadString`, `DownloadFile`, `Start-BitsTransfer`, `curl`, `wget`, `certutil -urlcache`, `bitsadmin`.
- Encoded-command execution: `-EncodedCommand`, `-enc`, `FromBase64String` combined with `Invoke-Expression`/`iex`.

Warnings appear in the UI and the API response. They are informational and don't block generation, except under the CLI flag `--strict`.

### 7.3 Golden-file baselines

- Each preset in `presets/` has a matching `baselines/<preset>.xml`.
- A test regenerates every preset and compares byte-for-byte after normalizing anything nondeterministic. **Verify** whether upstream output contains timestamps or other nondeterministic content.
- A `--update-baselines` path (test setting or CLI command) rewrites them intentionally.

This is what makes upstream bumps reviewable: the diff of `baselines/` *is* the behavior change.

---

## 8. Upstream update workflow

`tools/bump-upstream.sh <new-sha>` should:

1. Print `git log --oneline <old>..<new>` and a `--stat` diff for the submodule.
2. Check out the new SHA in the submodule.
3. Build and run the tests (they're expected to fail if output changed).
4. Regenerate baselines and print the `git diff baselines/`.
5. Run the §7.2 heuristic scan across all presets and print any *new* flags compared with the old SHA.
6. Stop there. The human reviews and commits. The script never commits.

Document this in `README.md`.

---

## 9. Web app (`UnattendHost.Web`)

### 9.1 UI

- ASP.NET Core **Razor Pages**, server-rendered, plain HTML forms. Vanilla JS only where needed (show/hide sections, Server-mode toggling), served from `wwwroot`. No front-end framework or build step.
- Send a strict CSP header: `default-src 'self'`, no inline script, so the JS goes in files.
- Page flow:
  1. **Generator page**: preset picker (load), target OS selector at the top (drives which sections and options appear), collapsible sections mirroring upstream's settings groups.
  2. **Result page**: download button, XML preview, script review panel, warnings, and a "save as preset" form.
- Light/dark via `prefers-color-scheme`.

### 9.2 Presets

- Stored as JSON files in a mounted directory (default `/data/presets`), seeded from the repo's `presets/` on first run if empty.
- **Secret placeholders:** string fields may contain `{{secret:NAME}}`, resolved at generation time from files in a mounted secrets directory (`/run/secrets/NAME`, Docker-secrets style). Presets can then be committed or shared without passwords in them. A missing secret is a hard error, never an empty string.
- Validate on load. A preset that fails validation shows an error and isn't silently fixed.

### 9.3 HTTP API

- `GET  /api/presets` — list preset names and metadata.
- `GET  /api/presets/{name}` — preset JSON (with placeholders unresolved).
- `GET  /api/presets/{name}/autounattend.xml` — generated XML with secrets resolved. Audit warnings go in a response header (count) and a JSON sibling endpoint (`.../audit`).
- `POST /api/generate` — `HostConfig` JSON in, XML out.
- `GET  /healthz` — returns 200 plus the upstream SHA and app version, so the build pipeline can log which generator version produced each image.
- **Optional bearer-token auth** for `/api/*`, enabled by env var `UNATTENDHOST_API_TOKEN`, off by default. Because XML endpoints resolve secrets, the README should recommend enabling it if secrets are in use.

### 9.4 Intended consumer

A weekly golden-image build job on a Hyper-V lab host (Server 2025, Server 2022 and Windows 11 templates). It will call something like:

```powershell
Invoke-WebRequest -Uri "http://<host>:8080/api/presets/server2025-dc-desktop/autounattend.xml" `
  -Headers @{ Authorization = "Bearer $token" } -OutFile autounattend.xml
```

Design the API and preset naming with this in mind: stable URLs, clear errors, and no HTML error pages on API routes.

---

## 10. CLI (`UnattendHost.Cli`) — Phase 3

A fallback for when the container is down, and a tool for CI/baselines:

```
unattendhost generate --preset presets/server2025-dc-desktop.json --secrets ./secrets --out autounattend.xml [--strict]
unattendhost audit    --xml autounattend.xml
unattendhost update-baselines
```

It uses the same `GenerationService` as the web app. Publish it as a self-contained single-file binary for `win-x64` and `linux-x64` from the Dockerfile's build stage or a separate script.

---

## 11. Container

### `docker/Dockerfile`

- Multi-stage: SDK image for restore/test/publish, then ASP.NET runtime image (prefer a chiseled/distroless variant if one is available for the chosen .NET version). **Pin both by digest.**
- The build stage runs the test suite, so a failing build produces no image.
- Clone with submodules. Fail the build if the submodule is missing or at an unexpected SHA. Pass the expected SHA as a build arg defaulting to the one recorded in the repo.
- Runtime: non-root user, `EXPOSE 8080`, `HEALTHCHECK` against `/healthz`.

### `docker/compose.yaml`

- `read_only: true`, `tmpfs: /tmp`, `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]`.
- Volumes: `./data/presets:/data/presets`, secrets directory mounted read-only.
- Port bound to a specific LAN interface in the example (`"192.168.x.x:8080:8080"`), with a comment explaining why it shouldn't be exposed publicly.
- The app makes no outbound network calls at runtime. The README can note that host-level egress blocking for this container is a reasonable hardening step.

---

## 12. Testing

- **Unit tests:** each post-processor against fixed input XML; validator rules (including Client-only-on-Server rejection); secret placeholder resolution, including the missing-secret error; audit heuristics against crafted samples.
- **Golden-file tests:** §7.3.
- **Well-formedness:** every generated document parses, and has exactly one of each component per pass/architecture. A duplicate-component check catches post-processors that append when they should replace.
- **Upstream smoke test:** `Configuration.Default` generates without throwing.
- **Manual VM acceptance checklist** in `docs/acceptance.md`. A test install in a Hyper-V VM for each shipped preset, confirming: no setup prompts, the correct edition is installed, accounts work, the computer name is applied, and each Server-specific option took effect. The implementer can't run these, but must write the checklist and list each **Verify** item from this plan that the checklist resolves.

---

## 13. Phases

The scope question (full parity vs. a focused UI) wasn't settled. The plan starts focused and is structured so that reaching parity is mechanical work.

**Phase 1 — Working core (client)**
Submodule + build, `HostConfig`, mapper, `GenerationService`, Razor UI covering: language/region/keyboard, edition/product key, partitioning, computer name, user accounts, requirement bypasses, bloatware removal, custom scripts. Result page with download and XML preview. Docker image. Golden-file test harness with 1–2 client presets.

**Phase 2 — Server support + auditing**
Target OS selector, applicability map and docs, image selection, GVLK handling, Server-specific post-processors, script review panel, heuristic flags. Server presets for 2022/2025 Core and Desktop (image names left as placeholders until verified).

**Phase 3 — Presets, API, CLI**
Preset storage, secret placeholders, HTTP API with optional token, `/healthz` with upstream SHA, CLI, `bump-upstream.sh`.

**Phase 4 — Parity and extras**
Remaining upstream settings groups, domain join (with warnings), any other gaps found by comparing against the live schneegans.de site's option list.

Each phase ends with: tests passing, baselines committed, README updated, and a short summary of which **Verify** items were resolved and how.

---

## 14. Definition of done (overall)

- `docker compose up` on a fresh clone serves a working generator with no outbound network access.
- Every preset in `presets/` has a committed baseline, and the test suite passes.
- A Server 2025 Desktop Experience preset and a Windows 11 preset each install unattended in a VM per `docs/acceptance.md`.
- Bumping upstream with `tools/bump-upstream.sh` produces a readable report of code changes, output changes, and new audit flags.
- `README.md` lets someone who has never seen the project build it, run it, add a preset, and update upstream.
