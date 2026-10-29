# Gerichte schade en modale rol-aanwijzingen — fase 9

Versie **0.9.0** voegt vaste gerichte schade en expliciete modale opties toe aan
**Mijn decks → Alternatieven**. De vier effectfamilies blijven hetzelfde. Schade is
een mogelijke removalroute, geen bewijs van dodelijkheid. Een modale kaart krijgt
uitsluitend een onvolledige aanwijzing; herkende opties vormen geen onvoorwaardelijke
combinatie. De module blijft lokaal en gebruikt geen externe AI-API.

## Gebruiken

1. Werk de catalogus online bij, registreer bezit en bewaar een BO1-/BO3-profiel.
   Het oorspronkelijke deck moet de bestaande formaatcontrole doorstaan.
2. Open **Alternatieven**, bereken opnieuw en zoek kandidaten voor een tekort.
3. Lees het rollabel en de volledige verschillen. Bij schade staan hoeveelheid,
   doelgroep, voorwaarden en de waarschuwing dat dodelijkheid onbekend is.
4. Bij modale kaarten staan **Optie 1**, **Optie 2**, enzovoort afzonderlijk in de uitleg,
   inclusief onbekende opties. Lees ook de volledige keuzeheader en resttekst.
5. Bekijk het gezamenlijke voorbeeld. Het label meldt een beperkte match of
   onvolledige aanwijzing. Open **Rol, verschillen en herkenningsversie** voor de
   volledige waarschuwingen. Sla daarna bewust op als nieuw deck.

Het origineel en bezit blijven behouden. Een geslaagde **formaatcontrole** bewijst
geen strategische gelijkwaardigheid en geen oplossing van alle voorraadtekorten.
Reprints van dezelfde geverifieerde Oracle-kaart houden de hoogste prioriteit.

## Wat wordt herkend?

Classifier **Oracle roles v3 (2026-10-22)** ondersteunt naast de eerdere patronen:

| Tekstvorm | Afhandeling |
| --- | --- |
| Exacte kaartnaam of This spell deals N damage, N 1–9 | Alleen gericht op creature, planeswalker, creature or planeswalker of any target |
| Ondersteunde doelbeperkingen | Attacking/tapped/untapped en opponent controls/you don't control blijven vastgelegd |
| Ondersteunde schade plus ondersteund extra effect | De volledige tekst kan worden herkend; het extra effect blijft in de vergelijking |
| Schade met overige kosten/voorwaarden/effecten | Alleen een zelfstandige ondersteunde passage, met volledige overige tekst als onvolledige aanwijzing |
| Choose one/two/three/one or both/one or more/two or more met bullets | Alleen op enkelzijdige Instant/Sorcery, 2–8 opties, header vooraan; opties apart geanalyseerd |
| Niet ondersteunde optie | Optietekst blijft staan, zonder afgeleide familie |

De exacte naam wordt letterlijk behandeld, ook bij leestekens. Variabele X-schade,
fight/bite via creature power, schade aan alle creatures, alleen speler-schade,
benoemde modi zoals Magic — en algemene modale kosten/voorwaarden worden niet volledig
geïnterpreteerd. Een header, quote of trefwoord alleen bewijst geen rol. Dubbelzijdige
kaarten blijven buiten de module. De bestaande tekstgrens van 8.000 tekens en
regex-time-out van 100 ms blijven gelden.

**Schade wordt nooit een sterke rolmatch**, ook niet als de schade hoger is of beide
teksten volledig herkend zijn. Toughness en speltoestand ontbreken. **Modale
aanwijzingen worden eveneens nooit een sterke match**: kosten, keuzeregels en alle
mogelijkheden zijn niet volledig bewezen. Volledige herkenning betekent alleen dat
de ondersteunde tekst is geparseerd, niet dat de kaart een vijand zeker verwijdert.

Modale analyses bewaren alle herkende families per optie. Een kaart kan daardoor
meer dan één familie hebben, zonder dat al die effecten samen worden verondersteld.
Overlappende families geven hoogstens een beperkte match. Zonder overlap is de
rolvervanging niet bewezen. De rangschikking en alle filters uit fase 8 blijven gelden.

## Werkelijke catalogusdekking

Gemeten op dezelfde opgeslagen Scryfall-snapshot van **21 oktober 2026 21:06:00.423 UTC**:
5.164 Standard-legale kaartdefinities, volgens de opgeslagen bronstatus.

| Uitkomst | Fase 8 | Fase 9 |
| --- | ---: | ---: |
| Volledige tekst herkend | 43 | **52** |
| Onvolledige aanwijzing | 207 | **300** |
| Onbekend | 4.914 | **4.812** |

De 52 volledige gevallen omvatten 11 counters, 25 removal (waarvan 9 vaste schade),
11 draw en 5 mana-versnelling. Binnen de 300 onvolledige gevallen zitten **50 modale
kaarten** en **45 niet-modale schade-aanwijzingen**. Die 50 tellen niet als volledige
kaartinterpretatie. Volledige dekking is nog maar circa **1,0%**.

Per familie zijn er 155 removal-, 28 counter-, 78 draw- en 42 mana-aanwijzingen.
Deze aantallen **overlappen** bij modale kaarten en mogen niet tot een kaarttotaal
worden opgeteld. De hele meting telt kaarten, geen printings. Het zijn patroonuitkomsten,
geen handmatige juistheidscontrole van alle 352 kaarten met enige herkenning.

## Nieuwe gereserveerde evaluatie

Dertig kaarten zijn vóór implementatie gereserveerd: tien uit elk van Damage,
Modal en Other. Damage heeft bij overlap voorrang; selectie is deterministisch met
de laagste SHA-256-waarden van `phase9-eval-v1/OracleId`. Oude referentie-/evaluatienamen
en drie al eerder bekeken praktijkkaarten zijn uitgesloten. Strata zijn trefwoordgroepen,
geen handmatig bewezen rollen of representatieve steekproef.

De v0.8-classifier is vóór implementatie op dezelfde bron vastgelegd. Parser,
vergelijking en rangschikking zijn daarna bevroren vóór de gereserveerde teksten
werden bekeken. Dezelfde ontwikkelaar labelde verwachte families en belangrijke
voorwaarden vóór baseline- en huidige voorspellingen per kaart werden bekeken.
Geen onafhankelijke reviewer. De parser is na deze uitkomsten niet aangepast.

De [nieuwe evaluatieset](../examples/role-evaluation-v2.json) bevat 21 kaarten met
minstens één relevante familie en 9 zonder. Vergelijking met de
[vastgelegde v0.8-baseline](../examples/role-evaluation-v2-baseline.json):

| Uitkomst | v0.8 | v0.9 |
| --- | ---: | ---: |
| Volledig herkend | 0 | **1** |
| Onvolledige aanwijzing | 3 | **7** |
| Onbekend | 27 | **22** |
| Relevante kaarten geheel gemist | 18/21 | **13/21** |
| Kaarten met minstens één herkende relevante familie | 14,3% | **38,1%** |
| Verkeerde families vastgesteld | 0 | 0 |

Een kaart telt als gevonden als minstens één relevante familie is herkend. Dit meet
niet of iedere rol op een kaart is gevonden. Playful Shove is het enige volledig
herkende geval; Pumpkin Bombardment, Flick a Coin, Reverent Howl, Fatehold Charm,
Early Winter, Thorin's Last Stand en Bake into a Pie geven aanwijzingen. Alle vereiste
voorwaarden blijven in de uitleg aanwezig. Dertien relevante kaarten worden gemist,
onder meer variabele power-schade, extra headers en complexere triggers/modale doelen.

Dit resultaat is beter op deze kleine set, maar nog onvoldoende voor brede
kaartkennis. Nul verkeerde families onder acht gevonden kaarten is geen betrouwbaar
algemeen precisiepercentage. De oude fase-8-evaluatie is nu regressiemateriaal:
0 volledig, 9 aanwijzingen, 21 onbekend. De ontwikkelset van 30 gevallen heeft
26 volledig en 4 onvolledig; de bestaande tien rangschikkingsscenario's blijven
10/10 tegenover fase-6-baseline 3/10. Geen van beide is een nieuwe ongeziene meting.

## Opslag, versies en grenzen

Engine **Role alternatives v4**, classifier v3. De bestaande JSON-envelop bewaart
optienummer, optietekst, optieanalyse en herkende families, naast hoeveelheid, doelen,
beperkingen, passage en resttekst. Geen nieuwe databasekolom of migratie. Dezelfde zes
migraties en maximale ChoicesJson-lengte van 2.000.000 tekens blijven staan.
Ongebruikte oude voorbeelden vereisen herberekenen; toegepaste ontvangsten blijven
idempotent. Alle voorraad-, conflict-, rollback- en formaatcontroles blijven gelden.

Geen volledige spelregels, castbaarheids-/manabronanalyse, synergie, winkans,
metagame-optimalisatie, wildcardbudget, automatische deckgeneratie of live Arena-sync.
De bron mag maximaal 24 uur oud zijn; ingebouwde regels maximaal 14 dagen na controle.
Een oude catalogus zonder legaliteit vereist online bijwerken. Zie
[controlebewijs fase 9](VALIDATION_PHASE9.md) voor tests, hashes en native start.
