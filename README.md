# DotDet

DotDet is a .NET engineering analysis platform. It analyzes a local `.sln`/`.slnx` file or an uploaded zipped .NET solution and returns a production-readiness report for architecture, dependency injection, EF Core, security/configuration, and API readiness.

The MVP is intentionally stateless: the ASP.NET Core API analyzes the submitted solution and returns a JSON report directly to the React dashboard.

## Tech Stack

- Backend: .NET 9, ASP.NET Core Web API, C#
- Frontend: React, TypeScript, Vite
- Styling: Tailwind CSS
- Analysis: Roslyn syntax and semantic APIs, MSBuildWorkspace where available, project-file/config-file scanning
- Storage: in-memory request/response only

## Project Structure

```text
backend/Forge.Api      ASP.NET Core API and analyzer services
frontend               React dashboard
Forge.slnx             Solution file for the backend project
```

## Run The Backend

```powershell
dotnet restore backend/Forge.Api/Forge.Api.csproj
dotnet run --project backend/Forge.Api/Forge.Api.csproj --launch-profile http
```

The API listens on `http://localhost:5241` with the default launch profile.

Available endpoints:

- `POST /api/analysis/analyze-path`
- `POST /api/analysis/analyze-zip`
- `GET /health`

Analyze a local solution path:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:5241/api/analysis/analyze-path `
  -Method Post `
  -ContentType 'application/json' `
  -Body '{"solutionPath":"C:\\src\\Acme\\Acme.sln"}'
```

Analyze a zipped solution with multipart form field `file`:

```powershell
curl.exe -F "file=@C:\src\Acme.zip" http://localhost:5241/api/analysis/analyze-zip
```

## Run The Frontend

```powershell
cd frontend
npm install
npm run dev
```

The dashboard runs at `http://localhost:5173` and calls `http://localhost:5241` by default.

To point the dashboard at a different API URL:

```powershell
$env:VITE_DOTDET_API_URL='http://localhost:5000'
npm run dev
```

`VITE_FORGE_API_URL` is still accepted as a compatibility fallback.

## Report Shape

The API returns:

- `solutionName`
- `analyzedAt`
- `overallScore`
- `categoryScores`
- `issues`
- `projectGraph`

Issue severities are `Info`, `Warning`, `Error`, and `Critical`. Scoring starts at 100 and subtracts:

- Critical: 15
- Error: 8
- Warning: 4
- Info: 1

Scores never go below 0. Category scores are calculated from issues in that category.

## MVP Analyzers

Architecture:

- Project references and dependency graph
- Circular project dependencies
- Domain references to Infrastructure/Web/API/EF Core/ASP.NET Core
- Application references to Infrastructure
- Lower layers referencing API/Web projects

Dependency Injection:

- Constructor-injected services
- Registrations in `Program.cs` or `Startup.cs`
- Symbol-aware type matching when MSBuild/Roslyn can load the solution
- Source-defined composition methods such as `AddApplication()` or `AddInfrastructure()`
- Injected services that appear unregistered
- Duplicate registrations

EF Core:

- EF Core package references
- `DbContext` classes and `DbSet<TEntity>` properties
- Migration files with `DropTable`, `DropColumn`, or raw SQL
- Entities missing conventional primary key patterns

Security/Configuration:

- `appsettings*.json`
- Possible secrets and connection strings in config
- CORS policies using `AllowAnyOrigin`
- Missing HTTPS redirection
- JWT package usage without auth middleware
- Weak JWT issuer/audience/key values

API Readiness:

- Controllers and minimal APIs
- Swagger/OpenAPI setup
- Health checks
- Global exception handling
- Structured logging
- Validation patterns

## Current MVP Limitations

- Heuristics are intentionally conservative and source-based; DotDet does not perform full semantic dataflow analysis yet.
- Some framework extension methods may register services that the DI analyzer cannot infer.
- MSBuildWorkspace is used when possible, but DotDet falls back to project-file discovery if a solution cannot be fully loaded.
- Zip analysis extracts to a temporary folder for the request and deletes it after the response.
- There is no database, authentication, CI integration, historical trend view, or auto-fix engine.

## Future Roadmap

- Semantic Roslyn analyzers with symbol resolution and richer cross-project reasoning
- SARIF export and CI/PR annotations
- Analyzer rule configuration and severity overrides
- Historical report storage and trend dashboards
- Rich dependency graph visualization
- Auto-fix suggestions and guided remediation playbooks
- Authentication and team/project workspaces
