using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class RoleTextInterpreterTests
{
    private static AdvicePrinting Card(string text, string type = "Instant") => new(Guid.NewGuid(), Guid.NewGuid().ToString(),
        "Development fixture", "TST", "1", "U", 2, type, text, CardLegality.Legal, true, "{1}{U}");
    [Fact]
    public void FullyUnderstoodLimitedMatchPrecedesPartialEvidenceAtTheSameFitLevel()
    {
        var from = Card("Counter target noncreature spell.") with { Name = "Original" };
        var partial = Card("Counter target spell. Create a 1/1 Bird creature token.") with { Name = "AAA partial" };
        var limited = Card("Counter target creature spell.") with { Name = "ZZZ complete" };
        var land = Card("") with { Name = "Forest", TypeLine = "Basic Land — Forest" };
        var now = DeckValidationPersistenceTests.SourceTime.AddHours(1);
        var snapshot = new AdviceSnapshot(Guid.NewGuid(), "Fixture", DeckRuleProfiles.Bo3, DeckRuleProfile.ArenaStandardBo3,
            new(Guid.NewGuid(), "Fixture", "v1", now.AddHours(-1), now, 1), [from, partial, limited, land],
            [new(land.Id, DeckZone.Mainboard, 60), new(from.Id, DeckZone.Mainboard, 1)],
            [new(land.Id, 60, Guid.NewGuid()), new(partial.Id, 1, Guid.NewGuid()), new(limited.Id, 1, Guid.NewGuid())]);
        var result = DeckAdviceEngine.Candidates(snapshot, "fixture", [], from.Id, DeckZone.Mainboard, now, TestContext.Current.CancellationToken);
        Assert.Equal(limited.Id, result[0].PrintingId); Assert.All(result, x => Assert.Equal(RoleFit.Limited, x.Role!.Fit));
    }
    [Theory]
    [InlineData("Counter target spell. Draw a card.", CardRole.Counterspell, "draw 1")]
    [InlineData("Destroy target creature. You gain 2 life.", CardRole.Removal, "you gain 2 life")]
    [InlineData("Draw three cards, then discard a card.", CardRole.Draw, "discard 1")]
    [InlineData("Discard a card, then draw two cards.", CardRole.Draw, "discard first 1")]
    [InlineData("Scry 2. Draw a card.", CardRole.Draw, "Scry 2")]
    public void FullySupportedCompoundTextKeepsItsOtherEffects(string text, CardRole role, string other)
    {
        var result = CardRoleClassifier.Analyze(Card(text));
        Assert.True(result.FullyRecognized); Assert.Equal(role, result.Role); Assert.Equal(other, result.OtherEffects);
        Assert.Equal(text, result.Evidence); Assert.Empty(result.UnparsedText);
        var pure = CardRoleClassifier.Analyze(Card("Draw a card.")); Assert.True(pure.FullyRecognized);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Draw a card."), Card("Scry 2. Draw a card.")).Fit);
    }
    [Theory]
    [InlineData("Counter target spell. Create a 1/1 Bird creature token.", "Counter target spell.", "Create a 1/1 Bird creature token.")]
    [InlineData("Draw a card. Do this only if you attacked this turn.", "Draw a card.", "only if you attacked")]
    [InlineData("Counter target spell. Its controller draws a card.", "Counter target spell.", "Its controller")]
    public void UnparsedRemainderCannotBecomeACompleteOrStrongRoleMatch(string text, string evidence, string remainder)
    {
        var from = Card(evidence); var candidate = Card(text); var result = CardRoleClassifier.Analyze(candidate);
        Assert.True(result.Known); Assert.False(result.Complete); Assert.False(result.FullyRecognized);
        Assert.Equal(evidence, result.Evidence); Assert.Contains(remainder, result.UnparsedText);
        Assert.Contains("Onvolledige", result.Recognition); Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(from, candidate).Fit);
    }
    [Fact]
    public void TapManaAndDrawTriggersRetainTheirMechanismAndActivationCosts()
    {
        var elf = Card("{T}: Add {G}.", "Creature — Elf Druid"); var mana = CardRoleClassifier.Analyze(elf);
        Assert.True(mana.FullyRecognized); Assert.Equal("tap-ability", mana.Mechanism); Assert.Equal("tap", mana.Restriction);
        var restricted = CardRoleClassifier.Analyze(elf with { OracleText = "{T}: Add {G}. Spend this mana only to cast a creature spell." });
        Assert.False(restricted.Complete); Assert.Contains("Spend this mana only", restricted.UnparsedText);
        var draw = CardRoleClassifier.Analyze(Card("{3}{U}{U}: Draw two cards.", "Creature — Human Wizard"));
        Assert.True(draw.FullyRecognized); Assert.Equal(2, draw.Amount); Assert.Contains("{3}{U}{U}", draw.Restriction);
        var enter = Card("When this creature enters, draw a card.", "Creature — Cat");
        var dies = Card("When this creature dies, draw a card.", "Creature — Cat");
        Assert.Equal("enters-trigger", CardRoleClassifier.Analyze(enter).Mechanism);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(enter, dies).Fit);
    }
    [Theory]
    [InlineData("Choose one —\n• Create a token.\n• Gain 2 life.")]
    [InlineData("This creature has \"Draw a card.\" as reminder text.")]
    [InlineData("Whenever you cast a spell, draw a card.")]
    [InlineData("If you attacked this turn, draw two cards.")]
    public void ContextOrQuotedKeywordsDoNotProveAStandaloneEffect(string text)
        => Assert.False(CardRoleClassifier.Analyze(Card(text)).Known);
    [Fact]
    public void QualifiedTargetsDoNotBecomeUnrestricted()
    {
        var narrow = Card("Exile target attacking creature."); var full = Card("Exile target creature.");
        Assert.Equal("attacking", CardRoleClassifier.Analyze(narrow).Restriction);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(full, narrow).Fit);
        Assert.Equal(RoleFit.Matching, CardRoleClassifier.Compare(narrow, full).Fit);
        var colored = Card("Counter target red or green spell.");
        Assert.Equal("red or green spell", CardRoleClassifier.Analyze(colored).Target);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Counter target spell."), colored).Fit);
    }
}
