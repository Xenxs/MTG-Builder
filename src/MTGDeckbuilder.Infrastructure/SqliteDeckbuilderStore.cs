using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteDeckbuilderStore(IDbContextFactory<DeckbuilderDbContext> factory) : IDeckbuilderStore
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return new DashboardSummary(
            await db.CollectionEntries.CountAsync(x => x.Quantity > 0, ct),
            await db.CollectionEntries.SumAsync(x => x.Quantity, ct),
            await db.Decks.CountAsync(ct));
    }

    public async Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (from entry in db.CollectionEntries.AsNoTracking()
                      join printing in db.Printings on entry.PrintingId equals printing.Id
                      join card in db.Cards on printing.CardId equals card.Id
                      orderby card.Name, printing.SetCode, printing.CollectorNumber
                      select new CollectionItem(printing.Id, card.Name, printing.SetCode, printing.CollectorNumber, entry.Quantity)).ToListAsync(ct);
    }

    // One SaveChanges transaction atomically stores the card, printing and owned count.
    public async Task SaveCollectionAsync(CollectionInput input, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var matches = await db.Printings.Where(x => x.SetCode == input.SetCode && x.CollectorNumber == input.CollectorNumber && x.Language == "en").ToListAsync(ct);
        if (matches.Count > 1) throw new ArgumentException("Deze printing heeft meerdere varianten. Kies de juiste printing in Catalogus.");
        var printing = matches.SingleOrDefault();
        if (printing is null)
        {
            var upperName = input.Name.ToUpperInvariant();
            var card = await db.Cards.FirstOrDefaultAsync(x => x.SearchName == upperName && x.OracleId == null, ct);
            if (card is null)
            {
                card = new Card(input.Name);
                db.Cards.Add(card);
            }
            printing = new Printing(card.Id, input.SetCode, input.CollectorNumber);
            db.Printings.Add(printing);
        }
        else
        {
            var name = await db.Cards.Where(x => x.Id == printing.CardId).Select(x => x.Name).SingleAsync(ct);
            if (!string.Equals(name, input.Name, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Deze setcode en dit collectornummer horen al bij een andere kaart.");
        }
        var entry = await db.CollectionEntries.FindAsync([printing.Id], ct);
        if (entry is null) db.CollectionEntries.Add(new CollectionEntry(printing.Id, input.Quantity));
        else entry.SetQuantity(input.Quantity);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetCollectionQuantityAsync(Guid printingId, int quantity, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entry = await db.CollectionEntries.FindAsync([printingId], ct)
            ?? throw new ArgumentException("Deze kaart staat niet in de collectie.");
        entry.SetQuantity(quantity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DeckItem>> GetDecksAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Decks.AsNoTracking().OrderBy(x => x.Name).Select(x => new DeckItem(x.Id, x.Name, x.Format, x.ValidationProfile)).ToListAsync(ct);
    }

    public async Task<Guid> CreateDeckAsync(string name, string format, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var deck = new Deck(name, format);
        db.Decks.Add(deck);
        await db.SaveChangesAsync(ct);
        return deck.Id;
    }

    public async Task DeleteDeckAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var deck = await db.Decks.FindAsync([id], ct) ?? throw new ArgumentException("Deck bestaat niet meer.");
        db.Decks.Remove(deck);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DeckCardItem>> GetDeckCardsAsync(Guid deckId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (from entry in db.DeckCards.AsNoTracking()
                      join printing in db.Printings on entry.PrintingId equals printing.Id
                      join card in db.Cards on printing.CardId equals card.Id
                      where entry.DeckId == deckId
                      orderby entry.Zone, card.Name
                      select new DeckCardItem(printing.Id, card.Name, printing.SetCode, printing.CollectorNumber, entry.Zone, entry.Quantity)).ToListAsync(ct);
    }

    public async Task SetDeckCardAsync(Guid deckId, Guid printingId, DeckZone zone, int quantity, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Decks.AnyAsync(x => x.Id == deckId, ct)) throw new ArgumentException("Deck bestaat niet meer.");
        if (!await db.Printings.AnyAsync(x => x.Id == printingId, ct)) throw new ArgumentException("Kaart bestaat niet.");
        var entry = await db.DeckCards.FindAsync([deckId, printingId, zone], ct);
        if (quantity == 0)
        {
            if (entry is not null) db.DeckCards.Remove(entry);
        }
        else if (entry is null) db.DeckCards.Add(new DeckCard(deckId, printingId, zone, quantity));
        else entry.SetQuantity(quantity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> GetPlayerNameAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Settings.Select(x => x.PlayerName).SingleOrDefaultAsync(ct) ?? "Planeswalker";
    }

    public async Task SavePlayerNameAsync(string name, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var settings = await db.Settings.FindAsync([1], ct);
        if (settings is null) { settings = new UserSettings(); db.Settings.Add(settings); }
        settings.SetPlayerName(name);
        await db.SaveChangesAsync(ct);
    }
}
