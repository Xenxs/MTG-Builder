using System.Text.RegularExpressions;

namespace MTGDeckbuilder.Application;

/// <summary>Exposes supported options without asserting that their effects can be combined.</summary>
internal static class ModalRoleInterpreter
{
    public static CardRoleAnalysis? Analyze(AdvicePrinting card, Func<string, CardRoleAnalysis> analyzeOption)
    {
        var timing = card.TypeLine?.Split('—')[0].Trim();
        if (timing is not ("Instant" or "Sorcery")) return null;
        var lines = card.OracleText!.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        if (lines.Length < 3 || !Regex.IsMatch(lines[0],
            @"\AChoose (?:one or both|one or more|two or more|one|two|three)(?: —|\.)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        // Each option must start with a real bullet; no nested choices or arbitrary keyword extraction.
        if (!lines[1].StartsWith("• ", StringComparison.Ordinal)) return null;
        var texts = new List<string>();
        foreach (var line in lines.Skip(1))
        {
            if (line.StartsWith("• ", StringComparison.Ordinal)) texts.Add(line[2..]);
            else texts[^1] += "\n" + line;
        }
        if (texts.Count is < 2 or > 8 || texts.Any(x => x.Contains('•') || x.StartsWith("Choose ", StringComparison.Ordinal))) return null;
        var options = texts.Select((text, index) => new CardRoleOption(index + 1, text, analyzeOption(text))).ToArray();
        if (!options.Any(x => x.Analysis.Known)) return null;
        return new(CardRoleClassifier.Version, "modal.options.v3", CardRole.Unknown, "per optie", "modal", lines[0], 0,
            timing, string.IsNullOrWhiteSpace(card.ManaCost) ? "onbekend" : card.ManaCost,
            "Modale kaart: alleen afzonderlijke opties zijn herkend. Keuzeregels, kosten en niet ondersteunde opties zijn niet volledig beoordeeld. Effecten zijn geen onvoorwaardelijke combinatie.",
            Complete: false, Evidence: string.Join("\n", options.Where(x => x.Analysis.Known).Select(x => $"Optie {x.Number}: {x.Analysis.Evidence}")),
            UnparsedText: card.OracleText.Trim(), Mechanism: "modal-spell", Options: options);
    }
}
