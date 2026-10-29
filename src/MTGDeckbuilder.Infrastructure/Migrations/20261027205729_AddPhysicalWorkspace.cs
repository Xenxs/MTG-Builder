using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTGDeckbuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperDecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Profile = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperDecks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    StockHash = table.Column<string>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperPrintings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperPrintings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperDeckCards",
                columns: table => new
                {
                    DeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrintingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Zone = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperDeckCards", x => new { x.DeckId, x.PrintingId, x.Zone });
                    table.CheckConstraint("CK_PaperDeckCard_Quantity", "Quantity >= 1 AND Quantity <= 9999");
                    table.CheckConstraint("CK_PaperDeckCard_Zone", "Zone IN (0,1,2)");
                    table.ForeignKey(
                        name: "FK_PaperDeckCards_PaperDecks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "PaperDecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaperDeckCards_PaperPrintings_PrintingId",
                        column: x => x.PrintingId,
                        principalTable: "PaperPrintings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaperHoldings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    PrintingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    Finish = table.Column<string>(type: "TEXT", nullable: false),
                    Condition = table.Column<string>(type: "TEXT", nullable: false),
                    Binder = table.Column<string>(type: "TEXT", nullable: false),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperHoldings", x => x.Id);
                    table.CheckConstraint("CK_PaperHolding_Quantity", "Quantity >= 0 AND Quantity <= 9999");
                    table.ForeignKey(
                        name: "FK_PaperHoldings_PaperPrintings_PrintingId",
                        column: x => x.PrintingId,
                        principalTable: "PaperPrintings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperDeckCards_PrintingId",
                table: "PaperDeckCards",
                column: "PrintingId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperHoldings_Key_IsDemo",
                table: "PaperHoldings",
                columns: new[] { "Key", "IsDemo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperHoldings_PrintingId",
                table: "PaperHoldings",
                column: "PrintingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperDeckCards");

            migrationBuilder.DropTable(
                name: "PaperHoldings");

            migrationBuilder.DropTable(
                name: "PaperImports");

            migrationBuilder.DropTable(
                name: "PaperDecks");

            migrationBuilder.DropTable(
                name: "PaperPrintings");
        }
    }
}
