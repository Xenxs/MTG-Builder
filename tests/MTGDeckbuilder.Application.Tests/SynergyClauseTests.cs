using System.Text.Json;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application.Tests;

public sealed class SynergyClauseTests
{
    private static PaperCard Fixture(string text,string type="Enchantment",string name="Neutral fixture")=>
        ScryfallPaperCatalog.Parse(PaperWorkspaceTests.Example()["Cards"]![0]!.ToJsonString(),DateTime.UtcNow)with
        {Id=Guid.NewGuid(),OracleId=Guid.NewGuid().ToString(),Name=name,TypeLine=type,OracleText=text};
    [Theory]
    [InlineData("Whenever an opponent searches their library, you gain 1 life and draw a card.",OwnDraw)]
    [InlineData("{4}, {T}, Sacrifice this land: Draw a card.",OwnDraw)]
    [InlineData("Whenever you cast an artifact spell, you may draw a card.",ArtifactCastPayoff)]
    [InlineData("Whenever a player casts an artifact, instant, or sorcery spell, you create a Mutagen token.",OwnArtifactTokens)]
    [InlineData("Whenever you cast a spell from exile, create a 1/1 white Ally creature token.",CastFromExilePayoff)]
    [InlineData("Creatures you control get +0/+1.",TeamBuff)]
    [InlineData("Creatures you control gain afterlife 1 until end of turn. Draw a card.",OwnCreatureTokens)]
    [InlineData("At the beginning of your end step, if you gained 3 or more life this turn, create a 3/3 white Angel creature token with flying.",RequiresLifeGainThree)]
    public void ConditionsAndCostsDoNotChangeWhoReceivesTheEffect(string text,SynergyFeature feature)
    {var p=SynergyProfiles.Analyze(Fixture(text));Assert.True(p.Has(feature),p.Notes);Assert.False(p.Reviewed);Assert.Contains("Bronfragment",p.Notes);}
    [Theory]
    [InlineData("Whenever you draw a card, target opponent creates a 1/1 creature token.",OwnCreatureTokens)]
    [InlineData("Whenever you draw a card, you lose 1 life.",DrawPayoff)]
    [InlineData("Whenever an opponent casts an artifact spell, draw a card.",ArtifactCastPayoff)]
    [InlineData("Whenever a creature attacks, defending player sacrifices a creature.",OpponentCreatureSacrifice)]
    [InlineData("Create a 1/1 creature token under target player's control.",OwnCreatureTokens)]
    [InlineData("Creatures you control get -1/-1.",TeamBuff)]
    [InlineData("Creatures you control get +0/+0.",TeamBuff)]
    [InlineData("Creatures your opponents control gain afterlife 1.",OwnCreatureTokens)]
    [InlineData("You may search your library for a card named Example.",NamedGraveyardRetrieval)]
    [InlineData("Whenever an opponent searches your graveyard for a card named Example, draw a card.",NamedGraveyardRetrieval)]
    public void OpponentScopeNegativeEffectsAndUnsupportedActorsFailClosed(string text,SynergyFeature notFeature)
        =>Assert.False(SynergyProfiles.Analyze(Fixture(text)).Has(notFeature));
    [Fact]
    public void TokenCreatureArtifactAndDeathKindsStaySeparate()
    {
        var creaturePayoff=SynergyProfiles.Analyze(Fixture("Whenever a creature you control dies, draw a card."));
        var artifactPayoff=SynergyProfiles.Analyze(Fixture("Whenever an artifact you control dies, draw a card."));
        var sacrificeArtifact=SynergyProfiles.Analyze(Fixture("{T}, Sacrifice this artifact: Draw a card.","Artifact"));
        Assert.True(creaturePayoff.Has(OwnCreatureDeathPayoff));Assert.False(creaturePayoff.Has(OwnArtifactDeathPayoff));
        Assert.DoesNotContain(SynergyEngine.Between(sacrificeArtifact,creaturePayoff),x=>x.Kind=="own-sacrifice-death");
        Assert.Contains(SynergyEngine.Between(sacrificeArtifact,artifactPayoff),x=>x.Kind=="own-sacrifice-death");
        var mutagen=SynergyProfiles.Analyze(Fixture("Create a Mutagen token."));var buff=SynergyProfiles.Analyze(Fixture("Creatures you control get +0/+1."));
        Assert.True(mutagen.Has(OwnArtifactTokens));Assert.False(mutagen.Has(OwnCreatureTokens));Assert.DoesNotContain(SynergyEngine.Between(mutagen,buff),x=>x.Kind=="tokens-buff");
    }
    [Fact]
    public void SpecifiedSpellTypesDoNotBecomeAnyNoncreatureSpell()
    {
        var payoff=SynergyProfiles.Analyze(Fixture("Whenever a player casts an artifact, instant, or sorcery spell, you create a Mutagen token."));
        Assert.True(payoff.Has(ArtifactInstantSorceryCastPayoff));Assert.False(payoff.Has(NoncreatureCastPayoff));
        var artifact=SynergyProfiles.Analyze(Fixture("","Artifact"));var enchantment=SynergyProfiles.Analyze(Fixture("","Enchantment"));
        Assert.Contains(SynergyEngine.Between(artifact,payoff),x=>x.Kind=="specified-spell-cast");Assert.DoesNotContain(SynergyEngine.Between(enchantment,payoff),x=>x.Kind=="specified-spell-cast");
    }
    [Fact]
    public void ShortLegendaryNamesAndSequentialActionsRetainAttackerAndTokenConditions()
    {
        var p=SynergyProfiles.Analyze(Fixture("Whenever you attack with one or more non-Gnome creatures, put a +1/+1 counter on Example, then create X 1/1 colorless Gnome artifact creature tokens that are tapped and attacking.","Legendary Creature — Human","Example, Counter Maker"));
        Assert.True(p.Has(OwnCreatureTokens));Assert.True(p.Has(OwnArtifactTokens));Assert.True(p.Has(AttackCreaturePayoff));Assert.True(p.Has(NeedsNonGnomeAttacker));Assert.True(p.Has(TokensEnterAttacking));
        var exalted=Fixture("Exalted","Creature — Soldier");var report=SynergyEngine.Assess([new(exalted,DeckZone.Mainboard,1,1),new(p.Card,DeckZone.Mainboard,1,1)]);
        Assert.Contains(report.Warnings,x=>x.Contains("niet als aanvaller verklaard"));
    }
    [Fact]
    public void FirstExternalSetIsNowExplicitDevelopmentRegressionNotUnseenBenchmark()
    {
        using var raw=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"synergy-holdout-cards-v1.json")));
        using var truth=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"synergy-holdout-truth-v1.json")));
        var cards=raw.RootElement.GetProperty("Cards").EnumerateArray().Select(c=>ScryfallPaperCatalog.Parse(c.GetRawText(),DateTime.UtcNow)).ToDictionary(c=>c.OracleId);
        string[] families=["equipment","tokens","graveyard","sacrifice","domain","exalted","draw"];
        foreach(var row in truth.RootElement.EnumerateArray())
        {
            var card=cards[row.GetProperty("OracleId").GetString()!];var expected=row.GetProperty("ExpectedThemes").EnumerateArray().Select(x=>x.GetString()).Order().ToArray();
            Assert.Equal(expected,families.Where(t=>CardThemes.Matches(card,t)).Order().ToArray());Assert.False(SynergyProfiles.Analyze(card).Reviewed);
        }
    }
    [Fact]
    public void NamedRetrievalCannotBeUsedAsGenericCreatureRecursion()
    {
        var helper=Fixture("When this creature enters, you may search your library and/or graveyard for a card named Specific, Named Target, reveal it, and put it into your hand.","Creature — Wizard");
        var target=Fixture("","Legendary Planeswalker","Specific, Named Target");var wrong=Fixture("","Creature — Soldier","Wrong target");var p=SynergyProfiles.Analyze(helper);
        Assert.True(p.Has(NamedGraveyardRetrieval));Assert.False(p.Has(CreatureRecursion));
        Assert.Contains(SynergyEngine.Between(SynergyProfiles.Analyze(target),p),x=>x.Kind=="named-graveyard-target");Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(wrong),p),x=>x.Weight>0);
        Assert.Contains(SynergyEngine.Assess([new(helper,DeckZone.Mainboard,1,1),new(wrong,DeckZone.Mainboard,1,1)]).Warnings,x=>x.Contains("Specific, Named Target"));
    }
    [Fact]
    public void SelfCyclingUsesTheActualCardTypeAndNeverAnyArbitraryGraveyardCard()
    {
        PaperCard[] known=LoadProducts();PaperCard Card(string n)=>known.First(c=>c.Name==n);
        var moogle=SynergyProfiles.Analyze(Card("Cloudbound Moogle"));var gleam=SynergyProfiles.Analyze(Card("Gleam of Resistance"));
        var reanimate=SynergyProfiles.Analyze(Card("Evil Reawakened"));var xande=SynergyProfiles.Analyze(Card("Xande, Dark Mage"));
        Assert.Contains(SynergyEngine.Between(moogle,reanimate),x=>x.Kind=="cycle-recursion");Assert.DoesNotContain(SynergyEngine.Between(moogle,xande),x=>x.Kind=="cycle-spell-card");
        Assert.Contains(SynergyEngine.Between(gleam,xande),x=>x.Kind=="cycle-spell-card");Assert.DoesNotContain(SynergyEngine.Between(gleam,reanimate),x=>x.Kind=="cycle-recursion");
    }
    private static PaperCard[] LoadProducts()
    {
        var cards=new List<PaperCard>();foreach(var name in new[]{"conflux-paper-v1","final-fantasy-starter-v1"})
        {using var stream=typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources."+name+".json")!;using var d=JsonDocument.Parse(stream);cards.AddRange(d.RootElement.GetProperty("Cards").EnumerateArray().Select(x=>ScryfallPaperCatalog.Parse(x.GetRawText(),DateTime.UtcNow)));}return cards.ToArray();
    }
}
