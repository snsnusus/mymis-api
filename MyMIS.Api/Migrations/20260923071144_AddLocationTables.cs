using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddLocationTables : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "Regions",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            PsgcCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Regions", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "Cities",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            PsgcCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
            RegionId = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Cities", x => x.Id);
            table.ForeignKey(
                      name: "FK_Cities_Regions_RegionId",
                      column: x => x.RegionId,
                      principalTable: "Regions",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Barangays",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            PsgcCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
            ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
            CityId = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Barangays", x => x.Id);
            table.ForeignKey(
                      name: "FK_Barangays_Cities_CityId",
                      column: x => x.CityId,
                      principalTable: "Cities",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateIndex(
          name: "IX_Barangays_CityId_Name",
          table: "Barangays",
          columns: ["CityId", "Name"],
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_Cities_RegionId_Name",
          table: "Cities",
          columns: ["RegionId", "Name"],
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "Barangays");

      migrationBuilder.DropTable(
          name: "Cities");

      migrationBuilder.DropTable(
          name: "Regions");
    }
  }
}
