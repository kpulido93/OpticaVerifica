using Dapper;
using OptimaVerifica.Api.Models;
using Newtonsoft.Json;

namespace OptimaVerifica.Api.Services;

public interface IJobService
{
    Task<Job> CreateJobAsync(CreateJobRequest request, string createdBy);
    Task<JobResponse?> GetJobAsync(string jobId, string username);
    Task<List<JobResponse>> GetJobsAsync(string username, string role, int page = 1, int pageSize = 20);
    Task<JobResultsResponse> GetJobResultsAsync(string jobId, int page = 1, int pageSize = 50);
    Task UpdateJobStatusAsync(string jobId, JobStatus status, string? errorMessage = null);
    Task AddJobResultAsync(string jobId, string cedula, Dictionary<string, object> result);
    Task<int> GetPendingItemCountAsync(string jobId);
    Task<List<JobItem>> GetPendingItemsAsync(string jobId, int batchSize);
    Task UpdateItemStatusAsync(long itemId, JobItemStatus status, string? errorMessage = null);
    Task IncrementProcessedAsync(string jobId, bool failed = false);
}

public class JobService : IJobService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IPresetService _presetService;
    private readonly ILogger<JobService> _logger;
    private readonly IConfiguration _config;

    public JobService(
        IDbConnectionFactory dbFactory, 
        IPresetService presetService,
        ILogger<JobService> logger,
        IConfiguration config)
    {
        _dbFactory = dbFactory;
        _presetService = presetService;
        _logger = logger;
        _config = config;
    }

    public async Task<Job> CreateJobAsync(CreateJobRequest request, string createdBy)
    {
        var preset = await _presetService.GetPresetByKeyAsync(request.PresetKey);
        if (preset == null)
        {
            throw new ArgumentException($"Preset '{request.PresetKey}' not found or not enabled");
        }

        // Validate and clean cedulas
        var cedulas = ValidateAndCleanCedulas(request.Cedulas);
        
        var maxIds = _config.GetValue<int>("Worker:MaxIdsPerJob", 10000);
        if (cedulas.Count > maxIds)
        {
            throw new ArgumentException($"Maximum {maxIds} cedulas allowed per job. Received: {cedulas.Count}");
        }

        using var conn = _dbFactory.CreateConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            var job = new Job
            {
                Id = Guid.NewGuid().ToString(),
                PresetKey = request.PresetKey,
                PresetVersion = preset.CurrentVersion,
                Status = JobStatus.PENDING,
                TotalItems = cedulas.Count,
                ParamsJson = request.Params != null ? JsonConvert.SerializeObject(request.Params) : null,
                CreatedBy = createdBy
            };

            var jobSql = @"
                INSERT INTO jobs (id, preset_key, preset_version, status, total_items, params_json, created_by, created_at)
                VALUES (@Id, @PresetKey, @PresetVersion, @Status, @TotalItems, @ParamsJson, @CreatedBy, @CreatedAt)";

            await conn.ExecuteAsync(jobSql, new
            {
                job.Id,
                job.PresetKey,
                job.PresetVersion,
                Status = job.Status.ToString(),
                job.TotalItems,
                job.ParamsJson,
                job.CreatedBy,
                job.CreatedAt
            }, transaction);

            // Insert job items in batches
            var batchSize = 1000;
            for (int i = 0; i < cedulas.Count; i += batchSize)
            {
                var batch = cedulas.Skip(i).Take(batchSize).ToList();
                var itemsSql = @"
                    INSERT INTO job_items (job_id, cedula, status, created_at)
                    VALUES (@JobId, @Cedula, 'PENDING', @CreatedAt)";

                var items = batch.Select(c => new
                {
                    JobId = job.Id,
                    Cedula = c,
                    CreatedAt = DateTime.UtcNow
                });

                await conn.ExecuteAsync(itemsSql, items, transaction);
            }

            transaction.Commit();
            _logger.LogInformation("Created job {JobId} with {ItemCount} cedulas for preset {Preset}", 
                job.Id, cedulas.Count, request.PresetKey);

            return job;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<JobResponse?> GetJobAsync(string jobId, string username)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT 
                j.id as Id, j.preset_key as PresetKey, j.status as Status,
                j.total_items as TotalItems, j.processed_items as ProcessedItems,
                j.failed_items as FailedItems, j.params_json as ParamsJson,
                j.error_message as ErrorMessage, j.created_by as CreatedBy,
                j.created_at as CreatedAt, j.started_at as StartedAt, j.completed_at as CompletedAt,
                pd.name as PresetName
            FROM jobs j
            LEFT JOIN preset_definitions pd ON j.preset_key = pd.preset_key
            WHERE j.id = @JobId";

        var job = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { JobId = jobId });
        if (job == null) return null;

        return new JobResponse
        {
            Id = job.Id,
            PresetKey = job.PresetKey,
            PresetName = job.PresetName ?? job.PresetKey,
            Status = Enum.Parse<JobStatus>(job.Status),
            TotalItems = (int)job.TotalItems,
            ProcessedItems = (int)job.ProcessedItems,
            FailedItems = (int)job.FailedItems,
            Params = string.IsNullOrEmpty(job.ParamsJson) 
                ? null 
                : JsonConvert.DeserializeObject<Dictionary<string, object>>(job.ParamsJson),
            ErrorMessage = job.ErrorMessage,
            CreatedBy = job.CreatedBy,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt
        };
    }

    public async Task<List<JobResponse>> GetJobsAsync(string username, string role, int page = 1, int pageSize = 20)
    {
        using var conn = _dbFactory.CreateConnection();

        var offset = (page - 1) * pageSize;
        var whereClause = role == "ADMIN" ? "" : "WHERE j.created_by = @Username";

        var sql = $@"
            SELECT 
                j.id as Id, j.preset_key as PresetKey, j.status as Status,
                j.total_items as TotalItems, j.processed_items as ProcessedItems,
                j.failed_items as FailedItems, j.params_json as ParamsJson,
                j.error_message as ErrorMessage, j.created_by as CreatedBy,
                j.created_at as CreatedAt, j.started_at as StartedAt, j.completed_at as CompletedAt,
                pd.name as PresetName
            FROM jobs j
            LEFT JOIN preset_definitions pd ON j.preset_key = pd.preset_key
            {whereClause}
            ORDER BY j.created_at DESC
            LIMIT @PageSize OFFSET @Offset";

        var jobs = await conn.QueryAsync<dynamic>(sql, new { Username = username, PageSize = pageSize, Offset = offset });

        return jobs.Select(job => new JobResponse
        {
            Id = job.Id,
            PresetKey = job.PresetKey,
            PresetName = job.PresetName ?? job.PresetKey,
            Status = Enum.Parse<JobStatus>(job.Status),
            TotalItems = (int)job.TotalItems,
            ProcessedItems = (int)job.ProcessedItems,
            FailedItems = (int)job.FailedItems,
            Params = string.IsNullOrEmpty(job.ParamsJson)
                ? null
                : JsonConvert.DeserializeObject<Dictionary<string, object>>(job.ParamsJson),
            ErrorMessage = job.ErrorMessage,
            CreatedBy = job.CreatedBy,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt
        }).ToList();
    }

    public async Task<JobResultsResponse> GetJobResultsAsync(string jobId, int page = 1, int pageSize = 50)
    {
        using var conn = _dbFactory.CreateConnection();

        var countSql = "SELECT COUNT(*) FROM job_results WHERE job_id = @JobId";
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, new { JobId = jobId });

        var offset = (page - 1) * pageSize;
        var sql = @"
            SELECT cedula as Cedula, result_json as ResultJson
            FROM job_results
            WHERE job_id = @JobId
            ORDER BY id
            LIMIT @PageSize OFFSET @Offset";

        var results = await conn.QueryAsync<dynamic>(sql, new { JobId = jobId, PageSize = pageSize, Offset = offset });

        List<Dictionary<string, object>> parsedResults = ((IEnumerable<dynamic>)results)
            .Select(r =>
            {
                string resultJson = (string)r.ResultJson;
                string cedula = (string)r.Cedula;

                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultJson)
                           ?? new Dictionary<string, object>();

                dict["cedula"] = cedula;
                return dict;
            })
            .ToList();

        return new JobResultsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Results = parsedResults
        };
    }

    public async Task UpdateJobStatusAsync(string jobId, JobStatus status, string? errorMessage = null)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            UPDATE jobs 
            SET status = @Status, 
                error_message = @ErrorMessage,
                started_at = CASE WHEN @Status = 'PROCESSING' AND started_at IS NULL THEN NOW() ELSE started_at END,
                completed_at = CASE WHEN @Status IN ('COMPLETED', 'COMPLETED_WITH_ERRORS', 'FAILED', 'CANCELLED') THEN NOW() ELSE completed_at END
            WHERE id = @JobId";

        await conn.ExecuteAsync(sql, new { JobId = jobId, Status = status.ToString(), ErrorMessage = errorMessage });
    }

    public async Task AddJobResultAsync(string jobId, string cedula, Dictionary<string, object> result)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            INSERT INTO job_results (job_id, cedula, result_json, created_at)
            VALUES (@JobId, @Cedula, @ResultJson, @CreatedAt)";

        await conn.ExecuteAsync(sql, new
        {
            JobId = jobId,
            Cedula = cedula,
            ResultJson = JsonConvert.SerializeObject(result),
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<int> GetPendingItemCountAsync(string jobId)
    {
        using var conn = _dbFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM job_items WHERE job_id = @JobId AND status = 'PENDING'",
            new { JobId = jobId });
    }

    public async Task<List<JobItem>> GetPendingItemsAsync(string jobId, int batchSize)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            SELECT id as Id, job_id as JobId, cedula as Cedula, status as Status
            FROM job_items
            WHERE job_id = @JobId AND status = 'PENDING'
            LIMIT @BatchSize";

        var items = await conn.QueryAsync<dynamic>(sql, new { JobId = jobId, BatchSize = batchSize });

        return items.Select(i => new JobItem
        {
            Id = (long)i.Id,
            JobId = i.JobId,
            Cedula = i.Cedula,
            Status = Enum.Parse<JobItemStatus>(i.Status)
        }).ToList();
    }

    public async Task UpdateItemStatusAsync(long itemId, JobItemStatus status, string? errorMessage = null)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = @"
            UPDATE job_items 
            SET status = @Status, 
                error_message = @ErrorMessage,
                processed_at = NOW()
            WHERE id = @ItemId";

        await conn.ExecuteAsync(sql, new { ItemId = itemId, Status = status.ToString(), ErrorMessage = errorMessage });
    }

    public async Task IncrementProcessedAsync(string jobId, bool failed = false)
    {
        using var conn = _dbFactory.CreateConnection();

        var sql = failed
            ? "UPDATE jobs SET processed_items = processed_items + 1, failed_items = failed_items + 1 WHERE id = @JobId"
            : "UPDATE jobs SET processed_items = processed_items + 1 WHERE id = @JobId";

        await conn.ExecuteAsync(sql, new { JobId = jobId });
    }

    private List<string> ValidateAndCleanCedulas(List<string>? cedulas)
    {
        if (cedulas == null || cedulas.Count == 0)
        {
            throw new ArgumentException("At least one cedula is required");
        }

        return cedulas
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct()
            .ToList();
    }
}
