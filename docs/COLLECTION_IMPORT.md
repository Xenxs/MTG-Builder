# Collectie-import — formaat en gebruik

Fase 3 ondersteunt **MTGDeckbuilder collectie JSON v1**. Dit is een eigen, expliciet
bestandsformaat; het is geen officiële Arena-export. Een Scryfall-bulkbestand, Arena-log
of decklijst bevat niet automatisch jouw volledige bezit en wordt niet als zodanig gelezen.
Er is geen accountkoppeling of live synchronisatie. Alle verwerking blijft lokaal.

## Een bestand gebruiken

1. Importeer zo nodig eerst de kaartcatalogus onder **Catalogus**. Reeds bestaande
   handmatige printings kunnen ook zonder catalogus worden gekozen.
2. Kies **Collectie-import → Collectiebestand kiezen**. Gebruik bijvoorbeeld
   `examples/collection-v1.json` of exporteer je bestaande collectie en pas de aantallen aan.
3. Controleer de voorbeeldweergave. Voorbereiden wijzigt geen bezit of decks. De
   voorbereiding wordt lokaal opgeslagen en is na herstart via de historie te openen.
4. Selecteer bij een onzekere regel **Geselecteerde regel koppelen of overslaan**.
   Toon mogelijke koppelingen of zoek een bestaande printing op naam, set of nummer.
   Controleer naam, set, nummer en taal; klik daarna expliciet op koppelen of overslaan.
5. Controleer alle pagina's, vink de controle aan en kies **Wijzigingen toepassen**.
   Ongekoppelde regels en meerdere regels voor hetzelfde doel blokkeren de hele import.
6. **Voorbereiding verwerpen** bewaart de registratie maar voert niets uit. Een
   toegepaste import kan met de herstelbevestiging via de historie worden teruggedraaid,
   zolang geen betrokken aantal daarna is gewijzigd.

## Betekenis van aantallen

- Aantallen zijn **absoluut**: bezit 2 met invoer 4 wordt 4, geen 6. Herimporteren
  telt dus niets dubbel. Een identieke herimport registreert nul wijzigingen.
- Een expliciete `quantity: 0` maakt het bezit van die printing nul.
- Kaarten die niet in het bestand staan blijven ongewijzigd. Er is geen impliciete
  vervanging van de hele collectie en geen knop die ontbrekende kaarten automatisch wist.
- Dubbele regels worden nooit opgeteld of op basis van bestandsvolgorde gekozen.
  Ook twee regels met hetzelfde aantal voor hetzelfde doel vereisen een keuze.
- Een koppeling naar een bestaande handmatige printing behoudt diens ID en deckregels.
  Dit is een bronregel aan een doel koppelen, geen samenvoeging van twee printings.
  Handmatige en catalogusprintings kunnen naast elkaar blijven bestaan.

## JSON v1

Het bestand is UTF-8 JSON (een UTF-8-BOM mag), maximaal **16 MiB**, met **1–50.000** regels:

```json
{
  "schemaVersion": 1,
  "cards": [
    {
      "setCode": "FDN",
      "collectorNumber": "227",
      "language": "en",
      "name": "Llanowar Elves",
      "quantity": 4
    }
  ]
}
```

Elke regel heeft precies één identiteit:

| Identiteit | Velden | Koppeling |
| --- | --- | --- |
| Scryfall-printing | `scryfallId` met een geldig, niet-leeg GUID | Exacte printing; voorkeurskeuze bij export |
| Arena | `arenaId` als positief geheel getal | Alleen automatisch bij één passende printing; Arena-ID kan gedeeld zijn |
| Set/nummer/taal | `setCode`, `collectorNumber`, `language` samen | Alleen automatisch bij één passende printing, inclusief handmatige regels |

`quantity` is verplicht: een geheel JSON-getal van 0 tot en met 9999, geen tekst of breuk.
`name` is optioneel. Als deze aanwezig is moet de naam ook overeenkomen voor een automatische
koppeling; bij verschil blijft een expliciete keuze mogelijk. Een naam alleen is geen identiteit.
Taal is bij de natuurlijke identiteit verplicht; er wordt geen Engelse versie geraden.
Setcodes en nummers worden naar hoofdletters genormaliseerd, taal naar kleine letters;
tekstranden worden getrimd. Namen worden zonder onderscheid tussen hoofd-/kleine letters vergeleken.

Veldnamen zijn exact en hoofdlettergevoelig. Onbekende of dubbele velden, ongeldige types,
ontbrekende aantallen, meerdere identiteiten per regel en afgekapt JSON worden geweigerd.
Optionele velden mogen ontbreken of `null` zijn; vereiste identiteiten en aantallen niet.
Maximum JSON-diepte is 8. Er wordt niets gedownload op basis van de bestandsinhoud.

## Voorbereiding en conflicten

De voorbereiding slaat de gevalideerde kaartregels, bronhash, bestandsnaam, expliciete keuzes,
doelidentiteiten en de toenmalige aantallen/versies op. Het bronbestand hoeft daarna niet
aanwezig te blijven. De volledige oorspronkelijke log of een bestandspad wordt niet bewaard.
Een nieuwe poging leest de bron opnieuw en krijgt een eigen voorbereiding.

Verandert na de voorbereiding een betrokken aantal, dan blokkeert toepassen. Dit geldt
ook als iemand een aantal wijzigt en daarna terugzet. Een gewijzigde kaartidentiteit/naam
blokkeert eveneens. Bereid in dat geval opnieuw voor en controleer de nieuwe verschillen.
Wijzigingen aan niet-betrokken kaarten blokkeren de import niet.

Een pagina bevat maximaal 50 regels. De totaaltellers en blokkades gelden voor **alle**
pagina's. De kandidatenlijst en doelzoekfunctie leveren maximaal 50 printings; verfijn
de zoekterm of gebruik het collectornummer als de gewenste variant niet in beeld staat.
De historie toont 50 imports per pagina, met navigatie naar oudere registraties.

## Herstel en export

Herstel controleert alle door de import gewijzigde regels vóór er iets wordt teruggedraaid.
Iedere expliciete latere schrijfactie op zo'n aantal blokkeert het volledige herstel,
ook als het uiteindelijke aantal gelijk is. Er is geen gedwongen herstel dat latere
bewerkingen overschrijft. Een ongewijzigde herimport raakt de versies niet aan.

Bestaande rijen krijgen bij herstel hun oude aantal; uitsluitend door de import aangemaakte
bezitsrijen verdwijnen. Printings en deckregels blijven altijd staan. Herstel werkt ook na
herstart. Het is conservatief: na import B toepassen en herstellen kan een oudere import A
nog geblokkeerd zijn, doordat de versies inmiddels zijn gewijzigd. Handmatig aantallen
aanpassen is dan een afzonderlijke, bewuste bewerking.

**Mijn collectie exporteren** schrijft bestaande bezitsrijen, inclusief nul-aantallen,
naar hetzelfde formaat. Catalogusprintings krijgen een Scryfall-ID; handmatige printings
een set/nummer/taal-identiteit. Een lege collectie wordt met een duidelijke melding geweigerd.
Een export schrijft eerst een tijdelijk bestand naast het doel en vervangt het gekozen
`.json`-bestand pas na succes. Export is geen volledige databaseback-up: decks, instellingen
en importhistorie staan er niet in. Er is geen automatisch accountonderscheid; controleer
zelf bij welk Arena-account een aangeleverd collectiebestand hoort.

## Grenzen van Arena-validatie

Op 22 oktober 2026 stond in deze ontwikkelomgeving geen `Player.log` op de standaardlocatie.
[Wizards beschrijft uitgebreide logging](https://mtgarena-support.wizards.com/hc/en-us/articles/360000726823-Creating-Log-Files-on-PC-Mac-Steam),
maar garandeert daar geen complete collectie-snapshot of stabiel exportformaat.
Een eventuele latere Arena-adapter vereist een aantoonbaar geschikte actuele bron en
afzonderlijke validatie. De bestandsimport kan zelfstandig worden gebruikt en getest.
