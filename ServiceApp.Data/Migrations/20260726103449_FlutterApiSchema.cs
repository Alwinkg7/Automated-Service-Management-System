using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlutterApiSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_ServiceRequests_RequestId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceHistories_ServiceRequests_RequestId",
                table: "ServiceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_TechnicianProfiles_AssignedTechnicianProfileId",
                table: "ServiceRequests");

            migrationBuilder.RenameColumn(
                name: "RequestId",
                table: "Bills",
                newName: "ServiceRequestId");

            migrationBuilder.RenameColumn(
                name: "BillId",
                table: "Bills",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Bills_RequestId",
                table: "Bills",
                newName: "IX_Bills_ServiceRequestId");

            migrationBuilder.AlterColumn<string>(
                name: "PinCode",
                table: "ServiceRequests",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "ServiceRequests",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedById",
                table: "ServiceHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceRequestId",
                table: "ServiceHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Bills",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "LaborCost",
                table: "Bills",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialCost",
                table: "Bills",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "AspNetUsers",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15);

            migrationBuilder.CreateTable(
                name: "StatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceRequestId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedById = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusHistories_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceHistories_ServiceRequestId",
                table: "ServiceHistories",
                column: "ServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusHistories_ServiceRequestId",
                table: "StatusHistories",
                column: "ServiceRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_ServiceRequests_ServiceRequestId",
                table: "Bills",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "RequestId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceHistories_ServiceRequests_RequestId",
                table: "ServiceHistories",
                column: "RequestId",
                principalTable: "ServiceRequests",
                principalColumn: "RequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceHistories_ServiceRequests_ServiceRequestId",
                table: "ServiceHistories",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "RequestId",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_TechnicianProfiles_AssignedTechnicianProfileId",
                table: "ServiceRequests",
                column: "AssignedTechnicianProfileId",
                principalTable: "TechnicianProfiles",
                principalColumn: "TechnicianProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_ServiceRequests_ServiceRequestId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceHistories_ServiceRequests_RequestId",
                table: "ServiceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceHistories_ServiceRequests_ServiceRequestId",
                table: "ServiceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_TechnicianProfiles_AssignedTechnicianProfileId",
                table: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "StatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_ServiceHistories_ServiceRequestId",
                table: "ServiceHistories");

            migrationBuilder.DropColumn(
                name: "ChangedById",
                table: "ServiceHistories");

            migrationBuilder.DropColumn(
                name: "ServiceRequestId",
                table: "ServiceHistories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "LaborCost",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "MaterialCost",
                table: "Bills");

            migrationBuilder.RenameColumn(
                name: "ServiceRequestId",
                table: "Bills",
                newName: "RequestId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Bills",
                newName: "BillId");

            migrationBuilder.RenameIndex(
                name: "IX_Bills_ServiceRequestId",
                table: "Bills",
                newName: "IX_Bills_RequestId");

            migrationBuilder.AlterColumn<string>(
                name: "PinCode",
                table: "ServiceRequests",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "ServiceRequests",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "AspNetUsers",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_ServiceRequests_RequestId",
                table: "Bills",
                column: "RequestId",
                principalTable: "ServiceRequests",
                principalColumn: "RequestId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceHistories_ServiceRequests_RequestId",
                table: "ServiceHistories",
                column: "RequestId",
                principalTable: "ServiceRequests",
                principalColumn: "RequestId",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_TechnicianProfiles_AssignedTechnicianProfileId",
                table: "ServiceRequests",
                column: "AssignedTechnicianProfileId",
                principalTable: "TechnicianProfiles",
                principalColumn: "TechnicianProfileId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
