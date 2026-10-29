using System.Text.Json.Nodes;
using MTGDeckbuilder.Infrastructure;
using Xunit;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CommanderBuildPlanTests
{
    private static PaperCard Card(string name,string type,string text)=>ScryfallPaperCatalog.Parse(PaperWorkspaceTests.Example()["Cards"]![0]!.ToJsonString(),DateTime.UtcNow)with{Id=Guid.NewGuid(),OracleId=Guid.NewGuid().ToString(),Name=name,TypeLine=type,OracleText=text,PrintedColors="W",ManaValue=2};
    [Fact]
    public void AngelCastDependencyUsesActualCreatureTypeNotCardNameOrUncastTokens()
    {
        var leader=Card("Test leader","Legendary Creature — Angel","Each other Angel you control enters with an additional +1/+1 counter. {T}: Add {W}. Spend this mana only to cast an Angel spell.");
        var plan=new CommanderBuildPlan(SynergyProfiles.Analyze(leader));Assert.Equal(["Angel"],plan.CreatureTypes);Assert.True(plan.CastTypeRequired);
        var angel=Card("Ordinary card","Creature — Angel","Flying");var human=Card("Angelic pretend angel","Creature — Human","Flying");
        Assert.True(plan.Matches(SynergyProfiles.Analyze(angel)));Assert.False(plan.Matches(SynergyProfiles.Analyze(human)));Assert.False(plan.Matches(SynergyProfiles.Analyze(Card("Producer","Sorcery","Create a 4/4 white Angel creature token."))));
        var selection=new SynergySelection([leader,angel,human],62,commander:leader);Assert.True(selection.Score(angel)>selection.Score(human));
    }
    [Fact]
    public void GoblinCountSupportsGoblinCardsAndOwnGoblinTokensButNotOtherTokens()
    {
        var leader=Card("Test chief","Legendary Creature — Goblin Warrior","{T}: Create X 1/1 red Goblin creature tokens, where X is the number of Goblins you control.");
        var plan=new CommanderBuildPlan(SynergyProfiles.Analyze(leader));Assert.Equal(["Goblin"],plan.CreatureTypes);Assert.False(plan.CastTypeRequired);
        Assert.True(plan.Matches(SynergyProfiles.Analyze(Card("Ordinary body","Creature — Goblin Rogue",""))));
        Assert.True(plan.Matches(SynergyProfiles.Analyze(Card("Own source","Sorcery","Create two 1/1 red Goblin creature tokens."))));
        Assert.False(plan.Matches(SynergyProfiles.Analyze(Card("Other source","Sorcery","Create a 1/1 white Soldier creature token."))));
        Assert.False(plan.Matches(SynergyProfiles.Analyze(Card("Foreign source","Sorcery","Target opponent creates a 1/1 red Goblin creature token."))));
    }
    [Fact]
    public void ArtifactSubtypeEquipmentIsNotTreatedAsCreatureTribe()
    {
        var plan=new CommanderBuildPlan(SynergyProfiles.Analyze(Card("Equipment chief","Legendary Creature — Human","Equipment spells you cast cost {1} less to cast.")));
        Assert.Empty(plan.CreatureTypes);Assert.False(plan.HasFocus);
        Assert.DoesNotContain("Equipment",CommanderBuildPlan.PrintedCreatureTypes(Card("Mixed body","Artifact Creature — Equipment Boar","")));
    }
    [Fact]
    public void SpellCommanderRequiresRealInstantSorceryCardsRatherThanTokensOrCreatures()
    {
        var leader=Card("Spell chief","Legendary Creature — Merfolk Wizard","Whenever you cast an instant or sorcery spell, create a 2/2 blue Drake creature token.");var plan=new CommanderBuildPlan(SynergyProfiles.Analyze(leader));Assert.Equal("instant-sorcery",plan.CastFocus);
        Assert.True(plan.Matches(SynergyProfiles.Analyze(Card("Spell","Instant","Draw a card."))));Assert.False(plan.Matches(SynergyProfiles.Analyze(Card("Creature","Creature — Human","Draw a card."))));
    }
    [Fact]
    public void AForeignCastTriggerDoesNotCreateAnOwnTribalBuildPlan()
    {Assert.Empty(new CommanderBuildPlan(SynergyProfiles.Analyze(Card("Enemy watcher","Creature — Human","Whenever an opponent casts an Elf spell, draw a card."))).CreatureTypes);}
    [Fact]
    public void ExperienceDeathPayoffAndUnrestrictedCardRecursionAreUsefulWithoutGuessingThresholds()
    {
        var xp=SynergyProfiles.Analyze(Card("Experience chief","Legendary Creature — Human","Whenever another creature you control dies, you get an experience counter."));Assert.True(xp.Has(OwnCreatureDeathPayoff));
        var witness=SynergyProfiles.Analyze(Card("Witness","Creature — Human Shaman","When this creature enters, you may return target card from your graveyard to your hand."));Assert.True(witness.Has(TypedCardRecursion));Assert.Contains(SynergyEngine.Between(SynergyProfiles.Analyze(Card("Old spell","Instant","")),witness),r=>r.Kind=="typed-recursion");
    }
    [Fact]
    public void TooMuchSetupLosesPriorityToActualReuse()
    {
        var setup=Card("Setup","Creature — Human","Mill three cards.");var reuse=Card("Reuse","Creature — Human","Return target card from your graveyard to your hand.");var selection=new SynergySelection([setup,reuse],62,new("Balance","casual60","selected","WUBRG","graveyard","balanced",true));
        for(var i=0;i<18;i++)selection.Pick(setup);Assert.True(selection.Score(reuse)>selection.Score(setup));
    }
}
