using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoOrganizer.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    ReorderThreshold = table.Column<decimal>(type: "TEXT", nullable: true),
                    ExtraAttributesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MappingJson = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultRecordType = table.Column<int>(type: "INTEGER", nullable: false),
                    HintsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_SourceProfiles_SourceProfileId",
                        column: x => x.SourceProfileId,
                        principalTable: "SourceProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RawRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    DataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawRecords_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PartyName = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    RawRecordId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movements_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Movements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Movements_RawRecords_RawRecordId",
                        column: x => x.RawRecordId,
                        principalTable: "RawRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_SourceProfileId",
                table: "ImportBatches",
                column: "SourceProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ImportBatchId",
                table: "Movements",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_OccurredOn",
                table: "Movements",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ProductId",
                table: "Movements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_RawRecordId",
                table: "Movements",
                column: "RawRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_RawRecords_ImportBatchId",
                table: "RawRecords",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceProfiles_Fingerprint",
                table: "SourceProfiles",
                column: "Fingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "RawRecords");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "SourceProfiles");
        }
    }
}
