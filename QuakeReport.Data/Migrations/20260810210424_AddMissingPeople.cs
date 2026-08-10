using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingPeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissingPeople",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EarthquakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SearchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Aliases = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApproximateAge = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IdentificationDocumentType = table.Column<int>(type: "integer", nullable: true),
                    IdentificationNumberHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IdentificationLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PhysicalDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClothingDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PhotoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ManagementCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicationConsentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingPeople", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissingPeople_Earthquakes_EarthquakeId",
                        column: x => x.EarthquakeId,
                        principalTable: "Earthquakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AbuseReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissingPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbuseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbuseReports_MissingPeople_MissingPersonId",
                        column: x => x.MissingPersonId,
                        principalTable: "MissingPeople",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissingPersonLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissingPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SearchAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingPersonLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissingPersonLocations_MissingPeople_MissingPersonId",
                        column: x => x.MissingPersonId,
                        principalTable: "MissingPeople",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissingPersonTips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissingPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SightedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    ResponderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResponderPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResponderEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingPersonTips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissingPersonTips_MissingPeople_MissingPersonId",
                        column: x => x.MissingPersonId,
                        principalTable: "MissingPeople",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbuseReports_MissingPersonId_CreatedAt",
                table: "AbuseReports",
                columns: new[] { "MissingPersonId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissingPeople_EarthquakeId_IdentificationNumberHash",
                table: "MissingPeople",
                columns: new[] { "EarthquakeId", "IdentificationNumberHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissingPeople_EarthquakeId_Status_CreatedAt",
                table: "MissingPeople",
                columns: new[] { "EarthquakeId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissingPeople_SearchName",
                table: "MissingPeople",
                column: "SearchName");

            migrationBuilder.CreateIndex(
                name: "IX_MissingPersonLocations_MissingPersonId",
                table: "MissingPersonLocations",
                column: "MissingPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_MissingPersonLocations_SearchAddress",
                table: "MissingPersonLocations",
                column: "SearchAddress");

            migrationBuilder.CreateIndex(
                name: "IX_MissingPersonTips_MissingPersonId_CreatedAt",
                table: "MissingPersonTips",
                columns: new[] { "MissingPersonId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbuseReports");

            migrationBuilder.DropTable(
                name: "MissingPersonLocations");

            migrationBuilder.DropTable(
                name: "MissingPersonTips");

            migrationBuilder.DropTable(
                name: "MissingPeople");
        }
    }
}
