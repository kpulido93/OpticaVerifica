using DbUp;

var connectionString =
    ResolveConnectionString()
    ?? throw new InvalidOperationException(
        "Missing connection string. Set ConnectionStrings__DefaultConnection, ConnectionStrings:DefaultConnection, CONNECTION_STRING, or MYSQL_HOST/MYSQL_PORT/MYSQL_DATABASE/MYSQL_USER/MYSQL_PASSWORD.");

var migrationsPath = Environment.GetEnvironmentVariable("MIGRATIONS_PATH") ?? "/db/migrations";

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

static string? ResolveConnectionString()
{
    var directConnection =
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection")
        ?? Environment.GetEnvironmentVariable("CONNECTION_STRING");

    if (!string.IsNullOrWhiteSpace(directConnection))
    {
        return directConnection;
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
