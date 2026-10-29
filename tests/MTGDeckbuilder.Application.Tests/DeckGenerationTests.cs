using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckGenerationTests
{
    internal static ArchetypePoolInput FullInput()
    {
        var input = ArchetypePoolTests.Input();
        return input with { Stock = input.Printings.Select(x => new AdviceStock(x.Id, x.Name == "Mountain" ? 22 : 4, Guid.NewGuid())).ToArray() };
    }
    internal static GeneratedDeck Build(ArchetypePoolInput input, GenerationMode mode = GenerationMode.OwnedOnly)
        => DeckGenerationEngine.Build(input, new string('A', 64), mode, ArchetypePoolTests.Now);
    [Fact]
    public void FullStockProducesExactTemplateCurveAndLegalMainboardWithoutInventedStock()
    {
        var input = FullInput(); var result = Build(input);
        Assert.True(result.CanPreview, result.Status); Assert.Equal(60, result.Count); Assert.Equal(0, result.Missing);
        Assert.Equal(22, result.Lines.Where(x => x.Entry.Slot == AggroSlot.Land).Sum(x => x.Quantity));
        Assert.Equal(20, result.Lines.Where(x => x.Printing.TypeLine!.Contains("Creature")).Sum(x => x.Quantity));
        Assert.True(result.OneDrops >= 12); Assert.True(result.AverageMana <= 2);
        Assert.All(result.Groups, x => Assert.Equal(x.Required, x.Selected));
        Assert.All(result.Lines.Where(x => x.Entry.Slot != AggroSlot.Land).GroupBy(x => x.Entry.OracleId), x => Assert.InRange(x.Sum(c => c.Quantity), 1, 4));
        Assert.All(result.Lines, x => { Assert.True(x.Quantity <= x.Owned); Assert.NotEmpty(x.Entry.Cautions); Assert.Contains(x.Entry.Cautions, x.Details); });
        Assert.DoesNotContain(result.Lines, x => !x.Entry.Included);
    }
    [Theory]
    [InlineData("lands")][InlineData("creatures")][InlineData("wrong-functions")][InlineData("curve")]
    public void InsufficientFunctionsOrCurveCannotBeSavedAsComplete(string scenario)
    {
        var input = FullInput();
        bool Keep(AdvicePrinting card) => scenario switch
        {
            "lands" => card.Name != "Mountain", "creatures" => !card.TypeLine!.Contains("Creature"),
            "wrong-functions" => card.Name is "Mountain" or "Shock" or "Lightning Strike",
            _ => card.ManaValue != 1
        };
        var result = Build(input with { Stock = input.Stock.Where(x => Keep(input.Printings.Single(p => p.Id == x.PrintingId))).ToArray() });
        Assert.False(result.CanPreview); Assert.NotEmpty(result.Problems); Assert.Contains("Geen compleet", result.Summary);
        if (scenario == "lands") { Assert.Equal(38, result.Count); Assert.Contains(result.Problems, x => x.StartsWith("Mountains: 22")); }
        if (scenario == "curve") Assert.Contains(result.Problems, x => x.StartsWith("Curve:"));
    }
    [Fact]
    public void PlanningIsExplicitAndListsAllSixtyMissingIncludingBasicLands()
    {
        var input = FullInput() with { Stock = [] }; Assert.False(Build(input).CanPreview);
        var result = Build(input, GenerationMode.Planning); Assert.True(result.CanPreview, result.Status);
        Assert.Equal(60, result.Missing); Assert.Contains("PLANNING", result.ModeLabel); Assert.Contains("22× Mountain", result.Shortages);
        Assert.All(result.Lines, x => Assert.Equal(x.Quantity, x.Missing));
    }
    [Fact]
    public void MultiplePrintingsShareOneFourCopyLimitAndUseOwnedCopiesBeforeShortages()
    {
        var input = FullInput(); var original = input.Printings.Single(x => x.Name == "Clockwork Percussionist");
        var reprint = original with { Id = Guid.NewGuid(), SetCode = "OTHER" };
        input = input with
        {
            Printings = [.. input.Printings, reprint],
            Stock = [.. input.Stock.Select(x => x.PrintingId == original.Id ? x with { Quantity = 2 } : x), new(reprint.Id, 2, Guid.NewGuid())]
        };
        var result = Build(input); var lines = result.Lines.Where(x => x.Entry.OracleId == original.OracleId).ToArray();
        Assert.True(result.CanPreview); Assert.Equal(2, lines.Length); Assert.All(lines, x => Assert.Equal(2, x.Quantity)); Assert.Equal(4, lines.Sum(x => x.Quantity));
        var planned = Build(input, GenerationMode.Planning); Assert.Equal(0, planned.Missing);
    }
    [Fact]
    public void PlanningPrefersOwnedFunctionBeforePreferredUnownedCards()
    {
        var input = FullInput(); var ownedNames = new[] { "Mountain", "Ghitu Lavarunner", "Frenzied Goblin", "Charming Scoundrel", "Fugitive Codebreaker",
            "Viashino Pyromancer", "Shock", "Boltwave", "Scorching Shot", "Crash Through", "Blazing Crescendo", "Grab the Prize" };
        input = input with { Stock = input.Stock.Where(x => ownedNames.Contains(input.Printings.Single(p => p.Id == x.PrintingId).Name)).ToArray() };
        var result = Build(input, GenerationMode.Planning); Assert.True(result.CanPreview, result.Status); Assert.Equal(0, result.Missing);
        Assert.Contains(result.Lines, x => x.Printing.Name == "Fugitive Codebreaker"); Assert.DoesNotContain(result.Lines, x => x.Printing.Name == "Slickshot Show-Off");
        Assert.Contains(result.Lines, x => x.Printing.Name == "Grab the Prize" && x.Details.Contains("discard", StringComparison.OrdinalIgnoreCase));
    }
    [Theory]
    [InlineData("old")][InlineData("missing")][InlineData("rules")]
    public void SourceAndRuleExpiryRemainHardGatesEvenForPlanning(string mode)
    {
        var input = FullInput(); var now = ArchetypePoolTests.Now;
        input = input with { Source = mode switch { "old" => input.Source with { SourceUpdatedUtc = now.AddDays(-2) }, "missing" => input.Source with { RunId = null }, _ => input.Source } };
        if (mode == "rules") now = now.AddDays(16);
        var result = DeckGenerationEngine.Build(input, "hash", GenerationMode.Planning, now);
        Assert.False(result.CanPreview); Assert.Empty(result.Lines); Assert.False(result.Validation.Passed);
    }
    [Theory]
    [InlineData("metadata")][InlineData("banned")][InlineData("inactive")]
    public void UnsuitablePrintingIsExcludedWhileOtherVerifiedCardsMayFillThePlan(string mode)
    {
        var input = FullInput(); input = input with { Printings = input.Printings.Select(x => x.Name != "Clockwork Percussionist" ? x : mode switch
        { "metadata" => x with { OracleText = "Changed" }, "banned" => x with { Standard = CardLegality.Banned }, _ => x with { AvailableOnArena = false } }).ToArray() };
        var result = Build(input); Assert.True(result.CanPreview, result.Status); Assert.DoesNotContain(result.Lines, x => x.Printing.Name == "Clockwork Percussionist");
        Assert.False(result.Pool.Cards.Single(x => x.Entry.Name == "Clockwork Percussionist").Eligible);
    }
    [Fact]
    public void InputOrderDoesNotChangeTheProposalOrPrintingAllocation()
    {
        var input = FullInput(); var first = Build(input); var reverse = Build(input with { Printings = input.Printings.Reverse().ToArray(), Stock = input.Stock.Reverse().ToArray() });
        Assert.Equal(first.Lines, reverse.Lines); Assert.Equal(first.Groups, reverse.Groups);
        Assert.Throws<ArgumentException>(() => Build(input, (GenerationMode)999));
    }
}
