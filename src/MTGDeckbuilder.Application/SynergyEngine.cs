using MTGDeckbuilder.Domain;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application;

public sealed record SynergyRelation(string SupplierOracle, string ConsumerOracle, string Supplier, string Consumer,
    string Kind, string Theme, int Weight, string Explanation, string Condition, bool Reviewed)
{
    public string Key => SupplierOracle + "/" + ConsumerOracle + "/" + Kind;
    public bool CompatibilityOnly => Weight == 1;
    public string Label => Supplier + " → " + Consumer + ": " + Explanation + " Voorwaarde: " + Condition;
}

public sealed record SynergyAssessment(IReadOnlyList<SynergyProfile> Profiles, IReadOnlyList<SynergyRelation> Relations,
    IReadOnlyList<string> Warnings, IReadOnlyList<string> BasicTypes, int CreatureCopies, int EquipmentCopies, int JobSelectCopies)
{
    public int ReviewedCards => Profiles.Count(x => x.Reviewed);
    public int ChangedCards => Profiles.Count(x => x.Changed);
    public int LimitedCards => Profiles.Count(x => !x.Reviewed && !x.Changed && x.Features.Any(SynergyProfiles.IsSemantic));
    public int TypeOnlyCards => Profiles.Count - ReviewedCards - ChangedCards - LimitedCards;
    public string ProfileSummary => $"{ReviewedCards} gecontroleerde profielen · {LimitedCards} beperkt herkend · {TypeOnlyCards} alleen typen/onbekend · {ChangedCards} gewijzigd";
    public string WarningsText => Warnings.Count==0?"Geen aanvullende aandachtspunten binnen de ondersteunde herkenning.":string.Join("\n",Warnings.Select(x=>"• "+x));
    public IReadOnlyList<SynergyRelation> Positive => Relations.Where(x => x.Weight > 0).ToArray();
    public IReadOnlyList<SynergyRelation> Conflicts => Relations.Where(x => x.Weight < 0).ToArray();
    public string Summary => $"Synergiecontrole · {Positive.Count(x=>!x.CompatibilityOnly)} gerichte relaties · {Positive.Count(x=>x.CompatibilityOnly)} basiscombinaties · {Warnings.Count} aandachtspunten";
    public string Details => $"{SynergyProfiles.Version}\n{Summary}\n" +
        $"Profielen per unieke Oracle-kaart: {ReviewedCards} gecontroleerd, {LimitedCards} beperkt herkend, {TypeOnlyCards} alleen typefeiten, {ChangedCards} gewijzigd.\n" +
        $"{CreatureCopies} creaturekaarten · {EquipmentCopies} Equipment · {JobSelectCopies} Job select-kaarten. Kaarten in het deck zijn geen gelijktijdig aanwezige permanents.\n" +
        $"Basislandtypen mogelijk uit de landkaarten: {(BasicTypes.Count == 0 ? "geen" : string.Join(", ", BasicTypes))} ({BasicTypes.Count}/5), niet gegarandeerd op het slagveld.\n" +
        string.Join("\n", Warnings.Select(x => "Let op: " + x)) + "\n\nOndersteunde relaties (verschillende kaarten; kopieën tellen niet als extra relaties):\n" +
        (Positive.Count == 0 ? "Geen ondersteunde relatie tussen verschillende kaarten vastgesteld." : string.Join("\n", Positive.Take(18).Select(x => "• " + x.Label))) +
        (Positive.Count > 18 ? $"\nNog {Positive.Count - 18} relaties in het volledige rapport." : "") +
        (Conflicts.Count == 0 ? "" : "\n\nVoorwaardelijke conflicten:\n" + string.Join("\n", Conflicts.Take(8).Select(x => "• " + x.Label))) +
        "\n\nDit is deckbouwondersteuning, geen simulatie van speltoestand, oneindige combo, speelsterkte of castingkans. Lees ook de voorwaarden van iedere kaart.";
}

public static class SynergyEngine
{
    private static readonly string[] LandNames = ["Plains", "Island", "Swamp", "Mountain", "Forest"];
    public static IReadOnlyList<string> LandTypes(PaperCard card)
    {
        if(card.RequiresUnsupportedRules||SynergyProfiles.ComplexCard(card))return [];
        if (!card.TypeLine.Split([' ', '—', '/'], StringSplitOptions.RemoveEmptyEntries).Contains("Land")) return [];
        var words = card.TypeLine.Split([' ', '—', '/'], StringSplitOptions.RemoveEmptyEntries);
        return LandNames.Where(words.Contains).ToArray();
    }
    public static bool IsColoredPermanent(SynergyProfile profile, char color) => !SynergyProfiles.ComplexCard(profile.Card)&&!profile.Has(InstantSorcery) &&
        (profile.Card.PrintedColors ?? "").Contains(color);
    public static bool CreatureBody(SynergyProfile profile) => profile.Has(Creature);
    private static bool WantsCreatureDeath(SynergyProfile p)=>p.Any(OwnDeathPayoff,OtherOwnDeathPayoff,OwnCreatureDeathPayoff,EquippedDeathPayoff);
    private static bool WantsArtifactDeath(SynergyProfile p)=>p.Any(OwnDeathPayoff,OtherOwnDeathPayoff,OwnArtifactDeathPayoff);
    private static bool WantsOwnDeath(SynergyProfile p)=>WantsCreatureDeath(p)||WantsArtifactDeath(p);
    private static bool SacrificesSelf(SynergyProfile p)=>p.Any(SelfArtifactSacrifice,SelfCreatureSacrifice,SelfEnchantmentSacrifice,SelfLandSacrifice);
    public static bool CanReceiveEquipment(SynergyProfile profile) => CreatureBody(profile) && !profile.Any(Shroud,TargetingUncertain);
    public static bool CanTarget(SynergyProfile source,SynergyProfile target)
    {
        if(target.Any(Shroud,ProtectionEverything,TargetingUncertain))return false;
        if(target.Has(ProtectionArtifacts)&&source.Has(Artifact)||target.Has(ProtectionCreatures)&&source.Has(Creature))return false;
        SynergyFeature[] protections=[ProtectionWhite,ProtectionBlue,ProtectionBlack,ProtectionRed,ProtectionGreen];
        return !protections.Where((p,i)=>target.Has(p)&&(source.Card.PrintedColors??"").Contains("WUBRG"[i])).Any();
    }

    /// <summary>Relations describe prerequisites, never current battlefield state. No same-Oracle/self-pair inflation.</summary>
    public static IReadOnlyList<SynergyRelation> Between(SynergyProfile supply, SynergyProfile payoff)
    {
        if (supply.Card.OracleId == payoff.Card.OracleId) return [];
        if(supply.Card.RequiresUnsupportedRules||payoff.Card.RequiresUnsupportedRules||SynergyProfiles.ComplexCard(supply.Card)||SynergyProfiles.ComplexCard(payoff.Card))return [];
        var links = new List<SynergyRelation>();
        void Add(string kind, string theme, int weight, string explanation, string condition) =>
            links.Add(new(supply.Card.OracleId, payoff.Card.OracleId, supply.Card.Name, payoff.Card.Name, kind, theme, weight,
                explanation, condition, supply.Reviewed && payoff.Reviewed));
        if (supply.Has(Equipment) && payoff.Has(EquipmentCountPayoff))
            Add("equipment-count", "equipment", 3, "eigen Equipment ondersteunt een Equipment-aantalpayoff.", "Equipment moet onder jouw controle op het slagveld zijn; aanhechten is voor deze telling niet vereist. " + payoff.Notes);
        if (supply.Has(AttachableEquipment) && payoff.Any(EquippedCreaturePayoff, EquipmentDiscount))
        {
            if (CanTarget(supply,payoff)) Add("equipped-payoff", "equipment", 4, "Equipment kan een voorwaarde met uitgeruste creatures ondersteunen.",
                "Eerst een legaal uitgeruste eigen creature en het vereiste aanvalsmoment. Meerdere Equipment op één creature zijn niet meerdere uitgeruste creatures. " + payoff.Notes);
        }
        else if (supply.Has(AttachableEquipment) && CanReceiveEquipment(payoff)&&CanTarget(supply,payoff))
            Add("equipment-bearer", "equipment", 1, "creaturekaart biedt een mogelijke drager.", "Creature en Equipment moeten beschikbaar zijn; betaal de juiste equipkosten op het juiste moment en controleer doelwitregels/bescherming.");
        if (supply.Any(EquipmentDiscount, EquipmentMana, AttachEquipment) && payoff.Has(AttachableEquipment))
            Add("equipment-support", "equipment", 3, "kan betalen voor of aanhechten van Equipment ondersteunen.", supply.Notes + " " + payoff.Notes);
        if (supply.Has(AttachableEquipment) && payoff.Has(Shroud))
            Add("shroud-equip", "equipment", -4, "shroud verhindert eigen doelgerichte equip-/aanhechtacties.", "Zolang shroud geldt kan deze creature geen target van de equip-actie of Beatrix zijn; geen dragerbonus toegekend.");
        else if(supply.Has(AttachableEquipment)&&payoff.Has(Creature)&&payoff.Has(TargetingUncertain))
            Add("targeting-uncertain","equipment",0,"geen dragerbonus: doelwitbeperking is onvoldoende herkend.","Dit is geen vastgesteld permanent verbod. Lees de volledige bescherming/voorwaarde voordat je deze combinatie gebruikt.");
        else if(supply.Has(AttachableEquipment)&&payoff.Has(Creature)&&!CanTarget(supply,payoff))
            Add("protection-equip","equipment",-4,"herkende bescherming verhindert dit Equipment aan deze creature.","Kleur/kaarttype van de Equipment-bron valt onder de herkende protection van de drager; geen equip-dragerbonus.");
        if (supply.Has(TeamShroud) && payoff.Any(AttachableEquipment, AttachEquipment, UnblockableSupport))
            Add("temporary-shroud", "equipment", -1, "tijdelijke shroud kan eigen doelwitacties blokkeren.", "Alleen zolang shroud op de bedoelde creature geldt; eerst equip/targeten en pas daarna beschermen kan een andere volgorde zijn.");
        if (supply.Has(OwnCreatureTokens) && payoff.Has(TeamBuff))
            Add("tokens-buff", "tokens", 4, "eigen creatuurtokens kunnen van de teambonus profiteren.", "De tokens moeten vóór de bonus aanwezig zijn; respecteer de timing van beide kaarten. " + supply.Notes+" "+payoff.Notes);
        if(supply.Has(OwnCreatureTokens)&&payoff.Has(Convoke))
            Add("tokens-convoke","tokens",3,"eigen creatuurtokens kunnen bij convoke helpen betalen.","Alleen aanwezige untapped creatures; kleurloze tokens betalen generiek, niet willekeurige gekleurde kosten. Tokens die getapt/aanvallend binnenkomen zijn niet direct beschikbaar. "+supply.Notes+" "+payoff.Notes);
        if(supply.Has(OwnCreatureTokens)&&payoff.Has(OwnCreatureEnterPayoff)&&!payoff.Has(NonTokenEnterOnly)||supply.Has(OwnArtifactTokens)&&payoff.Has(OwnArtifactEnterPayoff)&&!payoff.Has(NonTokenEnterOnly))
            Add("token-enter","tokens",4,"passende eigen tokens kunnen de binnenkomstpayoff ondersteunen.","De juiste tokens moeten werkelijk onder jouw controle binnenkomen. Geen casttrigger; aantal, timing, andere-object- en overige voorwaarden blijven gelden. "+supply.Notes+" "+payoff.Notes);
        if(supply.Has(Creature)&&payoff.Has(OwnCreatureEnterPayoff)||supply.Has(Artifact)&&payoff.Has(OwnArtifactEnterPayoff))
            Add("permanent-enter","tokens",2,"de passende permanente kaart kan een eigen binnenkomsttrigger voeden.","De kaart moet werkelijk binnenkomen terwijl de payoff actief is; op hand/in het deck zijn is onvoldoende. "+payoff.Notes);
        if (supply.Has(ConditionalCopyToken) && payoff.Has(TeamBuff))
            Add("conditional-copy-buff", "tokens", 1, "kopietoken kan een creature zijn en dan de teambonus krijgen.", "Kies een legaal creature-target; een land- of niet-creature-artifactkopie voldoet niet.");
        if ((CreatureBody(supply) || supply.Has(OwnCreatureTokens) && !payoff.Has(NonTokenSacrificeOnly)) && payoff.Has(OwnCreatureSacrifice))
            Add("creature-fodder", "sacrifice", 3, "kan een eigen creature voor de sacrifice-kosten leveren.", "Eerst de eigen creature/token aanwezig; nontoken-/andere beperkingen, mana en timing van de outlet blijven gelden. " + payoff.Notes);
        if ((supply.Has(Artifact) || supply.Has(OwnArtifactTokens)&&!payoff.Has(NonTokenSacrificeOnly)) && payoff.Has(OwnArtifactSacrifice))
            Add("artifact-fodder", "sacrifice", 3, "kan een eigen artifact voor de sacrifice-kosten leveren.", "Eerst een passend eigen artifact/token aanwezig; voorwaarden/kosten van de outlet blijven gelden.");
        if(supply.Has(Artifact)&&supply.Has(Creature)&&payoff.Has(ArtifactCreatureSacrifice)&&CanTarget(payoff,supply))
            Add("artifact-creature-fodder","sacrifice",3,"is een mogelijke eigen artifact-creature voor de gerichte sacrifice-actie.","Beide kaarttypen en een legaal target zijn nodig; tapping/summoning sickness, kosten en controller van het doelwit blijven gelden. "+payoff.Notes);
        if(supply.Has(OwnCreatureSacrifice)&&payoff.Has(SelfCreatureDeathPayoff))
            Add("self-death-sacrifice","sacrifice",4,"kan specifiek de creature met de eigen sterfability opofferen.","Offer juist deze payoff-creature op, niet een willekeurige andere creature. Kosten, nontoken-/andere-objectbeperkingen en werkelijk sterven blijven vereist. Geen lus of gratis herhaling. "+supply.Notes+" "+payoff.Notes);
        if (supply.Has(OwnCreatureSacrifice)&&WantsCreatureDeath(payoff)||supply.Has(OwnArtifactSacrifice)&&WantsArtifactDeath(payoff)||
            SacrificesSelf(supply)&&(supply.Has(Creature)&&WantsCreatureDeath(payoff)||supply.Has(Artifact)&&WantsArtifactDeath(payoff))||
            supply.Has(TreasureOnDeath)&&WantsArtifactDeath(payoff)||supply.Has(ArtifactCreatureSacrifice)&&WantsOwnDeath(payoff))
            Add("own-sacrifice-death", "sacrifice", 4, "kan een echte eigen creature-/artifact-dood veroorzaken voor de eigen sterfpayoff.",
                "Het juiste object moet daadwerkelijk sterven; tokencreatie, opofferkosten en limieten blijven gelden. Een equipped-deathpayoff vereist eerst een uitgeruste creature. " + supply.Notes + " " + payoff.Notes);
        else if (!payoff.Has(NonTokenDeathOnly)&&(supply.Has(OwnCreatureTokens)&&WantsCreatureDeath(payoff)||supply.Has(OwnArtifactTokens)&&WantsArtifactDeath(payoff)))
            Add("tokens-death", "sacrifice", 1, "token kan een passend eigen sterfobject zijn.", "Het token moet eerst ontstaan en daarna daadwerkelijk sterven; deze combinatie alleen levert geen sacrifice-outlet. " + payoff.Notes);
        if(supply.Any(DelayedCreatureSacrifice,SymmetricCreatureSacrifice)&&WantsCreatureDeath(payoff))
            Add("conditional-sacrifice-death","sacrifice",2,"kan onder de voorgeschreven omstandigheden een eigen creature laten sterven.","Eerst een passend eigen object; terugkeer, controller, subtype, uitstel en timing blijven voorwaarden. Dit is geen vrij inzetbare of onbeperkte sacrifice-outlet. "+supply.Notes+" "+payoff.Notes);
        if (supply.Any(SelfMill, Surveil, Loot,SelfDiscard) && payoff.Any(CreatureRecursion,TypedCardRecursion,PermanentRecursion, SelfRecursion, Flashback, GraveyardSpellPayoff,GraveyardPermanentPayoff))
            Add("graveyard-setup", "graveyard", 4, "kan de eigen graveyard vullen voor hergebruik of een payoff.", "Er moet het juiste kaarttype in de graveyard terechtkomen; de gekozen/milled kaart en kosten zijn niet gegarandeerd. " + payoff.Notes);
        if (SynergyTargets.Accepts(payoff,supply.Card))
            Add(supply.Has(Creature)?"creature-recursion":"typed-recursion", "graveyard", 1, "is qua ondersteund kaarttype en mana value een mogelijk recursiontarget.", "De echte kaart moet eerst in jouw graveyard liggen. Tokens zijn geen blijvende kaarten; timing/kosten en overige voorwaarden blijven gelden. "+string.Join("; ",SynergyTargets.Recursion(payoff.Card).Select(r=>r.Description))+". "+payoff.Notes);
        if ((supply.Has(InstantSorcery) || supply.Has(SelfArtifactSacrifice)) && payoff.Has(GraveyardSpellPayoff))
            Add("graveyard-noncreature", "graveyard", 2, "kan als noncreature/nonlandkaart in de eigen graveyard meetellen.", "Pas na werkelijk in graveyard belanden; afhandelen/cast uit graveyard kan tot exile leiden. Landen, creatures en verdwenen tokens tellen niet. " + payoff.Notes);
        if (supply.Has(GraveyardHate) && payoff.Any(CreatureRecursion, SelfRecursion, Flashback, GraveyardSpellPayoff,GraveyardPermanentPayoff))
            Add("graveyard-conflict", "graveyard", -4, "eigen graveyard leegmaken kan het hergebruik/de payoff wegnemen.", "Alleen als de betreffende eigen kaarten worden verbannen; de kaarttekst en volgorde bepalen het werkelijke conflict.");
        if (LandTypes(supply.Card).Count > 0 && payoff.Has(DomainPayoff))
            Add("domain-type", "domain", 2, "land heeft een of meer echte basislandtypen voor Domain.", "Verschillende typen moeten onder jouw landen op het slagveld aanwezig zijn. Mana-kleuren en kopieën van hetzelfde type voegen geen nieuw type toe.");
        if (supply.Any(BasicLandSearch, PlainsSearch, FourTypeSearch,RestrictedBasicLandSearch) && payoff.Has(DomainPayoff))
            Add("domain-search", "domain", 3, "kan een ontbrekend basislandtype helpen vinden.", "Een passend zoekdoel moet nog in de library zitten. Naar hand zoeken is niet direct op het slagveld; Plainscycling zoekt alleen Plains, Shard Convergence geen Forest. " + supply.Notes);
        if (supply.Has(Exalted) && CreatureBody(payoff) && !payoff.Any(Defender,SoloAttackBlocked))
            Add("solo-attacker", "exalted", payoff.Has(Evasion) ? 3 : 1, payoff.Has(Evasion) ? "exalted kan een solo-aanvaller met evasion versterken." : "exalted kan één eigen solo-aanvaller versterken.",
                "Precies één creature als aanvaller verklaren. Exalted target niet, dus shroud verhindert deze bonus niet. Evasion garandeert geen schade.");
        if (supply.Has(UnblockableSupport) && CreatureBody(payoff) && payoff.Any(Exalted, EquippedCreaturePayoff, AttackDrawPayoff) && CanTarget(supply,payoff))
            Add("attacker-support", "exalted", 2, "kan de belangrijke aanvaller met ontwijking ondersteunen.", "Controleer het verleende effect, legaal target en timing; flying/menace zijn niet hetzelfde als onblokkeerbaar. " + supply.Notes);
        if (supply.Has(Evasion) && CreatureBody(supply) && payoff.Has(AttackDrawPayoff))
            Add("combat-draw", "draw", 3, "evasion kan combat damage aan een speler en daarmee draw ondersteunen.", "De creature moet werkelijk combat damage aan een speler doen; alleen aanvallen of flying hebben is onvoldoende.");
        if (supply.Has(OwnDraw) && payoff.Has(DrawPayoff))
            Add("draw-payoff", "draw", 3, "eigen draw kan de expliciete draw-payoff activeren.", "Er moet werkelijk een kaart worden getrokken; kaarttoegang via exile/reveal is geen draw. " + supply.Notes+" "+payoff.Notes);
        if (CreatureBody(supply) && int.TryParse(supply.Card.Power, out var power) && power >= 5 && payoff.Has(PowerFivePayoff))
            Add("power-five", "power", 3, "gedrukte power kan een power-vijfvoorwaarde ondersteunen.", "Daadwerkelijke power en de specifieke target-/ETB-voorwaarde moeten gelden; Paleoloth vereist een andere creature die binnenkomt. " + payoff.Notes);
        if (IsColoredPermanent(supply, 'G') && payoff.Has(GreenPermanentCondition))
            Add("green-permanent", "draw", 3, "is een groene permanent voor de conditionele draw.", "Die permanente kaart moet onder jouw controle op het slagveld zijn. Een normaal Forest is kleurloos.");
        if (IsColoredPermanent(supply, 'R') && payoff.Has(RedPermanentCondition))
            Add("red-permanent", "combat", 2, "is een rode permanent voor de voorwaarde.", "Die permanente kaart moet onder jouw controle op het slagveld zijn. Een normaal Mountain is kleurloos.");
        if (supply.Any(DestroyRemoval, ConditionalToughnessRemoval,OpponentCreatureSacrifice) && payoff.Has(OpponentDeathPayoff))
            Add("opponent-death", "opponent-death", supply.Has(DestroyRemoval) ? 4 : 2, "kan een vijandelijke creature laten sterven voor de payoff.",
                "Richt de werking op een vijandelijke creature; zij moet werkelijk sterven. Damage/-X/-X heeft eigen voorwaarden, exile is geen sterven. " + supply.Notes+" "+payoff.Notes);
        if (supply.Any(ExileRemoval, DeathToExile) && payoff.Has(OpponentDeathPayoff))
            Add("exile-not-death", "opponent-death", -2, "exile levert de gevraagde sterftrigger niet; vervangende exile kan die verhinderen.",
                "Dit geldt voor het verbannen object en de gekozen modus; exile kan nog andere nuttige redenen hebben. " + supply.Notes);
        if (supply.Has(NoncreatureNonland) && payoff.Has(NoncreatureCastPayoff) && (!payoff.Has(NeedsFourManaSpent) || supply.Card.ManaValue >= 4))
            Add("noncreature-cast", "spells", 2, "kan een noncreature-casttrigger ondersteunen.", "Echt casten vereist; Sahagin vraagt minstens vier bestede mana, niet alleen gedrukte MV. Shambling Cie'th moet in graveyard zijn en vraagt extra {B}. " + payoff.Notes);
        if(SacrificesSelf(supply)&&payoff.Has(GraveyardPermanentPayoff))
            Add("graveyard-permanent","graveyard",2,"opgeofferde permanente KAART kan voor de graveyarddrempel meetellen.","Het object moet als echte permanente kaart in jouw graveyard belanden; een token verdwijnt en telt niet blijvend mee. "+payoff.Notes);
        if(supply.Has(SelfCycling)&&supply.Has(Creature)&&payoff.Has(CreatureRecursion))
            Add("cycle-recursion","graveyard",3,"cycling kan deze creatureKAART in de eigen graveyard leggen voor hergebruik.","Kies cycling in plaats van normaal casten, betaal kosten en houd de kaart in graveyard als legaal target. "+payoff.Notes);
        if(supply.Has(SelfCycling)&&supply.Has(NoncreatureNonland)&&payoff.Has(GraveyardSpellPayoff))
            Add("cycle-spell-card","graveyard",2,"cycling van deze noncreature/nonlandkaart kan voor de graveyardtelling helpen.","De eigen kaart zelf moet na discard in graveyard blijven. Cycling van een creaturekaart telt niet voor deze payoff.");
        if(supply.Has(SelfCycling)&&!supply.Has(InstantSorcery)&&payoff.Has(GraveyardPermanentPayoff))
            Add("cycle-permanent","graveyard",2,"cycling van een permanente KAART kan de graveyarddrempel ondersteunen.","Het moet werkelijk een permanente kaart zijn en in eigen graveyard blijven; niet iedere cycling-spell is een permanente kaart.");
        if(payoff.Has(NamedGraveyardRetrieval)&&SynergyTextRules.NamedTargets(payoff.Card).Contains(supply.Card.Name,StringComparer.OrdinalIgnoreCase))
            Add("named-graveyard-target","graveyard",3,"is de expliciet genoemde kaart voor deze zoekactie.","De genoemde eigen kaart moet in de toegestane zone liggen; namen zijn niet uitwisselbaar en dit is geen algemene creature-recursion. "+payoff.Notes);
        if(supply.Has(CreatureRecursion)&&payoff.Any(SelfRecursion)&&payoff.Has(NoncreatureCastPayoff))
            Add("recursion-target-timing","graveyard",-1,"terugkeer bij spellcast kan hetzelfde reanimatietarget voortijdig naar hand verplaatsen.","Alleen als je deze creature als target koos en de optionele eigen terugkeer gebruikt voordat de andere spell afhandelt. Kies een ander target of gebruik de optie niet.");
        if(supply.Any(Artifact,OwnArtifactTokens)&&payoff.Has(ArtifactAffinity))
            Add("artifact-affinity","artifacts",3,"eigen artifact kan de generieke affinity-kosten verlagen.","Artifact op het slagveld onder jouw controle tijdens kostenbepaling; geen verlaging van gekleurde kosten. "+payoff.Notes);
        if(supply.Has(Artifact)&&payoff.Has(ArtifactCastPayoff)||supply.Has(InstantSorcery)&&payoff.Has(InstantSorceryCastPayoff)||supply.Any(Artifact,InstantSorcery)&&payoff.Has(ArtifactInstantSorceryCastPayoff))
            Add("specified-spell-cast","tokens",3,"cast van een ondersteund spelltype kan de payoff activeren.","De juiste kaart echt casten; niet alleen op slagveld plaatsen. De genoemde spelltypen en wie het effect krijgt zijn leidend. "+payoff.Notes);
        if(supply.Has(ExileCardAccess)&&payoff.Has(CastFromExilePayoff))
            Add("cast-from-exile","tokens",3,"kaarttoegang vanuit exile kan een cast-uit-exiletrigger ondersteunen.","Een toegestane SPELL werkelijk uit exile casten binnen de toegestane termijn; een land spelen is geen spellcast. "+supply.Notes+" "+payoff.Notes);
        if(CreatureBody(supply)&&!supply.Any(Defender,SoloAttackBlocked)&&payoff.Has(AttackCreaturePayoff)&&
            (!payoff.Has(NeedsNonGnomeAttacker)||!supply.Card.TypeLine.Split([' ','—','/'],StringSplitOptions.RemoveEmptyEntries).Contains("Gnome")))
            Add("specified-attacker","tokens",2,"kan een passend eigen aanvallend creature voor de trigger leveren.","De creature moet echt aanvallen en aan het gevraagde type voldoen; tokenaantal/counters en tapped-and-attacking volgen de kaarttekst. "+payoff.Notes);
        if(supply.Has(OwnLifeGainThree)&&payoff.Has(RequiresLifeGainThree))
            Add("life-three","tokens",3,"kan de vereiste drie gewonnen levens in een beurt ondersteunen.","Leven moet werkelijk vóór het relevante end step zijn gewonnen; controleer timing en eventuele vervangende effecten. "+supply.Notes+" "+payoff.Notes);
        return links;
    }

    public static SynergyAssessment ForTheme(SynergyAssessment result,string theme)
    {
        var notes=result.Warnings.ToList();bool Any(params SynergyFeature[] tags)=>result.Profiles.Any(p=>p.Any(tags));
        if(theme=="tokens"&&!Any(OwnCreatureTokens,OwnArtifactTokens,OwnEnchantmentTokens,OwnUnknownTokens,ConditionalCopyToken))notes.Add("Token-thema zonder herkende tokenproducent; alleen een payoff/buff is geen complete tokenstrategie.");
        if(theme=="tokens"&&!Any(OwnCreatureTokens)&&Any(OwnArtifactTokens,OwnEnchantmentTokens,OwnUnknownTokens))notes.Add("Geen creatuurtokenproductie herkend; artifact-, enchantment- of onbekende tokens zijn niet automatisch extra creatures voor buffs/aanvallen.");
        if(theme=="graveyard"&&!Any(CreatureRecursion,PermanentRecursion,SelfRecursion,Flashback,GraveyardSpellPayoff,GraveyardPermanentPayoff,NamedGraveyardRetrieval))notes.Add("Graveyard-thema zonder herkend hergebruik/payoff; alleen graveyard vullen is geen complete strategie.");
        if(theme=="opponent-death"&&!Any(OpponentDeathPayoff))notes.Add("Alleen removalrollen herkend; geen vijandelijke sterfpayoff in dit voorstel. Removal op zichzelf bewijst geen sterftrigger-synergie.");
        if(theme=="draw"&&!Any(OwnDraw,Loot,DeathDraw,AttackDrawPayoff))notes.Add("Draw-thema zonder herkende extra eigen draw-source; een payoff moet nog werkelijk worden gevoed.");
        return result with{Warnings=notes.Distinct().ToArray()};
    }
    public static SynergyAssessment Assess(IEnumerable<PaperDeckLine> lines)
    {
        var active = lines.Where(x => x.Zone is DeckZone.Mainboard or DeckZone.Commander && x.Quantity > 0).ToArray();
        var groups = active.GroupBy(x => x.Card.OracleId).ToArray();
        var profiles = groups.Select(g =>
        {
            var p = SynergyProfiles.Analyze(g.First().Card);
            return g.Select(x => SynergyProfiles.Fingerprint(x.Card)).Distinct().Count() <= 1 ? p : p with
            { Changed = true, Reviewed = false, Features = p.Features.Where(f => !SynergyProfiles.IsSemantic(f)).ToHashSet(), Notes = "Gameplaymetadata verschilt tussen uitgaven van dezelfde Oracle-kaart; geen gecontroleerde tekstfeatures gebruikt." };
        }).OrderBy(x => x.Card.Name, StringComparer.Ordinal).ToArray();
        var links = profiles.SelectMany(a => profiles.SelectMany(b => Between(a, b))).DistinctBy(x => x.Key)
            .OrderByDescending(x => x.Weight).ThenBy(x => x.Supplier, StringComparer.Ordinal).ThenBy(x => x.Consumer, StringComparer.Ordinal).ThenBy(x => x.Kind, StringComparer.Ordinal).ToArray();
        int Copies(SynergyFeature tag) => groups.Where(g => profiles.Single(p => p.Card.OracleId == g.Key).Has(tag)).Sum(g => g.Sum(x => x.Quantity));
        var types = LandNames.Where(t => active.Any(x => x.Zone == DeckZone.Mainboard && LandTypes(x.Card).Contains(t))).ToArray();
        var warnings = new List<string>();
        bool Any(params SynergyFeature[] tags) => profiles.Any(p => p.Any(tags));
        var creatures = Copies(Creature); var equipment = Copies(Equipment); var jobs = Copies(JobSelect);
        if (equipment > 0 && !profiles.Any(CanReceiveEquipment) && jobs == 0) warnings.Add("Equipment zonder herkende legale drager of Job select-body. Shroud-creatures zijn geen equip-doelwit.");
        if (equipment > Math.Max(6, creatures + jobs)) warnings.Add("Veel Equipment tegenover creaturekaarten/Job select; een stapel Equipment levert geen evenredig aantal uitgeruste creatures op.");
        if (jobs > 0) warnings.Add($"{jobs} Job select-kaarten leveren een eigen token als zij binnenkomen; verplaatsen naar bijvoorbeeld Cloud vraagt daarna nog de juiste equip-/aanhechtactie.");
        if (Any(EquippedCreaturePayoff)) warnings.Add("Uitgeruste creatures tellen is iets anders dan Equipment tellen. Eén drager met meerdere Equipment is nog steeds één uitgeruste creature.");
        if (Any(DomainPayoff) && types.Length < 3) warnings.Add($"Domain heeft in deze landkaarten slechts {types.Length}/5 mogelijke basislandtypen; geen kleuren mana als extra typen geteld.");
        if (Any(DomainPayoff) && Any(BasicLandSearch, PlainsSearch, FourTypeSearch) && !active.Any(x => x.Card.IsBasicLand)) warnings.Add("Landzoekers zonder basislanden in deze lijst leveren geen aangetoonde Domain-aanvulling; controleer de precieze zoektypen.");
        if (profiles.Any(WantsOwnDeath) && !Any(OwnCreatureSacrifice, OwnArtifactSacrifice, SelfArtifactSacrifice, SelfCreatureSacrifice, TreasureOnDeath)) warnings.Add("Eigen sterfpayoffs zonder herkende eigen creature-/artifact-sacrificebron. Sterven in combat is mogelijk, maar dit is geen aangetoonde sacrifice-engine.");
        else if(profiles.Any(WantsOwnDeath)&&!Any(OwnCreatureSacrifice))warnings.Add("Geen herkende creature-sacrifice-outlet: de eigen sterfpayoffs steunen hier op sterven in combat en/of beperkte artifact-/Treasure-routes. Geen vrij herhaalbare creature-sacrifice-engine vastgesteld.");
        if (profiles.Any(p=>p.Has(OpponentDeathPayoff)&&!WantsCreatureDeath(p))) warnings.Add("Een of meer sterfpayoffs kijken uitsluitend naar vijandelijke creatures; eigen creatures opofferen activeert die specifieke voorwaarde niet.");
        if(Any(ArtifactCreatureSacrifice)&&!profiles.Any(p=>p.Has(Creature)&&p.Has(Artifact)))warnings.Add("Gerichte artifact-creature-sacrifice zonder herkende artifact-CREATURE-kaart. Losse artifacts en losse creatures voldoen niet aan beide typen tegelijk; tokens vragen aparte typecontrole.");
        if (Any(CreatureRecursion, SelfRecursion, Flashback, GraveyardSpellPayoff,GraveyardPermanentPayoff) && !Any(SelfMill, Surveil, Loot,SelfDiscard)&&!links.Any(r=>r.Kind.StartsWith("cycle-",StringComparison.Ordinal)&&r.Weight>1)) warnings.Add("Graveyardplan zonder herkende passende self-mill/surveil/loot/cycling. Geschikte kaarten moeten eerst werkelijk in de eigen graveyard komen.");
        if (Any(CreatureRecursion) && !Any(Creature)) warnings.Add("Creature-recursion heeft hier geen creatureKAART als doelwitbron; creatuurtokens zijn geen blijvende kaarten in de graveyard.");
        if (Any(Exalted) && Copies(OwnCreatureTokens) >= 4 && Any(TeamBuff)) warnings.Add("Twee aanvalsplannen: exalted vraagt één aanvaller; een tokenleger met teambonus kan breed willen aanvallen. De bonussen niet allebei als gegarandeerd tellen.");
        if (Any(PowerFivePayoff) && !profiles.Any(p => p.Has(Creature) && int.TryParse(p.Card.Power, out var n) && n >= 5)) warnings.Add("Power-vijfvoorwaarde zonder creature met gedrukte power vijf of meer; variabele power/buffs vragen echte speltoestand.");
        if (Any(GreenPermanentCondition) && !profiles.Any(p => IsColoredPermanent(p, 'G'))) warnings.Add("Conditionele draw mist een herkende groene permanent; groene mana uit Forest is niet genoeg.");
        if (Any(RedPermanentCondition) && !profiles.Any(p => IsColoredPermanent(p, 'R'))) warnings.Add("Voorwaarde mist een herkende rode permanent; rode mana uit Mountain is niet genoeg.");
        if (profiles.Any(p => p.Changed)) warnings.Add("Gewijzigde gecontroleerde kaartmetadata: betrokken tekstfeatures niet vertrouwd. Laat deze profielen opnieuw beoordelen.");
        if(Any(TargetingUncertain))warnings.Add("Doelwitbeperkingen op een of meer kaarten zijn niet volledig herkend; voor die kaarten geen positieve equip-dragerclaim gedaan.");
        if(Any(CastFromExilePayoff)&&!Any(ExileCardAccess))warnings.Add("Cast-uit-exilepayoff zonder herkende kaarttoegang vanuit exile; overige kaartwerking kan buiten de herkenning vallen.");
        if(Any(Modal))warnings.Add("Modale effecten zijn mogelijke afzonderlijke effecten; het rapport bewijst niet dat zij in dezelfde cast tegelijk kunnen worden gekozen.");
        if(profiles.Any(p=>SynergyProfiles.ComplexCard(p.Card)))warnings.Add("Kaart met meerdere kanten/Adventure/split: kosten en effecten niet samengevoegd, geen automatische positieve relatie of selectie. Handmatige controle per kant vereist.");
        if(profiles.Any(p=>p.Card.RequiresUnsupportedRules))warnings.Add("Afwijkende acorn/silver-border/host-augment-spelregels worden niet automatisch beoordeeld; handmatige controle vereist.");
        var leader=active.FirstOrDefault(x=>x.Zone==DeckZone.Commander);
        if(leader is not null)
        {
            var profile=profiles.FirstOrDefault(p=>p.Card.OracleId==leader.Card.OracleId);
            if(profile is not null&&!profile.Reviewed&&!profile.Features.Any(SynergyProfiles.IsSemantic))warnings.Add("Commanderwerking alleen als typefeiten herkend: dit is een algemene compositie binnen kleur/voorraad, geen beoordeeld build-aroundplan voor deze commander.");
            else if(profile is not null&&!profile.Reviewed)warnings.Add("Commanderwerking slechts gedeeltelijk herkend; onbekende subtype-, teller- en timingvoorwaarden sturen de selectie mogelijk niet. Geen volledig commanderplan bewezen.");
        }
        foreach(var p in profiles.Where(p=>!SynergyProfiles.ComplexCard(p.Card)&&SynergyProfiles.AutomaticSelectionIssue(p.Card) is not null))warnings.Add(p.Card.Name+": "+SynergyProfiles.AutomaticSelectionIssue(p.Card)+" Handmatig gebruik vraagt een eigen beoordeling; deze kaart wordt niet automatisch gekozen.");
        foreach(var named in profiles.Where(p=>p.Has(NamedGraveyardRetrieval)))
        {
            var targets=SynergyTextRules.NamedTargets(named.Card);
            if(targets.Count==0)warnings.Add(named.Card.Name+": specifieke kaartnaam niet volledig herkend; geen algemene recursionbonus toegekend.");
            else if(!profiles.Any(p=>targets.Contains(p.Card.Name,StringComparer.OrdinalIgnoreCase)))warnings.Add(named.Card.Name+": zoekt specifiek "+string.Join(" / ",targets)+"; die kaart niet in het geladen hoofddeck herkend.");
        }
        if(profiles.Any(p=>p.Has(OpponentDraw)&&p.Any(OwnCreatureSacrifice,OwnArtifactSacrifice)))warnings.Add("Een sacrifice-outlet laat de tegenstander kaarten trekken. Dit nadeel niet als gratis of winstgevende lus beoordelen.");
        if(Any(TokensEnterAttacking)&&Any(Exalted))warnings.Add("Tokens die al aanvallend binnenkomen zijn niet als aanvaller verklaard; zij maken een eerder solo verklaarde aanval niet automatisch ongedaan voor exalted.");
        if (profiles.Any(p => !p.Reviewed && !p.Changed)) warnings.Add("Kaarten buiten de gecontroleerde productpool hebben beperkte of onbekende werking; ontbrekende relaties zijn geen bewijs dat zij niet bestaan.");
        return new(profiles, links, warnings.Distinct().ToArray(), types, creatures, equipment, jobs);
    }
}

