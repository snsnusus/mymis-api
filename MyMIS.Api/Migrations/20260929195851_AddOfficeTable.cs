using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddOfficeTable : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "Offices",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            City = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
            Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Offices", x => x.Id);
          });

      migrationBuilder.CreateIndex(
          name: "IX_Offices_Name",
          table: "Offices",
          column: "Name",
          unique: true);

      migrationBuilder.Sql("""
          INSERT INTO "Permissions" ("Name")
          VALUES ('offices.manage')
          ON CONFLICT ("Name") DO NOTHING;
          """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "Offices");

      migrationBuilder.Sql("""
          DELETE FROM "Permissions"
          WHERE "Name" = 'offices.manage';
          """);
    }
  }
}
