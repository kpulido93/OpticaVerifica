using Dapper;
using OptimaVerifica.Api.Models;
using Newtonsoft.Json;

namespace OptimaVerifica.Api.Services;

public interface IPresetService
{
    Task<List<PresetResponse>> GetAllPresetsAsync();
    Task<PresetResponse?> GetPresetByKeyAsync(string presetKey);
    Task<PresetDefinition?> GetPresetDefinitionAsync(string presetKey);
    Task<PresetVersion?> GetActiveVersionAsync(int presetId);
    Task<PresetAst?> GetPresetAstAsync(string presetKey);
    Task<PresetDefinition> CreatePresetAsync(CreatePresetRequest request, string createdBy);
    Task<PresetVersion> CreateVersionAsync(int presetId, PresetAst ast, string createdBy);
}

public class PresetService : IPresetService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<PresetService> _logger;

    public PresetService(IDbConnectionFactory dbFactory, ILogger<PresetService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<List<PresetResponse>> GetAllPresetsAsync()
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT 
                pd.id, pd.preset_key as PresetKey, pd.name, pd.description, pd.dataset,
                pd.is_hardcoded as IsHardcoded, pd.is_enabled as IsEnabled,
                COALESCE(pv.version, 1) as CurrentVersion,
                pv.ast_json as AstJson
            FROM preset_definitions pd
            LEFT JOIN preset_versions pv ON pd.id = pv.preset_id AND pv.is_active = 1
            WHERE pd.is_enabled = 1
            ORDER BY pd.name";

        var presets = await conn.QueryAsync<dynamic>(sql);

        return presets.Select(p => new PresetResponse
        {
            Id = (int)p.id,
            PresetKey = p.PresetKey,
            Name = p.name,
            Description = p.description,
            Dataset = p.dataset,
            IsHardcoded = p.IsHardcoded == 1,
            IsEnabled = p.IsEnabled == 1,
            CurrentVersion = (int)p.CurrentVersion,
            Inputs = ParseInputsFromAst(p.AstJson)
        }).ToList();
    }

    public async Task<PresetResponse?> GetPresetByKeyAsync(string presetKey)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT 
                pd.id, pd.preset_key as PresetKey, pd.name, pd.description, pd.dataset,
                pd.is_hardcoded as IsHardcoded, pd.is_enabled as IsEnabled,
                COALESCE(pv.version, 1) as CurrentVersion,
                pv.ast_json as AstJson
            FROM preset_definitions pd
            LEFT JOIN preset_versions pv ON pd.id = pv.preset_id AND pv.is_active = 1
            WHERE pd.preset_key = @PresetKey AND pd.is_enabled = 1";

        var p = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { PresetKey = presetKey });
        if (p == null) return null;

        return new PresetResponse
        {
            Id = (int)p.id,
            PresetKey = p.PresetKey,
            Name = p.name,
            Description = p.description,
            Dataset = p.dataset,
            IsHardcoded = p.IsHardcoded == 1,
            IsEnabled = p.IsEnabled == 1,
            CurrentVersion = (int)p.CurrentVersion,
            Inputs = ParseInputsFromAst(p.AstJson)
        };
    }

    public async Task<PresetDefinition?> GetPresetDefinitionAsync(string presetKey)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT 
                id as Id, preset_key as PresetKey, name as Name, description as Description,
                dataset as Dataset, is_hardcoded as IsHardcoded, is_enabled as IsEnabled,
                created_by as CreatedBy, created_at as CreatedAt, updated_at as UpdatedAt
            FROM preset_definitions
            WHERE preset_key = @PresetKey";

        return await conn.QueryFirstOrDefaultAsync<PresetDefinition>(sql, new { PresetKey = presetKey });
    }

    public async Task<PresetVersion?> GetActiveVersionAsync(int presetId)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT 
                id as Id, preset_id as PresetId, version as Version,
                ast_json as AstJson, compiled_sql as CompiledSql,
                is_active as IsActive, created_by as CreatedBy, created_at as CreatedAt
            FROM preset_versions
            WHERE preset_id = @PresetId AND is_active = 1
            ORDER BY version DESC
            LIMIT 1";

        return await conn.QueryFirstOrDefaultAsync<PresetVersion>(sql, new { PresetId = presetId });
    }

    public async Task<PresetAst?> GetPresetAstAsync(string presetKey)
    {
        var preset = await GetPresetDefinitionAsync(presetKey);
        if (preset == null) return null;

        var version = await GetActiveVersionAsync(preset.Id);
        if (version == null) return null;

        try
        {
            return JsonConvert.DeserializeObject<PresetAst>(version.AstJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing AST for preset {PresetKey}", presetKey);
            return null;
        }
    }

    public async Task<PresetDefinition> CreatePresetAsync(CreatePresetRequest request, string createdBy)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            INSERT INTO preset_definitions (preset_key, name, description, dataset, is_hardcoded, is_enabled, created_by)
            VALUES (@PresetKey, @Name, @Description, @Dataset, 0, 1, @CreatedBy);
            SELECT LAST_INSERT_ID();";

        var id = await conn.ExecuteScalarAsync<int>(sql, new
        {
            request.PresetKey,
            request.Name,
            request.Description,
            request.Dataset,
            CreatedBy = createdBy
        });

        // Create initial version
        await CreateVersionAsync(id, request.Ast, createdBy);

        return (await GetPresetDefinitionAsync(request.PresetKey))!;
    }

    public async Task<PresetVersion> CreateVersionAsync(int presetId, PresetAst ast, string createdBy)
    {
        using var conn = _dbFactory.CreateConnection();

        // Get next version number
        var maxVersion = await conn.ExecuteScalarAsync<int?>(
            "SELECT MAX(version) FROM preset_versions WHERE preset_id = @PresetId",
            new { PresetId = presetId }) ?? 0;

        var newVersion = maxVersion + 1;

        // Deactivate previous versions
        await conn.ExecuteAsync(
            "UPDATE preset_versions SET is_active = 0 WHERE preset_id = @PresetId",
            new { PresetId = presetId });

        var sql = @"
            INSERT INTO preset_versions (preset_id, version, ast_json, is_active, created_by)
            VALUES (@PresetId, @Version, @AstJson, 1, @CreatedBy);
            SELECT LAST_INSERT_ID();";

        var id = await conn.ExecuteScalarAsync<int>(sql, new
        {
            PresetId = presetId,
            Version = newVersion,
            AstJson = JsonConvert.SerializeObject(ast),
            CreatedBy = createdBy
        });

        return new PresetVersion
        {
            Id = id,
            PresetId = presetId,
            Version = newVersion,
            AstJson = JsonConvert.SerializeObject(ast),
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    private List<AstInput>? ParseInputsFromAst(string? astJson)
    {
        if (string.IsNullOrEmpty(astJson)) return null;

        try
        {
            var ast = JsonConvert.DeserializeObject<PresetAst>(astJson);
            return ast?.Inputs;
        }
        catch
        {
            return null;
        }
    }
}
