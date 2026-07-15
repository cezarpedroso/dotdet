# MVC Web UI, No Swagger

A server-rendered customer-support portal built with MVC views. It deliberately
does not configure Swagger because it does not publish an API surface. The
sample validates DotDet's API/Web UI intent classification.

## Expected DotDet result

- Readiness level: High
- Expected score range: 82-100
- Categories exercised: Architecture, Dependency Injection, Security, API Readiness

Expected findings are limited to low-impact Web UI operational observations, if
any.

Findings that should not appear:

- API002: the project is a Web UI, so API endpoint discovery is not applicable.
- API003: Swagger/OpenAPI is not required for this MVC-only host.
- API004: health checks are configured.
- API005: global exception handling is configured.
- DI002: the custom dashboard service and framework services are registered or framework-provided.
