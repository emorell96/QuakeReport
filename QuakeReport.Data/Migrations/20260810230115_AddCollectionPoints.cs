using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionPoints",
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
                    Description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    NeedsSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReceivingInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ContactWhatsApp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_CollectionPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPoints_Earthquakes_EarthquakeId",
                        column: x => x.EarthquakeId,
                        principalTable: "Earthquakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPointAbuseReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionPointId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPointAbuseReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPointAbuseReports_CollectionPoints_CollectionPoin~",
                        column: x => x.CollectionPointId,
                        principalTable: "CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPointComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionPointId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPointComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPointComments_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalTable: "CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointAbuseReports_CollectionPointId_CreatedAt",
                table: "CollectionPointAbuseReports",
                columns: new[] { "CollectionPointId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointComments_CollectionPointId_CreatedAt",
                table: "CollectionPointComments",
                columns: new[] { "CollectionPointId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_EarthquakeId_ModerationStatus_OperationalS~",
                table: "CollectionPoints",
                columns: new[] { "EarthquakeId", "ModerationStatus", "OperationalStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_EarthquakeId_OperationalStatus_UpdatedAt",
                table: "CollectionPoints",
                columns: new[] { "EarthquakeId", "OperationalStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_ManagementCodeHash",
                table: "CollectionPoints",
                column: "ManagementCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_SearchText",
                table: "CollectionPoints",
                column: "SearchText");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionPointAbuseReports");

            migrationBuilder.DropTable(
                name: "CollectionPointComments");

            migrationBuilder.DropTable(
                name: "CollectionPoints");
        }
    }
}
