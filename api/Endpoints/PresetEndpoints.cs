using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OptimaVerifica.Api.Models;
using OptimaVerifica.Api.Services;

namespace OptimaVerifica.Api.Endpoints;

public static class PresetEndpoints
{
    public static void MapPresetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/presets")
            .RequireAuthorization("AnyRole")
            .WithTags("Presets");

        group.MapGet("/", GetAllPresets)
            .WithName("GetAllPresets")
            .WithSummary("Get all available presets");

        group.MapGet("/{presetKey}", GetPreset)
            .WithName("GetPreset")
            .WithSummary("Get a specific preset by key");
    }

    private static async Task<IResult> GetAllPresets(
        [FromServices] IPresetService presetService)
    {
        var presets = await presetService.GetAllPresetsAsync();
        return Results.Ok(presets);
    }

    private static async Task<IResult> GetPreset(
        string presetKey,
        [FromServices] IPresetService presetService)
    {
        var preset = await presetService.GetPresetByKeyAsync(presetKey);
        if (preset == null)
        {
            return Results.NotFound(new { error = $"Preset '{presetKey}' not found" });
        }
        return Results.Ok(preset);
    }
}
