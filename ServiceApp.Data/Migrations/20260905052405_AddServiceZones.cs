using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── KEEP ONLY THIS: Create ServiceZones table ──────────

            migrationBuilder.CreateTable(
                name: "ServiceZones",
                columns: table => new
                {
                    ZoneId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZoneName = table.Column<string>(
                        maxLength: 100, nullable: false),
                    City = table.Column<string>(
                        maxLength: 100, nullable: false),
                    State = table.Column<string>(
                        maxLength: 100, nullable: false),
                    PinCodes = table.Column<string>(
                        maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(
                        nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceZones", x => x.ZoneId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceZones_ZoneName",
                table: "ServiceZones",
                column: "ZoneName",
                unique: true);

            // ── Add ServiceZoneId to TechnicianProfiles ────────────

            migrationBuilder.AddColumn<int>(
                name: "ServiceZoneId",
                table: "TechnicianProfiles",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianProfiles_ServiceZoneId",
                table: "TechnicianProfiles",
                column: "ServiceZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicianProfiles_ServiceZones_ServiceZoneId",
                table: "TechnicianProfiles",
                column: "ServiceZoneId",
                principalTable: "ServiceZones",
                principalColumn: "ZoneId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TechnicianProfiles_ServiceZones_ServiceZoneId",
                table: "TechnicianProfiles");

            migrationBuilder.DropIndex(
                name: "IX_TechnicianProfiles_ServiceZoneId",
                table: "TechnicianProfiles");

            migrationBuilder.DropColumn(
                name: "ServiceZoneId",
                table: "TechnicianProfiles");

            migrationBuilder.DropTable(name: "ServiceZones");
        }
    }
}