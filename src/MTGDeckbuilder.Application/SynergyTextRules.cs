using System.Text.RegularExpressions;
using static MTGDeckbuilder.Application.SynergyFeature;

namespace MTGDeckbuilder.Application;

public sealed record SynergyTextFacts(IReadOnlySet<SynergyFeature> Features,IReadOnlyList<string> Evidence);

/// <summary>
/// Recognizes selected Oracle actions, separating trigger/cost from effect beneficiary.
/// It deliberately does not interpret arbitrary English, choices, timing or board state.
/// </summary>
public static partial class SynergyTextRules
{
    public static SynergyTextFacts Analyze(PaperCard card,string normalizedText)
    {
        var tags=new HashSet<SynergyFeature>();var evidence=new List<string>();
        var modal=Regex.IsMatch(normalizedText,@"\bChoose (one|two|three|one or more)\b",RegexOptions.IgnoreCase);
        if(modal)tags.Add(Modal);
        bool Type(string value)=>card.TypeLine.Split([' ','—','/'],StringSplitOptions.RemoveEmptyEntries).Contains(value);
        void Fact(SynergyFeature feature,string clause,string condition)
        {
            tags.Add(feature);
            var note=clause.Trim()+" · "+condition+(modal?" · Keuzemodus: niet alle effecten zijn tegelijk beschikbaar.":"");
            if(!evidence.Contains(note,StringComparer.Ordinal))evidence.Add(note);
        }
        foreach(var raw in normalizedText.Split('\n',StringSplitOptions.RemoveEmptyEntries))
        {
            var line=raw.Trim().TrimStart('•').Trim();
            if(line.Contains('"')||line.Contains('“')||line.Contains('”'))continue;
            if(Regex.IsMatch(line,@"^Affinity for artifacts\s*$",RegexOptions.IgnoreCase))
                Fact(ArtifactAffinity,line,"Eigen artifacts nodig; alleen de generieke kosten worden verlaagd.");
            if(Regex.IsMatch(line,@"^Convoke\s*$",RegexOptions.IgnoreCase))
                Fact(Convoke,line,"Eigen untapped creatures kunnen bij betalen helpen; gekleurde kosten vereisen een creature van die kleur. Creatures/tokens in het deck zijn nog geen beschikbare mana.");
            var lineDraw=false;var lineDiscard=false;
            if(Regex.IsMatch(line,@"^Flashback\s+\{",RegexOptions.IgnoreCase))
                Fact(Flashback,line,"Kaart eerst in eigen graveyard; betaal de alternatieve kosten en exile na het casten.");
            if(Regex.IsMatch(line,@"^(Cycling|Basic landcycling|Plainscycling|Islandcycling|Swampcycling|Mountaincycling|Forestcycling)\s+\{",RegexOptions.IgnoreCase))
                Fact(SelfCycling,line,"Deze kaart zelf discarden als alternatieve actie; niet tegelijk als normale spell gebruiken. Zoek-/draw-effect hangt van het keyword af.");
            if(Regex.IsMatch(line,@"^Cycling\s+\{",RegexOptions.IgnoreCase))Fact(OwnDraw,line,"Gewone cycling: betaal kosten en discard deze kaart om één kaart te trekken. Typecycling zoekt en is geen draw. Geen gelijktijdige normale spellcast.");
            if(Regex.IsMatch(line,@"\bDiscard (a|one|another|two|three|\d+) cards?\b",RegexOptions.IgnoreCase)&&
                (line.Contains(':')||Regex.IsMatch(line,@"^Equip\s*[—-]",RegexOptions.IgnoreCase))&&!OtherActor().IsMatch(line[..Math.Max(0,line.IndexOf("discard",StringComparison.OrdinalIgnoreCase))]))
                Fact(SelfDiscard,line,"Eigen kaart discarden als kosten/alternatief; geen gratis kaarten en geen draw uit discard alleen.");
            var colon=line.IndexOf(':');var cost=colon>=0?line[..colon]:"";var body=colon>=0?line[(colon+1)..].Trim():line;
            if(cost.Length>0&&!OtherActor().IsMatch(cost))
            {
                if(Regex.IsMatch(cost,@"(^|,\s*)Sacrifice (another |a |an |one or more )?(nontoken )?creature\s*$",RegexOptions.IgnoreCase))Fact(OwnCreatureSacrifice,line,"Eigen passende creature als activatiekosten; voorwaarden/kosten en het effect blijven relevant.");
                if(Regex.IsMatch(cost,@"(^|,\s*)Sacrifice (another |a |an |one or more )?(nontoken )?artifact\s*$",RegexOptions.IgnoreCase))Fact(OwnArtifactSacrifice,line,"Eigen passend artifact als activatiekosten; voorwaarden/kosten en het effect blijven relevant.");
                if(Type("Creature")&&Regex.IsMatch(cost,@"\bSacrifice this (card|creature)\b",RegexOptions.IgnoreCase))Fact(SelfCreatureSacrifice,line,"Alleen zichzelf opofferen, geen herhaalbare outlet voor andere creatures.");
                if(Type("Enchantment")&&Regex.IsMatch(cost,@"\bSacrifice this (card|enchantment)\b",RegexOptions.IgnoreCase))Fact(SelfEnchantmentSacrifice,line,"Alleen zichzelf opofferen, geen artifact-/creature-outlet.");
                if(Regex.IsMatch(cost,@"\bSacrifice (another |a |an |one or more )?(nontoken )?(artifact|creature)\b",RegexOptions.IgnoreCase)&&cost.Contains("nontoken",StringComparison.OrdinalIgnoreCase))tags.Add(NonTokenSacrificeOnly);
            }
            foreach(var sentence in Regex.Split(body,@"(?<=[.!?])\s+"))
            {
                var statement=sentence.Trim();if(statement.Length==0)continue;
                // A comma in a sequence ("Search ..., put ...") is not a trigger boundary.
                var action=Regex.IsMatch(statement,@"^(Domain\s*[—-]\s*)?(When|Whenever|At|If|For each)\b",RegexOptions.IgnoreCase)?EffectStart().Match(statement):Match.Empty;var trigger=action.Success?statement[..action.Index]:"";
                var effect=action.Success?statement[(action.Index+action.Length)..].Trim():statement;
                var condition=(cost.Length>0?"Kosten: "+cost+". ":"")+(trigger.Length>0?"Voorwaarde: "+trigger:"Afhandelen van het effect en overige kaarttekst blijven vereist.");
                if(ReplacementOrNegation().IsMatch(effect))continue;
                var foreign=OtherActor().IsMatch(effect);
                if(!foreign&&!Regex.IsMatch(effect,@"\bcreatures? you control\b",RegexOptions.IgnoreCase)&&
                    Regex.IsMatch(effect,@"^Destroy (target (?:(?:nonblack|nonartifact|attacking|blocking|tapped|untapped) )?creature|target artifact or creature|target artifact, creature, or enchantment|(?:each|all) (?:(?:nonartifact|attacking|blocking|tapped|untapped) )?creatures?)\b",RegexOptions.IgnoreCase))
                    Fact(DestroyRemoval,line,condition+" Kies een passende vijandelijke CREATURE binnen de target-/typebeperkingen. Indestructible/regeneration/vervangingen en werkelijk sterven blijven vereist; een planeswalker kiezen levert geen creature-dood.");
                if(!foreign&&!Regex.IsMatch(effect,@"\bcreatures? you control\b",RegexOptions.IgnoreCase)&&
                   (Regex.IsMatch(effect,@"^this (card|creature|artifact|enchantment|spell) deals [1-9][0-9]* damage to (any target|target (?:(?:attacking or blocking|attacking|blocking|tapped|untapped|nonblack|nonartifact) )?creature|(?:each|all) (?:(?:attacking or blocking|attacking|blocking|tapped|untapped) )?creatures?)\b",RegexOptions.IgnoreCase)||
                    Regex.IsMatch(effect,@"^Target [^.]*creature gets -[0-9]+/-[1-9][0-9]*\b",RegexOptions.IgnoreCase)))
                    Fact(ConditionalToughnessRemoval,line,condition+" Alleen een legaal vijandelijk creature-target dat aan de tekst voldoet en daadwerkelijk door voldoende damage/-toughness sterft. Combat, flying, gekozen verdeling en overige beperkingen niet wegdenken.");
                if(Regex.IsMatch(effect,@"^(target opponent|each opponent|an opponent|your opponents?) (may )?draws? (a|one|two|three|[0-9]+) cards?\b",RegexOptions.IgnoreCase))Fact(OpponentDraw,line,condition+" Dit is draw voor een tegenstander, geen eigen draw-source.");
                var positiveOwn=false;
                if(!foreign)
                {
                    var draw=OwnDrawAction().IsMatch(effect)||Regex.IsMatch(effect,@"^You\b.*\band draw (a|one|two|three|four|five|X|\d+) cards?\b",RegexOptions.IgnoreCase)||
                        Regex.IsMatch(effect,@"^(Scry|Surveil|Mill) (one|two|three|four|five|X|\d+)( cards?)?, (then )?(you (may )?)?draw (a|one|two|three|four|five|X|\d+) cards?\b",RegexOptions.IgnoreCase);
                    if(draw){Fact(OwnDraw,line,condition);positiveOwn=true;lineDraw=true;}
                    if(Regex.IsMatch(effect,@"^(You (may )?discard|Discard) (a|one|two|three|[0-9]+) cards?\b",RegexOptions.IgnoreCase))
                    {Fact(SelfDiscard,line,condition+" Eigen handkaart discarden; hoeveelheid en timing volgen de tekst.");lineDiscard=true;}
                    if(draw&&Regex.IsMatch(effect,@"\b(?:then |and )?(?:you )?discard (a|one|two|\d+) cards?\b",RegexOptions.IgnoreCase))Fact(Loot,line,condition+" Trekken gevolgd door discard is niet automatisch netto kaartvoordeel.");
                    if(Regex.IsMatch(effect,@"^(You (may )?mill|Mill) (one|two|three|four|five|six|X|\d+) cards?\b",RegexOptions.IgnoreCase))Fact(SelfMill,line,condition);
                    if(Regex.IsMatch(effect,@"^(You (may )?surveil|Surveil) (one|two|three|X|\d+)\b",RegexOptions.IgnoreCase))Fact(Surveil,line,condition+" De keuze/topkaarten zijn niet gegarandeerd.");
                    if(Regex.IsMatch(effect,@"^Return target creature card from your graveyard to (your hand|the battlefield)\b",RegexOptions.IgnoreCase))Fact(CreatureRecursion,line,condition+" Geschikte creatureKAART nodig; tokens verdwijnen en zijn geen blijvende doelen.");
                    if(Regex.IsMatch(effect,@"^(You (may )?)?Return target\b",RegexOptions.IgnoreCase))
                    {
                        var rules=SynergyTargets.Recursion(card with{OracleText=line});
                        if(rules.Count>0)
                        {
                            Fact(TypedCardRecursion,line,condition+" Targetbeperking: "+string.Join("; ",rules.Select(r=>r.Description))+". Geschikte echte kaart in eigen graveyard vereist.");
                            if(rules.Any(r=>r.Types.Contains("Creature")||r.Types.Contains("Card")))Fact(CreatureRecursion,line,condition+" Alleen passende creaturekaart; mana-value- en typebeperkingen blijven gelden.");
                            if(rules.Any(r=>r.Types.Contains("Creature")&&r.Destination=="the battlefield")&&Regex.IsMatch(line,@"\bSacrifice it at the beginning of the next end step\b",RegexOptions.IgnoreCase))
                                Fact(DelayedCreatureSacrifice,line,condition+" Alleen de werkelijk teruggekeerde creature aan het volgende end step opofferen; geen sacrifice van deze bronkaart en geen vrij herhaalbare outlet.");
                        }
                    }
                    if(Regex.IsMatch(effect,@"^(Domain\s*[—-]\s*)?(You (may )?)?Search your library for a basic land card\b",RegexOptions.IgnoreCase))Fact(BasicLandSearch,line,condition+" Alleen een nog aanwezige eigen basic; geen extra basislandtype voordat het land echt op slagveld ligt.");
                    if(Regex.IsMatch(effect,@"^Return (all|any number of|target) (nonland )?permanent cards?\b.+\bfrom your graveyard to the battlefield\b",RegexOptions.IgnoreCase))Fact(PermanentRecursion,line,condition+" Alleen echte permanente kaarten die aan de tekstuele beperkingen voldoen; geen onbeperkte creature-recursion.");
                    if(Regex.IsMatch(effect,@"^(You (may )?put|Put) a creature card from your hand onto the battlefield\b",RegexOptions.IgnoreCase))
                    {Fact(CreatureFromHand,line,condition+" CreatureKAART in je hand nodig; geen graveyard-recursion of token terughalen.");positiveOwn=true;}
                    var tokenAction=Regex.Match(effect,@"(?:^|\bthen\s+|\band\s+)((?:you (?:may )?)?create\b.+)$",RegexOptions.IgnoreCase);
                    var tokenEffect=tokenAction.Success?tokenAction.Groups[1].Value:"";
                    if(tokenEffect.Length>0&&!ForeignTokenController().IsMatch(tokenEffect))
                    {
                        if(tokenEffect.Contains("copy of",StringComparison.OrdinalIgnoreCase))Fact(ConditionalCopyToken,line,condition+" Eigenschappen van een kopietoken hangen van het legale target af.");
                        else
                        {
                            var typedToken=false;
                            if(Regex.IsMatch(tokenEffect,@"\bcreature tokens?\b",RegexOptions.IgnoreCase)){Fact(OwnCreatureTokens,line,condition);positiveOwn=true;typedToken=true;}
                            if(Regex.IsMatch(tokenEffect,@"\bartifact (creature )?tokens?\b|\b(Treasure|Food|Clue|Blood|Map|Mutagen|Junk|Powerstone|Gold) tokens?\b",RegexOptions.IgnoreCase))
                            {Fact(OwnArtifactTokens,line,condition);positiveOwn=true;typedToken=true;}
                            if(Regex.IsMatch(tokenEffect,@"\benchantment tokens?\b|\b(Role|Shard) tokens?\b",RegexOptions.IgnoreCase))
                            {Fact(OwnEnchantmentTokens,line,condition+" Enchantment/Aura-token, geen creature of artifact; wie de enchanted creature controleert bepaalt niet automatisch wie de Aura controleert.");positiveOwn=true;typedToken=true;}
                            if(!typedToken&&Regex.IsMatch(tokenEffect,@"\btokens?\b",RegexOptions.IgnoreCase))
                            {Fact(OwnUnknownTokens,line,condition+" Tokenmaking herkend, maar het tokentype is onbekend; geen creature-/artifactbonus toegekend.");positiveOwn=true;}
                            if(tokenEffect.Contains("tapped and attacking",StringComparison.OrdinalIgnoreCase))Fact(TokensEnterAttacking,line,condition+" Aanvallend binnenkomen is niet als aanvaller verklaard worden.");
                        }
                    }
                    if(Regex.IsMatch(effect,@"^Creatures you control (get \+[0-9]+/\+[0-9]+|(?:have|gain) (flying|trample|menace|vigilance|haste|lifelink|first strike|double strike|deathtouch|reach))\b",RegexOptions.IgnoreCase)&&!Regex.IsMatch(effect,@"^Creatures you control get \+0/\+0\b",RegexOptions.IgnoreCase)||
                        Regex.IsMatch(effect,@"^Put (a|one|two|\d+) \+1/\+1 counters? on each creature you control\b",RegexOptions.IgnoreCase))
                    {Fact(TeamBuff,line,condition);positiveOwn=true;}
                    if(Regex.IsMatch(effect,@"^(Creatures you control|Each creature you control|Target creature you control) (gain|gains|have|has) afterlife [1-9]\b",RegexOptions.IgnoreCase))
                    {Fact(OwnCreatureTokens,line,condition+" De betreffende eigen creature moet daarna daadwerkelijk sterven om afterlife-tokens te maken.");positiveOwn=true;}
                    if(Regex.IsMatch(effect,@"^Sacrifice this (card|creature|artifact|enchantment|land)\b",RegexOptions.IgnoreCase)||
                        Regex.IsMatch(effect,@"^Sacrifice it\b",RegexOptions.IgnoreCase)&&Regex.IsMatch(trigger,@"^When this (card|creature|artifact|enchantment|land) (has|is|becomes|leaves|enters)\b",RegexOptions.IgnoreCase))
                    {
                        if(Type("Creature"))Fact(SelfCreatureSacrifice,line,condition+" Alleen een eenmalige eigen-doodroute, geen outlet voor andere creatures.");
                        if(Type("Artifact"))Fact(SelfArtifactSacrifice,line,condition);
                        if(Type("Enchantment"))Fact(SelfEnchantmentSacrifice,line,condition);
                        if(Type("Land"))Fact(SelfLandSacrifice,line,condition);
                    }
                    if(Regex.IsMatch(effect,@"^(You )?(?:may )?gain ([3-9]|[1-9][0-9]+) life\b",RegexOptions.IgnoreCase))
                    {Fact(OwnLifeGainThree,line,condition+" Leven moet werkelijk worden gewonnen; dit is geen belofte van een herhaalbare lus.");positiveOwn=true;}
                    if(Regex.IsMatch(effect,@"^(You (may )?)?gain ([1-9][0-9]*) life\b",RegexOptions.IgnoreCase))positiveOwn=true;
                    if(Regex.IsMatch(effect,@"^Put (a|one|two|\d+) \+1/\+1 counters? on this (card|creature)\b",RegexOptions.IgnoreCase))positiveOwn=true;
                    if(Regex.IsMatch(effect,@"^You get (an|one|[1-9][0-9]*) experience counters?\b",RegexOptions.IgnoreCase))positiveOwn=true;
                    if(Regex.IsMatch(effect,@"\bExile the top (card|[0-9]+ cards) of your library\b",RegexOptions.IgnoreCase)&&
                        Regex.IsMatch(normalizedText,@"\byou may (play|cast) (that card|those cards)\b",RegexOptions.IgnoreCase))Fact(ExileCardAccess,line,condition+" Alleen de toegestane kaarten/tijd; een land spelen is geen spell casten.");
                    if(Regex.IsMatch(effect,@"^(You (may )?)?Search (your library and/or graveyard|your graveyard and/or library|your graveyard) for a card named\b",RegexOptions.IgnoreCase))
                        Fact(NamedGraveyardRetrieval,line,condition+" Alleen de expliciet genoemde kaart, geen algemene creature-recursion.");
                }
                if(Regex.IsMatch(effect,@"^(target opponent|each opponent|target player) (loses|lose) [1-9][0-9]* life\b",RegexOptions.IgnoreCase))positiveOwn=true;
                if(Regex.IsMatch(effect,@"^this (card|creature|artifact|enchantment) deals [1-9][0-9]* damage to (each|target) opponent\b",RegexOptions.IgnoreCase))positiveOwn=true;
                if(Regex.IsMatch(effect,@"^Each player sacrifices (a|one) (non-[A-Za-z]+ )?creature\b",RegexOptions.IgnoreCase))
                {
                    Fact(SymmetricCreatureSacrifice,line,condition+" Elke speler kiest zelf een passend object, ook jij. Subtypebeperking en timing blijven gelden; geen vrije eigen sacrifice-outlet.");
                    Fact(OpponentCreatureSacrifice,line,condition+" Ook de tegenstander offert alleen op als een passend object aanwezig is. Eigen materiaal kan eveneens verloren gaan.");
                }
                if(Regex.IsMatch(effect,@"^Target artifact creature['’]s controller sacrifices it\b",RegexOptions.IgnoreCase))
                    Fact(ArtifactCreatureSacrifice,line,condition+" Kies een legaal artifact-CREATURE-target. Alleen bij een eigen target is dit eigen sacrifice; controller doet de vervolghandeling. Geen algemene creature- of artifact-outlet.");
                var ownAttack=Regex.IsMatch(trigger,@"\b(this (card|creature) attacks|you attack|creature you control attacks)\b",RegexOptions.IgnoreCase);
                if(Regex.IsMatch(effect,@"^(target opponent|each opponent) sacrifices? (a |one |two |another )?creatures?\b",RegexOptions.IgnoreCase)||
                    ownAttack&&Regex.IsMatch(effect,@"^defending player sacrifices? (a |one |two )?creatures?\b",RegexOptions.IgnoreCase))
                    Fact(OpponentCreatureSacrifice,line,condition+" Vijand kiest het passende object; het moet werkelijk sterven. Geen eigen outlet.");
                if(positiveOwn&&trigger.Length>0)
                {
                    if(Type("Creature")&&Regex.IsMatch(trigger,@"\bthis (card|creature) dies\b",RegexOptions.IgnoreCase))Fact(SelfCreatureDeathPayoff,line,condition+" Alleen deze creature zelf moet sterven; een andere creature opofferen activeert dit niet.");
                    if(Regex.IsMatch(trigger,@"\bnontoken\b.*\bdie[sd]?\b",RegexOptions.IgnoreCase))tags.Add(NonTokenDeathOnly);
                    if(Regex.IsMatch(trigger,@"\bequipped creature dies\b",RegexOptions.IgnoreCase))Fact(EquippedDeathPayoff,line,condition+" Eerst de creature werkelijk uitrusten; geen algemene payoff voor elke creature-dood.");
                    if(Regex.IsMatch(trigger,@"\b(creature|creatures) (an opponent|your opponents?) controls? die[sd]?\b",RegexOptions.IgnoreCase))Fact(OpponentDeathPayoff,line,condition);
                    else if(Regex.IsMatch(trigger,@"\b(artifacts?|creatures?)(?: or (artifacts?|creatures?))? you control die[sd]?\b",RegexOptions.IgnoreCase))
                    {
                        var subject=Regex.Match(trigger,@"\b(?<types>artifacts?|creatures?)(?: or (?<types>artifacts?|creatures?))? you control die[sd]?\b",RegexOptions.IgnoreCase).Groups["types"].Captures.Select(c=>c.Value).ToArray();
                        if(subject.Any(t=>t.StartsWith("creature",StringComparison.OrdinalIgnoreCase)))Fact(OwnCreatureDeathPayoff,line,condition);
                        if(subject.Any(t=>t.StartsWith("artifact",StringComparison.OrdinalIgnoreCase)))Fact(OwnArtifactDeathPayoff,line,condition);
                    }
                    else if(Regex.IsMatch(trigger,@"\b(a|another|one or more) (nontoken )?creatures? die[sd]?\b",RegexOptions.IgnoreCase)&&!Regex.IsMatch(trigger,@"\b(opponent|you control)\b",RegexOptions.IgnoreCase))
                    {
                        Fact(OwnCreatureDeathPayoff,line,condition+" Onbeperkte creature-doodtekst kan ook eigen creatures betreffen. Voor target-player-drain kies je een tegenstander.");
                        Fact(OpponentDeathPayoff,line,condition+" Deze payoff kan zowel eigen als vijandelijke creature-dood zien; geen beperking tot één controller aangenomen.");
                    }
                    var enter=Regex.Match(trigger,@"\b(?<types>creatures?|artifacts?)(?: or (?<types>creatures?|artifacts?))? (?:you control enters?|enters? (?:the battlefield )?under your control)\b",RegexOptions.IgnoreCase);
                    var anyEnter=Regex.Match(trigger,@"\b(a|another|one or more) (nontoken )?(?<types>creatures?|artifacts?) enters?( the battlefield)?[,.]?\s*$",RegexOptions.IgnoreCase);
                    if((enter.Success||anyEnter.Success&&!Regex.IsMatch(trigger,@"\b(opponent|another player)\b",RegexOptions.IgnoreCase))&&
                       !Regex.IsMatch(trigger,@"\b(with (power|toughness|mana value)|if (its|their) (power|toughness)|(two|three|four|[2-9]) or more)\b",RegexOptions.IgnoreCase))
                    {
                        var subject=(enter.Success?enter:anyEnter).Groups["types"].Captures.Select(c=>c.Value).ToArray();
                        if(subject.Any(t=>t.StartsWith("creature",StringComparison.OrdinalIgnoreCase)))Fact(OwnCreatureEnterPayoff,line,condition+" Een passende eigen creature moet werkelijk binnenkomen. Geen casttrigger en geen bewijs dat de bron/token al aanwezig is.");
                        if(subject.Any(t=>t.StartsWith("artifact",StringComparison.OrdinalIgnoreCase)))Fact(OwnArtifactEnterPayoff,line,condition+" Een passend eigen artifact moet werkelijk binnenkomen; casten is niet hetzelfde.");
                        if(Regex.IsMatch(trigger,@"\bnontoken\b",RegexOptions.IgnoreCase))tags.Add(NonTokenEnterOnly);
                    }
                    if(Regex.IsMatch(trigger,@"\byou draw (a|one|your|[0-9]+) cards?\b",RegexOptions.IgnoreCase))Fact(DrawPayoff,line,condition);
                    if(Regex.IsMatch(trigger,@"\byou cast a spell from exile\b",RegexOptions.IgnoreCase))Fact(CastFromExilePayoff,line,condition);
                    else if(Regex.IsMatch(trigger,@"\b(you|a player|any player) casts?\b",RegexOptions.IgnoreCase))
                    {
                        if(Regex.IsMatch(trigger,@"\bnoncreature spell\b",RegexOptions.IgnoreCase))Fact(NoncreatureCastPayoff,line,condition);
                        else if(trigger.Contains("artifact",StringComparison.OrdinalIgnoreCase)&&trigger.Contains("instant",StringComparison.OrdinalIgnoreCase)&&trigger.Contains("sorcery",StringComparison.OrdinalIgnoreCase))Fact(ArtifactInstantSorceryCastPayoff,line,condition);
                        else if(Regex.IsMatch(trigger,@"\ban instant or sorcery spell\b",RegexOptions.IgnoreCase))Fact(InstantSorceryCastPayoff,line,condition);
                        else if(Regex.IsMatch(trigger,@"\ban artifact spell\b",RegexOptions.IgnoreCase))Fact(ArtifactCastPayoff,line,condition);
                    }
                    if(Regex.IsMatch(trigger,@"\byou attack with (one or more |a |an )?(non-[A-Za-z]+ )?creatures?\b",RegexOptions.IgnoreCase))
                    {Fact(AttackCreaturePayoff,line,condition);if(trigger.Contains("non-Gnome",StringComparison.OrdinalIgnoreCase))tags.Add(NeedsNonGnomeAttacker);}
                    if(Regex.IsMatch(trigger,@"\byou gained (3|three) or more life this turn\b",RegexOptions.IgnoreCase))Fact(RequiresLifeGainThree,line,condition);
                }
            }
            if(Regex.IsMatch(line,@"\bpermanent cards in your graveyard\b",RegexOptions.IgnoreCase)&&
                Regex.IsMatch(line,@"\bthis (card|creature) gets \+[1-9][0-9]*/\+[1-9][0-9]*\b",RegexOptions.IgnoreCase))
                Fact(GraveyardPermanentPayoff,line,"Echte permanente KAARTEN in eigen graveyard en de genoemde drempel vereist; niet alle spells/tokens tellen.");
            if(lineDraw&&lineDiscard&&!modal)Fact(Loot,line,"Trekken en daarna eigen discard volgen de tekst; geen automatisch netto kaartvoordeel. Bewaak een eventuele limiet per beurt.");
            if(Regex.IsMatch(line,@"Search your library for a basic (Forest|Plains|Island|Swamp|Mountain)\b",RegexOptions.IgnoreCase)&&
                !Regex.IsMatch(line,@"\b(opponent|its controller|that player)\b",RegexOptions.IgnoreCase))Fact(RestrictedBasicLandSearch,line,"Alleen de genoemde basislandtypen mogen worden gezocht; het doel moet nog in eigen library zitten.");
        }
        return new(tags,evidence.Distinct(StringComparer.Ordinal).ToArray());
    }
    public static IReadOnlyList<string> NamedTargets(PaperCard card)=>Regex.Matches(card.OracleText,@"for a card named (.+?)(?=, (?:reveal|put|then)|\.)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
        .Select(m=>m.Groups[1].Value.Trim()).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    [GeneratedRegex(@",\s*(?=(you\s+(may\s+)?(create|draw|discard|gain|get|mill|put|return|lose|search)|create\b|draw\b|discard\b|gain\b|mill\b|put\b|return\b|search\b|sacrifice\b|defending player\b|each (opponent|player)\b|target (opponent|player)\b|that player\b|this (card|creature|artifact|enchantment)\b))",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex EffectStart();
    [GeneratedRegex(@"^(target opponent|each opponent|an opponent|your opponents?|each player|target player|its controller|their controller|its owner|their owner|that player|that (creature|artifact|permanent|spell|card|token|object)['’]s (controller|owner))\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex OtherActor();
    [GeneratedRegex(@"\b(can't|cannot|don't|doesn't|instead|would|rather than|without)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex ReplacementOrNegation();
    [GeneratedRegex(@"\bunder (?!your control\b|your team'?s? control\b)[^.]+ control\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex ForeignTokenController();
    [GeneratedRegex(@"^(You (may )?draw|Draw) (a|one|two|three|four|five|six|seven|X|[0-9]+) cards?\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex OwnDrawAction();
}
