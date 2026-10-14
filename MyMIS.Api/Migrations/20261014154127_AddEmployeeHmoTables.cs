using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMIS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeHmoTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeHmos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    HmoProviderId = table.Column<int>(type: "integer", nullable: false),
                    HmoPlanId = table.Column<int>(type: "integer", nullable: false),
                    MemberNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeHmos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeHmos_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeHmos_HmoPlans_HmoPlanId",
                        column: x => x.HmoPlanId,
                        principalTable: "HmoPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeHmos_HmoProviders_HmoProviderId",
                        column: x => x.HmoProviderId,
                        principalTable: "HmoProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeHmoDependents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeHmoId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Suffix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Birthdate = table.Column<DateOnly>(type: "date", nullable: false),
                    Relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HmoPlanId = table.Column<int>(type: "integer", nullable: false),
                    MemberNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeHmoDependents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeHmoDependents_EmployeeHmos_EmployeeHmoId",
                        column: x => x.EmployeeHmoId,
                        principalTable: "EmployeeHmos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeHmoDependents_HmoPlans_HmoPlanId",
                        column: x => x.HmoPlanId,
                        principalTable: "HmoPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHmoDependents_EmployeeHmoId",
                table: "EmployeeHmoDependents",
                column: "EmployeeHmoId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHmoDependents_HmoPlanId",
                table: "EmployeeHmoDependents",
                column: "HmoPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHmos_EmployeeId",
                table: "EmployeeHmos",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHmos_EmployeeId_Active",
                table: "EmployeeHmos",
                column: "EmployeeId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHmos_HmoPlanId",
                table: "EmployeeHmos",
                column: "HmoPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeHmos_HmoProviderId_MemberNumber",
                table: "EmployeeHmos",
                columns: new[] { "HmoProviderId", "MemberNumber" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeHmoDependents");

            migrationBuilder.DropTable(
                name: "EmployeeHmos");
        }
    }
}
