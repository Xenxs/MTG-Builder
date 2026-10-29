using System.Text.RegularExpressions;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public static class StandardDeckValidator
{
    public const string RulesVersion = "Arena Standard 2026-10-22 / CR 2026-09-25";
    public static readonly DateTime RulesCheckedUtc = new(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
    public static readonly TimeSpan MaxSourceAge = TimeSpan.FromDays(1);
    public static readonly TimeSpan MaxRulesAge = TimeSpan.FromDays(14);
    public static IReadOnlyList<RuleProfileOption> Profiles { get; } =
    [new(DeckRuleProfile.Unspecified, "Niet gekozen / ander formaat"),
     new(DeckRuleProfile.ArenaStandardBo1, DeckRuleProfiles.Bo1), new(DeckRuleProfile.ArenaStandardBo3, DeckRuleProfiles.Bo3)];
    // Wizards' dated Standard list; Scryfall legalities remain the catalog authority for rotation.
    private static readonly HashSet<string> Banned = new(StringComparer.OrdinalIgnoreCase)
    {
        "Abuelo's Awakening", "Badgermole Cub", "Cori-Steel Cutter", "Gran-Gran", "Heartfire Hero",
        "Hopeless Nightmare", "Monstrous Rage", "Proft's Eidetic Memory", "Screaming Nemesis",
        "Stormchaser's Talent", "This Town Ain't Big Enough", "Up the Beanstalk", "Vivi Ornitier"
    };

    public static DeckValidation Validate(DeckRuleProfile profile, IReadOnlyList<ValidationCard> cards,
        LegalitySnapshot snapshot, DateTime nowUtc)
    {
        if (!Enum.IsDefined(profile)) throw new ArgumentException("Onbekend controleprofiel.");
        if (cards.Any(x => x.Quantity < 1 || !Enum.IsDefined(x.Zone))) throw new ArgumentException("Ongeldige deckregels.");
        var issues = new List<DeckValidationIssue>();
        void Issue(string code, ValidationSeverity severity, string message, string action, string card = "")
            => issues.Add(new(code, severity, card, message, action));
        void Unknown(string code, string message, string action, string card = "") => Issue(code, ValidationSeverity.Unknown, message, action, card);
        void Error(string code, string message, string action, string card = "") => Issue(code, ValidationSeverity.Error, message, action, card);
        var main = cards.Where(x => x.Zone == DeckZone.Mainboard).Sum(x => x.Quantity);
        var side = cards.Where(x => x.Zone == DeckZone.Sideboard).Sum(x => x.Quantity);
        DeckValidation Result() => new(profile, nowUtc, RulesVersion, snapshot, main, side, issues);
        if (profile == DeckRuleProfile.Unspecified)
        {
            Unknown("profile", "Kies expliciet Arena Standard BO1 of BO3. Een vrij formaatlabel bepaalt geen regels.", "Kies en bewaar een controleprofiel.");
            return Result();
        }
        if (nowUtc < RulesCheckedUtc || nowUtc - RulesCheckedUtc > MaxRulesAge)
            Unknown("rules-stale", "De ingebouwde Arena-regels en aanvullende banlijst zijn niet recent gecontroleerd.", "Gebruik een appversie met opnieuw gecontroleerde regels.");
        if (snapshot.MetadataVersion < 1 || snapshot.RunId is null)
            Unknown("metadata", "De catalogus bevat nog geen fase-5-legaliteitsgegevens.", "Ververs de kaartcatalogus online.");
        if (snapshot.SourceUpdatedUtc is not { } date || date > nowUtc.AddMinutes(5) || nowUtc - date > MaxSourceAge)
            Unknown("source-stale", "De brondata zijn ouder dan 24 uur, hebben een toekomstige datum of missen een bewezen brondatum.", "Ververs de catalogus online; een recente bestandsimport bewijst de ouderdom niet.");
        if (main < 60) Error("main-min", $"Het mainboard bevat {main} kaarten; minimaal 60 vereist.", "Vul het mainboard aan tot minstens 60 kaarten.");
        if (side > 15) Error("side-max", $"Het sideboard bevat {side} kaarten; maximaal 15 bij zowel BO1 als BO3.", "Verklein het sideboard tot maximaal 15 kaarten.");
        if (main > 250) Unknown("arena-size", "Dit mainboard bevat meer dan 250 kaarten; de clientlimiet valt buiten de bewezen controles.", "Verklein het deck of controleer de actuele Arena-clientlimiet.");
        if (cards.Any(x => x.Zone == DeckZone.Commander)) Error("commander", "Standard heeft geen commander-zone.", "Verplaats of verwijder de commanderkaarten, of gebruik een ander formaat.");
        foreach (var group in cards.GroupBy(x => x.Name.ToUpperInvariant()))
        {
            var card = group.First(); var count = group.Sum(x => x.Quantity);
            if (group.Any(x => !Guid.TryParse(x.OracleId, out var id) || id == Guid.Empty || string.IsNullOrWhiteSpace(x.TypeLine) || x.OracleText is null))
            {
                Unknown("identity", "Kaartidentiteit of regelmetadata ontbreken; aantallen en legaliteit zijn niet bewezen.", "Koppel de printing aan de kaartcatalogus en controleer opnieuw.", card.Name);
                continue;
            }
            if (group.Select(x => x.OracleId).Distinct().Count() != 1 || group.Select(x => (x.TypeLine, x.OracleText)).Distinct().Count() != 1)
            { Unknown("identity-conflict", "Dezelfde Engelse naam heeft tegenstrijdige identiteiten/metadata.", "Ververs en controleer de catalogus.", card.Name); continue; }
            if (group.Any(x => !x.AvailableOnArena)) Unknown("arena-printing", "Een printing is niet bevestigd in de actuele Arena-catalogus.", "Kies een actuele catalogusprinting.", card.Name);
            var legality = group.Select(x => x.Standard).Distinct().ToArray();
            if (Banned.Contains(card.Name) || profile == DeckRuleProfile.ArenaStandardBo1 && string.Equals(card.Name, "Leyline of Resonance", StringComparison.OrdinalIgnoreCase))
                Error("official-ban", "Deze kaart staat op de gecontroleerde Wizards-banlijst voor dit profiel.", "Verwijder de kaart of kies een toegestaan formaat.", card.Name);
            else if (legality.Length != 1 || legality[0] is CardLegality.Unknown or CardLegality.Restricted)
                Unknown("legality", "Standard-legaliteit ontbreekt, is tegenstrijdig of bevat een niet-ondersteunde status.", "Ververs de catalogus en controleer de bron.", card.Name);
            else if (legality[0] != CardLegality.Legal)
                Error("card-not-legal", legality[0] == CardLegality.Banned ? "Verboden volgens de opgeslagen Standard-brondata." : "Niet toegestaan in Standard volgens de opgeslagen brondata.", "Verwijder/vervang deze kaart of kies een ander formaat.", card.Name);
            var limit = CopyLimit(card);
            if (limit is null) Unknown("copy-exception", "Een deckbouwuitzondering wordt niet volledig ondersteund.", "Controleer de kaartregel handmatig; een volledig geslaagd oordeel is geblokkeerd.", card.Name);
            else if (count > limit) Error("copies", $"{count} exemplaren over alle printings en zones; maximaal {limit}.", $"Breng het gezamenlijke aantal terug naar maximaal {limit}.", card.Name);
        }
        return Result();
    }

    private static int? CopyLimit(ValidationCard card)
    {
        var type = card.TypeLine!.Split(" // ")[0].Split('—')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (type.Contains("Basic") && type.Contains("Land")) return int.MaxValue;
        var text = card.OracleText!;
        var name = card.Name.Split(" // ")[0];
        if (text.Contains("interchangeable", StringComparison.OrdinalIgnoreCase)) return null;
        var clauses = Regex.Matches(text, @"(?:A|Your) deck can have[^.]*\.", RegexOptions.CultureInvariant);
        if (clauses.Count == 0) return text.Contains("deck can have", StringComparison.OrdinalIgnoreCase) ? null : 4;
        if (clauses.Count != 1) return null;
        var clause = clauses[0].Value;
        if (clause == $"A deck can have any number of cards named {name}.") return int.MaxValue;
        var words = new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
        for (var i = 0; i < words.Length; i++)
            if (clause == $"A deck can have up to {words[i]} cards named {name}.") return i + 1;
        return null;
    }
}
