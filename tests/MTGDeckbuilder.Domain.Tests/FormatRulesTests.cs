using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;

public sealed class FormatRulesTests
{
    [Theory]
    [InlineData("Standard", DeckRuleProfile.Unspecified)]
    [InlineData("Traditional Standard", DeckRuleProfile.Unspecified)]
    [InlineData("Brawl", DeckRuleProfile.Unspecified)]
    [InlineData(DeckRuleProfiles.Bo1, DeckRuleProfile.ArenaStandardBo1)]
    [InlineData(DeckRuleProfiles.Bo3, DeckRuleProfile.ArenaStandardBo3)]
    public void OnlyExplicitLabelsSelectAProfile(string format, DeckRuleProfile expected)
        => Assert.Equal(expected, new Deck("Test", format).ValidationProfile);

    [Fact]
    public void ProfileSelectionPreservesTheOriginalLabelAndRejectsUnknownValues()
    {
        var deck = new Deck("Old", "Standard"); deck.SetValidationProfile(DeckRuleProfile.ArenaStandardBo3);
        Assert.Equal("Standard", deck.Format);
        Assert.Throws<ArgumentException>(() => deck.SetValidationProfile((DeckRuleProfile)999));
        Assert.Throws<ArgumentException>(() => new CardFormatLegality(Guid.Empty, Guid.NewGuid(), CardLegality.Legal));
        Assert.Throws<ArgumentException>(() => new CardFormatLegality(Guid.NewGuid(), Guid.NewGuid(), (CardLegality)99));
    }
}
