using System.Text.RegularExpressions;

namespace MTGDeckbuilder.Application;

/// <summary>Counts visible colored costs only, not casting probability or every alternative-cost rule.</summary>
public static partial class DeckManaHeuristics
{
    public static Dictionary<char,int> Demand(IEnumerable<(PaperCard Card,int Quantity)> rows)
    {
        var demand="WUBRGC".ToDictionary(c=>c,_=>0);
        foreach(var (card,quantity) in rows)
        {
            var activatedCosts=SynergyProfiles.RemoveParentheses(card.OracleText).Split('\n')
                .Where(line=>line.Contains(':')).Select(line=>line[..line.IndexOf(':')]);
            var visibleCosts=card.ManaCost+"\n"+string.Join("\n",activatedCosts);
            foreach(Match match in Symbols().Matches(visibleCosts))
                foreach(var color in match.Groups[1].Value.Where(c=>"WUBRGC".Contains(c)).Distinct())demand[color]+=quantity;
        }
        return demand;
    }
    [GeneratedRegex(@"\{([0-9WUBRGCXP/]+)\}")]
    private static partial Regex Symbols();
}
