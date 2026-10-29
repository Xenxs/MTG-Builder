# Commander-decks maken

MTG Smart Deckbuilder 1.3.0, fase 16, 28 oktober 2026.

Gebruik bij voorkeur **Deckwizard → Commander 100**: kies kleuren, thema, stijl en één
commander, laat bezitmodus aan of schakel planning bewust in. Bij Cloud past Equipment;
bij Sephiroth onderscheidt het thema **Removal & vijandelijke sterftriggers** zijn
werkelijke voorwaarde van eigen creatures opofferen. Lees de relaties, voorwaarden,
onbekende kaartwerking en tekorten vóór opslaan. Zie [synergiecontrole](SYNERGY_ENGINE.md).

De onderstaande directe generator blijft daarnaast beschikbaar onder **Mijn decks**.

## Een voorstel maken

1. Open **Mijn decks**. Vul een naam voor het nieuwe deck in.
2. Kies onder **Commander** een kaart uit de lijst. Cloud uit de Starter Kit is de
   standaardkeuze zolang het aangenomen testbezit actief is. Voor een andere commander:
   zoek de kaart bij Scryfall, selecteer de exacte uitgave en gebruik de knop om de
   geselecteerde kaart als commander te kiezen.
3. Laat **Planning** uit voor bezitmodus. De app gebruikt dan uitsluitend passende
   eigen kaarten. Klik **100-kaart Commander-voorstel voorbereiden**.
4. Is er onvoldoende geschikte voorraad, dan volgt een duidelijke blokkade. Wil je
   ontbrekende kaarten als planning meenemen, schakel dan Planning bewust in en bereid
   opnieuw voor. De app zoekt gericht passende papieren kaarten bij Scryfall.
5. Bekijk de volledige lijst en alle tekorten. Een kaart kan qua kleur en structuur
   passen zonder een goede strategische keuze te zijn. Controleer de kaartafbeeldingen,
   kosten en gewenste speelstijl zelf.
6. Bevestig de controle en kies **Voorbeeld als nieuw fysiek deck opslaan**. Het
   brondeck en je bezit blijven behouden. De voorraad en metadata worden bij opslag
   opnieuw gecontroleerd. Wijzigingen na voorbereiding kunnen opnieuw voorbereiden vragen.

Een opgeslagen voorstel is een normaal bewerkbaar fysiek deck met Commander-profiel.
De tijdelijke voorbereiding en gekozen modus zijn geen blijvend optimalisatierapport.
Na herstart moet een nog niet opgeslagen voorstel opnieuw worden voorbereid.

## Wat de generator doet

Het vaste constructieplan is **één commander, 62 verschillende niet-landen en 37
basislanden**. Selectie gebruikt bezit, ondersteunde onderlinge relaties en voorwaarden,
curve, creature-/Equipment-verhouding en eventuele wizardvoorkeuren. Landen volgen een
eenvoudige verhouding van gekleurde kosten, inclusief geactiveerde kosten. De vaste
verhouding is een standaardkeuze, geen aanbeveling voor ieder
mogelijk commander-effect.

De controle omvat exact 100 inclusief commander, 99 librarykaarten, singleton over
uitgaven, kleuridentiteit, basislandtypen, geen sideboard, papierlegaliteit en actualiteit.
Scryfall levert de volledige kleuridentiteit, dus ook kleuren van achterzijden en
regelsymbolen. Andere uitgaven/talen/foil van dezelfde Oracle-kaart tellen samen voor
bezit en singleton. Basic en snow-basic lands mogen vaker voorkomen.

De gewone selectie ondersteunt één geschikte legendary creature, legendary Vehicle,
Spacecraft met P/T of een expliciete kaarttekst die de kaart als commander toestaat.
Niet-herkende uitzonderingen worden niet stilzwijgend toegestaan. Twee commanders,
partner/background-paren en een vooraf te kiezen commander-kleur worden nog niet ondersteund.

Brongegevens mogen maximaal één dag oud zijn; de ingebouwde regelcontrole verloopt na
veertien dagen. Vernieuw deckkaarten online wanneer nodig. Een nieuwe regelversie vraagt
onderhoud aan de app. Dit is geen volledige Comprehensive Rules-engine, bracketbepaling,
Game Changer-analyse, cEDH-, combo- of winstoptimalisatie.

## Betekenis van het testbezit

Conflux Naya Domain en Bant on the March hebben samen 82 vaste speelkaarten. De FINAL
FANTASY Starter Kit voegt de 60-kaart Cloud- en 60-kaart Sephiroth-decks toe: **202**
speelkaarten in totaal. Deze producten zijn geen vier Commander-decks. De vier Starter
Kit-tokens en Arena-codekaarten tellen niet mee als speelkaarten in de collectie of decklijst.

De historische fase-14-proef vond voor Cloud **34 van de vereiste 62 verschillende passende
niet-landen** in dit aangenomen bezit. Bezitmodus blokkeerde dus terecht. Planning maakte
100 kaarten met **32 ontbrekende exemplaren van 29 Oracle-kaarten**. Dit resultaat
hoort bij die vastgelegde proef en selectie; een andere commander, bijgewerkte catalogus
of collectie kan andere tekorten geven. De actuele fase-16-proeven en hun grenzen staan
in [het controleverslag](VALIDATION_PHASE16.md).

Multi-face/Adventure/split en expliciete eigen verlies-/winblokkades worden niet
automatisch gekozen. De engine kan deze voorwaarden niet veilig op deckniveau beoordelen.
Dit is geen volledige controle van alle nadelen; handmatige decks blijven jouw beoordeling vragen.

Planning is geen bezit. Ook na opslaan blijven tekorten zichtbaar. De app reserveert
geen kaartkopieën tegenover andere decks. De uitgevoerde proef gebruikte echte Scryfall-
kaartinformatie, maar geen echte telefoonscan, speltest of onafhankelijk adviesbenchmark.

## Bronnen

- [Wizards Commander](https://magic.wizards.com/en/formats/commander)
- [Actuele gelezen regels, CR 903.3–903.5](https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt)
- [Officiële Starter Kit-decklijsten](https://magic.wizards.com/en/news/announcements/final-fantasy-starter-kit-decklists)
- [Volledig bron- en importcontract](PHYSICAL_COLLECTION.md)
