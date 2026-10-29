using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;

public sealed class DomainRulesTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CardRequiresName(string name) => Assert.Throws<ArgumentException>(() => new Card(name));

    [Fact]
    public void PrintingNormalizesIdentifiers()
    {
        var printing = new Printing(Guid.NewGuid(), " fdn ", " 12a ");
        Assert.Equal("FDN", printing.SetCode);
        Assert.Equal("12A", printing.CollectorNumber);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    public void CollectionRejectsInvalidCounts(int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CollectionEntry(Guid.NewGuid(), count));

    [Fact]
    public void ZeroOwnedCopiesAreAllowed()
    {
        var entry = new CollectionEntry(Guid.NewGuid(), 4);
        entry.SetQuantity(0);
        Assert.Equal(0, entry.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    public void DeckRowsRequirePositiveCount(int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DeckCard(Guid.NewGuid(), Guid.NewGuid(), DeckZone.Mainboard, count));

    [Fact]
    public void InvalidZoneIsRejected()
        => Assert.Throws<ArgumentException>(() => new DeckCard(Guid.NewGuid(), Guid.NewGuid(), (DeckZone)42, 1));

    [Fact]
    public void EmptyForeignKeyIsRejected()
        => Assert.Throws<ArgumentException>(() => new Printing(Guid.Empty, "FDN", "1"));

    [Fact]
    public void DeckRequiresNameAndFormat()
    {
        Assert.Throws<ArgumentException>(() => new Deck(" ", "Standard"));
        Assert.Throws<ArgumentException>(() => new Deck("Test", " "));
        var deck = new Deck(" Elves ", " Standard ");
        Assert.Equal("Elves", deck.Name);
        Assert.Equal("Standard", deck.Format);
        Assert.Equal(DateTimeKind.Utc, deck.CreatedUtc.Kind);
    }

    [Fact]
    public void OversizeNamesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Card(new string('x', 201)));
        Assert.Throws<ArgumentException>(() => new UserSettings().SetPlayerName(new string('x', 81)));
    }
}
