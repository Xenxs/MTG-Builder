using System.Text.Json;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class ArchetypePoolTests
{
    internal static readonly DateTime Now = new(2026, 10, 23, 7, 0, 0, DateTimeKind.Utc);
    internal static AdvicePrinting Printing(ArchetypePoolEntry entry) => new(Guid.NewGuid(), entry.OracleId, entry.Name, "POOL", "1",
        entry.ColorIdentity, entry.ManaValue, entry.TypeLine, entry.OracleText, CardLegality.Legal, true, entry.ManaCost);
    internal static ArchetypePoolInput Input()
    {
        var cards = MonoRedPool.Current.Cards.Select(Printing).ToArray();
        return new(new(Guid.NewGuid(), "Fixed test source", "pool-test", Now.AddHours(-1), Now.AddMinutes(-30), 1), cards,
            cards.Select(x => new AdviceStock(x.Id, 2, Guid.NewGuid())).ToArray());
    }
    [Fact]
    public void ReviewedPoolIncludesTwentySixNonlandsMountainAndExplicitExclusions()
    {
        var pool = MonoRedPool.Current; var report = ArchetypePoolEngine.Evaluate(pool, Input(), Now);
        Assert.Equal(30, pool.Cards.Count); Assert.Equal(26, pool.Cards.Count(x=>x.Included && x.Slot != AggroSlot.Land));
        Assert.Single(pool.Cards, x=>x.Included && x.Slot == AggroSlot.Land && x.Name == "Mountain");
        Assert.Equal(27, report.Cards.Count(x=>x.Eligible)); Assert.Equal(3, report.Cards.Count(x=>!x.Entry.Included));
        Assert.All(pool.Cards, x=> { Assert.Equal(64, x.MetadataSha256.Length); Assert.NotEmpty(x.Cautions); Assert.NotEmpty(x.Purpose); });
        Assert.Contains("Wizards-banlijst", report.Cards.Single(x=>x.Entry.Name=="Leyline of Resonance").Status, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("name")][InlineData("text")][InlineData("cost")][InlineData("type")][InlineData("color")][InlineData("mana")]
    public void EachReviewedMetadataFieldMustStillMatchExactly(string field)
    {
        var input = Input(); var original = input.Printings.Single(x=>x.Name=="Shock");
        var changed = field switch
        {
            "name" => original with {Name="SHOCK"}, "text"=>original with {OracleText=original.OracleText+" Extra effect."},
            "cost"=>original with {ManaCost="{2}{R}"}, "type"=>original with {TypeLine="Sorcery"},
            "color"=>original with {ColorIdentity="RU"}, _=>original with {ManaValue=4}
        };
        var report = ArchetypePoolEngine.Evaluate(MonoRedPool.Current, input with {Printings=input.Printings.Select(x=>x.Id==original.Id?changed:x).ToArray()}, Now);
        var row = report.Cards.Single(x=>x.Entry.Name=="Shock");
        Assert.False(row.Eligible); Assert.Equal(0,row.OwnedEligible); Assert.Contains("Metadata gewijzigd",row.Status);
        Assert.Empty(ArchetypePoolEngine.Alternatives(report,row.Entry.OracleId));
    }
    [Fact]
    public void NameAloneNeverLinksAnUnverifiedOracleIdentity()
    {
        var input=Input(); var original=input.Printings.Single(x=>x.Name=="Shock");
        var report=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,input with {Printings=input.Printings.Select(x=>x.Id==original.Id?x with{OracleId=Guid.NewGuid().ToString()}:x).ToArray()},Now);
        var row=report.Cards.Single(x=>x.Entry.Name=="Shock"); Assert.False(row.Eligible); Assert.Contains("Oracle-identiteit ontbreekt",row.Status);
    }
    [Fact]
    public void ConflictingReprintBlocksTheCardAndInactiveStockIsNotUsable()
    {
        var input=Input(); var original=input.Printings.Single(x=>x.Name=="Shock");
        var stale=original with {Id=Guid.NewGuid(),AvailableOnArena=false};
        var extended=input with {Printings=[..input.Printings,stale],Stock=[..input.Stock,new(stale.Id,5,Guid.NewGuid())]};
        var report=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,extended,Now); var row=report.Cards.Single(x=>x.Entry.Name=="Shock");
        Assert.Equal(7,row.OwnedTotal); Assert.Equal(2,row.OwnedEligible);
        report=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,extended with {Printings=[..input.Printings,stale with{OracleText="Wrong definition."}]},Now);
        Assert.False(report.Cards.Single(x=>x.Entry.Name=="Shock").Eligible);
    }
    [Theory]
    [InlineData("old")][InlineData("future")][InlineData("missing")][InlineData("metadata")][InlineData("rules")]
    public void InvalidSourceOrRulesBlockEveryRecommendation(string mode)
    {
        var input=Input(); var now=mode=="rules"?Now.AddDays(15):Now;
        var source=mode switch
        {
            "old"=>input.Source with {SourceUpdatedUtc=Now.AddHours(-25)},
            "future"=>input.Source with {SourceUpdatedUtc=Now.AddHours(2)},
            "missing"=>input.Source with {RunId=null}, "metadata"=>input.Source with {MetadataVersion=0}, _=>input.Source
        };
        var report=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,input with {Source=source},now);
        Assert.DoesNotContain(report.Cards,x=>x.Eligible); Assert.All(report.Cards,x=>Assert.Empty(ArchetypePoolEngine.Alternatives(report,x.Entry.OracleId)));
    }
    [Theory]
    [InlineData(CardLegality.Unknown,true)][InlineData(CardLegality.Banned,true)][InlineData(CardLegality.NotLegal,true)][InlineData(CardLegality.Legal,false)]
    public void CurrentArenaAvailabilityAndLegalityRemainRequired(CardLegality legality,bool arena)
    {
        var input=Input(); var report=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,input with
        {Printings=input.Printings.Select(x=>x.Name=="Shock"?x with{Standard=legality,AvailableOnArena=arena}:x).ToArray()},Now);
        Assert.False(report.Cards.Single(x=>x.Entry.Name=="Shock").Eligible);
    }
    [Fact]
    public void OnlyReviewedSameFunctionOwnedCardsAreReturnedAndRecheckingRevokesAdvice()
    {
        var input=Input(); var report=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,input,Now);
        var original=report.Cards.Single(x=>x.Entry.Name=="Slickshot Show-Off");
        var options=ArchetypePoolEngine.Alternatives(report,original.Entry.OracleId);
        Assert.Equal(new[]{"Emberheart Challenger","Fugitive Codebreaker"},options.Select(x=>x.Name));
        Assert.All(options,x=>Assert.Contains("geen bewezen",x.Explanation));
        var target=input.Printings.Single(x=>x.Name=="Emberheart Challenger");
        var changed=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,input with {Stock=input.Stock.Select(x=>x.PrintingId==target.Id?x with{Quantity=0}:x).ToArray()},Now);
        Assert.DoesNotContain(ArchetypePoolEngine.Alternatives(changed,original.Entry.OracleId),x=>x.Name==target.Name);
        Assert.Empty(ArchetypePoolEngine.Alternatives(report,report.Cards.Single(x=>x.Entry.Name=="Mountain").Entry.OracleId));
        Assert.Empty(ArchetypePoolEngine.Alternatives(report,report.Cards.Single(x=>x.Entry.Name=="Manifold Mouse").Entry.OracleId));
    }
    [Fact]
    public void FixedReferencesMeasureFunctionFilteringAndRankingAgainstGenericAdvice()
    {
        var reference=JsonSerializer.Deserialize<Reference>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"mono-red-advice-reference-v1.json")))!;
        var rows=new List<object>();var oldCorrect=0;var correct=0;
        foreach(var scenario in reference.Cases)
        {
            var input=Input();var original=input.Printings.Single(x=>x.Name==scenario.From);var land=input.Printings.Single(x=>x.Name=="Mountain");
            var candidates=input.Printings.Where(x=>scenario.Candidates.Contains(x.Name)).ToArray();
            var stock=candidates.Select(x=>new AdviceStock(x.Id,2,Guid.NewGuid())).Append(new(land.Id,60,Guid.NewGuid())).ToArray();
            var report=ArchetypePoolEngine.Evaluate(MonoRedPool.Current,input with {Stock=stock},Now);
            var actual=ArchetypePoolEngine.Alternatives(report,original.OracleId!);
            var snapshot=new AdviceSnapshot(Guid.NewGuid(),"Fixed mono-red reference",DeckRuleProfiles.Bo1,DeckRuleProfile.ArenaStandardBo1,input.Source,input.Printings,
                [new(land.Id,DeckZone.Mainboard,60),new(original.Id,DeckZone.Mainboard,1)],stock);
            var old=DeckAdviceEngine.Candidates(snapshot,"fixed",[],original.Id,DeckZone.Mainboard,Now,TestContext.Current.CancellationToken);
            var byId=input.Printings.ToDictionary(x=>x.Id);var oldName=old.Count==0?"":byId[old[0].PrintingId].Name;
            var actualName=actual.Count==0?"":actual[0].Name;
            if(scenario.AcceptableTop.Contains(oldName))oldCorrect++;
            if(scenario.AcceptableTop.Contains(actualName))correct++;
            rows.Add(new{scenario.Id,scenario.From,scenario.Candidates,scenario.AcceptableTop,scenario.Reason,BaselineTop=oldName,PoolTop=actualName,
                BaselineEligible=old.Select(x=>byId[x.PrintingId].Name),PoolEligible=actual.Select(x=>x.Name)});
            Assert.Contains(actualName,scenario.AcceptableTop);
        }
        var result=new{reference.Version,reference.PoolVersion,Engine=ArchetypePoolEngine.Version,Cases=rows.Count,BaselineTop1=oldCorrect,PoolTop1=correct,
            Limit=reference.Review+" Vergelijking meet functie-filtering EN rangschikking op dezelfde invoerpool; geschiktheidsfilters verschillen bewust van generiek advies.",Rows=rows};
        var directory=Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if(!string.IsNullOrEmpty(directory)){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(Directory.GetParent(directory)!.FullName,"phase10-advice-reference-evaluation.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));}
        Assert.Equal(20,reference.Cases.Length);Assert.True(correct>oldCorrect);Assert.Equal(20,correct);
    }
    private sealed record Reference(string Version,string PoolVersion,string Review,Case[] Cases);
    private sealed record Case(string Id,string From,string[] Candidates,string[] AcceptableTop,string Reason);
}
