using System.Text.RegularExpressions;

namespace MTGDeckbuilder.Application;

/// <summary>Accounts for the whole text, or retains all remainder as explicitly incomplete evidence.</summary>
internal static class RoleTextInterpreter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static readonly string[] Numbers = ["a", "one", "two", "three", "four", "five", "six", "seven"];
    private static readonly string[] Keywords = ["Flying", "Vigilance", "Deathtouch", "Haste", "Defender", "Reach", "Trample", "Lifelink", "Menace", "First strike", "Double strike", "Hexproof", "Indestructible"];
    private const string Count = "(a|one|two|three|four|five|six|seven)";
    private const string Target = "(creature|artifact|enchantment|planeswalker|creature or enchantment|creature or artifact|creature or Vehicle|creature or Spacecraft|artifact or creature|artifact or enchantment|creature or planeswalker|nonland permanent|permanent)";

    public static CardRoleAnalysis Analyze(AdvicePrinting card)
    {
        var exact = CardRoleClassifier.AnalyzeExact(card);
        if (card.TypeLine?.Contains("//", StringComparison.Ordinal) == true || string.IsNullOrWhiteSpace(card.OracleText) || card.OracleText.Length > 8000)
            return exact;
        var text = Normalize(card.OracleText);
        var modal = ModalRoleInterpreter.Analyze(card, clause => Analyze(card with { OracleText = clause }));
        if (modal is not null) return modal;
        if (text.StartsWith("Choose ", StringComparison.Ordinal) || card.OracleText.Contains("\n•", StringComparison.Ordinal))
            return exact with { Explanation = "Modale keuzestructuur niet ondersteund of geen ondersteunde optie; geen onvoorwaardelijke rol afleiden." };
        if (exact.Known) return exact with { Evidence = text };
        var whole = Whole(card, text);
        if (whole is not null) return whole with { Evidence = text };

        // Only sentence/line starts. Never scan for a keyword inside a quotation or a reminder paragraph.
        foreach (var line in card.OracleText.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var normalized = Normalize(line);
            var fragment = Regex.Match(normalized,
                @"\A(?<clause>(?:Counter target |Destroy target |Exile target |Return target |Draw |Target player draws |Search your library for |\{T\}: Add |(?:\{[0-9WUBRGC]+\})+: Draw |" + Regex.Escape(card.Name) + @" deals |This spell deals ).+?\.)(?:\s|$)", RegexOptions.CultureInvariant, Timeout);
            var evidence = fragment.Success ? fragment.Groups["clause"].Value : normalized;
            var analysis = Whole(card, evidence);
            if (analysis is null) continue;
            var at = text.IndexOf(evidence, StringComparison.Ordinal);
            if (at < 0) continue;
            var remainder = (text[..at] + " " + text[(at + evidence.Length)..]).Trim();
            return analysis with { Complete = false, Evidence = evidence, UnparsedText = remainder,
                RuleId = "evidence/" + analysis.RuleId,
                Explanation = "Alleen deze clausule is herkend. De rest kan voorwaarden, kosten of andere effecten bevatten; de volledige kaartwerking is niet bewezen. " + analysis.Explanation };
        }
        return exact with { Explanation = "Geen ondersteunde volledige tekst of zelfstandig herkende clausule; kaartwerking blijft onbekend." };
    }
    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant, Timeout).Trim();
    private static Match Match(string text, string pattern) => Regex.Match(text, @"\A" + pattern + @"\z", RegexOptions.CultureInvariant, Timeout);
    private static int Amount(string value) => Math.Max(1, Array.IndexOf(Numbers, value));
    private static CardRoleAnalysis? Whole(AdvicePrinting card, string text)
    {
        var timing = card.TypeLine?.Split('—')[0].Trim() ?? "onbekend";
        var spell = timing is "Instant" or "Sorcery";
        CardRoleAnalysis Result(string rule, CardRole role, string target, string effect, string restriction, int amount, string explanation,
            string mechanism = "spell", string other = "") => new(CardRoleClassifier.Version, rule, role, target, effect, restriction, amount, timing,
                string.IsNullOrWhiteSpace(card.ManaCost) ? "onbekend" : card.ManaCost, explanation, Mechanism: mechanism, OtherEffects: other);
        if (spell)
        {
            var simple = CardRoleClassifier.AnalyzeExact(card with { OracleText = text });
            if (simple.Known) return simple;
            var damage = Damage(card, text);
            if (damage is not null) return damage;
            var colored = Match(text, @"Counter target (?<colors>(?:white|blue|black|red|green)(?: or (?:white|blue|black|red|green))?) spell\.");
            if (colored.Success) return Result("counter.colors.v2", CardRole.Counterspell, colored.Groups["colors"].Value + " spell", "counter", "color restriction", 1,
                "Alleen spells met genoemde kleuren: " + colored.Groups["colors"].Value + ".");
            var counterValue = Match(text, @"Counter target spell with mana value (?<value>[1-9])(?<range> or less)?\.");
            if (counterValue.Success) return Result("counter.value.v2", CardRole.Counterspell, "spell", "counter",
                "mana value " + counterValue.Groups["value"].Value + counterValue.Groups["range"].Value, 1, "Beperkt tot de genoemde manawaarde: " + text);

            var removal = Match(text, @"(?<action>Destroy|Exile) target (?<qualifier>attacking |tapped |untapped |nonartifact |nonblack |(?:green or white|red or green) )?" + Target +
                @"(?<condition> (?:an opponent controls|with mana value [1-9] or less))?\.");
            if (removal.Success)
            {
                // The unnamed target group has index 1; named groups are numbered after unnamed groups.
                var target = removal.Groups[1].Value;
                return Result("removal.qualified.v2", CardRole.Removal, target, removal.Groups["action"].Value.ToLowerInvariant(),
                    (removal.Groups["qualifier"].Value + removal.Groups["condition"].Value).Trim(), 1, "Doel en alle beperkingen in deze tekst: " + text);
            }
            var bounce = Match(text, @"Return target " + Target + @"(?<condition> (?:an opponent controls|with mana value [1-9] or less))? to its owner's hand\.");
            if (bounce.Success) return Result("removal.return.v2", CardRole.Removal, bounce.Groups[1].Value, "bounce", bounce.Groups["condition"].Value.Trim(), 1,
                "Terug naar de hand met genoemde beperkingen: " + text);

            var loot = Match(text, "Draw " + Count + @" cards?, then discard " + Count + @" cards?\.");
            if (loot.Success) return Result("draw.discard.v2", CardRole.Draw, "you", "draw", "", Amount(loot.Groups[1].Value),
                "Kaarten trekken met verplicht discard-effect; dit is geen zuivere kaartwinst.", other: "discard " + Amount(loot.Groups[2].Value));
            var discardFirst = Match(text, "Discard " + Count + @" cards?, then draw " + Count + @" cards?\.");
            if (discardFirst.Success) return Result("draw.discard-first.v2", CardRole.Draw, "you", "draw", "", Amount(discardFirst.Groups[2].Value),
                "Eerst een discard-effect, daarna kaarten trekken; geen zuivere kaartwinst.", other: "discard first " + Amount(discardFirst.Groups[1].Value));
            var blood = Match(text, "Target player draws " + Count + @" cards? and loses (?<life>[1-9]) life\.");
            if (blood.Success) return Result("draw.life-loss.v2", CardRole.Draw, "player", "draw", "", Amount(blood.Groups[1].Value),
                "Gekozen speler trekt kaarten en verliest leven.", other: "target loses " + blood.Groups["life"].Value + " life");

            var selection = Match(text, @"(?<selection>Scry|Surveil) (?<amount>[1-3])\. (?<rest>.+)");
            if (selection.Success)
            {
                var rest = selection.Groups["rest"].Value;
                var reminder = selection.Groups["selection"].Value == "Scry"
                    ? "(Look at the top card of your library. You may put that card on the bottom.) "
                    : "(Look at the top card of your library. You may put it into your graveyard.) ";
                if (selection.Groups["amount"].Value == "1" && rest.StartsWith(reminder, StringComparison.Ordinal)) rest = rest[reminder.Length..];
                var after = CardRoleClassifier.AnalyzeExact(card with { OracleText = rest });
                if (after.Role == CardRole.Draw) return after with { RuleId = "draw.selection.v2",
                    OtherEffects = selection.Groups["selection"].Value + " " + selection.Groups["amount"].Value,
                    Explanation = "Kaartselectie gevolgd door trekken; de volgorde en extra selectie zijn bewaard." };
            }
            // A primary supported clause plus a fully supported supplement. Unknown remainder never disappears.
            var compound = Match(text, @"(?<first>.+?\.) (?<rest>.+)");
            if (compound.Success)
            {
                var first = CardRoleClassifier.AnalyzeExact(card with { OracleText = compound.Groups["first"].Value });
                if (!first.Known) first = Damage(card, compound.Groups["first"].Value) ?? first;
                var rest = compound.Groups["rest"].Value;
                var supplement = Supplement(rest);
                if (first.Known && supplement is not null) return first with { RuleId = "compound/" + first.RuleId,
                    OtherEffects = supplement, Explanation = "De primaire rol en het bijkomende effect zijn herkend. " + first.Explanation };
            }
            if (text == "Search your library for up to two basic land cards, reveal those cards, put one onto the battlefield tapped and the other into your hand, then shuffle.")
                return Result("mana.land-and-hand.v2", CardRole.ManaAcceleration, "basic land", "land", "tapped", 1,
                    "Maximaal twee basic lands: één getapt op battlefield, één naar de hand.", other: "up to one basic land to hand");
            return null;
        }

        if (timing.Contains("Land", StringComparison.Ordinal)) return null;
        var lines = card.OracleText!.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var keywords = lines.TakeWhile(x => x.Split(", ").All(Keywords.Contains)).ToArray();
        var body = text;
        if (keywords.Length > 0 && text.StartsWith(string.Join(" ", keywords) + " ", StringComparison.Ordinal)) body = text[(string.Join(" ", keywords).Length + 1)..];
        var manaAbility = Match(body, @"\{T\}: Add (?<mana>(?:\{[WUBRGC]\}){1,3})\.");
        if (manaAbility.Success)
            return Result("mana.tap-ability.v2", CardRole.ManaAcceleration, "mana pool", manaAbility.Groups["mana"].Value, "tap", manaAbility.Groups["mana"].Value.Count(x => x == '{'),
                "Herhaalbare tap-ability; activatie vereist tappen. Ziekte na oproepen en speltoestand worden niet berekend.", "tap-ability", string.Join(", ", keywords));
        var drawAbility = Match(body, @"(?<cost>(?:\{[0-9WUBRGC]+\})+): Draw " + Count + @" cards?\.");
        if (drawAbility.Success)
            return Result("draw.mana-ability.v2", CardRole.Draw, "you", "draw", "activation " + drawAbility.Groups["cost"].Value,
                Amount(drawAbility.Groups[1].Value), "Geactiveerd kaarten trekken met expliciete activatiekosten: " + drawAbility.Groups["cost"].Value,
                "activated", string.Join(", ", keywords));
        var enter = Match(body, @"When this creature (?<event>enters(?: the battlefield)?|dies), draw " + Count + @" cards?\.");
        if (enter.Success)
            return Result("draw.event.v2", CardRole.Draw, "you", "draw", "", Amount(enter.Groups[1].Value),
                "Trekken gebeurt via een trigger: " + enter.Groups["event"].Value + "; geen losse trekspreuk.",
                enter.Groups["event"].Value.StartsWith("enters", StringComparison.Ordinal) ? "enters-trigger" : "dies-trigger", string.Join(", ", keywords));
        return null;
    }
    private static CardRoleAnalysis? Damage(AdvicePrinting card, string text)
    {
        var match = Match(text, "(?:" + Regex.Escape(card.Name) + @"|This spell) deals (?<amount>[1-9]) damage to (?:(?<any>any target)|target (?<qualifier>attacking |tapped |untapped )?(?<target>creature or planeswalker|creature|planeswalker)(?<controller> (?:an opponent controls|you don't control))?)\.");
        if (!match.Success) return null;
        var target = match.Groups["any"].Success ? "any target" : match.Groups["target"].Value;
        return new(CardRoleClassifier.Version, "removal.damage.v3", CardRole.Removal, target, "damage",
            (match.Groups["qualifier"].Value + match.Groups["controller"].Value).Trim(), int.Parse(match.Groups["amount"].Value),
            card.TypeLine!.Split('—')[0].Trim(), string.IsNullOrWhiteSpace(card.ManaCost) ? "onbekend" : card.ManaCost,
            "Vaste gerichte schade: " + match.Groups["amount"].Value + " aan " + target + ". Dodelijkheid is niet bewezen: toughness en speltoestand ontbreken. Geen gegarandeerde destroy/exile-removal.");
    }
    private static string? Supplement(string text)
    {
        var draw = Match(text, "Draw " + Count + @" cards?\.");
        if (draw.Success) return "draw " + Amount(draw.Groups[1].Value);
        var loot = Match(text, "Draw " + Count + @" cards?, then discard " + Count + @" cards?\.");
        if (loot.Success) return "draw " + Amount(loot.Groups[1].Value) + ", discard " + Amount(loot.Groups[2].Value);
        var life = Match(text, @"You (?<direction>gain|lose) (?<amount>[1-9]) life\.");
        if (life.Success) return "you " + life.Groups["direction"].Value + " " + life.Groups["amount"].Value + " life";
        var selection = Match(text, @"(?<action>Scry|Surveil) (?<amount>[1-3])\.");
        return selection.Success ? selection.Groups["action"].Value + " " + selection.Groups["amount"].Value : null;
    }
}
