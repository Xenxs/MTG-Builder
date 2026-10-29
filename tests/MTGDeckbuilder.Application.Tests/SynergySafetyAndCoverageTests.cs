using System.Text.Json;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application.Tests;

public sealed class SynergySafetyAndCoverageTests
{
    private static PaperCard Fixture(string text,string type="Enchantment")=>ScryfallPaperCatalog.Parse(PaperWorkspaceTests.Example()["Cards"]![0]!.ToJsonString(),DateTime.UtcNow)with
        {Id=Guid.NewGuid(),OracleId=Guid.NewGuid().ToString(),Name="Independent fixture",TypeLine=type,OracleText=text};
    private static PaperCard[] PreviousSet()
    {using var raw=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"synergy-holdout-cards-v2.json")));return raw.RootElement.GetProperty("Cards").EnumerateArray().Select(c=>ScryfallPaperCatalog.Parse(c.GetRawText(),DateTime.UtcNow)).ToArray();}
    [Fact]
    public void AdventureSplitAndMdfcEffectsAreNotMergedOrAutomaticallySelected()
    {
        foreach(var card in PreviousSet().Where(SynergyProfiles.ComplexCard))
        {
            var p=SynergyProfiles.Analyze(card);Assert.Empty(p.Features);Assert.False(p.Reviewed);Assert.Contains("meerdere",p.Notes,StringComparison.OrdinalIgnoreCase);
            Assert.Empty(SynergyEngine.Between(p,SynergyProfiles.Analyze(Fixture("Creatures you control get +1/+1."))));
            Assert.Empty(SynergyEngine.LandTypes(card with{TypeLine="Land — Forest // Land — Island"}));
            Assert.Contains(SynergyEngine.Assess([new(card,DeckZone.Mainboard,1,1)]).Warnings,w=>w.Contains("meerdere kanten"));
            Assert.Throws<ArgumentException>(()=>PhysicalDeckPlanner.Create([card],[],[card],true,new("No merged modes","casual60","any","","tokens","balanced",true)));
        }
    }
    [Fact]
    public void LootAcrossSentencesKeepsOncePerTurnAndDoesNotBecomeArtifactCastTrigger()
    {
        var card=PreviousSet().Single(c=>c.Name=="Academy Wall");var p=SynergyProfiles.Analyze(card);
        Assert.True(p.Has(OwnDraw));Assert.True(p.Has(Loot));Assert.True(p.Has(SelfDiscard));Assert.True(p.Has(InstantSorceryCastPayoff));Assert.False(p.Has(NoncreatureCastPayoff));
        Assert.Contains("once each turn",p.Notes);
        Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Fixture("","Artifact")),p),r=>r.Kind=="specified-spell-cast");
        Assert.Contains(SynergyEngine.Between(SynergyProfiles.Analyze(Fixture("","Instant")),p),r=>r.Kind=="specified-spell-cast");
    }
    [Fact]
    public void EnchantmentAndUnknownTokensNeverBecomeCreaturesOrArtifactMana()
    {
        var curse=SynergyProfiles.Analyze(PreviousSet().Single(c=>c.Name=="Asinine Antics"));
        Assert.True(curse.Has(OwnEnchantmentTokens));Assert.False(curse.Has(OwnCreatureTokens));Assert.False(curse.Has(OwnArtifactTokens));Assert.True(curse.Theme("tokens"));
        var buff=SynergyProfiles.Analyze(Fixture("Creatures you control get +1/+1."));Assert.DoesNotContain(SynergyEngine.Between(curse,buff),r=>r.Kind=="tokens-buff");
        var unknown=SynergyProfiles.Analyze(Fixture("Create a Strange Example token."));Assert.True(unknown.Has(OwnUnknownTokens));Assert.False(unknown.Has(OwnCreatureTokens));Assert.False(unknown.Has(OwnArtifactTokens));
    }
    [Fact]
    public void TokenCreationCanHelpAffinityButDoesNotCastAnArtifactSpell()
    {
        var token=SynergyProfiles.Analyze(Fixture("Create a Treasure token."));var affinity=SynergyProfiles.Analyze(Fixture("Affinity for artifacts","Instant"));
        var casts=SynergyProfiles.Analyze(Fixture("Whenever you cast an artifact spell, draw a card."));
        Assert.Contains(SynergyEngine.Between(token,affinity),r=>r.Kind=="artifact-affinity");Assert.DoesNotContain(SynergyEngine.Between(token,casts),r=>r.Kind=="specified-spell-cast");
    }
    [Fact]
    public void ConvokeUsesUntappedCreatureTokensAndDoesNotAssumeFreeColoredMana()
    {
        var source=SynergyProfiles.Analyze(Fixture("Create two 1/1 colorless Hero creature tokens."));var convoke=SynergyProfiles.Analyze(Fixture("Convoke","Creature — Soldier"));
        var link=Assert.Single(SynergyEngine.Between(source,convoke),r=>r.Kind=="tokens-convoke");Assert.Contains("untapped",link.Condition);Assert.Contains("gekleurde",link.Condition);
        var artifactTokens=SynergyProfiles.Analyze(Fixture("Create a Treasure token."));Assert.DoesNotContain(SynergyEngine.Between(artifactTokens,convoke),r=>r.Kind=="tokens-convoke");
    }
    [Fact]
    public void EquippedDeathIsNeitherGenericDeathNorGraveyardRecursion()
    {
        var p=SynergyProfiles.Analyze(PreviousSet().Single(c=>c.Name=="Deathrender"));Assert.True(p.Has(EquippedDeathPayoff));Assert.True(p.Has(CreatureFromHand));Assert.False(p.Has(CreatureRecursion));
        var artifact=SynergyProfiles.Analyze(Fixture("{T}, Sacrifice this artifact: Draw a card.","Artifact"));Assert.DoesNotContain(SynergyEngine.Between(artifact,p),r=>r.Kind=="own-sacrifice-death");
        var creature=SynergyProfiles.Analyze(Fixture("Sacrifice a creature: Draw a card."));var link=Assert.Single(SynergyEngine.Between(creature,p),r=>r.Kind=="own-sacrifice-death");Assert.Contains("uitgeruste",link.Condition);
    }
    [Fact]
    public void OpponentWordsElsewhereDoNotHideExplicitOwnDomainButDoNotCreateOwnRamp()
    {
        var exploding=SynergyProfiles.Analyze(PreviousSet().Single(c=>c.Name=="Exploding Borders"));Assert.True(exploding.Has(DomainPayoff));Assert.True(exploding.Has(BasicLandSearch));
        var wrong=SynergyProfiles.Analyze(Fixture("Exile target creature. Its controller may search their library for a basic land card."));Assert.False(wrong.Has(BasicLandSearch));
        var panorama=SynergyProfiles.Analyze(PreviousSet().Single(c=>c.Name=="Bant Panorama"));Assert.True(panorama.Has(RestrictedBasicLandSearch));Assert.False(panorama.Has(BasicLandSearch));
    }
    [Fact]
    public void AbilityWordDoesNotHideRealTriggerOrRemoveItsToughnessRestriction()
    {
        var zar=SynergyProfiles.Analyze(PreviousSet().Single(c=>c.Name=="Zar Ojanen, Scion of Efrava"));
        Assert.True(zar.Has(DomainPayoff));Assert.True(zar.Has(TeamBuff));Assert.Contains("toughness less",zar.Notes);Assert.Contains("becomes tapped",zar.Notes);
    }
    [Theory]
    [InlineData("That creature's controller may draw a card and create a 1/1 creature token.")]
    [InlineData("Create a 1/1 creature token under its controller's control.")]
    public void AmbiguousControllersDoNotBecomeGuaranteedOwnTokenSources(string text)
    {var p=SynergyProfiles.Analyze(Fixture(text));Assert.False(p.Has(OwnCreatureTokens));Assert.False(p.Has(OwnUnknownTokens));}
    [Theory]
    [InlineData("You can't win the game and your opponents can't lose the game.")]
    [InlineData("At the beginning of your upkeep, if there are no oil counters on this creature, you lose the game.")]
    [InlineData("You cannot win the game.")]
    public void ExplicitOwnLossAndWinLocksAreNotAutomaticallyChosen(string text)
    {
        var risk=Fixture(text,"Legendary Creature — Demon");Assert.NotNull(SynergyProfiles.AutomaticSelectionIssue(risk));
        Assert.Contains("uitgesloten",SynergyProfiles.Analyze(risk).Details);
        Assert.Contains(SynergyEngine.Assess([new(risk,DeckZone.Mainboard,1,1)]).Warnings,w=>w.Contains("verliesvoorwaarde"));
        Assert.Throws<ArgumentException>(()=>PhysicalDeckPlanner.Create([risk],[new(risk,4)],[risk],false,new("Risk","casual60","any","","any","balanced",false)));
        Assert.Throws<ArgumentException>(()=>CommanderDeckPlanner.Create(risk,[risk],[new(risk,1)],true,new DateTime(2026,10,28,0,0,0,DateTimeKind.Utc)));
        Assert.Null(SynergyProfiles.AutomaticSelectionIssue(Fixture("Target opponent loses the game.")));
    }
    [Fact]
    public void LaterAbilityRestrictionsRemainInTheActualRelationshipExplanation()
    {
        var card=Fixture("Draw a card.\nCreate a Food token.\nSurveil 1.\nMill two cards.\nReturn target creature card from your graveyard to your hand.\nWhenever you cast an instant or sorcery spell, draw a card. This ability triggers only once each turn.");
        var relation=Assert.Single(SynergyEngine.Between(SynergyProfiles.Analyze(Fixture("","Instant")),SynergyProfiles.Analyze(card)),r=>r.Kind=="specified-spell-cast");
        Assert.Contains("only once each turn",relation.Condition);Assert.Contains("Whenever you cast",relation.Condition);
    }
}
