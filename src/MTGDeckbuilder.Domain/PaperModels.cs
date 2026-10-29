namespace MTGDeckbuilder.Domain;

public sealed class PaperPrinting
{
    private PaperPrinting() { }
    public PaperPrinting(Guid id, string metadataJson, DateTime fetchedUtc)
    { Id = Rules.Id(id); Update(metadataJson, fetchedUtc); }
    public Guid Id { get; private set; }
    public string MetadataJson { get; private set; } = "";
    public DateTime FetchedUtc { get; private set; }
    public void Update(string json, DateTime fetchedUtc) { MetadataJson = Rules.Text(json, "Kaartgegevens", 200_000); FetchedUtc = fetchedUtc; }
}
public sealed class PaperHolding
{
    private PaperHolding() { }
    public PaperHolding(string key, Guid printingId, string language, string finish, string condition, string binder, int quantity, bool demo)
    {
        Key = Rules.Text(key, "Voorraadsleutel", 64); PrintingId = Rules.Id(printingId); Language = Rules.Text(language, "Taal", 12);
        Finish = Rules.Text(finish, "Uitvoering", 20); Condition = Rules.Text(condition, "Conditie", 40); Binder = Rules.Text(binder, "Map", 200);
        IsDemo = demo; SetQuantity(quantity);
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Key { get; private set; } = "";
    public Guid PrintingId { get; private set; }
    public string Language { get; private set; } = "en";
    public string Finish { get; private set; } = "normal";
    public string Condition { get; private set; } = "unknown";
    public string Binder { get; private set; } = "Collectie";
    public bool IsDemo { get; private set; }
    public int Quantity { get; private set; }
    public Guid Version { get; private set; }
    public void SetQuantity(int quantity) { Quantity = Rules.Quantity(quantity); Version = Guid.NewGuid(); }
}
public sealed class PaperImportPlan
{
    private PaperImportPlan() { }
    public PaperImportPlan(string fileName, string sourceHash, string stockHash, string payload)
    { FileName = Rules.Text(fileName, "Bestandsnaam", 240); SourceHash = sourceHash; StockHash = stockHash; Update(payload); }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid Version { get; private set; }
    public string FileName { get; private set; } = "";
    public string SourceHash { get; private set; } = "";
    public string StockHash { get; private set; } = "";
    public string Payload { get; private set; } = "";
    public int State { get; private set; }
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public void Update(string payload) { Payload = Rules.Text(payload, "Importvoorbeeld", 24_000_000); Version = Guid.NewGuid(); }
    public void MarkApplied() { if (State != 0) throw new ArgumentException("Deze import is niet meer voorbereid."); State = 1; Version = Guid.NewGuid(); }
    public void MarkUndone() { if (State != 1) throw new ArgumentException("Deze import kan niet worden hersteld."); State = 2; Version = Guid.NewGuid(); }
}
public sealed class PaperDeck
{
    private PaperDeck() { }
    public PaperDeck(string name, bool demo = false, Guid? id = null) { Name = Rules.Text(name, "Decknaam", 120); IsDemo = demo; if (id is { } value) Id = Rules.Id(value); }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public bool IsDemo { get; private set; }
    public string Profile { get; private set; } = "casual60";
    public Guid Version { get; private set; } = Guid.NewGuid();
    public void ChangeProfile(string profile)
    { if (profile is not ("casual60" or "standard" or "commander")) throw new ArgumentException("Onbekend fysiek controleprofiel."); Profile = profile; Version = Guid.NewGuid(); }
    public void Touch() => Version = Guid.NewGuid();
}
public sealed class PaperDeckCard
{
    private PaperDeckCard() { }
    public PaperDeckCard(Guid deckId, Guid printingId, DeckZone zone, int quantity)
    { DeckId = Rules.Id(deckId); PrintingId = Rules.Id(printingId); if (!Enum.IsDefined(zone)) throw new ArgumentException("Onbekende zone."); Zone = zone; SetQuantity(quantity); }
    public Guid DeckId { get; private set; }
    public Guid PrintingId { get; private set; }
    public DeckZone Zone { get; private set; }
    public int Quantity { get; private set; }
    public void SetQuantity(int quantity) => Quantity = Rules.Quantity(quantity, false);
}
