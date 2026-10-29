using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;

if(args.Length!=2)throw new ArgumentException("Fresh isolated directory and report path required.");
var paths=new AppPaths(Path.GetFullPath(args[0]));
if(File.Exists(paths.DatabasePath))throw new ArgumentException("Existing data is never used for destructive probes.");
ServiceProvider Open(){var s=new ServiceCollection();s.AddLogging();s.AddDeckbuilder(paths);return s.BuildServiceProvider();}
var provider=Open();
try
{
 await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
 var store=provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync();
 var original=await store.SummaryAsync();var own=await store.CollectionAsync();
 var filtered=CardThemes.Filter(own,"","W","equipment","Creature","fin");
 if(filtered.Count!=3)throw new Exception("Combined filters changed unexpectedly.");
 var catalog=provider.GetRequiredService<IPaperCatalog>();var adelbert=own.Single(x=>x.Card.Name=="Adelbert Steiner").Card;
 var image=await catalog.ImageAsync(adelbert,false)??throw new Exception("No actual image.");
 var cloud=own.Single(x=>x.Card.Name=="Cloud, Planet's Champion").Card;
 DeckWizardRequest[] requests=[new("Wizard WR Equipment own","casual60","selected","WR","equipment","aggro",false),new("Wizard WR Standard planning","standard","selected","WR","equipment","aggro",true),new("Wizard Cloud Commander planning","commander","selected","WR","equipment","balanced",true,cloud.Id)];
 var decks=new List<object>();var ids=new List<Guid>();
 foreach(var request in requests)
 {
  var draft=await store.PrepareWizardAsync(request);
  if(draft.Lines.Sum(x=>x.Quantity)!=(request.Format=="commander"?100:60)||draft.Lines.Any(x=>!request.Allows(x.Card))||draft.Report.Problems.Any(p=>!request.Planning||!p.Contains("tekort",StringComparison.Ordinal)))throw new Exception("Wizard constraints failed.");
  var id=await store.SaveDeckDraftAsync(draft);ids.Add(id);
  decks.Add(new{request,id,Cards=draft.Lines.Sum(x=>x.Quantity),draft.Report.Problems,draft.Report.Analysis,List=draft.Lines.Select(x=>x.Label)});
 }
 await provider.DisposeAsync();SqliteConnection.ClearAllPools();provider=Open();await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();store=provider.GetRequiredService<IPaperWorkspace>();
 foreach(var id in ids)await store.DeckAsync(id);
 if((await store.SummaryAsync()).Copies!=202)throw new Exception("Wizard changed ownership.");
 var reset=provider.GetRequiredService<IDatabaseResetService>();var review=await reset.ReviewAsync();var result=await reset.ClearAsync(review.Id,"WISSEN");
 await provider.DisposeAsync();SqliteConnection.ClearAllPools();provider=Open();await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();store=provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync();
 var empty=await store.SummaryAsync();if(empty.Copies!=0||empty.Decks!=0||empty.DemoActive)throw new Exception("Reset reseeded after restart.");
 var backups=provider.GetRequiredService<IBackupService>();var inspected=await backups.InspectAsync(result.BackupPath);await backups.ScheduleAsync(inspected.Id,true);
 SqliteConnection.ClearAllPools();using(var lease=DataDirectoryLease.Acquire(paths))await provider.GetRequiredService<SqliteBackupService>().ApplyPendingAsync(lease);
 await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();var restored=await store.SummaryAsync();if(restored.Copies!=202||restored.Decks!=7)throw new Exception("Pre-reset backup did not restore.");
 await File.WriteAllTextAsync(Path.GetFullPath(args[1]),JsonSerializer.Serialize(new{CheckedUtc=DateTimeOffset.UtcNow,Source="Real Scryfall; simulated product ownership. Reset only in fresh isolated probe database.",Original=original,Filters=filtered.Select(x=>x.Label),Image=new{adelbert.Id,File=image,Bytes=new FileInfo(image).Length,Sha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(image)))},Decks=decks,RestartVerified=true,EmptyAfterResetAndRestart=empty,PreResetBackup=result.BackupPath,Restored=restored},new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine(args[1]);
}
finally{await provider.DisposeAsync();SqliteConnection.ClearAllPools();}
