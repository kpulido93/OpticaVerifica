using DbUp;

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? throw new InvalidOperationException("Missing connection string. Set ConnectionStrings__DefaultConnection or CONNECTION_STRING.");

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
