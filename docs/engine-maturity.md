# Analysis Engine Maturity

DotDet v0.1 is a **Preview / Calibration** release. The engine is useful for
structured engineering review, but its findings and score should not be treated as
an automatic certification or release gate.

## Analysis approach

DotDet combines:

- Roslyn syntax inspection;
- Roslyn semantic models and compiled symbols when trusted project loading
  succeeds;
- solution/project reference inspection;
- MSBuild project information for trusted local/sample inputs;
- JSON configuration and package-reference inspection;
- conservative heuristics where the runtime result cannot be proven statically.

Each finding can expose rule ID, severity, confidence, detection method, source
location, evidence, production impact, recommended pattern, implementation
guidance, examples, and Microsoft documentation. Fidelity metadata describes
whether an analysis achieved semantic coverage, degraded project loading, syntax
fallback, or safe syntax analysis.

## Coverage

| Area | Current coverage | Maturity |
| --- | --- | --- |
| Architecture boundaries | Project graph, cycles, inferred layers, forbidden references, architecture map | Preview; naming and topology inference still require calibration |
| Dependency Injection | Constructor requirements, registrations, duplicate registrations, lifetime risks, framework exclusions, selected composition methods | Preview; not a runtime container simulation |
| EF Core migrations | `DbContext`, `DbSet<T>`, key conventions, migration operations, raw SQL, destructive-change indicators | Preview; cannot prove release context or data impact |
| Security/configuration | Configuration sensitivity, CORS, HTTPS, JWT/auth middleware, issuer/audience/key indicators | Preview; static configuration evidence only |
| API readiness | API/Web UI intent, controllers/minimal APIs, OpenAPI, exception handling, health checks, logging, validation | Preview; mixed-host intent continues to be calibrated |
| Scoring/ranking | Weighted category scores, confidence/severity calibration, root-cause grouping, top risks and roadmap | Preview; intended as prioritization, not certification |

## Evidence and confidence

DotDet prefers source-linked evidence over generic project-level warnings.
Confidence communicates how directly the engine established the condition:

- **High** - supported by semantic analysis or explicit project/configuration
  evidence;
- **Medium** - strongly inferred from syntax or consistent usage patterns;
- **Low** - heuristic or incomplete evidence that should not dominate production
  risk.

Detection methods identify Roslyn semantic analysis, Roslyn syntax analysis,
MSBuild/project configuration, or heuristic analysis. Findings are downgraded when
the available evidence cannot justify stronger language.

## Calibration history

Recent calibration focused on reducing false positives rather than increasing
rule count. Improvements include:

- test-project classification and exclusion from production scoring;
- API/Web UI intent for Razor Pages, MVC, Blazor, minimal APIs, and mixed hosts;
- framework-provided DI service exclusions;
- MediatR registration recognition;
- environment-aware connection-string severity;
- migration-risk severity calibration;
- weighted category scoring and root-cause grouping;
- clean-report consistency when no active production findings remain.

The current calibration repositories include the bundled Forge.SampleShop,
SchemaArchitect, and Microsoft's eShopOnWeb sample. See
[Calibration](calibration.md).

## Known limits

DotDet cannot fully establish runtime behavior from static source. In particular,
it is not a replacement for:

- manual architecture and threat-model review;
- penetration testing or a complete SAST/DAST program;
- runtime dependency-injection container validation;
- migration rehearsal, backup verification, or database review;
- production telemetry, logging, tracing, and availability monitoring;
- maintainer code review and domain-specific engineering judgment.

Untrusted ZIP and GitHub inputs intentionally use safe syntax analysis until an
isolated semantic worker is available. See [Known limitations](known-limitations.md).

## Maturity roadmap

The next engine-quality milestones are:

1. expand the repeatable calibration repository suite;
2. measure rule-level precision and document known failure modes;
3. improve host-scoped DI and environment-aware configuration reasoning;
4. add isolated semantic analysis for untrusted repositories;
5. expand fidelity/timing metadata and deterministic regression fixtures;
6. validate branch and pull-request analysis when those workflows are added.

See the [Roadmap](roadmap.md) and
[Rule quality principles](rule-quality.md).
