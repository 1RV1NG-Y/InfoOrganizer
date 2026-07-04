using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoOrganizer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementLocationAndExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraAttributesJson",
                table: "Movements",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "Movements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movements_LocationName",
                table: "Movements",
                column: "LocationName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Movements_LocationName",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "ExtraAttributesJson",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "Movements");
        }
    }
}
