namespace MTGDeckbuilder.Domain;

public static class Rules
{
    public static string Text(string value, string label, int max)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = value.Trim();
        if (value.Length == 0 || value.Length > max)
            throw new ArgumentException($"{label} moet 1 tot {max} tekens bevatten.");
        return value;
    }

    public static int Quantity(int value, bool allowZero = true)
    {
        if (value < (allowZero ? 0 : 1) || value > 9999)
            throw new ArgumentOutOfRangeException(nameof(value), "Aantal moet tussen 0 en 9999 liggen (deckkaarten minimaal 1).");
        return value;
    }

    public static Guid Id(Guid value) => value != Guid.Empty ? value : throw new ArgumentException("Een geldig ID is vereist.");
}

public sealed class Card
{
    private Card() { }
    public Card(string name)
    {
        Name = Rules.Text(name, "Kaartnaam", 200);
        SearchName = Name.ToUpperInvariant();
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string SearchName { get; private set; } = "";
    public string? OracleId { get; private set; }
    public string? ManaCost { get; private set; }
    public string? TypeLine { get; private set; }
    public string? OracleText { get; private set; }
    public string ColorIdentity { get; private set; } = "";
    public double ManaValue { get; private set; }

    public bool UpdateCatalog(string oracleId, string name, string manaCost, string typeLine,
        string oracleText, string colorIdentity, double manaValue)
    {
        if (!Guid.TryParse(oracleId, out var parsed) || parsed == Guid.Empty)
            throw new ArgumentException("Ongeldige Oracle-identiteit.");
        if (OracleId is not null && OracleId != parsed.ToString())
            throw new ArgumentException("Een bestaande kaart mag niet van Oracle-identiteit veranderen.");
        name = Rules.Text(name, "Kaartnaam", 200);
        if (!double.IsFinite(manaValue) || manaValue < 0) throw new ArgumentException("Ongeldige manawaarde.");
        if (colorIdentity.Any(c => !"WUBRG".Contains(c))) throw new ArgumentException("Ongeldige kleuridentiteit.");
        colorIdentity = string.Concat("WUBRG".Where(colorIdentity.Contains));
        var changed = OracleId != parsed.ToString() || Name != name || ManaCost != manaCost ||
            TypeLine != typeLine || OracleText != oracleText || ColorIdentity != colorIdentity || ManaValue != manaValue;
        OracleId = parsed.ToString(); Name = name; SearchName = name.ToUpperInvariant(); ManaCost = manaCost;
        TypeLine = typeLine; OracleText = oracleText; ColorIdentity = colorIdentity; ManaValue = manaValue;
        return changed;
    }
}

public sealed class Printing
{
    private Printing() { }
    public Printing(Guid cardId, string setCode, string collectorNumber)
    {
        CardId = Rules.Id(cardId);
        SetCode = Rules.Text(setCode, "Setcode", 12).ToUpperInvariant();
        CollectorNumber = Rules.Text(collectorNumber, "Collectornummer", 20).ToUpperInvariant();
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CardId { get; private set; }
    public string SetCode { get; private set; } = "";
    public string CollectorNumber { get; private set; } = "";
    public int? ArenaId { get; private set; }
    public string? ScryfallId { get; private set; }
    public string Language { get; private set; } = "en";
    public string SetName { get; private set; } = "";
    public string Rarity { get; private set; } = "";
    public bool IsInCatalog { get; private set; }
    public Guid? LastSeenImportId { get; private set; }

    public bool UpdateCatalog(Guid cardId, string scryfallId, int? arenaId, string language,
        string setName, string rarity, Guid importId)
    {
        cardId = Rules.Id(cardId);
        importId = Rules.Id(importId);
        if (!Guid.TryParse(scryfallId, out var parsed) || parsed == Guid.Empty)
            throw new ArgumentException("Ongeldige Scryfall-identiteit.");
        if (ScryfallId is not null && ScryfallId != parsed.ToString())
            throw new ArgumentException("Een printing mag niet van Scryfall-identiteit veranderen.");
        if (arenaId is <= 0) throw new ArgumentException("Ongeldige Arena-identiteit.");
        language = Rules.Text(language, "Taal", 12).ToLowerInvariant();
        setName = Rules.Text(setName, "Setnaam", 200);
        rarity = Rules.Text(rarity, "Zeldzaamheid", 20).ToLowerInvariant();
        var changed = CardId != cardId || ScryfallId != parsed.ToString() || ArenaId != arenaId ||
            Language != language || SetName != setName || Rarity != rarity || !IsInCatalog;
        CardId = cardId; ScryfallId = parsed.ToString(); ArenaId = arenaId;
        Language = language; SetName = setName; Rarity = rarity;
        IsInCatalog = true; LastSeenImportId = importId;
        return changed;
    }
}

public sealed class CollectionEntry
{
    private CollectionEntry() { }
    public CollectionEntry(Guid printingId, int quantity)
    {
        PrintingId = Rules.Id(printingId);
        SetQuantity(quantity);
    }
    public Guid PrintingId { get; private set; }
    public int Quantity { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public void SetQuantity(int value)
    {
        Quantity = Rules.Quantity(value);
        Version = Guid.NewGuid();
    }
}

public enum DeckZone { Mainboard, Sideboard, Commander }

public sealed class Deck
{
    private Deck() { }
    public Deck(string name, string format)
    {
        Name = Rules.Text(name, "Decknaam", 120);
        Format = Rules.Text(format, "Formaat", 40);
        ValidationProfile = DeckRuleProfiles.FromExplicitLabel(Format);
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string Format { get; private set; } = "";
    public DeckRuleProfile ValidationProfile { get; private set; }
    public void SetValidationProfile(DeckRuleProfile profile)
    {
        if (!Enum.IsDefined(profile)) throw new ArgumentException("Onbekend controleprofiel.");
        ValidationProfile = profile;
    }
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
}

public sealed class DeckCard
{
    private DeckCard() { }
    public DeckCard(Guid deckId, Guid printingId, DeckZone zone, int quantity)
    {
        DeckId = Rules.Id(deckId);
        PrintingId = Rules.Id(printingId);
        if (!Enum.IsDefined(zone)) throw new ArgumentException("Ongeldige deckzone.");
        Zone = zone;
        SetQuantity(quantity);
    }
    public Guid DeckId { get; private set; }
    public Guid PrintingId { get; private set; }
    public DeckZone Zone { get; private set; }
    public int Quantity { get; private set; }
    public void SetQuantity(int value) => Quantity = Rules.Quantity(value, false);
}

public sealed class UserSettings
{
    public int Id { get; private set; } = 1;
    public string PlayerName { get; private set; } = "Planeswalker";
    public bool ExamplesEnabled { get; private set; } = true;
    public void DisableExamples() => ExamplesEnabled = false;
    public void SetPlayerName(string value) => PlayerName = Rules.Text(value, "Spelersnaam", 80);
}
