# Controleverslag fase 8

Uitgevoerd op **22 oktober 2026**, Windows x64, .NET SDK 10.0.401, versie **0.8.0**.
Fase 7 is geaccepteerd en fase 8 gestart met "Goed ga naar fase 8".
Scope: meer praktische tekstherkenning, expliciet onvolledige aanwijzingen en een
aparte gereserveerde evaluatie. Geen algemene semantische engine of win-rateclaim.

## Build en automatische controles

Definitieve opdracht, met werkelijk uitgevoerde restore, Release-build en schermtests:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase8-delivery
```

Resultaat: **0 waarschuwingen, 0 fouten; 255 geslaagde tests**, 0 mislukt/overgeslagen:

| Suite | Geslaagd | TRX onder artifacts/test-results |
| --- | ---: | --- |
| Domain | 28 | phase8-delivery_net10.0_20261022124626.trx |
| Application, SQLite-integratie en Avalonia UI | 227 | phase8-delivery_net10.0_20261022124647.trx |

`artifacts/build.log` bevat de builduitkomst. Nieuwe regressies toetsen volledige en
gedeeltelijke herkenning, behoud van beperkingen/extra effecten/activatiekosten,
ongeschikte contexten, nooit een sterke match bij gedeeltelijke herkenning en
voorrang voor volledige vergelijkingen binnen dezelfde categorie. Bestaande
voorraad-, rollback-, conflict-, actualiteits-, versie- en idempotentietests slagen.
De tests gebruiken uitsluitend geïsoleerde wegwerpdatabases.

Een eerdere volledige run vond een opruimrace: een asynchrone deckselectie hield de
testdatabase nog open tijdens verwijderen. TestDatabase wacht nu de lopende
LoadDeckCommand af vóór disposal/poolopruiming en controleert de tijdelijke doelmap.
Daarna slaagden twee volledige runs (254 vóór de laatste sorteerregressie, definitief 255).
Geen productiebeveiligingsbeleid is hiervoor veranderd.

## Aparte evaluatie en bewijsgrens

De bron is de eerder werkelijk geïmporteerde Scryfall-snapshot van
**2026-10-21T21:06:00.423Z**, met 5.164 Standard-legale kaartdefinities. Geen nieuwe
bulkdownload voor deze fase. `phase8-evaluation-selection.json` reserveert op
**10:31:20 UTC** dertig namen, zes per trefwoordstratum. Per stratum zijn de zes laagste
SHA-256-waarden van `phase8-eval-v1/OracleId` gekozen, zonder de oude referentienamen
en de eerder bekeken Consult the Star Charts, Dazzling Denial en Desculpting Blast.
Dit is bewust gestratificeerd, geen willekeurige representatieve steekproef.

Interpreter en vergelijking zijn op **10:41:02 UTC** bevroren vóór het bekijken van de
gereserveerde teksten. Vastgelegde en achteraf gecontroleerde SHA-256:

| Bestand | SHA-256 |
| --- | --- |
| src/MTGDeckbuilder.Application/CardRoles.cs | 167C7B1EAEB974E748AD431BEB915042A710CB43C86E130522E58C4E45E26521 |
| src/MTGDeckbuilder.Application/RoleTextInterpreter.cs | 083795724BB7CE1E7D87A6D033D43608A4A206718FBB168942BBE1FD9D95D049 |

`examples/role-evaluation-v1.json` bewaart de teksten, labels, voorwaarden en
beoordelingsnotities. Dezelfde ontwikkelaar labelde na de freeze, vóór het bekijken
van voorspellingen per kaart. Geen onafhankelijke beoordeling. De parser is daarna
niet op de evaluatieresultaten aangepast. De aanvullende sorteerregressie wijzigde
alleen de prioriteit van volledige vergelijkingen ten opzichte van aanwijzingen.

Definitief `artifacts/phase8-reserved-evaluation.json`: **0 volledig**, **4 onvolledig**,
**26 onbekend**; **15 gemist van 19 relevante kaarten** (herkenning inclusief
aanwijzingen **21,1%**). Geen verkeerde families bij vier aanwijzingen; alle
vereiste voorwaarden zijn aanwezig in de uitleg. Dit bewijst geen brede precisie
of bruikbaarheid. Het ontbreken van volledige voorspellingen maakt hun precisie
op deze set onmeetbaar. De geringe dekking is een wezenlijke beperking.

De ontwikkelset `role-reference-v2.json` geeft 24 volledig, 4 onvolledig en 2 onbekend.
`phase8-role-development-evaluation.json` rapporteert de bestaande tien
rangschikkingsscenario's: fase-6-eerste-keuze 3/10, huidige 10/10; MRR 0,583 → 1,000.
De historische JSON-veldnamen Phase7Top1/Phase7Mrr duiden hier de huidige rolrangschikking
aan. Dit zijn gekozen ontwikkelgevallen met gesimuleerd bezit/legaliteit, geen nieuw
bewijs voor algemene advieskwaliteit. De fase-7-referentie blijft apart bewaard.

## Definitieve praktijkproef

`artifacts/phase8-real-catalog-final.json` is op **10:49:32 UTC** gemaakt met de
definitieve assemblies en een nieuwe geïsoleerde database onder
`.local/phase8-smoke-data-final`. De bronkopie komt uit de eerdere gevulde fase-7-proef,
niet uit de echte gebruikersdatabase. De bron bevat 21.621 printings; bezit is gesimuleerd.

| Familie | Volledig | Onvolledige aanwijzing |
| --- | ---: | ---: |
| Counterspell | 11 | 20 |
| Removal | 16 | 73 |
| Draw | 11 | 74 |
| Mana-versnelling | 5 | 40 |
| Totaal | **43** | **207** |

**4.914 onbekend**, samen 5.164. Fase 7 herkende 16 volledig; nu 43 (circa 0,83%).
Dit zijn patroonuitkomsten, geen handmatige juistheidscontrole van alle gevonden kaarten.

De technische Negate-proef gebruikt 2.003 gesimuleerde bezitsregels en een deck met
60 Forest en 2 Negate. De lijst is een technische controle, geen competitief deck.
64 kandidaten zoeken duurde **0,222 seconde**; gezamenlijk voorbeeld 0,103 seconde,
opslaan 0,103 seconde; piekwerkgeheugen circa 126,5 MiB. Dit is één lokale meting.
De reprint staat eerst; Cancel volgt als passende counter. Essence Scatter wordt
gewaarschuwd wegens andere doelen. Reprint plus Essence Scatter zijn samen als
nieuw deck opgeslagen: geen restanttekort, formaatcontrole geslaagd volgens de bron,
origineel en bezit ongewijzigd. Herhalen retourneert hetzelfde deck. Twee oudere
toegepaste ontvangsten blijven hun bestaande resultaat retourneren.

## Schermen en echte Windows-start

De definitieve Avalonia-headlessbeelden zijn daadwerkelijk bekeken:

- `artifacts/ui/deck-roles-partial.png`: onvolledige aanwijzing, geen sterke match,
  herkende passage en niet beoordeeld token-effect zichtbaar.
- `artifacts/ui/deck-roles-partial-preview.png`: dezelfde waarschuwing in het
  gezamenlijke voorbeeld vóór opslag; resterend tekort blijft zichtbaar.

De overige roltoestanden zijn eveneens door de bestaande schermtests gerenderd.
Dit is renderbewijs, geen volledige handmatige bediening van alle native flows.

Native start is daarnaast werkelijk uitgevoerd met de definitieve Windows-executable:

| Database | Proces bij controle | Resultaat |
| --- | ---: | --- |
| Bestaande gevulde fase-7-proef | 18804 | Reagerend venster, v0.8.0-log en migratie-initialisatie; normaal gesloten |
| Normale lokale gebruikersdatabase | 296 | Reagerend venster, v0.8.0-log en migratie-initialisatie; open gelaten |

Start normale database: **12:49:59 Europe/Amsterdam**, initialisatie bevestigd om 12:50:01.
`phase8-synthetic-native-start.json` en `phase8-user-native-start.json` bewaren de
nieuwe logregels, tijdstippen en assemblyhashes; geen WRN/ERR/FTL in die startregels.

| Definitieve assembly | SHA-256 |
| --- | --- |
| Desktop | 16F5683D0FAEE2C2C8070950B595E95D245068E276B18AD12EE63EAF42005A7E |
| Infrastructure | 8177042CBE6E08FAE57EF5E7EE5B337DCBFC918A147734F6B3923B7AE5E78EF5 |

De Infrastructure-hash stemt overeen met de definitieve catalogusproef.

## Gegevensbehoud

Vóór en na beide native starts zijn 13 bestaande tabellen alleen-lezen gehasht:
collectie, decks, deckregels, instellingen, collectieimports/-regels, adviesplannen,
deckimports/-regels, kaarten, legaliteiten, printings en catalogusimports.
**Alle rijtellingen en alle 13 hashes zijn per database gelijk**.

De normale database bevat geen bezit of decks. Daarom is ook de bestaande gevulde
proef gecontroleerd: **2.003 bezitsregels, 6 decks, 16 deckregels en 2 adviesontvangsten**
blijven behouden. Geen nieuwe migratie; beide databases behouden zes migraties,
laatste `20261022090051_AddDeckAdvicePlans`. Back-upbestandslijsten vóór/na zijn gelijk.
Bewijs: `phase8-user-before/after.json`, `phase8-synthetic-before/after.json`,
`phase8-data-preservation.json` en `phase8-backups-before/after.json` onder artifacts.
De nieuwe adviesopslag is alleen in de afzonderlijke praktijkproef uitgevoerd.

## Grenzen en oplevering

De handleiding [ROLE_COVERAGE.md](ROLE_COVERAGE.md) beschrijft gebruik en grenzen.
Vier families, geen volledige spelregels, brede kaartkennis, bewezen synergie,
winkans, automatische Arena-sync of installer. Bron- en regelverval blijven gelden.
Geen nieuwe NuGet-pakketten, externe AI-API, secrets of beveiligingswijzigingen.

Modeladvies bij start: **GPT-6.1 Sol / High**; geen ondersteunde directe instellingstool
beschikbaar, dus geen model-/reasoningwijziging geclaimd. Raming vóór uitvoering:
45–90 actieve minuten, 90–150 bij lastige varianten. Werkelijke tijd wordt in
PROJECT_PLAN.md vastgelegd. Fase 8 wacht op beoordeling; fase 9 is niet gestart.

De solution en tests zijn reproduceerbaar met het script. Tijdelijke proefhelpers en
lokale meetbestanden staan buiten de solution, onder de genegeerde `.local`/`artifacts`.
De vaste ontwikkel- en evaluatievoorbeelden en hun tests worden wel meegeleverd.
Er is geen Git-repository of remote ingericht; geen commit, PR of CI-run geclaimd.

## Bestanden van deze fase

Nieuwe bestanden: `RoleTextInterpreter.cs`, `RoleTextInterpreterTests.cs`,
`RoleEvaluationTests.cs`, `examples/role-reference-v2.json`,
`examples/role-evaluation-v1.json`, deze validatie en `ROLE_COVERAGE.md`.
Gewijzigd: rolmodel/vergelijking en adviesrangschikking, deckscherm, versievermelding,
testfixtures/-project, verificatiescript en projectdocumentatie. Geen wijziging aan
Domain, databaseopslag of migraties in deze fase. Het volledige overzicht van
**140 bron-/configuratie-/voorbeeld-/documentatiebestanden** staat in [FILES.md](FILES.md).
