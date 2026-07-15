# DotDet SampleShop

This is an intentionally imperfect .NET 9 sample solution for testing DotDet.

From the DotDet repository root, the sample solution is located at:

```text
samples/Forge.SampleShop/Forge.SampleShop.slnx
```

The sample is designed to produce findings across multiple categories:

- Domain project references EF Core.
- Application project references Infrastructure directly.
- API has duplicate DI registrations and one intentionally unregistered dependency.
- EF Core migration contains `DropColumn` and raw SQL.
- Configuration contains connection strings, weak JWT values, and a sample API key.
- CORS allows any origin.
- API omits HTTPS redirection, authentication middleware, health checks, global exception handling, structured logging, and validation patterns.

Expected readiness level: Medium. Expected score range: 82-92
under DotDet's conservative weighted scoring and confirmed-error caps. The score
should be read together with the breadth and severity of the findings.

Build the sample:

```powershell
dotnet build samples/Forge.SampleShop/Forge.SampleShop.slnx
```
