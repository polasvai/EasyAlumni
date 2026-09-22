using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyAlumni.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Donations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonationTrackingNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReunionEventId = table.Column<int>(type: "int", nullable: true),
                    DonorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DonorEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DonorPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BatchYear = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DonationPurpose = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PaymentMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SenderNumber = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    GatewayFtNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GatewayReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SlipAttachmentPath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Donations_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Donations_ReunionEvents_ReunionEventId",
                        column: x => x.ReunionEventId,
                        principalTable: "ReunionEvents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ApprovedByUserId",
                table: "Donations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_DonationTrackingNo",
                table: "Donations",
                column: "DonationTrackingNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ReunionEventId",
                table: "Donations",
                column: "ReunionEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Donations");
        }
    }
}
