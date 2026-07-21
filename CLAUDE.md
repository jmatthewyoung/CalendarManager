# CalendarManager

Clean Architecture .NET solution (generated from [Clean.Architecture.Solution.Template](https://github.com/jasontaylordev/CleanArchitecture)) with a .NET Aspire AppHost and an Angular front end under `src/Web/ClientApp`.

## Run

```bash
dotnet run --project src/apphost
```

Starts the .NET Aspire AppHost and opens the Aspire dashboard with app URLs and logs.

## Build

```bash
dotnet build CalendarManager.slnx
```

NuGet audit (`NU1902`/`NU1903`) is treated as an error on this solution — a newly vulnerable transitive package will fail the build, not just warn.

## Test

```bash
dotnet test
```

## Front end (Angular, `src/Web/ClientApp`)

`npm run build` (and `start`) first runs `generate-api` (nswag), which regenerates `src/app/web-api-client.ts` from `src/Web/wwwroot/openapi/v1.json`. That OpenAPI doc is emitted during `dotnet build` of the `Web` project, so build the .NET solution at least once before building/serving the Angular app standalone.

## Dependency vulnerabilities

- **.NET**: transitive packages with known CVEs (MessagePack, Microsoft.OpenApi, OpenTelemetry.Api, System.Security.Cryptography.Xml, etc.) are pinned to patched versions in `Directory.Packages.props` via `CentralPackageTransitivePinningEnabled`. When `dotnet build`/`restore` starts failing on NU1902/NU1903 again, check the flagged package + GHSA advisory for its `first_patched_version` and add/bump a `<PackageVersion>` entry for it there.
- **npm** (`src/Web/ClientApp`): run `npm audit`. `npm audit fix` handles most; Angular tooling (`@angular/*`, `@angular-devkit/build-angular`) sometimes needs its version bumped by hand in `package.json` beyond what `audit fix` alone will do, since its nested `vite`/`undici`/`postcss` versions ship pinned inside that package. As of the last pass, 4 moderate findings remain with no upstream fix (`uuid` <11.1.1 pulled in via `webpack-dev-server` → `sockjs`, dev-server only, not shipped in production builds).
