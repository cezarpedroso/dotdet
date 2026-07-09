using Microsoft.AspNetCore.Http;

namespace Forge.Api.Contracts;

public sealed class AnalyzeZipRequest
{
    public IFormFile? File { get; init; }
}
