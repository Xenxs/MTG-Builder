using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTGDeckbuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OracleId = table.Column<string>(type: "TEXT", nullable: true),
                    ManaCost = table.Column<string>(type: "TEXT", nullable: true),
                    TypeLine = table.Column<string>(type: "TEXT", nullable: true),
                    OracleText = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Decks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.CheckConstraint("CK_Settings_Singleton", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "Printings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SetCode = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    CollectorNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ArenaId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScryfallId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Printings_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionEntries",
                columns: table => new
                {
                    PrintingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionEntries", x => x.PrintingId);
                    table.CheckConstraint("CK_Collection_Quantity", "Quantity >= 0 AND Quantity <= 9999");
                    table.ForeignKey(
                        name: "FK_CollectionEntries_Printings_PrintingId",
                        column: x => x.PrintingId,
                        principalTable: "Printings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeckCards",
                columns: table => new
                {
                    DeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrintingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Zone = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckCards", x => new { x.DeckId, x.PrintingId, x.Zone });
                    table.CheckConstraint("CK_DeckCard_Quantity", "Quantity >= 1 AND Quantity <= 9999");
                    table.CheckConstraint("CK_DeckCard_Zone", "Zone IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_DeckCards_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeckCards_Printings_PrintingId",
                        column: x => x.PrintingId,
                        principalTable: "Printings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Name",
                table: "Cards",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_OracleId",
                table: "Cards",
                column: "OracleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeckCards_PrintingId",
                table: "DeckCards",
                column: "PrintingId");

            migrationBuilder.CreateIndex(
                name: "IX_Printings_ArenaId",
                table: "Printings",
                column: "ArenaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Printings_CardId",
                table: "Printings",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_Printings_ScryfallId",
                table: "Printings",
                column: "ScryfallId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Printings_SetCode_CollectorNumber",
                table: "Printings",
                columns: new[] { "SetCode", "CollectorNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionEntries");

            migrationBuilder.DropTable(
                name: "DeckCards");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Decks");

            migrationBuilder.DropTable(
                name: "Printings");

            migrationBuilder.DropTable(
                name: "Cards");
        }
    }
}
