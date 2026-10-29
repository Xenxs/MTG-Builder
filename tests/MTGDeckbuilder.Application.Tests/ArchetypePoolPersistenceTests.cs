using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class ArchetypePoolPersistenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static DeckValidationPersistenceTests.BulkHandler Handler() => new()
    {
        Cards = MonoRedPool.Current.Cards.Select((entry,index) =>
        {
            var card=DeckValidationPersistenceTests.LegalCard(entry.Name,(index+1).ToString(),entry.OracleId);
            card["mana_cost"]=entry.ManaCost;card["type_line"]=entry.TypeLine;card["oracle_text"]=entry.OracleText;
            card["color_identity"]=entry.ColorIdentity.Select(x=>x.ToString()).ToArray();card["cmc"]=entry.ManaValue;
            return card;
        }).ToArray()
    };
    internal static async Task Seed(TestDatabase test)
    {
        await test.InitializeAsync();await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct:TestContext.Current.CancellationToken);
        var catalog=test.Provider.GetRequiredService<CatalogService>();
        var page=await catalog.SearchAsync(new(PageSize:100),TestContext.Current.CancellationToken);
        foreach(var name in new[]{"Emberheart Challenger","Fugitive Codebreaker","Coruscation Mage","Mountain"})
            await catalog.SetOwnedQuantityAsync(page.Items.Single(x=>x.Name==name).PrintingId,name=="Mountain"?60:2,TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task RealSqlitePoolReadSurvivesRestartAndNeverReservesOrChangesOwnershipOrDecks()
    {
        await using var test=DeckValidationPersistenceTests.Database(Handler());await Seed(test);
        var catalog=test.Provider.GetRequiredService<CatalogService>();var land=(await catalog.SearchAsync(new(Search:"Mountain"),Ct)).Items.Single();
        var deck=await test.Service.CreateDeckAsync("Existing deck",DeckRuleProfiles.Bo1,Ct);await test.Service.SetDeckCardAsync(deck,land.PrintingId,DeckZone.Mainboard,60,Ct);
        var before=await test.Service.GetCollectionAsync(Ct);var beforeLines=await test.Service.GetDeckCardsAsync(deck,Ct);
        var report=await test.Provider.GetRequiredService<IArchetypePoolStore>().ReadAsync(Ct);
        Assert.Equal(27,report.Cards.Count(x=>x.Eligible));Assert.Equal(60,report.Cards.Single(x=>x.Entry.Name=="Mountain").OwnedEligible);
        var original=report.Cards.Single(x=>x.Entry.Name=="Slickshot Show-Off");
        Assert.Equal(new[]{"Emberheart Challenger","Fugitive Codebreaker"},ArchetypePoolEngine.Alternatives(report,original.Entry.OracleId).Select(x=>x.Name));
        await test.ReopenAsync();var after=await test.Provider.GetRequiredService<IArchetypePoolStore>().ReadAsync(Ct);
        Assert.Equal(report.Cards.Select(x=>x.Label),after.Cards.Select(x=>x.Label));
        Assert.Equal(before,await test.Service.GetCollectionAsync(Ct));Assert.Equal(beforeLines,await test.Service.GetDeckCardsAsync(deck,Ct));
        Assert.Single(await test.Service.GetDecksAsync(Ct));
    }
    [Fact]
    public async Task CatalogRefreshAndOwnershipChangesRevokePoolAdviceWithoutDeletingData()
    {
        var handler=Handler();await using var test=DeckValidationPersistenceTests.Database(handler);await Seed(test);
        var store=test.Provider.GetRequiredService<IArchetypePoolStore>();var before=await store.ReadAsync(Ct);var original=before.Cards.Single(x=>x.Entry.Name=="Slickshot Show-Off");
        Assert.Equal(2,ArchetypePoolEngine.Alternatives(before,original.Entry.OracleId).Count);
        handler.Cards.Single(x=>(string)x["name"]! == "Emberheart Challenger")["oracle_text"]="Changed Oracle text.";
        handler.Cards.Single(x=>(string)x["name"]! == "Fugitive Codebreaker")["legalities"]=new Dictionary<string,string>{{"standard","banned"}};
        handler.Version++;await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct:Ct);
        var after=await store.ReadAsync(Ct);Assert.Empty(ArchetypePoolEngine.Alternatives(after,original.Entry.OracleId));
        Assert.Contains("Metadata gewijzigd",after.Cards.Single(x=>x.Entry.Name=="Emberheart Challenger").Status);
        Assert.Equal(2,after.Cards.Single(x=>x.Entry.Name=="Emberheart Challenger").OwnedTotal);
        Assert.Equal(2,after.Cards.Single(x=>x.Entry.Name=="Fugitive Codebreaker").OwnedTotal);
        Assert.Equal(0,after.Cards.Single(x=>x.Entry.Name=="Fugitive Codebreaker").OwnedEligible);
    }
    [Fact]
    public async Task EmptyCatalogAndCancellationDoNotFabricateEligibility()
    {
        await using var test=new TestDatabase();await test.InitializeAsync();var store=test.Provider.GetRequiredService<IArchetypePoolStore>();
        var report=await store.ReadAsync(Ct);Assert.Equal(30,report.Cards.Count);Assert.DoesNotContain(report.Cards,x=>x.Eligible);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.ReadAsync(cancellation.Token));
        Assert.Empty(await test.Service.GetCollectionAsync(Ct));Assert.Empty(await test.Service.GetDecksAsync(Ct));
    }
}
