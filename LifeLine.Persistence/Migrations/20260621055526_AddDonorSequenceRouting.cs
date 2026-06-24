using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeLine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDonorSequenceRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DonorSequenceNumber",
                table: "Donations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "WasRoutedToPlatform",
                table: "Donations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DonorSequenceNumber",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "WasRoutedToPlatform",
                table: "Donations");
        }
    }
}
