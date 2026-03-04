using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;

namespace OptimaVerifica.Api.Services;

public interface IIdsParserService
{
    Task<ParsedIdsResult> ParseAsync(IFormFile file, string? selectedColumn = null, CancellationToken cancellationToken = default);
}

public class ParsedIdsResult
{
    public List<string> Headers { get; set; } = new();
    public string SuggestedColumn { get; set; } = string.Empty;
    public string SelectedColumn { get; set; } = string.Empty;
    public List<Dictionary<string, string>> SampleRows { get; set; } = new();
    public List<string> Ids { get; set; } = new();
    public int TotalRows { get; set; }
}

public class IdsParserService : IIdsParserService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const int MaxIds = 20000;
    private static readonly string[] SuggestedNames = ["cedula", "cédula", "id", "identificacion", "identificación", "documento"];

    public async Task<ParsedIdsResult> ParseAsync(IFormFile file, string? selectedColumn = null, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new ArgumentException("El archivo está vacío.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("El archivo excede el tamaño máximo permitido (10MB).");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        return ext switch
        {
            ".csv" => await ParseCsvAsync(file, selectedColumn, cancellationToken),
            ".xlsx" => await ParseXlsxAsync(file, selectedColumn, cancellationToken),
            _ => throw new ArgumentException("Formato no soportado. Solo se permiten archivos .csv y .xlsx.")
        };
    }

    private async Task<ParsedIdsResult> ParseCsvAsync(IFormFile file, string? selectedColumn, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!await csv.ReadAsync())
        {
            throw new ArgumentException("No se pudieron leer encabezados del CSV.");
        }

        csv.ReadHeader();

        var headers = csv.HeaderRecord?.Where(h => !string.IsNullOrWhiteSpace(h)).ToList() ?? new List<string>();
        if (headers.Count == 0)
        {
            throw new ArgumentException("El CSV no contiene encabezados válidos.");
        }

        var rows = new List<Dictionary<string, string>>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                row[header] = csv.GetField(header)?.Trim() ?? string.Empty;
            }
            rows.Add(row);
        }

        return BuildResult(headers, rows, selectedColumn);
    }

    private async Task<ParsedIdsResult> ParseXlsxAsync(IFormFile file, string? selectedColumn, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var workbook = new XLWorkbook(memory);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            throw new ArgumentException("El archivo XLSX no contiene hojas.");
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow == null)
        {
            throw new ArgumentException("No se encontraron encabezados en el XLSX.");
        }

        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var headers = new List<string>();
        for (var col = 1; col <= lastCol; col++)
        {
            var header = headerRow.Cell(col).GetString().Trim();
            headers.Add(string.IsNullOrWhiteSpace(header) ? $"col_{col}" : header);
        }

        var rows = new List<Dictionary<string, string>>();
        var firstDataRow = headerRow.RowNumber() + 1;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? firstDataRow - 1;

        for (var rowNumber = firstDataRow; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>();
            var hasValue = false;
            for (var col = 1; col <= headers.Count; col++)
            {
                var value = worksheet.Cell(rowNumber, col).GetFormattedString().Trim();
                if (!string.IsNullOrWhiteSpace(value)) hasValue = true;
                row[headers[col - 1]] = value;
            }

            if (hasValue)
            {
                rows.Add(row);
            }
        }

        return BuildResult(headers, rows, selectedColumn);
    }

    private static ParsedIdsResult BuildResult(List<string> headers, List<Dictionary<string, string>> rows, string? selectedColumn)
    {
        var suggested = SuggestHeader(headers);
        var selected = string.IsNullOrWhiteSpace(selectedColumn) ? suggested : selectedColumn.Trim();

        if (!headers.Contains(selected, StringComparer.OrdinalIgnoreCase))
        {
            selected = suggested;
        }

        var ids = rows
            .Select(r => r.FirstOrDefault(kv => string.Equals(kv.Key, selected, StringComparison.OrdinalIgnoreCase)).Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count > MaxIds)
        {
            throw new ArgumentException($"Se excedió el máximo de IDs permitidos ({MaxIds}).");
        }

        return new ParsedIdsResult
        {
            Headers = headers,
            SuggestedColumn = suggested,
            SelectedColumn = selected,
            SampleRows = rows.Take(10).ToList(),
            Ids = ids,
            TotalRows = rows.Count
        };
    }

    private static string SuggestHeader(List<string> headers)
    {
        foreach (var candidate in SuggestedNames)
        {
            var match = headers.FirstOrDefault(h => Normalize(h) == Normalize(candidate));
            if (!string.IsNullOrWhiteSpace(match)) return match;
        }

        return headers.First();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
    }
}
