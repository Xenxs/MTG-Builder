# Vooraf vastgelegde beoordeling — fase 16

28 oktober 2026, vastgelegd vóór aanpassing van herkenning/rangschikking en vóór
systematische inspectie van de Oracle-teksten van de productcollectie.
De bestaande code en kaartidentiteiten zijn wel bekeken. Dit is geen onafhankelijke
review: implementatie en beoordeling worden door dezelfde ontwikkelaar gedaan.

## Wat moet worden verbeterd

De v1.2-logica is een woordfilter plus een statische sortering, geen beoordeling van
onderlinge werking. Een geslaagd 60/100-kaartdeck bewijst geen strategische kwaliteit.
Doel: uitlegbare, conservatieve relaties en ontbrekende voorwaarden in de fysieke
productcollectie; die relaties moeten zowel zichtbaar zijn als de selectie beïnvloeden.

Vooraf vastgelegde ontwikkelreferenties:

| Kaarten / situatie | Verwachting |
| --- | --- |
| Cloud + Warrior's Sword | Equipment kan Cloud uitrusten; benoem equip/aanvallen en kosten als voorwaarden. |
| Adelbert Steiner + Equipment | Profiteert van eigen Equipment, niet van een louter woordvermelding bij de tegenstander. |
| Beatrix + Equipment + eigen creature | Verplaatsen/aanhechten kan ondersteunen; geen gratis onbeperkte acties beloven. |
| Barret + veel Equipment op één creature | Niet gelijkstellen aan veel uitgeruste creatures. |
| Job select-Equipment | Creatuurtoken en Equipment uit dezelfde kaart herkennen, indien Oracle-tekst dit bevestigt. |
| Sephiroth + eigen opofferbare creatures | Onderscheid eigen sacrifice-mogelijkheid van tegenstanders laten opofferen. |
| Al Bhed Salvagers + eigen creature/artifact-dood | Eigen trigger en betreffende permanenttypen onderscheiden. |
| Recursion + creatuurtokens | Tokens zijn geen herbruikbare creaturekaarten in de graveyard. |
| Eigen graveyard-recursion + graveyard legen | Geen positieve relatie; benoem relevant conflict of onzekerheid. |
| Matca Rioters + verschillende basislandtypen | Domain kijkt naar typen van eigen landen op het slagveld. |
| Domain + Kaleidostone | Vijf kleuren mana verhogen niet het aantal basislandtypen. |
| Domain + Rampant Growth / Terramorphic Expanse | Zoeken kan ontbrekende basislandtypen ondersteunen; getapt/volgorde niet als al gerealiseerd tellen. |
| Domain + alleen Forest-kopieën | Nog steeds één basislandtype, geen vijf. |
| Aven Squire + vliegende aanvaller | Exalted ondersteunt één aanvaller; evasion helpt mogelijk. |
| Exalted + aanvallen met een tokenleger | Exalted-bonus niet toekennen aan een aanval met meerdere creatures. |
| Rhox Meditant | Conditionele draw vereist de in de kaart vermelde toestand. |
| Paleoloth + Woolly Thoctar | Power-voorwaarde en andere triggerbeperkingen benoemen; niet iedere creature voldoet. |
| Token maken voor tegenstander | Geen positieve eigen tokenproductie claimen. |
| Tegenstander offert op | Geen eigen sacrifice-outlet claimen. |
| Kaarten uit graveyard van tegenstander verbannen | Geen eigen recursion-/graveyardvoordeel afleiden uit dat ene woord. |
| Tegenstander trekt / kan niet trekken | Geen eigen draw-engine noemen. |
| Ontkende token-/equip-/draw-tekst of alleen herinnering | Geen bevestigde positieve relatie uit een woordtreffer. |
| Twee payoffs zonder producer | Ontbrekende ondersteuning melden; geen synergie uit gedeeld label alleen. |
| Gewijzigde metadata van gecontroleerde kaart | Controleprofiel niet stilzwijgend blijven vertrouwen. |

## Acceptatie voor selectie en uitleg

- Beoordeel de huidige 202 aangenomen exemplaren, alle unieke Oracle-kaarten en vier
  productdecks. Een onbekende kaart blijft zichtbaar onbekend; dat is geen negatieve
  gameplaybeoordeling. Naam/identiteit alleen mag geen gewijzigde tekst valideren.
- Maak scheiding tussen kaartkenmerk, onderlinge relatie, voorwaarde, conflict en
  deckbouwprobleem. Geen percentages/winrates zonder onderliggende meting.
- Equipment-voorstellen moeten creatures/dragers overhouden; alleen payoffs stapelen
  mag de ontbrekende enablers niet verhullen. Geen beloning voor irrelevante kopieën.
- Domain-landkeuze moet landtypen overwegen; nooit typeaantal uit mana-kleuren afleiden.
- Selectie moet aantoonbaar veranderen bij het toevoegen/verwijderen van passende
  ondersteuning. Behoud determinisme, kleuridentiteit, actuele legaliteit, singleton/
  vierlimiet, exacte voorraad, idempotente opslag en gegevensbehoud.
- Bewaar vóór implementatie een v1.2-baselinerapport met keuzes/woordtreffers/decklijsten.
  Meet verbeteringen op vooraf gedefinieerde ontwikkelreferenties en tegengevallen.
  Dit zijn ontwikkelmetingen, geen ongeziene of onafhankelijke benchmark.
- Reserveer daarnaast een reproduceerbare aparte set kaarten buiten de productpool
  vóór Oracle-inspectie; evalueer pas na bevriezen van de eerste nieuwe implementatie.
  Rapporteer gemiste/onjuiste relaties en onbekende gevallen afzonderlijk. Als daarna
  wordt verbeterd, noem de hergebruikte set expliciet ontwikkelmateriaal.
- UI toont concrete relaties en voorwaarden; een uitgebreide evaluatie blijft als
  bestand beschikbaar. Bouwen, tests, screenshots en distributie met bewijs; geen
  normale gebruikersdata als schrijf-/resetproef gebruiken.

Raming 4–8 actieve uren; modeladvies GPT-6.1 Sol / High. Geen ondersteunde bediening
voor modelinstellingen gevonden, niets gewijzigd. Stop na fase 16 voor beoordeling.
