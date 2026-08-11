using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBloodDonationCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BloodDonationCenters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EarthquakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SearchText = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    OperatingInstructions = table.Column<string>(type: "character varying(2500)", maxLength: 2500, nullable: false),
                    NeedsSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PublicPhone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PublicWhatsApp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PublicEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CenterType = table.Column<int>(type: "integer", nullable: false),
                    BloodTypes = table.Column<int>(type: "integer", nullable: false),
                    Components = table.Column<int>(type: "integer", nullable: false),
                    OperationalStatus = table.Column<int>(type: "integer", nullable: false),
                    ModerationStatus = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ManagementCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModeratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModeratedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodDonationCenters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodDonationCenters_Earthquakes_EarthquakeId",
                        column: x => x.EarthquakeId,
                        principalTable: "Earthquakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodDonationCenterAbuseReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodDonationCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodDonationCenterAbuseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodDonationCenterAbuseReports_BloodDonationCenters_BloodD~",
                        column: x => x.BloodDonationCenterId,
                        principalTable: "BloodDonationCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BloodDonationCenterComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodDonationCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodDonationCenterComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodDonationCenterComments_BloodDonationCenters_BloodDonat~",
                        column: x => x.BloodDonationCenterId,
                        principalTable: "BloodDonationCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenterAbuseReports_BloodDonationCenterId_Creat~",
                table: "BloodDonationCenterAbuseReports",
                columns: new[] { "BloodDonationCenterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenterComments_BloodDonationCenterId_CreatedAt",
                table: "BloodDonationCenterComments",
                columns: new[] { "BloodDonationCenterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenters_EarthquakeId_BloodTypes_Components",
                table: "BloodDonationCenters",
                columns: new[] { "EarthquakeId", "BloodTypes", "Components" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenters_EarthquakeId_ModerationStatus_Operatio~",
                table: "BloodDonationCenters",
                columns: new[] { "EarthquakeId", "ModerationStatus", "OperationalStatus", "CenterType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenters_EarthquakeId_OperationalStatus_Updated~",
                table: "BloodDonationCenters",
                columns: new[] { "EarthquakeId", "OperationalStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenters_ManagementCodeHash",
                table: "BloodDonationCenters",
                column: "ManagementCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenters_SearchText",
                table: "BloodDonationCenters",
                column: "SearchText");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloodDonationCenterAbuseReports");

            migrationBuilder.DropTable(
                name: "BloodDonationCenterComments");

            migrationBuilder.DropTable(
                name: "BloodDonationCenters");
        }
    }
}
