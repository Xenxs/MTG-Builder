namespace MTGDeckbuilder.Application;

public sealed record CardChoice(string Key,string Label)
{ public override string ToString()=>Label; }
public static class CardThemes
{
    public static readonly CardChoice[] Choices=[new("any","Geen voorkeur / alles"),new("equipment","Equipment & uitgerust"),new("tokens","Tokens"),new("graveyard","Graveyard & herstel"),new("sacrifice","Eigen sacrifice & sterftriggers"),new("opponent-death","Removal & vijandelijke sterftriggers"),new("domain","Domain"),new("exalted","Exalted"),new("draw","Kaarten trekken")];
    public static readonly CardChoice[] Colors=[new("any","Alle kleuren"),new("W","Wit"),new("U","Blauw"),new("B","Zwart"),new("R","Rood"),new("G","Groen"),new("colorless","Kleurloos"),new("multi","Meerkleurig")];
    public static readonly CardChoice[] Types=[new("any","Alle kaarttypen"),new("Creature","Creature"),new("Instant","Instant"),new("Sorcery","Sorcery"),new("Artifact","Artifact"),new("Enchantment","Enchantment"),new("Planeswalker","Planeswalker"),new("Battle","Battle"),new("Land","Land")];
    public static bool Matches(PaperCard card,string theme)=>theme=="any"||SynergyProfiles.Analyze(card).Theme(theme);
    public static IReadOnlyList<PaperOwned> Filter(IEnumerable<PaperOwned> cards,string text,string color,string theme,string type,string set)
        =>cards.Where(x=>x.Card.Name.Contains(text.Trim(),StringComparison.OrdinalIgnoreCase))
            .Where(x=>color=="any"||color=="colorless"?(color=="any"||(x.Card.PrintedColors??x.Card.Colors).Length==0):color=="multi"?(x.Card.PrintedColors??x.Card.Colors).Length>1:(x.Card.PrintedColors??x.Card.Colors).Contains(color,StringComparison.Ordinal))
            .Where(x=>Matches(x.Card,theme)).Where(x=>type=="any"||x.Card.TypeLine.Split([' ','—','/'],StringSplitOptions.RemoveEmptyEntries).Contains(type))
            .Where(x=>set=="any"||string.Equals(x.Card.Set,set,StringComparison.OrdinalIgnoreCase)).ToArray();
}
