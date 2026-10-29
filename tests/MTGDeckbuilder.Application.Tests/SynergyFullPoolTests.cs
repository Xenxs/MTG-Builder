using System.Text.Json.Nodes;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application.Tests;

public sealed class SynergyFullPoolTests
{
    private static PaperCard Card(string text,string type="Creature — Human",double mv=2,string colors="W",string name="Independent source")=>
        ScryfallPaperCatalog.Parse(PaperWorkspaceTests.Example()["Cards"]![0]!.ToJsonString(),DateTime.UtcNow)with
        {Id=Guid.NewGuid(),OracleId=Guid.NewGuid().ToString(),Name=name,TypeLine=type,OracleText=text,ManaValue=mv,PrintedColors=colors,Power="2",Toughness="2"};
    [Fact]
    public void FactCachingPreservesCallerPrintingFreshnessAndInvalidatesChangedGameplay()
    {
        var a=Card("Draw a card.");var b=a with{Id=Guid.NewGuid(),Set="new",FetchedUtc=a.FetchedUtc.AddHours(1),ImageFront="https://example.invalid/new"};
        var first=SynergyProfiles.Analyze(a);var second=SynergyProfiles.Analyze(b);Assert.Equal(b,second.Card);Assert.Equal(first.Features,second.Features);
        Assert.True(first.Has(OwnDraw));Assert.False(SynergyProfiles.Analyze(a with{OracleText="Target opponent draws a card."}).Has(OwnDraw));
        Assert.Empty(SynergyProfiles.Analyze(a with{RequiresUnsupportedRules=true}).Features);
    }
    [Theory]
    [InlineData("Scry 2, then draw two cards.",true)]
    [InlineData("Scry 2, then target opponent draws two cards.",false)]
    [InlineData("Target player scries 2, then draws two cards.",false)]
    [InlineData("If you would scry 2, draw two cards instead.",false)]
    public void SequentialOwnSelectionAndDrawKeepTheirBeneficiary(string text,bool own)=>Assert.Equal(own,SynergyProfiles.Analyze(Card(text)).Has(OwnDraw));
    [Theory]
    [InlineData("Cycling {2}",true)]
    [InlineData("Plainscycling {2}",false)]
    [InlineData("Basic landcycling {2}",false)]
    public void OrdinaryCyclingDrawsAndTypecyclingDoesNot(string text,bool draw)=>Assert.Equal(draw,SynergyProfiles.Analyze(Card(text)).Has(OwnDraw));
    [Theory]
    [InlineData("Destroy target creature or planeswalker.",true)]
    [InlineData("Destroy target artifact and create a creature token.",false)]
    [InlineData("Destroy target creature you control.",false)]
    [InlineData("This card can't destroy target creature.",false)]
    public void DirectCreatureDestructionDoesNotStealOtherEffectsSubjects(string text,bool removal)=>Assert.Equal(removal,SynergyProfiles.Analyze(Card(text)).Has(DestroyRemoval));
    [Theory]
    [InlineData("Independent source deals 2 damage to any target.",true)]
    [InlineData("Independent source deals 2 damage to target player and creates a creature token.",false)]
    [InlineData("Independent source deals 2 damage to target creature you control.",false)]
    [InlineData("Target creature gets -2/-2 until end of turn.",true)]
    public void DamageAndNegativeToughnessRequireAnActualPossibleEnemyCreature(string text,bool removal)=>Assert.Equal(removal,SynergyProfiles.Analyze(Card(text)).Has(ConditionalToughnessRemoval));
    [Theory]
    [InlineData("When this creature enters, you skip your next two turns.")]
    [InlineData("When this creature enters, exile all cards from your library.")]
    [InlineData("When this creature enters, if you haven't completed a dungeon, return it to its owner's hand.")]
    public void UnmodeledOwnTurnLibraryAndDungeonConditionsAreNotChosenAutomatically(string text)=>Assert.NotNull(SynergyProfiles.AutomaticSelectionIssue(Card(text)));
    [Fact]
    public void SelfDeathPayoffDoesNotBecomeAPayoffForEveryOtherCreature()
    {
        var self=SynergyProfiles.Analyze(Card("When this creature dies, create a Treasure token."));Assert.True(self.Has(SelfCreatureDeathPayoff));Assert.False(self.Has(OwnCreatureDeathPayoff));
        var outlet=SynergyProfiles.Analyze(Card("Sacrifice a creature: Draw a card.","Artifact"));Assert.Contains(SynergyEngine.Between(outlet,self),r=>r.Kind=="self-death-sacrifice"&&r.Condition.Contains("juist deze"));
        Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Card("Sacrifice this creature: Draw a card.")),self),r=>r.Weight>1);
    }
    [Fact]
    public void TypedRecursionRejectsHighManaWrongTypesAndTokenSubstitutes()
    {
        var cheap=SynergyProfiles.Analyze(Card("Return target creature card with mana value 3 or less from your graveyard to the battlefield.","Sorcery"));
        Assert.True(cheap.Has(CreatureRecursion));Assert.Single(SynergyEngine.Between(SynergyProfiles.Analyze(Card("",mv:3)),cheap),r=>r.Kind=="creature-recursion");
        Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Card("",mv:6)),cheap),r=>r.Kind=="creature-recursion");
        var find=SynergyProfiles.Analyze(Card("Return target artifact or enchantment card from your graveyard to your hand.","Instant"));
        Assert.True(find.Has(TypedCardRecursion));Assert.False(find.Has(CreatureRecursion));
        Assert.Contains(SynergyEngine.Between(SynergyProfiles.Analyze(Card("","Artifact")),find),r=>r.Kind=="typed-recursion");
        Assert.Contains(SynergyEngine.Between(SynergyProfiles.Analyze(Card("","Enchantment")),find),r=>r.Kind=="typed-recursion");
        Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Card("Create a creature token.","Sorcery")),find),r=>r.Kind.Contains("recursion"));
        Assert.False(SynergyProfiles.Analyze(Card("Return target creature card from your graveyard to the battlefield under an opponent's control.","Sorcery")).Has(TypedCardRecursion));
    }
    [Fact]
    public void DelayedSacrificeIsNotSacrificingTheSourceOrAFreeOutlet()
    {
        var p=SynergyProfiles.Analyze(Card("When this creature enters, if it was kicked, return target creature card with mana value 3 or less from your graveyard to the battlefield. It gains haste. Sacrifice it at the beginning of the next end step."));
        Assert.True(p.Has(DelayedCreatureSacrifice));Assert.False(p.Has(SelfCreatureSacrifice));Assert.False(p.Has(OwnCreatureSacrifice));Assert.Contains("next end step",p.Notes);
    }
    [Fact]
    public void AnyCreatureDeathDiffersFromOpponentOnlyAndNonTokenIsEnforced()
    {
        var blood=SynergyProfiles.Analyze(Card("Whenever this creature or another creature dies, target player loses 1 life and you gain 1 life."));
        Assert.True(blood.Has(OwnCreatureDeathPayoff));Assert.True(blood.Has(OpponentDeathPayoff));
        var report=SynergyEngine.Assess([new(blood.Card,DeckZone.Mainboard,1,1)]);Assert.DoesNotContain(report.Warnings,w=>w.Contains("uitsluitend"));
        var non=SynergyProfiles.Analyze(Card("Whenever another nontoken creature you control dies, draw a card."));Assert.True(non.Has(NonTokenDeathOnly));
        var tokens=SynergyProfiles.Analyze(Card("Create two 1/1 creature tokens.","Sorcery"));Assert.DoesNotContain(SynergyEngine.Between(tokens,non),r=>r.Kind=="tokens-death");
    }
    [Fact]
    public void EnterPayoffsSeparateTokenTypesCastAndNontokenRestrictions()
    {
        var payoff=SynergyProfiles.Analyze(Card("Whenever another creature you control enters, Independent source deals 2 damage to each opponent.","Enchantment"));Assert.True(payoff.Has(OwnCreatureEnterPayoff));
        var creatureToken=SynergyProfiles.Analyze(Card("Create a 1/1 creature token.","Sorcery"));var food=SynergyProfiles.Analyze(Card("Create a Food token.","Instant"));
        Assert.Contains(SynergyEngine.Between(creatureToken,payoff),r=>r.Kind=="token-enter");Assert.DoesNotContain(SynergyEngine.Between(food,payoff),r=>r.Kind=="token-enter");
        var non=SynergyProfiles.Analyze(Card("Whenever a nontoken creature you control enters, draw a card.","Enchantment"));Assert.True(non.Has(NonTokenEnterOnly));Assert.DoesNotContain(SynergyEngine.Between(creatureToken,non),r=>r.Kind=="token-enter");
        Assert.Contains(SynergyEngine.Between(SynergyProfiles.Analyze(Card("")),non),r=>r.Kind=="permanent-enter");
        Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Card("Draw a card.","Instant")),payoff),r=>r.Kind=="permanent-enter");
    }
    [Theory]
    [InlineData("Whenever a creature an opponent controls enters, draw a card.")]
    [InlineData("Whenever a creature you control enters, target opponent draws a card.")]
    [InlineData("Whenever a creature with power 4 or greater enters under your control, draw a card.")]
    [InlineData("Whenever two or more creatures you control enter, draw a card.")]
    public void ForeignBenefitOrUnmodeledEnterRestrictionIsNotAnUnrestrictedOwnPayoff(string text)=>Assert.False(SynergyProfiles.Analyze(Card(text)).Has(OwnCreatureEnterPayoff));
    [Fact]
    public void ASecondaryArtifactConditionDoesNotChangeTheTriggerSubject()
    {
        var enter=SynergyProfiles.Analyze(Card("Whenever a creature you control enters, if you control an artifact, draw a card."));Assert.True(enter.Has(OwnCreatureEnterPayoff));Assert.False(enter.Has(OwnArtifactEnterPayoff));
        var death=SynergyProfiles.Analyze(Card("Whenever a creature you control dies, if you control an artifact, draw a card."));Assert.True(death.Has(OwnCreatureDeathPayoff));Assert.False(death.Has(OwnArtifactDeathPayoff));
    }
    [Fact]
    public void TargetedArtifactCreatureSacrificeRequiresBothTypesAndLegalTarget()
    {
        var outlet=SynergyProfiles.Analyze(Card("{T}: Target artifact creature's controller sacrifices it. That player may search their library for a noncreature artifact card, put it onto the battlefield, then shuffle."));Assert.True(outlet.Has(ArtifactCreatureSacrifice));Assert.False(outlet.Has(OwnCreatureSacrifice));
        Assert.Contains(SynergyEngine.Between(SynergyProfiles.Analyze(Card("","Artifact Creature — Golem")),outlet),r=>r.Kind=="artifact-creature-fodder");
        foreach(var type in new[]{"Creature — Human","Artifact"})Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Card("",type)),outlet),r=>r.Kind=="artifact-creature-fodder");
        Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Card("Shroud","Artifact Creature — Golem")),outlet),r=>r.Kind=="artifact-creature-fodder");
    }
    [Fact]
    public void SymmetricSacrificeIsNotAnAtWillOwnCreatureOutlet()
    {var p=SynergyProfiles.Analyze(Card("At the beginning of your upkeep, each player sacrifices a non-Vampire creature of their choice."));Assert.True(p.Has(SymmetricCreatureSacrifice));Assert.True(p.Has(OpponentCreatureSacrifice));Assert.False(p.Has(OwnCreatureSacrifice));Assert.Contains("non-Vampire",p.Notes);}
    [Fact]
    public void UnsupportedPrintingRulesAreBlockedWithoutBlockingOrdinaryEternalLegalUnfinity()
    {
        var raw=PaperWorkspaceTests.Example()["Cards"]![0]!.DeepClone().AsObject();raw["border_color"]="silver";Assert.True(ScryfallPaperCatalog.Parse(raw.ToJsonString(),DateTime.UtcNow).RequiresUnsupportedRules);
        raw["border_color"]="black";raw["security_stamp"]="acorn";Assert.True(ScryfallPaperCatalog.Parse(raw.ToJsonString(),DateTime.UtcNow).RequiresUnsupportedRules);
        raw["security_stamp"]="oval";raw["set_type"]="funny";Assert.False(ScryfallPaperCatalog.Parse(raw.ToJsonString(),DateTime.UtcNow).RequiresUnsupportedRules);
        raw["layout"]="leveler";var leveler=ScryfallPaperCatalog.Parse(raw.ToJsonString(),DateTime.UtcNow);Assert.True(leveler.RequiresUnsupportedRules);Assert.Empty(SynergyProfiles.Analyze(leveler).Features);
    }
    [Fact]
    public void NumericTraitCacheAndSearchBoundsMatchRealDirectedRelations()
    {
        var cards=new List<PaperCard>{Card(""),Card("",mv:7),Card("Flying"),Card("Shroud"),Card("Exalted"),Card("Protection from black."),Card("","Artifact Creature — Gnome"),Card("","Artifact"),Card("","Enchantment"),Card("Draw a card.","Instant"),Card("Create a Food token.","Sorcery"),Card("Create a 1/1 creature token.","Sorcery"),Card("Creatures you control get +1/+1.","Enchantment"),Card("Sacrifice a creature: Draw a card.","Artifact"),Card("Whenever you draw a card, each opponent loses 1 life."),Card("Return target creature card with mana value 3 or less from your graveyard to your hand.","Instant"),Card("Return target artifact or enchantment card from your graveyard to your hand.","Instant"),Card("Whenever a creature you control enters, draw a card."),Card("Whenever another creature dies, target player loses 1 life."),Card("Search your graveyard for a card named Wanted, reveal it, then put it into your hand.","Sorcery"),Card("",name:"Wanted"),Card("","Artifact — Equipment",colors:"B"),Card("Domain — Draw a card for each basic land type among lands you control.","Sorcery")};
        // Different physical objects with identical gameplay traits must not be suppressed as self-pairs.
        cards.Add(cards[4]with{Id=Guid.NewGuid(),OracleId=Guid.NewGuid().ToString(),Name="Second exalted creature"});
        var p=cards.Select(SynergyProfiles.Analyze).ToArray();var named=p.Where(x=>x.Has(NamedGraveyardRetrieval)).SelectMany(x=>SynergyTextRules.NamedTargets(x.Card)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var theme in CardThemes.Choices.Where(t=>t.Key!="any").Select(t=>t.Key).Append("any"))
        {
            var index=new SynergyInteractionIndex(p,_=>true,theme);
            foreach(var a in p)foreach(var b in p.Where(x=>x.Card.OracleId!=a.Card.OracleId))
            {
                var actual=SynergyEngine.Between(a,b).Concat(SynergyEngine.Between(b,a)).ToArray();
                Assert.Equal(actual.Select(x=>(x.Theme,x.Weight)),index.Pair(index.Group(a.Card.OracleId),index.Group(b.Card.OracleId)).Select(x=>(x.Theme,x.Weight)));
                foreach(var edge in actual.Where(x=>x.Weight>0&&(theme=="any"||x.Theme==theme)))Assert.True(edge.Weight<=SynergyInteractionIndex.UpperBound(a,theme,named.Contains(a.Card.Name)),$"Bound too small: {a.Card.Name}/{theme}/{edge.Kind}");
            }
            foreach(var a in p){var expected=p.Where(x=>x.Card.OracleId!=a.Card.OracleId).SelectMany(b=>SynergyEngine.Between(a,b).Concat(SynergyEngine.Between(b,a))).Where(x=>x.Weight>0&&(theme=="any"||x.Theme==theme)).Select(x=>x.Weight).DefaultIfEmpty().Max();Assert.Equal(expected,index.Best(a.Card.OracleId));}
        }
    }
}
