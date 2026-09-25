using Inventory.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Persistence.Migrations
{
    /// <summary>Stored procedure cho thẻ kho (kardex, window function) và dashboard (RULES 3.7, 3.10e).</summary>
    public partial class AddReportStoredProcedures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScripts.Read(SqlScripts.ReportKardex));
            migrationBuilder.Sql(SqlScripts.Read(SqlScripts.ReportDashboard));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[usp_Report_Dashboard];");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[usp_Report_Kardex];");
        }
    }
}
