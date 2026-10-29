using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application;

public sealed record SynergyReplacementMatch(bool ThematicRole, int ContextChange, string Explanation);
public static class SynergyReplacement
{
    private static readonly SynergyFeature[] Roles = [Equipment,OwnDraw,CreatureRecursion,SelfMill,Surveil,Exalted,
        DestroyRemoval,ConditionalToughnessRemoval,OwnCreatureTokens,EquipmentCountPayoff,EquippedCreaturePayoff,
        OwnDeathPayoff,OtherOwnDeathPayoff,DomainPayoff];
    public static SynergyReplacementMatch Compare(PaperCard source,PaperCard candidate,IEnumerable<PaperDeckLine> deck)
    {
        var from=SynergyProfiles.Analyze(source);var to=SynergyProfiles.Analyze(candidate);
        var common=Roles.Any(role=>from.Has(role)&&to.Has(role));
        var sameType=from.Has(Creature)==to.Has(Creature)&&from.Has(Equipment)==to.Has(Equipment)&&from.Has(Land)==to.Has(Land);
        if(!from.Reviewed||!to.Reviewed||!common||!sameType)return new(false,0,"");
        var context=deck.Where(x=>x.Zone!=MTGDeckbuilder.Domain.DeckZone.Sideboard&&x.Card.OracleId!=source.OracleId&&x.Card.OracleId!=candidate.OracleId)
            .Select(x=>SynergyProfiles.Analyze(x.Card)).DistinctBy(x=>x.Card.OracleId).ToArray();
        SynergyRelation[] Links(SynergyProfile p)=>context.SelectMany(other=>SynergyEngine.Between(p,other).Concat(SynergyEngine.Between(other,p))).Where(x=>!x.CompatibilityOnly).ToArray();
        int Fit(IEnumerable<SynergyRelation> links)=>links.GroupBy(x=>x.Kind).Sum(g=>g.MaxBy(x=>Math.Abs(x.Weight))!.Weight);
        var before=Links(from);var after=Links(to);var delta=Fit(after)-Fit(before);
        var detail=after.Where(x=>x.Weight>0).OrderByDescending(x=>x.Weight).Take(1).Select(x=>x.Label).FirstOrDefault()??"Geen extra gerichte relatie in dit deck herkend.";
        return new(true,delta,"Zelfde ondersteunde themarol, geen functie-equivalente vervanging. "+detail+" Controleer afwijkend effect, kosten, timing en de volledige lijst.");
    }
}
