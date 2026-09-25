using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcessLogger.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FocusEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", nullable: true),
                    WindowTitle = table.Column<string>(type: "TEXT", nullable: true),
                    FocusedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FocusLostAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentProcessName = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    CommandLine = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<double>(type: "REAL", nullable: true),
                    PeakWorkingSetBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    PeakPrivateBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    FinalWorkingSetBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    SampleCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocusEvents_FocusedAtUtc",
                table: "FocusEvents",
                column: "FocusedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_Name",
                table: "ProcessEvents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_OpenedUtc",
                table: "ProcessEvents",
                column: "OpenedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FocusEvents");

            migrationBuilder.DropTable(
                name: "ProcessEvents");
        }
    }
}
