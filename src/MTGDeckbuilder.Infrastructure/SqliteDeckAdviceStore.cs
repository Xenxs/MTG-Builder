using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteDeckAdviceStore(IDbContextFactory<DeckbuilderDbContext> factory,
    DatabaseWriteLock writes, TimeProvider clock) : IDeckAdviceStore
{
    public Task<DeckAdvice> GetAsync(Guid deckId, string? expectedFingerprint = null, IReadOnlyList<AdviceChoice>? choices = null, CancellationToken ct = default)
    {
        var selection = choices?.ToArray() ?? [];
        return ReadAsync(deckId, (snapshot, hash) =>
        {
            Check(expectedFingerprint, hash);
            if (selection.Length > 0 && expectedFingerprint is null) throw new ArgumentException("Bereken eerst een actueel advies.");
            return DeckAdviceEngine.Build(snapshot, hash, selection, clock.GetUtcNow().UtcDateTime);
        }, ct);
    }
    public Task<IReadOnlyList<AdviceCandidate>> CandidatesAsync(Guid deckId, string fingerprint,
        IReadOnlyList<AdviceChoice> choices, Guid printingId, DeckZone zone, CancellationToken ct = default)
    {
        var selection = choices.ToArray();
        return ReadAsync(deckId, (snapshot, hash) =>
        {
            Check(fingerprint, hash);
            return DeckAdviceEngine.Candidates(snapshot, hash, selection, printingId, zone, clock.GetUtcNow().UtcDateTime, ct);
        }, ct);
    }
    public Task<AdvicePreview> PreviewAsync(Guid deckId, string fingerprint, IReadOnlyList<AdviceChoice> choices, string name, CancellationToken ct = default)
    {
        var selection = choices.ToArray();
        return Task.Run(async () =>
        {
            AdvicePreview? preview = null;
            await writes.RunAsync(async () =>
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var snapshot = await LoadAsync(db, deckId, ct); var hash = Fingerprint(snapshot); Check(fingerprint, hash);
                var advice = DeckAdviceEngine.Build(snapshot, hash, selection, clock.GetUtcNow().UtcDateTime);
                if (!advice.CanPreview) throw new ArgumentException("Kies minstens één geldige vervanging voordat je een voorbeeld maakt.");
                var payload = new AdvicePlanPayload(1, DeckAdviceEngine.Version, CardRoleClassifier.Version, selection, advice.Changes.ToArray());
                var plan = new DeckAdvicePlan(deckId, hash, name, JsonSerializer.Serialize(payload));
                db.DeckAdvicePlans.Add(plan); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                preview = new(plan.Id, plan.Name, advice);
            }, ct);
            return preview!;
        }, ct);
    }
    public Task<Guid> ApplyAsync(Guid previewId, CancellationToken ct = default) => Task.Run(async () =>
    {
        var result = Guid.Empty;
        await writes.RunAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var plan = await db.DeckAdvicePlans.SingleOrDefaultAsync(x => x.Id == previewId, ct)
                ?? throw new ArgumentException("Voorbeeld niet gevonden. Bereken het advies opnieuw.");
            if (plan.AppliedDeckId is { } applied) { result = applied; return; }
            var snapshot = await LoadAsync(db, plan.SourceDeckId, ct); var hash = Fingerprint(snapshot); Check(plan.Fingerprint, hash);
            var payload = JsonSerializer.Deserialize<AdvicePlanPayload>(plan.ChoicesJson) ?? throw new ArgumentException("Ongeldig voorstel.");
            if (payload.SchemaVersion != 1 || payload.EngineVersion != DeckAdviceEngine.Version || payload.ClassifierVersion != CardRoleClassifier.Version)
                throw new ArgumentException("Dit voorstel gebruikt een andere adviesversie. Bereken het advies opnieuw.");
            var advice = DeckAdviceEngine.Build(snapshot, hash, payload.Choices, clock.GetUtcNow().UtcDateTime);
            if (!advice.CanPreview) throw new ArgumentException("Het voorstel is niet meer geldig. Bereken opnieuw.");
            var deck = new Deck(plan.Name, snapshot.Format); deck.SetValidationProfile(snapshot.Profile);
            db.Decks.Add(deck);
            db.DeckCards.AddRange(advice.Lines.Select(x => new DeckCard(deck.Id, x.PrintingId, x.Zone, x.Quantity)));
            plan.MarkApplied(deck.Id);
            await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested(); await tx.CommitAsync(ct);
            result = deck.Id;
        }, ct);
        return result;
    }, ct);

    private Task<T> ReadAsync<T>(Guid deckId, Func<AdviceSnapshot, string, T> read, CancellationToken ct) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var snapshot = await LoadAsync(db, deckId, ct);
        ct.ThrowIfCancellationRequested(); return read(snapshot, Fingerprint(snapshot));
    }, ct);

    private static void Check(string? expected, string actual)
    {
        if (expected is not null && expected != actual)
            throw new ArgumentException("Deck, collectie of catalogus is gewijzigd sinds dit voorstel. Bereken het advies opnieuw.");
    }
    private static string Fingerprint(AdviceSnapshot snapshot) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { Snapshot = snapshot, Algorithm = DeckAdviceEngine.Version, Rules = StandardDeckValidator.RulesVersion })));

    private static async Task<AdviceSnapshot> LoadAsync(DeckbuilderDbContext db, Guid deckId, CancellationToken ct)
    {
        var deck = await db.Decks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deckId, ct) ?? throw new ArgumentException("Het oorspronkelijke deck bestaat niet meer.");
        var run = await db.CatalogImports.AsNoTracking().Where(x => x.State == CatalogImportState.Completed)
            .OrderByDescending(x => x.CompletedUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var source = run is null ? new LegalitySnapshot(null, "Geen catalogus", "—", null, null, 0)
            : new LegalitySnapshot(run.Id, run.Source, run.SourceVersion,
                run.SourceUpdatedUtc is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null,
                run.CompletedUtc is { } i ? DateTime.SpecifyKind(i, DateTimeKind.Utc) : null, run.MetadataVersion);
        var lines = await db.DeckCards.AsNoTracking().Where(x => x.DeckId == deckId)
            .Select(x => new AdviceLine(x.PrintingId, x.Zone, x.Quantity)).ToListAsync(ct);
        // Include zero-count rows and their versions so even intervening edits invalidate a preview.
        var stock = await db.CollectionEntries.AsNoTracking().Select(x => new AdviceStock(x.PrintingId, x.Quantity, x.Version)).ToListAsync(ct);
        var ids = lines.Select(x => x.PrintingId).Concat(stock.Where(x => x.Quantity > 0).Select(x => x.PrintingId)).Distinct().ToArray();
        var raw = await (from p in db.Printings.AsNoTracking() join c in db.Cards.AsNoTracking() on p.CardId equals c.Id
            where ids.Contains(p.Id)
            select new { p.Id, p.CardId, c.OracleId, c.Name, p.SetCode, p.CollectorNumber, c.ColorIdentity, c.ManaValue,
                c.TypeLine, c.OracleText, c.ManaCost, p.IsInCatalog, p.LastSeenImportId }).ToListAsync(ct);
        var cardIds = raw.Select(x => x.CardId).Distinct().ToArray();
        var legalities = await db.CardLegalities.AsNoTracking().Where(x => cardIds.Contains(x.CardId)).ToDictionaryAsync(x => x.CardId, ct);
        var printings = raw.Select(x => new AdvicePrinting(x.Id, x.OracleId, x.Name, x.SetCode, x.CollectorNumber,
            x.ColorIdentity, x.ManaValue, x.TypeLine, x.OracleText,
            legalities.TryGetValue(x.CardId, out var l) && l.RunId == run?.Id ? l.Standard : CardLegality.Unknown,
            x.IsInCatalog && x.LastSeenImportId == run?.Id, x.ManaCost)).OrderBy(x => x.Id).ToArray();
        return new(deck.Id, deck.Name, deck.Format, deck.ValidationProfile, source, printings,
            lines.OrderBy(x => x.Zone).ThenBy(x => x.PrintingId).ToArray(), stock.OrderBy(x => x.PrintingId).ToArray());
    }
    private sealed record AdvicePlanPayload(int SchemaVersion, string EngineVersion, string ClassifierVersion,
        AdviceChoice[] Choices, AdviceChange[] ReviewedChanges);
}
