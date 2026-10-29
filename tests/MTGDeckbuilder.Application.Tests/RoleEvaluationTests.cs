using System.Text.Json;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class RoleEvaluationTests
{
    [Fact]
    public void PreviousEvaluationIsRegressionMaterialAndRetainsItsSafetyChecks()
    {
        var reference = JsonSerializer.Deserialize<Evaluation>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "role-evaluation-v1.json")))!;
        var rows = new List<object>(); var full = 0; var partial = 0; var wrong = 0; var misses = 0; var positive = 0; var unknown = 0;
        foreach (var card in reference.Cases)
        {
            var print = new AdvicePrinting(Guid.Parse(card.OracleId), card.OracleId, card.Name, "REF", "1", card.Colors,
                card.ManaValue, card.TypeLine, card.OracleText, CardLegality.Legal, true, card.ManaCost);
            var result = CardRoleClassifier.Analyze(print);
            if (card.AllowedRoles.Length > 0) positive++;
            if (result.FullyRecognized) full++; else if (result.Known) partial++; else unknown++;
            if (result.Roles.Any(role => !card.AllowedRoles.Contains(role.ToString()))) wrong++;
            if (!result.Known && card.AllowedRoles.Length > 0) misses++;
            var missingTerms = result.Known ? card.RequiredTerms.Where(x => !result.Details.Contains(x, StringComparison.OrdinalIgnoreCase)).ToArray() : [];
            rows.Add(new { card.Name, card.Stratum, card.AllowedRoles, Role = result.Role.ToString(), result.Complete,
                Roles = result.Roles.Select(x => x.ToString()), result.RuleId, result.Evidence, result.UnparsedText, MissingTerms = missingTerms, card.ReviewNote });
            Assert.Empty(missingTerms);
            if (result.Known && !result.Complete)
            {
                var comparison = CardRoleClassifier.Compare(print, print with { Id = Guid.NewGuid() });
                Assert.NotEqual(RoleFit.Matching, comparison.Fit); Assert.False(result.FullyRecognized);
                Assert.False(string.IsNullOrWhiteSpace(result.UnparsedText));
            }
        }
        var report = new { reference.DatasetVersion, Classifier = CardRoleClassifier.Version, Cases = reference.Cases.Length,
            Positives = positive, Negatives = reference.Cases.Length - positive, FullyRecognized = full, PartialEvidence = partial,
            Unknown = unknown, WrongRoleClaims = wrong, PositiveMisses = misses,
            RoleEvidenceRecall = positive == 0 ? (double?)null : (positive - misses) / (double)positive,
            Limitation = "Previous phase8 evaluation is now regression material, not a new unseen evaluation. Original labeling: " + reference.Labeling, Rows = rows };
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(Directory.GetParent(directory)!.FullName, "phase9-previous-evaluation-regression.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Equal(30, reference.Cases.Length); Assert.Equal(0, wrong);
        Assert.Equal(reference.Cases.Length, full + partial + unknown);
    }
    private sealed record Evaluation(string DatasetVersion, string Labeling, Case[] Cases);
    private sealed record Case(string Name, string TypeLine, string OracleText, string ManaCost, string Colors, double ManaValue,
        string OracleId, string[] AllowedRoles, string[] RequiredTerms, string ReviewNote, string Stratum);
}
