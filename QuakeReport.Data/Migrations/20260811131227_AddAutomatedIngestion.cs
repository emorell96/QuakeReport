using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomatedIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngestionSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EarthquakeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExternalPostId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExtractedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    EvidenceSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionSubmissions_Earthquakes_EarthquakeId",
                        column: x => x.EarthquakeId,
                        principalTable: "Earthquakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionSubmissions_EarthquakeId_CreatedAt",
                table: "IngestionSubmissions",
                columns: new[] { "EarthquakeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionSubmissions_EntityType_IdempotencyKeyHash",
                table: "IngestionSubmissions",
                columns: new[] { "EntityType", "IdempotencyKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestionSubmissions_EntityType_Platform_ExternalPostId",
                table: "IngestionSubmissions",
                columns: new[] { "EntityType", "Platform", "ExternalPostId" },
                unique: true,
                filter: "\"ExternalPostId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngestionSubmissions");
        }
    }
}
