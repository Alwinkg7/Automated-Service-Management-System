using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class BillDiscountsAndPaymentPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Bills",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PointsRedeemed",
                table: "Bills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PreferredPaymentMethod",
                table: "Bills",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromoCode",
                table: "Bills",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PointsRedeemed",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PreferredPaymentMethod",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PromoCode",
                table: "Bills");
        }
    }
}
