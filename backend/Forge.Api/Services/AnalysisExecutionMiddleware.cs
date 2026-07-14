using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Forge.Api.Services;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AnalysisExecutionAttribute : Attribute;

public sealed class AnalysisConcurrencyGate(IOptions<AnalysisExecutionOptions> options)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);
    private readonly int maxConcurrentPerCaller = Math.Max(1, options.Value.MaxConcurrentPerCaller);

    public IDisposable? TryAcquire(string callerKey)
    {
        var gate = gates.GetOrAdd(callerKey, _ => new SemaphoreSlim(maxConcurrentPerCaller, maxConcurrentPerCaller));
        return gate.Wait(0) ? new Lease(gate) : null;
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? currentGate = gate;

        public void Dispose()
        {
            Interlocked.Exchange(ref currentGate, null)?.Release();
        }
    }
}

public sealed class AnalysisExecutionMiddleware(
    RequestDelegate next,
    IOptions<AnalysisExecutionOptions> options)
{
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.TimeoutSeconds));

    public async Task InvokeAsync(HttpContext context, AnalysisConcurrencyGate concurrencyGate)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<AnalysisExecutionAttribute>() is null)
        {
            await next(context);
            return;
        }

        using var lease = concurrencyGate.TryAcquire(GetCallerKey(context));
        if (lease is null)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status429TooManyRequests,
                "Analysis already running",
                "Another analysis is already running for this account or network. Wait for it to finish before starting another.");
            return;
        }

        var requestCancellation = context.RequestAborted;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation);
        timeoutCancellation.CancelAfter(timeout);
        context.RequestAborted = timeoutCancellation.Token;

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (!requestCancellation.IsCancellationRequested && timeoutCancellation.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status408RequestTimeout,
                    "Analysis timed out",
                    "The analysis exceeded the configured execution time limit. Try a smaller solution or run it again later.");
            }
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
            // The client disconnected; every downstream operation observes RequestAborted.
        }
        finally
        {
            context.RequestAborted = requestCancellation;
        }
    }

    public static string GetCallerKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        });
    }
}
