using System.Text.Json;
using System.Text.Json.Serialization;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;

if(args.Length!=2)throw new ArgumentException("Annotation JSON and output resource path required.");
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0])!,".."));
var cards=new List<PaperCard>();
foreach(var name in new[]{"conflux-paper-v1","final-fantasy-starter-v1"})
{
 using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"src/MTGDeckbuilder.Application/Resources",name+".json")));
 var fetched=doc.RootElement.GetProperty("FetchedUtc").GetDateTime();cards.AddRange(doc.RootElement.GetProperty("Cards").EnumerateArray().Select(x=>ScryfallPaperCatalog.Parse(x.GetRawText(),fetched)));
}
var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,WriteIndented=true};options.Converters.Add(new JsonStringEnumConverter());
var annotations=JsonSerializer.Deserialize<Annotation[]>(await File.ReadAllTextAsync(args[0]),options)!;
var unique=cards.DistinctBy(c=>c.OracleId).OrderBy(c=>c.Name,StringComparer.Ordinal).ToArray();
if(annotations.Select(a=>a.Name).Distinct().Count()!=annotations.Length||unique.Length!=annotations.Length||annotations.Any(a=>!unique.Any(c=>c.Name==a.Name)))throw new Exception("Annotations must cover each unique product Oracle exactly once.");
var entries=unique.Select(c=>{var a=annotations.Single(a=>a.Name==c.Name);return new SynergyProfiles.Entry(c.OracleId,c.Name,SynergyProfiles.Fingerprint(c),a.Features,a.Notes);}).ToArray();
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{Version=SynergyProfiles.Version,ReviewedUtc="2026-10-28",Source="Manual supported-interaction review of unchanged Conflux and Final Fantasy Scryfall product snapshots; no general completeness claim",Cards=entries},options));
Console.WriteLine($"{entries.Length} reviewed Oracle profiles; {cards.Count} printings. No source metadata modified.");
public sealed record Annotation(string Name,SynergyFeature[] Features,string Notes);
