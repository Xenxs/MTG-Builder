using System.Text.Json;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;

if(args.Length is not(2 or 3)||args.Length==3&&args[2]!="--development")throw new ArgumentException("Sealed card JSON, fresh predictions path and optional --development required.");
if(File.Exists(args[1]))throw new ArgumentException("Never overwrite first held-out predictions.");
using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var profiles=doc.RootElement.GetProperty("Cards").EnumerateArray().Select(c=>SynergyProfiles.Analyze(ScryfallPaperCatalog.Parse(c.GetRawText(),DateTime.UtcNow))).ToArray();
if(profiles.Any(p=>p.Reviewed))throw new Exception("Holdout must be outside the controlled product pool.");
var known=new List<PaperCard>();foreach(var resource in new[]{"conflux-paper-v1","final-fantasy-starter-v1"})
{using var stream=typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources."+resource+".json")!;using var data=JsonDocument.Parse(stream);known.AddRange(data.RootElement.GetProperty("Cards").EnumerateArray().Select(c=>ScryfallPaperCatalog.Parse(c.GetRawText(),DateTime.UtcNow)));}
var reference=known.DistinctBy(c=>c.OracleId).Select(SynergyProfiles.Analyze).ToArray();
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{RecordedUtc=DateTimeOffset.UtcNow,Version=SynergyProfiles.Version,Note=args.Length==3?"Development regression; this input was inspected and used for parser design. Not unseen.":"Frozen implementation predictions before manual Oracle scoring; no independent reviewer.",Cards=profiles.Select(p=>new{p.Card.OracleId,p.Card.Name,p.Card.ScryfallUrl,p.Card.OracleText,p.Card.TypeLine,p.Status,Features=p.Features.Select(x=>x.ToString()).ToArray(),Themes=CardThemes.Choices.Where(t=>t.Key!="any"&&p.Theme(t.Key)).Select(t=>t.Key),Relations=reference.SelectMany(r=>SynergyEngine.Between(p,r).Concat(SynergyEngine.Between(r,p))).Where(x=>x.Weight>1).ToArray()})},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{profiles.Length} predictions recorded; {profiles.Count(p=>p.Features.Any(SynergyProfiles.IsSemantic))} with limited semantic features; no texts printed.");
