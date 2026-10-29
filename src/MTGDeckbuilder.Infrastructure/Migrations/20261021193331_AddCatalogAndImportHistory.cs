using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTGDeckbuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndImportHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Printings_ArenaId",
                table: "Printings");

            migrationBuilder.DropIndex(
                name: "IX_Printings_SetCode_CollectorNumber",
                table: "Printings");

            migrationBuilder.AddColumn<bool>(
                name: "IsInCatalog",
                table: "Printings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Printings",
                type: "TEXT",
                maxLength: 12,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<Guid>(
                name: "LastSeenImportId",
                table: "Printings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "Printings",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SetName",
                table: "Printings",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ColorIdentity",
                table: "Cards",
                type: "TEXT",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "ManaValue",
                table: "Cards",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "SearchName",
                table: "Cards",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CatalogImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceVersion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceUri = table.Column<string>(type: "TEXT", nullable: true),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CacheReady = table.Column<bool>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    ProcessedRecords = table.Column<int>(type: "INTEGER", nullable: false),
                    StagedRecords = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedPrintings = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedPrintings = table.Column<int>(type: "INTEGER", nullable: false),
                    UnlinkedManualPrintings = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogImports", x => x.Id);
                    table.CheckConstraint("CK_Import_Checkpoint", "ProcessedRecords >= 0 AND StagedRecords >= 0 AND StagedRecords <= ProcessedRecords");
                });

            migrationBuilder.CreateTable(
                name: "CatalogStaging",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScryfallId = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogStaging", x => new { x.RunId, x.ScryfallId });
                    table.ForeignKey(
                        name: "FK_CatalogStaging_CatalogImports_RunId",
                        column: x => x.RunId,
                        principalTable: "CatalogImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Printings_ArenaId",
                table: "Printings",
                column: "ArenaId");

            migrationBuilder.CreateIndex(
                name: "IX_Printings_SetCode_CollectorNumber_Language",
                table: "Printings",
                columns: new[] { "SetCode", "CollectorNumber", "Language" },
                unique: true,
                filter: "ScryfallId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Printings_SetCode_CollectorNumber_Language_IsInCatalog",
                table: "Printings",
                columns: new[] { "SetCode", "CollectorNumber", "Language", "IsInCatalog" });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SearchName",
                table: "Cards",
                column: "SearchName");

            migrationBuilder.Sql("UPDATE Cards SET SearchName = upper(Name)");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogImports_StartedUtc",
                table: "CatalogImports",
                column: "StartedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogStaging");

            migrationBuilder.DropTable(
                name: "CatalogImports");

            migrationBuilder.DropIndex(
                name: "IX_Printings_ArenaId",
                table: "Printings");

            migrationBuilder.DropIndex(
                name: "IX_Printings_SetCode_CollectorNumber_Language",
                table: "Printings");

            migrationBuilder.DropIndex(
                name: "IX_Printings_SetCode_CollectorNumber_Language_IsInCatalog",
                table: "Printings");

            migrationBuilder.DropIndex(
                name: "IX_Cards_SearchName",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "IsInCatalog",
                table: "Printings");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Printings");

            migrationBuilder.DropColumn(
                name: "LastSeenImportId",
                table: "Printings");

            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "Printings");

            migrationBuilder.DropColumn(
                name: "SetName",
                table: "Printings");

            migrationBuilder.DropColumn(
                name: "ColorIdentity",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ManaValue",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_Printings_ArenaId",
                table: "Printings",
                column: "ArenaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Printings_SetCode_CollectorNumber",
                table: "Printings",
                columns: new[] { "SetCode", "CollectorNumber" },
                unique: true);
        }
    }
}
