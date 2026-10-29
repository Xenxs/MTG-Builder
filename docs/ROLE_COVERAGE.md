# Praktische roldekking — fase 8

Dit document beschrijft de **fase-8-baseline**. Voor het huidige v0.9.0-gebruik,
gerichte schade, modale opties en de nieuwe evaluatie zie
[fase 9](DAMAGE_AND_MODAL_ROLES.md) en [controlebewijs](VALIDATION_PHASE9.md).
De aantallen hieronder blijven de historische fase-8-meting.

Versie **0.8.0** breidt de vier bestaande families uit: counterspells, gerichte
removal, kaarten trekken en mana-versnelling. De belangrijkste wijziging is het
onderscheid tussen **volledige tekstherkenning**, **onvolledige rol-aanwijzing** en
**onbekend**. Dit is een lokale patroonmodule, geen algemene spelregel- of strategie-engine.

## Gebruik en betekenis

Open **Mijn decks → Alternatieven**, bereken opnieuw en selecteer een kandidaat.
Lees de rol, doelwitten, beperkingen, timing, kosten en beide kaartteksten. De uitleg
toont de herkende passage en alle overige, niet beoordeelde tekst. De waarschuwing
wordt herhaald in het gezamenlijke voorbeeld vóór **Als nieuw deck opslaan**.

| Herkenning | Wat de app wel en niet heeft vastgesteld |
| --- | --- |
| Volledige tekst herkend | De gehele tekst past binnen ondersteunde patronen. Geen bewijs van castbaarheid, synergie of strategische gelijkwaardigheid. |
| Onvolledige rol-aanwijzing | Een zelfstandige passage is herkend. De overige tekst kan voorwaarden, kosten en effecten bevatten en blijft volledig zichtbaar. Dit levert nooit een sterke rolmatch op. |
| Rol niet bewezen | Er is geen ondersteunde volledige tekst of zelfstandige passage. De volledige kaarttekst blijft beschikbaar voor eigen beoordeling. |

Een aanwijzing is geen claim dat de kaart zonder voorwaarden die rol vervult. Een
modale keuze, geciteerde voorbeeldtekst of effect binnen een niet ondersteunde
voorwaarde wordt niet door een losse zoekterm tot sterke rolmatch verheven.

Dezelfde geverifieerde Oracle-kaart blijft eerst staan. Daarna volgen passende,
beperkte, onbekende en andere rollen. Binnen dezelfde vergelijkingscategorie hebben
volledig herkende vergelijkingen voorrang op onvolledige aanwijzingen; daarna gelden
de bestaande criteria voor kleur, manawaarde, type en stabiele naam-/printingvolgorde.
Een onbekende of zwakke kandidaat blijft bewust selecteerbaar met waarschuwing.
Voorraad, bronactualiteit en volledige formaatcontrole bepalen nog steeds de geschiktheid.

## Ondersteunde uitbreiding

De classifier heet **Oracle roles v2 (2026-10-22)**. De oude volledige patronen blijven
beschikbaar. De nieuwe interpreter ondersteunt onder meer:

- Exact beschreven kleur-/manawaardebeperkingen bij counters en doelbeperkingen bij
  destroy, exile en bounce, zoals attacking, tapped en an opponent controls.
- Een ondersteund hoofdeffect met een volledig ondersteund extra draw-, discard-,
  life-, scry- of surveil-effect. Extra effecten blijven in de vergelijking staan.
- Draw/discard in beide ondersteunde volgordes; scry/surveil gevolgd door draw, met
  de exact ondersteunde herinneringstekst. Dit wordt niet als zuivere kaartwinst gepresenteerd.
- De exacte Cultivate-landverdeling; eenvoudige tap-mana-abilities op niet-landen,
  mana-betaalde draw-abilities en creature-enters-/dies-drawtriggers. Mechanisme,
  activatiekosten en ondersteunde keywordregels blijven zichtbaar.
- Als de gehele tekst onbekend is: alleen ondersteunde passages aan het begin van
  een regel of zin als expliciet onvolledige aanwijzing, met de volledige resttekst.

Een spell, activatie en trigger zijn geen automatisch gelijkwaardige mechanismen.
Verschillende bijkomende effecten leveren hoogstens een beperkte rolmatch op.
Dubbelzijdige kaarten, algemene modale interpretatie, schade als removal, complexe
kosten/voorwaarden en algemene synergie blijven buiten deze module. Gewone landmana
en cost reduction zijn in deze meting geen bewezen mana-versnelling. De tekstgrens
is 8.000 tekens en reguliere expressies gebruiken een time-out van 100 ms.

## Gemeten dekking

De echte, eerder opgehaalde Scryfall-snapshot van **21 oktober 2026 21:06:00.423 UTC**
bevat 5.164 unieke Standard-legale kaartdefinities. Het betreft de opgeslagen
bronstatus, geen nieuwe online legaliteitscontrole voor iedere kaart.

| Familie | Volledig herkend | Onvolledige aanwijzing |
| --- | ---: | ---: |
| Counterspell | 11 | 20 |
| Removal | 16 | 73 |
| Draw | 11 | 74 |
| Mana-versnelling | 5 | 40 |
| **Totaal** | **43** | **207** |

**4.914 blijven onbekend.** Volledige dekking stijgt van 16 naar 43 kaarten: circa
**0,83%** van deze kaartset. De 207 aanwijzingen zijn ongeveer 4,0%, maar mogen daar
niet als volledig begrepen kaarten bij worden opgeteld. De aantallen meten
patroondekking, geen handmatig bewezen juistheid van alle 250 gevonden gevallen.

## Ontwikkelset en aparte evaluatie

[role-reference-v2.json](../examples/role-reference-v2.json) is een ontwikkel-/regressieset
van dertig teksten: **24 volledig, 4 onvolledig, 2 onbekend**. Op de bestaande tien
gekozen rangschikkingsscenario's blijft de eerste-keuzescore **10/10**, tegenover
**3/10** met de bevroren fase-6-sortering. Dit is geen nieuwe onafhankelijke kwaliteitsmeting.

Daarnaast zijn vooraf dertig andere kaartnamen gereserveerd: zes per trefwoordstratum
Counter, Removal, Draw, Mana en Other, deterministisch gekozen met SHA-256 op Oracle-ID.
De ontwikkelset en drie al bekeken praktijkkaarten werden uitgesloten. De interpreter
en vergelijking zijn bevroren vóór het bekijken van deze gereserveerde teksten.
Daarna heeft dezelfde ontwikkelaar de verwachte families en belangrijke voorwaarden
vastgelegd, vóór de uitkomsten per kaart werden bekeken. Geen onafhankelijke reviewer;
de gestratificeerde selectie is niet representatief voor alle kaarten.

De gepubliceerde [evaluatieset](../examples/role-evaluation-v1.json) bevat 19 kaarten met
een relevante rol binnen deze afbakening en 11 zonder. Uitkomst:

- **0 volledig**, **4 onvolledige aanwijzingen**, **26 onbekend**.
- **15 van de 19 relevante kaarten gemist**; herkenning inclusief aanwijzingen: **21,1%**.
- Onder de vier aanwijzingen zijn geen verkeerde families vastgesteld en blijven de
  vereiste voorwaarden zichtbaar. Er zijn geen volledige voorspellingen om de
  precisie van volledige herkenning op deze set te beoordelen.

Dit is een zwak dekkingsresultaat. Nul verkeerde labels onder vier aanwijzingen
rechtvaardigt geen claim van 100% nauwkeurigheid. De parser is na deze uitkomst niet
op de gereserveerde gevallen aangepast. Een volgende verbetering heeft nieuwe,
ongeziene evaluatiegevallen en bij voorkeur een tweede beoordelaar nodig.

## Opslag en grenzen

Engine **Role alternatives v3**, classifier v2: voorbereidingen bewaren herkenning,
passage, resttekst, mechanisme, bijkomende effecten, regel en versie in de bestaande
JSON-envelop. Oude ongebruikte voorbereidingen moeten opnieuw worden berekend;
oude toegepaste ontvangsten blijven hetzelfde resultaat opleveren. Het origineel
en bezit worden niet overschreven. Geen nieuwe migratie: dezelfde zes blijven staan.
De domeingrens voor ChoicesJson is **2.000.000 tekens**, geen exact aantal bytes.

De bestaande 24-uursgrens voor de bron en 14-dagentermijn voor ingebouwde regels blijven
gelden. Een oude catalogus zonder legaliteitsdata vereist online bijwerken; de app-upgrade
verzint die gegevens niet. Geen winkans, metagame-optimalisatie, volledige deckgeneratie,
wildcardbudget, Arena-accountkoppeling of externe AI-API. Zie het
[controleverslag](VALIDATION_PHASE8.md) voor uitvoering, bewijsbestanden en gegevensbehoud.
