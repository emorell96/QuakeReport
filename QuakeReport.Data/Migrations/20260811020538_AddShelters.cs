using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShelters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shelters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EarthquakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SearchText = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    OperatingInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ContactWhatsApp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ModerationStatus = table.Column<int>(type: "integer", nullable: false),
                    OperationalStatus = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ManagementCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModeratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModeratedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shelters_Earthquakes_EarthquakeId",
                        column: x => x.EarthquakeId,
                        principalTable: "Earthquakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShelterAbuseReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelterAbuseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShelterAbuseReports_Shelters_ShelterId",
                        column: x => x.ShelterId,
                        principalTable: "Shelters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShelterAbuseReports_ShelterId_CreatedAt",
                table: "ShelterAbuseReports",
                columns: new[] { "ShelterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_EarthquakeId_ModerationStatus_OperationalStatus_Cr~",
                table: "Shelters",
                columns: new[] { "EarthquakeId", "ModerationStatus", "OperationalStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_EarthquakeId_OperationalStatus_UpdatedAt",
                table: "Shelters",
                columns: new[] { "EarthquakeId", "OperationalStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_ManagementCodeHash",
                table: "Shelters",
                column: "ManagementCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_SearchText",
                table: "Shelters",
                column: "SearchText");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShelterAbuseReports");

            migrationBuilder.DropTable(
                name: "Shelters");
        }
    }
}
