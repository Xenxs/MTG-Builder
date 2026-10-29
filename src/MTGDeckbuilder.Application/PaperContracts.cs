using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record PaperCard(Guid Id, string OracleId, string Name, string Set, string Number, string Language,
    string ManaCost, double ManaValue, string TypeLine, string OracleText, string Colors, string Standard,
    string ImageFront, string ImageBack, string ScryfallUrl, string[] Finishes, DateTime FetchedUtc,
    string CommanderLegality = "unknown", bool HasPowerToughness = false, string SetName = "", string? PrintedColors = null,
    string Power = "", string Toughness = "", bool RequiresUnsupportedRules = false)
{
    public string Label => $"{Name} · {Set.ToUpperInvariant()} {Number} · {Language}";
    public bool IsBasicLand => TypeLine.Split('—')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("Basic") && TypeLine.Contains("Land", StringComparison.Ordinal);
    public bool IsCommanderCandidate => !OracleText.Contains("choose a color before the game begins", StringComparison.OrdinalIgnoreCase) &&
        (TypeLine.Split("//")[0].Contains("Legendary", StringComparison.Ordinal) &&
            (TypeLine.Split("//")[0].Contains("Creature", StringComparison.Ordinal) || TypeLine.Split("//")[0].Contains("Vehicle", StringComparison.Ordinal) || TypeLine.Split("//")[0].Contains("Spacecraft", StringComparison.Ordinal) && HasPowerToughness) ||
         OracleText.Split("\n//\n")[0].Contains("can be your commander", StringComparison.OrdinalIgnoreCase));
    public string Details => $"{ManaCost} · {TypeLine}\n{OracleText}\nBron: Scryfall · {FetchedUtc:yyyy-MM-dd HH:mm} UTC\n" +
        $"Standard (papier): {Standard} · Commander: {CommanderLegality}.\n{SynergyProfiles.Analyze(this).Details}";
    public AdvicePrinting AsAdvice() => new(Id, OracleId, Name, Set, Number, Colors, ManaValue, TypeLine, OracleText,
        Standard == "legal" ? CardLegality.Legal : CardLegality.Unknown, false, ManaCost);
}
public sealed record PaperOwned(PaperCard Card, int Quantity, string Positions = "")
{ public string Label => $"{Quantity}× {Card.Label}"; }
public sealed record PaperStock(Guid Id, string Key, Guid PrintingId, string Language, string Finish, string Condition,
    string Binder, int Quantity, bool IsDemo, Guid Version);
public sealed record PaperSourceRow(int Number, Guid? ScryfallId, string Name, string Set, string Collector,
    string Language, string Finish, string Condition, string Binder, int Quantity, bool IsProxy, bool IsList, string Raw);
public sealed record PaperImportRow(PaperSourceRow Source, Guid? PrintingId, bool Skipped, string Problem, bool Explicit = false)
{
    public string Label => $"{Source.Number}. {Source.Quantity}× {(Source.Name.Length > 0 ? Source.Name : Source.ScryfallId?.ToString())} · {Source.Set} {Source.Collector} · {Source.Finish} · {Source.Language} · {Source.Condition} · {Source.Binder}";
    public string Details => $"Gekoppelde Scryfall-uitgave: {PrintingId}\n" + string.Join(" · ", (JsonSerializer.Deserialize<Dictionary<string,string>>(Source.Raw) ?? []).Select(x => $"{x.Key}: {x.Value}"));
    public string Status => Skipped ? "Overgeslagen; telt niet mee in deze import" : Problem.Length > 0 ? Problem : Explicit ? "Expliciet gekoppeld" : "Exacte uitgave gekoppeld";
}
public sealed record PaperImportPreview(Guid Id, Guid Version, string FileName, int State, IReadOnlyList<PaperImportRow> Rows)
{
    public bool CanApply => State == 0 && Rows.Any(x => !x.Skipped) && Rows.All(x => x.Skipped || x.PrintingId is not null && x.Problem.Length == 0);
    public string Summary => $"{FileName} · {Rows.Count} regels · {Rows.Where(x => !x.Skipped).Sum(x => x.Source.Quantity)} exemplaren · {Rows.Count(x => x.Skipped)} overgeslagen · {Rows.Count(x => !x.Skipped && x.Problem.Length > 0)} op te lossen · " +
        $"{Rows.Where(x => !x.Skipped && x.PrintingId is not null).GroupBy(x => PaperIdentity.Key(x.PrintingId!.Value, x.Source.Language, x.Source.Finish, x.Source.Condition, x.Source.Binder)).Count(g => g.Count() > 1)} dubbele posities worden opgeteld · " +
        (State == 0 ? "Voorbeeld; nog geen bezit gewijzigd" : State == 1 ? "Toegepast" : "Hersteld");
}
public sealed record PaperImportHistory(Guid Id, string FileName, int State, DateTime CreatedUtc)
{ public string Label => $"{CreatedUtc.ToLocalTime():dd-MM HH:mm} · {FileName} · {(State == 0 ? "Voorbereid" : State == 1 ? "Toegepast" : "Hersteld")}"; }
public sealed record PaperDeckInfo(Guid Id, string Name, string Profile, bool IsDemo)
{ public string Label => Name + (IsDemo ? " · productvoorbeeld" : ""); }
public sealed record PaperDeckLine(PaperCard Card, DeckZone Zone, int Quantity, int Owned)
{ public string Label => $"{Quantity}× {Card.Label} · {Zone} · bezit totaal {Owned}"; }
public sealed record PaperDeckReport(PaperDeckInfo Deck, Guid Version, IReadOnlyList<PaperDeckLine> Lines,
    IReadOnlyList<string> Problems, string Analysis, SynergyAssessment? Synergy = null)
{
    public string Summary => $"{Lines.Where(x => x.Zone == DeckZone.Mainboard).Sum(x => x.Quantity)} mainboard · " +
        $"{Lines.Where(x => x.Zone == DeckZone.Sideboard).Sum(x => x.Quantity)} sideboard · " +
        (Deck.Profile == "commander" ? $"{Lines.Where(x => x.Zone == DeckZone.Commander).Sum(x => x.Quantity)} commander · " : "") +
        (Problems.Count == 0 ? "Ondersteunde controles geslaagd" : $"{Problems.Count} aandachtspunten");
    public string Details => string.Join("\n", Problems) + "\n\n" + Analysis;
}
public sealed record PaperAlternative(PaperCard Card, int Owned, string Explanation)
{ public string Label => $"{Card.Name} · {Owned}× bezit · {Explanation}"; }
public sealed record PaperDeckDraft(Guid Id, string Name, string Profile, IReadOnlyList<PaperDeckLine> Lines,
    string StockFingerprint, string CardsFingerprint, PaperDeckReport Report)
{ public string Details => Report.Details + "\n\nVolledige voorgestelde lijst:\n" + string.Join("\n", Lines.Select(x => x.Label)); }
public sealed record PaperSummary(int Printings, int Copies, int Decks, bool DemoActive)
{ public string Label => $"{Copies} fysieke exemplaren · {Printings} printings · {Decks} decks · " + (DemoActive ? "Gesimuleerd testbezit; geen echte scan" : "CSV-/handmatig geregistreerd bezit"); }
public interface IPaperCatalog
{
    Task<PaperCard> GetAsync(Guid id, bool refresh = false, CancellationToken ct = default);
    Task<PaperCard> GetAsync(string set, string number, string language, CancellationToken ct = default);
    Task<IReadOnlyList<PaperCard>> SearchAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<PaperCard>> CommanderPoolAsync(string colors, CancellationToken ct = default);
    Task<IReadOnlyList<PaperCard>> WizardPoolAsync(DeckWizardRequest request, CancellationToken ct = default);
    Task<string?> ImageAsync(PaperCard card, bool back, CancellationToken ct = default);
}
public interface IPaperWorkspace
{
    Task InitializeExampleAsync(CancellationToken ct = default);
    Task<PaperSummary> SummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PaperOwned>> CollectionAsync(string search = "", CancellationToken ct = default);
    Task SetOwnedAsync(Guid printing, int quantity, CancellationToken ct = default);
    Task<PaperImportPreview> PrepareCsvAsync(string path, CancellationToken ct = default);
    Task<PaperImportPreview> PreviewAsync(Guid id, CancellationToken ct = default);
    Task<PaperImportPreview> ResolveAsync(Guid id, Guid version, int row, Guid? target, bool skip, CancellationToken ct = default);
    Task ApplyAsync(Guid id, Guid version, CancellationToken ct = default);
    Task UndoAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PaperImportHistory>> HistoryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PaperDeckInfo>> DecksAsync(CancellationToken ct = default);
    Task<Guid> CreateDeckAsync(string name, CancellationToken ct = default);
    Task DeleteDeckAsync(Guid id, CancellationToken ct = default);
    Task SetDeckCardAsync(Guid deck, Guid printing, DeckZone zone, int quantity, CancellationToken ct = default);
    Task SetProfileAsync(Guid id, string profile, CancellationToken ct = default);
    Task<PaperDeckReport> DeckAsync(Guid id, CancellationToken ct = default);
    Task<string> ExportDeckAsync(Guid id, CancellationToken ct = default);
    Task<PaperDeckDraft> PrepareDeckTextAsync(string text, string name, string language, CancellationToken ct = default);
    Task<PaperDeckDraft> PrepareDeckCopyAsync(Guid deck, string name, Guid? source = null, Guid? replacement = null, int quantity = 0, CancellationToken ct = default);
    Task<Guid> SaveDeckDraftAsync(PaperDeckDraft draft, CancellationToken ct = default);
    Task<PaperDeckDraft> PrepareProposalAsync(string name, bool planning, CancellationToken ct = default);
    Task<PaperDeckDraft> PrepareCommanderAsync(string name, Guid commander, bool planning, CancellationToken ct = default);
    Task<PaperDeckDraft> PrepareWizardAsync(DeckWizardRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PaperAlternative>> AlternativesAsync(Guid deck, Guid printing, CancellationToken ct = default);
}
public static class PaperIdentity
{
    public static string Key(Guid id, string language, string finish, string condition, string binder) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, language, finish, condition, binder })));
}
