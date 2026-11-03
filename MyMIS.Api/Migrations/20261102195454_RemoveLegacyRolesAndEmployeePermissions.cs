using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class RemoveLegacyRolesAndEmployeePermissions : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "EmployeePermissions");

      migrationBuilder.DropColumn(
          name: "Role",
          table: "Employees");

      // Legacy catalog rows: no controller or policy uses *.manage any more.
      // Placed AFTER DropTable("EmployeePermissions") so no old grant row can
      // block the delete through its foreign key. RolePermissions rows pointing
      // at them are removed by that table's cascade.
      migrationBuilder.Sql("""
          DELETE FROM "Permissions"
          WHERE "Name" IN ('hmo.manage', 'locations.manage', 'offices.manage');
          """);

      // A system role's permissions are COMPUTED by RolePermissionCache (every
      // row in Permissions), never stored (decision 1a). The rows Step 3 seeded
      // for SuperAdmin are leftovers that only mislead; remove them.
      migrationBuilder.Sql("""
          DELETE FROM "RolePermissions" rp
          USING "Roles" r
          WHERE rp."RoleId" = r."Id"
            AND r."IsSystem" = true;
          """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      // Bring back the legacy catalog rows. SuperAdmin's stored rows are not
      // restored: the cache computes a system role's permissions either way.
      migrationBuilder.Sql("""
          INSERT INTO "Permissions" ("Name")
          VALUES ('hmo.manage'), ('locations.manage'), ('offices.manage')
          ON CONFLICT ("Name") DO NOTHING;
          """);

      migrationBuilder.AddColumn<string>(
          name: "Role",
          table: "Employees",
          type: "text",
          nullable: false,
          defaultValue: "User");

      migrationBuilder.CreateTable(
          name: "EmployeePermissions",
          columns: table => new
          {
            EmployeeId = table.Column<int>(type: "integer", nullable: false),
            PermissionId = table.Column<int>(type: "integer", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_EmployeePermissions", x => new { x.EmployeeId, x.PermissionId });
            table.ForeignKey(
                      name: "FK_EmployeePermissions_Employees_EmployeeId",
                      column: x => x.EmployeeId,
                      principalTable: "Employees",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "FK_EmployeePermissions_Permissions_PermissionId",
                      column: x => x.PermissionId,
                      principalTable: "Permissions",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_EmployeePermissions_PermissionId",
          table: "EmployeePermissions",
          column: "PermissionId");
    }
  }
}
