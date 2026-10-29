using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CommanderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime Now = PaperDeckRules.CheckedUtc.AddHours(23);
    private static IPaperWorkspace Store(TestDatabase t) => t.Provider.GetRequiredService<IPaperWorkspace>();
    private static IDbContextFactory<DeckbuilderDbContext> Factory(TestDatabase t) => t.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
    private static PaperCard Card(string name, string type, string colors = "RW", double mv = 2) =>
        ScryfallPaperCatalog.Parse(PaperWorkspaceTests.Example()["Cards"]![0]!.ToJsonString(), Now) with
        { Id = Guid.NewGuid(), OracleId = Guid.NewGuid().ToString(), Name = name, TypeLine = type, OracleText = "", Colors = colors, ManaCost = colors == "R" ? "{1}{R}" : "{1}{W}", ManaValue = mv, CommanderLegality = "legal", Standard = "not_legal", FetchedUtc = Now };
    private static (PaperCard Leader, PaperCard[] Cards, PaperOwned[] Stock) Fixture()
    {
        var leader = Card("Fixture Commander", "Legendary Creature — Human");
        var spells = Enumerable.Range(0,62).Select(i => Card("Fixture " + i.ToString("D2"), "Creature — Soldier", i % 2 == 0 ? "R" : "W", 1 + i % 7)).ToArray();
        var plains = Card("Plains", "Basic Land — Plains", "W", 0); var mountain = Card("Mountain", "Basic Land — Mountain", "R", 0);
        var known = new[] { leader }.Concat(spells).Concat([plains, mountain]).ToArray();
        return (leader, known, known.Select(c => new PaperOwned(c, c.Id == plains.Id ? 20 : c.Id == mountain.Id ? 17 : 1)).ToArray());
    }
    [Fact]
    public void OwnedCommanderIsExactly100WithSingletonAndExactPrintingStock()
    {
        var f = Fixture(); var rows = CommanderDeckPlanner.Create(f.Leader, f.Cards, f.Stock, false, Now);
        Assert.Equal(100, rows.Sum(x => x.Quantity)); Assert.Equal(1, Assert.Single(rows, x => x.Zone == DeckZone.Commander).Quantity);
        Assert.Equal(99, rows.Where(x => x.Zone == DeckZone.Mainboard).Sum(x => x.Quantity)); Assert.Equal(37, rows.Where(x => x.Card.IsBasicLand).Sum(x => x.Quantity));
        foreach (var row in rows) Assert.InRange(row.Quantity, 1, f.Stock.Single(x => x.Card.Id == row.Card.Id).Quantity);
        Assert.All(rows.Where(x => !x.Card.IsBasicLand).GroupBy(x => x.Card.OracleId), g => Assert.Equal(1,g.Sum(x => x.Quantity)));
        Assert.Empty(PaperDeckRules.Evaluate(new(Guid.NewGuid(), "Commander", "commander", false), Guid.NewGuid(), rows, Now).Problems);
        Assert.Equal(rows, CommanderDeckPlanner.Create(f.Leader, f.Cards, f.Stock, false, Now));
    }
    [Fact]
    public void ReprintsDoNotCreateExtraSingletonSlotsAndBasicSnowLandsAreUnlimited()
    {
        var f = Fixture(); var duplicate = f.Cards[1] with { Id = Guid.NewGuid() };
        var known = f.Cards.Where(c => c.Id != f.Cards[2].Id).Append(duplicate).ToArray();
        Assert.Throws<ArgumentException>(() => CommanderDeckPlanner.Create(f.Leader, known, f.Stock, true, Now));
        var snow = Card("Snow-Covered Plains", "Basic Snow Land — Plains", "W", 0);
        var rows = new PaperDeckLine[] { new(f.Leader, DeckZone.Commander, 1, 1), new(snow, DeckZone.Mainboard, 99, 99) };
        Assert.Empty(PaperDeckRules.Evaluate(new(Guid.NewGuid(), "Structure only", "commander", false), Guid.NewGuid(), rows, Now).Problems);
    }
    [Fact]
    public void IllegalOffColorStaleAndDeckExceptionCardsCannotFillTheGenerator()
    {
        var f = Fixture(); var off = Card("Blue card", "Creature", "U"); var banned = Card("Banned", "Creature") with { CommanderLegality = "banned" };
        var stale = Card("Old source", "Creature") with { FetchedUtc = Now.AddDays(-2) };
        var exception = Card("Exception", "Creature") with { OracleText = "A deck can have any number of cards named Exception." };
        var extra = new[] { off, banned, stale, exception }; var rows = CommanderDeckPlanner.Create(f.Leader, f.Cards.Concat(extra).ToArray(), f.Stock, true, Now);
        Assert.DoesNotContain(rows, x => extra.Any(c => c.Id == x.Card.Id));
        Assert.Throws<ArgumentException>(() => CommanderDeckPlanner.Create(f.Leader with { CommanderLegality = "banned" }, f.Cards, f.Stock, true, Now));
        Assert.Throws<ArgumentException>(() => CommanderDeckPlanner.Create(f.Leader, f.Cards, f.Stock, true, Now.AddDays(15)));
    }
    [Theory]
    [InlineData("Legendary Creature — Human", false, true)]
    [InlineData("Creature — Human", true, false)]
    [InlineData("Legendary Artifact — Vehicle", false, true)]
    [InlineData("Legendary Artifact — Spacecraft", false, false)]
    [InlineData("Legendary Artifact — Spacecraft", true, true)]
    [InlineData("Legendary Artifact", true, false)]
    [InlineData("Sorcery // Legendary Creature — Human", true, false)]
    public void CommanderEligibilityUsesCurrentFrontFaceRules(string type, bool stats, bool expected)
        => Assert.Equal(expected, (Card("Candidate", type) with { HasPowerToughness = stats }).IsCommanderCandidate);
    [Fact]
    public void ExplicitCommanderExceptionWorksButVariableColorChoiceIsNotSilentlyAssumed()
    {
        var c = Card("Planeswalker", "Legendary Planeswalker") with { OracleText = "This card can be your commander." }; Assert.True(c.IsCommanderCandidate);
        c = Card("Variable", "Legendary Creature") with { OracleText = "You choose a color before the game begins." }; Assert.False(c.IsCommanderCandidate);
    }
    [Fact]
    public void CommanderRulesCheckWholeColorIdentityLandTypesZonesAndReprintDuplicates()
    {
        var f = Fixture(); var blueBack = Card("Red front // blue back", "Creature // Creature", "RU");
        var badLand = Card("Island with incomplete identity", "Basic Land — Island", "", 0); Assert.False(PaperDeckRules.ColorsFit(badLand, f.Leader));
        var rows = new List<PaperDeckLine> { new(f.Leader, DeckZone.Commander,1,1), new(blueBack,DeckZone.Mainboard,1,1),new(badLand,DeckZone.Mainboard,97,97),new(blueBack with { Id=Guid.NewGuid() },DeckZone.Mainboard,1,1) };
        var report = PaperDeckRules.Evaluate(new(Guid.NewGuid(),"Invalid", "commander",false),Guid.NewGuid(),rows,Now);
        Assert.Contains(report.Problems,x=>x.Contains("kleuridentiteit")); Assert.Contains(report.Problems,x=>x.Contains("singleton")); Assert.Contains(report.Problems,x=>x.Contains("tekort"));
        rows.Add(new(f.Leader, DeckZone.Sideboard,1,1)); report=PaperDeckRules.Evaluate(report.Deck,Guid.NewGuid(),rows,Now);
        Assert.Contains(report.Problems,x=>x.Contains("sideboard")); Assert.Contains(report.Problems,x=>x.Contains("exact 100"));
        rows.Add(new(f.Leader,DeckZone.Commander,1,1)); Assert.Contains(PaperDeckRules.Evaluate(report.Deck,Guid.NewGuid(),rows,Now).Problems,x=>x.Contains("precies één"));
    }
    [Fact]
    public void PlanningNeverPretendsThatUnknownOrMissingStockIsOwned()
    {
        var f = Fixture(); Assert.Throws<ArgumentException>(()=>CommanderDeckPlanner.Create(f.Leader,f.Cards,[],false,Now));
        var rows=CommanderDeckPlanner.Create(f.Leader,f.Cards,[],true,Now); Assert.Equal(100,rows.Sum(x=>x.Quantity)); Assert.All(rows,x=>Assert.Equal(0,x.Owned));
        Assert.Contains(PaperDeckRules.Evaluate(new(Guid.NewGuid(),"Planning","commander",false),Guid.NewGuid(),rows,Now).Problems,x=>x.Contains("tekort"));
    }
    [Fact]
    public void ColorlessCommanderUsesWastesWithoutInventingColoredIdentity()
    {
        var f=Fixture();var leader=f.Leader with {Colors="",ManaCost="{6}"};var spells=f.Cards.Where(x=>!x.IsBasicLand&&x.Id!=f.Leader.Id).Select(c=>c with {Colors="",ManaCost="{2}"}).ToArray();var wastes=Card("Wastes","Basic Land","",0);
        var known=new[]{leader}.Concat(spells).Append(wastes).ToArray();var rows=CommanderDeckPlanner.Create(leader,known,[],true,Now);
        Assert.Equal(100,rows.Sum(x=>x.Quantity));Assert.Equal(37,Assert.Single(rows,x=>x.Card.Id==wastes.Id).Quantity);Assert.All(rows,x=>Assert.Empty(x.Card.Colors));
    }
    [Fact]
    public void EquipmentHintRecognizesEquippedTextWithoutClaimingCompleteSynergy()
    {
        var f=Fixture();var leader=f.Leader with {OracleText="As long as this commander is equipped, it has double strike."};var equipment=Card("Equipment example","Artifact — Equipment","",8);
        var known=f.Cards.Where(c=>c.Id!=f.Leader.Id).Append(leader).Append(equipment).ToArray();var rows=CommanderDeckPlanner.Create(leader,known,[],true,Now);
        Assert.Contains(rows,x=>x.Card.Id==equipment.Id);Assert.Equal(100,rows.Sum(x=>x.Quantity));
    }
    private sealed class FrozenClock : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(Now); }
    private sealed class PoolHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        private readonly JsonObject[] _pool;
        public PoolHandler()
        {
            var f=Fixture(); _pool=f.Cards.Where(x=>x.Id!=f.Leader.Id).Select(c=>
            {
                var raw=PaperWorkspaceTests.Example()["Cards"]![0]!.DeepClone().AsObject(); raw["id"]=c.Id;raw["oracle_id"]=c.OracleId;raw["name"]=c.Name;
                raw["type_line"]=c.TypeLine;raw["oracle_text"]=c.OracleText;raw["mana_cost"]=c.ManaCost;raw["cmc"]=c.ManaValue;raw["color_identity"]=new JsonArray(c.Colors.Select(v=>(JsonNode?)JsonValue.Create(v.ToString())).ToArray());
                raw["legalities"]!["commander"]="legal";return raw;
            }).ToArray();
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Requests++; var query=Uri.UnescapeDataString(request.RequestUri!.Query);
            if (!request.RequestUri.AbsolutePath.EndsWith("/search",StringComparison.Ordinal)) throw new InvalidOperationException("Fixture expected only scoped Commander search.");
            var basic=query.Contains(" t:basic",StringComparison.Ordinal); var items=_pool.Where(c=>c["type_line"]!.GetValue<string>().Contains("Land",StringComparison.Ordinal)==basic);
            if(!basic) items=items.Where(c=>query.Contains("cmc<=3",StringComparison.Ordinal)?c["cmc"]!.GetValue<double>()<=3:query.Contains("cmc<=5",StringComparison.Ordinal)?c["cmc"]!.GetValue<double>()>3&&c["cmc"]!.GetValue<double>()<=5:c["cmc"]!.GetValue<double>()>5);
            var payload=new JsonObject { ["data"]=new JsonArray(items.Select(x=>(JsonNode?)x.DeepClone()).ToArray()) };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(payload.ToJsonString())});
        }
    }
    private static TestDatabase Database(PoolHandler? handler=null)=>new(s=>{s.AddSingleton<TimeProvider>(new FrozenClock());if(handler is not null)s.AddSingleton(new HttpClient(handler));});
    [Fact]
    public async Task CommanderPlanningFetchesScopedPaperPoolAndPersists100WithoutChanging202OwnedCards()
    {
        var handler=new PoolHandler(); await using var t=Database(handler); await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);
        var leader=(await Store(t).CollectionAsync(ct:Ct)).Single(x=>x.Card.Name=="Cloud, Planet's Champion").Card;
        await Assert.ThrowsAsync<ArgumentException>(()=>Store(t).PrepareCommanderAsync("Owned",leader.Id,false,Ct)); Assert.Equal(0,handler.Requests);
        var draft=await Store(t).PrepareCommanderAsync("Commander planning",leader.Id,true,Ct);Assert.Equal(4,handler.Requests);Assert.Equal("commander",draft.Profile);Assert.Equal(100,draft.Lines.Sum(x=>x.Quantity));
        Assert.Contains(draft.Report.Problems,x=>x.Contains("tekort"));Assert.DoesNotContain(draft.Report.Problems,x=>!x.Contains("tekort"));
        var id=await Store(t).SaveDeckDraftAsync(draft,Ct);Assert.Equal(id,await Store(t).SaveDeckDraftAsync(draft,Ct)); await t.ReopenAsync();
        var saved=await Store(t).DeckAsync(id,Ct);Assert.Equal("commander",saved.Deck.Profile);Assert.Equal(100,saved.Lines.Sum(x=>x.Quantity));Assert.Equal(202,(await Store(t).SummaryAsync(Ct)).Copies);
        Assert.Contains("Commander",await Store(t).ExportDeckAsync(id,Ct));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedCommanderCannotSaveAfterStockOrMetadataChanges(bool metadata)
    {
        await using var t=Database(new PoolHandler());await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);var leader=(await Store(t).CollectionAsync(ct:Ct)).Single(x=>x.Card.Name=="Cloud, Planet's Champion").Card;
        var draft=await Store(t).PrepareCommanderAsync("Review",leader.Id,true,Ct);
        if(metadata)
        {
            await using var db=await Factory(t).CreateDbContextAsync(Ct);var printing=await db.PaperPrintings.SingleAsync(x=>x.Id==leader.Id,Ct);var raw=JsonNode.Parse(printing.MetadataJson)!;raw["oracle_text"]="Changed after review";printing.Update(raw.ToJsonString(),printing.FetchedUtc);await db.SaveChangesAsync(Ct);
        }
        else await Store(t).SetOwnedAsync(leader.Id,0,Ct);
        await Assert.ThrowsAsync<ArgumentException>(()=>Store(t).SaveDeckDraftAsync(draft,Ct));Assert.Equal(4,(await Store(t).DecksAsync(Ct)).Count);
    }
    [Fact]
    public async Task FullStarterCsvAndSeedMarkersAreIdempotentAndKeepTokensOutOfDeckCounts()
    {
        await using var t=Database();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);await Store(t).InitializeExampleAsync(Ct);
        Assert.Equal(202,(await Store(t).SummaryAsync(Ct)).Copies);Assert.Empty(await Store(t).HistoryAsync(Ct));
        var draft=await Store(t).PrepareCsvAsync(Path.Combine(AppContext.BaseDirectory,"manabox-physical-starters.csv"),Ct);Assert.True(draft.CanApply,draft.Summary);Assert.Equal(118,draft.Rows.Count);Assert.Equal(202,draft.Rows.Sum(x=>x.Source.Quantity));
        await Store(t).ApplyAsync(draft.Id,draft.Version,Ct);await t.ReopenAsync();Assert.False((await Store(t).SummaryAsync(Ct)).DemoActive);Assert.Equal(202,(await Store(t).SummaryAsync(Ct)).Copies);
        await using var db=await Factory(t).CreateDbContextAsync(Ct);var marker=await db.PaperImports.SingleAsync(x=>x.SourceHash=="final-fantasy-starter-v1",Ct);
        await Assert.ThrowsAsync<ArgumentException>(()=>Store(t).UndoAsync(marker.Id,Ct));
    }
    [Fact]
    public async Task AddingStarterKitToOlderConfluxWorkspaceDoesNotChangeAnyExistingHolding()
    {
        await using var t=Database();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);var card=(await Store(t).CollectionAsync(ct:Ct))[0].Card;await Store(t).SetOwnedAsync(card.Id,7,Ct);
        await using(var db=await Factory(t).CreateDbContextAsync(Ct))
        {
            await db.PaperHoldings.Where(x=>x.IsDemo&&x.Binder.StartsWith("FINAL FANTASY")).ExecuteDeleteAsync(Ct);
            await db.PaperDecks.Where(x=>x.IsDemo&&x.Name.StartsWith("FINAL FANTASY")).ExecuteDeleteAsync(Ct);
            await db.PaperImports.Where(x=>x.SourceHash=="final-fantasy-starter-v1").ExecuteDeleteAsync(Ct);
            var changed=await db.PaperHoldings.FirstAsync(x=>x.IsDemo,Ct);changed.SetQuantity(77);await db.SaveChangesAsync(Ct);
        }
        Dictionary<Guid,string> before;
        await using(var db=await Factory(t).CreateDbContextAsync(Ct))before=(await db.PaperHoldings.AsNoTracking().ToArrayAsync(Ct)).ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
        await Store(t).InitializeExampleAsync(Ct);await Store(t).InitializeExampleAsync(Ct);
        await using(var db=await Factory(t).CreateDbContextAsync(Ct))
        { foreach(var (id,json) in before)Assert.Equal(json,JsonSerializer.Serialize(await db.PaperHoldings.AsNoTracking().SingleAsync(x=>x.Id==id,Ct)));Assert.Equal(before.Count+55,await db.PaperHoldings.CountAsync(Ct)); }
        Assert.Equal(7,(await Store(t).SummaryAsync(Ct)).Copies);Assert.Equal(4,(await Store(t).DecksAsync(Ct)).Count);
    }
    private sealed class MovingClock : TimeProvider { public int Days {get;set;} public override DateTimeOffset GetUtcNow()=>new(Now.AddDays(Days)); }
    [Fact]
    public async Task CommanderSourceExpiryBetweenReviewAndSaveBlocksCommit()
    {
        var clock=new MovingClock();await using var t=new TestDatabase(s=>{s.AddSingleton<TimeProvider>(clock);s.AddSingleton(new HttpClient(new PoolHandler()));});await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);
        var commander=(await Store(t).CollectionAsync(ct:Ct)).Single(x=>x.Card.Name=="Cloud, Planet's Champion").Card;var draft=await Store(t).PrepareCommanderAsync("Expiry",commander.Id,true,Ct);clock.Days=2;
        await Assert.ThrowsAsync<ArgumentException>(()=>Store(t).SaveDeckDraftAsync(draft,Ct));Assert.Equal(4,(await Store(t).DecksAsync(Ct)).Count);
    }
    [AvaloniaFact]
    public async Task CommanderUiKeepsPlanningExplicitAndRequiresReviewBeforeNewDeckStorage()
    {
        await using var t=Database(new PoolHandler());var shell=t.Provider.GetRequiredService<ShellViewModel>();var window=new MainWindow {DataContext=shell};window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null);await shell.NavigateCommand.ExecuteAsync("paper-decks");var vm=Assert.IsType<PaperViewModel>(shell.CurrentPage);
            Assert.Equal("Cloud, Planet's Champion",vm.SelectedCommander!.Name);Assert.False(vm.PlanningProposal);
            await vm.PrepareCommanderCommand.ExecuteAsync(null);Assert.True(shell.Operations.HasError);Assert.Null(vm.DeckDraft);
            vm.PlanningProposal=true;await vm.PrepareCommanderCommand.ExecuteAsync(null);Assert.False(shell.Operations.HasError,shell.Operations.ErrorMessage);Assert.NotNull(vm.DeckDraft);Assert.Equal(100,vm.DeckDraft.Lines.Sum(x=>x.Quantity));
            await vm.SaveDeckDraftCommand.ExecuteAsync(null);Assert.True(shell.Operations.HasError);Assert.Equal(4,(await Store(t).DecksAsync(Ct)).Count);
            vm.ConfirmDeckDraft=true;await vm.SaveDeckDraftCommand.ExecuteAsync(null);Assert.False(shell.Operations.HasError,shell.Operations.ErrorMessage);Assert.True(vm.CommanderProfile);Assert.False(vm.StandardProfile);Assert.Contains("1 commander",vm.DeckSummary);
            Dispatcher.UIThread.RunJobs();window.UpdateLayout();var path=Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(path);using var image=window.CaptureRenderedFrame();Assert.NotNull(image);image.Save(Path.Combine(path,"paper-commander.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        }
        finally{window.Close();}
    }
}
