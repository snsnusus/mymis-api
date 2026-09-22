using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeMiddleNameRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing NULLs have to be resolved before the NOT NULL constraint
            // can be applied — AlterColumn's defaultValue only governs future
            // rows, it doesn't retroactively touch rows that already exist
            migrationBuilder.Sql(
                "UPDATE \"Employees\" SET \"MiddleName\" = '' WHERE \"MiddleName\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "MiddleName",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MiddleName",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
