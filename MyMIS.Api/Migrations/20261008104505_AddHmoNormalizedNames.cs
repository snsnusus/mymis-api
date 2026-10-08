using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddHmoNormalizedNames : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_HmoProviders_Name",
          table: "HmoProviders");

      migrationBuilder.DropIndex(
          name: "IX_HmoPlans_HmoProviderId_Name",
          table: "HmoPlans");

      migrationBuilder.AddColumn<string>(
          name: "NormalizedName",
          table: "HmoProviders",
          type: "character varying(150)",
          maxLength: 150,
          nullable: false,
          defaultValue: "");

      migrationBuilder.AddColumn<string>(
          name: "NormalizedName",
          table: "HmoPlans",
          type: "character varying(150)",
          maxLength: 150,
          nullable: false,
          defaultValue: "");

      // Backfill from Name before the unique indexes are built; otherwise every
      // existing row holds the default "" and CreateIndex fails as a duplicate.
      migrationBuilder.Sql(@"UPDATE ""HmoProviders"" SET ""NormalizedName"" = UPPER(TRIM(""Name""));");
      migrationBuilder.Sql(@"UPDATE ""HmoPlans"" SET ""NormalizedName"" = UPPER(TRIM(""Name""));");


      migrationBuilder.CreateIndex(
          name: "IX_HmoProviders_NormalizedName",
          table: "HmoProviders",
          column: "NormalizedName",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_HmoPlans_HmoProviderId_NormalizedName",
          table: "HmoPlans",
          columns: new[] { "HmoProviderId", "NormalizedName" },
          unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_HmoProviders_NormalizedName",
          table: "HmoProviders");

      migrationBuilder.DropIndex(
          name: "IX_HmoPlans_HmoProviderId_NormalizedName",
          table: "HmoPlans");

      migrationBuilder.DropColumn(
          name: "NormalizedName",
          table: "HmoProviders");

      migrationBuilder.DropColumn(
          name: "NormalizedName",
          table: "HmoPlans");

      migrationBuilder.CreateIndex(
          name: "IX_HmoProviders_Name",
          table: "HmoProviders",
          column: "Name",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_HmoPlans_HmoProviderId_Name",
          table: "HmoPlans",
          columns: new[] { "HmoProviderId", "Name" },
          unique: true);
    }
  }
}
