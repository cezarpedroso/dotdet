# DotDet SampleShop

This is an intentionally imperfect .NET 9 sample solution for testing DotDet.

Paste this solution path into the DotDet dashboard:

```text
C:\Users\cezar\Documents\Codex\2026-07-06\build-the-mvp-for-a-project\samples\Forge.SampleShop\Forge.SampleShop.slnx
```

The sample is designed to produce findings across multiple categories:

- Domain project references EF Core.
- Application project references Infrastructure directly.
- API has duplicate DI registrations and one intentionally unregistered dependency.
- EF Core migration contains `DropColumn` and raw SQL.
- Configuration contains connection strings, weak JWT values, and a sample API key.
- CORS allows any origin.
- API omits HTTPS redirection, authentication middleware, health checks, global exception handling, structured logging, and validation patterns.

Build the sample:

```powershell
dotnet build samples/Forge.SampleShop/Forge.SampleShop.slnx
```
