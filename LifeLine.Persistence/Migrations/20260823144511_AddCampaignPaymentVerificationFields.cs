using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeLine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignPaymentVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAccountNameResolved",
                table: "Campaigns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaymentReady",
                table: "Campaigns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentActivatedAt",
                table: "Campaigns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentSetupErrorMessage",
                table: "Campaigns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaystackSubaccountIsActive",
                table: "Campaigns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PaystackSubaccountIsVerified",
                table: "Campaigns",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAccountNameResolved",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "IsPaymentReady",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PaymentActivatedAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PaymentSetupErrorMessage",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PaystackSubaccountIsActive",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PaystackSubaccountIsVerified",
                table: "Campaigns");
        }
    }
}
