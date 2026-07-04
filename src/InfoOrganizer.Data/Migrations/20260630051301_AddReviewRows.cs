using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoOrganizer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReviewRowId",
                table: "Movements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    RawRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", nullable: true),
                    Sku = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LocationName = table.Column<string>(type: "TEXT", nullable: true),
                    PartyName = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    IsAbsoluteCount = table.Column<bool>(type: "INTEGER", nullable: false),
                    IssuesJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    ExtraAttributesJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewRows_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewRows_RawRecords_RawRecordId",
                        column: x => x.RawRecordId,
                        principalTable: "RawRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ReviewRowId",
                table: "Movements",
                column: "ReviewRowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRows_ImportBatchId",
                table: "ReviewRows",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRows_RawRecordId",
                table: "ReviewRows",
                column: "RawRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRows_Status",
                table: "ReviewRows",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_ReviewRows_ReviewRowId",
                table: "Movements",
                column: "ReviewRowId",
                principalTable: "ReviewRows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movements_ReviewRows_ReviewRowId",
                table: "Movements");

            migrationBuilder.DropTable(
                name: "ReviewRows");

            migrationBuilder.DropIndex(
                name: "IX_Movements_ReviewRowId",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "ReviewRowId",
                table: "Movements");
        }
    }
}
