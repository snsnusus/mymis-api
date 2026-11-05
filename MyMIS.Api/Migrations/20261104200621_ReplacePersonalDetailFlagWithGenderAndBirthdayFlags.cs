using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class ReplacePersonalDetailFlagWithGenderAndBirthdayFlags : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "ShowInProfile",
          table: "EmployeePersonalDetails");

      migrationBuilder.AddColumn<bool>(
          name: "ShowBirthdayInProfile",
          table: "Employees",
          type: "boolean",
          nullable: false,
          defaultValue: false);

      migrationBuilder.AddColumn<bool>(
          name: "ShowGenderInProfile",
          table: "Employees",
          type: "boolean",
          nullable: false,
          defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "ShowBirthdayInProfile",
          table: "Employees");

      migrationBuilder.DropColumn(
          name: "ShowGenderInProfile",
          table: "Employees");

      migrationBuilder.AddColumn<bool>(
          name: "ShowInProfile",
          table: "EmployeePersonalDetails",
          type: "boolean",
          nullable: false,
          defaultValue: true);
    }
  }
}
