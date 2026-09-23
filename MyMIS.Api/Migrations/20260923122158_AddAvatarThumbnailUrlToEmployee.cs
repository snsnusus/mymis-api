using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMIS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarThumbnailUrlToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarThumbnailUrl",
                table: "Employees",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarThumbnailUrl",
                table: "Employees");
        }
    }
}
