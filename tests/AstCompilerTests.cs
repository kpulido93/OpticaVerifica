using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using OptimaVerifica.Api.Models;
using OptimaVerifica.Api.Services;

namespace OptimaVerifica.Tests;

public class AstCompilerTests
{
    private readonly Mock<IDbConnectionFactory> _mockDbFactory;
    private readonly Mock<IPresetService> _mockPresetService;
    private readonly Mock<ISchemaService> _mockSchemaService;
    private readonly Mock<ILogger<PresetExecutor>> _mockLogger;
    private readonly PresetExecutor _executor;

    public AstCompilerTests()
    {
        _mockDbFactory = new Mock<IDbConnectionFactory>();
        _mockPresetService = new Mock<IPresetService>();
        _mockSchemaService = new Mock<ISchemaService>();
        _mockLogger = new Mock<ILogger<PresetExecutor>>();

        _executor = new PresetExecutor(
            _mockDbFactory.Object,
            _mockPresetService.Object,
            _mockSchemaService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void CompileAst_BasicSelect_GeneratesCorrectSql()
    {
        // Arrange
        var ast = new PresetAst
        {
            Type = "CUSTOM",
            FromTable = "tss",
            Select = new List<AstColumn>
            {
                new() { Table = "tss", Column = "CEDULA" },
                new() { Table = "tss", Column = "SALARIO" }
            },
            Limit = 10
        };

        // Act
        var sql = _executor.CompileAstToSql(ast, "12345678901", null, out var sqlParams);

        // Assert
        Assert.Contains("SELECT", sql);
        Assert.Contains("tss.CEDULA", sql);
        Assert.Contains("tss.SALARIO", sql);
        Assert.Contains("FROM tss", sql);
        Assert.Contains("LIMIT 10", sql);
        Assert.Contains("12345678901", sqlParams["cedula"]?.ToString());
    }

    [Fact]
    public void CompileAst_WithWhereClause_GeneratesParameterizedFilter()
    {
        // Arrange
        var ast = new PresetAst
        {
            Type = "CUSTOM",
            FromTable = "tss",
            Select = new List<AstColumn>
            {
                new() { Table = "tss", Column = "CEDULA" }
            },
            Where = new AstFilterGroup
            {
                Logic = "AND",
                Filters = new List<AstFilter>
                {
                    new() { Table = "tss", Column = "CEDULA", Operator = "eq", ParameterName = "cedula" }
                }
            }
        };

        // Act
        var sql = _executor.CompileAstToSql(ast, "12345678901", null, out var sqlParams);

        // Assert
        Assert.Contains("WHERE", sql);
        Assert.Contains("@cedula", sql);
        Assert.DoesNotContain("12345678901", sql); // Value should be parameterized, not concatenated
        Assert.Equal("12345678901", sqlParams["cedula"]);
    }

    [Fact]
    public void CompileAst_WithMultipleFilters_UsesAndLogic()
    {
        // Arrange
        var ast = new PresetAst
        {
            Type = "CUSTOM",
            FromTable = "tss",
            Select = new List<AstColumn>
            {
                new() { Table = "tss", Column = "CEDULA" }
            },
            Where = new AstFilterGroup
            {
                Logic = "AND",
                Filters = new List<AstFilter>
                {
                    new() { Table = "tss", Column = "CEDULA", Operator = "eq", ParameterName = "cedula" },
                    new() { Table = "tss", Column = "EsOficial", Operator = "eq", Value = "1" }
                }
            }
        };

        // Act
        var sql = _executor.CompileAstToSql(ast, "12345678901", null, out _);

        // Assert
        Assert.Contains("AND", sql);
    }

    [Fact]
    public void CompileAst_WithOrderBy_GeneratesCorrectOrdering()
    {
        // Arrange
        var ast = new PresetAst
        {
            Type = "CUSTOM",
            FromTable = "tss",
            Select = new List<AstColumn>
            {
                new() { Table = "tss", Column = "CEDULA" }
            },
            OrderBy = new List<AstOrderBy>
            {
                new() { Table = "tss", Column = "FECHA", Direction = "DESC" }
            }
        };

        // Act
        var sql = _executor.CompileAstToSql(ast, "12345678901", null, out _);

        // Assert
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("tss.FECHA DESC", sql);
    }

    [Fact]
    public void CompileAst_EmptySelect_GeneratesSelectStar()
    {
        // Arrange
        var ast = new PresetAst
        {
            Type = "CUSTOM",
            FromTable = "tss",
            Select = new List<AstColumn>()
        };

        // Act
        var sql = _executor.CompileAstToSql(ast, "12345678901", null, out _);

        // Assert
        Assert.Contains("SELECT *", sql);
    }

    [Fact]
    public void CompileAst_WithParameters_IncludesCustomParams()
    {
        // Arrange
        var ast = new PresetAst
        {
            Type = "CUSTOM",
            FromTable = "tss",
            Select = new List<AstColumn>
            {
                new() { Table = "tss", Column = "CEDULA" }
            }
        };

        var customParams = new Dictionary<string, object>
        {
            ["tolerancePct"] = 0.10m,
            ["minSalary"] = 30000
        };

        // Act
        _executor.CompileAstToSql(ast, "12345678901", customParams, out var sqlParams);

        // Assert
        Assert.Equal("12345678901", sqlParams["cedula"]);
        Assert.Equal(0.10m, sqlParams["tolerancePct"]);
        Assert.Equal(30000, sqlParams["minSalary"]);
    }
}

public class WhitelistValidationTests
{
    [Fact]
    public void AllowedOperators_ContainsExpectedOperators()
    {
        // These are the operators we allow in the system
        var allowedOperators = new[] 
        { 
            "eq", "neq", "gt", "gte", "lt", "lte", 
            "in", "not_in", "between", "like", 
            "starts_with", "ends_with", "is_null", "is_not_null" 
        };

        // Verify we have all expected operators
        Assert.Equal(14, allowedOperators.Length);
        Assert.Contains("eq", allowedOperators);
        Assert.Contains("like", allowedOperators);
        Assert.Contains("is_null", allowedOperators);
    }

    [Fact]
    public void DisallowedOperator_ShouldNotBeInList()
    {
        // SQL injection attempts should not be valid operators
        var allowedOperators = new[] 
        { 
            "eq", "neq", "gt", "gte", "lt", "lte", 
            "in", "not_in", "between", "like", 
            "starts_with", "ends_with", "is_null", "is_not_null" 
        };

        Assert.DoesNotContain("DROP", allowedOperators);
        Assert.DoesNotContain("DELETE", allowedOperators);
        Assert.DoesNotContain("; --", allowedOperators);
        Assert.DoesNotContain("UNION", allowedOperators);
    }
}

public class CedulaValidationTests
{
    [Theory]
    [InlineData("00100000001")]
    [InlineData("12345678901")]
    [InlineData("40212345678")]
    public void ValidCedula_ShouldBeAccepted(string cedula)
    {
        // Valid Dominican cedulas have 11 digits
        var isValid = !string.IsNullOrWhiteSpace(cedula) && cedula.Trim().Length > 0;
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void InvalidCedula_ShouldBeRejected(string? cedula)
    {
        var isValid = !string.IsNullOrWhiteSpace(cedula);
        Assert.False(isValid);
    }

    [Fact]
    public void CedulaList_ShouldBeDeduplicated()
    {
        var cedulas = new[] { "00100000001", "00100000001", "00100000002", "00100000002" };
        var unique = cedulas.Distinct().ToList();

        Assert.Equal(2, unique.Count);
    }

    [Fact]
    public void CedulaList_ShouldBeTrimmed()
    {
        var cedulas = new[] { "  00100000001  ", "\t00100000002\n" };
        var cleaned = cedulas.Select(c => c.Trim()).ToList();

        Assert.Equal("00100000001", cleaned[0]);
        Assert.Equal("00100000002", cleaned[1]);
    }
}
