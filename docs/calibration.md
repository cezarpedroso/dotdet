# Analyzer Calibration

DotDet v0.1 Preview is being calibrated against representative ASP.NET Core
solutions. Calibration means comparing findings, applicability, severity,
confidence, grouping, category scores, and report language with what an
experienced .NET reviewer would conclude.

## Current approach

For each repository or fixture, calibration asks:

- Did DotDet classify project intent and layer intent correctly?
- Does each active finding have sufficient evidence?
- Are framework-provided behaviors excluded?
- Are repeated symptoms grouped under a useful root cause?
- Does severity match likely release impact?
- Does confidence match the detection method?
- Do category and overall scores emotionally match the report?
- Are top risks and remediation priorities unique and actionable?
- Does a clean solution produce a clean, internally consistent summary?

Every confirmed false-positive fix should gain a regression fixture. Broad rule
suppression is avoided when a narrower applicability check can preserve valid
coverage.

## Calibration repositories

### Forge.SampleShop

The bundled solution intentionally contains architecture, DI, EF Core, security,
configuration, and API-readiness issues. It validates that major rule families,
source linking, architecture mapping, scoring, suppressions, and report exports
remain visible in a controlled demonstration.

### SchemaArchitect

SchemaArchitect represents a Web UI-oriented multi-project application. It has
been used to validate API/Web UI intent, clean-report consistency, project/source
tree preview, architecture inference, and the requirement that categories with no
active findings do not become fake primary concerns.

### eShopOnWeb

Microsoft's eShopOnWeb sample represents a realistic Clean Architecture solution
with MVC/Razor UI, a separate API surface, tests, MediatR, framework DI services,
and EF Core migrations. It has been used to calibrate project classification,
mixed-host API applicability, DI exclusions, migration severity, root-cause
grouping, and overall score sanity.

These repositories are calibration inputs, not endorsements or certifications of
their production readiness.

## Before/after expectations

### API intent

**Before:** Any ASP.NET Core host, or any project containing a `ControllerBase`
support type, could be treated as an API and warned about missing Swagger.

**Expected now:** Razor Pages, MVC, and Blazor/Razor Components projects do not
require Swagger unless an API surface is supported by evidence such as mapped API
controllers, minimal API endpoints, concrete `api/` routes, or explicit API
project intent. Genuine API and mixed API/UI hosts remain covered.

### Framework dependency injection

**Before:** `ILogger<T>`, `ILoggerFactory`, `IConfiguration`, `IOptions<T>`, and
other framework services could appear unregistered.

**Expected now:** Framework-provided services do not produce DI002. MediatR
injection is accepted when `AddMediatR` or a recognized source-defined composition
method provides registration evidence. A custom unregistered service still
produces a finding.

### EF migration operations

**Before:** Destructive calls, including operations in migration `Down()` methods,
could be treated as confirmed production blockers.

**Expected now:** Destructive migration operations are review-level risks unless
explicit release context supports stronger severity. Historical rollback
operations do not automatically collapse the readiness score.

### Clean reports

**Before:** Info-only or non-applicable observations could create an open finding,
primary concern, highest-risk category, or contradictory score explanation.

**Expected now:** A solution with no active production findings reports zero open
findings, no major concern, no invented highest risk, and no immediate remediation
action.

## Score philosophy

The overall score is derived from weighted category health rather than raw issue
count. Repeated evidence should not repeatedly penalize the same root cause.
Confirmed Critical or high-confidence Error findings may cap the score or status,
but moderate findings should not turn otherwise healthy category scores into an
implausible near-zero result.

The score is a prioritization aid. It is not a probability, compliance result,
security rating, or guarantee that a release is safe.

## Future calibration suite

The planned suite should include:

- clean minimal API, controller API, MVC, Razor Pages, Blazor, and worker hosts;
- mixed UI/API applications with mapped and unmapped support controllers;
- Clean Architecture, vertical-slice, modular monolith, and simpler layered apps;
- DI composition methods, keyed services, decorators, open generics, and hosted
  services;
- EF migrations covering initial schema, expand/contract, rollback, provider-
  specific SQL, and deliberate destructive changes;
- environment-specific configuration and authentication patterns;
- degraded project-loading and unsupported SDK/package scenarios;
- representative open-source repositories pinned to reviewed revisions.

Calibration results should remain deterministic and reproducible. See
[Engine maturity](engine-maturity.md) and
[Rule quality principles](rule-quality.md).
