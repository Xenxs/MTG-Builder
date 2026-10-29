using System.Text.Json;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DamageAndModalRoleTests
{
    private static AdvicePrinting Card(string text, string name = "Test Spell") => new(Guid.NewGuid(), Guid.NewGuid().ToString(),
        name, "TST", "1", "R", 2, "Instant", text, CardLegality.Legal, true, "{1}{R}");

    [Theory]
    [InlineData("Test Spell deals 3 damage to any target.", "any target", "", 3)]
    [InlineData("Test Spell deals 2 damage to target creature.", "creature", "", 2)]
    [InlineData("This spell deals 9 damage to target creature or planeswalker an opponent controls.", "creature or planeswalker", "an opponent controls", 9)]
    [InlineData("Test Spell deals 4 damage to target attacking creature.", "creature", "attacking", 4)]
    public void ExactDamageKeepsAmountTargetAndConditionsWithoutProvingLethality(string text, string target, string restriction, int amount)
    {
        var result = CardRoleClassifier.Analyze(Card(text));
        Assert.True(result.FullyRecognized); Assert.Equal(CardRole.Removal, result.Role); Assert.Equal("damage", result.Effect);
        Assert.Equal(amount, result.Amount); Assert.Equal(target, result.Target); Assert.Equal(restriction, result.Restriction);
        Assert.Contains("dodelijkheid onbekend", result.Label); Assert.Contains("toughness", result.Details);
        Assert.NotEqual(RoleFit.Matching, CardRoleClassifier.Compare(Card(text), Card(text)).Fit);
    }
    [Theory]
    [InlineData("Test Spell deals X damage to target creature.")]
    [InlineData("Test Spell deals damage equal to its power to target creature.")]
    [InlineData("Test Spell deals 3 damage to each creature.")]
    [InlineData("Test Spell deals 3 damage to target opponent.")]
    [InlineData("If you attacked this turn, Test Spell deals 3 damage to target creature.")]
    [InlineData("Other Spell deals 3 damage to target creature.")]
    public void VariableMassPlayerOnlyAndWrongSourceTextDoNotProveTargetedRemoval(string text)
        => Assert.False(CardRoleClassifier.Analyze(Card(text)).Known);

    [Fact]
    public void DamageRemainderAndExtraCostsRemainVisibleAndNeverBecomeStrong()
    {
        var text = "As an additional cost, sacrifice a creature.\nTest Spell deals 4 damage to target creature. If it would die, exile it instead.";
        var analysis = CardRoleClassifier.Analyze(Card(text));
        Assert.True(analysis.Known); Assert.False(analysis.Complete); Assert.Equal(4, analysis.Amount);
        Assert.Contains("sacrifice a creature", analysis.UnparsedText); Assert.Contains("If it would die", analysis.UnparsedText);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Destroy target creature."), Card(text)).Fit);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Test Spell deals 2 damage to target creature."), Card("Test Spell deals 5 damage to target creature.")).Fit);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Test Spell deals 5 damage to target creature."), Card("Destroy target creature.")).Fit);
    }
    [Fact]
    public void DamageSourceNameIsLiteralAndSupportedSupplementRemainsRecorded()
    {
        var analysis = CardRoleClassifier.Analyze(Card("A.B's Spell deals 3 damage to target creature. Draw a card.", "A.B's Spell"));
        Assert.True(analysis.FullyRecognized); Assert.Equal("draw 1", analysis.OtherEffects);
        Assert.False(CardRoleClassifier.Analyze(Card("AxB's Spell deals 3 damage to target creature.", "A.B's Spell")).Known);
    }
    [Theory]
    [InlineData("Choose one —")]
    [InlineData("Choose two —")]
    [InlineData("Choose one or both —")]
    [InlineData("Choose one. If a condition is met, choose both instead.")]
    public void ModalChoicesKeepDistinctRolesHeaderAndUnknownOptions(string header)
    {
        var text = header + "\n• Counter target spell.\n• Draw two cards.\n• Create a token.";
        var analysis = CardRoleClassifier.Analyze(Card(text));
        Assert.True(analysis.Known); Assert.True(analysis.IsModal); Assert.False(analysis.FullyRecognized); Assert.False(analysis.Complete);
        Assert.Equal(CardRole.Unknown, analysis.Role); Assert.Equal(new[] {CardRole.Counterspell, CardRole.Draw}, analysis.Roles);
        Assert.Equal(3, analysis.Options!.Count); Assert.False(analysis.Options[2].Analysis.Known);
        Assert.Equal(header, analysis.Restriction); Assert.Equal(text, analysis.UnparsedText);
        Assert.Contains("Optie 3: Create a token.", analysis.Details); Assert.Contains("geen onvoorwaardelijke combinatie", analysis.Details);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Counter target spell."), Card(text)).Fit);
        Assert.NotEqual(RoleFit.Matching, CardRoleClassifier.Compare(Card(text), Card(text)).Fit);
        var copy = JsonSerializer.Deserialize<CardRoleAnalysis>(JsonSerializer.Serialize(analysis))!;
        Assert.Equal(analysis.Details, copy.Details); Assert.Equal(analysis.Roles, copy.Roles);
    }
    [Fact]
    public void ModalDamageAndUnknownConditionalOptionNeverBecomeUnconditionalRemoval()
    {
        var text = "Choose one —\n• Test Spell deals 3 damage to target creature.\n• Destroy target artifact. Its controller draws a card.";
        var analysis = CardRoleClassifier.Analyze(Card(text));
        Assert.True(analysis.IsModal); Assert.Equal("damage", analysis.Options![0].Analysis.Effect);
        Assert.False(analysis.Options[1].Analysis.Complete); Assert.Contains("Its controller", analysis.Details);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Destroy target creature."), Card(text)).Fit);
        Assert.Equal(RoleFit.Unknown, CardRoleClassifier.Compare(Card("Draw a card."), Card(text)).Fit);
    }
    [Theory]
    [InlineData("Choose one —\nCounter target spell.\nDraw a card.")]
    [InlineData("This spell has an example:\nChoose one —\n• Counter target spell.\n• Draw a card.")]
    [InlineData("Choose one —\n• Create a token.\n• Gain 2 life.")]
    public void UnsupportedChoiceShapeOrOnlyUnsupportedEffectsStayUnknown(string text)
        => Assert.False(CardRoleClassifier.Analyze(Card(text)).Known);
}
