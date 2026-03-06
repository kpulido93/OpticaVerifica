using Microsoft.Extensions.Diagnostics.HealthChecks;
using MySqlConnector;

namespace OptimaVerifica.Api.HealthChecks;

public sealed class MySqlReadinessHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public MySqlReadinessHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("Connection string 'DefaultConnection' is missing.");
        }

        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("MySQL is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MySQL ping failed.", ex);
        }
    }
}
