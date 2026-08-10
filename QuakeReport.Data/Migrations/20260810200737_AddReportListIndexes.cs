using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DamageReports_Severity",
                table: "DamageReports");

            migrationBuilder.CreateIndex(
                name: "IX_DamageReports_CreatedAt_Id",
                table: "DamageReports",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DamageReports_Severity_CreatedAt_Id",
                table: "DamageReports",
                columns: new[] { "Severity", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DamageReports_CreatedAt_Id",
                table: "DamageReports");

            migrationBuilder.DropIndex(
                name: "IX_DamageReports_Severity_CreatedAt_Id",
                table: "DamageReports");

            migrationBuilder.CreateIndex(
                name: "IX_DamageReports_Severity",
                table: "DamageReports",
                column: "Severity");
        }
    }
}
