using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application;

/// <summary>A bounded construction heuristic. Context changes after each pick; points are not win probability.</summary>
public sealed class SynergySelection
{
    private readonly Dictionary<string, SynergyProfile> _profiles;
    private readonly SynergyInteractionIndex _interactions;
    private readonly double[] _context;
    private readonly double[] _compatibility;
    private readonly HashSet<string> _uniquePicked=new(StringComparer.Ordinal);
    private readonly Dictionary<string,(double Context,double Compatibility)> _self=new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _copies = [];
    private readonly List<SynergyProfile> _picked = [];
    private readonly int _slots;
    private readonly string _style;
    private readonly string _theme;
    private readonly string? _commander;
    private readonly bool _autoColors;
    private readonly Dictionary<string,HashSet<char>> _costColors;
    private readonly CommanderBuildPlan? _commanderPlan;
    private readonly Dictionary<string,bool> _planMatches=[];
    private readonly int _planTarget;
    public string FocusColors {get;}
    public int RelationGroupCount=>_interactions.GroupCount;

    public SynergySelection(IEnumerable<PaperCard> pool, int slots, DeckWizardRequest? preferences = null, PaperCard? commander = null,
        IReadOnlyDictionary<string,int>? capacity = null)
    {
        _slots = slots; _style = preferences?.Style ?? "balanced"; _theme = preferences?.Theme ?? "any"; _commander = commander?.OracleId;
        _autoColors=commander is null&&preferences?.ColorMode=="any";
        _profiles = pool.Concat(commander is null?[]:[commander]).GroupBy(c => c.OracleId)
            .ToDictionary(g => g.Key, g => SynergyProfiles.Analyze(g.First()));
        _costColors=_profiles.Values.ToDictionary(p=>p.Card.OracleId,p=>DeckManaHeuristics.Demand([(p.Card,1)]).Where(x=>x.Value>0&&x.Key!='C').Select(x=>x.Key).ToHashSet());
        FocusColors=_autoColors?ChooseFocus(capacity):"WUBRG";
        if(commander is not null)
        {
            _commanderPlan=new(_profiles[commander.OracleId]);
            foreach(var p in _profiles.Values)_planMatches[p.Card.OracleId]=_commanderPlan.Matches(p);
            _planTarget=Math.Min(_slots/2,_profiles.Values.Count(p=>p.Card.OracleId!=_commander&&!p.Has(Land)&&_planMatches[p.Card.OracleId]));
        }
        _interactions=new(_profiles.Values,FitsFocus,_theme);_context=new double[_interactions.GroupCount];_compatibility=new double[_interactions.GroupCount];
        if (commander is not null){_picked.Add(_profiles[commander.OracleId]);AddContext(commander.OracleId);}
    }
    public void Pick(PaperCard card)
    {
        _copies[card.OracleId] = _copies.GetValueOrDefault(card.OracleId) + 1;
        _picked.Add(_profiles[card.OracleId]);
        AddContext(card.OracleId);
    }
    private void AddContext(string oracle)
    {
        if(!_uniquePicked.Add(oracle))return;var other=_interactions.Group(oracle);var commander=oracle==_commander;
        for(var g=0;g<_context.Length;g++){var addition=_interactions.Contribution(g,other,commander);_context[g]+=addition.Context;_compatibility[g]+=addition.Compatibility;}
        _self[oracle]=_interactions.Contribution(other,other,commander);
    }
    public double Score(PaperCard card)
    {
        var profile = _profiles[card.OracleId];
        var isCreature = profile.Has(Creature); var pickedSpells = _copies.Values.Sum(); var left = _slots - pickedSpells;
        var targetCreatures = (int)Math.Round(_slots * (_style == "aggro" ? .58 : _style == "control" ? .26 : .48));
        var currentCreatures = _picked.Count(p => p.Has(Creature) && p.Card.OracleId != _commander);
        var deficit = Math.Max(0, targetCreatures - currentCreatures);
        var desiredMv = _style == "aggro" ? 2 : _style == "control" ? 4 : 3;
        var score = Affinity(profile, _theme) + (isCreature ? .8 : 0) - Math.Abs(card.ManaValue - desiredMv) * .32;
        if (_style == "aggro" && isCreature) score += 1.2;
        if(_style=="aggro"&&profile.Has(Defender))score-=3.5;
        if (_style == "control" && profile.Has(InstantSorcery)) score += 1.1;
        if (isCreature && deficit > 0) score += 1.2 + 3d * deficit / Math.Max(1, left);
        if (!isCreature && left <= deficit) score -= 12;
        var equipmentCount = _picked.Count(p => p.Has(Equipment));
        if (profile.Has(Equipment))
        {
            var targetEquipment = _theme == "equipment" ? Math.Max(6, _slots / 4) : Math.Max(3, _slots / 8);
            score -= Math.Max(0, equipmentCount + 1 - targetEquipment) * 2.2;
            if (_picked.All(p => !SynergyEngine.CanReceiveEquipment(p)) && !profile.Has(JobSelect)) score -= 1.5;
        }
        if(_commanderPlan?.HasFocus==true)
        {
            var support=_picked.Count(p=>p.Card.OracleId!=_commander&&_planMatches.GetValueOrDefault(p.Card.OracleId));var needed=Math.Max(0,_planTarget-support);var fits=_planMatches.GetValueOrDefault(card.OracleId);
            if(fits)score+=needed>0?9:2;
            else if(needed>0){if(_commanderPlan.CreatureTypes.Length>0&&isCreature)score-=3;if(left<=needed)score-=15;}
        }
        if(_theme=="graveyard"&&profile.Any(SelfMill,Surveil,Loot,SelfDiscard))
        {
            var setup=_picked.Count(p=>p.Any(SelfMill,Surveil,Loot,SelfDiscard));score-=Math.Max(0,setup+1-Math.Max(6,_slots/4))*2;
        }
        var utilityCount = _picked.Count(Utility);
        if (Utility(profile) && utilityCount < Math.Max(4, _slots / 6)) score += 1.8;
        var group=_interactions.Group(card.OracleId);var self=_self.GetValueOrDefault(card.OracleId);
        var context=_context[group]-self.Context;var compatibility=_compatibility[group]-self.Compatibility;
        score += Math.Clamp(context, -7, 6);
        score += Math.Min(1.5,compatibility);
        if (_picked.Count < 2)
        {
            // Consider available counterparts before the first pick, without pretending they are already in the deck.
            var bestPartner = _interactions.Best(card.OracleId);
            score += Math.Min(1.5, bestPartner * .35);
        }
        if (profile.Has(GreenPermanentCondition) && !_profiles.Values.Any(p => !p.Has(Land)&&FitsFocus(p.Card)&&SynergyEngine.IsColoredPermanent(p, 'G'))) score -= 4;
        if (profile.Has(RedPermanentCondition) && !_profiles.Values.Any(p => !p.Has(Land)&&FitsFocus(p.Card)&&SynergyEngine.IsColoredPermanent(p, 'R'))) score -= 4;
        if (profile.Changed) score -= 3;
        if(profile.Has(OpponentDraw))score-=2;
        if(_autoColors)
        {
            score-=_costColors[card.OracleId].Count(c=>!FocusColors.Contains(c))*20;
        }
        score -= _copies.GetValueOrDefault(card.OracleId) * .75;
        return score;
    }
    private static bool Utility(SynergyProfile p) => p.Any(OwnDraw, Loot, DestroyRemoval, ConditionalToughnessRemoval,
        ExileRemoval, BasicLandSearch, CreatureRecursion);
    private bool FitsFocus(PaperCard card)=>!_autoColors||_costColors[card.OracleId].All(FocusColors.Contains);
    private string ChooseFocus(IReadOnlyDictionary<string,int>? capacity)
    {
        int Count(SynergyProfile p)=>Math.Min(p.Has(Land)?10000:4,capacity?.GetValueOrDefault(p.Card.OracleId)??(p.Has(Land)?10000:4));
        var options=new List<(string Colors,double Quality)>();
        for(var size=1;size<=5;size++)
        {
            for(var mask=1;mask<32;mask++)
            {
                var colors=new string("WUBRG".Where((_,i)=>(mask&(1<<i))!=0).ToArray());if(colors.Length!=size)continue;
                var spells=_profiles.Values.Where(p=>!p.Has(Land)&&_costColors[p.Card.OracleId].All(colors.Contains)).ToArray();
                if(spells.Sum(Count)<_slots)continue;
                if(_theme=="equipment"&&spells.Where(p=>SynergyEngine.CanReceiveEquipment(p)||p.Has(JobSelect)).Sum(Count)<_slots/3)continue;
                if(_theme=="opponent-death"&&_profiles.Values.Any(p=>p.Has(OpponentDeathPayoff))&&!spells.Any(p=>p.Has(OpponentDeathPayoff)))continue;
                var landCapacity=_profiles.Values.Where(p=>p.Card.IsBasicLand&&(_theme=="domain"||p.Card.Colors.All(colors.Contains))).Sum(p=>(long)Count(p));
                if(landCapacity<24)continue;
                var quality=spells.SelectMany(p=>Enumerable.Repeat(Affinity(p,_theme)+(p.Has(Creature)?_style=="aggro"?1.5:.5:0)-Math.Abs(p.Card.ManaValue-(_style=="aggro"?2:3))*.15,Count(p)))
                    .OrderByDescending(x=>x).Take(_slots).Sum()-size*1.5;
                options.Add((colors,quality));
            }
            if(size>=(_theme=="domain"?3:2)&&options.Count>0)break;
        }
        return options.OrderByDescending(x=>x.Quality).ThenBy(x=>x.Colors.Length).ThenBy(x=>x.Colors,StringComparer.Ordinal).Select(x=>x.Colors).FirstOrDefault()??"WUBRG";
    }
    public static int Affinity(SynergyProfile p, string theme)
    {
        if (theme == "any") return 0;
        if (theme == "domain" && p.Any(BasicLandSearch, PlainsSearch, FourTypeSearch)) return 3;
        if (theme == "exalted" && p.Any(Evasion, UnblockableSupport)) return 2;
        if (theme == "equipment" && p.Has(AttachableEquipment)) return 4;
        if (theme == "sacrifice" && p.Any(OwnCreatureSacrifice, OwnArtifactSacrifice, SelfArtifactSacrifice, TreasureOnDeath)) return 4;
        if(theme=="opponent-death"&&p.Has(OpponentDeathPayoff))return 4;
        if (theme == "graveyard" && p.Any(SelfMill, Surveil, Loot)) return 4;
        if (!p.Theme(theme)) return 0;
        return 3;
    }
}
