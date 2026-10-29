namespace MTGDeckbuilder.Domain;

public enum DeckRuleProfile { Unspecified, ArenaStandardBo1, ArenaStandardBo3 }
public enum CardLegality { Unknown, Legal, NotLegal, Banned, Restricted }

public static class DeckRuleProfiles
{
    public const string Bo1 = "Arena Standard BO1";
    public const string Bo3 = "Arena Standard BO3";
    public static DeckRuleProfile FromExplicitLabel(string label) => label switch
    {
        Bo1 => DeckRuleProfile.ArenaStandardBo1, Bo3 => DeckRuleProfile.ArenaStandardBo3,
        _ => DeckRuleProfile.Unspecified
    };
}

// Current legality snapshot; older catalog/import receipts remain available separately.
public sealed class CardFormatLegality
{
    private CardFormatLegality() { }
    public CardFormatLegality(Guid cardId, Guid runId, CardLegality standard)
    {
        CardId = Rules.Id(cardId); RunId = Rules.Id(runId);
        if (!Enum.IsDefined(standard)) throw new ArgumentException("Ongeldige legaliteitsstatus.");
        Standard = standard;
    }
    public Guid CardId { get; private set; }
    public Guid RunId { get; private set; }
    public CardLegality Standard { get; private set; }
}
