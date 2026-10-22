using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class BackfillSoftDeleteForPreviouslyDeletedEmployees : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // Employees soft-deleted before EmployeeService.DeleteAsync mirrored the delete
      // still have live child rows holding unique values (phones, emails, HMO member
      // numbers). Give those rows the employee's own DeletedAt, exactly what
      // DeleteAsync now does. Rows already deleted on their own keep their date.

      // Tables with an EmployeeId column and both timestamps.
      string[] childTables =
      [
        "EmployeeAddresses",
        "EmployeeEmails",
        "EmployeePhones",
        "EmergencyContacts",
        "EmployeeHmos",
      ];

      foreach (var table in childTables)
      {
        migrationBuilder.Sql($@"
          UPDATE ""{table}"" AS c
          SET ""DeletedAt"" = e.""DeletedAt"",
              ""UpdatedAt"" = e.""DeletedAt""
          FROM ""Employees"" AS e
          WHERE c.""EmployeeId"" = e.""Id""
            AND e.""DeletedAt"" IS NOT NULL
            AND c.""DeletedAt"" IS NULL;");
      }

      // Government IDs: DeletedAt only. UpdatedAt is paired with UpdatedById
      // ("who last verified or changed it"), same rule as DeleteAsync.
      migrationBuilder.Sql(@"
        UPDATE ""EmployeeGovernmentIds"" AS g
        SET ""DeletedAt"" = e.""DeletedAt""
        FROM ""Employees"" AS e
        WHERE g.""EmployeeId"" = e.""Id""
          AND e.""DeletedAt"" IS NOT NULL
          AND g.""DeletedAt"" IS NULL;");

      // HMO dependents have no EmployeeId; reach the employee through the enrollment.
      migrationBuilder.Sql(@"
        UPDATE ""EmployeeHmoDependents"" AS d
        SET ""DeletedAt"" = e.""DeletedAt"",
            ""UpdatedAt"" = e.""DeletedAt""
        FROM ""EmployeeHmos"" AS h
        JOIN ""Employees"" AS e ON e.""Id"" = h.""EmployeeId""
        WHERE d.""EmployeeHmoId"" = h.""Id""
          AND e.""DeletedAt"" IS NOT NULL
          AND d.""DeletedAt"" IS NULL;");

      // Revoke refresh tokens still active for deleted employees.
      migrationBuilder.Sql(@"
        UPDATE ""RefreshTokens"" AS rt
        SET ""RevokedAt"" = e.""DeletedAt""
        FROM ""Employees"" AS e
        WHERE rt.""EmployeeId"" = e.""Id""
          AND e.""DeletedAt"" IS NOT NULL
          AND rt.""RevokedAt"" IS NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      // Intentionally empty: a data fix can't be reliably undone. After Up(), rows
      // this migration changed look exactly like rows DeleteAsync changed, so there
      // is no way to tell which ones to restore.
    }
  }
}