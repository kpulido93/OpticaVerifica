using Microsoft.AspNetCore.Mvc;
using OptimaVerifica.Api.Services;

namespace OptimaVerifica.Api.Endpoints;

public static class IdsEndpoints
{
    public static void MapIdsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ids")
            .RequireAuthorization("OperatorOrAbove")
            .WithTags("IDs");

        group.MapPost("/parse", ParseIds)
            .DisableAntiforgery()
            .WithName("ParseIdsFile")
            .WithSummary("Parse CSV/XLSX file and extract cedulas");
    }

    private static async Task<IResult> ParseIds(
        HttpRequest request,
        [FromServices] IIdsParserService parser,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Content-Type debe ser multipart/form-data." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        var selectedColumn = form["selectedColumn"].FirstOrDefault();

        if (file == null)
        {
            return Results.BadRequest(new { error = "Debe enviar un archivo en el campo 'file'." });
        }

        try
        {
            var result = await parser.ParseAsync(file, selectedColumn, cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
