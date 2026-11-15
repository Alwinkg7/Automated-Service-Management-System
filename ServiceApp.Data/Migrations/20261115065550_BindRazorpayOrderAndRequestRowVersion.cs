using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class BindRazorpayOrderAndRequestRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ServiceRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazorpayOrderId",
                table: "Bills",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_RazorpayOrderId",
                table: "Bills",
                column: "RazorpayOrderId",
                unique: true,
                filter: "[RazorpayOrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_RazorpayOrderId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "RazorpayOrderId",
                table: "Bills");
        }
    }
}
