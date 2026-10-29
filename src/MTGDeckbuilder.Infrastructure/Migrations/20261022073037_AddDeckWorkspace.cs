using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTGDeckbuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RebuildDeckCards(migrationBuilder, "0, 1, 2");

            migrationBuilder.CreateTable(
                name: "DeckImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    SourceText = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    DeckId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeckImportRows",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SetCode = table.Column<string>(type: "TEXT", nullable: true),
                    CollectorNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Zone = table.Column<int>(type: "INTEGER", nullable: false),
                    PrintingId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrintingSignature = table.Column<string>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckImportRows", x => new { x.BatchId, x.Number });
                    table.CheckConstraint("CK_DeckImportRow_Quantity", "Quantity >= 1 AND Quantity <= 9999 AND Number >= 1");
                    table.CheckConstraint("CK_DeckImportRow_Zone", "Zone IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_DeckImportRows_DeckImports_BatchId",
                        column: x => x.BatchId,
                        principalTable: "DeckImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeckImportRows_Printings_PrintingId",
                        column: x => x.PrintingId,
                        principalTable: "Printings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeckImportRows_PrintingId",
                table: "DeckImportRows",
                column: "PrintingId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckImports_CreatedUtc",
                table: "DeckImports",
                column: "CreatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeckImportRows");

            migrationBuilder.DropTable(
                name: "DeckImports");

            RebuildDeckCards(migrationBuilder, "0, 1");
        }

        private static void RebuildDeckCards(MigrationBuilder migrationBuilder, string zones)
        {
            // DeckCards is a leaf table: nothing references it. Rebuild with foreign keys ON
            // inside EF's migration transaction, avoiding the provider's generic nontransactional PRAGMA.
            migrationBuilder.Sql($"""
                CREATE TABLE "__phase4_DeckCards" (
                    "DeckId" TEXT NOT NULL,
                    "PrintingId" TEXT NOT NULL,
                    "Zone" INTEGER NOT NULL,
                    "Quantity" INTEGER NOT NULL,
                    CONSTRAINT "PK_DeckCards" PRIMARY KEY ("DeckId", "PrintingId", "Zone"),
                    CONSTRAINT "CK_DeckCard_Quantity" CHECK (Quantity >= 1 AND Quantity <= 9999),
                    CONSTRAINT "CK_DeckCard_Zone" CHECK (Zone IN ({zones})),
                    CONSTRAINT "FK_DeckCards_Decks_DeckId" FOREIGN KEY ("DeckId") REFERENCES "Decks" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_DeckCards_Printings_PrintingId" FOREIGN KEY ("PrintingId") REFERENCES "Printings" ("Id") ON DELETE RESTRICT
                );
                INSERT INTO "__phase4_DeckCards" ("DeckId", "PrintingId", "Zone", "Quantity")
                    SELECT "DeckId", "PrintingId", "Zone", "Quantity" FROM "DeckCards";
                DROP TABLE "DeckCards";
                ALTER TABLE "__phase4_DeckCards" RENAME TO "DeckCards";
                CREATE INDEX "IX_DeckCards_PrintingId" ON "DeckCards" ("PrintingId");
                """);
        }
    }
}
