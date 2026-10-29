using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;

public sealed class CatalogRulesTests
{
    [Fact]
    public void CatalogIdentitiesAreStableAndColorIdentityIsNormalized()
    {
        var oracle = Guid.NewGuid().ToString();
        var card = new Card("Test");
        card.UpdateCatalog(oracle, "Test", "{U}{R}", "Sorcery", "Rules", "RUU", 2);
        Assert.Equal("UR", card.ColorIdentity);
        Assert.False(card.UpdateCatalog(oracle, "Test", "{U}{R}", "Sorcery", "Rules", "UR", 2));
        Assert.Throws<ArgumentException>(() => card.UpdateCatalog(Guid.NewGuid().ToString(), "Other", "", "", "", "", 0));
        Assert.Equal("Test", card.Name);
        Assert.Throws<ArgumentException>(() => card.UpdateCatalog(oracle, "Test", "", "", "", "X", 0));
        Assert.Throws<ArgumentException>(() => card.UpdateCatalog(oracle, "Test", "", "", "", "", double.NaN));
    }

    [Fact]
    public void InvalidImportIdDoesNotPartiallyMutateAPrinting()
    {
        var printing = new Printing(Guid.NewGuid(), "FDN", "1");
        Assert.Throws<ArgumentException>(() => printing.UpdateCatalog(Guid.NewGuid(), Guid.NewGuid().ToString(), 1, "en", "Test", "rare", Guid.Empty));
        Assert.Null(printing.ScryfallId);
        Assert.False(printing.IsInCatalog);
    }

    [Fact]
    public void ImportCheckpointsCannotMoveBackwardsOrExceedProcessedRecords()
    {
        var run = new CatalogImportRun("Test", "v1");
        run.SetCache(new string('A', 64), 100, useHashAsVersion: true);
        run.Checkpoint(500, 100);
        Assert.Throws<ArgumentException>(() => run.Checkpoint(499, 100));
        Assert.Throws<ArgumentException>(() => run.Checkpoint(600, 601));
        Assert.Throws<ArgumentException>(() => run.Complete(-1, 0));
        run.Complete(100, 0);
        Assert.Equal(CatalogImportState.Completed, run.State);
        Assert.NotNull(run.CompletedUtc);
    }
}
