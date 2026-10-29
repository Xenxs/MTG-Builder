# Deckimport, planning en analyse — fase 4

Versie 0.4.0 kan decktekst voorbereiden, koppelingen laten controleren en een nieuw
deck opslaan zonder bezit te wijzigen. Mijn decks bevat kaarten, analyse en tekstexport.
Vanaf v0.5.0 is daarnaast een afzonderlijke tab Formaatcontrole voor Arena Standard
BO1/BO3 beschikbaar; zie [uitleg en grenzen](FORMAT_VALIDATION.md). De beschrijvende
analyse zelf blijft gescheiden van legaliteit en is geen aanbevelingsmotor.
Vanaf v0.6.0 biedt de aparte tab Alternatieven structurele kaartalternatieven uit
eigen bezit met voorraad-/formaatcontrole en opslag als nieuw deck. Zie
[handleiding en grenzen](DECK_ALTERNATIVES.md).
Vanaf v0.7.0 wordt de beperkte rolherkenning met haar meetresultaten en onzekerheden
beschreven in [kaartrollen](CARD_ROLES.md).

## Een deck importeren

1. Zorg dat de lokale kaartcatalogus is gevuld. Bestaande handmatige printings kunnen
   ook worden gekoppeld, maar ontbreken hun metadata dan blijft analyse gedeeltelijk onbekend.
2. Open **Deck importeren** en plak je decktekst of laad een `.txt`-bestand.
3. Kies een formaatlabel en eventueel een naam. Een lege naam gebruikt `About / Name`
   uit de bron, of anders `Ingevoerd deck`. Het formaat wordt niet uit de bron geraden.
4. Klik **Voorbereiden**. Er ontstaat alleen een opgeslagen voorbereiding.
5. Controleer alle pagina's en los ongekoppelde regels op via kandidaten of zoeken.
   Een expliciete keuze kan ook een andere kaart aanwijzen; controleer die bewust.
6. Vink de controle aan en kies **Nieuw deck opslaan**. Er ontstaat één nieuw deck.
   Bestaande decks en collectieaantallen blijven ongemoeid.
7. Open **Mijn decks** om inhoud en analyse te bekijken. Voorbereidingen blijven na
   herstart via de historie beschikbaar. Een toegepaste voorbereiding kan niet opnieuw
   een deck maken, ook niet als het aangemaakte deck later is verwijderd.

Voor een tweede bewuste kopie van een lijst maak je een nieuwe voorbereiding. Een
herhaalde klik op dezelfde voorbereiding geeft geen duplicaat. Import maakt nooit
een bestaande deckinhoud leeg en slaat nooit stilzwijgend onbekende regels over.

## Ondersteunde tekstnotatie

```text
About
Name Mijn plan

Deck
4 Llanowar Elves (FDN) 227

Sideboard
2 Negate
```

Ondersteund zijn `aantal Naam` en `aantal Naam (SET) collectornummer`. Headers zijn
`Deck`, `Sideboard`, `Commander` en optioneel `About` met één `Name ...`-regel.
Sectieheaders zijn niet hoofdlettergevoelig. Zonder header horen kaarten bij mainboard.
Lege regels worden genegeerd: ze maken **geen** sideboard. CRLF, LF en CR worden gelezen;
een BOM aan het begin wordt geaccepteerd. UTF-8 is aanbevolen voor tekstbestanden.

De volledige dubbelzijdige naam en de naam van de voorzijde kunnen worden gekoppeld.
Setcode en collectornummer worden naar hoofdletters genormaliseerd. De catalogusnamen
vormen de basis: er is geen automatische vertaling van lokale kaartnamen of sectiekoppen.

`Companion`, `Maybeboard`, onbekende metadata, ongeldige aantallen of ongeldige regels
worden geweigerd vóór iets wordt opgeslagen. Companion vereist eigen annotatie-/sideboard-
semantiek en wordt hier niet stilzwijgend als extra kaart geteld. Commander is wél een
afzonderlijke zone; geschiktheid, singletonregels en kleureisen worden niet gecontroleerd.

Technische limieten: 256 KiB UTF-8-tekst, 256 tekens per niet-lege regel, maximaal
500 kaartregels en 10.000 exemplaren totaal. Een regel bevat 1–9999 exemplaren.
Dit zijn opslag-/verwerkingslimieten, geen toegestane deckgroottes per spelvorm.

## Hoe printings worden gekozen

- Met set/nummer: kaartnaam of voorzijde én set/nummer moeten overeenkomen. Bij meer
  dan één variant/taal is een expliciete keuze nodig.
- Zonder set/nummer: één kandidaat wordt direct gekozen. Meerdere kandidaten mogen
  alleen automatisch worden gekozen als ze allemaal dezelfde geverifieerde Oracle-ID
  hebben. De uitgave met het meeste lokale bezit heeft voorrang; daarna Engelse taal,
  actieve catalogus, set/nummer en een stabiele interne ID. De reden staat in de preview.
- Bij verschillende of handmatige, ongeverifieerde identiteiten blijft de regel open.
  Er wordt geen naamgelijkheid als bewijs van dezelfde kaart gebruikt.
- Een naamkoppeling verandert na voorbereiding niet automatisch als je bezit wijzigt.
  Relevante kaartidentiteitswijzigingen blokkeren toepassen; aantallen in bezit mogen
  veranderen omdat deckplanning dat bezit niet overschrijft.
- Herhaalde regels voor dezelfde printing in dezelfde zone worden opgeteld. Verschillende
  zones blijven aparte regels. Een totaal boven 9999 voor één printing/zone blokkeert
  de preview en toepassing. Dit verschilt bewust van collectie-import: deckregels zijn
  gevraagde aantallen, geen meerdere absolute bezitsopdrachten.

Preview en historie tonen 50 regels/imports per pagina. Kandidaten en zoeken tonen
maximaal 50 resultaten; verfijn zo nodig de naam of zoek een collectornummer.
De totaaltellers en blokkades omvatten alle pagina's. Een gewijzigde bron, naam of
formaatlabel moet opnieuw worden voorbereid; oude keuzes krijgen daarmee niet ongemerkt
een nieuwe betekenis. Navigeren bewaart de nog niet opnieuw voorbereide invoertekst.

## Een deck zelf bouwen

Maak onder **Mijn decks** een nieuw deck, of selecteer een bestaand deck.
Op **Kaarten** kun je een collectieprinting kiezen of de catalogus openklappen en een
kaart zonder bezit plannen. Kies mainboard, sideboard of commander en sla het aantal op.
Een bestaande printing/zone krijgt dit nieuwe absolute aantal; de collectie verandert niet.

## Tekorten en statistieken

**Analyse** geeft voor het geselecteerde deck de huidige mainboard-, sideboard- en
commandertotalen. Alle zones delen dezelfde lokale voorraad; dezelfde exemplaren kunnen
niet tegelijk een mainboardtekort en een sideboardtekort wegwerken.

| Maatstaf | Betekenis |
| --- | --- |
| Ontbreekt | Gevraagde exemplaren minus totaal bezit van dezelfde geverifieerde Oracle-kaart, minimaal nul |
| Tekort voor exacte printings | Per gevraagde printing: vraag over alle zones minus bezit van precies die printing; daarna opgeteld |
| Handmatige kaart | Zonder geverifieerde Oracle-ID telt alleen bezit van die interne printing mee |

Voorbeeld: vraag 4× uitgave A, bezit 2× A en 2× B met dezelfde Oracle-ID. Er ontbreekt
geen kaart, maar nog wel 2× de exacte uitgave A. De app wisselt de gekozen printing niet
automatisch om. Bezit wordt per deck beoordeeld en niet tussen meerdere opgeslagen decks
gereserveerd. Basislanden worden volgens geregistreerde aantallen behandeld; eventuele
onbeperkte Arena-beschikbaarheid of speciale unlocks zijn niet gemodelleerd.

De mana-/type-/kleurstatistieken gelden alleen voor mainboard. De curve en gewogen
gemiddelde manawaarde sluiten landen en onbekende metadata uit. De curve gebruikt de
catalogus-manawaarde in bereiken 0–<1, 1–<2, enzovoort, met 7+ als laatste groep.
Handmatige kaarten zonder catalogusmetadata zijn onbekend en worden niet als gratis
kleurloze kaarten met mana 0 voorgesteld.

Kaarttype gebruikt de voorzijde (vóór ` // `); de manawaarde komt uit de catalogus.
Dat is geen simulatie van alternatieve gezichten, splitkosten, X, reducties of speelkeuzes.
Kaarten met meerdere typen of kleuridentiteiten tellen bij elk passend type/kleur mee.
Kleuridentiteit is geen mana-symbolenverdeling of analyse van beschikbare manabronnen.
**Analyse en bezit vernieuwen** leest het huidige bezit opnieuw; navigeren naar Mijn decks
doet hetzelfde.

## Export

Selecteer een deck en open **Export → Decktekst maken**. Selecteer de tekst en kopieer
met Ctrl+C, of kies **Tekstbestand opslaan**. Export is beschrijvend; er worden geen
wildcards uitgegeven of accountacties uitgevoerd. Bestandsopslag accepteert `.txt`,
schrijft eerst een tijdelijk bestand naast het doel en vervangt het doel pas na succes.

De uitvoer bevat `About / Name`, vervolgens aanwezige commander-, deck- en sideboardsecties
met aantallen, volledige kaartnaam, set en collectornummer. Een leeg deck wordt geweigerd.
Een handmatige setcode/nummer die niet in deze notatie past wordt duidelijk geweigerd.
Taal en artvariant hebben geen apart veld: bij varianten kan opnieuw expliciet koppelen
nodig zijn. De tekst bewaart geen formaatlabel of interne GUID's. Er is geen volledige
databaseback-up of universele uitwisseling met alle deckbouwers beloofd.

## Bronnen en bewijsgrens

Voor de huidige rolherkenning bij alternatieven, inclusief onvolledige passages en
de geringe praktijkdekking, zie [fase 9: schade en modale opties](DAMAGE_AND_MODAL_ROLES.md).
Daarnaast bestaat vanaf fase 10 een afzonderlijke [mono-rode kaartpool](MONO_RED_POOL.md)
met expliciete deckfuncties. Die werkbank schrijft geen decks en reserveert geen voorraad.

[Wizards beschrijft import van tekst via het klembord](https://mtgarena-support.wizards.com/hc/en-us/articles/360049857771-Importing-a-Deck)
en [beschrijft het kiezen van verzamelde uitgaven bij import](https://mtgarena-support.wizards.com/hc/en-us/articles/32275576862484-Patch-Notes-2024-43-00).
De uitgebreidere sectie-/setnotatie is tevens beschreven in de
[primaire documentatie van een decktool](https://magicalpiles.com/docs/).
Dit is geen formeel, volledig door Wizards gepubliceerd bestandsprotocol.

Deze app heeft parser-, database-, UI- en export/herimport-tests. De volledige, echte
kaartcatalogus is in een geïsoleerde praktijkproef gebruikt. De tekst is niet binnen
een actuele, ingelogde Arena-client geïmporteerd. Het voorbeeld `examples/arena-deck.txt`
is een technische planninglijst, geen bewezen legaal of competitief deck.
