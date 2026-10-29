# Verificatie fase 4 — 22 oktober 2026

Versie **0.4.0**. Fase 3 is geaccepteerd met "goed ga door"; daarmee is fase 4 gestart.
Er is geen fase 5 uitgevoerd. De zes solutionprojecten en bestaande NuGet-versies blijven.

## Definitieve build en tests

Uitgevoerd op Windows x64, .NET SDK 10.0.401:

```powershell
./scripts/Verify.ps1 -SkipRestore -Screenshots -ReportPrefix phase4-delivery
```

De reeds herstelde afhankelijkheden zijn gebruikt; geen nieuwe pakketten nodig.
**Release-build geslaagd met 0 waarschuwingen/fouten; 134 tests geslaagd, 0 mislukt,
0 overgeslagen.** Buildlog: `artifacts/build.log`.

| Project | Geslaagd | Exact resultaatbestand |
| --- | --- | --- |
| Domain.Tests | 21 | artifacts/test-results/phase4-delivery_net10.0_20261022094815.trx |
| Application.Tests inclusief SQLite/UI | 113 | artifacts/test-results/phase4-delivery_net10.0_20261022094828.trx |

Tests draaiden buiten de sandbox vanwege Windows-testhostcommunicatie. Geen Windows-
beveiligingsbeleid aangepast. Geen externe CI-run, Git-repository of PR aangemaakt.

## Wat daadwerkelijk is gecontroleerd

- Domeinstatussen, commander-zone en ontvangstregistratie: één voorbereiding maakt
  hoogstens één deck, ook na verwijderen van dat deck; verwerpen verhindert toepassen.
- Parser voor namen, `(SET) nummer`, About/Name, mainboard/sideboard/commander,
  regelafbrekingen, BOM, Unicode, dubbelzijdige namen en haakjes in gewone kaartnamen.
- Ongeldige aantallen/metadata, Companion, Maybeboard, lege invoer en alle invoerlimieten
  worden geweigerd. Er worden geen regels stilzwijgend overgeslagen.
- Voorbereiden zonder bezit/deckwijzigingen; voorbereiding, toepassing en inhoud blijven
  na serviceproviderherstart behouden. Bestaande decks/bezit blijven ongewijzigd.
- Naam-only-koppeling bij één geverifieerde Oracle-identiteit, voorkeur voor bezit,
  expliciete variantkeuze, oude keuzeversies en gewijzigde catalogusidentiteiten.
- Dubbele regels tellen per printing/zone op; hoeveelheidsconflicten worden over alle
  pagina's berekend en blokkeren toepassen. Zones blijven apart.
- Twee onafhankelijke DI-providers kunnen dezelfde voorbereiding niet tweemaal toepassen.
- Fouten/annulering na echte SQL-writes maar vóór commit laten geen gedeeltelijk deck,
  deckregels of onjuiste ontvangstregistratie achter.
- Tekorten combineren vraag over alle zones; voorraad wordt niet per zone dubbel gebruikt.
  Reprints tellen uitsluitend met dezelfde Oracle-ID mee; exacte en handmatige identiteiten
  blijven apart. Gewijzigd bezit wordt bij opnieuw analyseren gelezen.
- Curve en gemiddelde sluiten landen en onbekende metadata uit; sideboard is geen
  mainboard; overlappende typen/kleuren, meervoudige kaarten en een leeg deck zijn getest.
- Export/herimport behoudt gekoppelde kaarten, hoeveelheden en zones; ongeldige handmatige
  printingcodes produceren geen corrupte tekst.
- Een gevulde fase-3-database migreert met behoud van bezit, deckzones en collectiehistorie,
  maakt een back-up en accepteert daarna commander-regels.
- Een foutinjectie na copy/drop/rename van DeckCards bewijst dat de definitieve rebuild
  in een transactie draait: oude deckregels en drie migratieregistraties blijven intact.
  Opnieuw uitvoeren slaagt en commander kan vervolgens worden opgeslagen.
- Alle zeven navigatiepagina's renderen. Echte tekst-, selectie-, bevestigings- en
  commandobindingen koppelen een onbekende bronregel en maken een deck zonder bezit te geven.
- De editor plant een niet-bezeten cataloguskaart als commander; analyse en export werken
  via de gebonden schermen en het exportbestandscommand.
- Gewijzigde broninvoer blokkeert toepassen en blijft bij vernieuwen/navigeren staan.
  Annuleren blijft beschikbaar en de busy-status herstelt.

## Gevonden en opgeloste problemen

De eerste build kon DLL's van de nog geopende fase-3-app niet vervangen. Die door de
agent gestarte instantie is normaal gesloten; daarna bouwde de solution zonder waarschuwing.

De eerste native fase-4-upgrade om circa 09:46 gebruikte de gegenereerde EF-rebuild en
waarschuwde over `PRAGMA foreign_keys = 0` buiten de migratietransactie. De upgrade
slaagde met back-up en behouden gebruikersgegevens. Vóór oplevering is de nieuwe,
nog niet uitgeleverde fase-4-migratie verfijnd: DeckCards is een leaf-tabel en wordt
met foreign keys ingeschakeld binnen één transactie gekopieerd, vervangen en geïndexeerd.
Schema-uitkomst en migratie-ID blijven gelijk; de drie eerdere migraties zijn niet gewijzigd.

`artifacts/phase4-migration.sql` bevat één BEGIN/COMMIT en geen uitschakeling van foreign
keys. De rollbacktest en een nieuwe native upgrade van een aparte gevulde fase-3-database
bevestigen de definitieve variant. Geen gebruikersdatabase teruggezet of gedowngraded.

## Praktijkproef met echte kaartcatalogus

Een aparte database gebruikte de echte Scryfall-snapshot van 21 oktober 2026 uit fase 2:
**21.621 Arena-printings, 16.399 kaartdefinities**. Deck en bezit zijn gesimuleerd.

| Controle | Gemeten resultaat |
| --- | --- |
| Voorbeeldlijst | 11 regels; 60 mainboardkaarten en 2 sideboardkaarten |
| Eerste koppeling | 0 ongekoppelde regels |
| Analyse | 24 landen, 36 niet-landen, 0 onbekende metadata |
| Voorbereiden inclusief preview | circa 0,326 seconde |
| Toepassen | circa 0,030 seconde |
| Analyse na bezitaanpassing | circa 0,0014 seconde |
| Gemiddelde manawaarde niet-landen | 2,3333 |
| Reprintproef | vraag 4 Llanowar Elves, bezit 2 gekozen + 2 andere uitgave: kaarttekort 0, exact tekort 2 |
| Export/herimport | kaarten, aantallen en zones gelijk; bezit onveranderd |
| Piekgeheugen | circa 246 MiB, inclusief catalogusimport in hetzelfde proces |

Bewijs: `artifacts/phase4-deck-smoke.json`. Lokale metingen, geen algemene prestatiegaranties.
Het voorbeeld is technische testinvoer, geen bewezen legaal of competitief deck.
Niet getest binnen een actuele ingelogde Arena-client.

## Native migratie en definitieve appstart

Om circa **09:49 Europe/Amsterdam** startte de definitieve executable met een aparte
bestaande fase-3-smokedatabase: 2 bezitregels, 1 deck, 1 deckregel, 2 collectie-imports
en 4.001 bron-/auditregels. Alle voor/na-aantallen en hashes zijn gelijk; vier migraties
toegepast, geen waarschuwingen/fouten in het applicatielog. De tijdelijke instantie
(PID 31676) is normaal gesloten. Dit is testdata, geen kopie van de gebruikerscollectie.
Bewijs: `phase4-migration-native-proof.json`, `phase4-synthetic-before.json` en
`phase4-synthetic-after.json` onder artifacts.

De definitieve v0.4.0 is om circa **09:50 Europe/Amsterdam** met normale opslag gestart:

- PID **26440**, titel `MTG Arena Smart Deckbuilder`, `Responding = true`.
- Desktop-DLL SHA-256: `31067C29CA855D3836A3E292B8B1B4A88C65795690ED44A07D843E10DF114639`.
- Infrastructure-DLL SHA-256: `61F4C79F3855AB90CE3820589A0C2080A06816E79EDD9E2E30C861D335CE0F02`.
- Vier migraties, inclusief `20261022073037_AddDeckWorkspace`; log bevestigt initialisatie.

De normale database kreeg bij de eerste ontwikkelstart een upgradeback-up van 33.325.056
bytes: `pre-migration-20261022-074610-063-ab239c7684634325a43ab5f401ad9005.db`. Deze was
tijdens de draaiende app leesbaar; hash staat in `artifacts/phase4-backup.json`.

Voor/na-controles van bezit, decks, deckregels en instellingen komen overeen. Deze
gebruikerstabellen waren leeg; gevuld gegevensbehoud is apart getest. Geen voorbeelddeck
of testbezit aan de normale database toegevoegd. Bewijs: `phase4-native-start.json`,
`phase4-user-before.json`, `phase4-user-after.json`, `phase4-user-preservation.json` en
applicatielogs. De eerste ontwikkelstart staat in `phase4-development-upgrade-native-start.json`.

## Visueel bekeken en grenzen

Werkelijk bekeken onder artifacts/ui: `deck-import-preview.png`, `deck-transfer.png`,
`decks.png`, `deck-editor-unowned.png`, `deck-analysis-missing.png`, `deck-export.png`,
`dashboard.png` en `settings.png`. Screenshots gebruiken geïsoleerde testdata.
Geen volledige handmatige bestandspicker-, muis-, toegankelijkheids- of DPI-matrix.

Geen Companion/Maybeboard, legaliteit, wildcardkosten, slimme aanbevelingen, live Arena-sync
of voorraadreservering tussen decks. Tekst bewaart geen taal/artvariant/GUID/formaatlabel.
Basisland-unlocks en variabele kosten worden niet gesimuleerd. Historie heeft geen opruimfunctie.
Gebruik en exacte betekenis staan in DECKS.md. Fase 5 is niet gestart.
