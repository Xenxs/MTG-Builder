using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteDeckGenerationStore(IDbContextFactory<DeckbuilderDbContext> factory,
    DatabaseWriteLock writes, TimeProvider clock) : IDeckGenerationStore
{
    public Task<GeneratedDeck> CalculateAsync(GenerationMode mode, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var input = await SqliteArchetypePoolStore.LoadInputAsync(db, ct);
        ct.ThrowIfCancellationRequested();
        return DeckGenerationEngine.Build(input, Fingerprint(input), mode, clock.GetUtcNow().UtcDateTime);
    }, ct);

    public Task<GenerationPreview> PreviewAsync(string fingerprint, GenerationMode mode, string name, CancellationToken ct = default) => Task.Run(async () =>
    {
        GenerationPreview? result = null;
        await writes.RunAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var input = await SqliteArchetypePoolStore.LoadInputAsync(db, ct); Check(fingerprint, Fingerprint(input));
            var deck = DeckGenerationEngine.Build(input, fingerprint, mode, clock.GetUtcNow().UtcDateTime);
            if (!deck.CanPreview) throw new ArgumentException("Geen compleet geldig voorstel. Bereken opnieuw en controleer de blokkades.");
            var payload = new Payload(1, DeckGenerationEngine.Version, MonoRedPool.Current.Version, mode, deck.Lines.ToArray(), deck);
            var plan = new DeckGenerationPlan(fingerprint, name, JsonSerializer.Serialize(payload));
            db.DeckGenerationPlans.Add(plan); await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested(); await tx.CommitAsync(ct);
            result = new(plan.Id, plan.Name, deck);
        }, ct);
        return result!;
    }, ct);

    public Task<Guid> ApplyAsync(Guid previewId, CancellationToken ct = default) => Task.Run(async () =>
    {
        var result = Guid.Empty;
        await writes.RunAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var plan = await db.DeckGenerationPlans.SingleOrDefaultAsync(x => x.Id == previewId, ct)
                ?? throw new ArgumentException("Voorbeeld niet gevonden; bereken opnieuw.");
            if (plan.AppliedDeckId is { } applied) { result = applied; return; }
            var input = await SqliteArchetypePoolStore.LoadInputAsync(db, ct); Check(plan.Fingerprint, Fingerprint(input));
            var payload = JsonSerializer.Deserialize<Payload>(plan.ReviewedJson) ?? throw new ArgumentException("Ongeldig deckvoorbeeld.");
            if (payload.SchemaVersion != 1 || payload.EngineVersion != DeckGenerationEngine.Version || payload.PoolVersion != MonoRedPool.Current.Version)
                throw new ArgumentException("Andere generator-/poolversie; bereken opnieuw.");
            var proposal = DeckGenerationEngine.Build(input, plan.Fingerprint, payload.Mode, clock.GetUtcNow().UtcDateTime);
            if (!proposal.CanPreview || JsonSerializer.Serialize(proposal.Lines) != JsonSerializer.Serialize(payload.ReviewedLines))
                throw new ArgumentException("Voorstel niet meer geldig of gewijzigd; bereken opnieuw.");
            var deck = new Deck(plan.Name, DeckRuleProfiles.Bo1);
            db.Decks.Add(deck); db.DeckCards.AddRange(proposal.Lines.Select(x => new DeckCard(deck.Id, x.Printing.Id, DeckZone.Mainboard, x.Quantity)));
            plan.MarkApplied(deck.Id);
            await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested(); await tx.CommitAsync(ct);
            result = deck.Id;
        }, ct);
        return result;
    }, ct);

    private static string Fingerprint(ArchetypePoolInput input) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        Input = input, Definition = MonoRedPool.Current, Engine = DeckGenerationEngine.Version,
        PoolEngine = ArchetypePoolEngine.Version, Rules = StandardDeckValidator.RulesVersion
    })));
    private static void Check(string expected, string actual)
    {
        if (expected != actual) throw new ArgumentException("Collectie, catalogus of generator is gewijzigd sinds dit voorstel. Bereken opnieuw.");
    }
    private sealed record Payload(int SchemaVersion, string EngineVersion, string PoolVersion, GenerationMode Mode,
        GeneratedLine[] ReviewedLines, GeneratedDeck ReviewedDeck);
}
