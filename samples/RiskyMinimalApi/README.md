# Risky Minimal API

A compact four-project inventory API that works locally but mixes persistence
into lower layers and omits common production safeguards. It includes
placeholder-only configuration values; none are real credentials.

## Expected DotDet result

- Readiness level: Low
- Expected score range: 78-86
- Categories exercised: Architecture, Dependency Injection, EF Core, Security, API Readiness

Expected findings:

- API003: OpenAPI/Swagger setup is missing.
- API004: health checks are missing.
- API005: global exception handling is missing.
- Security findings for permissive CORS, missing JWT middleware, and intentionally weak sample-only JWT configuration.
- Architecture findings for EF in Core and Application referencing Persistence.
- DI findings for duplicate registration and a singleton capturing a scoped service.
- EF observations for a context without migrations and an entity without an obvious key pattern.

Findings that should not appear:

- API002: minimal API endpoints are present.
- DI002: the custom catalog dependency is registered.
- EF Core migration findings: this sample does not use EF Core.
