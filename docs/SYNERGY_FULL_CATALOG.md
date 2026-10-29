# Alle papieren kaarten als testbezit — v1.4.0

De proef gebruikt **33.727 verschillende fysieke Oracle-kaarten**, uitgebracht tot en
met 28 oktober 2026. Oudere sets tellen mee. Per kaart is één fysieke uitgave gekozen;
dit zijn dus niet alle talen, foilversies en herdrukken. Aangenomen bezit: vier van elke
niet-basic en 250 van elke basic, samen **138.106 exemplaren**. De gewone collectie
is niet vervangen. Dit is een testsituatie, geen echte telefoonscan.

## Zelf bekijken

In deze ontwikkelwerkmap staat de afzonderlijke database onder
`.local/phase17-all-cards`. Start vanuit de projectmap:

~~~powershell
./scripts/Start-AllCardsTest.ps1
~~~

Deze start gebruikt de portable v1.4.0 en uitsluitend de gemarkeerde testmap. De
interface meldt **Gesimuleerd testbezit; geen echte scan**. Onder Mijn collectie
kun je alle kaarten filteren. Onder Mijn decks staan de opgeslagen proefvoorstellen;
verschillende engineversies hebben gelijknamige voorstellen, dus gebruik voor een
nieuwe vergelijking de deckwizard en een nieuwe naam.

De tijdelijke start weigert een snapshot ouder dan 24 uur. Voor actuele Standard-
en Commander-generatie moet eerst een nieuwe bulkproef worden voorbereid. Dit
voorkomt dat deze testsituatie later tienduizenden afzonderlijke kaartverzoeken doet.
Opgeslagen rapporten blijven leesbaar. Dit is geen automatische bulkimportfunctie
voor gewone gebruikers. De normale installer/portable gebruikt je gewone collectie
onder de lokale gebruikersmap; sluit de proef en start het programma normaal.

## Wat is veranderd

- De selectie bouwt geen voorafgaande grafiek van ieder mogelijk kaartpaar meer.
  Gelijksoortige feiten delen berekeningen; de uitleg gebruikt steeds de echte kaarten.
  Alle geschikte kandidaten mogen meedingen, zonder verborgen eerste-50-beperking.
- Dezelfde Oracle-feiten worden hergebruikt zonder uitgave, afbeelding of bronactualiteit
  van een andere kaart over te nemen. Filters voor grote collecties rekenen op de
  achtergrond en verouderde zoekresultaten overschrijven een nieuwere keuze niet.
- Gewoon cycling betekent ook zelf trekken; typecycling zoekt en is geen draw.
  Eigen/vijandelijke sterftriggers, nontoken-beperkingen, ETB-payoffs, terughaaldoelen
  met kaarttype en vaste mana-valuegrens zijn nauwkeuriger gescheiden.
- Delayed/symmetrisch sacrifice is geen vrije outlet. Een artifact-creaturedoel
  vereist beide typen. Een creature die alleen bij zijn eigen dood beloont, geeft
  geen algemene sterftrigger voor andere creatures.
- Expliciete creaturetype-/castafhankelijkheden van een commander sturen de bouw.
  Een Angel in een kaartnaam telt niet als het creaturetype Angel. Een gemaakt token
  telt niet als een gecaste Angel- of Vampire-spell. Bij graveyard-plannen wordt
  overmatige setup afgeremd ten gunste van daadwerkelijk hergebruik.
- Acorn/silver/playtest, levelers en eerder uitgesloten samengestelde kaartvormen
  worden niet automatisch gekozen. Herkende eigen verlies-, volgende-beurt-overslaan,
  bibliotheek-exile- en onvoorbereide dungeonvoorwaarden worden begrensd. Dit is geen
  volledige herkenning van alle nadelen.

## Resultaat en grens

De eerste complete proef omvatte 64 aanvragen: 27 Casual, 27 Standard en tien
Commanders. **61 voorstellen** voldeden aan structuur, kleuridentiteit, formaat,
voorraad, kopielimieten, opslag en opnieuw openen. Drie Standard/Exalted-aanvragen
werden terecht geblokkeerd: nul beschikbare ondersteunde Exalted-kaarten in dat formaat.
Na aanpassing van Commander/graveyard zijn de **16 betreffende aanvragen** opnieuw
geslaagd. Dat bewijst de gecontroleerde eigenschappen, geen sterke decks.

| Gericht onderzochte samenstelling | Voor aanpassing | Na aanpassing |
| --- | ---: | ---: |
| Giada: andere kaarten met gedrukt creaturetype Angel | 1 | 47 |
| Krenko: andere kaarten met gedrukt creaturetype Goblin | 13 | 43 |
| Talrand: instants/sorceries | 2 | 46 |
| Meren: herkende graveyard-setupkaarten | 56 | 15 |
| Meren: herkende hergebruikrollen | 12 | 40 |

Dit zijn achteraf onderzochte ontwikkelgevallen en rol-/typeaantallen, geen onafhankelijke
benchmark. Een kaart kan meerdere rollen of creaturetypen hebben. De commander zelf
is bij de Angel-/Goblintelling weggelaten. Meer van één rol is niet automatisch beter.

De algemene herkenning is nog onvoldoende voor een universele deckoptimizer. De apart
gereserveerde tweede steekproef gaf bij eerste beoordeling **22 van 57 relevante
themalabels** tegenover 19 met v1.3.0; **35 gemist**. Daarvan horen 13 labels bij bewust
afgewezen complexe/afwijkende kaarten, 22 bij overige gevallen. Nul onterechte labels in
deze beperkte acht-familiemeting bewijst geen algemene precisie. Dezelfde ontwikkelaar
maakte en beoordeelde de engine. Latere aanpassingen gebruikten deze kennis; hun
hermetingen zijn ontwikkelregressies, geen ongeziene tests.

Op alle 33.727 kaarten zijn er 87 gecontroleerde productprofielen, 13.716 met beperkte
semantische aanwijzingen en 19.924 met alleen typen of onbekende werking. Geen van
deze aantallen betekent volledig begrip van de kaart. Proliferate, algemene counters,
veel tribal-/combo-interacties, variabele thresholds, Saga- en toestandafhankelijke
werking worden niet breed gemodelleerd. Atraxa krijgt bijvoorbeeld geen bewezen
proliferate-deck. Vaste basislandplannen en eenvoudige curve blijven beperkingen.

De selectorproef met alle 31.037 geschikte niet-landen duurde circa 40 seconden en
gebruikte circa 308 MiB piekwerkgeheugen. Complete voorstellen gebruiken meer werk:
de meetprogramma's met veel bewaarde deckrapporten kwamen tot circa **1,9 GiB**.
Dit is geen gegarandeerde snelheid of bovengrens voor de interactieve app.

Zie [volledig bewijs en methode](VALIDATION_PHASE17.md),
[synergiegebruik](SYNERGY_ENGINE.md) en [vooraf vastgelegde scope](SYNERGY_ACCEPTANCE_PHASE17.md).
Bronnen: [Scryfall Default Cards](https://api.scryfall.com/bulk-data/default-cards),
[creaturetypen](https://api.scryfall.com/catalog/creature-types) en
[officiële Magic-regels](https://magic.wizards.com/en/rules), gecontroleerd 28 oktober 2026.
