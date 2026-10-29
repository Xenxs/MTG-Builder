using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;

if (args.Length != 3) throw new ArgumentException("Specify fresh isolated data directory, example CSV, report path.");
var paths = new AppPaths(Path.GetFullPath(args[0]));
if (File.Exists(paths.DatabasePath)) throw new ArgumentException("Use a fresh isolated probe directory.");
var services = new ServiceCollection(); services.AddLogging(); services.AddDeckbuilder(paths);
await using var provider = services.BuildServiceProvider();
await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
var store = provider.GetRequiredService<IPaperWorkspace>(); var catalog = provider.GetRequiredService<IPaperCatalog>();
await store.InitializeExampleAsync(); var before = await store.SummaryAsync();
if (before.Copies != 202 || before.Printings != 109 || !before.DemoActive) throw new Exception("Unexpected four-product seed.");
var card = await catalog.GetAsync((await store.CollectionAsync())[0].Card.Id, true);
var image = await catalog.ImageAsync(card, false) ?? throw new Exception("No image.");
var preview = await store.PrepareCsvAsync(Path.GetFullPath(args[1]));
if (!preview.CanApply || preview.Rows.Count != 118) throw new Exception("Conflux CSV did not resolve exactly: " + preview.Summary);
await store.ApplyAsync(preview.Id, preview.Version); var imported = await store.SummaryAsync();
if (imported.Copies != 202 || imported.DemoActive) throw new Exception("Wrong imported ownership.");
var decks = await store.DecksAsync(); var sizes = new List<object>();
foreach (var d in decks)
{
    var report = await store.DeckAsync(d.Id); if (report.Lines.Sum(x => x.Quantity) != (d.Name.StartsWith("FINAL FANTASY", StringComparison.Ordinal) ? 60 : 41) || report.Problems.Any(x => x.Contains("tekort"))) throw new Exception("Wrong Conflux deck.");
    sizes.Add(new { d.Name, Cards = report.Lines.Sum(x => x.Quantity), report.Problems, report.Analysis });
}
var draft = await store.PrepareDeckCopyAsync(decks[0].Id, "Fysieke praktijkkopie"); await store.SaveDeckDraftAsync(draft);
var proposal = await store.PrepareProposalAsync("Fysiek 60-kaart praktijkvoorstel", false);
if (proposal.Lines.Sum(x => x.Quantity) != 60 || proposal.Report.Problems.Count != 0) throw new Exception("Physical owned proposal invalid.");
await store.SaveDeckDraftAsync(proposal);
var commander = (await store.CollectionAsync()).Single(x => x.Card.Name == "Cloud, Planet's Champion").Card;
string ownedCommanderBlock;
try { await store.PrepareCommanderAsync("Owned Commander must block", commander.Id, false); throw new Exception("Unexpected owned Commander success for insufficient starter singleton pool."); }
catch (ArgumentException ex) { ownedCommanderBlock = ex.Message; }
var commanderDraft = await store.PrepareCommanderAsync("Cloud Commander — planning", commander.Id, true);
if (commanderDraft.Lines.Sum(x => x.Quantity) != 100 || commanderDraft.Report.Problems.Any(x => !x.Contains("tekort", StringComparison.Ordinal))) throw new Exception("Commander planning failed supported legality/structure checks.");
await store.SaveDeckDraftAsync(commanderDraft);
var backups = provider.GetRequiredService<IBackupService>(); var backup = await backups.CreateAsync();
await store.SetOwnedAsync(card.Id, 9); var restore = await backups.InspectAsync(backup.Path); await backups.ScheduleAsync(restore.Id, true);
SqliteConnection.ClearAllPools(); using (var lease = DataDirectoryLease.Acquire(paths)) await provider.GetRequiredService<SqliteBackupService>().ApplyPendingAsync(lease);
await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(); var restored = await store.SummaryAsync();
if (restored.Copies != 202 || restored.Decks != 7) throw new Exception("Physical restore mismatch.");
await store.UndoAsync(preview.Id); if (!(await store.SummaryAsync()).DemoActive) throw new Exception("Receipt did not survive restore.");
var reportPath = Path.GetFullPath(args[2]); Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { CheckedUtc = DateTimeOffset.UtcNow, Source = "Real Scryfall; simulated ownership, not a phone scan", Before = before, Imported = imported, Restored = restored, CsvRows = preview.Rows.Count,
    Image = new { Card = card.Label, card.ScryfallUrl, File = image, Bytes = new FileInfo(image).Length, Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(image))) }, Decks = sizes,
    OwnedProposal = new { Cards = proposal.Lines.Sum(x => x.Quantity), BasicLands = proposal.Lines.Where(x => x.Card.IsBasicLand).Sum(x => x.Quantity), proposal.Report.Problems, List = proposal.Lines.Select(x => x.Label) }, Commander = new { Name = commander.Name, OwnedModeBlocked = ownedCommanderBlock, Cards = commanderDraft.Lines.Sum(x => x.Quantity), Missing = commanderDraft.Report.Problems, List = commanderDraft.Lines.Select(x => x.Label) }, BackupRestored = true, ImportUndoAfterRestore = true }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(reportPath);
