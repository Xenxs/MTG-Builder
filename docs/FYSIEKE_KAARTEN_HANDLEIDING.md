# Fysieke kaarten scannen en gebruiken

Voor MTG Smart Deckbuilder **1.3.0**. Scanner-/exportinstructies gecontroleerd op 27 oktober 2026; importgedrag in fase 16 ongewijzigd.
De telefoon herkent de kaarten; de Windows-app verwerkt het geëxporteerde CSV-bestand.
Er is geen rechtstreekse telefoonverbinding of automatische cloudsync.

## 1. Scannen in ManaBox

1. Open de kaartscanner in ManaBox. Leg kaarten afzonderlijk op een rustige,
   contrasterende ondergrond, met voldoende licht en zo weinig mogelijk reflectie.
2. Controleer de herkende **uitgave, collectornummer, taal en normal/foil**.
   Hetzelfde plaatje kan bij verschillende uitgaven voorkomen. Quick Mode uitzetten
   laat ManaBox bij twijfel om een uitgavekeuze vragen.
3. Stel het aantal fysieke exemplaren in. Controleer conditie en eventuele afwijkingen.
   Bewaar de scans als collectie/binder met een duidelijke, blijvende mapnaam.
4. Controleer de resultaten vóór export. Een deck- of verlanglijst is geen bewijs
   dat je die kaarten bezit. Registreer dezelfde kaartkopieën niet in meerdere binders.

De Windows-app corrigeert geen scanfouten op basis van een foto. De uitgave-afbeelding
is een hulpmiddel bij jouw controle, geen bewijs van echtheid of conditie.

## 2. Een CSV-bestand exporteren en overbrengen

Open **Collection/Collectie** in ManaBox en het menu rechtsboven. Kies de export-/deeloptie
voor **CSV**. Dit kan voor de hele collectie of vanuit een afzonderlijke binder/list.
De precieze menutekst kan per taal/versie verschillen. Kies een binder met echt bezit.

Bewaar het `.csv`-bestand op je Windows-pc, bijvoorbeeld via je eigen bestandenopslag,
kabel of een deelmethode naar keuze. Laat het bestand als UTF-8 CSV staan; vermijd openen
en opnieuw opslaan in Excel als daardoor nummers, quotes of scheidingstekens veranderen.

De standaard ManaBox-kolomnamen worden gebruikt. Vereist: **Quantity**, plus een
**Scryfall ID** of de combinatie **Set code + Collector number + Language**.
Alleen een kaartnaam is onvoldoende om een uitgave veilig te kiezen. Zonder exacte
identiteit blijft een regel onopgelost; je kunt hem in de app expliciet koppelen.

Voor andere scanners: gebruik dezelfde kolomnamen of zet de export bewust om naar
dit schema. Er is geen garantie voor willekeurige CSV-indelingen.

## 3. In de Windows-app controleren en toepassen

1. Open **Scan-CSV importeren** en kies het bestand. De app zoekt de exacte papieren
   uitgaven bij Scryfall op en maakt een voorbeeld. Bezit verandert nog niet.
2. Bekijk aantallen, taal, uitvoering, conditie en map. Dubbele regels voor precies
   dezelfde positie worden opgeteld; de samenvatting vermeldt dit.
3. Selecteer een regel en kies **Afbeelding van doelprinting bekijken**. Controleer
   de kaart rechts. De foto kan de juiste kaart en uitgave tonen, maar foil/conditie
   moet je zelf vaststellen.
4. Bij een probleem: zoek op **Collectie** naar de juiste kaart, selecteer
   de exacte uitgave, ga terug en koppel de regel expliciet. Of sla de regel bewust over.
   Een onmogelijke finish vereist correctie van de CSV. Proxy- en deck/list-regels
   kunnen niet als echt bezit worden gekoppeld.
5. Bevestig de volledige controle en klik **Fysiek bezit toepassen**. Alle betrokken
   wijzigingen en de ontvangstregistratie worden samen opgeslagen.

**Aantallen zijn absoluut per uitgave/taal/finish/conditie/map.** Dezelfde export opnieuw
importeren verdubbelt die positie niet. Regels uit verschillende mappen zijn verschillende
posities en worden voor speelbaar bezit samengeteld. Niet meegeleverde posities blijven
bestaan; dit is geen volledige vervanging van de collectie. Verwijderde/verplaatste posities
moet je met aantal 0 corrigeren, anders kan oud bezit blijven staan. Een hernoemde binder
wordt een nieuwe map: corrigeer de oude registratie om dubbeltelling te voorkomen.

De eerste CSV-/handmatige registratie maakt de eigen voorraad actief. De aangenomen
Conflux-voorraad blijft opgeslagen maar telt dan niet mee. Zo worden voorbeeldkaarten
niet bij echte scans opgeteld. Een handmatig aantal hoort bij map **Handmatig** en
wijzigt geen eerder geïmporteerde binder.

## 4. Historie, opnieuw proberen en herstel

Onder de importregels vind je recente imports. Open het historische voorbeeld om de
bronvelden en koppelingen terug te zien. Bevestig **Import herstellen** om de eerdere
aantallen terug te zetten. De app weigert dit als betrokken posities later gewijzigd
zijn, ook wanneer het aantal intussen weer hetzelfde is geworden.

Een gewijzigde voorraad na voorbereiding blokkeert toepassen: bereid de CSV opnieuw voor.
Bij een fout wordt de import niet half toegepast. Reeds opgehaalde kaartgegevens mogen
wel in de cache staan. Een voorbereide import blijft na een herstart in de historie staan.

Maak vóór een grotere import via **Instellingen → Nu een back-up maken** een extra kopie.
Herstel van een volledige back-up vervangt de hele database, inclusief Arena-archief;
de app maakt vóór werkelijk herstel een veiligheidskopie.

## 5. Conflux-voorbeelden en fysieke decks

Zonder eigen registratie start de app met **Naya Domain** en **Bant on the March**.
Dit zijn historische introdecks: elk **41 vaste kaarten**, samen **82 exemplaren** in
**54 exacte uitgaven**. De twee foil-promokaarten zijn afzonderlijk geregistreerd.
Onbekende kaarten uit een destijds meegeleverde booster worden niet toegevoegd.
Het voorbeeld is aangenomen testbezit, geen werkelijk uitgevoerde telefoonscan.

Je kunt ook `examples/manabox-conflux.csv` importeren om de volledige scanroute te
oefenen. Dat bestand is gegenereerd uit gepubliceerde productlijsten. Het imiteert het
CSV-schema, niet een echte export uit jouw telefoon. Gebruik hiervoor bij voorkeur
een geïsoleerde gegevensmap en voeg het niet bij een andere registratie van dezelfde kaarten.

Open **Fysieke decks**, kies een productdeck en laad het. Voor wijzigingen maak je eerst
een kopie met een eigen naam. Controleer en bevestig het voorbeeld; sla het als nieuw
deck op. De oorspronkelijke productlijst blijft bij kaartbewerkingen behouden.

Selecteer daarna een kaart uit collectie/Scryfall en sla het gewenste aantal en de zone
op. Aantal 0 verwijdert die deckregel. Decks wijzigen geen bezit en reserveren geen
exemplaren tegenover andere decks. Tekorten tellen dezelfde Oracle-kaart samen over
uitgaven, talen en uitvoeringen. Dit betekent dat een andere uitgave voor spelen bruikbaar
kan zijn; de decklijst zelf bewaart de gekozen exacte uitgave.

**Casual** controleert de gekozen 60-kaartstructuur, kopielimieten en bezit. Je kunt
onderling met een introdeck spelen, maar de app maakt er geen volledig constructed
deck van. **Standard fysiek** gebruikt actuele Scryfall-legaliteit; Conflux uit 2009 is
geen actuele Standard-set. Een oude kaart met een geldige herdruk kan wel legaal zijn.
Vernieuw alle deckkaarten online om de bron opnieuw te controleren. Regelonderhoud en
bijzondere kaartgebonden uitzonderingen blijven nodig.

Decktekst gebruikt `aantal Engelse kaartnaam (SET) nummer`, eventueel met `Deck` en
`Sideboard`. Kies bij import expliciet de taal. Bekijk het voorbeeld en sla als nieuw
deck op. Bij onzekere identiteit wordt niets als deck opgeslagen. Een deckvoorbeeld
is tijdelijk: na herstart bereid je het opnieuw voor. Opgeslagen decks blijven bestaan.

Alternatieven zijn beperkt tot herkenbare rollen of een andere uitgave van dezelfde
kaart. Ze zijn geen bewezen strategische vervangers. Een vervangingskopie controleert
de gezamenlijke voorraad opnieuw. De beschrijving van domain/exalted is geen
synergiesimulatie, castingkans of voorspelling van speelsterkte. De oude mono-rode
Arena-generator staat alleen in het archief; fysiek is een begrensd Casual60-Conflux-plan beschikbaar.

## 6. Verbinding en afbeeldingen

Internet is nodig voor nieuwe kaarten, zoekopdrachten, actualisering en nog niet
gecachete afbeeldingen. Bestaand bezit en decks zijn lokaal beschikbaar. Afbeeldingen
worden per uitgave en zijde gecachet. Bij fouten blijft de kaartinformatie beschikbaar
en toont de app een melding. Dubbelzijdige kaarten hebben een schakelaar voor de achterzijde.

Een zoekopdracht toont maximaal de eerste 40 uitgaven; verfijn bijvoorbeeld met
`set:con` of een kaartnaam. De app haalt geen volledige wereldwijde catalogus binnen.

## Bronnen

- [ManaBox scanner](https://manabox.app/guides/scanner/getting-started/)
- [ManaBox collectie-export](https://manabox.app/guides/collection/import-export/)
- [Naya Domain-productlijst](https://mtgjson.com/api/v5/decks/NayaDomain_CON.json)
- [Bant on the March-productlijst](https://mtgjson.com/api/v5/decks/BantOnTheMarch_CON.json)
- [Scryfall API](https://scryfall.com/docs/api) en [kaartafbeeldingen](https://scryfall.com/docs/api/images)
- [Wizards Standard-formaat](https://magic.wizards.com/en/formats/standard)

De ManaBox-handleiding en productlijsten zijn gelezen; de echte telefoon → ManaBox →
Windows-keten is nog niet uitgevoerd. Netwerk-, CSV-, opslag- en herstelproeven zijn
wel uitgevoerd met echte Scryfall-gegevens en expliciet gesimuleerd bezit.

## 7. Een fysiek 60-kaartvoorstel maken

Open Fysieke decks en geef een naam op. Laat Planning uitgeschakeld voor **bezitmodus**.
Kies **60-kaart Conflux-voorstel voorbereiden**. Bekijk de volledige voorgestelde lijst,
mana, landtypen, voorraad en problemen. Bevestig onderaan het deckvoorbeeld en sla op
als nieuw deck. Je collectie en de twee productdecks blijven behouden.

Dit vaste constructievoorbeeld kiest **36 niet-landen en 24 basislanden** uit de
gecontroleerde Conflux-productpool. Het gebruikt eerst lagere gedrukte manawaarden en
de beschikbare basislandtypen, daarna een eenvoudige verhouding van manasymbolen voor
extra basislanden. Dat is een uitlegbare sorteermethode, geen bewijs van een goede
manabase, optimaal deck, strategie of speelsterkte. Het mengt kaarten uit beide productdecks.

Onvoldoende geschikte voorraad blokkeert bezitmodus. Met expliciete **Planning** mag
de app ontbrekende kaarten opnemen; die tekorten blijven zichtbaar en worden nooit als
bezit toegevoegd. Het profiel is **Casual60**, geen huidig Standard. Nieuwe kaarten buiten
de bevroren Conflux-pool worden nog niet automatisch gebruikt. Gewijzigde Oracle-metadata
sluit de betreffende kaart uit totdat de pool wordt onderhouden. Een voorstel is tijdelijk;
een opgeslagen voorstel is een normaal fysiek deck met actuele tekortcontrole.

## 8. FINAL FANTASY Starter Kit en Commander

De startercollectie bevat nu ook Cloud en Sephiroth: elk 60 kaarten, samen 120 extra.
Totaal met Conflux: **202 aangenomen speelkaarten**, vier productdecks en 109 uitgaven.
De vier dubbelzijdige tokens zijn hulpmiddelen en worden apart vermeld; niet bij de
100/60 deckkaarten of de geregistreerde speelkaartvoorraad opgeteld. Arena-codes tellen
niet mee. Een tweede seedmarker voegt de Starter Kit eenmaal toe; bestaande kaarten,
voorraadaantallen en productdecks worden niet opnieuw gemaakt of overschreven.

Gebruik `examples/manabox-physical-starters.csv` voor alle vier producten, of
`examples/manabox-final-fantasy-starter.csv` voor alleen de Starter Kit. Dit blijven
gegenereerde testbestanden, geen echte telefoonexports.

Voor een Commander-deck: zie [COMMANDER.md](COMMANDER.md). Kies een commander en laat
Planning uit voor eigen bezit. De 202 kaarten bevatten duplicaten en verschillende
kleuren; ze garanderen geen volledig Commander-deck. Schakel Planning alleen bewust in
om ontbrekende passende kaarten via Scryfall te laten meenemen. Controleer altijd de
volledige voorgestelde lijst en tekorten vóór opslag. De app koopt geen kaarten en
voegt geplande kaarten niet aan bezit toe.

Ook wijziging van taal, finish of conditie maakt een andere voorraadpositie. Bij een
correctie/verplaatsing moet de oude positie dus bewust op nul worden gezet; alleen een
nieuwe export met een andere identiteit verwijdert de eerdere registratie niet.

## Fase 15 — actuele interface en wizard

In v1.2.0 heet de navigatie **Mijn collectie**, **Kaarten importeren**, **Mijn decks**,
**Deckwizard** en **Instellingen**. Het eerdere Arena-archief is uit de app verwijderd.
Bestaande gegevens blijven intern behouden voor upgrades en back-ups; zij zijn geen
fysiek bezit. Historische beschrijvingen hierboven van een archieftab zijn vervallen.

Collectie heeft combineerbare filters op kaartkleur, synergie-aanwijzing, type en serie.
De nieuwe wizard gebruikt de actieve fysieke collectie en kan ook buiten de vaste
Conflux-pool voorstellen maken. Een expliciete planningsmodus vult via een begrensde
Scryfall-pool aan; geen bezit toevoegen. Voor Casual-planning gebruikt die aanvulling
normale Commander-legale papieren kaarten als afgebakende pool; dit is geen algemene
Casual-legaliteitsclaim. Bekende eigen kaarten in Casual worden niet zo beperkt.

Zie [filters, wizard en database legen](MAGIC_WIZARD_HANDLEIDING.md). Database legen
maakt eerst een verplichte reservekopie en schakelt automatische voorbeeldvulling uit.
Deze actie is optioneel; een gewone upgrade verwijdert geen gegevens.
