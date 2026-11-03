using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class UpdatePermissionCatalog : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // 1. Rename instead of delete + insert: the row keeps its Id, so every
      //    RolePermissions grant pointing at it carries over automatically.
      migrationBuilder.Sql("""
          UPDATE "Permissions"
          SET "Name" = 'employees.work-details.update'
          WHERE "Name" = 'employees.update.work';
          """);

      // 2. New HMO admin permissions (decision 5 / catalog section 4).
      migrationBuilder.Sql("""
          INSERT INTO "Permissions" ("Name")
          VALUES
              ('hmo-providers.read'), ('hmo-providers.create'), ('hmo-providers.update'), ('hmo-providers.delete'),
              ('hmo-plans.read'), ('hmo-plans.create'), ('hmo-plans.update'), ('hmo-plans.delete')
          ON CONFLICT ("Name") DO NOTHING;
          """);

      // 3. employees.read is now HR-level: take it off the Employee role.
      //    DELETE ... USING joins other tables into a DELETE, so we can match
      //    by role and permission NAME instead of hard-coded ids.
      migrationBuilder.Sql("""
          DELETE FROM "RolePermissions" rp
          USING "Roles" r, "Permissions" p
          WHERE rp."RoleId" = r."Id"
            AND rp."PermissionId" = p."Id"
            AND r."NormalizedName" = 'EMPLOYEE'
            AND p."Name" = 'employees.read';
          """);

      // 4. Permissions that no longer exist. RolePermissions rows pointing at
      //    them are removed by the cascade (FK_RolePermissions_Permissions_PermissionId),
      //    which also takes hmo.read off the Employee role.
      //    The legacy *.manage rows stay until 6d: controllers still use them.
      migrationBuilder.Sql("""
          DELETE FROM "Permissions"
          WHERE "Name" IN (
              'employees.private.read',
              'hmo.read', 'hmo.create', 'hmo.update', 'hmo.delete'
          );
          """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      // Re-create the removed permissions.
      migrationBuilder.Sql("""
          INSERT INTO "Permissions" ("Name")
          VALUES ('employees.private.read'), ('hmo.read'), ('hmo.create'), ('hmo.update'), ('hmo.delete')
          ON CONFLICT ("Name") DO NOTHING;
          """);

      // Give the Employee role back employees.read and hmo.read, as Step 3 seeded them.
      migrationBuilder.Sql("""
          INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "Scope")
          SELECT r."Id", p."Id", 'Company'
          FROM "Roles" r
          CROSS JOIN "Permissions" p
          WHERE r."NormalizedName" = 'EMPLOYEE'
            AND p."Name" IN ('employees.read', 'hmo.read')
          ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
          """);

      // Remove the HMO admin permissions (their grants cascade away).
      migrationBuilder.Sql("""
          DELETE FROM "Permissions"
          WHERE "Name" LIKE 'hmo-providers.%' OR "Name" LIKE 'hmo-plans.%';
          """);

      // Rename back.
      migrationBuilder.Sql("""
          UPDATE "Permissions"
          SET "Name" = 'employees.update.work'
          WHERE "Name" = 'employees.work-details.update';
          """);
    }
  }
}
