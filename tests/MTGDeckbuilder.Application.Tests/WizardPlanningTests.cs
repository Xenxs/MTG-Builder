using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class WizardPlanningTests
{
 private static CancellationToken Ct=>TestContext.Current.CancellationToken;
 private sealed class Clock:TimeProvider
 {public int Days{get;set;}public override DateTimeOffset GetUtcNow()=>new(PaperDeckRules.CheckedUtc.AddHours(23).AddDays(Days));}
 private sealed class Pool:HttpMessageHandler
 {
  public List<string> Queries{get;}=[];public bool Fail{get;set;}
  private readonly Guid[] _ids=Enumerable.Range(0,84).Select(_=>Guid.NewGuid()).ToArray();
  private readonly Guid[] _oracles=Enumerable.Range(0,84).Select(_=>Guid.NewGuid()).ToArray();
  public string Oracle(int i)=>_oracles[i].ToString();
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   var query=Uri.UnescapeDataString(request.RequestUri!.Query);Queries.Add(query);
   Assert.EndsWith("/search",request.RequestUri.AbsolutePath);Assert.Contains("game:paper",query);Assert.Contains("id<=rw",query);
   if(Fail)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
   var cards=new List<JsonNode>();
   for(var i=0;i<84;i++)
   {
    var raw=PaperWorkspaceTests.Example()["Cards"]![0]!.DeepClone().AsObject();raw["id"]=_ids[i];raw["oracle_id"]=_oracles[i];raw["name"]="Wizard fixture "+i.ToString("D2");
    var color=i==82?"U":i%2==0?"W":"R";var basic=i>=80&&i<=81;var mv=basic?0:1+i%7;
    raw["color_identity"]=new JsonArray(color);raw["colors"]=basic?new JsonArray():new JsonArray(color);raw["mana_cost"]=basic?"":"{1}{"+color+"}";raw["cmc"]=mv;
    raw["type_line"]=basic?i==80?"Basic Land — Plains":"Basic Land — Mountain":i<10?"Artifact — Equipment":i%2==0?"Creature — Soldier":"Instant";
    raw["oracle_text"]=i<10?"Equip {2}":"";raw["legalities"]!["standard"]=i==83?"banned":"legal";raw["legalities"]!["commander"]=i==83?"banned":"legal";
    if(query.Contains("t:basic",StringComparison.Ordinal)!=basic)continue;
    if(!basic&&query.Contains("cmc<=3",StringComparison.Ordinal)&&mv>3)continue;
    if(!basic&&query.Contains("cmc>3",StringComparison.Ordinal)&&(mv<=3||mv>5))continue;
    if(!basic&&query.Contains("cmc>5",StringComparison.Ordinal)&&mv<=5)continue;
    if(query.Contains("o:equipped",StringComparison.Ordinal)&&i>=10)continue;
    cards.Add(raw);
   }
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new JsonObject{["data"]=new JsonArray(cards.ToArray())}.ToJsonString())});
  }
 }
 [Theory]
 [InlineData("standard",60)]
 [InlineData("commander",100)]
 public async Task ScopedPlanningHonorsColorThemeLegalityAndExpiryAtSave(string format,int size)
 {
  var clock=new Clock();var pool=new Pool();await using var t=new TestDatabase(s=>{s.AddSingleton<TimeProvider>(clock);s.AddSingleton(new HttpClient(pool));});await t.InitializeAsync();
  var store=t.Provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync(Ct);var cloud=(await store.CollectionAsync(ct:Ct)).Single(x=>x.Card.Name=="Cloud, Planet's Champion").Card;
  var request=new DeckWizardRequest("Scoped wizard",format,"selected","RW","equipment","control",true,format=="commander"?cloud.Id:null);
  var draft=await store.PrepareWizardAsync(request,Ct);Assert.Equal(size,draft.Lines.Sum(x=>x.Quantity));Assert.Equal(5,pool.Queries.Count);Assert.All(pool.Queries,q=>Assert.Contains("legal:"+format,q));
  Assert.All(draft.Lines,x=>Assert.True(request.Allows(x.Card)));Assert.DoesNotContain(draft.Lines,x=>x.Card.OracleId==pool.Oracle(82)||x.Card.OracleId==pool.Oracle(83));
  Assert.Contains(draft.Lines,x=>x.Card.TypeLine.Contains("Equipment",StringComparison.Ordinal));Assert.Contains(draft.Report.Problems,p=>p.Contains("tekort"));Assert.DoesNotContain(draft.Report.Problems,p=>!p.Contains("tekort"));
  clock.Days=2;await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveDeckDraftAsync(draft,Ct));Assert.Equal(4,(await store.DecksAsync(Ct)).Count);Assert.Equal(202,(await store.SummaryAsync(Ct)).Copies);
 }
 [Fact]
 public async Task ServiceFailureDoesNotSilentlyFallBackOrCreateADeck()
 {
  var pool=new Pool{Fail=true};await using var t=new TestDatabase(s=>{s.AddSingleton<TimeProvider>(new Clock());s.AddSingleton(new HttpClient(pool));});await t.InitializeAsync();var store=t.Provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync(Ct);
  await Assert.ThrowsAsync<ArgumentException>(()=>store.PrepareWizardAsync(new("Failure","standard","selected","RW","any","aggro",true),Ct));Assert.Single(pool.Queries);Assert.Equal(4,(await store.DecksAsync(Ct)).Count);
 }
 [Fact]
 public void StyleSelectionChangesTheConstructedSpellMix()
 {
  var card=ScryfallPaperCatalog.Parse(PaperWorkspaceTests.Example()["Cards"]![0]!.ToJsonString(),PaperDeckRules.CheckedUtc);
  var spells=Enumerable.Range(0,20).Select(i=>card with{Id=Guid.NewGuid(),OracleId="style-"+i,Name="Style "+i,TypeLine=i<10?"Creature — Soldier":"Instant",Colors="W",ManaValue=i<10?2:4,OracleText=""});
  var land=card with{Id=Guid.NewGuid(),OracleId="style-basic",Name="Plains",TypeLine="Basic Land — Plains",Colors="W",ManaValue=0};var known=spells.Append(land).ToArray();
  var request=new DeckWizardRequest("Style","casual60","selected","W","any","aggro",true);
  var aggro=PhysicalDeckPlanner.Create(known,[],known,true,request);var control=PhysicalDeckPlanner.Create(known,[],known,true,request with{Style="control"});
  var aggroCreatures=aggro.Where(x=>x.Card.TypeLine.Contains("Creature",StringComparison.Ordinal)).Sum(x=>x.Quantity);
  var controlCreatures=control.Where(x=>x.Card.TypeLine.Contains("Creature",StringComparison.Ordinal)).Sum(x=>x.Quantity);
  Assert.True(aggroCreatures>controlCreatures,"Aggro must select more creature copies than control, without forcing every spell into one card type.");
  Assert.True(control.Where(x=>x.Card.TypeLine=="Instant").Sum(x=>x.Quantity)>aggro.Where(x=>x.Card.TypeLine=="Instant").Sum(x=>x.Quantity));
  Assert.Equal(24,aggro.Where(x=>x.Card.IsBasicLand).Sum(x=>x.Quantity));Assert.All(aggro,x=>Assert.Equal(0,x.Owned));
 }
}
