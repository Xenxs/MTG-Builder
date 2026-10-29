using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public enum AggroSlot { Land, EarlyThreat, SpellAttacker, SpellPinger, ReachCreature, BurnReach, CreatureInteraction, CombatSupport, CardFlow, Excluded }

public sealed record ArchetypePoolEntry(string OracleId, string Name, string ManaCost, string TypeLine, string OracleText,
    string ColorIdentity, double ManaValue, AggroSlot Slot, int Preference, bool Included, string Purpose, string Cautions)
{
    public string SlotLabel => Slot switch
    {
        AggroSlot.Land => "Basismana", AggroSlot.EarlyThreat => "Vroege aanvaller", AggroSlot.SpellAttacker => "Aanvallende spell-payoff",
        AggroSlot.SpellPinger => "Noncreature-spell-ping", AggroSlot.ReachCreature => "Creature met enters-burn", AggroSlot.BurnReach => "Burn naar tegenstander",
        AggroSlot.CreatureInteraction => "Creature-interactie", AggroSlot.CombatSupport => "Combat-ondersteuning", AggroSlot.CardFlow => "Hand verversen", _ => "Bewust uitgesloten"
    };
    public string PreferenceLabel => Preference switch { 1 => "Eerste poolvoorkeur", 2 => "Voorwaardelijke pooloptie", _ => "Smalle pooloptie" };
    public string MetadataSha256 => Fingerprint(OracleId, Name, ManaCost, TypeLine, OracleText, ColorIdentity, ManaValue);
    public bool Matches(AdvicePrinting card) => Guid.TryParse(card.OracleId, out var id) && id.ToString() == OracleId &&
        MetadataSha256 == Fingerprint(id.ToString(), card.Name, card.ManaCost ?? "", card.TypeLine ?? "", card.OracleText ?? "", card.ColorIdentity, card.ManaValue);
    private static string Fingerprint(string oracleId, string name, string cost, string type, string text, string colors, double mana)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { oracleId, name, cost, type, text, colors, mana })));
}

public sealed record ArchetypePoolDefinition(int SchemaVersion, string Id, string Version, string Name, DateTime ReviewedUtc,
    string ReviewMethod, string SourceVersion, string SourceSha256, IReadOnlyList<ArchetypePoolEntry> Cards);

public static class MonoRedPool
{
    private static readonly Lazy<ArchetypePoolDefinition> Definition = new(() =>
    {
        using var stream = typeof(MonoRedPool).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources.mono-red-aggro-v1.json")
            ?? throw new InvalidDataException("De ingebouwde kaartpool ontbreekt.");
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var pool = JsonSerializer.Deserialize<ArchetypePoolDefinition>(stream, options) ?? throw new InvalidDataException("Ongeldige kaartpool.");
        if (pool.SchemaVersion != 1 || pool.Id != "mono-red-aggro-bo1" || pool.Cards.Count is < 2 or > 200 ||
            pool.Cards.Select(x => x.OracleId).Distinct().Count() != pool.Cards.Count || pool.Cards.Any(x =>
                !Guid.TryParse(x.OracleId, out var id) || id == Guid.Empty || id.ToString() != x.OracleId ||
                !Enum.IsDefined(x.Slot) || x.Preference is < 1 or > 3 || !double.IsFinite(x.ManaValue) || x.ManaValue < 0 ||
                x.ColorIdentity is not ("R" or "") || string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Cautions) ||
                string.IsNullOrWhiteSpace(x.Purpose) || x.Included == (x.Slot == AggroSlot.Excluded)))
            throw new InvalidDataException("De ingebouwde kaartpool voldoet niet aan de grenzen.");
        return pool with { Cards = Array.AsReadOnly(pool.Cards.ToArray()) };
    });
    public static ArchetypePoolDefinition Current => Definition.Value;
}

public sealed record ArchetypePoolInput(LegalitySnapshot Source, IReadOnlyList<AdvicePrinting> Printings, IReadOnlyList<AdviceStock> Stock);
public sealed record ArchetypePoolCard(ArchetypePoolEntry Entry, bool Eligible, int OwnedTotal, int OwnedEligible, string Status)
{
    public string Label => $"{Entry.Name} · {Entry.SlotLabel} · {OwnedEligible}× bruikbaar bezit · {Status}";
    public string Details => $"{Entry.Purpose}\n{Entry.PreferenceLabel}; dit is een expliciete ontwikkelbeoordeling, geen strategische gelijkwaardigheid.\n" +
        $"Voorwaarden en beperkingen: {Entry.Cautions}\nMana: {Entry.ManaCost} · manawaarde {Entry.ManaValue:0.##} · {Entry.TypeLine}\n" +
        $"Geregistreerd bezit {OwnedTotal}×; actueel bruikbaar {OwnedEligible}×. Geen deckreservering of vrije voorraadberekening.\n" +
        $"Poolstatus: {Status}\nOracle-ID: {Entry.OracleId}\nBeoordeelde metadata SHA-256: {Entry.MetadataSha256}\nVolledige kaarttekst:\n{Entry.OracleText}";
}
public sealed record ArchetypePoolReport(ArchetypePoolDefinition Definition, LegalitySnapshot Source, DateTime CheckedUtc,
    IReadOnlyList<ArchetypePoolCard> Cards, string SourceStatus)
{
    public string Summary => $"{Cards.Count(x => x.Entry.Included)} beoordeelde poolkaarten · {Cards.Count(x => x.Eligible)} actueel geschikt · " +
        $"{Cards.Count(x => !x.Entry.Included)} bewust uitgesloten";
    public string Provenance => $"{Definition.Version} · beoordeeld {Definition.ReviewedUtc:dd-MM-yyyy}\n{Definition.ReviewMethod}\n" +
        $"Catalogus: {Source.Source} · {Source.SourceVersion}\nBron UTC: {Source.SourceUpdatedUtc:yyyy-MM-dd HH:mm} · {SourceStatus}\n" +
        $"Controle UTC: {CheckedUtc:yyyy-MM-dd HH:mm} · {StandardDeckValidator.RulesVersion}";
}
public sealed record ArchetypePoolAlternative(string OracleId, string Name, int Owned, string Explanation, string Details)
{
    public string Label => $"{Name} · {Owned}× geregistreerd · beoordeelde deckfunctie, geen gelijkwaardigheidsbewijs";
}
public interface IArchetypePoolStore { Task<ArchetypePoolReport> ReadAsync(CancellationToken ct = default); }

public static class ArchetypePoolEngine
{
    public const string Version = "Mono-red pool advice v1";
    public static ArchetypePoolReport Evaluate(ArchetypePoolDefinition definition, ArchetypePoolInput input, DateTime now)
    {
        var stock = input.Stock.ToDictionary(x => x.PrintingId, x => x.Quantity);
        var global = StandardDeckValidator.Validate(DeckRuleProfile.ArenaStandardBo1, [], input.Source, now).Issues
            .Where(x => x.Code != "main-min").Select(x => x.Message).ToArray();
        var rows = definition.Cards.Select(entry =>
        {
            var printings = input.Printings.Where(x => Guid.TryParse(x.OracleId, out var id) && id.ToString() == entry.OracleId).ToArray();
            var active = printings.Where(x => x.AvailableOnArena).ToArray();
            var problems = new List<string>(global);
            if (!entry.Included) problems.Add("Bewust buiten het ondersteunde deckplan.");
            if (printings.Length == 0) problems.Add("Oracle-identiteit ontbreekt in de catalogus; geen naamkoppeling.");
            else if (printings.Any(x => !entry.Matches(x))) problems.Add("Metadata gewijzigd of tegenstrijdig; nieuwe poolbeoordeling vereist.");
            if (active.Length == 0) problems.Add("Geen actuele Arena-printing.");
            foreach (var card in active)
                problems.AddRange(StandardDeckValidator.Validate(DeckRuleProfile.ArenaStandardBo1, [card.Validate(DeckZone.Mainboard, 1)], input.Source, now)
                    .Issues.Where(x => x.Code != "main-min").Select(x => x.Message));
            var owned = printings.Sum(x => Math.Max(0, stock.GetValueOrDefault(x.Id)));
            var eligible = problems.Count == 0;
            return new ArchetypePoolCard(entry, eligible, owned, eligible ? active.Sum(x => Math.Max(0, stock.GetValueOrDefault(x.Id))) : 0,
                eligible ? "Actueel gecontroleerd voor BO1" : string.Join(" ", problems.Distinct()));
        }).ToArray();
        return new(definition, input.Source, now, rows, global.Length == 0 ? "Bron- en regelactualiteit geslaagd" : string.Join(" ", global));
    }

    public static IReadOnlyList<ArchetypePoolAlternative> Alternatives(ArchetypePoolReport report, string sourceOracleId)
    {
        var source = report.Cards.SingleOrDefault(x => x.Entry.OracleId == sourceOracleId);
        if (source is not { Eligible: true } || source.Entry.Slot is AggroSlot.Land or AggroSlot.Excluded) return [];
        return report.Cards.Where(x => x.Eligible && x.OwnedEligible > 0 && x.Entry.OracleId != sourceOracleId && x.Entry.Slot == source.Entry.Slot)
            .OrderBy(x => x.Entry.Preference).ThenBy(x => Math.Abs(x.Entry.ManaValue - source.Entry.ManaValue))
            .ThenBy(x => x.Entry.Name, StringComparer.Ordinal).ThenBy(x => x.Entry.OracleId, StringComparer.Ordinal)
            .Select(x => new ArchetypePoolAlternative(x.Entry.OracleId, x.Entry.Name, x.OwnedEligible,
                $"Dezelfde beoordeelde deckfunctie: {source.Entry.SlotLabel}. Poolvoorkeur {x.Entry.Preference}; mana {source.Entry.ManaValue.ToString(CultureInfo.InvariantCulture)} → {x.Entry.ManaValue.ToString(CultureInfo.InvariantCulture)}. " +
                "Verschillen en voorwaarden zelf beoordelen; geen bewezen rol-/strategische gelijkwaardigheid.",
                $"Origineel — {source.Entry.Name}\n{source.Details}\n\nAlternatief — {x.Entry.Name}\n{x.Details}\n\n" +
                $"{Version} · {report.Definition.Version}. Alleen geregistreerd bezit; geen volledige deck-, voorraad- of manabroncontrole."))
            .ToArray();
    }
}
