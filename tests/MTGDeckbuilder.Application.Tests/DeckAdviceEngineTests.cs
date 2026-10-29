using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckAdviceEngineTests
{
    private static readonly DateTime Now = DeckValidationPersistenceTests.SourceTime.AddHours(1);
    private static AdvicePrinting Card(string name, string type = "Instant", string colors = "G", double mana = 2, string? oracle = null)
        => new(Guid.NewGuid(), oracle ?? Guid.NewGuid().ToString(), name, "TST", "1", colors, mana, type, "Draw a card.", CardLegality.Legal, true);
    private static AdviceSnapshot Snapshot(AdvicePrinting missing, params AdvicePrinting[] candidates)
    {
        var land = Card("Forest", "Basic Land — Forest", "G", 0);
        return new(Guid.NewGuid(), "Original", DeckRuleProfiles.Bo1, DeckRuleProfile.ArenaStandardBo1,
            new(Guid.NewGuid(), "Scryfall", "v1", Now.AddHours(-1), Now, 1), [land, missing, .. candidates],
            [new(land.Id, DeckZone.Mainboard, 60), new(missing.Id, DeckZone.Mainboard, 4)],
            [new(land.Id, 60, Guid.NewGuid()), .. candidates.Select(x => new AdviceStock(x.Id, 4, Guid.NewGuid()))]);
    }
    [Fact]
    public void RankingPrefersReprintsThenExactColorsManaAndStableNames()
    {
        var missing = Card("Original"); var reprint = missing with { Id = Guid.NewGuid(), SetCode = "ALT" };
        var close = Card("Alpha"); var tied = Card("Beta"); var far = Card("Far", mana: 4); var colorless = Card("Colorless", colors: "");
        var wrongColor = Card("Blue", colors: "U"); var wrongType = Card("Creature", type: "Creature — Elf");
        var snap = Snapshot(missing, colorless, far, tied, wrongType, reprint, wrongColor, close);
        var result = DeckAdviceEngine.Candidates(snap, "hash", [], missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { reprint.Id, close.Id, tied.Id, far.Id, colorless.Id }, result.Select(x => x.PrintingId));
        Assert.True(result[0].SameCard); Assert.Contains("geen gelijkwaardig", result[1].Reason);
        Assert.Equal(result, DeckAdviceEngine.Candidates(snap with { Printings = snap.Printings.Reverse().ToArray() }, "hash", [], missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void ReprintsAreOfferedForSideboardAndLandsButOtherCardsAreNot()
    {
        var missing = Card("Plains", "Basic Land — Plains"); var reprint = missing with { Id = Guid.NewGuid() };
        var other = Card("Other land", "Land"); var snap = Snapshot(missing, reprint, other);
        Assert.Equal(reprint.Id, Assert.Single(DeckAdviceEngine.Candidates(snap, "x", [], missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken)).PrintingId);
        missing = Card("Side"); reprint = missing with { Id = Guid.NewGuid() }; other = Card("Other"); snap = Snapshot(missing, reprint, other);
        snap = snap with { Lines = snap.Lines.Select(x => x.PrintingId == missing.Id ? x with { Zone = DeckZone.Sideboard } : x).ToArray() };
        Assert.Equal(reprint.Id, Assert.Single(DeckAdviceEngine.Candidates(snap, "x", [], missing.Id, DeckZone.Sideboard, Now, TestContext.Current.CancellationToken)).PrintingId);
    }
    [Fact]
    public void SharedStockAndCopyLimitsIncludeAllZonesAndPrintings()
    {
        var missing = Card("Missing"); var candidate = Card("Candidate"); var reprint = candidate with { Id = Guid.NewGuid() };
        var snap = Snapshot(missing, candidate, reprint);
        snap = snap with { Lines = [.. snap.Lines, new(candidate.Id, DeckZone.Sideboard, 1), new(reprint.Id, DeckZone.Sideboard, 1)] };
        var result = DeckAdviceEngine.Candidates(snap, "x", [], missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken);
        Assert.All(result, x => Assert.Equal(2, x.Available));
        AdviceChoice[] chosen = [new(missing.Id, DeckZone.Mainboard, candidate.Id, 2)];
        Assert.Empty(DeckAdviceEngine.Candidates(snap, "x", chosen, missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => DeckAdviceEngine.Build(snap, "x", [.. chosen, new(missing.Id, DeckZone.Mainboard, reprint.Id, 1)], Now));
    }
    [Fact]
    public void MultipleMissingCardsCannotSpendTheSameInventoryTwice()
    {
        var a = Card("Missing A"); var b = Card("Missing B"); var candidate = Card("Candidate");
        var snap = Snapshot(a, candidate);
        snap = snap with { Printings = [.. snap.Printings, b], Lines = [.. snap.Lines, new(b.Id, DeckZone.Mainboard, 2)] };
        AdviceChoice[] chosen = [new(a.Id, DeckZone.Mainboard, candidate.Id, 3)];
        Assert.Equal(1, Assert.Single(DeckAdviceEngine.Candidates(snap, "x", chosen, b.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken)).Available);
        Assert.Throws<ArgumentException>(() => DeckAdviceEngine.Build(snap, "x", [.. chosen, new(b.Id, DeckZone.Mainboard, candidate.Id, 2)], Now));
    }
    [Fact]
    public void SideboardGetsExactStockFirstAndOnlyShortagesMayBeReplaced()
    {
        var missing = Card("Shared"); var candidate = Card("Candidate"); var snap = Snapshot(missing, candidate);
        snap = snap with { Lines = [.. snap.Lines.Where(x => x.PrintingId != missing.Id), new(missing.Id, DeckZone.Mainboard, 3), new(missing.Id, DeckZone.Sideboard, 1)],
            Stock = [.. snap.Stock, new(missing.Id, 2, Guid.NewGuid())] };
        var report = DeckAdviceEngine.Build(snap, "x", [], Now);
        var need = Assert.Single(report.Needs); Assert.Equal(DeckZone.Mainboard, need.Zone); Assert.Equal(2, need.Missing);
        Assert.Throws<ArgumentException>(() => DeckAdviceEngine.Build(snap, "x", [new(missing.Id, DeckZone.Mainboard, candidate.Id, 3)], Now));
        Assert.Throws<ArgumentException>(() => DeckAdviceEngine.Build(snap, "x", [new(missing.Id, DeckZone.Sideboard, candidate.Id, 1)], Now));
    }
    [Theory]
    [InlineData("manual")]
    [InlineData("unknown")]
    [InlineData("banned")]
    [InlineData("inactive")]
    [InlineData("official")]
    [InlineData("land-face")]
    [InlineData("exception")]
    public void UnprovenIllegalOrUnsupportedCandidatesAreExcluded(string variation)
    {
        var missing = Card("Missing"); var candidate = Card("Candidate");
        candidate = variation switch
        {
            "manual" => candidate with { OracleId = null }, "unknown" => candidate with { Standard = CardLegality.Unknown },
            "banned" => candidate with { Standard = CardLegality.Banned }, "inactive" => candidate with { AvailableOnArena = false },
            "official" => candidate with { Name = "Leyline of Resonance" }, "land-face" => candidate with { TypeLine = "Instant // Land" },
            _ => candidate with { OracleText = "A deck can have unusual restrictions." }
        };
        Assert.Empty(DeckAdviceEngine.Candidates(Snapshot(missing, candidate), "x", [], missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void KnownCopyExceptionsAreHonoredAndStaleSourcesBlockAdvice()
    {
        var missing = Card("Missing"); var candidate = Card("Candidate") with { OracleText = "A deck can have up to seven cards named Candidate." };
        var snap = Snapshot(missing, candidate);
        snap = snap with { Lines = [.. snap.Lines, new(candidate.Id, DeckZone.Sideboard, 4)],
            Stock = snap.Stock.Select(x => x.PrintingId == candidate.Id ? x with { Quantity = 8 } : x).ToArray() };
        Assert.Equal(3, Assert.Single(DeckAdviceEngine.Candidates(snap, "x", [], missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken)).Available);
        var stale = snap with { Source = snap.Source with { SourceUpdatedUtc = Now.AddDays(-2) } };
        Assert.False(DeckAdviceEngine.Build(stale, "x", [], Now).Baseline.Passed);
        Assert.Empty(DeckAdviceEngine.Candidates(stale, "x", [], missing.Id, DeckZone.Mainboard, Now, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => DeckAdviceEngine.Build(stale, "x", [new(missing.Id, DeckZone.Mainboard, candidate.Id, 1)], Now));
    }
}
