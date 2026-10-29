using System.Text.Json;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class PhaseNineEvaluationTests
{
    [Fact]
    public void NewlyReservedCardsReportBaselineMissesAndPreserveCriticalConditions()
    {
        var data = JsonSerializer.Deserialize<Evaluation>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "role-evaluation-v2.json")))!;
        var baseline = JsonSerializer.Deserialize<Baseline>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "role-evaluation-v2-baseline.json")))!;
        Assert.Equal("Oracle roles v2 (2026-10-22)", baseline.Classifier); Assert.True(baseline.CapturedBeforeImplementation);
        var rows = new List<object>(); var full = 0; var partial = 0; var unknown = 0; var wrong = 0; var misses = 0;
        var baselineMisses = 0; var baselineWrong = 0; var positive = 0;
        foreach (var card in data.Cases)
        {
            var previous = Assert.Single(baseline.Cases, x => x.OracleId == card.OracleId);
            var print = new AdvicePrinting(Guid.Parse(card.OracleId), card.OracleId, card.Name, "REF", "1", card.Colors,
                card.ManaValue, card.TypeLine, card.OracleText, CardLegality.Legal, true, card.ManaCost);
            var result = CardRoleClassifier.Analyze(print);
            var roles = result.Roles.Select(x => x.ToString()).ToArray();
            if (result.FullyRecognized) full++; else if (result.Known) partial++; else unknown++;
            if (roles.Any(x => !card.AllowedRoles.Contains(x))) wrong++;
            if (previous.Roles.Any(x => !card.AllowedRoles.Contains(x))) baselineWrong++;
            if (card.AllowedRoles.Length > 0) { positive++; if (!result.Known) misses++; if (!previous.Known) baselineMisses++; }
            var missing = result.Known ? card.RequiredTerms.Where(x => !result.Details.Contains(x, StringComparison.OrdinalIgnoreCase)).ToArray() : [];
            Assert.Empty(missing);
            if (result.IsModal) { Assert.False(result.Complete); Assert.NotEmpty(result.Options!); }
            if (result.Known && (!result.Complete || result.Effect == "damage"))
                Assert.NotEqual(RoleFit.Matching, CardRoleClassifier.Compare(print, print with { Id = Guid.NewGuid() }).Fit);
            rows.Add(new { card.Name, card.Stratum, card.AllowedRoles, Roles = roles, result.FullyRecognized, result.Known,
                result.RuleId, result.Evidence, result.UnparsedText, result.Options, MissingTerms = missing, Baseline = previous, card.ReviewNote });
        }
        var report = new { data.DatasetVersion, Classifier = CardRoleClassifier.Version, Cases = data.Cases.Length,
            Positives = positive, Negatives = data.Cases.Length - positive, FullyRecognized = full, PartialEvidence = partial,
            Unknown = unknown, WrongRoleClaims = wrong, PositiveMisses = misses, RoleEvidenceRecall = (positive-misses)/(double)positive,
            Baseline = new { baseline.Classifier, FullyRecognized = baseline.Cases.Count(x=>x.FullyRecognized),
                PartialEvidence = baseline.Cases.Count(x=>x.Known && !x.FullyRecognized), Unknown = baseline.Cases.Count(x=>!x.Known),
                WrongRoleClaims = baselineWrong, PositiveMisses = baselineMisses, RoleEvidenceRecall = (positive-baselineMisses)/(double)positive },
            Limitation = data.Labeling, Rows = rows };
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(Directory.GetParent(directory)!.FullName, "phase9-reserved-evaluation.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Equal(30, data.Cases.Length); Assert.Equal(30, baseline.Cases.Length); Assert.Equal(0, wrong); Assert.Equal(0, baselineWrong);
        Assert.Equal(data.Cases.Length, full + partial + unknown);
    }
    private sealed record Evaluation(string DatasetVersion, string Labeling, Card[] Cases);
    private sealed record Card(string Name, string OracleId, string TypeLine, string OracleText, string ManaCost,
        string Colors, double ManaValue, string[] AllowedRoles, string[] RequiredTerms, string ReviewNote, string Stratum);
    private sealed record Baseline(string Classifier, bool CapturedBeforeImplementation, BaselineCard[] Cases);
    private sealed record BaselineCard(string Name, string OracleId, bool FullyRecognized, bool Known, string[] Roles);
}
