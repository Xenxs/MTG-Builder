using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record DeckWizardRequest(string Name,string Format,string ColorMode,string Colors,string Theme,string Style,bool Planning,Guid? Commander=null)
{
    public static readonly CardChoice[] Formats=[new("casual60","Casual · 60 kaarten"),new("standard","Standard · 60 kaarten"),new("commander","Commander · 100 kaarten")];
    public static readonly CardChoice[] Styles=[new("balanced","Gebalanceerd / midrange"),new("aggro","Aggro · snelle druk"),new("control","Control · reageren en opbouwen")];
    public string AllowedColors=>ColorMode=="any"?"WUBRG":ColorMode=="colorless"?"":Colors;
    public void Validate()
    {
        Rules.Text(Name,"Decknaam",120);if(Name.Any(char.IsControl))throw new ArgumentException("Decknaam bevat ongeldige tekens.");
        if(!Formats.Any(x=>x.Key==Format)||!Styles.Any(x=>x.Key==Style)||!CardThemes.Choices.Any(x=>x.Key==Theme))throw new ArgumentException("Kies een ondersteund formaat, thema en speelstijl.");
        if(ColorMode is not("any" or "selected" or "colorless")||Colors.Any(c=>!"WUBRG".Contains(c))||Colors.Distinct().Count()!=Colors.Length)throw new ArgumentException("Ongeldige kleurkeuze.");
        if(ColorMode=="selected"&&Colors.Length==0)throw new ArgumentException("Kies minstens één kleur, of kies bewust kleurloos/geen voorkeur.");
        if(Format=="commander"&&(Commander is null||Commander==Guid.Empty))throw new ArgumentException("Kies een commander.");
    }
    public bool Allows(PaperCard card)=>card.Colors.All(AllowedColors.Contains);
    public static int ThemeScore(PaperCard card,string theme)
    {
        return SynergySelection.Affinity(SynergyProfiles.Analyze(card),theme);
    }
    public static int StyleScore(PaperCard card,string style)=>style switch
    {
        "aggro"=>card.TypeLine.Contains("Creature",StringComparison.Ordinal)?2:0,
        "control"=>card.TypeLine.Contains("Instant",StringComparison.Ordinal)||card.TypeLine.Contains("Sorcery",StringComparison.Ordinal)?2:0,
        _=>0
    };
}
