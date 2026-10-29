using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;
using MTGDeckbuilder.Domain;

if(args.Length!=2)throw new ArgumentException("Fresh isolated directory and report path required.");
var paths=new AppPaths(Path.GetFullPath(args[0]));
if(File.Exists(paths.DatabasePath)||File.Exists(args[1]))throw new ArgumentException("Never reuse data or overwrite first practice evidence.");
ServiceProvider Open(){var s=new ServiceCollection();s.AddLogging();s.AddDeckbuilder(paths);return s.BuildServiceProvider();}
var provider=Open();
try
{
 await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();var store=provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync();
 var before=await store.SummaryAsync();var stock=await store.CollectionAsync();var catalog=provider.GetRequiredService<IPaperCatalog>();
 var cloud=stock.First(x=>x.Card.Name=="Cloud, Planet's Champion").Card;var seph=stock.First(x=>x.Card.Name=="Sephiroth, Planet's Heir").Card;
 DeckWizardRequest[] requests=[new("WR equipment owned","casual60","selected","WR","equipment","aggro",false),new("UB graveyard owned","casual60","selected","UB","graveyard","control",false),new("WB enemy deaths owned","casual60","selected","WB","opponent-death","balanced",false),new("Cloud Commander owned","commander","selected","WR","equipment","balanced",false,cloud.Id),new("Sephiroth Commander owned","commander","selected","UB","opponent-death","control",false,seph.Id),new("WR Standard planning","standard","selected","WR","equipment","aggro",true),new("Cloud Commander planning","commander","selected","WR","equipment","balanced",true,cloud.Id),new("Sephiroth Commander planning","commander","selected","UB","opponent-death","control",true,seph.Id)];
 var results=new List<object>();var saved=new List<Guid>();
 foreach(var request in requests)
 {
  var elapsed=System.Diagnostics.Stopwatch.StartNew();
  try
  {
   var draft=await store.PrepareWizardAsync(request);
   if(draft.Lines.Any(x=>SynergyProfiles.AutomaticSelectionIssue(x.Card) is not null))throw new Exception("Unsupported win/loss condition automatically selected.");
   if(draft.Lines.Sum(x=>x.Quantity)!=(request.Format=="commander"?100:60)||draft.Lines.Any(x=>!request.Allows(x.Card)||SynergyProfiles.ComplexCard(x.Card)))throw new Exception("Size/color/unsupported-layout constraint failed.");
   if(draft.Report.Problems.Any(p=>!request.Planning||!p.Contains("tekort",StringComparison.Ordinal)))throw new Exception("Unexpected rule/stock problem: "+string.Join("; ",draft.Report.Problems));
   foreach(var line in draft.Lines)if(!request.Planning&&line.Quantity>stock.Single(x=>x.Card.Id==line.Card.Id).Quantity)throw new Exception("Invented stock.");
   if(request.Format=="commander")
   {
    var leader=draft.Lines.Single(x=>x.Zone==DeckZone.Commander).Card;
    if(draft.Lines.Any(x=>!PaperDeckRules.ColorsFit(x.Card,leader)||x.Card.CommanderLegality!="legal")||draft.Lines.Where(x=>!x.Card.IsBasicLand).GroupBy(x=>x.Card.OracleId).Any(g=>g.Sum(x=>x.Quantity)>1))throw new Exception("Commander identity/legality/singleton failed.");
   }
   var id=await store.SaveDeckDraftAsync(draft);saved.Add(id);
   results.Add(new{request,Success=true,ElapsedMilliseconds=elapsed.ElapsedMilliseconds,id,Total=draft.Lines.Sum(x=>x.Quantity),draft.Report.Problems,draft.Report.Analysis,draft.Report.Synergy,Lines=draft.Lines.Select(x=>new{x.Card.OracleId,x.Card.Name,x.Card.TypeLine,x.Quantity,x.Owned,Profile=SynergyProfiles.Analyze(x.Card).Status})});
  }
  catch(ArgumentException e){results.Add(new{request,Success=false,ElapsedMilliseconds=elapsed.ElapsedMilliseconds,ExpectedOwnershipBlock=request.Format=="commander"&&!request.Planning,Error=e.Message});}
 }
 var adelbert=stock.Single(x=>x.Card.Name=="Adelbert Steiner").Card;var image=await catalog.ImageAsync(adelbert,false);
 await provider.DisposeAsync();SqliteConnection.ClearAllPools();provider=Open();await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();store=provider.GetRequiredService<IPaperWorkspace>();
 foreach(var id in saved)await store.DeckAsync(id);var after=await store.SummaryAsync();if(after.Copies!=202||after.Decks!=4+saved.Count)throw new Exception("Save/restart mutated collection or lost decks.");
 await File.WriteAllTextAsync(Path.GetFullPath(args[1]),JsonSerializer.Serialize(new{CheckedUtc=DateTimeOffset.UtcNow,Source="Live Scryfall and simulated product ownership; fresh isolated database; no resets or normal user-data writes",Before=before,After=after,RestartVerified=true,Image=image,Results=results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{saved.Count}/{requests.Length} proposals saved and reopened. Report: {args[1]}");
}
finally{await provider.DisposeAsync();SqliteConnection.ClearAllPools();}
