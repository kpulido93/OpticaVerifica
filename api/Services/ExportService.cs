using System.Globalization;
using System.Text;
using CsvHelper;
using ClosedXML.Excel;
using Newtonsoft.Json;
using OptimaVerifica.Api.Models;

namespace OptimaVerifica.Api.Services;

public interface IExportService
{
    Task<(byte[] data, string fileName, string contentType)> ExportJobResultsAsync(string jobId, string format);
}

public class ExportService : IExportService
{
    private readonly IJobService _jobService;
    private readonly IConfiguration _config;
    private readonly ILogger<ExportService> _logger;

    public ExportService(IJobService jobService, IConfiguration config, ILogger<ExportService> logger)
    {
        _jobService = jobService;
        _config = config;
        _logger = logger;
    }

    public async Task<(byte[] data, string fileName, string contentType)> ExportJobResultsAsync(string jobId, string format)
    {
        var maxRows = _config.GetValue<int>("Export:MaxRows", 100000);
        var allResults = new List<Dictionary<string, object>>();
        
        int page = 1;
        int pageSize = 1000;
        
        while (allResults.Count < maxRows)
        {
            var results = await _jobService.GetJobResultsAsync(jobId, page, pageSize);
            if (results.Results.Count == 0) break;
            
            allResults.AddRange(results.Results);
            if (results.Results.Count < pageSize) break;
            
            page++;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        
        return format.ToUpper() switch
        {
            "CSV" => ExportToCsv(allResults, jobId, timestamp),
            "XLSX" => ExportToXlsx(allResults, jobId, timestamp),
            "JSON" => ExportToJson(allResults, jobId, timestamp),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    private (byte[] data, string fileName, string contentType) ExportToCsv(
        List<Dictionary<string, object>> results, string jobId, string timestamp)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        if (results.Count == 0)
        {
            writer.WriteLine("No results");
            writer.Flush();
            return (memoryStream.ToArray(), $"job_{jobId}_{timestamp}.csv", "text/csv");
        }

        // Get all unique keys
        var allKeys = results.SelectMany(r => r.Keys).Distinct().ToList();
        
        // Write header
        foreach (var key in allKeys)
        {
            csv.WriteField(key);
        }
        csv.NextRecord();

        // Write data
        foreach (var row in results)
        {
            foreach (var key in allKeys)
            {
                csv.WriteField(row.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "");
            }
            csv.NextRecord();
        }

        writer.Flush();
        return (memoryStream.ToArray(), $"job_{jobId}_{timestamp}.csv", "text/csv");
    }

    private (byte[] data, string fileName, string contentType) ExportToXlsx(
        List<Dictionary<string, object>> results, string jobId, string timestamp)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Results");

        if (results.Count == 0)
        {
            worksheet.Cell(1, 1).Value = "No results";
        }
        else
        {
            var allKeys = results.SelectMany(r => r.Keys).Distinct().ToList();

            // Header
            for (int i = 0; i < allKeys.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = allKeys[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            // Data
            for (int row = 0; row < results.Count; row++)
            {
                for (int col = 0; col < allKeys.Count; col++)
                {
                    var value = results[row].TryGetValue(allKeys[col], out var v) ? v?.ToString() ?? "" : "";
                    worksheet.Cell(row + 2, col + 1).Value = value;
                }
            }

            worksheet.Columns().AdjustToContents();
        }

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return (memoryStream.ToArray(), $"job_{jobId}_{timestamp}.xlsx", 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private (byte[] data, string fileName, string contentType) ExportToJson(
        List<Dictionary<string, object>> results, string jobId, string timestamp)
    {
        var json = JsonConvert.SerializeObject(new { results, exportedAt = DateTime.UtcNow }, Formatting.Indented);
        var bytes = Encoding.UTF8.GetBytes(json);
        return (bytes, $"job_{jobId}_{timestamp}.json", "application/json");
    }
}
