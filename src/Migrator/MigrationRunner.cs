using System;
using System.Reflection;
using DbUp;
using MySqlConnector;

namespace OptimaVerifica.Migrations;

public static class MigrationRunner
{
    public static void EnsureMigrated(
        string connectionString,
        string lockName = "optima_veriifica_migrate",
        int lockSeconds = 60)
    {
        using var conn = new MySqlConnection(connectionString);
        conn.Open();

        // Lock global para evitar carrera si API y Worker arrancan a la vez
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT GET_LOCK(@name, @secs);";
            cmd.Parameters.AddWithValue("@name", lockName);
            cmd.Parameters.AddWithValue("@secs", lockSeconds);

            var got = Convert.ToInt32(cmd.ExecuteScalar());
            if (got != 1)
                throw new InvalidOperationException($"Could not acquire migration lock '{lockName}'.");
        }

        try
        {
            var upgrader = DeployChanges.To
                .MySqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
                throw new InvalidOperationException("Database migration failed.", result.Error);
        }
        finally
        {
            using var release = conn.CreateCommand();
            release.CommandText = "SELECT RELEASE_LOCK(@name);";
            release.Parameters.AddWithValue("@name", lockName);
            release.ExecuteScalar();
        }
    }
}