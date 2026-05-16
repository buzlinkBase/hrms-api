using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class changetopolygon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Coordinates",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Coordinates",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "Coordinates",
                table: "Areas");

            migrationBuilder.AddColumn<Polygon>(
                name: "Boundary",
                table: "Branches",
                type: "geometry",
                nullable: true)
                .Annotation("MySql:SpatialReferenceSystemId", 4326);

            migrationBuilder.AddColumn<Polygon>(
                name: "Boundary",
                table: "Attendances",
                type: "geometry",
                nullable: true)
                .Annotation("MySql:SpatialReferenceSystemId", 4326);

            migrationBuilder.AddColumn<Polygon>(
                name: "Boundary",
                table: "Areas",
                type: "geometry",
                nullable: true)
                .Annotation("MySql:SpatialReferenceSystemId", 4326);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "Areas");

            migrationBuilder.AddColumn<Point>(
                name: "Coordinates",
                table: "Branches",
                type: "point",
                nullable: true)
                .Annotation("MySql:SpatialReferenceSystemId", 4326);

            migrationBuilder.AddColumn<Point>(
                name: "Coordinates",
                table: "Attendances",
                type: "point",
                nullable: true)
                .Annotation("MySql:SpatialReferenceSystemId", 4326);

            migrationBuilder.AddColumn<Point>(
                name: "Coordinates",
                table: "Areas",
                type: "point",
                nullable: true)
                .Annotation("MySql:SpatialReferenceSystemId", 4326);
        }
    }
}
