using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OptimaVerifica.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new 
        { 
            name = "Optima Verifica API",
            version = "1.0.0",
            status = "running"
        }))
        .WithTags("Health")
        .AllowAnonymous();

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
            .WithTags("Health")
            .AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponseAsync
        })
        .WithTags("Health")
        .AllowAnonymous();

        // Backward-compatible aliases
        app.MapGet("/health", () => Results.Ok(new { status = "live" }))
            .WithTags("Health")
            .AllowAnonymous();

        app.MapHealthChecks("/api/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponseAsync
        })
        .WithTags("Health")
        .AllowAnonymous();
    }

    private static async Task WriteHealthCheckResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            }),
            durationMs = report.TotalDuration.TotalMilliseconds
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
