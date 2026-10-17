using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedByToEmployeeGovernmentIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpdatedById",
                table: "EmployeeGovernmentIds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGovernmentIds_UpdatedById",
                table: "EmployeeGovernmentIds",
                column: "UpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeGovernmentIds_Employees_UpdatedById",
                table: "EmployeeGovernmentIds",
                column: "UpdatedById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeGovernmentIds_Employees_UpdatedById",
                table: "EmployeeGovernmentIds");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeGovernmentIds_UpdatedById",
                table: "EmployeeGovernmentIds");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "EmployeeGovernmentIds");
        }
    }
}
