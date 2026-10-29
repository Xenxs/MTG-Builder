using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;

public sealed class DeckAdvicePlanTests
{
    [Fact]
    public void ReceiptIsOneTimeAndRejectsInvalidIdentityOrEmptyNames()
    {
        Assert.Throws<ArgumentException>(() => new DeckAdvicePlan(Guid.Empty, "hash", "Name", "[]"));
        Assert.Throws<ArgumentException>(() => new DeckAdvicePlan(Guid.NewGuid(), "hash", " ", "[]"));
        var plan = new DeckAdvicePlan(Guid.NewGuid(), "hash", " Name ", "[]");
        Assert.Equal("Name", plan.Name); Assert.Null(plan.AppliedDeckId);
        Assert.Throws<ArgumentException>(() => plan.MarkApplied(Guid.Empty));
        var id = Guid.NewGuid(); plan.MarkApplied(id); Assert.Equal(id, plan.AppliedDeckId);
        Assert.Throws<InvalidOperationException>(() => plan.MarkApplied(Guid.NewGuid()));
    }
}
