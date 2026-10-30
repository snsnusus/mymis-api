using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddRolesAndRolePermissions : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<int>(
          name: "RoleId",
          table: "Employees",
          type: "integer",
          nullable: false,
          defaultValue: 0);

      migrationBuilder.AddColumn<bool>(
          name: "ShowInProfile",
          table: "EmployeePhones",
          type: "boolean",
          nullable: false,
          defaultValue: true);

      migrationBuilder.AddColumn<bool>(
          name: "ShowInProfile",
          table: "EmployeePersonalDetails",
          type: "boolean",
          nullable: false,
          defaultValue: true);

      migrationBuilder.AddColumn<bool>(
          name: "ShowInProfile",
          table: "EmployeeEmails",
          type: "boolean",
          nullable: false,
          defaultValue: true);

      migrationBuilder.AddColumn<bool>(
          name: "ShowInProfile",
          table: "EmployeeAddresses",
          type: "boolean",
          nullable: false,
          defaultValue: true);

      migrationBuilder.CreateTable(
          name: "Roles",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            IsSystem = table.Column<bool>(type: "boolean", nullable: false),
            Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            NormalizedName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Roles", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "RolePermissions",
          columns: table => new
          {
            RoleId = table.Column<int>(type: "integer", nullable: false),
            PermissionId = table.Column<int>(type: "integer", nullable: false),
            Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
            table.ForeignKey(
                      name: "FK_RolePermissions_Permissions_PermissionId",
                      column: x => x.PermissionId,
                      principalTable: "Permissions",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "FK_RolePermissions_Roles_RoleId",
                      column: x => x.RoleId,
                      principalTable: "Roles",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_Employees_RoleId",
          table: "Employees",
          column: "RoleId");

      migrationBuilder.CreateIndex(
          name: "IX_RolePermissions_PermissionId",
          table: "RolePermissions",
          column: "PermissionId");

      migrationBuilder.CreateIndex(
          name: "IX_Roles_NormalizedName",
          table: "Roles",
          column: "NormalizedName",
          unique: true);

      // ── Seed: permissions, system roles, role permissions, existing employees ──
      // Runs BEFORE FK_Employees_Roles_RoleId is added, so every employee
      // has a real RoleId by the time the foreign key is enforced.

      // 1. Every permission in the new catalog (PermissionNames.All).
      //    ON CONFLICT skips rows that already exist (e.g. employees.create),
      //    using the unique index on "Name". Safe locally and on Supabase.
      migrationBuilder.Sql("""
          INSERT INTO "Permissions" ("Name")
          VALUES
              ('employees.read'), ('employees.create'), ('employees.update'),
              ('employees.delete'), ('employees.private.read'), ('employees.update.work'),
              ('departments.read'), ('departments.create'), ('departments.update'), ('departments.delete'),
              ('positions.read'), ('positions.create'), ('positions.update'), ('positions.delete'),
              ('offices.read'), ('offices.create'), ('offices.update'), ('offices.delete'),
              ('locations.read'), ('locations.create'), ('locations.update'), ('locations.delete'),
              ('hmo.read'), ('hmo.create'), ('hmo.update'), ('hmo.delete'),
              ('roles.read'), ('roles.create'), ('roles.update'), ('roles.delete'), ('roles.assign')
          ON CONFLICT ("Name") DO NOTHING;
          """);

      // 2. The two system-seeded roles. now() = current timestamp in Postgres.
      migrationBuilder.Sql("""
          INSERT INTO "Roles" ("Name", "NormalizedName", "Description", "IsSystem", "CreatedAt", "UpdatedAt")
          VALUES
              ('SuperAdmin', 'SUPERADMIN', 'Full system access. Managed by the system.', true,  now(), now()),
              ('Employee',   'EMPLOYEE',   'Default role for every employee.',           false, now(), now())
          ON CONFLICT ("NormalizedName") DO NOTHING;
          """);

      // 3. SuperAdmin gets EVERY permission row, company-wide.
      //    CROSS JOIN pairs the one SuperAdmin row with every permission row:
      //    1 role × N permissions = N rows. Like roles.flatMap(r => permissions.map(p => [r, p])).
      migrationBuilder.Sql("""
          INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "Scope")
          SELECT r."Id", p."Id", 'Company'
          FROM "Roles" r
          CROSS JOIN "Permissions" p
          WHERE r."NormalizedName" = 'SUPERADMIN'
          ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
          """);

      // 4. Employee gets every *.read permission, company-wide,
      //    EXCEPT private details and role management (design doc §4).
      //    LIKE '%.read' = "ends with .read"; % is a wildcard, like .* in a regex.
      migrationBuilder.Sql("""
          INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "Scope")
          SELECT r."Id", p."Id", 'Company'
          FROM "Roles" r
          CROSS JOIN "Permissions" p
          WHERE r."NormalizedName" = 'EMPLOYEE'
            AND p."Name" LIKE '%.read'
            AND p."Name" NOT IN ('employees.private.read', 'roles.read')
          ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
          """);

      // 5. Give every existing employee a role, based on the OLD enum column "Role".
      //    Raw SQL ignores EF's soft-delete query filter, so soft-deleted employees
      //    are updated too, which the foreign key requires.
      //    Admin/User → Employee for now; reassign real roles once the Roles API exists.
      migrationBuilder.Sql("""
          UPDATE "Employees"
          SET "RoleId" = (SELECT "Id" FROM "Roles" WHERE "NormalizedName" = 'SUPERADMIN')
          WHERE "Role" = 'SuperAdmin';

          UPDATE "Employees"
          SET "RoleId" = (SELECT "Id" FROM "Roles" WHERE "NormalizedName" = 'EMPLOYEE')
          WHERE "Role" <> 'SuperAdmin';
          """);

      migrationBuilder.AddForeignKey(
          name: "FK_Employees_Roles_RoleId",
          table: "Employees",
          column: "RoleId",
          principalTable: "Roles",
          principalColumn: "Id",
          onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.Sql("""
          DELETE FROM "Permissions"
          WHERE "Name" IN (
              'employees.read', 'employees.delete', 'employees.private.read', 'employees.update.work',
              'departments.read', 'departments.create', 'departments.update', 'departments.delete',
              'positions.read', 'positions.delete',
              'offices.read', 'offices.create', 'offices.update', 'offices.delete',
              'locations.read', 'locations.create', 'locations.update', 'locations.delete',
              'hmo.read', 'hmo.create', 'hmo.update', 'hmo.delete',
              'roles.read', 'roles.create', 'roles.update', 'roles.delete', 'roles.assign'
          );
          """);

      migrationBuilder.DropForeignKey(
          name: "FK_Employees_Roles_RoleId",
          table: "Employees");

      migrationBuilder.DropTable(
          name: "RolePermissions");

      migrationBuilder.DropTable(
          name: "Roles");

      migrationBuilder.DropIndex(
          name: "IX_Employees_RoleId",
          table: "Employees");

      migrationBuilder.DropColumn(
          name: "RoleId",
          table: "Employees");

      migrationBuilder.DropColumn(
          name: "ShowInProfile",
          table: "EmployeePhones");

      migrationBuilder.DropColumn(
          name: "ShowInProfile",
          table: "EmployeePersonalDetails");

      migrationBuilder.DropColumn(
          name: "ShowInProfile",
          table: "EmployeeEmails");

      migrationBuilder.DropColumn(
          name: "ShowInProfile",
          table: "EmployeeAddresses");
    }
  }
}
