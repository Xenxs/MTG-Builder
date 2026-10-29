using System.Text.Json;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application;

/// <summary>
/// Numeric selection summaries share only traits used by Between. Detailed reports always
/// call Between with the real cards. New relation predicates must update Key and equivalence tests.
/// No Oracle-pair graph or first-N card truncation is built.
/// </summary>
internal sealed class SynergyInteractionIndex
{
    internal readonly record struct Edge(string Theme,int Weight);
    private readonly Dictionary<string,int> _oracleGroups=new(StringComparer.Ordinal);
    private readonly List<SynergyProfile> _representatives=[];
    private readonly Dictionary<(int,int),Edge[]> _pairs=[];
    private readonly Dictionary<(int,bool),int> _best=[];
    private readonly Dictionary<int,int> _focusedCounts=[];
    private readonly HashSet<string> _focusedOracles=new(StringComparer.Ordinal);
    private readonly string _theme;
    private readonly IReadOnlySet<string> _named;
    private readonly int[] _manaThresholds;
    public int GroupCount=>_representatives.Count;
    public int Group(string oracle)=>_oracleGroups[oracle];
    public SynergyInteractionIndex(IEnumerable<SynergyProfile> profiles,Func<PaperCard,bool> fitsFocus,string theme)
    {
        _theme=theme;var all=profiles.ToArray();var named=all.Where(p=>p.Has(NamedGraveyardRetrieval)).SelectMany(p=>SynergyTextRules.NamedTargets(p.Card)).ToHashSet(StringComparer.OrdinalIgnoreCase);_named=named;
        _manaThresholds=all.Where(SynergyTargets.Reuses).SelectMany(p=>SynergyTargets.Recursion(p.Card)).Where(r=>r.MaximumManaValue is not null).Select(r=>r.MaximumManaValue!.Value).Distinct().Order().ToArray();
        var keys=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var p in all)
        {
            var key=Key(p,named,_manaThresholds);if(!keys.TryGetValue(key,out var group)){group=_representatives.Count;keys[key]=group;_representatives.Add(p);}
            _oracleGroups[p.Card.OracleId]=group;
            if(!p.Has(Land)&&fitsFocus(p.Card)){_focusedOracles.Add(p.Card.OracleId);_focusedCounts[group]=_focusedCounts.GetValueOrDefault(group)+1;}
        }
    }
    private static string Key(SynergyProfile p,IReadOnlySet<string> named,int[] thresholds)=>JsonSerializer.Serialize(new
    {
        Features=p.Features.OrderBy(f=>f).ToArray(),Complex=SynergyProfiles.ComplexCard(p.Card),p.Card.RequiresUnsupportedRules,
        Colors=p.Card.PrintedColors??"",PowerFive=int.TryParse(p.Card.Power,out var power)&&power>=5,
        FourMana=p.Card.ManaValue>=4,Gnome=p.Card.TypeLine.Split([' ','—','/'],StringSplitOptions.RemoveEmptyEntries).Contains("Gnome"),
        LandTypes=SynergyEngine.LandTypes(p.Card),Name=named.Contains(p.Card.Name)?p.Card.Name.ToUpperInvariant():"",
        CardTypes=p.Card.TypeLine.Split([' ','—','/'],StringSplitOptions.RemoveEmptyEntries).Where(x=>x is "Creature" or "Artifact" or "Enchantment" or "Land" or "Planeswalker" or "Battle").Order().ToArray(),
        ManaBands=thresholds.Select(n=>p.Card.ManaValue<=n).ToArray(),Recursion=SynergyTargets.Reuses(p)?SynergyTargets.Recursion(p.Card):[],
        Targets=p.Has(NamedGraveyardRetrieval)?SynergyTextRules.NamedTargets(p.Card).Order(StringComparer.OrdinalIgnoreCase).Select(x=>x.ToUpperInvariant()).ToArray():[]
    });
    public Edge[] Pair(int a,int b)
    {
        if(_pairs.TryGetValue((a,b),out var value))return value;
        var first=_representatives[a];var second=_representatives[b];
        if(first.Has(Land)||second.Has(Land))value=[];
        else
        {
            // Distinct synthetic identity prevents self-Oracle suppression for two real cards
            // with equal traits. Score subtracts the actual same-Oracle contribution separately.
            var left=first with{Card=first.Card with{OracleId="numeric-left"}};
            var right=second with{Card=second.Card with{OracleId="numeric-right"}};
            value=SynergyEngine.Between(left,right).Concat(SynergyEngine.Between(right,left)).Select(x=>new Edge(x.Theme,x.Weight)).ToArray();
        }
        _pairs[(a,b)]=value;return value;
    }
    public (double Context,double Compatibility) Contribution(int candidate,int other,bool commander)
    {
        var edges=Pair(candidate,other);
        var context=edges.Where(x=>x.Weight!=1).Sum(x=>x.Weight*(x.Theme==_theme||commander?1:_theme=="any"?.45:.12));
        return(Math.Clamp(context,-4,5),edges.Count(x=>x.Weight==1)*(_theme=="any"||_theme=="equipment"?.3:.1));
    }
    public int Best(string oracle)
    {
        var g=Group(oracle);var ownFocus=_focusedOracles.Contains(oracle);if(_best.TryGetValue((g,ownFocus),out var result))return result;
        var ceiling=UpperBound(_representatives[g],_theme,_named.Contains(_representatives[g].Card.Name));if(ceiling==0){_best[(g,ownFocus)]=0;return 0;}
        foreach(var (other,count) in _focusedCounts)
        {
            if(g==other&&count-(ownFocus?1:0)<=0)continue;
            var maximum=Pair(g,other).Where(x=>x.Weight>0&&(_theme=="any"||x.Theme==_theme)).Select(x=>x.Weight).DefaultIfEmpty().Max();result=Math.Max(result,maximum);
            if(result>=ceiling)break;
        }
        _best[(g,ownFocus)]=result;return result;
    }
    // Upper bounds only stop a proven maximum search; they never remove a card.
    // Keep in sync with Between; differential/property checks cover every supported theme.
    internal static int UpperBound(SynergyProfile p,string theme,bool namedTarget)
    {
        if(p.Card.RequiresUnsupportedRules||p.Has(Land)||SynergyProfiles.ComplexCard(p.Card))return 0;
        bool Four()=>p.Any(AttachableEquipment,EquippedCreaturePayoff,EquipmentDiscount,OwnCreatureTokens,OwnArtifactTokens,TeamBuff,
            OwnCreatureSacrifice,OwnArtifactSacrifice,SelfArtifactSacrifice,SelfCreatureSacrifice,TreasureOnDeath,OwnDeathPayoff,OtherOwnDeathPayoff,
            OwnCreatureDeathPayoff,OwnArtifactDeathPayoff,EquippedDeathPayoff,OpponentDeathPayoff,DestroyRemoval,SelfMill,Surveil,Loot,SelfDiscard,
            CreatureRecursion,TypedCardRecursion,PermanentRecursion,SelfRecursion,Flashback,GraveyardSpellPayoff,GraveyardPermanentPayoff,OwnCreatureEnterPayoff,OwnArtifactEnterPayoff,ArtifactCreatureSacrifice,SelfCreatureDeathPayoff);
        return theme switch
        {
            "any"=>Four()?4:3,
            "equipment"=>p.Any(AttachableEquipment,EquippedCreaturePayoff,EquipmentDiscount)?4:p.Any(Equipment,EquipmentCountPayoff,EquipmentMana,AttachEquipment)?3:p.Has(Creature)?1:0,
            "domain"=>p.Any(DomainPayoff,BasicLandSearch,PlainsSearch,FourTypeSearch,RestrictedBasicLandSearch)?3:0,
            "opponent-death"=>p.Any(OpponentDeathPayoff,DestroyRemoval)?4:p.Any(ConditionalToughnessRemoval,OpponentCreatureSacrifice,SymmetricCreatureSacrifice)?2:0,
            "exalted"=>p.Has(Exalted)?3:p.Has(UnblockableSupport)?2:p.Has(Creature)&&!p.Any(Defender,SoloAttackBlocked)?(p.Has(Evasion)?3:2):0,
            "draw"=>p.Any(OwnDraw,DrawPayoff,AttackDrawPayoff,GreenPermanentCondition)||p.Has(Creature)&&p.Has(Evasion)||SynergyEngine.IsColoredPermanent(p,'G')?3:0,
            "sacrifice"=>p.Any(OwnCreatureSacrifice,OwnArtifactSacrifice,SelfArtifactSacrifice,SelfCreatureSacrifice,TreasureOnDeath,OwnDeathPayoff,OtherOwnDeathPayoff,OwnCreatureDeathPayoff,OwnArtifactDeathPayoff,EquippedDeathPayoff,ArtifactCreatureSacrifice,SelfCreatureDeathPayoff)?4:p.Any(Creature,Artifact,OwnCreatureTokens,OwnArtifactTokens)?3:p.Any(DelayedCreatureSacrifice,SymmetricCreatureSacrifice)?2:0,
            "graveyard"=>p.Any(SelfMill,Surveil,Loot,SelfDiscard,CreatureRecursion,TypedCardRecursion,PermanentRecursion,SelfRecursion,Flashback,GraveyardSpellPayoff,GraveyardPermanentPayoff)?4:namedTarget||p.Any(SelfCycling,NamedGraveyardRetrieval)?3:p.Any(InstantSorcery,SelfArtifactSacrifice,SelfCreatureSacrifice,SelfEnchantmentSacrifice,SelfLandSacrifice)?2:p.Any(Creature,Artifact,NoncreatureNonland)?1:0,
            "tokens"=>p.Any(OwnCreatureTokens,OwnArtifactTokens,TeamBuff,OwnCreatureEnterPayoff,OwnArtifactEnterPayoff)?4:p.Any(Artifact,InstantSorcery,ArtifactCastPayoff,ArtifactInstantSorceryCastPayoff,InstantSorceryCastPayoff,Convoke,ExileCardAccess,CastFromExilePayoff,OwnLifeGainThree,RequiresLifeGainThree)?3:p.Any(Creature,AttackCreaturePayoff)?2:p.Has(ConditionalCopyToken)?1:0,
            _=>4
        };
    }
}
