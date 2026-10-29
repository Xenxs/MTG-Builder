using System.Text.Json;
using System.Text.RegularExpressions;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application;

/// <summary>Literal commander dependencies. It is not a full archetype/tribal or game-state model.</summary>
public sealed class CommanderBuildPlan
{
    private static readonly Lazy<HashSet<string>> Vocabulary=new(()=>
    {using var stream=typeof(CommanderBuildPlan).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources.creature-types-2026-10-28.json")!;return JsonSerializer.Deserialize<string[]>(stream)!.ToHashSet(StringComparer.OrdinalIgnoreCase);});
    public string[] CreatureTypes {get;}
    public bool CastTypeRequired {get;}
    public string CastFocus {get;}
    public string Evidence {get;}
    public bool HasFocus=>CreatureTypes.Length>0||CastFocus.Length>0;
    public CommanderBuildPlan(SynergyProfile commander)
    {
        var types=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var cast=false;var evidence=new List<string>();
        foreach(var line in SynergyProfiles.RemoveParentheses(commander.Card.OracleText).Split('\n'))
        {
            if(line.Contains('"')||line.Contains('“'))continue;
            foreach(Match m in Regex.Matches(line,@"\bnumber of (?<type>[A-Za-z'-]+) you control\b|\b(?<type>[A-Za-z'-]+) (?:creatures? )?you control (?:enters?|gets?|have|has|attacks?|dies)\b|\b(?:you cast|to cast) (?:an? |another )?(?<casttype>[A-Za-z'-]+) (?:creature )?spells?\b|\b(?<casttype>[A-Za-z'-]+) (?:creature )?spells you cast\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
            {
                var isCast=m.Groups["casttype"].Success;var type=Canonical(isCast?m.Groups["casttype"].Value:m.Groups["type"].Value);if(type is null)continue;
                types.Add(type);cast|=isCast;if(!evidence.Contains(line))evidence.Add(line);
            }
        }
        CreatureTypes=types.Order(StringComparer.OrdinalIgnoreCase).ToArray();CastTypeRequired=cast;
        CastFocus=commander.Has(InstantSorceryCastPayoff)?"instant-sorcery":commander.Has(ArtifactCastPayoff)?"artifact":commander.Has(NoncreatureCastPayoff)?"noncreature":"";
        Evidence=string.Join("\n",evidence);
    }
    private static string? Canonical(string word)
    {
        if(Vocabulary.Value.TryGetValue(word,out var exact))return exact;
        var irregular=word.ToLowerInvariant()switch{"mice"=>"Mouse","oxen"=>"Ox","children"=>"Child",_=>""};if(irregular.Length>0&&Vocabulary.Value.TryGetValue(irregular,out var special))return special;
        foreach(var variant in new[]{word.EndsWith("s",StringComparison.OrdinalIgnoreCase)?word[..^1]:"",word.EndsWith("ves",StringComparison.OrdinalIgnoreCase)?word[..^3]+"f":"",word.EndsWith("ies",StringComparison.OrdinalIgnoreCase)?word[..^3]+"y":""})if(Vocabulary.Value.TryGetValue(variant,out var result))return result;
        return null;
    }
    public static IReadOnlySet<string> PrintedCreatureTypes(PaperCard card)
    {
        if(SynergyProfiles.ComplexCard(card)||!card.TypeLine.Split([' ','—','/']).Any(t=>t is "Creature" or "Kindred"))return new HashSet<string>();
        var subtypes=card.TypeLine.Split('—').Skip(1).FirstOrDefault()??"";var result=subtypes.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(Canonical).Where(x=>x is not null).Select(x=>x!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(subtypes.Contains("Time Lord",StringComparison.OrdinalIgnoreCase)&&Vocabulary.Value.Contains("Time Lord"))result.Add("Time Lord");return result;
    }
    public bool Matches(SynergyProfile card)
    {
        if(CreatureTypes.Length>0)
        {
            if(PrintedCreatureTypes(card.Card).Overlaps(CreatureTypes))return true;
            if(CastTypeRequired||!card.Has(OwnCreatureTokens))return false;
            foreach(var line in SynergyProfiles.RemoveParentheses(card.Card.OracleText).Split('\n'))
            {
                if(Regex.IsMatch(line,@"\b(opponent|opponents|its controller|that player|each player)\b",RegexOptions.IgnoreCase)||line.Contains('"'))continue;
                foreach(Match m in Regex.Matches(line,@"\bCreate\b[^.]{0,220}?\bcreature tokens?\b",RegexOptions.IgnoreCase))
                    if(m.Value.Split([' ','/','—'],StringSplitOptions.RemoveEmptyEntries).Select(Canonical).Any(t=>t is not null&&CreatureTypes.Contains(t,StringComparer.OrdinalIgnoreCase)))return true;
            }
            return false;
        }
        return CastFocus switch{"instant-sorcery"=>card.Has(InstantSorcery),"artifact"=>card.Has(Artifact),"noncreature"=>card.Has(NoncreatureNonland),_=>false};
    }
    public string Describe(IReadOnlyList<PaperCard> selected)
    {
        if(!HasFocus)return "Geen aanvullende expliciete creaturetype-/castafhankelijkheid als bouwplan herkend.";
        var count=selected.Count(c=>Matches(SynergyProfiles.Analyze(c)));
        return $"Begrensd commanderbouwplan: {(CreatureTypes.Length>0?string.Join(" / ",CreatureTypes)+(CastTypeRequired?"-spells werkelijk casten":" onder eigen controle / passende tokenproductie"):CastFocus+" casttriggers")} · {count} verschillende ondersteunende niet-landen. Dit telt kaartrollen, geen aanwezige creatures, casts of gegarandeerde tokens.\n"+Evidence;
    }
}
