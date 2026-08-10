using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DamageReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    DamageSigns = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DamageReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Earthquakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Magnitude = table.Column<double>(type: "double precision", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EpicenterLatitude = table.Column<double>(type: "double precision", nullable: false),
                    EpicenterLongitude = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Earthquakes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DamageReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MediaType = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportMedia_DamageReports_DamageReportId",
                        column: x => x.DamageReportId,
                        principalTable: "DamageReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Earthquakes",
                columns: new[] { "Id", "CreatedAt", "EpicenterLatitude", "EpicenterLongitude", "Magnitude", "Name", "OccurredAt", "Source" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4.5709, -74.297300000000007, 7.4000000000000004, "M7.4 - Colombia", new DateTimeOffset(new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.CreateIndex(
                name: "IX_DamageReports_Severity",
                table: "DamageReports",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ReportMedia_DamageReportId",
                table: "ReportMedia",
                column: "DamageReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Earthquakes");

            migrationBuilder.DropTable(
                name: "ReportMedia");

            migrationBuilder.DropTable(
                name: "DamageReports");
        }
    }
}
