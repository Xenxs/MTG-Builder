using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;
using MTGDeckbuilder.Domain;

if(args.Length==3&&args[0]=="summary")
{
 using var stream=File.OpenRead(args[1]);using var doc=JsonDocument.Parse(stream);var root=doc.RootElement;
 var summaries=root.GetProperty("Results").EnumerateArray().Select(r=>new{Request=r.GetProperty("request"),Success=r.GetProperty("Success").GetBoolean(),Seconds=r.GetProperty("Seconds").GetDouble(),ExpectedNoTheme=r.TryGetProperty("ExpectedNoTheme",out var n)&&n.GetBoolean(),Error=r.TryGetProperty("Error",out var e)?e.GetString():null,
     Profiles=r.TryGetProperty("Synergy",out var synergy)?new{Reviewed=synergy.GetProperty("ReviewedCards").GetInt32(),Limited=synergy.GetProperty("LimitedCards").GetInt32(),Unknown=synergy.GetProperty("TypeOnlyCards").GetInt32(),Warnings=synergy.GetProperty("Warnings").EnumerateArray().Select(x=>x.GetString()).ToArray()}:null,
     Id=r.TryGetProperty("id",out var id)?id.GetGuid():(Guid?)null,
     Analysis=r.TryGetProperty("Analysis",out var analysis)?analysis.GetString():null,
     Composition=r.TryGetProperty("Synergy",out var assessment)?Composition(assessment):null,
     Names=r.TryGetProperty("Lines",out var lines)?lines.EnumerateArray().Select(x=>x.GetProperty("Name").GetString()).ToArray():[]}).ToArray();
 await File.WriteAllTextAsync(args[2],JsonSerializer.Serialize(new{Before=root.GetProperty("Before"),After=root.GetProperty("After"),TotalSeconds=root.GetProperty("TotalSeconds"),PeakWorkingSetBytes=root.GetProperty("PeakWorkingSetBytes"),NetworkRequests=root.GetProperty("NetworkRequests"),RestartVerified=root.GetProperty("RestartVerified"),Results=summaries},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Concise practice summary written.");return;
}
static object Composition(JsonElement assessment)
{
 var profiles=assessment.GetProperty("Profiles").EnumerateArray().Select(p=>JsonSerializer.Deserialize<PaperCard>(p.GetProperty("Card"))!).Select(SynergyProfiles.Analyze).ToArray();
 var spells=profiles.Where(p=>!p.Has(SynergyFeature.Land)).ToArray();
 return new{DistinctNonlands=spells.Length,PrintedCreatureTypes=spells.SelectMany(p=>CommanderBuildPlan.PrintedCreatureTypes(p.Card)).GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count()),InstantSorceries=spells.Count(p=>p.Has(SynergyFeature.InstantSorcery)),GraveyardSetup=spells.Count(p=>p.Any(SynergyFeature.SelfMill,SynergyFeature.Surveil,SynergyFeature.Loot,SynergyFeature.SelfDiscard)),GraveyardReuse=spells.Count(p=>p.Any(SynergyFeature.CreatureRecursion,SynergyFeature.TypedCardRecursion))};
}
if(args.Length is not(2 or 3)||args.Length==3&&args[2]!="--focused")throw new ArgumentException("Existing isolated simulation directory, fresh report path and optional --focused required.");
var directory=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);
if(!directory.StartsWith(Path.GetFullPath(".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(Path.Combine(directory,"FULL-CATALOG-SIMULATION.json"))||File.Exists(output))throw new ArgumentException("Only a prepared simulation and fresh report are allowed.");
var paths=new AppPaths(directory);var http=new RejectNetwork();ServiceProvider Open(){var services=new ServiceCollection();services.AddLogging();services.AddDeckbuilder(paths);services.AddSingleton(new HttpClient(http,false));return services.BuildServiceProvider();}
var provider=Open();var allWatch=Stopwatch.StartNew();var reports=new List<object>();var saved=new List<Guid>();
try
{
 await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();var store=provider.GetRequiredService<IPaperWorkspace>();var before=await store.SummaryAsync();var stock=await store.CollectionAsync();var cards=stock.Select(x=>x.Card).ToArray();
 var requests=new List<DeckWizardRequest>();foreach(var format in new[]{"casual60","standard"})foreach(var theme in CardThemes.Choices)foreach(var style in DeckWizardRequest.Styles)requests.Add(new("SIMULATIE "+format+" "+theme.Key+" "+style.Key,format,"any","",theme.Key,style.Key,false));
 foreach(var (name,theme,style) in new[]{("Krenko, Mob Boss","tokens","aggro"),("Giada, Font of Hope","any","balanced"),("Talrand, Sky Summoner","tokens","control"),("Meren of Clan Nel Toth","graveyard","balanced"),("Cloud, Planet's Champion","equipment","aggro"),("Sephiroth, Planet's Heir","opponent-death","control"),("Edgar Markov","tokens","aggro"),("Atraxa, Praetors' Voice","any","balanced"),("Kenrith, the Returned King","draw","control"),("Kozilek, the Great Distortion","any","balanced")})
 {
  var c=cards.SingleOrDefault(c=>c.Name==name);if(c is null||!c.IsCommanderCandidate||c.CommanderLegality!="legal")throw new Exception("Missing expected supported commander: "+name);
  requests.Add(new("SIMULATIE "+name,"commander",c.Colors.Length==0?"colorless":"selected",c.Colors,theme,style,false,c.Id));
 }
 if(args.Length==3)requests=requests.Where(r=>r.Format=="commander"||r.Theme=="graveyard").ToList();
 foreach(var request in requests)
 {
  var watch=Stopwatch.StartNew();
  try
  {
   var draft=await store.PrepareWizardAsync(request);var expected=request.Format=="commander"?100:60;
   if(draft.Lines.Sum(x=>x.Quantity)!=expected||draft.Report.Problems.Count!=0||draft.Lines.Any(x=>!request.Allows(x.Card)||SynergyProfiles.AutomaticSelectionIssue(x.Card)is not null))throw new Exception("Structural/color/stock/unsupported-shape failure: "+request.Name+" "+string.Join(";",draft.Report.Problems));
   if(draft.Lines.Any(x=>x.Quantity>stock.Single(y=>y.Card.Id==x.Card.Id).Quantity)||draft.Lines.Where(x=>!x.Card.IsBasicLand).GroupBy(x=>x.Card.OracleId).Any(g=>g.Sum(x=>x.Quantity)>(request.Format=="commander"?1:4)))throw new Exception("Invented stock or Oracle copy limit.");
   if(request.Format=="standard"&&draft.Lines.Any(x=>x.Card.Standard!="legal"))throw new Exception("Nonlegal Standard card.");
   if(request.Format=="commander"){var leader=draft.Lines.Single(x=>x.Zone==DeckZone.Commander).Card;if(draft.Lines.Any(x=>x.Card.CommanderLegality!="legal"||!PaperDeckRules.ColorsFit(x.Card,leader)))throw new Exception("Commander legality/identity failure.");}
   var id=await store.SaveDeckDraftAsync(draft);if(await store.SaveDeckDraftAsync(draft)!=id)throw new Exception("Save is not idempotent.");saved.Add(id);
   reports.Add(new{request,Success=true,id,Seconds=watch.Elapsed.TotalSeconds,Cards=expected,draft.Report.Analysis,draft.Report.Synergy,Lines=draft.Lines.Select(x=>new{x.Card.OracleId,x.Card.Name,x.Card.TypeLine,x.Card.ManaValue,x.Card.ManaCost,x.Card.Colors,x.Card.OracleText,x.Quantity,x.Owned})});
   Console.WriteLine($"PASS {request.Name}: {watch.Elapsed.TotalSeconds:F2}s");
  }
  catch(ArgumentException e)
  {
   var eligible=cards.Where(c=>SynergyProfiles.AutomaticSelectionIssue(c)is null&&request.Allows(c)&&(request.Format!="standard"||c.Standard=="legal")).ToArray();var themeCards=eligible.Count(c=>CardThemes.Matches(c,request.Theme));
   reports.Add(new{request,Success=false,Seconds=watch.Elapsed.TotalSeconds,EligibleThemeCards=themeCards,ExpectedNoTheme=themeCards==0,Error=e.Message});Console.WriteLine($"BLOCK {request.Name}: {e.Message}");
  }
 }
 await provider.DisposeAsync();SqliteConnection.ClearAllPools();provider=Open();await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();store=provider.GetRequiredService<IPaperWorkspace>();
 foreach(var id in saved)await store.DeckAsync(id);var after=await store.SummaryAsync();if(after.Copies!=before.Copies||after.Printings!=before.Printings||after.Decks!=before.Decks+saved.Count)throw new Exception("Stock changed or saved decks lost on reopen.");
 await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{RecordedUtc=DateTimeOffset.UtcNow,Engine=SynergyProfiles.Version,Before=before,After=after,Source="Full physical released Oracle pool, 4 per nonbasic / 250 per basic, isolated SQLite, simulated ownership",NetworkRequests=http.Requests,RestartVerified=true,TotalSeconds=allWatch.Elapsed.TotalSeconds,PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,Results=reports},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{saved.Count}/{requests.Count} saved, idempotent and reopened; {http.Requests} network requests.");
}
finally{await provider.DisposeAsync();SqliteConnection.ClearAllPools();}
sealed class RejectNetwork:HttpMessageHandler
{
 public int Requests{get;private set;}
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Requests++;throw new InvalidOperationException("Fresh catalog unexpectedly requested network: "+request.RequestUri);}
}
