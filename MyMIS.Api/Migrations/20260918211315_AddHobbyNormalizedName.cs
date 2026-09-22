using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHobbyNormalizedName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Hobbies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Hobbies_NormalizedName",
                table: "Hobbies",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hobbies_NormalizedName",
                table: "Hobbies");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Hobbies");
        }
    }
}
