using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;

if(args.Length!=2)throw new ArgumentException("Existing data directory and report path required.");
var paths=new AppPaths(Path.GetFullPath(args[0]));if(!File.Exists(paths.DatabasePath))throw new ArgumentException("Read-only inspection requires existing database.");
var s=new ServiceCollection();s.AddLogging();s.AddDeckbuilder(paths);s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(new ReadOnlyFactory(paths));
await using var provider=s.BuildServiceProvider();var workspace=provider.GetRequiredService<IPaperWorkspace>();var cards=await workspace.CollectionAsync();var summary=await workspace.SummaryAsync();
var reports=new List<object>();foreach(var deck in await workspace.DecksAsync())
{var report=await workspace.DeckAsync(deck.Id);reports.Add(new{deck.Id,deck.Name,deck.Profile,report.Summary,report.Synergy});}
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{CheckedUtc=DateTimeOffset.UtcNow,ReadOnlySqlite=true,NoInitializerOrSeederCalled=true,Database=paths.DatabasePath,summary,ActiveOracleCards=cards.Select(x=>x.Card.OracleId).Distinct().Count(),Cards=cards.Select(x=>new{x.Card.Name,x.Card.OracleId,x.Quantity,Profile=SynergyProfiles.Analyze(x.Card).Status}),Decks=reports},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Read-only: {summary.Copies} copies, {summary.Printings} printings, {summary.Decks} decks. No initialization, seeding, network, or database writes.");
public sealed class ReadOnlyFactory(AppPaths paths):IDbContextFactory<DeckbuilderDbContext>
{public DeckbuilderDbContext CreateDbContext()=>new(new DbContextOptionsBuilder<DeckbuilderDbContext>().UseSqlite(new SqliteConnectionStringBuilder{DataSource=paths.DatabasePath,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()).Options);}
