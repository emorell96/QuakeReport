using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EarthquakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequesterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SearchText = table.Column<string>(type: "character varying(1800)", maxLength: 1800, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    NeedDetails = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PublicPhone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PublicWhatsApp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PublicEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    NeedCategories = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModerationStatus = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    NeededBy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ManagementCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModeratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModeratedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpRequests_Earthquakes_EarthquakeId",
                        column: x => x.EarthquakeId,
                        principalTable: "Earthquakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpRequestAbuseReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HelpRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpRequestAbuseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpRequestAbuseReports_HelpRequests_HelpRequestId",
                        column: x => x.HelpRequestId,
                        principalTable: "HelpRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HelpRequestComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HelpRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpRequestComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpRequestComments_HelpRequests_HelpRequestId",
                        column: x => x.HelpRequestId,
                        principalTable: "HelpRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpRequestAbuseReports_HelpRequestId_CreatedAt",
                table: "HelpRequestAbuseReports",
                columns: new[] { "HelpRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpRequestComments_HelpRequestId_CreatedAt",
                table: "HelpRequestComments",
                columns: new[] { "HelpRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpRequests_EarthquakeId_ModerationStatus_Status_Priority_~",
                table: "HelpRequests",
                columns: new[] { "EarthquakeId", "ModerationStatus", "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpRequests_EarthquakeId_Status_UpdatedAt",
                table: "HelpRequests",
                columns: new[] { "EarthquakeId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpRequests_ManagementCodeHash",
                table: "HelpRequests",
                column: "ManagementCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpRequests_SearchText",
                table: "HelpRequests",
                column: "SearchText");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpRequestAbuseReports");

            migrationBuilder.DropTable(
                name: "HelpRequestComments");

            migrationBuilder.DropTable(
                name: "HelpRequests");
        }
    }
}
