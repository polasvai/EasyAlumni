using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyAlumni.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJanataPayGatewayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayFtNumber",
                table: "RegistrationPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayReferenceId",
                table: "RegistrationPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayStatus",
                table: "RegistrationPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayTransactionToken",
                table: "RegistrationPayments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayFtNumber",
                table: "RegistrationPayments");

            migrationBuilder.DropColumn(
                name: "GatewayReferenceId",
                table: "RegistrationPayments");

            migrationBuilder.DropColumn(
                name: "GatewayStatus",
                table: "RegistrationPayments");

            migrationBuilder.DropColumn(
                name: "GatewayTransactionToken",
                table: "RegistrationPayments");
        }
    }
}
