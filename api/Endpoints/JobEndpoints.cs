using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OptimaVerifica.Api.Models;
using OptimaVerifica.Api.Services;

namespace OptimaVerifica.Api.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs")
            .RequireAuthorization("OperatorOrAbove")
            .WithTags("Jobs");

        group.MapPost("/", CreateJob)
            .WithName("CreateJob")
            .WithSummary("Create a new job with cedulas");

        group.MapGet("/", GetJobs)
            .RequireAuthorization("AnyRole")
            .WithName("GetJobs")
            .WithSummary("Get list of jobs");

        group.MapGet("/{jobId}", GetJob)
            .RequireAuthorization("AnyRole")
            .WithName("GetJob")
            .WithSummary("Get job details");

        group.MapGet("/{jobId}/results", GetJobResults)
            .RequireAuthorization("AnyRole")
            .WithName("GetJobResults")
            .WithSummary("Get paginated job results");

        group.MapPost("/{jobId}/export", ExportJob)
            .RequireAuthorization("AnyRole")
            .WithName("ExportJob")
            .WithSummary("Export job results to CSV/XLSX/JSON");

        group.MapPost("/{jobId}/cancel", CancelJob)
            .WithName("CancelJob")
            .WithSummary("Cancel a pending or processing job");

        // Direct execution endpoint (for testing/small queries)
        group.MapPost("/execute", ExecutePresetDirect)
            .WithName("ExecutePresetDirect")
            .WithSummary("Execute a preset directly for a single cedula (no job creation)");
    }

    private static async Task<IResult> CreateJob(
        [FromBody] CreateJobRequest request,
        [FromServices] IJobService jobService,
        ClaimsPrincipal user)
    {
        try
        {
            var username = user.Identity?.Name ?? "anonymous";
            var job = await jobService.CreateJobAsync(request, username);

            return Results.Created($"/api/jobs/{job.Id}", new
            {
                id = job.Id,
                status = job.Status.ToString(),
                totalItems = job.TotalItems,
                message = "Job created successfully. Processing will begin shortly."
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetJobs(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IJobService jobService,
        ClaimsPrincipal user)
    {
        var username = user.Identity?.Name ?? "anonymous";
        var role = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "READER";

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);

        var jobs = await jobService.GetJobsAsync(username, role, page, pageSize);
        return Results.Ok(jobs);
    }

    private static async Task<IResult> GetJob(
        string jobId,
        [FromServices] IJobService jobService,
        ClaimsPrincipal user)
    {
        var username = user.Identity?.Name ?? "anonymous";
        var job = await jobService.GetJobAsync(jobId, username);

        if (job == null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        return Results.Ok(job);
    }

    private static async Task<IResult> GetJobResults(
        string jobId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IJobService jobService)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : (pageSize > 500 ? 500 : pageSize);

        var results = await jobService.GetJobResultsAsync(jobId, page, pageSize);
        return Results.Ok(results);
    }

    private static async Task<IResult> ExportJob(
        string jobId,
        [FromBody] ExportRequest request,
        [FromServices] IExportService exportService)
    {
        try
        {
            var (data, fileName, contentType) = await exportService.ExportJobResultsAsync(jobId, request.Format);

            return Results.File(data, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> CancelJob(
        string jobId,
        [FromServices] IJobService jobService,
        ClaimsPrincipal user)
    {
        var username = user.Identity?.Name ?? "anonymous";
        var job = await jobService.GetJobAsync(jobId, username);

        if (job == null)
        {
            return Results.NotFound(new { error = "Job not found" });
        }

        if (job.Status != JobStatus.PENDING && job.Status != JobStatus.PROCESSING)
        {
            return Results.BadRequest(new { error = "Only pending or processing jobs can be cancelled" });
        }

        await jobService.UpdateJobStatusAsync(jobId, JobStatus.CANCELLED);

        return Results.Ok(new { message = "Job cancelled successfully" });
    }

    private static async Task<IResult> ExecutePresetDirect(
        [FromBody] DirectExecuteRequest request,
        [FromServices] IPresetExecutor executor)
    {
        try
        {
            var results = await executor.ExecutePresetAsync(request.PresetKey, request.Cedula, request.Params);
            return Results.Ok(new { results, count = results.Count });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

public class DirectExecuteRequest
{
    public string PresetKey { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}
