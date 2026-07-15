# Clean Minimal API

A compact order-tracking service that demonstrates a mostly production-ready
minimal API. The sample uses clear project boundaries, explicit DI
registrations, problem details, HTTPS redirection, health checks, OpenAPI, and
structured console logging.

## Expected DotDet result

- Readiness level: High
- Expected score range: 88-100
- Categories exercised: Architecture, Dependency Injection, Security, API Readiness

Expected findings are limited to low-impact calibration observations, if any.

Findings that should not appear:

- API002: endpoints are present.
- API003: OpenAPI is configured.
- API004: health checks are configured.
- API005: global exception handling is configured.
- DI002: all custom constructor dependencies are registered.
- Architecture boundary violations.
