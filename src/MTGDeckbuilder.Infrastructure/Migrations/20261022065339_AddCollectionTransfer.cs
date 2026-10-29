using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTGDeckbuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "CollectionEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "CollectionImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppliedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UndoneUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionImports", x => x.Id);
                    table.CheckConstraint("CK_CollectionImport_Counts", "ChangedCount >= 0 AND SkippedCount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CollectionImportRows",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceData = table.Column<string>(type: "TEXT", nullable: false),
                    SourceLabel = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PrintingId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Skipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExplicitChoice = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchReason = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    BeforeVersion = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrintingSignature = table.Column<string>(type: "TEXT", nullable: true),
                    Changed = table.Column<bool>(type: "INTEGER", nullable: false),
                    AppliedVersion = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionImportRows", x => new { x.BatchId, x.Number });
                    table.CheckConstraint("CK_CollectionImportRow_Quantity", "Quantity >= 0 AND Quantity <= 9999 AND Number >= 1");
                    table.ForeignKey(
                        name: "FK_CollectionImportRows_CollectionImports_BatchId",
                        column: x => x.BatchId,
                        principalTable: "CollectionImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionImportRows_Printings_PrintingId",
                        column: x => x.PrintingId,
                        principalTable: "Printings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionImportRows_BatchId_PrintingId",
                table: "CollectionImportRows",
                columns: new[] { "BatchId", "PrintingId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionImportRows_PrintingId",
                table: "CollectionImportRows",
                column: "PrintingId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionImports_CreatedUtc",
                table: "CollectionImports",
                column: "CreatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionImportRows");

            migrationBuilder.DropTable(
                name: "CollectionImports");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CollectionEntries");
        }
    }
}
