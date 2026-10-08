using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddOfficeNormalizedName : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_Offices_Name",
          table: "Offices");

      migrationBuilder.AddColumn<string>(
          name: "NormalizedName",
          table: "Offices",
          type: "character varying(150)",
          maxLength: 150,
          nullable: false,
          defaultValue: "");

      // Backfill from Name before the unique index is built; otherwise every
      // existing row holds the default "" and CreateIndex fails as a duplicate.
      migrationBuilder.Sql(@"UPDATE ""Offices"" SET ""NormalizedName"" = UPPER(TRIM(""Name""));");

      migrationBuilder.CreateIndex(
          name: "IX_Offices_NormalizedName",
          table: "Offices",
          column: "NormalizedName",
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_Offices_NormalizedName",
          table: "Offices");

      migrationBuilder.DropColumn(
          name: "NormalizedName",
          table: "Offices");

      migrationBuilder.CreateIndex(
          name: "IX_Offices_Name",
          table: "Offices",
          column: "Name",
          unique: true);
    }
  }
}
