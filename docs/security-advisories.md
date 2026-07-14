# Dependency advisory status

DotDet pins `System.Security.Cryptography.Xml` to a patched .NET 8 servicing release and overrides Monaco's transitive DOMPurify copy to a compatible patched release. Monaco itself is not downgraded.

Recheck the production dependency graph before each release:

```powershell
dotnet list backend/Forge.Api/Forge.Api.csproj package --vulnerable --include-transitive
cd frontend
npm audit --omit=dev
```

Do not use `npm audit fix --force` when it proposes a Monaco downgrade. Validate any future override with the frontend build and browser tests.
