using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace MTGDeckbuilder.Application;

public enum SynergyFeature
{
    Creature, Artifact, Equipment, AttachableEquipment, InstantSorcery, NoncreatureNonland, Land,
    Evasion, Defender, Shroud, TeamShroud, TeamBuff, UnblockableSupport,
    ProtectionWhite, ProtectionBlue, ProtectionBlack, ProtectionRed, ProtectionGreen, ProtectionArtifacts, ProtectionCreatures, ProtectionEverything,
    TargetingUncertain, SoloAttackBlocked,
    Modal, SelfDiscard, SelfCycling, SelfCreatureSacrifice, SelfEnchantmentSacrifice,
    OwnCreatureDeathPayoff, OwnArtifactDeathPayoff, OpponentCreatureSacrifice,
    GraveyardPermanentPayoff, NamedGraveyardRetrieval, ArtifactAffinity,
    ArtifactCastPayoff, ArtifactInstantSorceryCastPayoff, CastFromExilePayoff, ExileCardAccess,
    AttackCreaturePayoff, NeedsNonGnomeAttacker, TokensEnterAttacking, RequiresLifeGainThree, OwnLifeGainThree,
    OwnEnchantmentTokens, OwnUnknownTokens, InstantSorceryCastPayoff, EquippedDeathPayoff, CreatureFromHand,
    Convoke, PermanentRecursion, RestrictedBasicLandSearch,
    TypedCardRecursion, DelayedCreatureSacrifice, SymmetricCreatureSacrifice, ArtifactCreatureSacrifice,
    OwnCreatureEnterPayoff, OwnArtifactEnterPayoff, NonTokenEnterOnly,
    NonTokenDeathOnly,
    SelfCreatureDeathPayoff,
    EquipmentCountPayoff, EquippedCreaturePayoff, EquipmentDiscount, EquipmentMana, AttachEquipment,
    OwnCreatureTokens, OwnArtifactTokens, ConditionalCopyToken, JobSelect, TreasureOnDeath,
    OwnCreatureSacrifice, OwnArtifactSacrifice, OwnLandSacrifice, SelfArtifactSacrifice, SelfLandSacrifice, NonTokenSacrificeOnly,
    OwnDeathPayoff, OtherOwnDeathPayoff, OpponentDeathPayoff, DeathDraw,
    OwnDraw, OpponentDraw, DrawPayoff, SelfMill, Surveil, Loot,
    CreatureRecursion, SelfRecursion, Flashback, GraveyardSpellPayoff, GraveyardHate,
    DomainPayoff, BasicLandSearch, PlainsSearch, FourTypeSearch, Exalted,
    DestroyRemoval, ConditionalToughnessRemoval, ExileRemoval, DeathToExile,
    PowerFivePayoff, GreenPermanentCondition, RedPermanentCondition, NoncreatureCastPayoff, NeedsFourManaSpent, AttackDrawPayoff
}

public sealed record SynergyProfile(PaperCard Card, IReadOnlySet<SynergyFeature> Features,
    string Origin, bool Reviewed, bool Changed, string Notes)
{
    public bool Has(SynergyFeature feature) => Features.Contains(feature);
    public bool Any(params SynergyFeature[] features) => features.Any(Has);
    public bool Theme(string theme) => theme switch
    {
        "any" => true,
        "equipment" => Any(SynergyFeature.Equipment, SynergyFeature.EquipmentCountPayoff, SynergyFeature.EquippedCreaturePayoff,
            SynergyFeature.EquipmentDiscount, SynergyFeature.EquipmentMana, SynergyFeature.AttachEquipment),
        "tokens" => Any(SynergyFeature.OwnCreatureTokens, SynergyFeature.OwnArtifactTokens,SynergyFeature.OwnEnchantmentTokens,SynergyFeature.OwnUnknownTokens, SynergyFeature.ConditionalCopyToken, SynergyFeature.TeamBuff,SynergyFeature.Convoke,SynergyFeature.OwnCreatureEnterPayoff,SynergyFeature.OwnArtifactEnterPayoff),
        "graveyard" => Any(SynergyFeature.SelfMill, SynergyFeature.Surveil, SynergyFeature.Loot, SynergyFeature.SelfDiscard,SynergyFeature.SelfCycling, SynergyFeature.CreatureRecursion,
            SynergyFeature.SelfRecursion, SynergyFeature.Flashback, SynergyFeature.GraveyardSpellPayoff,SynergyFeature.GraveyardPermanentPayoff,SynergyFeature.NamedGraveyardRetrieval,SynergyFeature.PermanentRecursion,SynergyFeature.TypedCardRecursion),
        "sacrifice" => Any(SynergyFeature.OwnCreatureSacrifice, SynergyFeature.OwnArtifactSacrifice, SynergyFeature.OwnLandSacrifice, SynergyFeature.SelfArtifactSacrifice, SynergyFeature.SelfLandSacrifice,
            SynergyFeature.OwnDeathPayoff, SynergyFeature.OtherOwnDeathPayoff, SynergyFeature.TreasureOnDeath,SynergyFeature.OwnCreatureDeathPayoff,SynergyFeature.OwnArtifactDeathPayoff,
            SynergyFeature.SelfCreatureSacrifice,SynergyFeature.SelfEnchantmentSacrifice,SynergyFeature.EquippedDeathPayoff,SynergyFeature.DelayedCreatureSacrifice,SynergyFeature.SymmetricCreatureSacrifice,SynergyFeature.ArtifactCreatureSacrifice,SynergyFeature.SelfCreatureDeathPayoff),
        "opponent-death" => Any(SynergyFeature.OpponentDeathPayoff,SynergyFeature.OpponentCreatureSacrifice,SynergyFeature.DestroyRemoval,SynergyFeature.ConditionalToughnessRemoval),
        "domain" => Has(SynergyFeature.DomainPayoff),
        "exalted" => Has(SynergyFeature.Exalted),
        "draw" => Any(SynergyFeature.OwnDraw, SynergyFeature.Loot, SynergyFeature.DeathDraw, SynergyFeature.AttackDrawPayoff,SynergyFeature.DrawPayoff),
        _ => throw new ArgumentException("Onbekend thema.")
    };
    public string Status => Card.RequiresUnsupportedRules?"Afwijkende spelregels: automatische analyse niet ondersteund":SynergyProfiles.ComplexCard(Card)?"Meerdere kanten/modi: synergie per kant nog niet beoordeeld":Changed ? "Gecontroleerd profiel gewijzigd; inhoud opnieuw beoordelen" : Reviewed
        ? "Gecontroleerd productprofiel voor ondersteunde relaties" : Features.Any(SynergyProfiles.IsSemantic)
        ? "Beperkte tekstherkenning; rest van de kaart niet beoordeeld" : "Alleen kaarttypefeiten; kaartwerking niet herkend";
    public string Details => Status + "\n" + (SynergyProfiles.AutomaticSelectionIssue(Card) is {} issue?"Automatische selectie uitgesloten: "+issue+"\n":"") + (Notes.Length == 0 ? "Geen aanvullende gecontroleerde interactie." : Notes) +
        "\nDit profiel beschrijft ondersteunde relaties en voorwaarden, geen volledige spelregelsimulatie.";
}

/// <summary>Reviewed product facts require an exact gameplay fingerprint. Other cards use narrow, conservative patterns.</summary>
public static partial class SynergyProfiles
{
    public sealed record Entry(string OracleId, string Name, string Fingerprint, SynergyFeature[] Features, string Notes);
    private sealed record Catalog(string Version, string ReviewedUtc, Entry[] Cards);
    private static readonly Lazy<IReadOnlyDictionary<string, Entry>> Known = new(() =>
    {
        using var stream = typeof(SynergyProfiles).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources.paper-synergy-v1.json")!;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<Catalog>(stream, options)!.Cards.ToDictionary(x => x.OracleId, StringComparer.Ordinal);
    });
    public const string Version = "Physical synergy v7 · 2026-10-28";
    public static bool ComplexCard(PaperCard card)=>card.TypeLine.Contains("//",StringComparison.Ordinal)||card.OracleText.Contains("\n//\n",StringComparison.Ordinal)||card.ManaCost.Contains("//",StringComparison.Ordinal);
    public static string? AutomaticSelectionIssue(PaperCard card)=>card.RequiresUnsupportedRules?"Deze kaart vraagt een niet-ondersteund niveau- of regelmodel (level up/acorn/silver-border/host-augment).":ComplexCard(card)?"Meerdere kanten/Adventure/split vragen analyse per kant.":
        Regex.IsMatch(RemoveParentheses(card.OracleText),@"\b(skip your next(?:(?: one| two| three| [0-9]+))? turns?|exile (?:all (?:the )?)?cards (?:in|from) your library|exile your library|if you haven't completed)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
        ?"Een zware eigen beurt-/librarykost of ontbrekende dungeon-voorwaarde wordt niet veilig op deckniveau beoordeeld.":
        Regex.IsMatch(RemoveParentheses(card.OracleText),@"\b(you (can't|cannot) win|your opponents? (can't|cannot) lose|you lose the game)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
        ?"Expliciete eigen verliesvoorwaarde of blokkade op winnen; een veilige uitweg wordt niet beoordeeld.":null;
    public static string Fingerprint(PaperCard card) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    { card.OracleId, card.Name, card.TypeLine, card.OracleText, card.ManaCost, card.ManaValue, card.Colors, card.PrintedColors, card.Power, card.Toughness })));
    public static bool IsSemantic(SynergyFeature feature) => feature is not (SynergyFeature.Creature or SynergyFeature.Artifact or
        SynergyFeature.Equipment or SynergyFeature.AttachableEquipment or SynergyFeature.InstantSorcery or SynergyFeature.NoncreatureNonland or SynergyFeature.Land);
    private sealed record Facts(IReadOnlySet<SynergyFeature> Features,string Origin,bool Reviewed,bool Changed,string Notes);
    private static readonly ConcurrentDictionary<string,Lazy<Facts>> FactsCache=new(StringComparer.Ordinal);
    public static SynergyProfile Analyze(PaperCard card)
    {
        var key=Fingerprint(card)+(card.RequiresUnsupportedRules?"/unsupported":"/ordinary");
        Facts Make(){var p=AnalyzeCore(card);return new(p.Features.ToFrozenSet(),p.Origin,p.Reviewed,p.Changed,p.Notes);}
        var facts=FactsCache.TryGetValue(key,out var cached)?cached.Value:FactsCache.Count<65_536
            ?FactsCache.GetOrAdd(key,_=>new Lazy<Facts>(Make,LazyThreadSafetyMode.ExecutionAndPublication)).Value:Make();
        // Cache gameplay facts only: the caller keeps its own printing, freshness and image metadata.
        return new(card,facts.Features,facts.Origin,facts.Reviewed,facts.Changed,facts.Notes);
    }
    private static SynergyProfile AnalyzeCore(PaperCard card)
    {
        if(card.RequiresUnsupportedRules)return new(card,new HashSet<SynergyFeature>(),"Afwijkende spelregels niet ondersteund",false,false,"Acorn/silver-border/host-augment vraagt eigen beoordeling. Geen automatische selectie of positieve relatie toegekend.");
        if(ComplexCard(card))return new(card,new HashSet<SynergyFeature>(),"Meerdere kanten/modi niet samengevoegd",false,false,
            "Deze kaart heeft meerdere kanten, een Adventure of gescheiden modi. Kosten, typen en effecten worden hier niet samengevoegd. Geen automatische selectie of positieve kaartrelaties totdat analyse per kant wordt ondersteund; handmatig bekijken blijft mogelijk.");
        var tags = new HashSet<SynergyFeature>();
        bool Type(string type) => card.TypeLine.Split([' ', '—', '/'], StringSplitOptions.RemoveEmptyEntries).Contains(type);
        if (Type("Creature")) tags.Add(SynergyFeature.Creature);
        if (Type("Artifact")) tags.Add(SynergyFeature.Artifact);
        if (Type("Equipment")) tags.Add(SynergyFeature.Equipment);
        if (Type("Equipment") && !Type("Creature")) tags.Add(SynergyFeature.AttachableEquipment);
        if (Type("Land")) tags.Add(SynergyFeature.Land);
        if (Type("Instant") || Type("Sorcery")) tags.Add(SynergyFeature.InstantSorcery);
        if (!Type("Land") && !Type("Creature")) tags.Add(SynergyFeature.NoncreatureNonland);
        if (Known.Value.TryGetValue(card.OracleId, out var known))
        {
            if (known.Fingerprint != Fingerprint(card))
                return new(card, tags, "Gewijzigd productprofiel", false, true, "Oracle-identiteit bestaat, maar de gecontroleerde gameplaymetadata wijkt af. Alleen actuele typefeiten gebruikt.");
            tags.UnionWith(known.Features);
            if(Regex.IsMatch(RemoveParentheses(card.OracleText),@"(^|\n)Cycling\s+\{",RegexOptions.IgnoreCase))tags.Add(SynergyFeature.OwnDraw);
            if(tags.Contains(SynergyFeature.TreasureOnDeath)&&tags.Contains(SynergyFeature.Creature))tags.Add(SynergyFeature.SelfCreatureDeathPayoff);
            return new(card, tags, "Productprofiel gecontroleerd op 28 oktober 2026", true, false, known.Notes+(tags.Contains(SynergyFeature.SelfCycling)&&tags.Contains(SynergyFeature.OwnDraw)?" Gewone cycling trekt één kaart na kosten en discard van deze kaart; typecycling zoekt in plaats daarvan.":""));
        }
        var text = RemoveParentheses(card.OracleText).Replace(card.Name, "this card", StringComparison.OrdinalIgnoreCase);
        if(card.TypeLine.Contains("Legendary",StringComparison.Ordinal)&&card.Name.Contains(','))
        {
            var shortName=card.Name.Split(',')[0].Trim();
            if(shortName.Length>1)text=Regex.Replace(text,@"\b"+Regex.Escape(shortName)+@"\b(?=\s+(gets|has|is|enters|attacks|deals|can't|cannot)\b|[.,]|$)","this card",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        }
        if(Type("Creature")&&Regex.IsMatch(text,@"\bthis (card|creature) (can't|cannot) attack\b",RegexOptions.IgnoreCase))tags.Add(SynergyFeature.SoloAttackBlocked);
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if(Regex.IsMatch(line,@"^Domain\s*[—-].*\b(basic land types? among lands you control)\b",RegexOptions.IgnoreCase))tags.Add(SynergyFeature.DomainPayoff);
            // Scope is deliberate: modal bullets, quoted granted abilities, replacement/negation and opponent scope need review.
            if (line.Contains('•') || line.Contains('"') || Negation().IsMatch(line)) continue;
            if (KeywordLine().IsMatch(line))
            {
                if (Standalone("flying|menace|trample|shadow|horsemanship", line)) tags.Add(SynergyFeature.Evasion);
                if (Standalone("shroud", line)) tags.Add(SynergyFeature.Shroud);
                if (Standalone("defender", line)) tags.Add(SynergyFeature.Defender);
                if (Standalone("exalted", line)) tags.Add(SynergyFeature.Exalted);
            }
            var scoped = OpponentScope().IsMatch(line);
            if (scoped) continue;
            if(Regex.IsMatch(line,@"^Protection from (white|blue|black|red|green|artifacts|creatures|everything)\s*\.?$",RegexOptions.IgnoreCase))
            {
                var word=line["Protection from ".Length..].Trim().TrimEnd('.');
                if(Enum.TryParse<SynergyFeature>("Protection"+char.ToUpperInvariant(word[0])+word[1..].ToLowerInvariant(),out var protection))tags.Add(protection);
            }
            if(Type("Equipment")&&!Type("Creature")&&Regex.IsMatch(line,@"^Job select\s*$",RegexOptions.IgnoreCase))
            {tags.Add(SynergyFeature.JobSelect);tags.Add(SynergyFeature.OwnCreatureTokens);}
            if(Type("Creature")&&Regex.IsMatch(line,@"^As long as this (card|creature|commander) is equipped, it has (double strike|indestructible|flying|menace|trample)\.?$",RegexOptions.IgnoreCase))
                tags.Add(SynergyFeature.EquippedCreaturePayoff);
            if (OwnToken().IsMatch(line))
            {
                if (Regex.IsMatch(line, @"\bcreature tokens?\b", RegexOptions.IgnoreCase)) tags.Add(SynergyFeature.OwnCreatureTokens);
                else if (Regex.IsMatch(line, @"\b(Treasure|Food|Clue|Blood|Map) tokens?\b", RegexOptions.IgnoreCase)) tags.Add(SynergyFeature.OwnArtifactTokens);
            }
            if (OwnDraw().IsMatch(line)) tags.Add(SynergyFeature.OwnDraw);
            if (OwnMill().IsMatch(line)) tags.Add(SynergyFeature.SelfMill);
            if (OwnRecursion().IsMatch(line)) tags.Add(SynergyFeature.CreatureRecursion);
            if(Type("Artifact")&&Regex.IsMatch(line,@"(^|,\s*)Sacrifice this artifact\s*:",RegexOptions.IgnoreCase))tags.Add(SynergyFeature.SelfArtifactSacrifice);
            if(Type("Land")&&Regex.IsMatch(line,@"(^|,\s*)Sacrifice this land\s*:",RegexOptions.IgnoreCase))tags.Add(SynergyFeature.SelfLandSacrifice);
            if (Regex.IsMatch(line,@"^Exile all cards from (all|your) graveyards?\.?$",RegexOptions.IgnoreCase)) tags.Add(SynergyFeature.GraveyardHate);
            if (SacrificeCreature().IsMatch(line))
            { tags.Add(SynergyFeature.OwnCreatureSacrifice); if(line.Contains("nontoken",StringComparison.OrdinalIgnoreCase))tags.Add(SynergyFeature.NonTokenSacrificeOnly); }
            if (SacrificeArtifact().IsMatch(line)) tags.Add(SynergyFeature.OwnArtifactSacrifice);
            if (Regex.IsMatch(line, @"^Domain\s*[—-]", RegexOptions.IgnoreCase) && line.Contains("basic land type", StringComparison.OrdinalIgnoreCase) && line.Contains("you control", StringComparison.OrdinalIgnoreCase)) tags.Add(SynergyFeature.DomainPayoff);
            if (Regex.IsMatch(line, @"^Equipped creature (gets|has|gains|is)\b", RegexOptions.IgnoreCase) && Type("Equipment")) { /* Equipment fact already recorded. */ }
        }
        var textFacts=SynergyTextRules.Analyze(card,text);tags.UnionWith(textFacts.Features);
        if(Type("Creature"))
        {
            var unknownProtection=text.Contains("protection",StringComparison.OrdinalIgnoreCase)&&!tags.Any(f=>f.ToString().StartsWith("Protection",StringComparison.Ordinal));
            var unknownShroud=text.Contains("shroud",StringComparison.OrdinalIgnoreCase)&&!tags.Contains(SynergyFeature.Shroud);
            if(unknownProtection||unknownShroud||Regex.IsMatch(text,@"\b(can't|cannot) be (the )?(target|equipped|enchanted)",RegexOptions.IgnoreCase))tags.Add(SynergyFeature.TargetingUncertain);
        }
        var evidence=string.Join("\n",textFacts.Evidence);
        return new(card, tags, "Begrensde algemene tekstherkenning", false, false, "Beperkt herkende effecten; overige kaartwerking blijft onbeoordeeld. "+
            (evidence.Length>0?"Bronfragmenten en voorwaarden:\n"+evidence:"Voorwaarden en kaarttekst kunnen buiten de herkenning vallen."));
    }
    private static bool Standalone(string words, string text) => Regex.IsMatch(text, @"\b(" + words + @")\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static string RemoveParentheses(string text)
    {
        var result = new System.Text.StringBuilder(); var depth = 0;
        foreach (var c in text) { if (c == '(') { depth++; continue; } if (c == ')' && depth > 0) { depth--; continue; } if (depth == 0) result.Append(c); }
        return result.ToString();
    }
    [GeneratedRegex(@"\b(can't|cannot|don't|doesn't|instead|would|unless|rather than|without)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Negation();
    [GeneratedRegex(@"\b(opponent|opponents|each player|target player|its controller|their controller|that player)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex OpponentScope();
    [GeneratedRegex(@"^(Flying|Menace|Trample|Shadow|Horsemanship|Shroud|Exalted|Vigilance|Lifelink|Reach|First strike|Double strike|Haste|Defender|Deathtouch)(\s*,\s*(Flying|Menace|Trample|Shadow|Horsemanship|Shroud|Exalted|Vigilance|Lifelink|Reach|First strike|Double strike|Haste|Defender|Deathtouch))*\s*\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex KeywordLine();
    [GeneratedRegex(@"^(You (may )?create|Create)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex OwnToken();
    [GeneratedRegex(@"^(You (may )?draw|Draw) (a|one|two|three|four|five|X|\d+) cards?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex OwnDraw();
    [GeneratedRegex(@"^(You (may )?mill|Mill) (one|two|three|four|five|X|\d+) cards?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex OwnMill();
    [GeneratedRegex(@"^Return target creature card from your graveyard to (your hand|the battlefield)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex OwnRecursion();
    [GeneratedRegex(@"(^|,\s*)[Ss]acrifice (another |a |an |one or more )?(nontoken )?creature\s*:", RegexOptions.CultureInvariant)] private static partial Regex SacrificeCreature();
    [GeneratedRegex(@"(^|,\s*)[Ss]acrifice (another |a |an |one or more )?artifact\s*:", RegexOptions.CultureInvariant)] private static partial Regex SacrificeArtifact();
}
