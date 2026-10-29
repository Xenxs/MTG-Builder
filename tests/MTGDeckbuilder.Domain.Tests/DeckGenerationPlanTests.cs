using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Domain.Tests;

public sealed class DeckGenerationPlanTests
{
    [Fact]
    public void ReceiptValidatesNameAndFingerprintAndCanOnlyBeAppliedOnce()
    {
        var plan = new DeckGenerationPlan(new string('A', 64), " New deck ", "{}");
        Assert.Equal("New deck", plan.Name); Assert.Null(plan.AppliedDeckId);
        Assert.Throws<ArgumentException>(() => plan.MarkApplied(Guid.Empty));
        var id = Guid.NewGuid(); plan.MarkApplied(id); Assert.Equal(id, plan.AppliedDeckId);
        Assert.Throws<InvalidOperationException>(() => plan.MarkApplied(Guid.NewGuid()));
    }
    [Theory]
    [InlineData("short", "Name", "{}")][InlineData("", "Name", "{}")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "", "{}")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "Name", "")]
    public void InvalidRequestsCannotBecomeReviewedPlans(string fingerprint, string name, string json)
        => Assert.Throws<ArgumentException>(() => new DeckGenerationPlan(fingerprint, name, json));
}
