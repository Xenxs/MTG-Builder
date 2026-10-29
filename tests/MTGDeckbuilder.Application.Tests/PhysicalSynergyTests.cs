using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class PhysicalSynergyTests
{
    private static readonly PaperCard[] Cards = Load();
    private static PaperCard[] Load()
    {
        var cards=new List<PaperCard>();
        foreach(var resource in new[]{"conflux-paper-v1","final-fantasy-starter-v1"})
        {
            using var stream=typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources."+resource+".json")!;
            using var doc=JsonDocument.Parse(stream);var fetched=doc.RootElement.GetProperty("FetchedUtc").GetDateTime();
            cards.AddRange(doc.RootElement.GetProperty("Cards").EnumerateArray().Select(c=>ScryfallPaperCatalog.Parse(c.GetRawText(),fetched)));
        }
        return cards.ToArray();
    }
    private static PaperCard Card(string name)=>Cards.First(c=>c.Name==name);
    private static SynergyProfile Profile(string name)=>SynergyProfiles.Analyze(Card(name));
    private static PaperDeckLine Line(string name,int quantity=1)=>new(Card(name),DeckZone.Mainboard,quantity,quantity);
    [Fact]
    public void AllProductPrintingsHaveAnExactReviewedProfileAndReprintsAgree()
    {
        Assert.Equal(109,Cards.Length);Assert.Equal(87,Cards.Select(c=>c.OracleId).Distinct().Count());
        Assert.All(Cards,c=>{var p=SynergyProfiles.Analyze(c);Assert.True(p.Reviewed,c.Name+": "+p.Status);Assert.False(p.Changed);});
        foreach(var group in Cards.GroupBy(c=>c.OracleId))Assert.Single(group.Select(SynergyProfiles.Fingerprint).Distinct());
    }
    [Theory]
    [InlineData("Warrior's Sword","Cloud, Planet's Champion","equipped-payoff")]
    [InlineData("Warrior's Sword","Adelbert Steiner","equipment-count")]
    [InlineData("Beatrix, Loyal General","Warrior's Sword","equipment-support")]
    [InlineData("Kaleidostone","Al Bhed Salvagers","own-sacrifice-death")]
    [InlineData("Kaleidostone","G'raha Tia","own-sacrifice-death")]
    [InlineData("Sephiroth's Intervention","Sephiroth, Planet's Heir","opponent-death")]
    [InlineData("Shinra Reinforcements","Evil Reawakened","graveyard-setup")]
    [InlineData("Dreams of Laguna","Xande, Dark Mage","graveyard-setup")]
    [InlineData("Woolly Thoctar","Paleoloth","power-five")]
    [InlineData("Aven Squire","Suntail Hawk","solo-attacker")]
    [InlineData("Aven Squire","Deft Duelist","solo-attacker")]
    [InlineData("The Crystal's Chosen","Gleam of Resistance","tokens-buff")]
    [InlineData("Rhox War Monk","Rhox Meditant","green-permanent")]
    public void RealRelationshipsHaveSpecificConditions(string source,string target,string kind)
    {
        var link=Assert.Single(SynergyEngine.Between(Profile(source),Profile(target)),x=>x.Kind==kind);
        Assert.True(link.Weight>0);Assert.True(link.Reviewed);Assert.NotEmpty(link.Condition);
    }
    [Theory]
    [InlineData("Warrior's Sword","Deft Duelist","shroud-equip")]
    [InlineData("Path to Exile","Sephiroth, Planet's Heir","exile-not-death")]
    [InlineData("Suplex","Sephiroth, Planet's Heir","exile-not-death")]
    public void RealConflictsAreExplicitAndNeverPositive(string source,string target,string kind)
    {
        var links=SynergyEngine.Between(Profile(source),Profile(target));Assert.Contains(links,x=>x.Kind==kind&&x.Weight<0);
        Assert.DoesNotContain(links,x=>x.Weight>0);
    }
    [Theory]
    [InlineData("Kaleidostone","Matca Rioters")]
    [InlineData("Path to Exile","Matca Rioters")]
    [InlineData("Seaside Citadel","Matca Rioters")]
    [InlineData("The Crystal's Chosen","Sephiroth, Planet's Heir")]
    [InlineData("Warrior's Sword","Evil Reawakened")]
    [InlineData("Undercity Dire Rat","Gleam of Resistance")]
    [InlineData("Forest","Rhox Meditant")]
    [InlineData("Mountain","Ember Weaver")]
    public void PlausibleWordsAndTokensDoNotInventUnsupportedRelations(string source,string target)
        =>Assert.DoesNotContain(SynergyEngine.Between(Profile(source),Profile(target)),x=>x.Weight>0);
    [Fact]
    public void DomainCountsRealDistinctTypesNotCopiesColorsOrTownLands()
    {
        var one=SynergyEngine.Assess([Line("Forest",5),Line("Kaleidostone"),Line("Seaside Citadel"),Line("Rabanastre, Royal City"),Line("Matca Rioters")]);
        Assert.Equal(["Forest"],one.BasicTypes);Assert.Contains(one.Warnings,w=>w.Contains("1/5"));
        var five=SynergyEngine.Assess([Line("Forest"),Line("Island"),Line("Plains"),Line("Swamp"),Line("Mountain"),Line("Matca Rioters")]);Assert.Equal(5,five.BasicTypes.Count);
        Assert.DoesNotContain(five.Warnings,w=>w.Contains("slechts"));
        Assert.DoesNotContain(SynergyEngine.Between(Profile("Matca Rioters"),Profile("Paleoloth")),x=>x.Kind=="power-five");
    }
    [Fact]
    public void CopyingOrSideboardingCardsCannotInflateRelationships()
    {
        var single=SynergyEngine.Assess([Line("Warrior's Sword"),Line("Cloud, Planet's Champion")]);
        var copies=SynergyEngine.Assess([Line("Warrior's Sword",4),Line("Cloud, Planet's Champion",4),Line("Adelbert Steiner")with{Zone=DeckZone.Sideboard}]);
        Assert.Equal(single.Relations.Select(x=>x.Key),copies.Relations.Select(x=>x.Key));Assert.Equal(4,copies.EquipmentCopies);Assert.Equal(4,copies.JobSelectCopies);
    }
    [Fact]
    public void ChangedOracleTextOrPowerLosesReviewedTrustButPrintingMetadataDoesNot()
    {
        var card=Card("Cloud, Planet's Champion");var changed=SynergyProfiles.Analyze(card with{OracleText="Equip abilities your opponents activate cost less."});
        Assert.True(changed.Changed);Assert.False(changed.Has(SynergyFeature.EquipmentDiscount));Assert.False(changed.Reviewed);
        Assert.True(SynergyProfiles.Analyze(card with{Power="99"}).Changed);
        Assert.True(SynergyProfiles.Analyze(card with{Id=Guid.NewGuid(),Set="new",Number="999",FetchedUtc=DateTime.UtcNow}).Reviewed);
    }
    [Theory]
    [InlineData("Target opponent creates two 1/1 creature tokens.",SynergyFeature.OwnCreatureTokens)]
    [InlineData("Create two 1/1 creature tokens under an opponent's control.",SynergyFeature.OwnCreatureTokens)]
    [InlineData("You can't create creature tokens.",SynergyFeature.OwnCreatureTokens)]
    [InlineData("(Create two 1/1 creature tokens.)",SynergyFeature.OwnCreatureTokens)]
    [InlineData("Target opponent sacrifices a creature.",SynergyFeature.OwnCreatureSacrifice)]
    [InlineData("Sacrifice this creature: Draw a card.",SynergyFeature.OwnCreatureSacrifice)]
    [InlineData("Each opponent draws a card.",SynergyFeature.OwnDraw)]
    [InlineData("Its controller draws a card.",SynergyFeature.OwnDraw)]
    [InlineData("Draw a card instead of creating a token.",SynergyFeature.OwnDraw)]
    [InlineData("Return target creature card from an opponent's graveyard to its owner's hand.",SynergyFeature.CreatureRecursion)]
    [InlineData("Exile all cards from target opponent's graveyard.",SynergyFeature.GraveyardHate)]
    [InlineData("Whenever you draw a card, you lose 1 life.",SynergyFeature.DrawPayoff)]
    public void UnreviewedDirectionNegationReminderAndCostsFailClosed(string text,SynergyFeature falseFeature)
    {
        var card=Card("Kaleidostone")with{Id=Guid.NewGuid(),OracleId=Guid.NewGuid().ToString(),Name="Unseen fixture",TypeLine="Enchantment",OracleText=text};
        var profile=SynergyProfiles.Analyze(card);Assert.False(profile.Reviewed);Assert.False(profile.Has(falseFeature));
    }
    [Fact]
    public void GenericNonTokenSacrificeCannotConsumeAnEquipmentHeroToken()
    {
        var outlet=Card("Kaleidostone")with{Id=Guid.NewGuid(),OracleId=Guid.NewGuid().ToString(),Name="Limited outlet",OracleText="Sacrifice a nontoken creature: Draw a card."};
        var p=SynergyProfiles.Analyze(outlet);Assert.True(p.Has(SynergyFeature.OwnCreatureSacrifice));Assert.True(p.Has(SynergyFeature.NonTokenSacrificeOnly));
        Assert.DoesNotContain(SynergyEngine.Between(Profile("Warrior's Sword"),p),x=>x.Kind=="creature-fodder");
    }
    [Fact]
    public void SharedPayoffsWithoutEnablersWarnInsteadOfPretendingTheySupportEachOther()
    {
        var assessment=SynergyEngine.Assess([Line("Al Bhed Salvagers"),Line("G'raha Tia")]);
        Assert.DoesNotContain(assessment.Positive,x=>x.Theme=="sacrifice");Assert.Contains(assessment.Warnings,w=>w.Contains("zonder herkende eigen"));
        Assert.False(CardThemes.Matches(Card("Gwafa Hazid, Profiteer"),"draw"));Assert.True(CardThemes.Matches(Card("Frontline Sage"),"draw"));
        Assert.False(CardThemes.Matches(Card("Sephiroth, Planet's Heir"),"sacrifice"));
    }
    [Fact]
    public void ProtectionDependsOnSourceColorAndExaltedDoesNotTarget()
    {
        var black=SynergyProfiles.Analyze(Card("Magitek Scythe")with{OracleId=Guid.NewGuid().ToString(),Name="Black Equipment",PrintedColors="B"});
        var protectedCreature=Profile("Valeron Outlander");
        Assert.Contains(SynergyEngine.Between(black,protectedCreature),x=>x.Kind=="protection-equip"&&x.Weight<0);
        Assert.DoesNotContain(SynergyEngine.Between(black,protectedCreature),x=>x.Weight>0);
        Assert.Contains(SynergyEngine.Between(Profile("Warrior's Sword"),protectedCreature),x=>x.Kind=="equipment-bearer");
        Assert.Contains(SynergyEngine.Between(Profile("Aven Squire"),protectedCreature),x=>x.Kind=="solo-attacker");
    }
    [Theory]
    [InlineData("Protection from white and from black.",true,false)]
    [InlineData("As long as you control an artifact, this creature has shroud.",true,false)]
    [InlineData("This creature can't attack alone.",false,true)]
    [InlineData("Flying\nDefender",false,true)]
    public void UnresolvedTargetingAndSoloAttackRestrictionsDoNotProducePositiveClaims(string text,bool noEquip,bool noSolo)
    {
        var card=Card("Suntail Hawk")with{OracleId=Guid.NewGuid().ToString(),Name="Unseen restricted creature",OracleText=text};var profile=SynergyProfiles.Analyze(card);
        if(noEquip)Assert.DoesNotContain(SynergyEngine.Between(Profile("Warrior's Sword"),profile),x=>x.Weight>0);
        if(noSolo)Assert.DoesNotContain(SynergyEngine.Between(Profile("Aven Squire"),profile),x=>x.Kind=="solo-attacker");
    }
    [Fact]
    public async Task OwnedWizardKeepsHardRulesAndReturnsActualRelationshipsWithItsDraft()
    {
        var ct=TestContext.Current.CancellationToken;await using var t=new TestDatabase();await t.InitializeAsync();var store=t.Provider.GetRequiredService<IPaperWorkspace>();await store.InitializeExampleAsync(ct);
        var request=new DeckWizardRequest("WR equipment semantics","casual60","selected","WR","equipment","balanced",false);var draft=await store.PrepareWizardAsync(request,ct);
        Assert.Equal(60,draft.Lines.Sum(x=>x.Quantity));Assert.Empty(draft.Report.Problems);Assert.All(draft.Lines,x=>Assert.True(request.Allows(x.Card)));
        var analysis=Assert.IsType<SynergyAssessment>(draft.Report.Synergy);Assert.Contains(analysis.Positive,x=>x.Kind=="equipped-payoff");Assert.InRange(analysis.EquipmentCopies,4,12);Assert.True(analysis.CreatureCopies>=10);
        Assert.Equal(202,(await store.SummaryAsync(ct)).Copies);Assert.Equal(4,(await store.DecksAsync(ct)).Count);
    }
}
