using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class MigrationUpgradeTests
{
    [Fact]
    public async Task PhaseOneDatabaseIsBackedUpAndMigratedWithoutChangingUserKeysOrAmounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        Directory.CreateDirectory(test.Paths.DataDirectory);
        var cardId = Guid.NewGuid(); var printingId = Guid.NewGuid(); var deckId = Guid.NewGuid();
        await using (var old = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(ct))
        {
            await old.GetService<IMigrator>().MigrateAsync("20261021182801_InitialCreate", ct);
            await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Cards (Id, Name) VALUES ({cardId}, {"Llanowar Elves"})", ct);
            await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Printings (Id, CardId, SetCode, CollectorNumber) VALUES ({printingId}, {cardId}, {"FDN"}, {"227"})", ct);
            await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollectionEntries (PrintingId, Quantity) VALUES ({printingId}, {4})", ct);
            await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Decks (Id, Name, Format, CreatedUtc) VALUES ({deckId}, {"Elves"}, {"Standard"}, {DateTime.UtcNow})", ct);
            await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO DeckCards (DeckId, PrintingId, Zone, Quantity) VALUES ({deckId}, {printingId}, {0}, {3})", ct);
            await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Settings (Id, PlayerName) VALUES ({1}, {"Nissa"})", ct);
        }
        await test.InitializeAsync();
        var item = Assert.Single(await test.Service.GetCollectionAsync(ct));
        Assert.Equal(printingId, item.PrintingId);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(3, Assert.Single(await test.Service.GetDeckCardsAsync(deckId, ct)).Quantity);
        Assert.Equal("Nissa", await test.Service.GetPlayerNameAsync(ct));
        await using (var current = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(ct))
        {
            Assert.Equal("en", (await current.Printings.SingleAsync(ct)).Language);
            Assert.Equal("LLANOWAR ELVES", (await current.Cards.SingleAsync(ct)).SearchName);
            Assert.False(current.Database.HasPendingModelChanges());
        }
        var backup = Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
        using var backupDb = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backup, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await backupDb.OpenAsync(ct);
        using var command = backupDb.CreateCommand();
        command.CommandText = "SELECT Quantity FROM CollectionEntries";
        Assert.Equal(4L, await command.ExecuteScalarAsync(ct));
        command.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
        Assert.Equal(1L, await command.ExecuteScalarAsync(ct));
        await test.InitializeAsync();
        Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
    }
}
