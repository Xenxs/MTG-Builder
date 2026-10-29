using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class PaperSourceBatchTests
{
    private sealed class Clock:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(PaperDeckRules.CheckedUtc.AddHours(23)); }
    private sealed class Catalog(IDbContextFactory<DeckbuilderDbContext> factory,TimeProvider clock):IPaperCatalog
    {
        public List<(Guid Id,bool Refresh)> Calls{get;}=[];
        public async Task<PaperCard> GetAsync(Guid id,bool refresh=false,CancellationToken ct=default)
        {
            Calls.Add((id,refresh));await using var db=await factory.CreateDbContextAsync(ct);var p=await db.PaperPrintings.SingleAsync(x=>x.Id==id,ct);p.Update(p.MetadataJson,clock.GetUtcNow().UtcDateTime);await db.SaveChangesAsync(ct);return ScryfallPaperCatalog.Parse(p.MetadataJson,p.FetchedUtc);
        }
        public Task<PaperCard> GetAsync(string set,string number,string language,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<IReadOnlyList<PaperCard>> SearchAsync(string query,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<IReadOnlyList<PaperCard>> CommanderPoolAsync(string colors,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<IReadOnlyList<PaperCard>> WizardPoolAsync(DeckWizardRequest request,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<string?> ImageAsync(PaperCard card,bool back,CancellationToken ct=default)=>Task.FromResult<string?>(null);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-48)]
    [InlineData(1)]
    public async Task OnlyStaleOrImplausiblyFutureOwnedMetadataUsesIndividualRefresh(int offsetHours)
    {
        var ct=TestContext.Current.CancellationToken;var clock=new Clock();await using var test=new TestDatabase(s=>{s.AddSingleton<TimeProvider>(clock);s.AddSingleton<Catalog>();s.AddSingleton<IPaperCatalog>(p=>p.GetRequiredService<Catalog>());});
        await test.InitializeAsync();var store=test.Provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync(TestContext.Current.CancellationToken);
        var id=(await store.CollectionAsync(ct:ct)).First(c=>c.Card.Name=="Adelbert Steiner").Card.Id;
        if(offsetHours!=0){await using var db=await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(ct);var row=await db.PaperPrintings.SingleAsync(x=>x.Id==id,ct);row.Update(row.MetadataJson,clock.GetUtcNow().UtcDateTime.AddHours(offsetHours));await db.SaveChangesAsync(ct);}
        var draft=await store.PrepareWizardAsync(new("Batch source probe","standard","selected","WR","equipment","balanced",false),ct);Assert.Equal(60,draft.Lines.Sum(x=>x.Quantity));Assert.Empty(draft.Report.Problems);
        var catalog=test.Provider.GetRequiredService<Catalog>();if(offsetHours==0)Assert.Empty(catalog.Calls);else Assert.Equal((id,true),Assert.Single(catalog.Calls));
    }
}
