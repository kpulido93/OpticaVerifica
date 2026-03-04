using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OptimaVerifica.Api.Models;
using OptimaVerifica.Api.Services;

namespace OptimaVerifica.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin");

        // Schema endpoints
        group.MapGet("/schema", GetDefaultSchema)
            .WithName("GetDefaultSchema")
            .WithSummary("Get allowed schema for default dataset");

        group.MapGet("/schema/{dataset}", GetSchema)
            .WithName("GetSchema")
            .WithSummary("Get allowed schema (tables, columns, operators) for a dataset");

        group.MapGet("/datasets", GetDatasets)
            .WithName("GetDatasets")
            .WithSummary("Get list of available datasets");

        // Preset management endpoints
        group.MapGet("/presets", GetAllPresetsAdmin)
            .WithName("GetAllPresetsAdmin")
            .WithSummary("Get all presets (including disabled)");

        group.MapPost("/presets", CreatePreset)
            .WithName("CreatePreset")
            .WithSummary("Create a new preset");

        group.MapPut("/presets/{presetId}/toggle", TogglePreset)
            .WithName("TogglePreset")
            .WithSummary("Enable or disable a preset");

        group.MapPost("/presets/{presetId}/versions", CreatePresetVersion)
            .WithName("CreatePresetVersion")
            .WithSummary("Create a new version for a preset");

        group.MapGet("/presets/{presetKey}/versions", GetPresetVersions)
            .WithName("GetPresetVersions")
            .WithSummary("Get all versions of a preset");

        group.MapPost("/presets/{presetKey}/test", TestPreset)
            .WithName("TestPreset")
            .WithSummary("Test a preset with a sample cedula");

        // AST Compiler endpoint
        group.MapPost("/presets/compile", CompileAst)
            .WithName("CompileAst")
            .WithSummary("Compile an AST to SQL (preview only)");

        group.MapPost("/compile-ast", CompileAst)
            .WithName("CompileAstLegacy")
            .WithSummary("Compile an AST to SQL (legacy route)");

        // Test AST directly endpoint
        group.MapPost("/test-ast", TestAstDirect)
            .WithName("TestAstDirect")
            .WithSummary("Test an AST directly with a cedula");

        // Activate version endpoint
        group.MapPost("/presets/{presetId:int}/versions/{versionId:int}/activate", ActivateVersion)
            .WithName("ActivateVersion")
            .WithSummary("Activate a specific version of a preset");
    }

    private static async Task<IResult> GetSchema(
        string dataset,
        [FromServices] ISchemaService schemaService)
    {
        var schema = await schemaService.GetAllowedSchemaAsync(dataset);
        return Results.Ok(schema);
    }

    private static async Task<IResult> GetDefaultSchema([FromServices] ISchemaService schemaService)
    {
        var schema = await schemaService.GetAllowedSchemaAsync("neon_templaris");
        return Results.Ok(schema);
    }

    private static IResult GetDatasets()
    {
        // For now, return a static list. Could be made dynamic later.
        var datasets = new[]
        {
            new { key = "neon_templaris", name = "Neon Templaris", description = "Main database with TSS, vehicles, and contacts" }
        };
        return Results.Ok(datasets);
    }

    private static async Task<IResult> GetAllPresetsAdmin(
        [FromServices] IPresetService presetService,
        [FromServices] IDbConnectionFactory dbFactory)
    {
        using var conn = dbFactory.CreateConnection();
        var sql = @"
            SELECT 
                pd.id, pd.preset_key as PresetKey, pd.name, pd.description, pd.dataset,
                pd.is_hardcoded as IsHardcoded, pd.is_enabled as IsEnabled,
                pd.created_by as CreatedBy, pd.created_at as CreatedAt, pd.updated_at as UpdatedAt,
                COALESCE(pv.version, 0) as CurrentVersion
            FROM preset_definitions pd
            LEFT JOIN preset_versions pv ON pd.id = pv.preset_id AND pv.is_active = 1
            ORDER BY pd.created_at DESC";

        var presets = await Dapper.SqlMapper.QueryAsync<dynamic>(conn, sql);
        return Results.Ok(presets);
    }

    private static async Task<IResult> CreatePreset(
        [FromBody] CreatePresetRequest request,
        [FromServices] IPresetService presetService,
        [FromServices] ISchemaService schemaService,
        ClaimsPrincipal user)
    {
        // Validate the AST against allowed schema
        var validationErrors = await ValidateAst(request.Ast, request.Dataset, schemaService);
        if (validationErrors.Count > 0)
        {
            return Results.BadRequest(new { errors = validationErrors });
        }

        try
        {
            var username = user.Identity?.Name ?? "admin";
            var preset = await presetService.CreatePresetAsync(request, username);
            return Results.Created($"/api/presets/{preset.PresetKey}", preset);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> TogglePreset(
        int presetId,
        [FromBody] TogglePresetRequest request,
        [FromServices] IDbConnectionFactory dbFactory)
    {
        using var conn = dbFactory.CreateConnection();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE preset_definitions SET is_enabled = @Enabled WHERE id = @Id",
            new { Id = presetId, Enabled = request.Enabled });

        return Results.Ok(new { message = request.Enabled ? "Preset enabled" : "Preset disabled" });
    }

    private static async Task<IResult> CreatePresetVersion(
        int presetId,
        [FromBody] CreateVersionRequest request,
        [FromServices] IPresetService presetService,
        [FromServices] ISchemaService schemaService,
        [FromServices] IDbConnectionFactory dbFactory,
        ClaimsPrincipal user)
    {
        // Get preset dataset for validation
        using var conn = dbFactory.CreateConnection();
        var preset = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<dynamic>(conn,
            "SELECT dataset FROM preset_definitions WHERE id = @Id", new { Id = presetId });

        if (preset == null)
        {
            return Results.NotFound(new { error = "Preset not found" });
        }

        // Validate AST
        var validationErrors = await ValidateAst(request.Ast, preset.dataset, schemaService);
        if (validationErrors.Count > 0)
        {
            return Results.BadRequest(new { errors = validationErrors });
        }

        var username = user.Identity?.Name ?? "admin";
        var version = await presetService.CreateVersionAsync(presetId, request.Ast, username);

        return Results.Created($"/api/admin/presets/{presetId}/versions/{version.Version}", version);
    }

    private static async Task<IResult> GetPresetVersions(
        string presetKey,
        [FromServices] IPresetService presetService,
        [FromServices] IDbConnectionFactory dbFactory)
    {
        var preset = await presetService.GetPresetDefinitionAsync(presetKey);
        if (preset == null)
        {
            return Results.NotFound(new { error = "Preset not found" });
        }

        using var conn = dbFactory.CreateConnection();
        var sql = @"
            SELECT id, preset_id as PresetId, version as Version, ast_json as AstJson,
                   is_active as IsActive, created_by as CreatedBy, created_at as CreatedAt
            FROM preset_versions
            WHERE preset_id = @PresetId
            ORDER BY version DESC";

        var versions = await Dapper.SqlMapper.QueryAsync<PresetVersion>(conn, sql, new { PresetId = preset.Id });
        return Results.Ok(versions);
    }

    private static async Task<IResult> TestPreset(
        string presetKey,
        [FromBody] TestPresetRequest request,
        [FromServices] IPresetExecutor executor)
    {
        try
        {
            var (sql, results, executionTime) = await executor.ExecuteAndExplainAsync(
                presetKey, request.Cedula, request.Params);

            return Results.Ok(new TestPresetResponse
            {
                Success = true,
                GeneratedSql = sql,
                Results = results,
                ExecutionTimeMs = executionTime
            });
        }
        catch (Exception ex)
        {
            return Results.Ok(new TestPresetResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private static async Task<IResult> CompileAst(
        [FromBody] CompileAstRequest request,
        [FromServices] IPresetExecutor executor,
        [FromServices] ISchemaService schemaService)
    {
        try
        {
            var dataset = string.IsNullOrWhiteSpace(request.Dataset) ? "neon_templaris" : request.Dataset;
            var validationErrors = await ValidateAst(request.Ast, dataset, schemaService);
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { errors = validationErrors });
            }

            var sql = executor.CompileAstToSql(request.Ast, "@cedula_placeholder", null, out _);
            return Results.Ok(new { sql, @params = new { cedula = "@cedula_placeholder" } });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult TestAstDirect(
        [FromBody] TestAstRequest request)
    {
        try
        {
            // Compile normalized AST to SQL preview
            var sql = CompileNormalizedAstToSql(request.Ast, request.Cedula);
            
            return Results.Ok(new TestPresetResponse
            {
                Success = true,
                GeneratedSql = sql,
                Results = new List<Dictionary<string, object>>(),
                ExecutionTimeMs = 0
            });
        }
        catch (Exception ex)
        {
            return Results.Ok(new TestPresetResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private static async Task<IResult> ActivateVersion(
        int presetId,
        int versionId,
        [FromServices] IDbConnectionFactory dbFactory)
    {
        using var conn = dbFactory.CreateConnection();

        // Deactivate all versions for this preset
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE preset_versions SET is_active = 0 WHERE preset_id = @PresetId",
            new { PresetId = presetId });

        // Activate the specified version
        var affected = await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE preset_versions SET is_active = 1 WHERE preset_id = @PresetId AND id = @VersionId",
            new { PresetId = presetId, VersionId = versionId });

        if (affected == 0)
        {
            return Results.NotFound(new { error = "Version not found" });
        }

        return Results.Ok(new { message = "Version activated successfully" });
    }

    private static string CompileNormalizedAstToSql(NormalizedAst ast, string cedula)
    {
        var sb = new System.Text.StringBuilder();

        // SELECT
        sb.Append("SELECT\n");
        if (ast.Select.Count == 0)
        {
            sb.Append("  *");
        }
        else
        {
            sb.Append(string.Join(",\n", ast.Select.Select(c => $"  {c.Expr} AS \"{c.Alias}\"")));
        }

        // FROM (infer from first selected column)
        var mainTable = ast.Select.FirstOrDefault()?.SourceTable ?? "unknown";
        sb.Append($"\nFROM {mainTable}");

        // JOINs
        foreach (var join in ast.Joins)
        {
            sb.Append($"\n{join.JoinType} JOIN {join.Table} ON {join.OnLeft} = {join.OnRight}");
        }

        // WHERE
        if (ast.Where != null && ast.Where.Rules.Count > 0)
        {
            sb.Append("\nWHERE ");
            sb.Append(CompileFilterGroup(ast.Where, cedula));
        }

        // ORDER BY
        if (ast.OrderBy.Count > 0)
        {
            sb.Append("\nORDER BY ");
            sb.Append(string.Join(", ", ast.OrderBy.Select(o => $"{o.Field} {o.Dir.ToUpper()}")));
        }

        // LIMIT
        sb.Append($"\nLIMIT {ast.Limit}");

        return sb.ToString();
    }

    private static string CompileFilterGroup(NormalizedFilterGroup group, string cedula)
    {
        var parts = new List<string>();

        foreach (var ruleObj in group.Rules)
        {
            if (ruleObj is Newtonsoft.Json.Linq.JObject jObj)
            {
                if (jObj.ContainsKey("op") && jObj.ContainsKey("rules"))
                {
                    var subGroup = jObj.ToObject<NormalizedFilterGroup>();
                    if (subGroup != null)
                    {
                        parts.Add($"({CompileFilterGroup(subGroup, cedula)})");
                    }
                }
                else if (jObj.ContainsKey("field"))
                {
                    var rule = jObj.ToObject<NormalizedFilterRule>();
                    if (rule != null)
                    {
                        parts.Add(CompileFilterRule(rule, cedula));
                    }
                }
            }
        }

        return string.Join($" {group.Op.ToUpper()} ", parts);
    }

    private static string CompileFilterRule(NormalizedFilterRule rule, string cedula)
    {
        return rule.Operator switch
        {
            "in_ids" => $"{rule.Field} IN (@ids)",
            "is_null" => $"{rule.Field} IS NULL",
            "is_not_null" => $"{rule.Field} IS NOT NULL",
            "between" when rule.Value is Newtonsoft.Json.Linq.JArray arr => 
                $"{rule.Field} BETWEEN '{arr[0]}' AND '{arr[1]}'",
            "in" when rule.Value is Newtonsoft.Json.Linq.JArray arr => 
                $"{rule.Field} IN ({string.Join(", ", arr.Select(v => $"'{v}'"))})",
            "like" => $"{rule.Field} LIKE '%{rule.Value}%'",
            "eq" => $"{rule.Field} = '{rule.Value}'",
            "neq" => $"{rule.Field} != '{rule.Value}'",
            "gt" => $"{rule.Field} > '{rule.Value}'",
            "gte" => $"{rule.Field} >= '{rule.Value}'",
            "lt" => $"{rule.Field} < '{rule.Value}'",
            "lte" => $"{rule.Field} <= '{rule.Value}'",
            _ => $"{rule.Field} = '{rule.Value}'"
        };
    }

    private static async Task<List<string>> ValidateAst(PresetAst ast, string dataset, ISchemaService schemaService)
    {
        var errors = new List<string>();

        // Skip validation for hardcoded presets
        if (ast.Type == "HARDCODED") return errors;

        // Validate FROM table
        if (!string.IsNullOrEmpty(ast.FromTable))
        {
            if (!await schemaService.IsTableAllowedAsync(dataset, ast.FromTable))
            {
                errors.Add($"Table '{ast.FromTable}' is not allowed in dataset '{dataset}'");
            }
        }

        // Validate SELECT columns
        if (ast.Select != null)
        {
            foreach (var col in ast.Select)
            {
                var table = string.IsNullOrEmpty(col.Table) ? ast.FromTable : col.Table;
                if (!string.IsNullOrEmpty(table) && !await schemaService.IsColumnAllowedAsync(dataset, table, col.Column))
                {
                    errors.Add($"Column '{table}.{col.Column}' is not allowed");
                }
            }
        }

        // Validate JOINs
        if (ast.Joins != null)
        {
            foreach (var join in ast.Joins)
            {
                if (!await schemaService.IsTableAllowedAsync(dataset, join.Table))
                {
                    errors.Add($"Join table '{join.Table}' is not allowed");
                }
            }
        }

        // Validate filters
        if (ast.Where != null)
        {
            await ValidateFilterGroup(ast.Where, dataset, ast.FromTable ?? "", schemaService, errors);
        }

        return errors;
    }

    private static async Task ValidateFilterGroup(
        AstFilterGroup group, string dataset, string defaultTable,
        ISchemaService schemaService, List<string> errors)
    {
        if (group.Filters != null)
        {
            foreach (var filter in group.Filters)
            {
                var table = string.IsNullOrEmpty(filter.Table) ? defaultTable : filter.Table;
                if (!string.IsNullOrEmpty(table) && !await schemaService.IsColumnAllowedAsync(dataset, table, filter.Column))
                {
                    errors.Add($"Filter column '{table}.{filter.Column}' is not allowed");
                }

                if (!await schemaService.IsOperatorAllowedAsync(filter.Operator))
                {
                    errors.Add($"Operator '{filter.Operator}' is not allowed");
                }
            }
        }

        if (group.Groups != null)
        {
            foreach (var subGroup in group.Groups)
            {
                await ValidateFilterGroup(subGroup, dataset, defaultTable, schemaService, errors);
            }
        }
    }
}

public class TogglePresetRequest
{
    public bool Enabled { get; set; }
}

public class CreateVersionRequest
{
    public PresetAst Ast { get; set; } = new();
}

public class CompileAstRequest
{
    public string? Dataset { get; set; }
    public PresetAst Ast { get; set; } = new();
}

public class TestAstRequest
{
    public NormalizedAst Ast { get; set; } = new();
    public string Cedula { get; set; } = string.Empty;
}

// Normalized AST format (from Preset Designer)
public class NormalizedAst
{
    public string Dataset { get; set; } = string.Empty;
    public List<NormalizedSelectColumn> Select { get; set; } = new();
    public List<NormalizedJoin> Joins { get; set; } = new();
    public NormalizedFilterGroup? Where { get; set; }
    public List<NormalizedOrderBy> OrderBy { get; set; } = new();
    public int Limit { get; set; } = 100;
}

public class NormalizedSelectColumn
{
    public string Id { get; set; } = string.Empty;
    public string Expr { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public string DataType { get; set; } = "unknown";
}

public class NormalizedJoin
{
    public string Id { get; set; } = string.Empty;
    public string JoinType { get; set; } = "INNER";
    public string Table { get; set; } = string.Empty;
    public string OnLeft { get; set; } = string.Empty;
    public string OnRight { get; set; } = string.Empty;
}

public class NormalizedFilterGroup
{
    public string Id { get; set; } = string.Empty;
    public string Op { get; set; } = "and"; // "and" | "or"
    public List<object> Rules { get; set; } = new(); // FilterRule or FilterGroup
}

public class NormalizedFilterRule
{
    public string Id { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public object? Value { get; set; }
    public string DataType { get; set; } = "unknown";
}

public class NormalizedOrderBy
{
    public string Id { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Dir { get; set; } = "asc";
}
