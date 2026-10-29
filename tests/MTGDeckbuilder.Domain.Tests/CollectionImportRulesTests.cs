using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;

public sealed class CollectionImportRulesTests
{
    [Fact]
    public void AppliedImportCannotBeEditedOrAppliedTwice()
    {
        var batch = new CollectionImportBatch("collection.json", new string('A', 64));
        var version = batch.Version;
        batch.Touch(); Assert.NotEqual(version, batch.Version);
        batch.Apply(3, 1);
        Assert.Equal(CollectionImportState.Applied, batch.State);
        Assert.Equal(3, batch.ChangedCount);
        Assert.NotNull(batch.AppliedUtc);
        Assert.Throws<ArgumentException>(() => batch.Touch());
        Assert.Throws<ArgumentException>(() => batch.Apply(3, 1));
        Assert.Throws<ArgumentException>(() => batch.Discard());
        batch.Undo();
        Assert.Equal(CollectionImportState.Undone, batch.State);
        Assert.NotNull(batch.UndoneUtc);
        Assert.Throws<ArgumentException>(() => batch.Undo());
    }
    [Fact]
    public void DiscardedImportCannotApplyOrUndo()
    {
        var batch = new CollectionImportBatch("collection.json", new string('A', 64));
        Assert.Throws<ArgumentException>(() => batch.Apply(-1, 0));
        Assert.Equal(CollectionImportState.Prepared, batch.State);
        batch.Discard();
        Assert.Throws<ArgumentException>(() => batch.Apply(0, 0));
        Assert.Throws<ArgumentException>(() => batch.Undo());
    }
    [Fact]
    public void EveryExplicitCountWriteChangesVersionEvenWhenRestoringTheSameCount()
    {
        var entry = new CollectionEntry(Guid.NewGuid(), 4);
        var first = entry.Version;
        entry.SetQuantity(2); entry.SetQuantity(4);
        Assert.NotEqual(first, entry.Version);
        var version = entry.Version;
        entry.SetQuantity(4);
        Assert.NotEqual(version, entry.Version);
        var valid = entry.Version;
        Assert.Throws<ArgumentOutOfRangeException>(() => entry.SetQuantity(-1));
        Assert.Equal(valid, entry.Version);
        Assert.Equal(4, entry.Quantity);
    }
}
