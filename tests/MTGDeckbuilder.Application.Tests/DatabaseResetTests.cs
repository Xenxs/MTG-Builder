using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;
namespace MTGDeckbuilder.Application.Tests;

public sealed class DatabaseResetTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private static IDatabaseResetService Reset(TestDatabase t)=>t.Provider.GetRequiredService<IDatabaseResetService>();
    private static IPaperWorkspace Paper(TestDatabase t)=>t.Provider.GetRequiredService<IPaperWorkspace>();
    private static IDbContextFactory<DeckbuilderDbContext> Factory(TestDatabase t)=>t.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
    private static async Task Seed(TestDatabase t)
    {
        await t.InitializeAsync();await Paper(t).InitializeExampleAsync(Ct);await t.Service.SavePlayerNameAsync("Before reset",Ct);
        await t.Service.SaveCollectionAsync(new("Legacy preserved until explicit reset","TEST","1",5),Ct);
        await using var db=await Factory(t).CreateDbContextAsync(Ct);var old=await db.Printings.FirstAsync(Ct);var deck=new Deck("Old deck","Casual");db.Decks.Add(deck);db.DeckCards.Add(new(deck.Id,old.Id,DeckZone.Mainboard,2));await db.SaveChangesAsync(Ct);
    }
    private static async Task<string> Dump(TestDatabase t)
    {
        await using var db=await Factory(t).CreateDbContextAsync(Ct);await db.Database.OpenConnectionAsync(Ct);var tables=db.Model.GetEntityTypes().Select(e=>e.GetTableName()!).Distinct().Order(StringComparer.Ordinal);var output=new Dictionary<string,List<object[]>>();
        foreach(var table in tables){using var cmd=db.Database.GetDbConnection().CreateCommand();cmd.CommandText="SELECT * FROM \""+table+"\" ORDER BY rowid";using var reader=await cmd.ExecuteReaderAsync(Ct);var rows=new List<object[]>();while(await reader.ReadAsync(Ct)){var row=new object[reader.FieldCount];reader.GetValues(row);rows.Add(row);}output[table]=rows;}
        return JsonSerializer.Serialize(output);
    }
    [Fact]
    public async Task ReviewAndWrongConfirmationNeverEraseDataOrCreateBackup()
    {
        await using var t=new TestDatabase();await Seed(t);var before=await Dump(t);var review=await Reset(t).ReviewAsync(Ct);Assert.Contains("118 fysieke voorraadregels",review.Summary);Assert.Equal(20,review.Tables.Count);
        foreach(var word in new[]{"","wissen","WISSEN "})await Assert.ThrowsAsync<ArgumentException>(()=>Reset(t).ClearAsync(review.Id,word,Ct));
        Assert.Equal(before,await Dump(t));Assert.False(Directory.Exists(t.Paths.BackupsDirectory));
    }
    [Fact]
    public async Task ClearBacksUpAllDataKeepsSchemaAndStaysEmptyAfterRestartThenCanRestore()
    {
        await using var t=new TestDatabase();await Seed(t);var before=await Dump(t);var review=await Reset(t).ReviewAsync(Ct);var receipt=await Reset(t).ClearAsync(review.Id,"WISSEN",Ct);
        Assert.True(File.Exists(receipt.BackupPath));Assert.Contains("pre-reset-",receipt.BackupPath);await t.ReopenAsync();await Paper(t).InitializeExampleAsync(Ct);
        var summary=await Paper(t).SummaryAsync(Ct);Assert.Equal(0,summary.Copies);Assert.Equal(0,summary.Decks);Assert.False(summary.DemoActive);
        await using(var db=await Factory(t).CreateDbContextAsync(Ct))
        {Assert.Equal(9,(await db.Database.GetAppliedMigrationsAsync(Ct)).Count());Assert.False((await db.Settings.SingleAsync(Ct)).ExamplesEnabled);Assert.Equal("Planeswalker",(await db.Settings.SingleAsync(Ct)).PlayerName);Assert.Empty(await db.Cards.ToArrayAsync(Ct));Assert.Empty(await db.PaperPrintings.ToArrayAsync(Ct));Assert.Empty(await db.PaperImports.ToArrayAsync(Ct));}
        var backups=t.Provider.GetRequiredService<IBackupService>();var restore=await backups.InspectAsync(receipt.BackupPath,Ct);await backups.ScheduleAsync(restore.Id,true,Ct);SqliteConnection.ClearAllPools();
        using(var lease=DataDirectoryLease.Acquire(t.Paths))await t.Provider.GetRequiredService<SqliteBackupService>().ApplyPendingAsync(lease,Ct);await t.ReopenAsync();Assert.Equal(before,await Dump(t));
    }
    [Fact]
    public async Task ChangedDatabaseBlocksReviewAndRepeatedCommitDoesNotEraseLaterData()
    {
        await using var t=new TestDatabase();await Seed(t);var review=await Reset(t).ReviewAsync(Ct);await t.Service.SavePlayerNameAsync("Changed",Ct);
        await Assert.ThrowsAsync<ArgumentException>(()=>Reset(t).ClearAsync(review.Id,"WISSEN",Ct));Assert.Equal(202,(await Paper(t).SummaryAsync(Ct)).Copies);
        review=await Reset(t).ReviewAsync(Ct);var result=await Reset(t).ClearAsync(review.Id,"WISSEN",Ct);await t.Service.SavePlayerNameAsync("Later data",Ct);
        Assert.Equal(result,await Reset(t).ClearAsync(review.Id,"WISSEN",Ct));Assert.Equal("Later data",await t.Service.GetPlayerNameAsync(Ct));
    }
    [Fact]
    public async Task BackupFailureAndPendingRestoreBlockDeletion()
    {
        await using var t=new TestDatabase();await Seed(t);var review=await Reset(t).ReviewAsync(Ct);var before=await Dump(t);await File.WriteAllTextAsync(t.Paths.BackupsDirectory,"occupied",Ct);
        await Assert.ThrowsAsync<IOException>(()=>Reset(t).ClearAsync(review.Id,"WISSEN",Ct));Assert.Equal(before,await Dump(t));File.Delete(t.Paths.BackupsDirectory);
        var backups=t.Provider.GetRequiredService<IBackupService>();var file=await backups.CreateAsync(Ct);var restored=await backups.InspectAsync(file.Path,Ct);await backups.ScheduleAsync(restored.Id,true,Ct);
        await Assert.ThrowsAsync<ArgumentException>(()=>Reset(t).ReviewAsync(Ct));await Assert.ThrowsAsync<ArgumentException>(()=>Reset(t).ClearAsync(review.Id,"WISSEN",Ct));Assert.Equal(before,await Dump(t));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterDeleteSqlRollsBackEveryTableAndKeepsSafetyCopy(bool cancellation)
    {
        var fault=new ClearFailure();await using var t=new TestDatabase(s=>s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp=>new FaultFactory(sp.GetRequiredService<AppPaths>(),fault)));await Seed(t);var before=await Dump(t);var review=await Reset(t).ReviewAsync(Ct);fault.Armed=true;
        using var cancel=new CancellationTokenSource();if(cancellation)fault.Cancel=cancel;
        if(cancellation)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Reset(t).ClearAsync(review.Id,"WISSEN",cancel.Token));else await Assert.ThrowsAsync<IOException>(()=>Reset(t).ClearAsync(review.Id,"WISSEN",Ct));
        Assert.True(fault.Fired);Assert.Equal(before,await Dump(t));Assert.Single(Directory.GetFiles(t.Paths.BackupsDirectory,"pre-reset-*.db"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnrestorableSchemaOrForeignKeysBlockResetBeforeDeletion(bool foreignKey)
    {
        await using var t=new TestDatabase();await Seed(t);
        using(var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=t.Paths.DatabasePath,ForeignKeys=false,Pooling=false}.ToString()))
        {
            await connection.OpenAsync(Ct);using var cmd=connection.CreateCommand();
            cmd.CommandText=foreignKey?"UPDATE PaperDeckCards SET PrintingId='00000000-0000-0000-0000-000000000000' WHERE rowid=(SELECT MIN(rowid) FROM PaperDeckCards)":"CREATE TABLE UnknownUserData (Id INTEGER PRIMARY KEY, Value TEXT)";
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        var before=await Dump(t);var review=await Reset(t).ReviewAsync(Ct);
        await Assert.ThrowsAsync<ArgumentException>(()=>Reset(t).ClearAsync(review.Id,"WISSEN",Ct));Assert.Equal(before,await Dump(t));
        Assert.Single(Directory.GetFiles(t.Paths.BackupsDirectory,"pre-reset-*.db"));
    }
    private sealed class ClearFailure:SaveChangesInterceptor
    {
        public bool Armed{get;set;}public bool Fired{get;private set;}public CancellationTokenSource? Cancel{get;set;}
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken cancellationToken=default)
        {if(Armed&&data.Context!.ChangeTracker.Entries<UserSettings>().Any(e=>!e.Entity.ExamplesEnabled)){Fired=true;if(Cancel is{} c)c.Cancel();else throw new IOException("Injected after DELETE and settings SQL");}return ValueTask.FromResult(result);}
    }
    private sealed class FaultFactory(AppPaths paths,SaveChangesInterceptor fault):IDbContextFactory<DeckbuilderDbContext>
    {public DeckbuilderDbContext CreateDbContext()=>new(new DbContextOptionsBuilder<DeckbuilderDbContext>().UseSqlite(new SqliteConnectionStringBuilder{DataSource=paths.DatabasePath,ForeignKeys=true}.ToString()).AddInterceptors(fault).Options);}
}
