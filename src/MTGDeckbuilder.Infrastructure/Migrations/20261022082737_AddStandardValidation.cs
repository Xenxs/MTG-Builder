using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTGDeckbuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ValidationProfile",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MetadataVersion",
                table: "CatalogImports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedUtc",
                table: "CatalogImports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardLegalities",
                columns: table => new
                {
                    CardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Standard = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardLegalities", x => x.CardId);
                    table.CheckConstraint("CK_CardLegality_Standard", "Standard IN (0, 1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_CardLegalities_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardLegalities_CatalogImports_RunId",
                        column: x => x.RunId,
                        principalTable: "CatalogImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardLegalities_RunId",
                table: "CardLegalities",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardLegalities");

            migrationBuilder.DropColumn(
                name: "ValidationProfile",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "MetadataVersion",
                table: "CatalogImports");

            migrationBuilder.DropColumn(
                name: "SourceUpdatedUtc",
                table: "CatalogImports");
        }
    }
}
