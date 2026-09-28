using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddRemainingPermissions : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // Idempotent: rows that already exist (matched by the unique
      // "Name" index) are skipped, so this is safe in every environment.
      migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Name")
                VALUES
                    ('employees.create'),
                    ('employees.update'),
                    ('positions.create'),
                    ('positions.update'),
                    ('locations.manage')
                ON CONFLICT ("Name") DO NOTHING;
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      // Only remove what this migration introduced. The two employee
      // permissions existed before it, so a rollback leaves them alone.
      migrationBuilder.Sql("""
                DELETE FROM "Permissions"
                WHERE "Name" IN ('positions.create', 'positions.update', 'locations.manage');
                """);
    }
  }
}