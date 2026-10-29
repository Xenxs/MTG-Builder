using System.Text.Json;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CardRoleTests
{
    private static readonly ReferenceSet Reference = JsonSerializer.Deserialize<ReferenceSet>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "role-reference-v3.json")))!;
    public static TheoryData<string> CardNames => new(Reference.Cases.Select(x => x.Name));
    [Theory, MemberData(nameof(CardNames))]
    public void ReviewedOracleReferenceKeepsExpectedRoleTargetAndRestriction(string name)
    {
        var reference = Reference.Cases.Single(x => x.Name == name); var result = CardRoleClassifier.Analyze(reference.Printing());
        Assert.Equal(reference.ExpectedRole, result.Role.ToString()); Assert.Equal(reference.ExpectedTarget, result.Target);
        Assert.Equal(reference.ExpectedRestriction, result.Restriction); Assert.Equal(CardRoleClassifier.Version, result.Version);
        Assert.Equal(reference.ExpectedComplete, result.Complete);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation)); Assert.Equal(result.Known, result.RuleId != "unknown");
    }
    [Theory]
    [InlineData("Choose one —\n• Draw a card.\n• Counter target spell.")]
    [InlineData("As an additional cost to cast this spell, discard a card. Draw two cards.")]
    [InlineData("Whenever you cast a spell, draw a card.")]
    [InlineData("{T}: Add {G}.")]
    [InlineData("You may draw two cards.")]
    public void WholeTextMustMatchInsteadOfContainingAKeyword(string text)
    {
        var printing = Reference.Cases.Single(x => x.Name == "Negate").Printing() with { OracleText = text };
        Assert.Equal(CardRole.Unknown, CardRoleClassifier.Analyze(printing).Role);
    }
    [Fact]
    public void TargetTaxAmountTimingAndTemporaryManaStayVisibleAsLimitations()
    {
        AdvicePrinting Card(string name) => Reference.Cases.Single(x => x.Name == name).Printing();
        var negate = Card("Negate");
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(negate, Card("Essence Scatter")).Fit);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(negate, Card("Spell Pierce")).Fit);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(negate, Card("Disdainful Stroke")).Fit);
        Assert.Equal(RoleFit.Matching, CardRoleClassifier.Compare(Card("Spell Pierce"), negate).Fit);
        Assert.Equal(RoleFit.Matching, CardRoleClassifier.Compare(negate, Card("Cancel")).Fit);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Murder"), Card("Go for the Throat")).Fit);
        Assert.Equal(RoleFit.Different, CardRoleClassifier.Compare(Card("Murder"), Card("Dark Ritual")).Fit);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Opportunity"), Card("Quick Study")).Fit);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Rampant Growth"), Card("Dark Ritual")).Fit);
        var draw = Card("Quick Study");
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(draw, draw with { TypeLine = "Sorcery" }).Fit);
        Assert.Equal(CardRole.Unknown, CardRoleClassifier.Analyze(draw with { TypeLine = "Instant // Sorcery" }).Role);
        Assert.Equal(CardRole.Unknown, CardRoleClassifier.Analyze(Card("Dark Ritual") with { ManaValue = 3 }).Role);
        Assert.Equal(RoleFit.Limited, CardRoleClassifier.Compare(Card("Naturalize"), Card("Murder") with { OracleText = "Destroy target nonland permanent." }).Fit);
    }
    [Fact]
    public void FixedRankingReferenceImprovesOnFrozenPhaseSixOrder()
    {
        var now = DeckValidationPersistenceTests.SourceTime.AddHours(1); var rows = new List<object>();
        var oldCorrect = 0; var newCorrect = 0; double oldRr = 0, newRr = 0;
        foreach (var scenario in Reference.Rankings)
        {
            var from = Reference.Cases.Single(x => x.Name == scenario.From).Printing();
            var candidates = scenario.Candidates.Select(n => Reference.Cases.Single(x => x.Name == n).Printing()).ToArray();
            var forest = new AdvicePrinting(Guid.NewGuid(), Guid.NewGuid().ToString(), "Forest", "REF", "0", "G", 0, "Basic Land — Forest", "", CardLegality.Legal, true);
            var snapshot = new AdviceSnapshot(Guid.NewGuid(), "Reference", DeckRuleProfiles.Bo3, DeckRuleProfile.ArenaStandardBo3,
                new(Guid.NewGuid(), "Fixed reference", "v1", now.AddHours(-1), now, 1), [forest, from, .. candidates],
                [new(forest.Id, DeckZone.Mainboard, 60), new(from.Id, DeckZone.Mainboard, 1)],
                [new(forest.Id, 60, Guid.NewGuid()), .. candidates.Select(x => new AdviceStock(x.Id, 1, Guid.NewGuid()))]);
            var result = DeckAdviceEngine.Candidates(snapshot, "ref", [], from.Id, DeckZone.Mainboard, now, TestContext.Current.CancellationToken);
            var byId = candidates.ToDictionary(x => x.Id); var actual = result.Select(x => byId[x.PrintingId]).ToArray();
            // Frozen v0.6 order on the identical legal/in-stock candidate pool; roles never filter eligibility.
            string[] Types(AdvicePrinting x) => new[] { "Creature", "Instant", "Sorcery", "Artifact", "Enchantment", "Planeswalker", "Battle" }
                .Intersect(x.TypeLine!.Split(" // ")[0].Split('—')[0].Split(' ')).ToArray();
            var old = actual.OrderByDescending(x => x.OracleId == from.OracleId).ThenByDescending(x => x.ColorIdentity == from.ColorIdentity)
                .ThenBy(x => Math.Abs(x.ManaValue - from.ManaValue))
                .ThenBy(x => Types(from).Union(Types(x)).Count() - Types(from).Intersect(Types(x)).Count())
                .ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.SetCode, StringComparer.Ordinal)
                .ThenBy(x => x.CollectorNumber, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray();
            var oldPosition = Array.FindIndex(old, x => x.Name == scenario.ExpectedTop) + 1;
            var position = Array.FindIndex(actual, x => x.Name == scenario.ExpectedTop) + 1;
            Assert.True(oldPosition > 0); Assert.True(position > 0);
            if (oldPosition == 1) oldCorrect++; if (position == 1) newCorrect++;
            oldRr += 1d / oldPosition; newRr += 1d / position;
            rows.Add(new { scenario.Id, scenario.From, scenario.ExpectedTop, scenario.Reason, OldTop = old[0].Name, NewTop = actual[0].Name,
                OldPosition = oldPosition, NewPosition = position, Role = result[0].Role!.Fit.ToString() });
            Assert.Equal(scenario.ExpectedTop, actual[0].Name); Assert.Equal(scenario.ExpectedFit, result[0].Role!.Fit.ToString());
        }
        Assert.True(newCorrect > oldCorrect); Assert.Equal(Reference.Rankings.Length, newCorrect);
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if (!string.IsNullOrEmpty(directory))
        {
            var report = new { Reference.DatasetVersion, Classifier = CardRoleClassifier.Version, Classifications = Reference.Cases.Length,
                FullyRecognized = Reference.Cases.Count(x => x.ExpectedRole != "Unknown" && x.ExpectedComplete),
                PartialEvidence = Reference.Cases.Count(x => x.ExpectedRole != "Unknown" && !x.ExpectedComplete), Unknown = Reference.Cases.Count(x => x.ExpectedRole == "Unknown"),
                Scenarios = rows.Count, Phase6Top1 = oldCorrect, Phase7Top1 = newCorrect,
                Phase6Mrr = oldRr / rows.Count, Phase7Mrr = newRr / rows.Count,
                Limit = "Curated development/reference set with synthetic ownership/legalities, not a held-out or representative quality/win-rate claim.", Rows = rows };
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(Directory.GetParent(directory)!.FullName, "phase9-role-development-evaluation.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private sealed record ReferenceSet(string DatasetVersion, ReferenceCard[] Cases, RankingCase[] Rankings);
    private sealed record RankingCase(string Id, string From, string[] Candidates, string ExpectedTop, string ExpectedFit, string Reason);
    private sealed record ReferenceCard(string Name, string TypeLine, string OracleText, string ManaCost, string Colors,
        double ManaValue, string OracleId, string ExpectedRole, string ExpectedTarget, string ExpectedRestriction, bool ExpectedComplete)
    {
        public AdvicePrinting Printing() => new(Guid.Parse(OracleId), OracleId, Name, "REF", "1", Colors, ManaValue, TypeLine, OracleText, CardLegality.Legal, true, ManaCost);
    }
}
