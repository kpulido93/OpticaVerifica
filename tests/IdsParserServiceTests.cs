using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using OptimaVerifica.Api.Services;

namespace OptimaVerifica.Tests;

public class IdsParserServiceTests
{
    private readonly IdsParserService _service = new();

    [Fact]
    public async Task ParseCsv_ShouldDetectHeaders_AndDedupeIds()
    {
        var csv = "CEDULA,NOMBRE\n00100000001,Ana\n00100000002,Luis\n00100000001,Ana";
        var file = BuildFormFile(csv, "ids.csv", "text/csv");

        var result = await _service.ParseAsync(file);

        Assert.Contains("CEDULA", result.Headers);
        Assert.Equal("CEDULA", result.SelectedColumn);
        Assert.Equal(2, result.Ids.Count);
        Assert.Contains("00100000001", result.Ids);
        Assert.Contains("00100000002", result.Ids);
    }

    [Fact]
    public async Task ParseXlsx_ShouldUseSelectedColumn()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Sheet1");
        ws.Cell(1, 1).Value = "id";
        ws.Cell(1, 2).Value = "CEDULA";
        ws.Cell(2, 1).Value = "A1";
        ws.Cell(2, 2).Value = "123";
        ws.Cell(3, 1).Value = "A2";
        ws.Cell(3, 2).Value = "456";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var file = new FormFile(ms, 0, ms.Length, "file", "ids.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        var result = await _service.ParseAsync(file, "CEDULA");

        Assert.Equal("CEDULA", result.SelectedColumn);
        Assert.Equal(2, result.Ids.Count);
        Assert.Equal(["123", "456"], result.Ids);
    }

    private static IFormFile BuildFormFile(string content, string fileName, string contentType)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
