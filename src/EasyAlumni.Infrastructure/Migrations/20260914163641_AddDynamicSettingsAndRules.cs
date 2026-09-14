using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyAlumni.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicSettingsAndRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxPassingYear",
                table: "RegistrationPackages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinPassingYear",
                table: "RegistrationPackages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventCustomQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReunionEventId = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    QuestionTextBangla = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FieldType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubQuestionText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OptionsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCustomQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventCustomQuestions_ReunionEvents_ReunionEventId",
                        column: x => x.ReunionEventId,
                        principalTable: "ReunionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReunionEventId = table.Column<int>(type: "int", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryNameBangla = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EligibilityRules = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    GenderRestriction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaxAllowed = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestCategories_ReunionEvents_ReunionEventId",
                        column: x => x.ReunionEventId,
                        principalTable: "ReunionEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageGiftItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationPackageId = table.Column<int>(type: "int", nullable: false),
                    GiftItemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageGiftItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageGiftItems_GiftItems_GiftItemId",
                        column: x => x.GiftItemId,
                        principalTable: "GiftItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageGiftItems_RegistrationPackages_RegistrationPackageId",
                        column: x => x.RegistrationPackageId,
                        principalTable: "RegistrationPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationGiftChoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventRegistrationId = table.Column<int>(type: "int", nullable: false),
                    GiftItemId = table.Column<int>(type: "int", nullable: false),
                    SelectedSize = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationGiftChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationGiftChoices_EventRegistrations_EventRegistrationId",
                        column: x => x.EventRegistrationId,
                        principalTable: "EventRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrationGiftChoices_GiftItems_GiftItemId",
                        column: x => x.GiftItemId,
                        principalTable: "GiftItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationQuestionResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventRegistrationId = table.Column<int>(type: "int", nullable: false),
                    EventCustomQuestionId = table.Column<int>(type: "int", nullable: false),
                    AnswerValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SubAnswerValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationQuestionResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationQuestionResponses_EventCustomQuestions_EventCustomQuestionId",
                        column: x => x.EventCustomQuestionId,
                        principalTable: "EventCustomQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrationQuestionResponses_EventRegistrations_EventRegistrationId",
                        column: x => x.EventRegistrationId,
                        principalTable: "EventRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationGuests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventRegistrationId = table.Column<int>(type: "int", nullable: false),
                    GuestCategoryId = table.Column<int>(type: "int", nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    FeeCharged = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationGuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationGuests_EventRegistrations_EventRegistrationId",
                        column: x => x.EventRegistrationId,
                        principalTable: "EventRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrationGuests_GuestCategories_GuestCategoryId",
                        column: x => x.GuestCategoryId,
                        principalTable: "GuestCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventCustomQuestions_ReunionEventId",
                table: "EventCustomQuestions",
                column: "ReunionEventId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestCategories_ReunionEventId",
                table: "GuestCategories",
                column: "ReunionEventId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageGiftItems_GiftItemId",
                table: "PackageGiftItems",
                column: "GiftItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageGiftItems_RegistrationPackageId",
                table: "PackageGiftItems",
                column: "RegistrationPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationGiftChoices_EventRegistrationId",
                table: "RegistrationGiftChoices",
                column: "EventRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationGiftChoices_GiftItemId",
                table: "RegistrationGiftChoices",
                column: "GiftItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationGuests_EventRegistrationId",
                table: "RegistrationGuests",
                column: "EventRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationGuests_GuestCategoryId",
                table: "RegistrationGuests",
                column: "GuestCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationQuestionResponses_EventCustomQuestionId",
                table: "RegistrationQuestionResponses",
                column: "EventCustomQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationQuestionResponses_EventRegistrationId",
                table: "RegistrationQuestionResponses",
                column: "EventRegistrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageGiftItems");

            migrationBuilder.DropTable(
                name: "RegistrationGiftChoices");

            migrationBuilder.DropTable(
                name: "RegistrationGuests");

            migrationBuilder.DropTable(
                name: "RegistrationQuestionResponses");

            migrationBuilder.DropTable(
                name: "GuestCategories");

            migrationBuilder.DropTable(
                name: "EventCustomQuestions");

            migrationBuilder.DropColumn(
                name: "MaxPassingYear",
                table: "RegistrationPackages");

            migrationBuilder.DropColumn(
                name: "MinPassingYear",
                table: "RegistrationPackages");
        }
    }
}
