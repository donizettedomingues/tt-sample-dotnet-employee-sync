namespace EmployeeSync.Infrastructure.Persistence.Sql;

/// <summary>Reads T-SQL scripts embedded from Persistence/Sql so migrations and the repo share one copy.</summary>
internal static class SqlScripts
{
    public static string Read(string fileName)
    {
        var name = $"EmployeeSync.Sql.{fileName}";
        using var stream = typeof(SqlScripts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded SQL script '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
