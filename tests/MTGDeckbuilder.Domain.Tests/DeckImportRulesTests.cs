using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;

public sealed class DeckImportRulesTests
{
    [Fact]
    public void ImportReceiptCannotCreateTwoDecks()
    {
        var batch = new DeckImportBatch("Elves", "Standard", "4 Elves");
        var version = batch.Version; batch.Touch(); Assert.NotEqual(version, batch.Version);
        var deckId = Guid.NewGuid(); batch.Apply(deckId);
        Assert.Equal(deckId, batch.DeckId); Assert.Equal(DeckImportState.Applied, batch.State);
        Assert.Throws<ArgumentException>(() => batch.Apply(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => batch.Touch());
        Assert.Throws<ArgumentException>(() => batch.Discard());
    }
    [Fact]
    public void DiscardedPreparationCannotCreateDeckAndCommanderZoneIsAccepted()
    {
        var batch = new DeckImportBatch("Brawl", "Brawl", "1 Commander"); batch.Discard();
        Assert.Throws<ArgumentException>(() => batch.Apply(Guid.NewGuid()));
        var card = new DeckCard(Guid.NewGuid(), Guid.NewGuid(), DeckZone.Commander, 1);
        Assert.Equal(DeckZone.Commander, card.Zone);
    }
}
