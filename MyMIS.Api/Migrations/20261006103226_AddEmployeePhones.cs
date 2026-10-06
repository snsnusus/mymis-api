using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMIS.Api.Migrations
{
  /// <inheritdoc />
  public partial class AddEmployeePhones : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "EmployeePhones",
          columns: table => new
          {
            Id = table.Column<int>(type: "integer", nullable: false)
                  .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            EmployeeId = table.Column<int>(type: "integer", nullable: false),
            Ownership = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            LineType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Phone_CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
            Phone_Number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_EmployeePhones", x => x.Id);
            table.ForeignKey(
                      name: "FK_EmployeePhones_Employees_EmployeeId",
                      column: x => x.EmployeeId,
                      principalTable: "Employees",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_EmployeePhones_EmployeeId",
          table: "EmployeePhones",
          column: "EmployeeId");

      migrationBuilder.CreateIndex(
          name: "IX_EmployeePhones_EmployeeId_Primary",
          table: "EmployeePhones",
          column: "EmployeeId",
          unique: true,
          filter: "\"IsPrimary\" = true AND \"DeletedAt\" IS NULL");

      migrationBuilder.CreateIndex(
          name: "IX_EmployeePhones_Phone_Number_Mobile",
          table: "EmployeePhones",
          column: "Phone_Number",
          unique: true,
          filter: "\"LineType\" = 'Mobile' AND \"DeletedAt\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "EmployeePhones");
    }
  }
}
