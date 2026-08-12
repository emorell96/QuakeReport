using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace QuakeReport.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPostGisLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Shelters",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "MissingPersonTips",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "MissingPersonLocations",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "HelpRequests",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Earthquakes",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "DamageReports",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "CollectionPoints",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "BloodDonationCenters",
                type: "geography (point, 4326)",
                nullable: true);

            foreach (var table in new[] { "Shelters", "MissingPersonTips", "MissingPersonLocations", "HelpRequests", "DamageReports", "CollectionPoints", "BloodDonationCenters" })
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}"
                    SET "Location" = ST_SetSRID(ST_MakePoint("Longitude", "Latitude"), 4326)::geography
                    WHERE "Latitude" IS NOT NULL AND "Longitude" IS NOT NULL;
                    """);
            }
            migrationBuilder.Sql("""
                UPDATE "Earthquakes"
                SET "Location" = ST_SetSRID(ST_MakePoint("EpicenterLongitude", "EpicenterLatitude"), 4326)::geography;
                """);

            migrationBuilder.AlterColumn<Point>(name: "Location", table: "Earthquakes", type: "geography (point, 4326)", nullable: false, oldClrType: typeof(Point), oldType: "geography (point, 4326)", oldNullable: true);
            migrationBuilder.AlterColumn<Point>(name: "Location", table: "DamageReports", type: "geography (point, 4326)", nullable: false, oldClrType: typeof(Point), oldType: "geography (point, 4326)", oldNullable: true);

            foreach (var table in new[] { "Shelters", "MissingPersonTips", "MissingPersonLocations", "HelpRequests", "DamageReports", "CollectionPoints", "BloodDonationCenters" })
            {
                migrationBuilder.DropColumn(name: "Latitude", table: table);
                migrationBuilder.DropColumn(name: "Longitude", table: table);
            }
            migrationBuilder.DropColumn(name: "EpicenterLatitude", table: "Earthquakes");
            migrationBuilder.DropColumn(name: "EpicenterLongitude", table: "Earthquakes");

            migrationBuilder.CreateTable(
                name: "GeocodingReviewItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AddressHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CandidateLocation = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    FormattedAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GooglePlaceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Granularity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeocodingReviewItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shelters_Location",
                table: "Shelters",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_MissingPersonTips_Location",
                table: "MissingPersonTips",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_MissingPersonLocations_Location",
                table: "MissingPersonLocations",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_HelpRequests_Location",
                table: "HelpRequests",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Earthquakes_Location",
                table: "Earthquakes",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_DamageReports_Location",
                table: "DamageReports",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_Location",
                table: "CollectionPoints",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonationCenters_Location",
                table: "BloodDonationCenters",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_GeocodingReviewItems_EntityType_EntityId_AddressHash",
                table: "GeocodingReviewItems",
                columns: new[] { "EntityType", "EntityId", "AddressHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeocodingReviewItems_Status_LastAttemptAt",
                table: "GeocodingReviewItems",
                columns: new[] { "Status", "LastAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeocodingReviewItems");

            var conventionalTables = new[]
            {
                "Shelters", "MissingPersonTips", "MissingPersonLocations", "HelpRequests",
                "DamageReports", "CollectionPoints", "BloodDonationCenters",
            };

            foreach (var table in conventionalTables)
            {
                migrationBuilder.AddColumn<double>(
                    name: "Latitude", table: table, type: "double precision", nullable: true);
                migrationBuilder.AddColumn<double>(
                    name: "Longitude", table: table, type: "double precision", nullable: true);
                migrationBuilder.Sql($$"""
                    UPDATE "{{table}}"
                    SET "Latitude" = ST_Y("Location"::geometry),
                        "Longitude" = ST_X("Location"::geometry)
                    WHERE "Location" IS NOT NULL;
                    """);
            }

            migrationBuilder.AddColumn<double>(
                name: "EpicenterLatitude", table: "Earthquakes", type: "double precision", nullable: true);
            migrationBuilder.AddColumn<double>(
                name: "EpicenterLongitude", table: "Earthquakes", type: "double precision", nullable: true);
            migrationBuilder.Sql("""
                UPDATE "Earthquakes"
                SET "EpicenterLatitude" = ST_Y("Location"::geometry),
                    "EpicenterLongitude" = ST_X("Location"::geometry);
                """);

            migrationBuilder.AlterColumn<double>(
                name: "Latitude", table: "DamageReports", type: "double precision", nullable: false,
                oldClrType: typeof(double), oldType: "double precision", oldNullable: true);
            migrationBuilder.AlterColumn<double>(
                name: "Longitude", table: "DamageReports", type: "double precision", nullable: false,
                oldClrType: typeof(double), oldType: "double precision", oldNullable: true);
            migrationBuilder.AlterColumn<double>(
                name: "EpicenterLatitude", table: "Earthquakes", type: "double precision", nullable: false,
                oldClrType: typeof(double), oldType: "double precision", oldNullable: true);
            migrationBuilder.AlterColumn<double>(
                name: "EpicenterLongitude", table: "Earthquakes", type: "double precision", nullable: false,
                oldClrType: typeof(double), oldType: "double precision", oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_Shelters_Location",
                table: "Shelters");

            migrationBuilder.DropIndex(
                name: "IX_MissingPersonTips_Location",
                table: "MissingPersonTips");

            migrationBuilder.DropIndex(
                name: "IX_MissingPersonLocations_Location",
                table: "MissingPersonLocations");

            migrationBuilder.DropIndex(
                name: "IX_HelpRequests_Location",
                table: "HelpRequests");

            migrationBuilder.DropIndex(
                name: "IX_Earthquakes_Location",
                table: "Earthquakes");

            migrationBuilder.DropIndex(
                name: "IX_DamageReports_Location",
                table: "DamageReports");

            migrationBuilder.DropIndex(
                name: "IX_CollectionPoints_Location",
                table: "CollectionPoints");

            migrationBuilder.DropIndex(
                name: "IX_BloodDonationCenters_Location",
                table: "BloodDonationCenters");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Shelters");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "MissingPersonTips");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "MissingPersonLocations");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "HelpRequests");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Earthquakes");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "DamageReports");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "CollectionPoints");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "BloodDonationCenters");
        }
    }
}
