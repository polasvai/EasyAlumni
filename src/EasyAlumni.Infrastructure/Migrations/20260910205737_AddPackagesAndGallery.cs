using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyAlumni.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagesAndGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegistrationPackageId",
                table: "EventRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GalleryImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReunionEventId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GalleryImages_ReunionEvents_ReunionEventId",
                        column: x => x.ReunionEventId,
                        principalTable: "ReunionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReunionEventId = table.Column<int>(type: "int", nullable: false),
                    PackageName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PackageNameBangla = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludesTShirt = table.Column<bool>(type: "bit", nullable: false),
                    IncludesKitBag = table.Column<bool>(type: "bit", nullable: false),
                    IncludesFood = table.Column<bool>(type: "bit", nullable: false),
                    IncludesRaffle = table.Column<bool>(type: "bit", nullable: false),
                    IncludedGuests = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BadgeText = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationPackages_ReunionEvents_ReunionEventId",
                        column: x => x.ReunionEventId,
                        principalTable: "ReunionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventRegistrations_RegistrationPackageId",
                table: "EventRegistrations",
                column: "RegistrationPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_GalleryImages_ReunionEventId",
                table: "GalleryImages",
                column: "ReunionEventId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPackages_ReunionEventId",
                table: "RegistrationPackages",
                column: "ReunionEventId");

            migrationBuilder.AddForeignKey(
                name: "FK_EventRegistrations_RegistrationPackages_RegistrationPackageId",
                table: "EventRegistrations",
                column: "RegistrationPackageId",
                principalTable: "RegistrationPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventRegistrations_RegistrationPackages_RegistrationPackageId",
                table: "EventRegistrations");

            migrationBuilder.DropTable(
                name: "GalleryImages");

            migrationBuilder.DropTable(
                name: "RegistrationPackages");

            migrationBuilder.DropIndex(
                name: "IX_EventRegistrations_RegistrationPackageId",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "RegistrationPackageId",
                table: "EventRegistrations");
        }
    }
}
