<p align="center">
  <img src="docs/assets/dotdet-readme-cover.png" alt="DotDet logo" width="320" />
</p>

# DotDet

> **v0.1 Preview** - DotDet is under active calibration. Review findings and
> recommendations with engineering judgment before using them as release gates.

DotDet is a production-readiness analysis workbench for ASP.NET Core solutions.
It connects static findings to source evidence, confidence, detection method,
Microsoft guidance, and a practical remediation path. The interface is designed
for developers and technical leads reviewing whether a solution is ready to
operate in production.

DotDet is useful for ASP.NET Core teams that want a structured first pass across
architecture boundaries, dependency injection, EF Core migrations,
security/configuration, and API readiness. It complements code review and
specialized security or runtime tooling; it does not replace them.

## Current capabilities

### Analysis sources

| Source | Access | Analysis fidelity |
| --- | --- | --- |
| Bundled sample | Public, rate-limited | Semantic when Roslyn/MSBuild loading succeeds |
| ZIP upload | Authenticated | Safe syntax analysis in the web process |
| Public GitHub repository | Authenticated | Safe syntax analysis in the web process |
| Private GitHub repository | Authenticated, explicit repository permission | Safe syntax analysis in the web process |
| Local path | Development only | Semantic when Roslyn/MSBuild loading succeeds |

Untrusted ZIP and GitHub content does not cross the in-process MSBuild execution
boundary. Full semantic analysis for those sources is deferred until DotDet has
an isolated analysis worker or container boundary.

### Analyzer categories

- **Architecture** - project dependencies, layer inference, boundary violations,
  cycles, and architecture-map evidence.
- **Dependency Injection** - constructor dependencies, registrations, duplicate
  registrations, lifetime risks, framework-provided service exclusions, and
  common registration patterns such as MediatR.
- **EF Core and migrations** - `DbContext` and entity inspection, key patterns,
  migration operations, destructive-change risk, and raw SQL indicators.
- **Security and configuration** - configuration secrets, connection-string
  sensitivity, CORS, HTTPS, JWT setup, and authentication middleware.
- **API readiness** - API/Web UI intent, controllers and minimal APIs, OpenAPI,
  exception handling, health checks, logging, and validation signals.

### Workbench features

- Production-readiness and category scoring with confidence-aware findings
- Source-linked findings, Code Explorer, and an Engineering Guide
- Architecture map and project dependency graph
- Rule Explorer with detection logic, examples, and Microsoft documentation
- Finding dispositions and repository-scoped suppressions
- Saved, user-scoped analysis history without retained raw source
- JSON, Markdown, and standalone printable HTML reports
- GitHub OAuth login and optional private-repository access
- Rate limits, per-caller concurrency control, and analysis timeouts

## Security and privacy

DotDet keeps GitHub access tokens on the backend and protects stored repository
tokens with ASP.NET Core Data Protection. Tokens are not returned to the browser
or written into reports and history. Live analysis can include source preview for
the current session, while history, browser persistence, and default exports are
sanitized and do not retain full source files. Server and temporary paths are
removed from API report data, including root-cause keys.

Uploaded and downloaded archives are size-limited, entry-limited, checked for
unsafe paths, extracted into temporary DotDet-owned directories, and cleaned up
after analysis. Local filesystem path analysis is blocked outside the
Development environment.

Read [Security](docs/security.md) and
[Private GitHub repository security](docs/private-github-security.md) before
enabling private-repository access.

Review the public-preview [Terms of Use](docs/terms.md) and
[Privacy Policy](docs/privacy.md) before sharing reports or analyzing code you
do not personally own.

## Engine maturity

The engine is in **Preview / Calibration**. DotDet combines Roslyn syntax and
semantic APIs, project/configuration inspection, and conservative heuristics.
Fidelity metadata states whether an analysis used semantic coverage, degraded
project loading, syntax fallback, or safe syntax analysis. Rules are being
calibrated against realistic ASP.NET Core repositories, including eShopOnWeb and
SchemaArchitect, with a preference for fewer high-confidence findings over broad
but noisy coverage.

See [Engine maturity](docs/engine-maturity.md),
[Rule quality principles](docs/rule-quality.md), and
[Calibration](docs/calibration.md).

## Interface preview

The repository currently includes the product logo and landing-page media but no
maintained workbench screenshot set. Screenshots and a shareable sample report
will be added as the Preview UI stabilizes. You can generate the current report
experience by running the bundled sample analysis.

## Repository layout

```text
backend/Forge.Api          ASP.NET Core API and analysis engine
backend/Forge.Api.Tests    Backend regression and analyzer tests
frontend                   React/TypeScript workbench and Playwright tests
samples                    Six built-in calibration solutions across readiness levels
docs                       Public product, security, and analysis documentation
Forge.slnx                 .NET solution
```

## Local development

### Prerequisites

- .NET 9 SDK
- Node.js and npm compatible with the versions in `frontend/package-lock.json`
- A GitHub OAuth application for authenticated workflows

Configure development OAuth credentials with .NET user secrets or environment
variables. Do not commit them:

```powershell
dotnet user-secrets set "Authentication:GitHub:ClientId" "<client-id>" --project backend/Forge.Api/Forge.Api.csproj
dotnet user-secrets set "Authentication:GitHub:ClientSecret" "<client-secret>" --project backend/Forge.Api/Forge.Api.csproj
```

Use these local GitHub OAuth application values:

```text
Homepage URL:              http://127.0.0.1:5173
Authorization callback:   http://127.0.0.1:5241/signin-github
```

Run the backend:

```powershell
dotnet restore backend/Forge.Api/Forge.Api.csproj
dotnet run --project backend/Forge.Api/Forge.Api.csproj --launch-profile http
```

Run the frontend in another terminal:

```powershell
cd frontend
npm install
npm run dev
```

The default development URLs are:

- Frontend: `http://127.0.0.1:5173`
- API: `http://127.0.0.1:5241`
- Health check: `http://127.0.0.1:5241/health`

Set `VITE_DOTDET_API_URL` when the frontend should use another API origin.
`VITE_FORGE_API_URL` remains a compatibility fallback.

## Validation

Run backend validation:

```powershell
dotnet build backend/Forge.Api/Forge.Api.csproj
dotnet test backend/Forge.Api.Tests/Forge.Api.Tests.csproj
dotnet list Forge.slnx package --vulnerable --include-transitive
```

Run frontend validation:

```powershell
cd frontend
npm run lint
npm run build
npm run test:e2e
npm audit --omit=dev
```

## Known limitations

- Untrusted ZIP and GitHub analysis uses safe syntax mode until an isolated
  semantic-analysis worker exists.
- DI analysis is not a runtime service-provider simulation.
- Security/configuration and migration findings are static indicators, not proof
  of runtime behavior or operational safety.
- Architecture intent is inferred from references, source, and conventional
  project naming.
- GitHub analysis currently targets the default branch; branch and pull-request
  analysis are not implemented.

The complete list is maintained in
[Known limitations](docs/known-limitations.md).

## Roadmap

Near-term work focuses on calibration, an isolated worker for semantic analysis
of untrusted repositories, branch and pull-request workflows, host-scoped DI
reasoning, and environment-aware configuration severity. See the
[public roadmap](docs/roadmap.md).

## Contributing

Issues and focused pull requests are welcome. Before proposing an analyzer rule,
read [Rule quality principles](docs/rule-quality.md). Include representative
positive and negative fixtures, explain applicability and confidence, and add a
regression test for any false positive being fixed. Run the validation commands
above before submitting changes.

For support or responsible disclosure, use the guidance in
[Security](docs/security.md). General product issues can be opened in the
[DotDet GitHub repository](https://github.com/cezarpedroso/dotdet/issues).

## License

No public license file is currently included. Until a license is added, the
repository should not be assumed to grant rights beyond those provided by its
owner. A release license should be selected before broader distribution or
external contribution.

## Documentation

Start with the [DotDet documentation index](docs/README.md):

- [Security](docs/security.md)
- [Private GitHub repository security](docs/private-github-security.md)
- [Terms of Use](docs/terms.md)
- [Privacy Policy](docs/privacy.md)
- [Engine maturity](docs/engine-maturity.md)
- [Rule quality principles](docs/rule-quality.md)
- [Calibration](docs/calibration.md)
- [Known limitations](docs/known-limitations.md)
- [Roadmap](docs/roadmap.md)
