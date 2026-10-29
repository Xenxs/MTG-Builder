using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application;

public sealed record RecursionTarget(string[] Types,int? MaximumManaValue,string Destination)
{
    public bool Accepts(PaperCard card)=>!SynergyProfiles.ComplexCard(card)&&
        (Types.Contains("Card")||Types.Intersect(card.TypeLine.Split([' ','—','/'],StringSplitOptions.RemoveEmptyEntries),StringComparer.OrdinalIgnoreCase).Any())&&
        (MaximumManaValue is null||card.ManaValue<=MaximumManaValue.Value);
    public string Description=>(Types.Contains("Card")?"kaart":$"{string.Join(" / ",Types)}kaart")+(MaximumManaValue is {} n?$" met mana value maximaal {n}":"")+" → "+Destination;
}

/// <summary>Selected literal target constraints, not an arbitrary Oracle-language interpreter.</summary>
public static partial class SynergyTargets
{
    private static readonly ConcurrentDictionary<string,IReadOnlyList<RecursionTarget>> Cache=new(StringComparer.Ordinal);
    public static IReadOnlyList<RecursionTarget> Recursion(PaperCard card)
    {
        if(SynergyProfiles.ComplexCard(card))return [];
        if(Cache.TryGetValue(card.OracleText,out var found))return found;
        var rules=new List<RecursionTarget>();
        foreach(var raw in SynergyProfiles.RemoveParentheses(card.OracleText).Split('\n'))
        {
            if(raw.Contains('"')||raw.Contains('“')||Regex.IsMatch(raw,@"\b(can't|cannot|instead|rather than)\b",RegexOptions.IgnoreCase)||
                Regex.IsMatch(raw,@"\bunder (an opponent's|that player's|their) control\b",RegexOptions.IgnoreCase))continue;
            foreach(Match m in Pattern().Matches(raw))
            {
                var types=Regex.Matches(m.Groups["types"].Value,@"creature|artifact|enchantment|land|planeswalker|battle",RegexOptions.IgnoreCase).Select(x=>char.ToUpperInvariant(x.Value[0])+x.Value[1..].ToLowerInvariant()).Distinct().ToArray();
                if(types.Length==0)types=["Card"];var maximum=m.Groups["max"].Success?int.Parse(m.Groups["max"].Value):(int?)null;
                rules.Add(new(types,maximum,m.Groups["destination"].Value.ToLowerInvariant()));
            }
        }
        var result=rules.ToArray();if(Cache.Count<65_536)Cache.TryAdd(card.OracleText,result);return result;
    }
    public static bool Reuses(SynergyProfile p)=>p.Any(CreatureRecursion,TypedCardRecursion);
    public static bool Accepts(SynergyProfile p,PaperCard candidate)=>Reuses(p)&&Recursion(p.Card).Any(r=>r.Accepts(candidate));
    [GeneratedRegex(@"\bReturn target (?:(?<types>(?:creature|artifact|enchantment|land|planeswalker|battle)(?:,? (?:or )?(?:creature|artifact|enchantment|land|planeswalker|battle))*) )?card(?: with mana value (?<max>[0-9]+) or less)? from your graveyard to (?<destination>your hand|the battlefield)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
