using System.Text.Json;
using DbUp;

var connectionString =
    ResolveConnectionString(FindRepositoryRoot())
    ?? throw new InvalidOperationException(
        "Missing connection string. Set ConnectionStrings__DefaultConnection, ConnectionStrings:DefaultConnection, CONNECTION_STRING, MYSQL_HOST/MYSQL_PORT/MYSQL_DATABASE/MYSQL_USER/MYSQL_PASSWORD, or api/appsettings*.json.");

var migrationsPath = ResolveMigrationsPath(FindRepositoryRoot());

if (!Directory.Exists(migrationsPath))
{
    Console.Error.WriteLine($"Migrations path not found: {migrationsPath}");
    return 1;
}

Console.WriteLine($"Using migrations path: {migrationsPath}");

EnsureDatabase.For.MySqlDatabase(connectionString);

var upgrader = DeployChanges.To
    .MySqlDatabase(connectionString)
    .WithScriptsFromFileSystem(migrationsPath)
    .LogToConsole()
    .Build();

var scriptsToExecute = upgrader.GetScriptsToExecute();
Console.WriteLine($"Pending scripts: {scriptsToExecute.Count()}");

var result = upgrader.PerformUpgrade();
if (!result.Successful)
{
    Console.Error.WriteLine(result.Error);
    return 1;
}

Console.WriteLine("Migrations applied successfully.");
return 0;

static string? ResolveConnectionString(DirectoryInfo? repoRoot)
{
    var directConnection =
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection")
        ?? Environment.GetEnvironmentVariable("CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(directConnection))
    {
        return directConnection;
    }

    var environmentName =
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Production";

    var migratorConfigConnection = ReadConnectionStringFromJsonFiles(
        Directory.GetCurrentDirectory(),
        environmentName);
    if (!string.IsNullOrWhiteSpace(migratorConfigConnection))
    {
        return migratorConfigConnection;
    }

    var appBaseConnection = ReadConnectionStringFromJsonFiles(
        AppContext.BaseDirectory,
        environmentName);
    if (!string.IsNullOrWhiteSpace(appBaseConnection))
    {
        return appBaseConnection;
    }

    if (repoRoot != null)
    {
        var apiConfigDirectory = Path.Combine(repoRoot.FullName, "api");
        var apiConfigConnection = ReadConnectionStringFromJsonFiles(
            apiConfigDirectory,
            environmentName);
        if (!string.IsNullOrWhiteSpace(apiConfigConnection))
        {
            return apiConfigConnection;
        }
    }

    var host = Environment.GetEnvironmentVariable("MYSQL_HOST");
    var port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306";
    var database = Environment.GetEnvironmentVariable("MYSQL_DATABASE");
    var user = Environment.GetEnvironmentVariable("MYSQL_USER");
    var password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD");

    if (!string.IsNullOrWhiteSpace(host)
        && !string.IsNullOrWhiteSpace(database)
        && !string.IsNullOrWhiteSpace(user)
        && password is not null)
    {
        return $"Server={host};Port={port};Database={database};User={user};Password={password};";
    }

    return null;
}

static string ResolveMigrationsPath(DirectoryInfo? repoRoot)
{
    var configuredPath = Environment.GetEnvironmentVariable("MIGRATIONS_PATH");
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        return configuredPath;
    }

    const string containerPath = "/db/migrations";
    if (Directory.Exists(containerPath))
    {
        return containerPath;
    }

    if (repoRoot != null)
    {
        var repoMigrationsPath = Path.Combine(repoRoot.FullName, "db", "migrations");
        if (Directory.Exists(repoMigrationsPath))
        {
            return repoMigrationsPath;
        }
    }

    return Path.Combine(Directory.GetCurrentDirectory(), "db", "migrations");
}

static string? ReadConnectionStringFromJsonFiles(string baseDirectory, string environmentName)
{
    if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
    {
        return null;
    }

    string? resolved = null;
    var appsettingsPath = Path.Combine(baseDirectory, "appsettings.json");
    var envSettingsPath = Path.Combine(baseDirectory, $"appsettings.{environmentName}.json");

    var fromBase = ReadDefaultConnectionFromJson(appsettingsPath);
    if (!string.IsNullOrWhiteSpace(fromBase))
    {
        resolved = fromBase;
    }

    var fromEnvironment = ReadDefaultConnectionFromJson(envSettingsPath);
    if (!string.IsNullOrWhiteSpace(fromEnvironment))
    {
        resolved = fromEnvironment;
    }

    return resolved;
}

static string? ReadDefaultConnectionFromJson(string filePath)
{
    if (!File.Exists(filePath))
    {
        return null;
    }

    try
    {
        using var stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
        {
            return null;
        }

        if (!connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
        {
            return null;
        }

        return defaultConnection.ValueKind == JsonValueKind.String
            ? defaultConnection.GetString()
            : null;
    }
    catch
    {
        return null;
    }
}

static DirectoryInfo? FindRepositoryRoot()
{
    var startCandidates = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var start in startCandidates)
    {
        if (string.IsNullOrWhiteSpace(start) || !Directory.Exists(start))
        {
            continue;
        }

        var current = new DirectoryInfo(start);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OptimaVerifica.sln")))
            {
                return current;
            }

            current = current.Parent;
        }
    }

    return null;
}
