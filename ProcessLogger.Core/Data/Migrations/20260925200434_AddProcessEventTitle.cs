using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcessLogger.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessEventTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ProcessEvents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "ProcessEvents");
        }
    }
}
