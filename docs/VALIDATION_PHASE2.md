# Validatie fase 2 — versie 0.2.0

Datum: 21 oktober 2026, Europe/Amsterdam. Fase 2 is expliciet door de gebruiker gestart.
Aanbevolen configuratie: GPT-6.1 Sol, High reasoning. De chatinstelling is niet automatisch
gewijzigd. Vooraf geraamd: 60–120 minuten actieve uitvoering; de werkzaamheden zijn in
circa 70–75 minuten verstreken uitvoering binnen die bandbreedte afgerond.

## Definitieve controles

| Controle | Werkelijk resultaat |
| --- | --- |
| NuGet restore | Alle zes solutionprojecten hersteld |
| Release-build | Geslaagd, nul fouten en nul waarschuwingen |
| Domain.Tests | 16 geslaagd, 0 mislukt, 0 overgeslagen |
| Application.Tests | 39 geslaagd, 0 mislukt, 0 overgeslagen |
| Totaal automatische tests | **55 geslaagd** |
| Windows-app | Laatste start op 21 oktober 2026 om 22:10:35 +02:00; venster bestaat en proces reageert |
| Migraties | InitialCreate plus AddCatalogAndImportHistory toegepast |
| Upgradeback-up gebruikersdatabase | Aangemaakt vóór de migratie, bestandsgrootte 90.112 bytes |
| Visuele controle | Lege en gevulde Catalogus plus de bestaande schermen gerenderd en bekeken |
| Beveiligingsbeleid | Geen beleidswijziging door de agent uitgevoerd |

Reproduceerbaar vanuit de projectmap:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase2-final
```

## Bewijsbestanden

- artifacts/build.log — geslaagde volledige Release-build.
- artifacts/test-results/phase2-final_net10.0_20261021220742.trx — 16 domeintests.
- artifacts/test-results/phase2-final_net10.0_20261021220748.trx — 39 overige tests.
- artifacts/phase2-real-import.json — eerste echte online-import in een geïsoleerde database.
- artifacts/phase2-user-import.json — import in de lokale gebruikersdatabase, vóór-/natotalen en bronidentiteit.
- artifacts/phase2-native-start.json — native venstercontrole en aangetroffen upgradeback-up.
- artifacts/ui/catalog.png en catalog-populated.png — lege en gevulde echte Avalonia-view.
- artifacts/ui/catalog-real.png — de echte gebruikerscatalogus met 21.621 printings en de
  Foundations-printing van Llanowar Elves, gerenderd zonder collectieaantallen te wijzigen.
- artifacts/ui/dashboard.png, collection.png, decks.png en settings.png — bestaande schermen na de uitbreiding.

De artifacts-map bevat ook eerdere testpogingen. Alleen de twee hierboven exact genoemde
fase2-final-rapporten zijn de definitieve testrapporten. De Windows CI-workflow is aanwezig;
er is geen remote CI-run of publicatie uitgevoerd.

## Echte Scryfall-import

Via https://api.scryfall.com/bulk-data/default_cards is de actuele bron gecontroleerd.
Het manifest bevatte jsonl_download_uri en compressed_size; de bron bedroeg 79.946.730 bytes
(circa 76,2 MiB) gecomprimeerde JSONL. De API-documentatie is rechtstreeks over HTTPS gelezen.

| Waarde | Gemeten resultaat |
| --- | --- |
| Bronrecords verwerkt | 119.106 |
| Arena-printings opgenomen | 21.621 |
| Oracle-kaartdefinities | 16.399 |
| Eerste geïsoleerde import | circa 17,8 seconden, inclusief downloaden |
| Import gebruikersdatabase | circa 8,9 seconden, inclusief downloaden |
| Herhaalde online-controle | AlreadyCurrent = true, geen nieuwe download/import |
| Piekgeheugen diagnostische import | circa 216 MiB in de gebruikersdatabasecontrole |
| Voor-/nabezit en decks gebruikersdatabase | gelijk: 0 unieke bezitsprintings, 0 exemplaren, 0 decks |

Dit zijn metingen op deze machine en deze bronversie, geen algemene doorlooptijdgaranties.
De gebruikersdatabase bevatte op dat moment geen bezits- of deckregels. Behoud van werkelijk
gevulde gegevens is aanvullend bewezen in de automatische migratie- en importtests.

Bronversie: e2ef41e3-5778-4bc2-af3f-78eca4dd9c23 met tijdstip
2026-10-21T09:05:44.5910000+00:00.
SHA-256 van de gecachete bron:
90AEAE84A92706D8C192B18FE217C4F28B341252A6719F94CB3E04501E2FB373.

De diagnostische import gebruikte dezelfde DI-registraties, migraties, bronclient,
parser en importer als de desktopapp. Het tijdelijke diagnoseprogramma staat onder
.local en is geen zevende solutionproject of noodzakelijke distributieafhankelijkheid.

## Wat de tests daadwerkelijk controleren

- Bestaande fase-1-invarianten en gebruikersstromen blijven werken.
- Een werkelijk fase-1-schema met collectie, deck en instellingen wordt inclusief back-up
  opgewaardeerd zonder sleutels, namen of aantallen te wijzigen.
- JSON-array, JSONL en gzip worden gelezen; niet-Arena-records worden overgeslagen.
- Herimporteren en herstarten behouden printing-ID's, bezit, decks en instellingen.
- Eenduidige handmatige printings worden gekoppeld; onzekere invoer blijft apart en wordt gemeld.
- Reprints, varianten, talen en dubbele Arena-ID's blijven via bronidentiteiten onderscheidbaar.
- Dubbelzijdige kaarttekst wordt samengevoegd tot leesbare details.
- Annuleren na een checkpoint en tijdens toepassing behoudt de oude catalogus. Hervatten na
  het opnieuw opbouwen van de serviceprovider voltooit de import zonder duplicaten.
- Beschadigde cache, afgekapt JSON, onvolledige HTTP-download, lege Arena-selectie en een
  tweede gelijktijdige import worden geweigerd zonder de huidige catalogus te vervangen.
- Achtergebleven actieve importstatus wordt bij herstart Onderbroken; dit scenario wordt
  met opgeslagen status gesimuleerd, niet met een geforceerde Windows-proceskill.
- Naam, type, set, kleuridentiteit, rarity, mana en bezitfilters kunnen gecombineerd worden.
  Paginering en Unicode-normalisatie worden met meerdere resultaatpagina's gecontroleerd.
- De online versiecontrole slaat een actuele online snapshot over en herstelt de officiële
  catalogus juist wel na een andere lokale snapshot.
- Ververste canonical kaarttekst werkt door naar alle reprints zonder bezitaantallen te wijzigen.
- Alle vijf views renderen; catalogusinvoer, zoeken via de gebonden TextBox, collectie opslaan
  en de actieve pauzeknop werken via de echte viewmodels.

De eerste uitgebreide testronde vond een niet-vertaalbare EF-query voor de setlijst. De
query is gecorrigeerd en de volledige solution is daarna opnieuw succesvol getest.

## Grenzen en resterend werk

Geen Arena-collectie-import/logparser, legaliteitscontrole, deckadvies, kaartafbeeldingen,
cloudsync, installer of periodieke back-up. Onzekere handmatige matches kunnen nog niet
via een conflictresolver worden samengevoegd. Lokale import is een volledige snapshot
van de zichtbare Arena-selectie, geen incrementele toevoeging van willekeurige losse kaarten.
Een afgebroken download wordt opnieuw gedaan; hervatten begint pas vanuit een complete cache.

De parser streamt de bron; de toepasstap houdt Arena-records en identiteitsmappen in geheugen.
Limieten: 512 MiB bronbestand, twee miljoen bronrecords en 200.000 Arena-records per import.
Onvoltooide importcaches/staging blijven voor hervatten staan en kunnen ruimte innemen.
De UI toont de twintig meest recente imports. Er is geen cachebeheer- of herstel-UI.
Automatische upgradeback-ups blijven staan en moeten naar behoefte apart worden beheerd.

Meerdere appinstanties hebben geen optimistische conflictcontrole voor gelijktijdige normale
bewerkingen; imports worden wel exclusief gehouden. Headless tests en een native appstart
zijn geen volledige testmatrix voor alle DPI-, muis- en toegankelijkheidsinstellingen.
Fase 3 is niet gestart.
