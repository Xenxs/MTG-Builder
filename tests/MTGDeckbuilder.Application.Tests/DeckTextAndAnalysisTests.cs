using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckTextAndAnalysisTests
{
    [Fact]
    public void ParserReadsMetadataZonesPrintingSuffixesAndFrontOrSplitNames()
    {
        var parsed = ArenaDeckText.Parse("\ufeffAbout\r\nName Test deck\r\n\r\nCommander\r\n1 Nissa (WAR) 169\r\n\r\nDeck\r\n4 Llanowar Elves (fdn) 227\r\n2 Fire // Ice\r\n\r\nSideboard\r\n1 Card (with parentheses)\r\n");
        Assert.Equal("Test deck", parsed.Name); Assert.Equal(4, parsed.Lines.Count);
        Assert.Equal(DeckZone.Commander, parsed.Lines[0].Zone);
        Assert.Equal("FDN", parsed.Lines[1].SetCode);
        Assert.Equal("227", parsed.Lines[1].CollectorNumber);
        Assert.Equal("Fire // Ice", parsed.Lines[2].Name);
        Assert.Equal("Card (with parentheses)", parsed.Lines[3].Name);
        Assert.Equal(DeckZone.Sideboard, parsed.Lines[3].Zone);
    }
    [Fact]
    public void NamesOnlyDefaultToMainboardAndBlankLinesDoNotInventSideboard()
    {
        var deck = ArenaDeckText.Parse("4 Negate\n\n3 Æther Spell");
        Assert.All(deck.Lines, x => Assert.Equal(DeckZone.Mainboard, x.Zone));
        Assert.Equal("Æther Spell", deck.Lines[1].Name);
    }
    [Theory]
    [InlineData("")]
    [InlineData("Deck\n")]
    [InlineData("0 Negate")]
    [InlineData("-1 Negate")]
    [InlineData("1.5 Negate")]
    [InlineData("10000 Negate")]
    [InlineData("Negate")]
    [InlineData("Companion\n1 Lurrus")]
    [InlineData("Maybeboard\n1 Negate")]
    [InlineData("About\nFormat Standard\nDeck\n1 Negate")]
    [InlineData("About\nName A\nName B\nDeck\n1 Negate")]
    public void UnsupportedOrMalformedInputNeverSilentlyDropsLines(string text)
        => Assert.Throws<ArgumentException>(() => ArenaDeckText.Parse(text));
    [Fact]
    public void TextLimitsRejectOversizedOrOverfullSources()
    {
        Assert.Throws<ArgumentException>(() => ArenaDeckText.Parse(new string('x', 262145)));
        Assert.Throws<ArgumentException>(() => ArenaDeckText.Parse("1 " + new string('x', 257)));
        Assert.Throws<ArgumentException>(() => ArenaDeckText.Parse(string.Join('\n', Enumerable.Repeat("1 Negate", 501))));
        Assert.Throws<ArgumentException>(() => ArenaDeckText.Parse("9999 Negate\n2 Other"));
    }
    [Fact]
    public void DemandAcrossReprintsAndAllZonesConsumesOwnershipOnlyOnce()
    {
        var oracle = Guid.NewGuid().ToString(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var other = Guid.NewGuid();
        var cards = new AnalysisCard[]
        {
            new(a, oracle, "Negate", DeckZone.Mainboard, 4, "Instant", 2, "U"),
            new(b, oracle, "Negate", DeckZone.Sideboard, 1, "Instant", 2, "U"),
            new(b, oracle, "Negate", DeckZone.Commander, 1, "Instant", 2, "U")
        };
        var analysis = DeckAnalyzer.Analyze(cards, [new(a, oracle, 1), new(b, oracle, 1), new(other, oracle, 2)]);
        var need = Assert.Single(analysis.Needs);
        Assert.Equal(6, need.Required); Assert.Equal(4, need.Owned); Assert.Equal(2, need.Missing); Assert.Equal(4, need.ExactPrintingMissing);
        Assert.Equal(4, analysis.Mainboard); Assert.Equal(1, analysis.Sideboard); Assert.Equal(1, analysis.Commanders);
        Assert.Equal(2, analysis.AverageManaValue);
        Assert.Equal(4, analysis.ManaCurve.Single(x => x.Label == "2–<3").Count);
    }
    [Fact]
    public void ManualNameMatchesDoNotCombineUnverifiedOwnership()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var analysis = DeckAnalyzer.Analyze([new(a, null, "Negate", DeckZone.Mainboard, 4, null, null, "")], [new(b, null, 4)]);
        Assert.Equal(4, analysis.Missing); Assert.Equal(4, analysis.UnknownMetadata);
        Assert.False(Assert.Single(analysis.Needs).VerifiedIdentity); Assert.Null(analysis.AverageManaValue);
        Assert.All(analysis.ManaCurve, x => Assert.Equal(0, x.Count));
    }
    [Fact]
    public void CurveExcludesLandsAndSideboardWhileTypesAndColorsCanOverlap()
    {
        var cards = new AnalysisCard[]
        {
            new(Guid.NewGuid(), "a", "Land // Back", DeckZone.Mainboard, 20, "Basic Land — Island // Sorcery", 0, "U"),
            new(Guid.NewGuid(), "b", "Robot", DeckZone.Mainboard, 4, "Artifact Creature — Golem", 3, "UR"),
            new(Guid.NewGuid(), "c", "Big spell", DeckZone.Mainboard, 2, "Sorcery", 8, ""),
            new(Guid.NewGuid(), "d", "Sideboard spell", DeckZone.Sideboard, 3, "Instant", 1, "U")
        };
        var analysis = DeckAnalyzer.Analyze(cards, []);
        Assert.Equal(20, analysis.Lands); Assert.Equal(6, analysis.Nonlands); Assert.Equal(26, analysis.Mainboard);
        Assert.Equal(6, analysis.ManaCurve.Sum(x => x.Count)); Assert.Equal(2, analysis.ManaCurve.Single(x => x.Label == "7+").Count);
        Assert.Equal(4, analysis.Types.Single(x => x.Label == "Creatures").Count);
        Assert.Equal(4, analysis.Types.Single(x => x.Label == "Artifacts").Count);
        Assert.Equal(24, analysis.Colors.Single(x => x.Label == "Blauw").Count);
        Assert.Equal(4, analysis.Colors.Single(x => x.Label == "Rood").Count);
        Assert.Equal(20, analysis.MultiFaceCards);
        Assert.Equal(28d / 6, analysis.AverageManaValue!.Value, 6);
    }
    [Fact]
    public void EmptyDeckAnalysisIsDefinedWithoutNanOrDivisionByZero()
    {
        var analysis = DeckAnalyzer.Analyze([], []);
        Assert.Equal(0, analysis.Mainboard); Assert.Equal(0, analysis.Missing);
        Assert.Null(analysis.AverageManaValue); Assert.Empty(analysis.Needs);
        Assert.All(analysis.ManaCurve, x => Assert.Equal(0, x.BarWidth));
    }
}
