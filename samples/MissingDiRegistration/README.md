# Missing DI Registration

A notification API with a realistic application service. The composition root
registers the coordinator but intentionally omits its custom
`ITemplateRenderer` dependency. Framework-provided dependencies are present to
validate DotDet's DI exclusions.

## Expected DotDet result

- Readiness level: Medium
- Expected score range: 80-92
- Categories exercised: Dependency Injection, Security, API Readiness

Expected findings:

- DI002 for the custom `ITemplateRenderer` dependency.
- DI003 for a singleton dispatcher capturing the scoped coordinator.

Findings that should not appear:

- DI002 for `ILogger<NotificationCoordinator>`.
- DI002 for `IConfiguration`.
- DI002 for `IOptions<NotificationOptions>`.
- API003/API004/API005: OpenAPI, health checks, and exception handling are configured.
