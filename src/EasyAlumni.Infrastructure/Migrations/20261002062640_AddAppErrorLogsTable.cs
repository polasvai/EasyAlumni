using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyAlumni.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppErrorLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppErrorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Controller = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UserIdentifier = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSyncedToRemote = table.Column<bool>(type: "bit", nullable: false),
                    RemoteSyncError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RemoteSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppErrorLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppErrorLogs_CreatedAt",
                table: "AppErrorLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppErrorLogs_IsSyncedToRemote",
                table: "AppErrorLogs",
                column: "IsSyncedToRemote");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppErrorLogs");
        }
    }
}
