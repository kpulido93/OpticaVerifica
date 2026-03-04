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

        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithTags("Health")
            .AllowAnonymous();

        app.MapGet("/api/health", () => Results.Ok(new 
        { 
            status = "healthy",
            timestamp = DateTime.UtcNow 
        }))
        .WithTags("Health")
        .AllowAnonymous();
    }
}
