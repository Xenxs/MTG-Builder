using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;

if(args.Length!=2)throw new ArgumentException("Fresh isolated data directory and output report required.");
var paths=new AppPaths(Path.GetFullPath(args[0]));if(File.Exists(paths.DatabasePath))throw new ArgumentException("Existing user data is not an evaluation fixture.");
var services=new ServiceCollection();services.AddLogging();services.AddDeckbuilder(paths);await using var provider=services.BuildServiceProvider();
await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();var store=provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync();
var stock=await store.CollectionAsync();var productReports=new List<PaperDeckReport>();foreach(var deck in await store.DecksAsync())productReports.Add(await store.DeckAsync(deck.Id));
var generated=new List<object>();
foreach(var theme in CardThemes.Choices)foreach(var style in DeckWizardRequest.Styles)
{
 var request=new DeckWizardRequest("Evaluation "+theme.Key+" "+style.Key,"casual60","any","",theme.Key,style.Key,false);
 try
 {
  var draft=await store.PrepareWizardAsync(request);var spells=draft.Lines.Where(x=>!x.Card.TypeLine.Contains("Land",StringComparison.Ordinal)).ToArray();
  generated.Add(new{request,Success=true,CreatureCopies=spells.Where(x=>x.Card.TypeLine.Contains("Creature",StringComparison.Ordinal)).Sum(x=>x.Quantity),EquipmentCopies=spells.Where(x=>x.Card.TypeLine.Contains("Equipment",StringComparison.Ordinal)).Sum(x=>x.Quantity),Demand=DeckManaHeuristics.Demand(spells.Select(x=>(x.Card,x.Quantity))),Synergy=draft.Report.Synergy,draft.Report.Analysis,draft.Report.Problems,Lines=draft.Lines.Select(x=>new{x.Card.OracleId,x.Card.Name,x.Card.TypeLine,x.Quantity})});
 }
 catch(ArgumentException e){generated.Add(new{request,Success=false,Error=e.Message});}
}
await File.WriteAllTextAsync(Path.GetFullPath(args[1]),JsonSerializer.Serialize(new{CheckedUtc=DateTimeOffset.UtcNow,Version=typeof(PaperCard).Assembly.GetName().Version?.ToString(),Source="Unchanged embedded product snapshots; simulated ownership; no normal database writes",Summary=await store.SummaryAsync(),UniqueOracles=stock.Select(x=>x.Card.OracleId).Distinct().Count(),Cards=stock.Select(x=>new{x.Card.OracleId,x.Card.Name,x.Card.TypeLine,x.Card.OracleText,x.Quantity,Themes=CardThemes.Choices.Where(t=>t.Key!="any"&&CardThemes.Matches(x.Card,t.Key)).Select(t=>t.Key)}),Products=productReports,Generated=generated},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(args[1]);
