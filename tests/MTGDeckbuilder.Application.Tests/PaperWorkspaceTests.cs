using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class PaperWorkspaceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static IPaperWorkspace Store(TestDatabase t) => t.Provider.GetRequiredService<IPaperWorkspace>();
    private static IDbContextFactory<DeckbuilderDbContext> Factory(TestDatabase t) => t.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
    internal static JsonNode Example()
    {
        using var stream = typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources.conflux-paper-v1.json")!;
        return JsonNode.Parse(stream)!;
    }
    private static TestDatabase Database(Action<IServiceCollection>? configure = null) => new(s => { s.AddSingleton<TimeProvider>(new FixedClock()); configure?.Invoke(s); });
    private sealed class FixedClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(Example()["FetchedUtc"]!.GetValue<DateTime>().AddHours(1)); }
    private static async Task Seed(TestDatabase t) { await t.InitializeAsync(); await Store(t).InitializeExampleAsync(Ct); }
    private static async Task<string> Csv(TestDatabase t, string text)
    {
        var path = Path.Combine(t.Paths.DataDirectory, Guid.NewGuid() + ".csv"); await File.WriteAllTextAsync(path, text, Ct); return path;
    }
    private static string Simple(Guid id, int quantity = 3) => $"Scryfall ID,Quantity,Language,Foil,Condition,Binder Name\n{id},{quantity},English,normal,near_mint,Scans";
    [Fact]
    public async Task PhysicalConfluxProposalContains60CardsWith24BasicsAndNoInventedPossession()
    {
        await using var t = Database(); await Seed(t); var before = await Store(t).CollectionAsync(ct: Ct);
        var draft = await Store(t).PrepareProposalAsync("Fysiek voorstel", false, Ct);
        Assert.Equal(60, draft.Lines.Sum(x => x.Quantity)); Assert.Equal(24, draft.Lines.Where(x => x.Card.IsBasicLand).Sum(x => x.Quantity));
        Assert.Equal(36, draft.Lines.Where(x => !x.Card.TypeLine.Contains("Land", StringComparison.Ordinal)).Sum(x => x.Quantity)); Assert.Empty(draft.Report.Problems);
        var stock = before.GroupBy(x => x.Card.OracleId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var line in draft.Lines) Assert.InRange(line.Quantity, 1, before.Single(x => x.Card.Id == line.Card.Id).Quantity);
        foreach (var g in draft.Lines.GroupBy(x => x.Card.OracleId)) { Assert.InRange(g.Sum(x => x.Quantity), 1, stock[g.Key]); if(!g.First().Card.IsBasicLand) Assert.InRange(g.Sum(x => x.Quantity), 1, 4); }
        Assert.Contains("BEZITMODUS", draft.Details); Assert.Contains("Volledige voorgestelde lijst", draft.Details);
        var id = await Store(t).SaveDeckDraftAsync(draft, Ct); Assert.Equal(id, await Store(t).SaveDeckDraftAsync(draft, Ct));
        await t.ReopenAsync(); Assert.Equal(60, (await Store(t).DeckAsync(id, Ct)).Lines.Sum(x => x.Quantity)); Assert.Equal(202, (await Store(t).SummaryAsync(Ct)).Copies);
    }
    [Fact]
    public async Task PhysicalPlanningIsExplicitAndNeverAddsMissingCardsToOwnership()
    {
        await using var t = Database(); await Seed(t); var c = (await Store(t).CollectionAsync(ct: Ct))[0].Card; await Store(t).SetOwnedAsync(c.Id, 0, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(t).PrepareProposalAsync("Owned cannot succeed", false, Ct));
        var draft = await Store(t).PrepareProposalAsync("Planning", true, Ct); Assert.Equal(60, draft.Lines.Sum(x => x.Quantity)); Assert.Contains("PLANNING", draft.Details); Assert.Contains(draft.Report.Problems, x => x.Contains("tekort"));
        await Store(t).SaveDeckDraftAsync(draft, Ct); Assert.Equal(0, (await Store(t).SummaryAsync(Ct)).Copies);
    }
    [Fact]
    public async Task PhysicalPlannerRejectsChangedOracleMetadataAndIsDeterministic()
    {
        await using var t = Database(); await Seed(t); var stock = await Store(t).CollectionAsync(ct: Ct); var known = stock.Select(x => x.Card).ToArray();
        var root = Example(); var time = root["FetchedUtc"]!.GetValue<DateTime>(); var reference = root["Cards"]!.AsArray().Select(x => ScryfallPaperCatalog.Parse(x!.ToJsonString(), time)).ToArray();
        var first = PhysicalDeckPlanner.Create(known, stock, reference, false); var second = PhysicalDeckPlanner.Create(known, stock, reference, false); Assert.Equal(first, second);
        var selected = first.First(x => !x.Card.IsBasicLand).Card;
        var changed = known.Select(c => c.OracleId == selected.OracleId ? c with { OracleText = c.OracleText + " changed source" } : c).ToArray();
        var result = PhysicalDeckPlanner.Create(changed, stock, reference, false); Assert.DoesNotContain(result, x => x.Card.OracleId == selected.OracleId);
    }
    [Fact]
    public async Task PhysicalAlternativeAvailabilityIncludesOracleCopyLimitAcrossOtherPrintings()
    {
        await using var t = Database(); await Seed(t); var source = (await Store(t).CollectionAsync(ct: Ct)).First(x => x.Card.Name == "Pacifism").Card;
        var root = Example(); var raw = root["Cards"]!.AsArray().First(x => x!["id"]!.GetValue<Guid>() == source.Id)!.DeepClone(); var id = Guid.NewGuid(); raw["id"] = id;
        await using (var db = await Factory(t).CreateDbContextAsync(Ct)) { db.PaperPrintings.Add(new(id, raw.ToJsonString(), root["FetchedUtc"]!.GetValue<DateTime>())); await db.SaveChangesAsync(Ct); }
        await Store(t).SetOwnedAsync(id, 10, Ct); var deck = (await Store(t).DecksAsync(Ct))[0]; var used = (await Store(t).DeckAsync(deck.Id, Ct)).Lines.Where(x => x.Card.OracleId == source.OracleId).Sum(x => x.Quantity);
        var alternative = Assert.Single(await Store(t).AlternativesAsync(deck.Id, source.Id, Ct)); Assert.Equal(4 - used, alternative.Owned);
    }
    [AvaloniaFact]
    public async Task PhysicalProposalRequiresReviewBeforeSavingAndUsesOwnedModeByDefault()
    {
        await using var t = Database(); var shell = t.Provider.GetRequiredService<ShellViewModel>(); var window = new MainWindow { DataContext = shell }; window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("paper-decks"); var vm = Assert.IsType<PaperViewModel>(shell.CurrentPage);
            Assert.False(vm.PlanningProposal); await vm.PrepareProposalCommand.ExecuteAsync(null); Assert.NotNull(vm.DeckDraft); Assert.Equal(60, vm.DeckDraft.Lines.Sum(x => x.Quantity));
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Capture(window, "paper-proposal");
            await vm.SaveDeckDraftCommand.ExecuteAsync(null); Assert.True(shell.Operations.HasError); Assert.Equal(4, (await Store(t).DecksAsync(Ct)).Count);
            vm.ConfirmDeckDraft = true; await vm.SaveDeckDraftCommand.ExecuteAsync(null); Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage); Assert.Equal(5, vm.Decks.Count); Assert.Contains("60 mainboard", vm.DeckSummary);
        }
        finally { window.Close(); }
    }
    [Fact]
    public async Task FullConfluxCsvResolvesAll63RowsAndPreservesAll82CopiesAcrossRestart()
    {
        await using var t = Database(); await Seed(t);
        var p = await Store(t).PrepareCsvAsync(Path.Combine(AppContext.BaseDirectory, "manabox-conflux.csv"), Ct);
        Assert.True(p.CanApply, p.Summary + string.Join(";", p.Rows.Where(x => x.Problem.Length > 0).Select(x => x.Problem)));
        Assert.Equal(63, p.Rows.Count); Assert.Equal(82, p.Rows.Sum(x => x.Source.Quantity));
        await Store(t).ApplyAsync(p.Id, p.Version, Ct); await t.ReopenAsync();
        var summary = await Store(t).SummaryAsync(Ct); Assert.Equal(82, summary.Copies); Assert.False(summary.DemoActive);
    }
    [Fact]
    public async Task PhysicalDeckCopyIsIdempotentAndStockChangesBlockUnappliedDraft()
    {
        await using var t = Database(); await Seed(t); var source = (await Store(t).DecksAsync(Ct))[0];
        var draft = await Store(t).PrepareDeckCopyAsync(source.Id, "Editable physical copy", ct: Ct);
        var id = await Store(t).SaveDeckDraftAsync(draft, Ct); Assert.Equal(id, await Store(t).SaveDeckDraftAsync(draft, Ct));
        Assert.NotEqual(source.Id, id); Assert.Equal(41, (await Store(t).DeckAsync(id, Ct)).Lines.Sum(x => x.Quantity)); Assert.Equal(5, (await Store(t).DecksAsync(Ct)).Count);
        var later = await Store(t).PrepareDeckCopyAsync(source.Id, "Later", ct: Ct); await Store(t).SetOwnedAsync(draft.Lines[0].Card.Id, 1, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(t).SaveDeckDraftAsync(later, Ct)); Assert.Equal(5, (await Store(t).DecksAsync(Ct)).Count);
    }
    [Fact]
    public async Task PhysicalDeckTextRoundTripsExactPrintingsWithoutChangingPossession()
    {
        var handler = new Handler(uri =>
        {
            var parts = uri.AbsolutePath.Split('/'); var set = parts[^3]; var number = parts[^2];
            return new StringContent(Example()["Cards"]!.AsArray().Single(x => x!["set"]!.GetValue<string>() == set.ToLowerInvariant() && x["collector_number"]!.GetValue<string>() == number)!.ToJsonString());
        });
        await using var t = Database(s => s.AddSingleton(new HttpClient(handler))); await Seed(t); var d = (await Store(t).DecksAsync(Ct))[0];
        var exported = await Store(t).ExportDeckAsync(d.Id, Ct); var draft = await Store(t).PrepareDeckTextAsync(exported, "Roundtrip", "en", Ct);
        Assert.Equal(41, draft.Lines.Sum(x => x.Quantity)); Assert.Equal(4, (await Store(t).DecksAsync(Ct)).Count);
        await Store(t).SaveDeckDraftAsync(draft, Ct); Assert.Equal(202, (await Store(t).SummaryAsync(Ct)).Copies);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(t).PrepareDeckTextAsync("4 Forest", "Ambiguous", "en", Ct));
    }

    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('\t')]
    public void CsvPreservesQuotedMultilineFieldsAndUnknownProperties(char d)
    {
        var data = $"\uFEFFName{d}Quantity{d}Scryfall ID{d}Foil{d}Language{d}Purchase price{d}Binder Name\r\n\"Knight, \"\"one\"\"\nsecond line\"{d}2{d}{Guid.NewGuid()}{d}etched{d}German{d}\"1,25\"{d}Album";
        var row = Assert.Single(ManaBoxCsv.Parse(Encoding.UTF8.GetBytes(data)));
        Assert.Equal("Knight, \"one\"\nsecond line", row.Name); Assert.Equal(2, row.Quantity);
        Assert.Equal("etched", row.Finish); Assert.Equal("de", row.Language); Assert.Contains("1,25", row.Raw); Assert.Equal("Album", row.Binder);
    }
    [Theory]
    [InlineData("Name,Name,Quantity\nx,x,1")]
    [InlineData("Name\nx")]
    [InlineData("Quantity\n-1")]
    [InlineData("Quantity\n1.5")]
    [InlineData("Quantity\n10000")]
    [InlineData("Quantity,Foil\n1,perhaps")]
    [InlineData("Quantity,Binder Type\n1,ambiguous")]
    [InlineData("Quantity,Scryfall ID\n1,not-an-id")]
    public void CsvRejectsAmbiguousOrInvalidData(string csv) => Assert.Throws<ArgumentException>(() => ManaBoxCsv.Parse(Encoding.UTF8.GetBytes(csv)));

    [Fact]
    public async Task ConfluxSeedIsExactIdempotentAndSeparateFromArenaAfterRestart()
    {
        await using var t = Database(); await Seed(t);
        await t.Service.SaveCollectionAsync(new("Arena only", "TEST", "1", 99), Ct);
        await Store(t).InitializeExampleAsync(Ct); await t.ReopenAsync();
        var summary = await Store(t).SummaryAsync(Ct); Assert.Equal(202, summary.Copies); Assert.True(summary.DemoActive); Assert.Equal(4, summary.Decks);
        await using var db = await Factory(t).CreateDbContextAsync(Ct); Assert.Equal(109, await db.PaperPrintings.CountAsync(Ct));
        Assert.Equal(4, await db.PaperHoldings.CountAsync(x => x.Finish == "foil", Ct));
        foreach (var d in await Store(t).DecksAsync(Ct))
        { var report = await Store(t).DeckAsync(d.Id, Ct); Assert.Equal(d.Name.StartsWith("FINAL FANTASY", StringComparison.Ordinal) ? 60 : 41, report.Lines.Sum(x => x.Quantity)); if (!d.Name.StartsWith("FINAL FANTASY", StringComparison.Ordinal)) Assert.Contains(report.Problems, x => x.Contains("41/60")); Assert.DoesNotContain(report.Problems, x => x.Contains("tekort")); }
        Assert.Equal(99, Assert.Single(await t.Service.GetCollectionAsync(Ct)).Quantity);
    }
    [Fact]
    public async Task CsvReviewDoesNotChangePossessionAndApplySumsExactIdentityOnly()
    {
        await using var t = Database(); await Seed(t); var card = (await Store(t).CollectionAsync(ct: Ct)).First(x => x.Card.Finishes.Contains("foil")).Card;
        var path = await Csv(t, $"Scryfall ID,Quantity,Foil,Language,Condition,Binder Name,Binder Type,Proxy\n{card.Id},2,normal,en,near_mint,A,binder,false\n{card.Id},3,normal,en,near_mint,A,binder,false\n{card.Id},1,foil,en,played,A,binder,false\n{card.Id},7,normal,en,near_mint,Plan,list,false\n{card.Id},8,normal,en,near_mint,Proxy,binder,true");
        var p = await Store(t).PrepareCsvAsync(path, Ct); Assert.True(p.CanApply); Assert.Equal(2, p.Rows.Count(x => x.Skipped)); Assert.Equal(202, (await Store(t).SummaryAsync(Ct)).Copies);
        await Store(t).ApplyAsync(p.Id, p.Version, Ct); await Store(t).ApplyAsync(p.Id, p.Version, Ct);
        Assert.Equal(6, (await Store(t).SummaryAsync(Ct)).Copies); Assert.False((await Store(t).SummaryAsync(Ct)).DemoActive);
        await using (var db = await Factory(t).CreateDbContextAsync(Ct))
        { var holdings = await db.PaperHoldings.Where(x => !x.IsDemo).ToArrayAsync(Ct); Assert.Equal(2, holdings.Length); Assert.Equal(5, Assert.Single(holdings, x => x.Finish == "normal").Quantity); Assert.Equal("played", Assert.Single(holdings, x => x.Finish == "foil").Condition); }
        await t.ReopenAsync(); await Store(t).UndoAsync(p.Id, Ct); Assert.Equal(202, (await Store(t).SummaryAsync(Ct)).Copies);
        Assert.Equal(2, (await Store(t).PreviewAsync(p.Id, Ct)).State);
    }
    [Fact]
    public async Task ReimportIsAbsoluteAndBinderIsASeparatePhysicalPosition()
    {
        await using var t = Database(); await Seed(t); var c = (await Store(t).CollectionAsync(ct: Ct))[0].Card;
        var path = await Csv(t, Simple(c.Id)); var a = await Store(t).PrepareCsvAsync(path, Ct); await Store(t).ApplyAsync(a.Id, a.Version, Ct);
        var b = await Store(t).PrepareCsvAsync(path, Ct); await Store(t).ApplyAsync(b.Id, b.Version, Ct); Assert.Equal(3, (await Store(t).SummaryAsync(Ct)).Copies);
        var other = await Store(t).PrepareCsvAsync(await Csv(t, Simple(c.Id, 2).Replace("Scans", "Other")), Ct); await Store(t).ApplyAsync(other.Id, other.Version, Ct);
        Assert.Equal(5, (await Store(t).SummaryAsync(Ct)).Copies);
        await Store(t).UndoAsync(b.Id, Ct); Assert.Equal(5, (await Store(t).SummaryAsync(Ct)).Copies);
        await Store(t).UndoAsync(other.Id, Ct); await Store(t).UndoAsync(a.Id, Ct); Assert.True((await Store(t).SummaryAsync(Ct)).DemoActive);
    }
    [Fact]
    public async Task ConflictingNamesRequireExplicitResolutionAndOldPreviewVersionCannotApply()
    {
        await using var t = Database(); await Seed(t); var c = (await Store(t).CollectionAsync(ct: Ct))[0].Card;
        var p = await Store(t).PrepareCsvAsync(await Csv(t, Simple(c.Id).Replace("Scryfall ID,", "Name,Scryfall ID,").Replace($"\n{c.Id}", $"\nWrong,{c.Id}")), Ct);
        Assert.False(p.CanApply); await Assert.ThrowsAsync<ArgumentException>(() => Store(t).ApplyAsync(p.Id, p.Version, Ct));
        var fixedP = await Store(t).ResolveAsync(p.Id, p.Version, 1, c.Id, false, Ct); Assert.True(fixedP.CanApply); Assert.True(fixedP.Rows[0].Explicit);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(t).ApplyAsync(p.Id, p.Version, Ct)); await Store(t).ApplyAsync(fixedP.Id, fixedP.Version, Ct);
    }
    [Fact]
    public async Task MissingIdentityIsAnUnresolvedRowRatherThanAGuessedEnglishCard()
    {
        await using var t = Database(); await Seed(t);
        var p = await Store(t).PrepareCsvAsync(await Csv(t, "Name,Quantity\nForest,4"), Ct);
        Assert.False(p.CanApply); Assert.Null(p.Rows[0].PrintingId); Assert.Contains("taal", p.Rows[0].Problem);
    }
    [Fact]
    public async Task StockChangedAndChangedBackBlocksOldPreviewAndUndo()
    {
        await using var t = Database(); await Seed(t); var c = (await Store(t).CollectionAsync(ct: Ct))[0].Card;
        var p = await Store(t).PrepareCsvAsync(await Csv(t, Simple(c.Id)), Ct);
        await Store(t).SetOwnedAsync(c.Id, 1, Ct); await Store(t).SetOwnedAsync(c.Id, 0, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(t).ApplyAsync(p.Id, p.Version, Ct));
        p = await Store(t).PrepareCsvAsync(await Csv(t, Simple(c.Id)), Ct); await Store(t).ApplyAsync(p.Id, p.Version, Ct);
        await using (var db = await Factory(t).CreateDbContextAsync(Ct))
        { var row = await db.PaperHoldings.SingleAsync(x => !x.IsDemo && x.Binder == "Scans", Ct); row.SetQuantity(4); row.SetQuantity(3); await db.SaveChangesAsync(Ct); }
        await Assert.ThrowsAsync<ArgumentException>(() => Store(t).UndoAsync(p.Id, Ct)); Assert.Equal(3, (await Store(t).SummaryAsync(Ct)).Copies);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterSqlWritesRollsBackStockAndReceipt(bool cancel)
    {
        var fault = new ApplyFailure(); await using var t = Database(s => s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp => new FaultFactory(sp.GetRequiredService<AppPaths>(), fault)));
        await Seed(t); var c = (await Store(t).CollectionAsync(ct: Ct))[0].Card;
        var p = await Store(t).PrepareCsvAsync(await Csv(t, Simple(c.Id)), Ct); fault.Armed = true;
        using var cancellation = new CancellationTokenSource(); if (cancel) fault.Cancel = cancellation;
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(t).ApplyAsync(p.Id, p.Version, cancellation.Token));
        else await Assert.ThrowsAsync<IOException>(() => Store(t).ApplyAsync(p.Id, p.Version, Ct));
        Assert.True(fault.Fired); Assert.Equal(202, (await Store(t).SummaryAsync(Ct)).Copies); Assert.Equal(0, (await Store(t).PreviewAsync(p.Id, Ct)).State);
        fault.Armed = false; await Store(t).ApplyAsync(p.Id, p.Version, Ct); Assert.Equal(3, (await Store(t).SummaryAsync(Ct)).Copies);
    }
    private sealed class ApplyFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; } public bool Fired { get; private set; } public CancellationTokenSource? Cancel { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken cancellationToken = default)
        { if (Armed && e.Context!.ChangeTracker.Entries<PaperImportPlan>().Any(x => x.Entity.State == 1)) { Fired = true; if (Cancel is not null) Cancel.Cancel(); else throw new IOException("Injected paper import failure after SQL"); } return ValueTask.FromResult(result); }
    }
    private sealed class FaultFactory(AppPaths paths, SaveChangesInterceptor fault) : IDbContextFactory<DeckbuilderDbContext>
    { public DeckbuilderDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DeckbuilderDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString()).AddInterceptors(fault).Options); }

    [Fact]
    public async Task PreviousReleaseUpgradePreservesArenaAndBackupWhileNewTablesStartEmpty()
    {
        await using var t = Database(); Directory.CreateDirectory(t.Paths.DataDirectory);
        await using (var db = await Factory(t).CreateDbContextAsync(Ct))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261023084552_AddDeckGenerationPlans", Ct);
            var c = new Card("Preserved"); var p = new Printing(c.Id, "OLD", "1"); var d = new Deck("Preserved", "Standard");
            db.Cards.Add(c); db.Printings.Add(p); db.CollectionEntries.Add(new(p.Id, 9)); db.Decks.Add(d); db.DeckCards.Add(new(d.Id, p.Id, DeckZone.Mainboard, 2)); await db.SaveChangesAsync(Ct);
        }
        await t.InitializeAsync(); Assert.Equal(9, Assert.Single(await t.Service.GetCollectionAsync(Ct)).Quantity);
        await using var after = await Factory(t).CreateDbContextAsync(Ct); Assert.Equal(9, (await after.Database.GetAppliedMigrationsAsync(Ct)).Count());
        Assert.Empty(await after.PaperHoldings.ToArrayAsync(Ct)); Assert.Empty(await after.PaperDecks.ToArrayAsync(Ct)); Assert.Single(Directory.GetFiles(t.Paths.BackupsDirectory, "*.db"));
    }
    [Fact]
    public async Task BackupRestoreIncludesPhysicalStockDecksAndImportReceipt()
    {
        await using var t = Database(); await Seed(t); var c = (await Store(t).CollectionAsync(ct: Ct))[0].Card;
        var p = await Store(t).PrepareCsvAsync(await Csv(t, Simple(c.Id)), Ct); await Store(t).ApplyAsync(p.Id, p.Version, Ct);
        var backups = t.Provider.GetRequiredService<IBackupService>(); var backup = await backups.CreateAsync(Ct);
        await Store(t).SetOwnedAsync(c.Id, 10, Ct); await Store(t).DeleteDeckAsync((await Store(t).DecksAsync(Ct))[0].Id, Ct);
        var preview = await backups.InspectAsync(backup.Path, Ct); await backups.ScheduleAsync(preview.Id, true, Ct); SqliteConnection.ClearAllPools();
        using (var lease = DataDirectoryLease.Acquire(t.Paths)) await t.Provider.GetRequiredService<SqliteBackupService>().ApplyPendingAsync(lease, Ct);
        await t.ReopenAsync(); Assert.Equal(3, (await Store(t).SummaryAsync(Ct)).Copies); Assert.Equal(4, (await Store(t).DecksAsync(Ct)).Count);
        await Store(t).UndoAsync(p.Id, Ct); Assert.Equal(202, (await Store(t).SummaryAsync(Ct)).Copies);
    }

    [Fact]
    public void PaperRulesAggregateOracleAcrossPrintingsAndZonesWithoutDoubleCountingOwnership()
    {
        var raw = Example()["Cards"]![0]!.ToJsonString(); var c = ScryfallPaperCatalog.Parse(raw, PaperDeckRules.CheckedUtc) with { TypeLine = "Creature", OracleText = "", Standard = "legal" };
        var other = c with { Id = Guid.NewGuid() }; var deck = new PaperDeckInfo(Guid.NewGuid(), "Paper", "casual60", false);
        var report = PaperDeckRules.Evaluate(deck, Guid.NewGuid(), [new(c, DeckZone.Mainboard, 3, 4), new(other, DeckZone.Sideboard, 2, 4)], PaperDeckRules.CheckedUtc);
        Assert.Contains(report.Problems, x => x.Contains("maximaal vier")); Assert.Contains(report.Problems, x => x.Contains("tekort 1"));
    }
    [Fact]
    public void BasicLandsAndPaperLegalityDoNotUseArenaBo1BansButStaleSourcesBlock()
    {
        var raw = Example()["Cards"]![0]!.ToJsonString(); var c = ScryfallPaperCatalog.Parse(raw, PaperDeckRules.CheckedUtc) with { Name = "Leyline of Resonance", TypeLine = "Enchantment", OracleText = "", Standard = "legal" };
        var land = c with { Id = Guid.NewGuid(), OracleId = Guid.NewGuid().ToString(), Name = "Mountain", TypeLine = "Basic Land — Mountain" };
        var deck = new PaperDeckInfo(Guid.NewGuid(), "Paper", "standard", false); PaperDeckLine[] lines = [new(c, DeckZone.Mainboard, 4, 4), new(land, DeckZone.Mainboard, 56, 56)];
        Assert.Empty(PaperDeckRules.Evaluate(deck, Guid.NewGuid(), lines, PaperDeckRules.CheckedUtc).Problems);
        Assert.Contains(PaperDeckRules.Evaluate(deck, Guid.NewGuid(), lines, PaperDeckRules.CheckedUtc.AddDays(2)).Problems, x => x.Contains("niet actueel"));
        Assert.Contains(PaperDeckRules.Evaluate(deck, Guid.NewGuid(), lines, PaperDeckRules.CheckedUtc.AddDays(15)).Problems, x => x.Contains("veertien dagen"));
    }
    [Fact]
    public async Task ScryfallExactIdRejectsDigitalOnlyAndWrongIdentityAndCachesRealPaper()
    {
        var raw = Example()["Cards"]![0]!.DeepClone(); var id = raw["id"]!.GetValue<Guid>();
        var handler = new Handler(_ => new StringContent(raw.ToJsonString()));
        await using var t = Database(s => s.AddSingleton(new HttpClient(handler))); await t.InitializeAsync();
        var catalog = ActivatorUtilities.CreateInstance<ScryfallPaperCatalog>(t.Provider);
        var card = await catalog.GetAsync(id, ct: Ct); await catalog.GetAsync(id, ct: Ct); Assert.Equal(1, handler.Calls); Assert.Equal(id, card.Id);
        raw["id"] = Guid.NewGuid(); await Assert.ThrowsAsync<ArgumentException>(() => catalog.GetAsync(id, true, Ct));
        raw["id"] = id; raw["games"] = new JsonArray("arena"); await Assert.ThrowsAsync<ArgumentException>(() => catalog.GetAsync(id, true, Ct));
    }
    [Fact]
    public async Task ScryfallNaturalIdentityAndImageHostsAreCheckedAndBackFaceCachedSeparately()
    {
        var raw = Example()["Cards"]![0]!.DeepClone(); var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0E8AAAAASUVORK5CYII=");
        var handler = new Handler(uri => uri.Host == "cards.scryfall.io" ? new ByteArrayContent(bytes) : new StringContent(raw.ToJsonString()));
        await using var t = Database(s => s.AddSingleton(new HttpClient(handler))); await t.InitializeAsync(); var catalog = ActivatorUtilities.CreateInstance<ScryfallPaperCatalog>(t.Provider);
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.GetAsync("wrong", "1", "en", Ct));
        var c = await catalog.GetAsync(raw["id"]!.GetValue<Guid>(), true, Ct); c = c with { ImageBack = "https://cards.scryfall.io/normal/back/test.png" };
        var front = await catalog.ImageAsync(c, false, Ct); var back = await catalog.ImageAsync(c, true, Ct); Assert.NotEqual(front, back); Assert.Equal(bytes, await File.ReadAllBytesAsync(front!, Ct));
        var calls = handler.Calls; await catalog.ImageAsync(c, false, Ct); Assert.Equal(calls, handler.Calls);
        foreach (var url in new[] { "http://cards.scryfall.io/x", "https://evil.example/x", "https://cards.scryfall.io:444/x" })
            await Assert.ThrowsAsync<ArgumentException>(() => catalog.ImageAsync(c with { ImageFront = url }, false, Ct));
    }
    private sealed class Handler(Func<Uri, HttpContent> response) : HttpMessageHandler
    { public int Calls { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = response(request.RequestUri!) }); } }

    [AvaloniaFact]
    public async Task PhysicalWorkspaceIsDefaultAndCsvConfirmationAndDeckScreensAreFunctional()
    {
        await using var t = Database(); var shell = t.Provider.GetRequiredService<ShellViewModel>(); var window = new MainWindow { DataContext = shell }; window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null); Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage); Assert.True(shell.IsPaper);
            var vm = Assert.IsType<PaperViewModel>(shell.CurrentPage); Assert.Contains("202", vm.Summary); Assert.Equal(4, vm.Decks.Count);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Capture(window, "paper-collection");
            var card = vm.Collection[0].Card; await shell.NavigateCommand.ExecuteAsync("paper-import"); Assert.Equal(1, vm.SelectedTab);
            await vm.PrepareCsvCommand.ExecuteAsync(await Csv(t, Simple(card.Id))); Assert.NotNull(vm.ImportPreview); Assert.False(vm.CanApply);
            await vm.ApplyImportCommand.ExecuteAsync(null); Assert.True(shell.Operations.HasError); Assert.Equal(202, (await Store(t).SummaryAsync(Ct)).Copies);
            vm.ConfirmImport = true; await vm.ApplyImportCommand.ExecuteAsync(null); Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage); Assert.Contains("3 fysieke", vm.Summary);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Capture(window, "paper-csv");
            await shell.NavigateCommand.ExecuteAsync("paper-decks"); await vm.LoadDeckCommand.ExecuteAsync(null); Assert.Equal(2, vm.SelectedTab); Assert.Contains("41 mainboard", vm.DeckSummary); Assert.Contains("tekort", vm.DeckDetails);
            await vm.ExportDeckCommand.ExecuteAsync(null); Assert.Contains("Deck", vm.ExportText); Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Capture(window, "paper-decks"); Assert.NotNull(window.GetVisualDescendants().OfType<Image>().SingleOrDefault(x => x.Name == "PaperCardImage"));
            await shell.NavigateCommand.ExecuteAsync("dashboard"); Assert.Equal("paper-decks",shell.CurrentKey); Assert.True(shell.IsPaper);
        }
        finally { window.Close(); }
    }
    private static void Capture(Window w, string name)
    { var path = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS"); if (string.IsNullOrEmpty(path)) return; Directory.CreateDirectory(path); using var frame = w.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(path, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); }
    [AvaloniaFact]
    public async Task CardImageIsDecodedAndDisplayedBesideTheSelectedPhysicalPrinting()
    {
        string file = "";
        await using var t = Database(s => s.AddSingleton<IPaperCatalog>(sp => new ImageCatalog(ActivatorUtilities.CreateInstance<ScryfallPaperCatalog>(sp), () => file)));
        await t.InitializeAsync(); file = Environment.GetEnvironmentVariable("MTGDECKBUILDER_REAL_IMAGE") ?? Path.Combine(t.Paths.DataDirectory, "test-image.png");
        if (!File.Exists(file))
        { using var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(new Avalonia.PixelSize(8,8), new Avalonia.Vector(96,96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque); bitmap.Save(file, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); }
        var shell = t.Provider.GetRequiredService<ShellViewModel>(); var window = new MainWindow { DataContext = shell }; window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null); var vm = Assert.IsType<PaperViewModel>(shell.CurrentPage);
            vm.SelectedOwned = Environment.GetEnvironmentVariable("MTGDECKBUILDER_REAL_IMAGE") is not null
                ? vm.Collection.Single(x => Path.GetFileName(file).StartsWith(x.Card.Id.ToString("N"), StringComparison.OrdinalIgnoreCase))
                : vm.Collection.Single(x => x.Card.Name == "Aerie Mystics");
            Assert.NotNull(vm.ImageLoadTask); await vm.ImageLoadTask; Assert.NotNull(vm.CardImage); Assert.Equal(vm.SelectedOwned.Card.Id, vm.SelectedCard!.Id);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var image = Assert.Single(window.GetVisualDescendants().OfType<Image>(), x => x.Name == "PaperCardImage"); Assert.Same(vm.CardImage, image.Source);
            var point = image.TranslatePoint(new Avalonia.Point(0,0), window); Assert.NotNull(point); Assert.InRange(point.Value.X + image.Bounds.Width, 0, window.Width); Capture(window, "paper-scryfall-image");
            window.Width = 1050; Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); point = image.TranslatePoint(new Avalonia.Point(0,0), window); Assert.NotNull(point); Assert.InRange(point.Value.X + image.Bounds.Width, 0, window.Width); Capture(window, "paper-compact");
        }
        finally { window.Close(); }
    }
    private sealed class ImageCatalog(IPaperCatalog inner, Func<string> file) : IPaperCatalog
    {
        public Task<PaperCard> GetAsync(Guid id, bool refresh = false, CancellationToken ct = default) => inner.GetAsync(id, refresh, ct);
        public Task<PaperCard> GetAsync(string set, string number, string language, CancellationToken ct = default) => inner.GetAsync(set, number, language, ct);
        public Task<IReadOnlyList<PaperCard>> SearchAsync(string query, CancellationToken ct = default) => inner.SearchAsync(query, ct);
        public Task<IReadOnlyList<PaperCard>> CommanderPoolAsync(string colors, CancellationToken ct = default) => inner.CommanderPoolAsync(colors, ct);
        public Task<IReadOnlyList<PaperCard>> WizardPoolAsync(DeckWizardRequest request, CancellationToken ct = default) => inner.WizardPoolAsync(request, ct);
        public Task<string?> ImageAsync(PaperCard card, bool back, CancellationToken ct = default) => Task.FromResult<string?>(back ? null : file());
    }
}
