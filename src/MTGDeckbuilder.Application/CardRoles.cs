using System.Text.RegularExpressions;

namespace MTGDeckbuilder.Application;

public enum CardRole { Unknown, Counterspell, Removal, Draw, ManaAcceleration }
public enum RoleFit { Matching, Limited, Unknown, Different }

public sealed record CardRoleOption(int Number, string Text, CardRoleAnalysis Analysis);

public sealed record CardRoleAnalysis(string Version, string RuleId, CardRole Role, string Target,
    string Effect, string Restriction, int Amount, string Timing, string ManaCost, string Explanation,
    bool Complete = true, string Evidence = "", string UnparsedText = "", string Mechanism = "spell", string OtherEffects = "",
    IReadOnlyList<CardRoleOption>? Options = null)
{
    public bool IsModal => Options is { Count: > 0 };
    public CardRole[] Roles => IsModal ? Options!.SelectMany(x => x.Analysis.Roles).Distinct().ToArray()
        : Role == CardRole.Unknown ? [] : [Role];
    public bool Known => Roles.Length > 0;
    public bool FullyRecognized => Known && Complete;
    public string Recognition => !Known ? "Onbekend" : Complete ? "Volledige tekst herkend" : "Onvolledige rol-aanwijzing";
    public string Label => IsModal ? "Modale rol-aanwijzingen · opties afzonderlijk beoordelen"
        : (!Complete && Known ? "Aanwijzing: " : "") + (Effect == "damage" ? "Gerichte schade · dodelijkheid onbekend" : Role switch
    {
        CardRole.Counterspell => "Counterspell", CardRole.Removal => "Gerichte removal",
        CardRole.Draw => "Kaarten trekken", CardRole.ManaAcceleration => "Mana-versnelling", _ => "Rol onbekend"
    });
    public string Details => $"{Recognition} · {Label} · {Explanation}\nTiming: {Timing} · gedrukte manakosten: {ManaCost}" +
        (OtherEffects.Length == 0 ? "" : $"\nBijkomende effecten: {OtherEffects}") +
        (Evidence.Length == 0 ? "" : $"\nHerkende tekst: {Evidence}") +
        (UnparsedText.Length == 0 ? "" : $"\nNiet beoordeelde tekst/voorwaarden: {UnparsedText}") +
        (!IsModal ? "" : "\n" + string.Join("\n", Options!.Select(x => $"Optie {x.Number}: {x.Text}\n{x.Analysis.Details}"))) +
        $"\nHerkenning: {RuleId} · {Version}";
}
public sealed record CardRoleComparison(RoleFit Fit, CardRoleAnalysis Source, CardRoleAnalysis Candidate, string Explanation)
{
    public string Label => !Source.Complete || !Candidate.Complete ? "Onvolledige rol-aanwijzing · geen sterke match" : Fit switch
    {
        RoleFit.Matching => "Rol past binnen ondersteunde herkenning",
        RoleFit.Limited => "Beperkte rolmatch · verschillen controleren",
        RoleFit.Different => "Andere herkende rol · alleen structurele overeenkomst",
        _ => "Rol niet bewezen · alleen structurele overeenkomst"
    };
    public string Details => $"{Label}\n{Explanation}\nOrigineel: {Source.Details}\nAlternatief: {Candidate.Details}";
}

/// <summary>Closed, anchored patterns over the entire English Oracle text; never keyword tagging.</summary>
public static class CardRoleClassifier
{
    public const string Version = "Oracle roles v3 (2026-10-22)";
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static readonly string[] Numbers = ["a", "one", "two", "three", "four", "five", "six", "seven"];

    public static CardRoleAnalysis Analyze(AdvicePrinting card) => RoleTextInterpreter.Analyze(card);

    internal static CardRoleAnalysis AnalyzeExact(AdvicePrinting card)
    {
        var timing = card.TypeLine?.Split('—')[0].Trim() ?? "onbekend";
        var cost = string.IsNullOrWhiteSpace(card.ManaCost) ? "onbekend; vergelijk de kaart" : card.ManaCost;
        CardRoleAnalysis Unknown(string reason) => new(Version, "unknown", CardRole.Unknown, "", "", "", 0, timing, cost, reason);
        if (card.TypeLine?.Contains("//", StringComparison.Ordinal) == true ||
            timing is not ("Instant" or "Sorcery"))
            return Unknown("Alleen enkelzijdige instant-/sorceryteksten worden ondersteund; abilities op permanents blijven onbekend.");
        if (string.IsNullOrWhiteSpace(card.OracleText) || card.OracleText.Length > 8000)
            return Unknown("Geen volledige ondersteunde Oracle-tekst beschikbaar.");
        var text = Regex.Replace(card.OracleText, @"\s+", " ", RegexOptions.CultureInvariant, Timeout).Trim();
        Match Match(string pattern) => Regex.Match(text, @"\A" + pattern + @"\z", RegexOptions.CultureInvariant, Timeout);
        CardRoleAnalysis Known(string rule, CardRole role, string target, string effect, string restriction, int amount, string explanation)
            => new(Version, rule, role, target, effect, restriction, amount, timing, cost, explanation);

        var counter = Match(@"Counter target (spell|creature spell|noncreature spell|instant spell|instant or sorcery spell|artifact or enchantment spell)(?: unless its controller pays (?<tax>(?:\{[0-9WUBRGC]+\})+))?\.");
        if (counter.Success)
        {
            var target = counter.Groups[1].Value; var tax = counter.Groups["tax"].Value;
            var restriction = tax.Length > 0 ? "unless controller pays " + tax : "";
            return Known("counter.target.v1", CardRole.Counterspell, target, "counter", restriction, 1,
                $"Doel: {target}. " + (tax.Length == 0 ? "Geen betaalvoorwaarde in de herkende tekst." : $"Counter werkt niet als de controller {tax} betaalt."));
        }
        var manaCounter = Match(@"Counter target spell with mana value (?<value>[1-9]) or greater\.");
        if (manaCounter.Success)
            return Known("counter.mana-min.v1", CardRole.Counterspell, "spell", "counter", "mana value " + manaCounter.Groups["value"].Value + " or greater", 1,
                $"Alleen spells met manawaarde {manaCounter.Groups["value"].Value} of hoger; geen onbeperkte counter.");

        var removal = Match(@"(Destroy|Exile) target (creature|artifact|enchantment|artifact or enchantment|creature or planeswalker|nonland permanent)\.");
        if (removal.Success)
            return Known("removal.target.v1", CardRole.Removal, removal.Groups[2].Value, removal.Groups[1].Value.ToLowerInvariant(), "", 1,
                $"{removal.Groups[1].Value}: doel {removal.Groups[2].Value}. Destroy en exile zijn verschillende effecten.");
        if (text == "Destroy target nonartifact creature.")
            return Known("removal.nonartifact.v1", CardRole.Removal, "creature", "destroy", "nonartifact", 1,
                "Vernietigt een creature, maar kan geen artifact creature raken.");
        var bounce = Match(@"Return target (creature|nonland permanent) to its owner's hand\.");
        if (bounce.Success)
            return Known("removal.bounce.v1", CardRole.Removal, bounce.Groups[1].Value, "bounce", "", 1,
                $"Doel {bounce.Groups[1].Value} terug naar de hand; dit verwijdert het niet definitief.");

        var draw = Match(@"Draw (a|one|two|three|four|five|six|seven) cards?\.");
        var playerDraw = Match(@"Target player draws (a|one|two|three|four|five|six|seven) cards?\.");
        if (draw.Success || playerDraw.Success)
        {
            var target = draw.Success ? "you" : "player";
            var amount = Array.IndexOf(Numbers, (draw.Success ? draw : playerDraw).Groups[1].Value);
            amount = Math.Max(1, amount);
            return Known("draw.simple.v1", CardRole.Draw, target, "draw", "", amount,
                $"{amount} kaart(en) trekken; doel " + (target == "you" ? "jijzelf." : "een gekozen speler, dus niet alleen jijzelf."));
        }

        var land = Match(@"Search your library for a basic land card, put (?:that card|it) onto the battlefield(?<tapped> tapped)?, then shuffle\.");
        if (land.Success)
            return Known("mana.basic-land.v1", CardRole.ManaAcceleration, "basic land", "land", land.Groups["tapped"].Success ? "tapped" : "", 1,
                "Eén basic land van library naar battlefield" + (land.Groups["tapped"].Success ? ", getapt." : ", zonder tapped-clausule."));
        if (text == "Search your library for a Plains, Island, Swamp, or Mountain card, put it onto the battlefield tapped, then shuffle.")
            return Known("mana.land-types.v1", CardRole.ManaAcceleration, "Plains, Island, Swamp, or Mountain", "land", "tapped", 1,
                "Eén land met genoemd type naar battlefield, getapt; geen Forest-only land en niet beperkt tot basics.");
        var mana = Match(@"Add (?<mana>(?:\{[WUBRGC]\}){1,7})\.");
        if (mana.Success)
        {
            if (mana.Groups["mana"].Value.Count(x => x == '{') <= card.ManaValue)
                return Unknown("Toegevoegde mana is niet groter dan de catalogus-manawaarde; mana-versnelling is niet bewezen.");
            return Known("mana.temporary.v1", CardRole.ManaAcceleration, "mana pool", mana.Groups["mana"].Value, "temporary", mana.Groups["mana"].Value.Count(x => x == '{'),
                $"Tijdelijk {mana.Groups["mana"].Value} toevoegen; geen blijvende manabron. Netto winst of alternatieve kosten zijn niet berekend.");
        }
        return Unknown("De volledige tekst past niet in een ondersteund patroon; extra effecten, voorwaarden en modaliteiten worden niet geraden.");
    }

    public static CardRoleComparison Compare(AdvicePrinting from, AdvicePrinting to)
    {
        var a = Analyze(from); var b = Analyze(to);
        if (!a.Known || !b.Known) return new(RoleFit.Unknown, a, b, "De rol van minstens één kaart is niet bewezen. Vergelijk de volledige kaartteksten zelf.");
        if (!a.Complete || !b.Complete)
            return new(a.Roles.Intersect(b.Roles).Any() ? RoleFit.Limited : RoleFit.Unknown, a, b,
                "Er is alleen een onvolledige rol-aanwijzing. Overige tekst, voorwaarden en kosten zijn niet volledig beoordeeld; geen sterke rolmatch. " +
                (a.IsModal || b.IsModal ? "Modale opties zijn afzonderlijke keuzes, geen onvoorwaardelijke combinatie; kosten en keuzeregels zelf controleren. " : "") +
                "Lees de niet beoordeelde tekst.");
        if (a.Role != b.Role) return new(RoleFit.Different, a, b, "De herkende effectfamilies verschillen; deze kandidaat bewaart de oorspronkelijke rol niet.");
        var problems = new List<string>();
        if (a.Effect == "damage" || b.Effect == "damage")
            problems.Add($"Gerichte schade ({a.Amount} → {b.Amount}) bewijst geen dodelijke removal. Toughness en speltoestand zijn niet beoordeeld; nooit een sterke rolmatch.");
        if (a.Mechanism != b.Mechanism) problems.Add($"Verschillende werking: {a.Mechanism} → {b.Mechanism}.");
        if (a.OtherEffects != b.OtherEffects) problems.Add($"Bijkomende effecten verschillen: {a.OtherEffects} → {b.OtherEffects}.");
        if (!Covers(a, b)) problems.Add($"Doelwitten verschillen: {a.Target} → {b.Target}; gelijke dekking is niet bewezen.");
        if (b.Restriction.Length > 0 && b.Restriction != a.Restriction)
            problems.Add($"Nieuwe/andere beperking: {a.Restriction switch { "" => "geen", _ => a.Restriction }} → {b.Restriction}.");
        if (a.Role == CardRole.Removal && (a.Effect == "bounce") != (b.Effect == "bounce"))
            problems.Add("Terug naar de hand en definitieve removal vervullen niet dezelfde werking.");
        if (a.Role == CardRole.ManaAcceleration && (a.Effect == "land") != (b.Effect == "land"))
            problems.Add("Tijdelijke mana en een land op het battlefield zijn verschillende soorten versnelling.");
        if (a.Role == CardRole.ManaAcceleration && a.Effect != "land" && b.Effect != a.Effect)
            problems.Add("De hoeveelheid of kleur van de toegevoegde mana verschilt.");
        if (a.Role == CardRole.Draw && b.Amount < a.Amount) problems.Add($"Minder kaarten: {a.Amount} → {b.Amount}.");
        if (a.Timing == "Instant" && b.Timing != "Instant") problems.Add("Het alternatief heeft sorcery-timing.");
        var differences = $"Doel {a.Target} → {b.Target}; effect {a.Effect} → {b.Effect}; hoeveelheid {a.Amount} → {b.Amount}; " +
            $"voorwaarde {(a.Restriction.Length == 0 ? "geen" : a.Restriction)} → {(b.Restriction.Length == 0 ? "geen" : b.Restriction)}; " +
            $"timing {a.Timing} → {b.Timing}; gedrukte manakosten {a.ManaCost} → {b.ManaCost}.";
        return new(problems.Count == 0 ? RoleFit.Matching : RoleFit.Limited, a, b,
            (problems.Count == 0 ? "Dezelfde ondersteunde effectfamilie met passende doeldekking; geen garantie van strategische gelijkwaardigheid. " : string.Join(" ", problems) + " ") + differences);
    }
    private static bool Covers(CardRoleAnalysis source, CardRoleAnalysis candidate)
    {
        if (source.Target == candidate.Target) return true;
        return source.Role switch
        {
            CardRole.Counterspell => candidate.Target == "spell" || candidate.Target == "noncreature spell" && source.Target is "instant spell" or "instant or sorcery spell"
                || candidate.Target == "instant or sorcery spell" && source.Target == "instant spell",
            CardRole.Removal => candidate.Target == "creature or planeswalker" && source.Target == "creature"
                || candidate.Target == "artifact or enchantment" && source.Target is "artifact" or "enchantment",
            CardRole.Draw => candidate.Target == "player" && source.Target == "you",
            _ => false
        };
    }
}
