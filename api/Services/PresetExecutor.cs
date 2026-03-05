using System.Diagnostics;
using System.Text;
using Dapper;
using OptimaVerifica.Api.Models;
using Newtonsoft.Json;

namespace OptimaVerifica.Api.Services;

public interface IPresetExecutor
{
    Task<List<Dictionary<string, object>>> ExecutePresetAsync(string presetKey, string cedula, Dictionary<string, object>? parameters = null);
    Task<(string sql, List<Dictionary<string, object>> results, long executionTimeMs)> ExecuteAndExplainAsync(string presetKey, string cedula, Dictionary<string, object>? parameters = null);
    string CompileAstToSql(PresetAst ast, string cedula, Dictionary<string, object>? parameters, out Dictionary<string, object> sqlParams);
}

public class PresetExecutor : IPresetExecutor
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IPresetService _presetService;
    private readonly ISchemaService _schemaService;
    private readonly ILogger<PresetExecutor> _logger;

    public PresetExecutor(
        IDbConnectionFactory dbFactory,
        IPresetService presetService,
        ISchemaService schemaService,
        ILogger<PresetExecutor> logger)
    {
        _dbFactory = dbFactory;
        _presetService = presetService;
        _schemaService = schemaService;
        _logger = logger;
    }

    public async Task<List<Dictionary<string, object>>> ExecutePresetAsync(string presetKey, string cedula, Dictionary<string, object>? parameters = null)
    {
        var preset = await _presetService.GetPresetDefinitionAsync(presetKey);
        if (preset == null)
        {
            throw new ArgumentException($"Preset '{presetKey}' not found");
        }

        var ast = await _presetService.GetPresetAstAsync(presetKey);
        if (ast == null)
        {
            throw new InvalidOperationException($"Could not load AST for preset '{presetKey}'");
        }

        // Handle hardcoded presets with special logic
        if (ast.Type == "HARDCODED" && !string.IsNullOrEmpty(ast.Handler))
        {
            return await ExecuteHardcodedPresetAsync(ast.Handler, cedula, parameters);
        }

        // Compile AST to SQL
        var sql = CompileAstToSql(ast, cedula, parameters, out var sqlParams);

        // Execute
        using var conn = _dbFactory.CreateConnection();
        var results = await conn.QueryAsync<dynamic>(sql, sqlParams);

        return results
        .Select(r => ConvertDynamicToDict((object)r))
        .ToList();
    }

    public async Task<(string sql, List<Dictionary<string, object>> results, long executionTimeMs)> ExecuteAndExplainAsync(
        string presetKey, string cedula, Dictionary<string, object>? parameters = null)
    {
        var preset = await _presetService.GetPresetDefinitionAsync(presetKey);
        if (preset == null)
        {
            throw new ArgumentException($"Preset '{presetKey}' not found");
        }

        var ast = await _presetService.GetPresetAstAsync(presetKey);
        if (ast == null)
        {
            throw new InvalidOperationException($"Could not load AST for preset '{presetKey}'");
        }

        string sql;
        Dictionary<string, object> sqlParams;

        if (ast.Type == "HARDCODED" && !string.IsNullOrEmpty(ast.Handler))
        {
            sql = $"-- HARDCODED HANDLER: {ast.Handler}\n-- This preset uses custom logic";
            sqlParams = new Dictionary<string, object> { ["cedula"] = cedula };
        }
        else
        {
            sql = CompileAstToSql(ast, cedula, parameters, out sqlParams);
        }

        var sw = Stopwatch.StartNew();
        var results = await ExecutePresetAsync(presetKey, cedula, parameters);
        sw.Stop();

        return (sql, results, sw.ElapsedMilliseconds);
    }

    public string CompileAstToSql(PresetAst ast, string cedula, Dictionary<string, object>? parameters, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object> { ["cedula"] = cedula };

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                sqlParams[p.Key] = p.Value;
            }
        }

        var sb = new StringBuilder();

        // SELECT clause
        sb.Append("SELECT ");
        if (ast.Select == null || ast.Select.Count == 0)
        {
            sb.Append("*");
        }
        else
        {
            var selectCols = ast.Select.Select(c =>
            {
                var colRef = string.IsNullOrEmpty(c.Table) ? c.Column : $"{c.Table}.{c.Column}";
                if (!string.IsNullOrEmpty(c.Aggregate))
                {
                    colRef = $"{c.Aggregate}({colRef})";
                }
                if (!string.IsNullOrEmpty(c.Alias))
                {
                    colRef = $"{colRef} AS {c.Alias}";
                }
                return colRef;
            });
            sb.Append(string.Join(", ", selectCols));
        }

        // FROM clause
        sb.Append($"\nFROM {ast.FromTable}");

        // JOIN clauses
        if (ast.Joins != null)
        {
            foreach (var join in ast.Joins)
            {
                sb.Append($"\n{join.JoinType} JOIN {join.Table} ON {join.OnLeft} = {join.OnRight}");
            }
        }

        // WHERE clause
        if (ast.Where != null)
        {
            sb.Append("\nWHERE ");
            sb.Append(CompileFilterGroup(ast.Where, sqlParams));
        }

        // ORDER BY clause
        if (ast.OrderBy != null && ast.OrderBy.Count > 0)
        {
            var orderCols = ast.OrderBy.Select(o =>
            {
                var colRef = string.IsNullOrEmpty(o.Table) ? o.Column : $"{o.Table}.{o.Column}";
                return $"{colRef} {o.Direction}";
            });
            sb.Append($"\nORDER BY {string.Join(", ", orderCols)}");
        }

        // LIMIT clause
        if (ast.Limit.HasValue)
        {
            sb.Append($"\nLIMIT {ast.Limit.Value}");
        }

        return sb.ToString();
    }

    private string CompileFilterGroup(AstFilterGroup group, Dictionary<string, object> sqlParams)
    {
        var conditions = new List<string>();

        if (group.Filters != null)
        {
            foreach (var filter in group.Filters)
            {
                conditions.Add(CompileFilter(filter, sqlParams));
            }
        }

        if (group.Groups != null)
        {
            foreach (var subGroup in group.Groups)
            {
                conditions.Add($"({CompileFilterGroup(subGroup, sqlParams)})");
            }
        }

        return string.Join($" {group.Logic} ", conditions);
    }

    private string CompileFilter(AstFilter filter, Dictionary<string, object> sqlParams)
    {
        var colRef = string.IsNullOrEmpty(filter.Table) ? filter.Column : $"{filter.Table}.{filter.Column}";

        // If it's a parameter reference
        if (!string.IsNullOrEmpty(filter.ParameterName))
        {
            return filter.Operator switch
            {
                "eq" => $"{colRef} = @{filter.ParameterName}",
                "neq" => $"{colRef} != @{filter.ParameterName}",
                "gt" => $"{colRef} > @{filter.ParameterName}",
                "gte" => $"{colRef} >= @{filter.ParameterName}",
                "lt" => $"{colRef} < @{filter.ParameterName}",
                "lte" => $"{colRef} <= @{filter.ParameterName}",
                "like" => $"{colRef} LIKE @{filter.ParameterName}",
                "is_null" => $"{colRef} IS NULL",
                "is_not_null" => $"{colRef} IS NOT NULL",
                _ => $"{colRef} = @{filter.ParameterName}"
            };
        }

        // Static value
        var paramName = $"p{sqlParams.Count}";
        sqlParams[paramName] = filter.Value!;

        return filter.Operator switch
        {
            "eq" => $"{colRef} = @{paramName}",
            "neq" => $"{colRef} != @{paramName}",
            "gt" => $"{colRef} > @{paramName}",
            "gte" => $"{colRef} >= @{paramName}",
            "lt" => $"{colRef} < @{paramName}",
            "lte" => $"{colRef} <= @{paramName}",
            "like" => $"{colRef} LIKE @{paramName}",
            "starts_with" => $"{colRef} LIKE CONCAT(@{paramName}, '%')",
            "ends_with" => $"{colRef} LIKE CONCAT('%', @{paramName})",
            "is_null" => $"{colRef} IS NULL",
            "is_not_null" => $"{colRef} IS NOT NULL",
            _ => $"{colRef} = @{paramName}"
        };
    }

    private async Task<List<Dictionary<string, object>>> ExecuteHardcodedPresetAsync(
        string handler, string cedula, Dictionary<string, object>? parameters)
    {
        return handler switch
        {
            "TssTop5Handler" => await ExecuteTssTop5Async(cedula),
            "CompanerosSalarioSimilarHandler" => await ExecuteCompanerosSalarioAsync(cedula, parameters),
            "VehiculoExisteHandler" => await ExecuteVehiculoExisteAsync(cedula),
            _ => throw new NotImplementedException($"Handler '{handler}' not implemented")
        };
    }

    // ============================================
    // HARDCODED PRESET HANDLERS
    // ============================================

    private async Task<List<Dictionary<string, object>>> ExecuteTssTop5Async(string cedula)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT id, RNC, CEDULA, FECHA, SALARIO, EsOficial
            FROM tss
            WHERE CEDULA = @Cedula
            ORDER BY FECHA DESC
            LIMIT 5";

        var results = await conn.QueryAsync<dynamic>(sql, new { Cedula = cedula });
        return results
            .Select(r => ConvertDynamicToDict((object)r))
            .ToList();
    }

    private async Task<List<Dictionary<string, object>>> ExecuteCompanerosSalarioAsync(string cedula, Dictionary<string, object>? parameters)
    {
        var tolerancePct = 0.10m;
        if (parameters != null && parameters.TryGetValue("tolerancePct", out var tol))
        {
            tolerancePct = Convert.ToDecimal(tol);
        }

        using var conn = _dbFactory.CreateConnection();

        // Step 1: Get the most recent RNC and SALARY for the input cedula
        var baseSql = @"
            SELECT RNC, SALARIO
            FROM tss
            WHERE CEDULA = @Cedula
            ORDER BY FECHA DESC
            LIMIT 1";

        var baseRecord = await conn.QueryFirstOrDefaultAsync<dynamic>(baseSql, new { Cedula = cedula });
        if (baseRecord == null)
        {
            return new List<Dictionary<string, object>>();
        }

        string rnc = baseRecord.RNC;
        decimal salary = baseRecord.SALARIO;
        decimal minSalary = salary * (1 - tolerancePct);
        decimal maxSalary = salary * (1 + tolerancePct);

        // Step 2: Find coworkers with similar salary
        var sql = @"
            WITH LatestTss AS (
                SELECT t.CEDULA, t.RNC, t.SALARIO, t.FECHA,
                       ROW_NUMBER() OVER (PARTITION BY t.CEDULA ORDER BY t.FECHA DESC) as rn
                FROM tss t
                WHERE t.RNC = @Rnc AND t.CEDULA != @Cedula
            ),
            LatestIloc AS (
                SELECT i.Cedula, i.Nombre, i.Telefono,
                       ROW_NUMBER() OVER (PARTITION BY i.Cedula ORDER BY i.FechaConsulta DESC) as rn
                FROM ilocalizadosappsprocessor i
            )
            SELECT 
                lt.CEDULA,
                COALESCE(li.Nombre, '') as Nombre,
                COALESCE(li.Telefono, '') as Telefono,
                lt.RNC,
                lt.SALARIO,
                lt.FECHA,
                ABS(lt.SALARIO - @BaseSalary) as SalaryDiff
            FROM LatestTss lt
            LEFT JOIN LatestIloc li ON lt.CEDULA = li.Cedula AND li.rn = 1
            WHERE lt.rn = 1
              AND lt.SALARIO BETWEEN @MinSalary AND @MaxSalary
            ORDER BY SalaryDiff ASC
            LIMIT 10";

        var results = await conn.QueryAsync<dynamic>(sql, new
        {
            Rnc = rnc,
            Cedula = cedula,
            BaseSalary = salary,
            MinSalary = minSalary,
            MaxSalary = maxSalary
        });

        return results
    .Select(r => ConvertDynamicToDict((object)r))
    .ToList();
    }

    private async Task<List<Dictionary<string, object>>> ExecuteVehiculoExisteAsync(string cedula)
    {
        using var conn = _dbFactory.CreateConnection();

        // Check if exists
        var existsSql = "SELECT COUNT(*) FROM vehi WHERE f19 = @Cedula";
        var count = await conn.ExecuteScalarAsync<int>(existsSql, new { Cedula = cedula });

        var result = new Dictionary<string, object>
        {
            ["existe"] = count > 0,
            ["total_vehiculos"] = count
        };

        if (count > 0)
        {
            // Get vehicle list (first 50)
            var listSql = @"
                SELECT f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f19
                FROM vehi
                WHERE f19 = @Cedula
                LIMIT 50";

            var vehicles = await conn.QueryAsync<dynamic>(listSql, new { Cedula = cedula });
            result["vehiculos"] = vehicles.Select(v => ConvertDynamicToDict(v)).ToList();
        }
        else
        {
            result["vehiculos"] = new List<Dictionary<string, object>>();
        }

        return new List<Dictionary<string, object>> { result };
    }

    private Dictionary<string, object> ConvertDynamicToDict(dynamic obj)
    {
        if (obj is IDictionary<string, object> dict)
        {
            return new Dictionary<string, object>(dict);
        }

        var result = new Dictionary<string, object>();
        foreach (var prop in ((object)obj).GetType().GetProperties())
        {
            result[prop.Name] = prop.GetValue(obj) ?? DBNull.Value;
        }
        return result;
    }
}
