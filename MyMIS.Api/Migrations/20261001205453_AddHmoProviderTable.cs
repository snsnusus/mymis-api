using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddHmoProviderTable : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "HmoProviders",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            AccountManagerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
            Hotline = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
            SupportEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
            WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            ContractStartDate = table.Column<DateOnly>(type: "date", nullable: false),
            ContractEndDate = table.Column<DateOnly>(type: "date", nullable: false),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_HmoProviders", x => x.Id);
          });

      migrationBuilder.CreateIndex(
          name: "IX_HmoProviders_Code",
          table: "HmoProviders",
          column: "Code",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_HmoProviders_Name",
          table: "HmoProviders",
          column: "Name",
          unique: true);

      migrationBuilder.Sql("""
          INSERT INTO "Permissions" ("Name")
          VALUES ('hmo.manage')
          ON CONFLICT ("Name") DO NOTHING;
          """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.Sql("""
          DELETE FROM "Permissions" WHERE "Name" = 'hmo.manage';
          """);

      migrationBuilder.DropTable(
          name: "HmoProviders");
    }
  }
}
