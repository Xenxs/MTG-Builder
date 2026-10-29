using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;
using MTGDeckbuilder.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

if(args.Length<3)throw new ArgumentException("extract/analyze/bench/scores, input, fresh output required.");
var mode=args[0];var input=Path.GetFullPath(args[1]);var output=Path.GetFullPath(args[2]);if(File.Exists(output))throw new ArgumentException("Preserve original evidence; choose a fresh output.");
var options=new JsonSerializerOptions{WriteIndented=true};var now=DateTime.UtcNow;var watch=Stopwatch.StartNew();
string Stable(string oracle)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("phase17-holdout-v1/"+oracle)));
async Task Save(object value)=>await File.WriteAllTextAsync(output,JsonSerializer.Serialize(value,options));
async IAsyncEnumerable<string> Rows(string path,bool gzip=false)
{await using var file=File.OpenRead(path);await using Stream data=gzip?new GZipStream(file,CompressionMode.Decompress):file;using var reader=new StreamReader(data,Encoding.UTF8);while(await reader.ReadLineAsync() is {} line)if(!string.IsNullOrWhiteSpace(line))yield return line;}
if(mode=="extract")
{
 var cutoff=new DateOnly(2026,10,28);var selected=new Dictionary<string,(string Raw,int Score,string Date,string Id)>();var skipped=new Dictionary<string,int>();var entries=0;
 void Skip(string why)=>skipped[why]=skipped.GetValueOrDefault(why)+1;
 await foreach(var line in Rows(input,true))
 {
  entries++;using var doc=JsonDocument.Parse(line);var c=doc.RootElement;
  string T(string key)=>c.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()!:"";
  if(T("object")!="card"||!c.TryGetProperty("games",out var games)||!games.EnumerateArray().Any(x=>x.GetString()=="paper")){Skip("NotPhysicalCard");continue;}
  var date=T("released_at");if(!DateOnly.TryParse(date,out var release)||release>cutoff){Skip("NotReleasedByCutoff");continue;}
  var layout=T("layout");var type=T("type_line");
  var meldResult=layout=="meld"&&c.TryGetProperty("all_parts",out var parts)&&parts.EnumerateArray().Any(p=>p.GetProperty("id").GetString()==T("id")&&p.GetProperty("component").GetString()=="meld_result");
  if(meldResult||layout is "token" or "double_faced_token" or "emblem" or "art_series" or "planar" or "scheme" or "vanguard"||
     type.Split([' ','—','/'],StringSplitOptions.RemoveEmptyEntries).Intersect(new[]{"Token","Emblem","Plane","Phenomenon","Scheme","Vanguard","Conspiracy","Dungeon","Card","Sticker"}).Any())
  {Skip("NotLibraryCardOrMeldResult");continue;}
  var oracle=T("oracle_id");if(!Guid.TryParse(oracle,out _)||!Guid.TryParse(T("id"),out _)){Skip("NoOracleOrPrintingIdentity");continue;}
  var score=(T("lang")=="en"?1000:0)+(T("border_color") is "gold" or "silver"?0:100)+(T("set_type")=="memorabilia"?0:10);
  if(selected.TryGetValue(oracle,out var prior)&& (score<prior.Score||score==prior.Score&&(string.CompareOrdinal(date,prior.Date)<0||date==prior.Date&&string.CompareOrdinal(T("id"),prior.Id)>=0))){Skip("DuplicatePrinting");continue;}
  if(selected.ContainsKey(oracle))Skip("DuplicatePrinting");selected[oracle]=(line,score,date,T("id"));
 }
 var ordered=selected.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray();await using(var writer=new StreamWriter(output,false,new UTF8Encoding(false)))foreach(var row in ordered)await writer.WriteLineAsync(row.Value.Raw);
 var excluded=new HashSet<string>();foreach(var name in new[]{"conflux-paper-v1","final-fantasy-starter-v1"}){using var s=typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources."+name+".json")!;using var d=JsonDocument.Parse(s);foreach(var c in d.RootElement.GetProperty("Cards").EnumerateArray())excluded.Add(c.GetProperty("oracle_id").GetString()!);}
 foreach(var n in new[]{1,2,3}){using var d=JsonDocument.Parse(await File.ReadAllTextAsync($"examples/synergy-holdout-cards-v{n}.json"));foreach(var c in d.RootElement.GetProperty("Cards").EnumerateArray())excluded.Add(c.GetProperty("oracle_id").GetString()!);}
 var candidates=ordered.Where(x=>!excluded.Contains(x.Key)).OrderBy(x=>Stable(x.Key),StringComparer.Ordinal).ToArray();var reserved=candidates.Take(48).Select(x=>x.Key).ToHashSet();
 string[] groups=["adventure","split","transform","modal_dfc","flip","leveler","saga","long"];
 foreach(var g in groups)foreach(var row in candidates.Where(x=>!reserved.Contains(x.Key)).Where(x=>{using var d=JsonDocument.Parse(x.Value.Raw);var c=d.RootElement;return g=="long"?(c.TryGetProperty("oracle_text",out var t)?t.GetString()?.Length:0)>=1200:g=="saga"?c.GetProperty("type_line").GetString()!.Contains("Saga"):c.GetProperty("layout").GetString()==g;}).Take(2))reserved.Add(row.Key);
 using(var s=File.Create(output+".reserved-cards.json"))using(var w=new Utf8JsonWriter(s)){w.WriteStartObject();w.WritePropertyName("Cards");w.WriteStartArray();foreach(var row in ordered.Where(x=>reserved.Contains(x.Key)))w.WriteRawValue(row.Value.Raw);w.WriteEndArray();w.WriteEndObject();}
 var manifest=new{RecordedUtc=DateTimeOffset.UtcNow,Cutoff=cutoff,InputEntries=entries,UniquePhysicalOracleCards=ordered.Length,Skipped=skipped,Selection="One physical released printing per Oracle; prefer English, ordinary border, non-memorabilia, then recent and stable ID.",AssumedNonBasicCopies=4,AssumedBasicCopies=250,Seconds=watch.Elapsed.TotalSeconds,Sha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(output))),Reserved=new{Rule="First 48 by SHA256(seed/Oracle-ID), then two per eight layout/text-length strata, excludes 87 product Oracles and all three previous sets. Not representative or independent.",Seed="phase17-holdout-v1",OracleIds=reserved.Order().ToArray()}};
 await File.WriteAllTextAsync(output+".manifest.json",JsonSerializer.Serialize(manifest,options));Console.WriteLine($"{entries} source entries -> {ordered.Length} physical Oracles; {reserved.Count} reserved without printing Oracle texts.");return;
}
var cards=new List<PaperCard>();await foreach(var line in Rows(input))cards.Add(ScryfallPaperCatalog.Parse(line,now));
if(mode=="reserve")
{
 if(args.Length<4)throw new ArgumentException("Prior reservation manifest required.");
 using var prior=JsonDocument.Parse(await File.ReadAllTextAsync(args[3]));var excluded=prior.RootElement.GetProperty("Reserved").GetProperty("OracleIds").EnumerateArray().Select(x=>x.GetString()!).ToHashSet();
 foreach(var n in new[]{1,2,3}){using var d=JsonDocument.Parse(await File.ReadAllTextAsync($"examples/synergy-holdout-cards-v{n}.json"));foreach(var c in d.RootElement.GetProperty("Cards").EnumerateArray())excluded.Add(c.GetProperty("oracle_id").GetString()!);}
 foreach(var name in new[]{"conflux-paper-v1","final-fantasy-starter-v1"}){using var s=typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources."+name+".json")!;using var d=JsonDocument.Parse(s);foreach(var c in d.RootElement.GetProperty("Cards").EnumerateArray())excluded.Add(c.GetProperty("oracle_id").GetString()!);}
 var candidates=new List<(string Oracle,string Layout,int Length,string Raw,string Hash)>();
 await foreach(var line in Rows(input)){using var d=JsonDocument.Parse(line);var c=d.RootElement;var oracle=c.GetProperty("oracle_id").GetString()!;if(excluded.Contains(oracle))continue;var length=c.TryGetProperty("oracle_text",out var t)?t.GetString()?.Length??0:0;var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("phase17-holdout-v2/"+oracle)));candidates.Add((oracle,c.GetProperty("layout").GetString()!,length,line,hash));}
 var ordered=candidates.OrderBy(x=>x.Hash,StringComparer.Ordinal).ToArray();var reserved=ordered.Take(48).Select(x=>x.Oracle).ToHashSet();
 foreach(var group in new[]{"adventure","split","transform","modal_dfc","flip","leveler","saga","long"})foreach(var row in ordered.Where(x=>!reserved.Contains(x.Oracle)&&(group=="long"?x.Length>=1200:x.Layout==group)).Take(2))reserved.Add(row.Oracle);
 using(var s=File.Create(output))using(var w=new Utf8JsonWriter(s)){w.WriteStartObject();w.WritePropertyName("Cards");w.WriteStartArray();foreach(var row in ordered.Where(x=>reserved.Contains(x.Oracle)))w.WriteRawValue(row.Raw);w.WriteEndArray();w.WriteEndObject();}
 await File.WriteAllTextAsync(output+".manifest.json",JsonSerializer.Serialize(new{ReservedUtc=DateTimeOffset.UtcNow,Seed="phase17-holdout-v2",Rule="48 SHA-ordered general plus up to 16 layout/length strata; excludes A, products and all previous-phase sets. Sealed before additional iteration.",OracleIds=reserved.Order().ToArray()},options));Console.WriteLine($"Reserved {reserved.Count} additional cards; no texts printed.");return;
}
if(mode=="seed")
{
 if(args.Length<4)throw new ArgumentException("A fresh workspace .local directory is required.");
 var directory=Path.GetFullPath(args[3]);var local=Path.GetFullPath(".local")+Path.DirectorySeparatorChar;
 if(!directory.StartsWith(local,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Simulation data must remain under this workspace .local.");
 var paths=new AppPaths(directory);if(File.Exists(paths.DatabasePath))throw new ArgumentException("Never overwrite an existing database.");
 var services=new ServiceCollection();services.AddLogging();services.AddDeckbuilder(paths);await using var provider=services.BuildServiceProvider();await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
 await using(var db=await provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync())
 {
  var setting=await db.Settings.SingleOrDefaultAsync();if(setting is null){setting=new UserSettings();db.Settings.Add(setting);}setting.DisableExamples();setting.SetPlayerName("SIMULATIE — alle papieren kaarten");await db.SaveChangesAsync();
  db.ChangeTracker.AutoDetectChangesEnabled=false;await using var tx=await db.Database.BeginTransactionAsync();var count=0;
  await foreach(var line in Rows(input))
  {
   var card=ScryfallPaperCatalog.Parse(line,now);db.PaperPrintings.Add(new(card.Id,line,now));var finish=card.Finishes.Contains("normal")?"normal":card.Finishes.First();
   var binder="SIMULATIE alle kaarten 2026-10-28; geen echte scan";db.PaperHoldings.Add(new(PaperIdentity.Key(card.Id,card.Language,finish,"unknown",binder),card.Id,card.Language,finish,"unknown",binder,card.IsBasicLand?250:4,true));
   if(++count%1000==0){await db.SaveChangesAsync();db.ChangeTracker.Clear();}
  }
  await db.SaveChangesAsync();await tx.CommitAsync();
 }
 var workspace=provider.GetRequiredService<IPaperWorkspace>();var summary=await workspace.SummaryAsync();
 await File.WriteAllTextAsync(Path.Combine(directory,"FULL-CATALOG-SIMULATION.json"),JsonSerializer.Serialize(new{RecordedUtc=DateTimeOffset.UtcNow,Source=input,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input))),Summary=summary,Note="Isolated assumed ownership, not a real scan. Normal collection not modified."},options));
 await Save(new{RecordedUtc=DateTimeOffset.UtcNow,Database=paths.DatabasePath,Summary=summary,Seconds=watch.Elapsed.TotalSeconds,Bytes=new FileInfo(paths.DatabasePath).Length,NoNormalDataWrites=true});Console.WriteLine($"Seeded {summary.Printings} Oracles, {summary.Copies} assumed copies, in {watch.Elapsed.TotalSeconds:F1}s.");return;
}
if(mode=="analyze")
{
 var counts=new Dictionary<string,int>();var features=new Dictionary<string,int>();var themes=new Dictionary<string,int>();var profiles=new List<object>();var rejected=0;
 foreach(var c in cards){var p=SynergyProfiles.Analyze(c);var key=p.Reviewed?"Reviewed":p.Changed?"Changed":p.Features.Any(SynergyProfiles.IsSemantic)?"Limited":"TypeOnlyOrUnknown";counts[key]=counts.GetValueOrDefault(key)+1;foreach(var f in p.Features)features[f.ToString()]=features.GetValueOrDefault(f.ToString())+1;var found=CardThemes.Choices.Where(t=>t.Key!="any"&&p.Theme(t.Key)).Select(t=>t.Key).ToArray();foreach(var t in found)themes[t]=themes.GetValueOrDefault(t)+1;if(SynergyProfiles.AutomaticSelectionIssue(c)is not null)rejected++;profiles.Add(new{c.OracleId,c.Name,Category=key,Features=p.Features.Select(x=>x.ToString()).ToArray(),Themes=found,AutomaticSelectionIssue=SynergyProfiles.AutomaticSelectionIssue(c)});}
 await Save(new{RecordedUtc=DateTimeOffset.UtcNow,Engine=SynergyProfiles.Version,Cards=cards.Count,Counts=counts,Features=features,Themes=themes,RejectedAutomatic=rejected,StandardLegal=cards.Count(c=>c.Standard=="legal"),CommanderLegal=cards.Count(c=>c.CommanderLegality=="legal"),Seconds=watch.Elapsed.TotalSeconds,PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,Note="Coverage/status, not accuracy or complete rules understanding.",Profiles=profiles});Console.WriteLine($"Analyzed all {cards.Count} cards in {watch.Elapsed.TotalSeconds:F1}s.");return;
}
if(mode=="bench")
{
 var size=args.Length>3?int.Parse(args[3]):100;var pool=cards.Where(c=>!c.TypeLine.Contains("Land")&&SynergyProfiles.AutomaticSelectionIssue(c)is null).OrderBy(c=>Stable(c.OracleId),StringComparer.Ordinal).Take(size).ToArray();
 Console.WriteLine($"Loaded {pool.Length} candidates; constructing selection.");var start=watch.Elapsed;var selection=new SynergySelection(pool,36,new("Full catalog baseline","casual60","selected","WUBRG","any","balanced",false));var ready=watch.Elapsed;Console.WriteLine($"Constructed {selection.RelationGroupCount} semantic groups in {(ready-start).TotalSeconds:F2}s; evaluating first choice.");var pick=pool.OrderByDescending(selection.Score).ThenBy(c=>c.OracleId,StringComparer.Ordinal).First();selection.Pick(pick);
 await Save(new{RecordedUtc=DateTimeOffset.UtcNow,Engine=SynergyProfiles.Version,Input=cards.Count,Size=pool.Length,ConstructionSeconds=(ready-start).TotalSeconds,FirstPickSeconds=(watch.Elapsed-ready).TotalSeconds,PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,Chosen=pick.Name});Console.WriteLine($"{pool.Length}: constructor {(ready-start).TotalSeconds:F2}s, total {watch.Elapsed.TotalSeconds:F2}s, peak {Process.GetCurrentProcess().PeakWorkingSet64/1024/1024} MiB");return;
}
if(mode=="scores")
{
 var pool=cards.Where(c=>!c.TypeLine.Contains("Land")&&SynergyProfiles.AutomaticSelectionIssue(c)is null).OrderBy(c=>Stable(c.OracleId),StringComparer.Ordinal).Take(80).ToArray();var rows=new List<object>();
 foreach(var theme in CardThemes.Choices)foreach(var style in DeckWizardRequest.Styles){var selection=new SynergySelection(pool,36,new("Compare", "casual60","selected","WUBRG",theme.Key,style.Key,false));for(var step=0;step<4;step++){rows.Add(new{Theme=theme.Key,Style=style.Key,Step=step,Scores=pool.Select(c=>new{c.OracleId,Score=selection.Score(c)}).ToArray()});selection.Pick(pool[step==3?0:step]);}}
 await Save(new{Engine=SynergyProfiles.Version,Rows=rows});Console.WriteLine("Small-pool score vectors recorded.");return;
}
throw new ArgumentException("Unknown mode.");
