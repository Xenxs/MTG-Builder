using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class StandardDeckValidatorTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 8, 30, 0, DateTimeKind.Utc);
    private static readonly LegalitySnapshot Fresh = new(Guid.NewGuid(), "Scryfall Default Cards", "fixture", Now.AddHours(-1), Now, 1);
    private static ValidationCard Card(string name = "Forest", int qty = 60, DeckZone zone = DeckZone.Mainboard,
        string type = "Basic Land — Forest", string text = "", CardLegality legality = CardLegality.Legal)
        => new(Guid.NewGuid(), Guid.NewGuid().ToString(), name, type, text, legality, zone, qty, true);
    private static DeckValidation Check(IEnumerable<ValidationCard> cards, DeckRuleProfile profile = DeckRuleProfile.ArenaStandardBo1,
        LegalitySnapshot? snapshot = null, DateTime? now = null)
        => StandardDeckValidator.Validate(profile, cards.ToArray(), snapshot ?? Fresh, now ?? Now);

    [Theory]
    [InlineData(DeckRuleProfile.ArenaStandardBo1)]
    [InlineData(DeckRuleProfile.ArenaStandardBo3)]
    public void BothProfilesAcceptFifteenSideboardCardsAndUnlimitedBasics(DeckRuleProfile profile)
    {
        var forest = Card(); var result = Check([forest, forest with { Zone = DeckZone.Sideboard, Quantity = 15 }], profile);
        Assert.True(result.Passed); Assert.Equal(60, result.Mainboard); Assert.Equal(15, result.Sideboard);
    }
    [Fact]
    public void SizesAndCommanderProduceActionableErrors()
    {
        var card = Card(qty: 59); var result = Check([card, card with { Zone = DeckZone.Sideboard, Quantity = 16 }, card with { Zone = DeckZone.Commander, Quantity = 1 }]);
        Assert.Equal(new[] { "main-min", "side-max", "commander" }, result.Issues.Select(x => x.Code));
        Assert.All(result.Issues, x => Assert.NotEmpty(x.Action)); Assert.False(result.Passed);
    }
    [Fact]
    public void CopiesAreCountedAcrossPrintingsAndZonesWithoutCountingOwnership()
    {
        var spell = Card("Negate", 3, type: "Instant");
        var result = Check([Card(), spell, spell with { PrintingId = Guid.NewGuid(), Zone = DeckZone.Sideboard, Quantity = 2 }]);
        Assert.Equal("copies", Assert.Single(result.Issues).Code); Assert.Contains("5 exemplaren", result.Issues[0].Message);
    }
    [Theory]
    [InlineData("A deck can have any number of cards named Hare Apparent.", 60, false)]
    [InlineData("A deck can have up to seven cards named Hare Apparent.", 7, false)]
    [InlineData("A deck can have up to seven cards named Hare Apparent.", 8, true)]
    public void ExactCatalogCopyExceptionsOverrideFour(string text, int qty, bool error)
    {
        var result = Check([Card(), Card("Hare Apparent", qty, type: "Creature — Rabbit", text: text)]);
        Assert.Equal(error, result.Errors > 0); Assert.Equal(0, result.Unknowns);
    }
    [Fact]
    public void UnknownExceptionIsIncompleteRatherThanAssumingFour()
    {
        var result = Check([Card(), Card("Future", 1, type: "Creature", text: "A deck can have exactly twelve cards named Future.")]);
        Assert.Equal("copy-exception", Assert.Single(result.Issues).Code); Assert.False(result.Passed);
    }
    [Fact]
    public void LeylineIsAnAdditionalBo1BanButBo3UsesStandardLegality()
    {
        var cards = new[] { Card(), Card("Leyline of Resonance", 4, type: "Enchantment") };
        Assert.Equal("official-ban", Assert.Single(Check(cards).Issues).Code);
        Assert.True(Check(cards, DeckRuleProfile.ArenaStandardBo3).Passed);
    }
    [Theory]
    [InlineData(CardLegality.Banned, ValidationSeverity.Error)]
    [InlineData(CardLegality.NotLegal, ValidationSeverity.Error)]
    [InlineData(CardLegality.Restricted, ValidationSeverity.Unknown)]
    [InlineData(CardLegality.Unknown, ValidationSeverity.Unknown)]
    public void CatalogStatusHasNoSilentFallback(CardLegality status, ValidationSeverity severity)
        => Assert.Equal(severity, Assert.Single(Check([Card(legality: status)]).Issues).Severity);
    [Fact]
    public void OfficialBanCatchesAContradictingLegalCatalogEntry()
        => Assert.Equal("official-ban", Assert.Single(Check([Card(), Card("Vivi Ornitier", 1, type: "Creature")]).Issues).Code);
    [Fact]
    public void UnverifiedManualBasicsAndMissingPrintingsCannotPass()
    {
        Assert.Contains(Check([Card() with { OracleId = null }]).Issues, x => x.Code == "identity");
        Assert.Contains(Check([Card() with { AvailableOnArena = false }]).Issues, x => x.Code == "arena-printing");
    }
    [Fact]
    public void ConflictingIdentitiesAreNotSilentlyMergedByName()
        => Assert.Equal("identity-conflict", Assert.Single(Check([Card(qty: 30), Card(qty: 30)]).Issues).Code);
    [Fact]
    public void MissingOldFutureAndUnversionedSourcesCannotPass()
    {
        foreach (var snapshot in new[] { Fresh with { SourceUpdatedUtc = null }, Fresh with { SourceUpdatedUtc = Now.AddDays(-2) },
            Fresh with { SourceUpdatedUtc = Now.AddHours(1) }, Fresh with { MetadataVersion = 0 } })
            Assert.False(Check([Card()], snapshot: snapshot).Passed);
        Assert.Contains(Check([Card()], now: Now.AddDays(15)).Issues, x => x.Code == "rules-stale");
        Assert.Equal("profile", Assert.Single(Check([Card()], DeckRuleProfile.Unspecified).Issues).Code);
    }
}
