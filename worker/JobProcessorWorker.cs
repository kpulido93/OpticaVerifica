using System.Data;
using Dapper;
using MySqlConnector;
using Newtonsoft.Json;

namespace OptimaVerifica.Worker;

public class JobProcessorWorker : BackgroundService
{
    private readonly ILogger<JobProcessorWorker> _logger;
    private readonly IConfiguration _config;
    private readonly string _connectionString;
    private readonly int _maxConcurrentJobs;
    private readonly int _batchSize;
    private const int StatusCheckEveryItems = 10;
    private static readonly TimeSpan MissingSchemaWarningInterval = TimeSpan.FromMinutes(1);
    private DateTime _lastMissingSchemaWarningUtc = DateTime.MinValue;

    public JobProcessorWorker(ILogger<JobProcessorWorker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _connectionString = config.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");
        _maxConcurrentJobs = config.GetValue<int>("Worker:MaxConcurrentJobs", 5);
        _batchSize = config.GetValue<int>("Worker:BatchSize", 100);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Processor Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job processor loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken stoppingToken)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(stoppingToken);

        if (!await RequiredTablesExistAsync(conn))
        {
            LogMissingSchemaWarning();
            return;
        }

        IEnumerable<JobInfo> pendingJobs;
        try
        {
            // Get pending jobs
            pendingJobs = await conn.QueryAsync<JobInfo>(
                @"SELECT id as Id, preset_key as PresetKey, params_json as ParamsJson
                  FROM jobs
                  WHERE status = 'PENDING'
                  ORDER BY created_at
                  LIMIT @Limit",
                new { Limit = _maxConcurrentJobs });
        }
        catch (MySqlException ex) when (ex.Number == 1146)
        {
            LogMissingSchemaWarning();
            return;
        }

        var tasks = pendingJobs.Select(job => ProcessJobAsync(job, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task<bool> RequiredTablesExistAsync(IDbConnection conn)
    {
        var tableCount = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*)
              FROM information_schema.tables
              WHERE table_schema = DATABASE()
                AND table_name IN ('jobs', 'job_items', 'job_results')");

        return tableCount == 3;
    }

    private void LogMissingSchemaWarning()
    {
        var now = DateTime.UtcNow;
        if (now - _lastMissingSchemaWarningUtc < MissingSchemaWarningInterval)
        {
            return;
        }

        _lastMissingSchemaWarningUtc = now;
        _logger.LogWarning(
            "Worker schema not ready (missing jobs/job_items/job_results). Waiting for migrator to apply DB migrations.");
    }

    private async Task ProcessJobAsync(JobInfo job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing job {JobId} with preset {Preset}", job.Id, job.PresetKey);

        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(stoppingToken);

        try
        {
            var claimedRows = await conn.ExecuteAsync(
                @"UPDATE jobs
                  SET status = 'PROCESSING', started_at = COALESCE(started_at, NOW())
                  WHERE id = @Id AND status = 'PENDING'",
                new { Id = job.Id });

            if (claimedRows == 0)
            {
                _logger.LogInformation(
                    "Skipping job {JobId}: could not claim as PENDING (already claimed or status changed).",
                    job.Id);
                return;
            }

            var initialControlAction = await CheckJobControlStatusAsync(conn, job.Id);
            if (initialControlAction != JobControlAction.Continue)
            {
                _logger.LogInformation("Stopping job {JobId} before item processing due to status control.", job.Id);
                return;
            }

            // Get preset AST
            var presetAst = await GetPresetAstAsync(conn, job.PresetKey);
            if (presetAst == null)
            {
                throw new InvalidOperationException($"Preset '{job.PresetKey}' not found");
            }

            // Parse parameters
            var parameters = string.IsNullOrEmpty(job.ParamsJson)
                ? null
                : JsonConvert.DeserializeObject<Dictionary<string, object>>(job.ParamsJson);

            // Process items in batches
            var itemsSinceLastControlCheck = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                var controlAction = await CheckJobControlStatusAsync(conn, job.Id);
                if (controlAction != JobControlAction.Continue)
                {
                    _logger.LogInformation("Stopping job {JobId} while processing due to status control.", job.Id);
                    return;
                }

                var items = await conn.QueryAsync<JobItemInfo>(
                    @"SELECT id as Id, cedula as Cedula
                      FROM job_items
                      WHERE job_id = @JobId AND status = 'PENDING'
                      LIMIT @BatchSize",
                    new { JobId = job.Id, BatchSize = _batchSize });

                var itemList = items.ToList();
                if (itemList.Count == 0) break;

                foreach (var item in itemList)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    if (itemsSinceLastControlCheck >= StatusCheckEveryItems)
                    {
                        controlAction = await CheckJobControlStatusAsync(conn, job.Id);
                        if (controlAction != JobControlAction.Continue)
                        {
                            _logger.LogInformation(
                                "Stopping job {JobId} during batch due to status control.",
                                job.Id);
                            return;
                        }

                        itemsSinceLastControlCheck = 0;
                    }

                    try
                    {
                        // Execute preset for this cedula
                        var results = await ExecutePresetAsync(conn, presetAst, item.Cedula, parameters);

                        // Save results
                        foreach (var result in results)
                        {
                            await conn.ExecuteAsync(
                                @"INSERT INTO job_results (job_id, job_item_id, cedula, result_json, created_at)
                                  VALUES (@JobId, @ItemId, @Cedula, @ResultJson, NOW())",
                                new
                                {
                                    JobId = job.Id,
                                    ItemId = item.Id,
                                    Cedula = item.Cedula,
                                    ResultJson = JsonConvert.SerializeObject(result)
                                });
                        }

                        // Update item status
                        await conn.ExecuteAsync(
                            "UPDATE job_items SET status = 'COMPLETED', processed_at = NOW() WHERE id = @Id",
                            new { Id = item.Id });

                        // Increment processed count
                        await conn.ExecuteAsync(
                            "UPDATE jobs SET processed_items = processed_items + 1 WHERE id = @Id",
                            new { Id = job.Id });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing item {ItemId} for job {JobId}", item.Id, job.Id);

                        await conn.ExecuteAsync(
                            "UPDATE job_items SET status = 'FAILED', error_message = @Error, processed_at = NOW() WHERE id = @Id",
                            new { Id = item.Id, Error = ex.Message });

                        await conn.ExecuteAsync(
                            "UPDATE jobs SET processed_items = processed_items + 1, failed_items = failed_items + 1 WHERE id = @Id",
                            new { Id = job.Id });
                    }

                    itemsSinceLastControlCheck++;
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Stopping token triggered; leaving job {JobId} without final completion status.", job.Id);
                return;
            }

            var finalState = await GetJobStateAsync(conn, job.Id);
            if (finalState == null)
            {
                _logger.LogWarning("Job {JobId} no longer exists during finalization.", job.Id);
                return;
            }

            if (finalState.Status == "CANCELLED")
            {
                await MarkCancelledAsync(conn, job.Id);
                _logger.LogInformation("Job {JobId} finalized as CANCELLED.", job.Id);
                return;
            }

            if (finalState.Status == "PAUSED_BY_SCHEDULE")
            {
                _logger.LogInformation("Job {JobId} is PAUSED_BY_SCHEDULE; skipping completion finalization.", job.Id);
                return;
            }

            if (finalState.PendingItems > 0)
            {
                _logger.LogWarning(
                    "Job {JobId} still has {PendingItems} PENDING items. Completion status will not be set.",
                    job.Id,
                    finalState.PendingItems);
                return;
            }

            var finalStatus = finalState.FailedItems > 0 ? "COMPLETED_WITH_ERRORS" : "COMPLETED";
            var finalizedRows = await conn.ExecuteAsync(
                @"UPDATE jobs
                  SET status = @Status, completed_at = NOW()
                  WHERE id = @Id AND status = 'PROCESSING'",
                new { Id = job.Id, Status = finalStatus });

            if (finalizedRows == 0)
            {
                _logger.LogInformation("Job {JobId} status changed before finalization; completion update skipped.", job.Id);
                return;
            }

            _logger.LogInformation("Job {JobId} completed with status {FinalStatus}", job.Id, finalStatus);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Job worker cancellation requested while processing job {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", job.Id);

            var currentStatus = await GetJobStatusAsync(conn, job.Id);
            if (currentStatus == "CANCELLED")
            {
                await MarkCancelledAsync(conn, job.Id);
                return;
            }

            if (currentStatus == "PAUSED_BY_SCHEDULE")
            {
                _logger.LogInformation("Job {JobId} is paused; skipping FAILED transition.", job.Id);
                return;
            }

            await conn.ExecuteAsync(
                @"UPDATE jobs
                  SET status = 'FAILED', error_message = @Error, completed_at = NOW()
                  WHERE id = @Id AND status = 'PROCESSING'",
                new { Id = job.Id, Error = ex.Message });
        }
    }

    private async Task<JobControlAction> CheckJobControlStatusAsync(IDbConnection conn, string jobId)
    {
        var status = await GetJobStatusAsync(conn, jobId);
        if (status == null)
        {
            return JobControlAction.Stop;
        }

        if (status == "CANCELLED")
        {
            await MarkCancelledAsync(conn, jobId);
            return JobControlAction.Stop;
        }

        if (status == "PAUSED_BY_SCHEDULE")
        {
            return JobControlAction.Stop;
        }

        return JobControlAction.Continue;
    }

    private async Task<string?> GetJobStatusAsync(IDbConnection conn, string jobId)
    {
        return await conn.ExecuteScalarAsync<string?>(
            "SELECT status FROM jobs WHERE id = @Id",
            new { Id = jobId });
    }

    private async Task<JobStateSnapshot?> GetJobStateAsync(IDbConnection conn, string jobId)
    {
        return await conn.QueryFirstOrDefaultAsync<JobStateSnapshot>(
            @"SELECT
                j.status as Status,
                j.failed_items as FailedItems,
                (
                    SELECT COUNT(*)
                    FROM job_items ji
                    WHERE ji.job_id = j.id AND ji.status = 'PENDING'
                ) as PendingItems
              FROM jobs j
              WHERE j.id = @Id",
            new { Id = jobId });
    }

    private async Task MarkCancelledAsync(IDbConnection conn, string jobId)
    {
        await conn.ExecuteAsync(
            @"UPDATE jobs
              SET status = 'CANCELLED', completed_at = COALESCE(completed_at, NOW())
              WHERE id = @Id AND status = 'CANCELLED'",
            new { Id = jobId });
    }

    private async Task<PresetAstInfo?> GetPresetAstAsync(IDbConnection conn, string presetKey)
    {
        return await conn.QueryFirstOrDefaultAsync<PresetAstInfo>(
            @"SELECT pv.ast_json as AstJson, pd.dataset as Dataset
              FROM preset_definitions pd
              JOIN preset_versions pv ON pd.id = pv.preset_id AND pv.is_active = 1
              WHERE pd.preset_key = @PresetKey",
            new { PresetKey = presetKey });
    }

    private async Task<List<Dictionary<string, object>>> ExecutePresetAsync(
        IDbConnection conn, PresetAstInfo presetInfo, string cedula, Dictionary<string, object>? parameters)
    {
        var ast = JsonConvert.DeserializeObject<PresetAst>(presetInfo.AstJson);
        if (ast == null) return new List<Dictionary<string, object>>();

        // Handle hardcoded presets
        if (ast.Type == "HARDCODED" && !string.IsNullOrEmpty(ast.Handler))
        {
            return ast.Handler switch
            {
                "TssTop5Handler" => await ExecuteTssTop5Async(conn, cedula),
                "CompanerosSalarioSimilarHandler" => await ExecuteCompanerosSalarioAsync(conn, cedula, parameters),
                "VehiculoExisteHandler" => await ExecuteVehiculoExisteAsync(conn, cedula),
                _ => new List<Dictionary<string, object>>()
            };
        }

        // For custom AST, compile and execute
        var sql = CompileAstToSql(ast, cedula, parameters, out var sqlParams);
        var results = await conn.QueryAsync<dynamic>(sql, sqlParams);
        return results.Select(ConvertDynamicToDict).ToList();
    }

    private async Task<List<Dictionary<string, object>>> ExecuteTssTop5Async(IDbConnection conn, string cedula)
    {
        var results = await conn.QueryAsync<dynamic>(
            @"SELECT id, RNC, CEDULA, FECHA, SALARIO, EsOficial
              FROM tss WHERE CEDULA = @Cedula ORDER BY FECHA DESC LIMIT 5",
            new { Cedula = cedula });
        return results.Select(ConvertDynamicToDict).ToList();
    }

    private async Task<List<Dictionary<string, object>>> ExecuteCompanerosSalarioAsync(
        IDbConnection conn, string cedula, Dictionary<string, object>? parameters)
    {
        var tolerancePct = 0.10m;
        if (parameters != null && parameters.TryGetValue("tolerancePct", out var tol))
        {
            tolerancePct = Convert.ToDecimal(tol);
        }

        var baseRecord = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT RNC, SALARIO FROM tss WHERE CEDULA = @Cedula ORDER BY FECHA DESC LIMIT 1",
            new { Cedula = cedula });

        if (baseRecord == null) return new List<Dictionary<string, object>>();

        string rnc = baseRecord.RNC;
        decimal salary = baseRecord.SALARIO;

        var results = await conn.QueryAsync<dynamic>(
            @"WITH LatestTss AS (
                SELECT t.CEDULA, t.RNC, t.SALARIO, t.FECHA,
                       ROW_NUMBER() OVER (PARTITION BY t.CEDULA ORDER BY t.FECHA DESC) as rn
                FROM tss t WHERE t.RNC = @Rnc AND t.CEDULA != @Cedula
            ),
            LatestIloc AS (
                SELECT i.Cedula, i.Nombre, i.Telefono,
                       ROW_NUMBER() OVER (PARTITION BY i.Cedula ORDER BY i.FechaConsulta DESC) as rn
                FROM ilocalizadosappsprocessor i
            )
            SELECT lt.CEDULA, COALESCE(li.Nombre, '') as Nombre, COALESCE(li.Telefono, '') as Telefono,
                   lt.RNC, lt.SALARIO, lt.FECHA, ABS(lt.SALARIO - @BaseSalary) as SalaryDiff
            FROM LatestTss lt
            LEFT JOIN LatestIloc li ON lt.CEDULA = li.Cedula AND li.rn = 1
            WHERE lt.rn = 1 AND lt.SALARIO BETWEEN @MinSalary AND @MaxSalary
            ORDER BY SalaryDiff ASC LIMIT 10",
            new
            {
                Rnc = rnc,
                Cedula = cedula,
                BaseSalary = salary,
                MinSalary = salary * (1 - tolerancePct),
                MaxSalary = salary * (1 + tolerancePct)
            });

        return results.Select(ConvertDynamicToDict).ToList();
    }

    private async Task<List<Dictionary<string, object>>> ExecuteVehiculoExisteAsync(IDbConnection conn, string cedula)
    {
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM vehi WHERE f19 = @Cedula", new { Cedula = cedula });

        var result = new Dictionary<string, object>
        {
            ["existe"] = count > 0,
            ["total_vehiculos"] = count
        };

        if (count > 0)
        {
            var vehicles = await conn.QueryAsync<dynamic>(
                "SELECT f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15, f19 FROM vehi WHERE f19 = @Cedula LIMIT 50",
                new { Cedula = cedula });
            result["vehiculos"] = vehicles.Select(ConvertDynamicToDict).ToList();
        }

        return new List<Dictionary<string, object>> { result };
    }

    private string CompileAstToSql(PresetAst ast, string cedula, Dictionary<string, object>? parameters, out Dictionary<string, object> sqlParams)
    {
        sqlParams = new Dictionary<string, object> { ["cedula"] = cedula };
        if (parameters != null)
        {
            foreach (var p in parameters) sqlParams[p.Key] = p.Value;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("SELECT ");
        sb.Append(ast.Select?.Count > 0 
            ? string.Join(", ", ast.Select.Select(c => $"{c.Table}.{c.Column}"))
            : "*");
        sb.Append($" FROM {ast.FromTable}");

        if (ast.Where != null && ast.Where.Filters?.Count > 0)
        {
            var conditions = ast.Where.Filters.Select(f => $"{f.Table}.{f.Column} = @{f.ParameterName ?? "cedula"}");
            sb.Append($" WHERE {string.Join($" {ast.Where.Logic} ", conditions)}");
        }

        if (ast.OrderBy?.Count > 0)
        {
            sb.Append($" ORDER BY {string.Join(", ", ast.OrderBy.Select(o => $"{o.Table}.{o.Column} {o.Direction}"))}");
        }

        if (ast.Limit.HasValue) sb.Append($" LIMIT {ast.Limit.Value}");

        return sb.ToString();
    }

    private Dictionary<string, object> ConvertDynamicToDict(dynamic obj)
    {
        if (obj is IDictionary<string, object> dict) return new Dictionary<string, object>(dict);
        var result = new Dictionary<string, object>();
        foreach (var prop in ((object)obj).GetType().GetProperties())
        {
            result[prop.Name] = prop.GetValue(obj) ?? DBNull.Value;
        }
        return result;
    }
}

// DTOs
public class JobInfo { public string Id { get; set; } = ""; public string PresetKey { get; set; } = ""; public string? ParamsJson { get; set; } }
public class JobItemInfo { public long Id { get; set; } public string Cedula { get; set; } = ""; }
public class PresetAstInfo { public string AstJson { get; set; } = ""; public string Dataset { get; set; } = ""; }
public class JobStateSnapshot
{
    public string Status { get; set; } = "";
    public int FailedItems { get; set; }
    public int PendingItems { get; set; }
}

public enum JobControlAction
{
    Continue,
    Stop
}

public class PresetAst
{
    public string Type { get; set; } = "CUSTOM";
    public string? Handler { get; set; }
    public List<AstColumn>? Select { get; set; }
    public string? FromTable { get; set; }
    public AstFilterGroup? Where { get; set; }
    public List<AstOrderBy>? OrderBy { get; set; }
    public int? Limit { get; set; }
}

public class AstColumn { public string Table { get; set; } = ""; public string Column { get; set; } = ""; }
public class AstFilterGroup { public string Logic { get; set; } = "AND"; public List<AstFilter>? Filters { get; set; } }
public class AstFilter { public string Table { get; set; } = ""; public string Column { get; set; } = ""; public string? ParameterName { get; set; } }
public class AstOrderBy { public string Table { get; set; } = ""; public string Column { get; set; } = ""; public string Direction { get; set; } = "ASC"; }
