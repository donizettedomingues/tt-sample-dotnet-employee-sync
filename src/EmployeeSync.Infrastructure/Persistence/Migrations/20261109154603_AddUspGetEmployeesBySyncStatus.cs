using EmployeeSync.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeSync.Infrastructure.Persistence.Migrations
{
    /// <summary>Deploys the dbo.usp_GetEmployeesBySyncStatus report procedure (SQL Server only).</summary>
    public partial class AddUspGetEmployeesBySyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Wrapped in EXEC so it is also valid inside the IF blocks of an idempotent script
            // (CREATE PROCEDURE must otherwise start its own batch).
            var script = SqlScripts.Read("usp_GetEmployeesBySyncStatus.sql").Replace("'", "''");
            migrationBuilder.Sql($"EXEC(N'{script}');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_GetEmployeesBySyncStatus;");
        }
    }
}
