using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;
public sealed class PaperDomainTests
{
    [Fact] public void StockVersionsChangeEvenWhenQuantityReturnsToEarlierValue()
    { var row = new PaperHolding("key", Guid.NewGuid(), "en", "normal", "unknown", "Album", 2, false); var version = row.Version; row.SetQuantity(3); row.SetQuantity(2); Assert.NotEqual(version, row.Version); Assert.Throws<ArgumentOutOfRangeException>(() => row.SetQuantity(-1)); }
    [Fact] public void ImportHasOneWayStateTransitions()
    { var p = new PaperImportPlan("scan.csv", "hash", "stock", "{}"); Assert.Throws<ArgumentException>(p.MarkUndone); p.MarkApplied(); Assert.Throws<ArgumentException>(p.MarkApplied); p.MarkUndone(); Assert.Throws<ArgumentException>(p.MarkUndone); }
    [Fact] public void PhysicalDeckRejectsArenaProfileAndInvalidZone()
    { var d = new PaperDeck("Physical"); Assert.Throws<ArgumentException>(() => d.ChangeProfile("arena-standard-bo1")); Assert.Throws<ArgumentException>(() => new PaperDeckCard(d.Id, Guid.NewGuid(), (DeckZone)99, 1)); Assert.Throws<ArgumentOutOfRangeException>(() => new PaperDeckCard(d.Id, Guid.NewGuid(), DeckZone.Mainboard, 0)); }
}
