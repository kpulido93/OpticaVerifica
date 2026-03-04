using System.Text.Json.Serialization;

namespace OptimaVerifica.Api.Models;

// ============================================
// JOB MODELS
// ============================================

public class Job
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PresetKey { get; set; } = string.Empty;
    public int PresetVersion { get; set; } = 1;
    public JobStatus Status { get; set; } = JobStatus.PENDING;
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int FailedItems { get; set; }
    public string? ParamsJson { get; set; }
    public string? ErrorMessage { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobStatus
{
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED,
    CANCELLED,
    PAUSED_BY_SCHEDULE
}

public class JobItem
{
    public long Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public JobItemStatus Status { get; set; } = JobItemStatus.PENDING;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobItemStatus
{
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED
}

public class JobResult
{
    public long Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public long? JobItemId { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string ResultJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class JobArtifact
{
    public long Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}

// ============================================
// PRESET MODELS
// ============================================

public class PresetDefinition
{
    public int Id { get; set; }
    public string PresetKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Dataset { get; set; } = string.Empty;
    public bool IsHardcoded { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PresetVersion
{
    public int Id { get; set; }
    public int PresetId { get; set; }
    public int Version { get; set; }
    public string AstJson { get; set; } = "{}";
    public string? CompiledSql { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PresetAllowedSchema
{
    public int Id { get; set; }
    public string Dataset { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? ColumnName { get; set; }
    public string? ColumnType { get; set; }
    public bool IsFilterable { get; set; } = true;
    public bool IsSortable { get; set; } = true;
    public bool IsSelectable { get; set; } = true;
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AllowedOperator
{
    public int Id { get; set; }
    public string OperatorKey { get; set; } = string.Empty;
    public string OperatorSql { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresValue { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
}

// ============================================
// AST MODELS (for Preset Designer)
// ============================================

public class PresetAst
{
    public string Type { get; set; } = "CUSTOM"; // HARDCODED or CUSTOM
    public string? Handler { get; set; } // For hardcoded presets
    public List<AstColumn> Select { get; set; } = new();
    public string? FromTable { get; set; }
    public List<AstJoin>? Joins { get; set; }
    public AstFilterGroup? Where { get; set; }
    public List<AstOrderBy>? OrderBy { get; set; }
    public int? Limit { get; set; }
    public List<AstInput>? Inputs { get; set; }
}

public class AstColumn
{
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? Aggregate { get; set; } // COUNT, SUM, AVG, MAX, MIN
}

public class AstJoin
{
    public string JoinType { get; set; } = "INNER"; // INNER, LEFT, RIGHT
    public string Table { get; set; } = string.Empty;
    public string OnLeft { get; set; } = string.Empty;
    public string OnRight { get; set; } = string.Empty;
}

public class AstFilterGroup
{
    public string Logic { get; set; } = "AND"; // AND or OR
    public List<AstFilter>? Filters { get; set; }
    public List<AstFilterGroup>? Groups { get; set; }
}

public class AstFilter
{
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public object? Value { get; set; }
    public string? ParameterName { get; set; } // For input parameters like @cedula
}

public class AstOrderBy
{
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string Direction { get; set; } = "ASC";
}

public class AstInput
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
    public object? Default { get; set; }
}

// ============================================
// DTOs (Request/Response)
// ============================================

public class CreateJobRequest
{
    public string PresetKey { get; set; } = string.Empty;
    public List<string>? Cedulas { get; set; }
    public Dictionary<string, object>? Params { get; set; }
}

public class JobResponse
{
    public string Id { get; set; } = string.Empty;
    public string PresetKey { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int FailedItems { get; set; }
    public double ProgressPercent => TotalItems > 0 ? Math.Round((double)ProcessedItems / TotalItems * 100, 2) : 0;
    public Dictionary<string, object>? Params { get; set; }
    public string? ErrorMessage { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class JobResultsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<Dictionary<string, object>> Results { get; set; } = new();
}

public class PresetResponse
{
    public int Id { get; set; }
    public string PresetKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Dataset { get; set; } = string.Empty;
    public bool IsHardcoded { get; set; }
    public bool IsEnabled { get; set; }
    public int CurrentVersion { get; set; }
    public List<AstInput>? Inputs { get; set; }
}

public class CreatePresetRequest
{
    public string PresetKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Dataset { get; set; } = string.Empty;
    public PresetAst Ast { get; set; } = new();
}

public class ExportRequest
{
    public string Format { get; set; } = "CSV"; // CSV, XLSX, JSON
}

public class SchemaResponse
{
    public string Dataset { get; set; } = string.Empty;
    public List<TableSchema> Tables { get; set; } = new();
    public List<AllowedOperator> Operators { get; set; } = new();
}

public class TableSchema
{
    public string TableName { get; set; } = string.Empty;
    public List<ColumnSchema> Columns { get; set; } = new();
}

public class ColumnSchema
{
    public string ColumnName { get; set; } = string.Empty;
    public string? ColumnType { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsSelectable { get; set; }
    public string? DisplayName { get; set; }
}

public class TestPresetRequest
{
    public string Cedula { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}

public class TestPresetResponse
{
    public bool Success { get; set; }
    public string? GeneratedSql { get; set; }
    public List<Dictionary<string, object>>? Results { get; set; }
    public string? ErrorMessage { get; set; }
    public long ExecutionTimeMs { get; set; }
}
