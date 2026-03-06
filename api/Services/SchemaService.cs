using Dapper;
using OptimaVerifica.Api.Models;
using System.Data;

namespace OptimaVerifica.Api.Services;

public interface ISchemaService
{
    Task<SchemaResponse> GetAllowedSchemaAsync(string dataset);
    Task<List<string>> GetAllowedTablesAsync(string dataset);
    Task<List<string>> GetAllowedColumnsAsync(string dataset, string tableName);
    Task<bool> IsTableAllowedAsync(string dataset, string tableName);
    Task<bool> IsColumnAllowedAsync(string dataset, string tableName, string columnName);
    Task<List<AllowedOperator>> GetAllowedOperatorsAsync();
    Task<bool> IsOperatorAllowedAsync(string operatorKey);
    Task RefreshAllowedSchemaAsync(string dataset, string[]? includeTables = null, string[]? excludeTables = null);
}

public class SchemaService : ISchemaService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<SchemaService> _logger;

    public SchemaService(IDbConnectionFactory dbFactory, ILogger<SchemaService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<SchemaResponse> GetAllowedSchemaAsync(string dataset)
    {
        using var conn = _dbFactory.CreateConnection();

        var schemaSql = @"
            SELECT 
                table_name as TableName, column_name as ColumnName, column_type as ColumnType,
                is_filterable as IsFilterable, is_sortable as IsSortable, is_selectable as IsSelectable,
                display_name as DisplayName
            FROM preset_allowed_schema
            WHERE dataset = @Dataset
            ORDER BY table_name, column_name";

        var schemaItems = await conn.QueryAsync<PresetAllowedSchema>(schemaSql, new { Dataset = dataset });

        var tables = schemaItems
            .GroupBy(s => s.TableName)
            .Select(g => new TableSchema
            {
                TableName = g.Key,
                Columns = g.Where(c => !string.IsNullOrEmpty(c.ColumnName)).Select(c => new ColumnSchema
                {
                    ColumnName = c.ColumnName!,
                    ColumnType = c.ColumnType,
                    IsFilterable = c.IsFilterable,
                    IsSortable = c.IsSortable,
                    IsSelectable = c.IsSelectable,
                    DisplayName = c.DisplayName
                }).ToList()
            }).ToList();

        var operators = await GetAllowedOperatorsAsync();

        return new SchemaResponse
        {
            Dataset = dataset,
            Tables = tables,
            Operators = operators
        };
    }

    public async Task<List<string>> GetAllowedTablesAsync(string dataset)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = "SELECT DISTINCT table_name FROM preset_allowed_schema WHERE dataset = @Dataset";
        var tables = await conn.QueryAsync<string>(sql, new { Dataset = dataset });
        return tables.ToList();
    }

    public async Task<List<string>> GetAllowedColumnsAsync(string dataset, string tableName)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT column_name 
            FROM preset_allowed_schema 
            WHERE dataset = @Dataset AND table_name = @TableName AND column_name IS NOT NULL";
        var columns = await conn.QueryAsync<string>(sql, new { Dataset = dataset, TableName = tableName });
        return columns.ToList();
    }

    public async Task<bool> IsTableAllowedAsync(string dataset, string tableName)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT COUNT(*) 
            FROM preset_allowed_schema 
            WHERE dataset = @Dataset AND table_name = @TableName";
        var count = await conn.ExecuteScalarAsync<int>(sql, new { Dataset = dataset, TableName = tableName });
        return count > 0;
    }

    public async Task<bool> IsColumnAllowedAsync(string dataset, string tableName, string columnName)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT COUNT(*) 
            FROM preset_allowed_schema 
            WHERE dataset = @Dataset AND table_name = @TableName AND column_name = @ColumnName";
        var count = await conn.ExecuteScalarAsync<int>(sql, 
            new { Dataset = dataset, TableName = tableName, ColumnName = columnName });
        return count > 0;
    }

    public async Task<List<AllowedOperator>> GetAllowedOperatorsAsync()
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT 
                id as Id, operator_key as OperatorKey, operator_sql as OperatorSql,
                description as Description, requires_value as RequiresValue, is_enabled as IsEnabled
            FROM allowed_operators
            WHERE is_enabled = 1";

        var operators = await conn.QueryAsync<AllowedOperator>(sql);
        return operators.ToList();
    }

    public async Task<bool> IsOperatorAllowedAsync(string operatorKey)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM allowed_operators WHERE operator_key = @OperatorKey AND is_enabled = 1";
        var count = await conn.ExecuteScalarAsync<int>(sql, new { OperatorKey = operatorKey });
        return count > 0;
    }

    public async Task RefreshAllowedSchemaAsync(string dataset, string[]? includeTables = null, string[]? excludeTables = null)
    {
        if (string.IsNullOrWhiteSpace(dataset))
        {
            throw new ArgumentException("Dataset is required.", nameof(dataset));
        }

        var include = NormalizeTableFilter(includeTables);
        var exclude = NormalizeTableFilter(excludeTables);

        if (includeTables != null && include.Length == 0)
        {
            _logger.LogInformation("Schema refresh skipped for dataset {Dataset}: includeTables was provided but empty.", dataset);
            return;
        }

        using var conn = _dbFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var whereParts = new List<string> { "c.table_schema = DATABASE()" };
        var queryParams = new DynamicParameters();

        if (include.Length > 0)
        {
            whereParts.Add("c.table_name IN @IncludeTables");
            queryParams.Add("IncludeTables", include);
        }

        if (exclude.Length > 0)
        {
            whereParts.Add("c.table_name NOT IN @ExcludeTables");
            queryParams.Add("ExcludeTables", exclude);
        }

        var schemaRows = (await conn.QueryAsync<InformationSchemaColumnRow>(
            $@"SELECT
                   c.table_name as TableName,
                   c.column_name as ColumnName,
                   c.column_type as ColumnType
               FROM information_schema.columns c
               WHERE {string.Join(" AND ", whereParts)}
               ORDER BY c.table_name, c.ordinal_position",
            queryParams,
            tx)).ToList();

        if (include.Length > 0)
        {
            await conn.ExecuteAsync(
                @"DELETE FROM preset_allowed_schema
                  WHERE dataset = @Dataset
                    AND table_name IN @IncludeTables",
                new { Dataset = dataset, IncludeTables = include },
                tx);
        }
        else if (exclude.Length > 0)
        {
            await conn.ExecuteAsync(
                @"DELETE FROM preset_allowed_schema
                  WHERE dataset = @Dataset
                    AND table_name NOT IN @ExcludeTables",
                new { Dataset = dataset, ExcludeTables = exclude },
                tx);
        }
        else
        {
            await conn.ExecuteAsync(
                "DELETE FROM preset_allowed_schema WHERE dataset = @Dataset",
                new { Dataset = dataset },
                tx);
        }

        if (schemaRows.Count > 0)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO preset_allowed_schema
                    (dataset, table_name, column_name, column_type, is_filterable, is_sortable, is_selectable, display_name)
                  VALUES
                    (@Dataset, @TableName, @ColumnName, @ColumnType, 1, 1, 1, NULL)
                  ON DUPLICATE KEY UPDATE
                    column_type = VALUES(column_type),
                    is_filterable = VALUES(is_filterable),
                    is_sortable = VALUES(is_sortable),
                    is_selectable = VALUES(is_selectable),
                    display_name = VALUES(display_name)",
                schemaRows.Select(row => new
                {
                    Dataset = dataset,
                    row.TableName,
                    row.ColumnName,
                    ColumnType = Truncate(row.ColumnType, 50)
                }),
                tx);
        }

        tx.Commit();
        _logger.LogInformation("Refreshed allowed schema for dataset {Dataset}. Rows upserted: {Rows}.", dataset, schemaRows.Count);
    }

    private static string[] NormalizeTableFilter(string[]? tables)
    {
        if (tables == null)
        {
            return Array.Empty<string>();
        }

        return tables
            .Where(table => !string.IsNullOrWhiteSpace(table))
            .Select(table => table.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed class InformationSchemaColumnRow
    {
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string ColumnType { get; set; } = string.Empty;
    }
}
