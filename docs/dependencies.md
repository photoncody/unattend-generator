# Dependencies

Every dependency that ends up in the build, and why it is there. Exact versions of all direct and
transitive packages are in the committed `packages.lock.json` files.

## Runtime

| Dependency | Version | Why |
|---|---|---|
| unattend-generator (git submodule) | see README | The generator itself. |
| Newtonsoft.Json | 13.0.4 | Required by upstream, which reads its embedded JSON resources with it. Our own code uses `System.Text.Json`. |
| ASP.NET Core / .NET runtime | 10.0 | Part of the shared framework, not a NuGet package. |

## Test only

| Package | Version | Why |
|---|---|---|
| xunit.v3 | 4.0.2 | The test framework PLAN.md allows. Runs on Microsoft.Testing.Platform, so no VSTest adapter packages are needed. |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | Microsoft's in-memory host for ASP.NET Core integration tests. The web tests use it to check the real pipeline: the CSP header, antiforgery, form binding and file download. Writing an equivalent test host by hand would be more code to audit than this first-party package. |

## Build and container

| Item | Pin | Why |
|---|---|---|
| .NET SDK | 10.0.402 (`global.json`, later feature bands allowed) | Upstream supports net10.0. |
| `mcr.microsoft.com/dotnet/sdk:10.0` | by digest in `docker/Dockerfile` | Build stage: restore, test, publish. Includes git, which the submodule check needs. |
| `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` | by digest in `docker/Dockerfile` | Runtime stage: no shell or package manager, non-root by default. |
