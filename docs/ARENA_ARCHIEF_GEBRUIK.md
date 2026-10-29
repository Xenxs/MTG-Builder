# MTG Arena Smart Deckbuilder

Windows-desktopapplicatie met .NET 10, Avalonia, MVVM, EF Core/SQLite en Serilog.
Versie 1.0.0 levert de lokale basis, Arena-catalogus, collectieoverdracht en deckplanning
met tekstimport/export, tekorten, beschrijvende analyse en uitlegbare controle voor
Arena Standard BO1/BO3 en kaartalternatieven met beperkte rolherkenning uit eigen collectie. De zes projecten uit fase 1
zijn behouden.

Nieuw: **handmatige databaseback-ups en gecontroleerd herstel bij herstart**, met
herstelvoorbeeld, expliciete bevestiging, automatische veiligheidskopie en controle
van schema/integriteit. Daarnaast complete mono-rode BO1-deckvoorstellen uit de beoordeelde pool: een vast
plan van 60 kaarten, geregistreerd bezit, expliciete planning met tekorten, mana/curve,
kaartvoorwaarden en hercontrole vóór opslag als nieuw deck. De pool omvat 26 niet-landen,
Mountain en drie bewuste uitsluitingen. De generator bewijst geen speelsterkte.

## Starten

Voor gebruikers: gebruik de Windows x64-installer of pak de portable zip volledig uit.
Beide bevatten de runtime. De installer is per gebruiker en de release is ongetekend.
Zie [RELEASE_GUIDE.md](docs/RELEASE_GUIDE.md) voor installatie, upgrade, verwijderen en
de expliciete grenzen van deze beperkte v1.0. Onderstaande stappen zijn voor ontwikkelen.

Vereist: Windows x64 en een .NET 10 SDK. Internet is nodig voor de eerste NuGet-restore.
Open PowerShell in de projectmap:

```powershell
dotnet restore MTGDeckbuilder.sln
dotnet run --project src/MTGDeckbuilder.Desktop -c Release
```

Na een Release-build kun je ook `src/MTGDeckbuilder.Desktop/bin/Release/net10.0/MTGDeckbuilder.Desktop.exe`
starten. Dit is een framework-dependent build: op een andere computer is de .NET 10-runtime nodig.
De zelfstandige distributie bouw je met `scripts/Package.ps1`; de ontwikkelbuild hierboven
blijft framework-dependent.

## Gebruik

Kaarten vanuit Arena meenemen: [stapsgewijze export- en importhandleiding](docs/ARENA_EXPORT_HANDLEIDING.md).
Deze aanvulling bevat een afzonderlijk omzettingsscript; de eerder geleverde installer is ongewijzigd.

- **Dashboard:** aantallen en je opgeslagen spelersnaam.
- **Mijn collectie:** kaartnaam, setcode en collectornummer invoeren. Opslaan vervangt
  het bezitsaantal voor dezelfde printing. Selecteer een regel om het aantal te wijzigen.
  Aantal 0 behoudt de printing voor toekomstig gebruik en in bestaande decks.
- **Catalogus:** Arena-kaarten zoeken, filteren en de exacte printing kiezen. Het
  collectieaantal opslaan vervangt alleen het bezit van die printing. De catalogusimport
  zelf voegt geen kaarten aan je bezit toe.
- **Collectie-import:** JSON-bezitaantallen voorbereiden, koppelingen controleren,
  toepassen, exporteren en zo nodig herstellen. Dit is geen automatische Arena-koppeling.
- **Mijn decks:** deck bouwen met bezit of cataloguskaarten, inclusief commander;
  tekorten, manacurve, kaarttypen en kleuridentiteiten bekijken; decktekst exporteren.
  Een herhaalde handmatige invoer vervangt het aantal in die zone. Deckplanning wijzigt
  geen bezit. Deck verwijderen vraagt bevestiging.
  De tab **Formaatcontrole** bewaart een expliciet BO1-/BO3-profiel en toont controles,
  problemen, herstelrichtingen en de gebruikte brondatum.
  **Alternatieven** zoekt vervangingen voor ontbrekende kaarten uit je collectie,
  toont redenen en kaartteksten en bewaart jouw keuzes na controle als nieuw deck.
- **Deck importeren:** Arena-tekst plakken of een tekstbestand kiezen, koppelingen
  controleren en een nieuw deck opslaan. Bestaande decks en bezit blijven staan.
- **Instellingen:** spelersnaam opslaan, database-/loglocatie bekijken, een back-up
  maken en een gecontroleerde back-up na expliciete bevestiging bij herstart terugzetten.
  Terugzetten vervangt de hele database; eerst wordt de nieuwste toestand veilig gekopieerd.
- **Mono-rode kaartpool:** bronstatus en bezit controleren, beoordeelde functies en
  voorwaarden bekijken en geschikte andere poolkaarten uit je bezit zoeken. Geen
  voorraadreservering of automatische deckwijziging. Een ontbrekende/verlopen bron
  of gewijzigde kaartmetadata blokkeert pooladvies.
- **Deckvoorstel:** berekent het vaste mono-rode BO1-plan van 22 Mountains en 38 spreuken.
  Standaard uitsluitend geregistreerd actueel bezit; ontoereikende functies blokkeren.
  Kies expliciet planningsmodus voor een lijst met ontbrekende kaarten. Bekijk lijst,
  curve en voorwaarden, geef een naam, controleer het voorbeeld en sla op als nieuw deck.
  Mijn decks biedt daarna de bestaande formaatcontrole en Arena-tekstexport.

De kaartnaam bij een bestaande setcode/collectornummer moet overeenkomen. Bij meerdere
varianten gebruik je Catalogus om de exacte printing te kiezen. Handmatige invoer uit
fase 1 blijft beschikbaar en behouden.

## Catalogus importeren

1. Open **Catalogus → Catalogus importeren / bijwerken**.
2. Kies **Bijwerken via Scryfall**. De app controleert de bronversie, downloadt zo nodig
   het actuele bulkbestand en verwerkt alleen records met `arena` in het bronveld `games`.
3. Bij voltooiing verschijnen de printings in de zoekresultaten. Filters: naam, kaarttype,
   set, kleuridentiteit, zeldzaamheid, maximale manawaarde en alleen in bezit. Druk na
   het wijzigen van filters op **Zoeken**. Resultaten worden per 50 getoond.
4. Selecteer een printing, voer het aantal in en kies **Collectieaantal opslaan**.

**Lokaal bestand kiezen** accepteert Scryfall JSONL, een JSON-array en gzip-varianten.
Dit bestand is een catalogussnapshot: de Arena-selectie vervangt de zichtbare catalogus.
Printings die ontbreken worden uit de zoekcatalogus gemarkeerd, maar niet fysiek verwijderd;
collectie- en deckregels blijven werken. Een bestand zonder Arena-kaarten wordt geweigerd.
Na een lokale import kan **Bijwerken via Scryfall** de officiële snapshot herstellen.

**Pauzeren** stopt de import. Als het bronbestand volledig is gecachet, kun je de import
in de geschiedenis selecteren en **hervatten**. Na het sluiten/herstarten van de app geldt
hetzelfde. Een afgebroken download wordt opnieuw gestart; er is geen HTTP-rangehervatting.
De oude catalogus blijft zichtbaar totdat de gehele import geldig en toegepast is.

Veilige matches met handmatige fase-1-printings behouden hun interne ID, bezit en deckregels.
Een onzekere match blijft apart bewaard en wordt in de importmelding en geschiedenis genoemd.
Er worden geen aantallen samengevoegd of geraden. Onder Collectie-import kun je een
bronregel expliciet aan een bestaande handmatige of catalogusprinting koppelen;
het samenvoegen van twee bestaande printings blijft buiten de scope.

Bron: [Scryfall bulk data](https://scryfall.com/docs/api/bulk-data) en
[kaartobjecten](https://scryfall.com/docs/api/cards). De huidige bron levert gecomprimeerde
JSONL; oudere JSON-arrays blijven ondersteund. Importeren vereist geen account of API-sleutel.

## Collectie importeren en exporteren

1. Open **Collectie-import → Collectiebestand kiezen**.
2. Gebruik het gedocumenteerde **MTGDeckbuilder collectie JSON v1**-formaat.
   Zie [formaat en uitleg](docs/COLLECTION_IMPORT.md) en [voorbeeldbestand](examples/collection-v1.json).
   Ook een export van je bestaande collectie kan worden aangepast en opnieuw ingelezen.
3. Controleer de voorbeeldweergave en los onbekende of dubbele koppelingen op.
4. Vink de controle aan en kies **Wijzigingen toepassen**. Aantallen vervangen het bezit
   per doelprinting; ontbrekende kaarten blijven ongewijzigd. Herimporteren telt niets op.
5. Gebruik de historie om een voorbereiding na herstart terug te vinden of een toegepaste
   import te herstellen. Latere wijzigingen aan betrokken aantallen blokkeren het volledige herstel.

Voorbereiden wijzigt geen bezit. Toepassen en herstellen zijn ieder één transactie;
annulering of fouten laten geen gedeeltelijke wijzigingen achter. Decks blijven behouden.
Een actuele volledige Arena-collectiebron is hier niet beschikbaar: deze fase bewijst
bestandsimport, geen live synchronisatie of officiële Arena-exportcompatibiliteit.

## Deckimport en analyse

Onder **Deck importeren** kun je aantallen met kaartnamen en optionele `(SET) nummer`
invoeren. Mainboard, sideboard en commander worden bewaard. Onzekere koppelingen
vereisen een expliciete keuze; er worden geen regels stilzwijgend overgeslagen.
Eén voorbereiding maakt één nieuw deck. Voorbereidingen blijven na herstart beschikbaar.

**Mijn decks → Analyse** verdeelt lokaal bezit over alle zones en combineert reprints
alleen met dezelfde Oracle-identiteit. Het tekort voor exacte printings blijft apart
zichtbaar. **Export** maakt Arena-decktekst en kan die als `.txt` opslaan.
Zie [deckhandleiding](docs/DECKS.md) en [technische voorbeeldlijst](examples/arena-deck.txt).
Companion, wildcardkosten en strategische aanbevelingen zijn nog niet ondersteund.
Er is geen test binnen een ingelogde Arena-client uitgevoerd.

## Standard controleren

1. Werk de catalogus bij via **Catalogus → Bijwerken via Scryfall**. Na de fase-5-upgrade
   wordt dezelfde bron zo nodig opnieuw verwerkt om legaliteitsvelden op te slaan.
2. Open **Mijn decks → Formaatcontrole**, kies **Arena Standard BO1** of **BO3** en klik
   **Profiel bewaren en controleren**. Het oorspronkelijke formaatlabel blijft behouden;
   een bestaand label `Standard` kiest niet stilzwijgend BO1.
3. Bekijk fouten en herstelrichtingen. De controle telt mainboard, sideboard, printings
   en kaartuitzonderingen. Beide varianten staan sinds 3 februari 2026 maximaal 15
   sideboardkaarten toe; Leyline of Resonance is aanvullend verboden in BO1.
4. Ontbrekende metadata, onzekere identiteiten of oude brondata geven **onvolledig
   gecontroleerd**. Brondata ouder dan 24 uur moeten worden ververst. Een lokaal
   bestand bewijst zijn eigen brondatum niet. De ingebouwde aanvullende regels worden
   na 14 dagen eveneens als verouderd gemeld; online kaartverversing vernieuwt die niet.

Een geslaagde controle geldt voor de getoonde brondata en ondersteunde regels, niet als
garantie van acceptatie door de live Arena-client. Bezit is een afzonderlijke analyse.
Zie [gebruik en grenzen](docs/FORMAT_VALIDATION.md).

## Kaartalternatieven uit je collectie

1. Werk de catalogus online bij en registreer bezit. Laat het oorspronkelijke deck in
   **Formaatcontrole** slagen voor het opgeslagen BO1-/BO3-profiel.
2. Open **Mijn decks → Alternatieven → Advies opnieuw berekenen**. Kies een tekort,
   zoek beschikbare alternatieven, vergelijk kaartteksten en kies aantallen.
3. Bekijk het gezamenlijke voorbeeld en sla op als **nieuw deck**. Het origineel en
   je bezit blijven staan. Alle keuzes delen één voorraad en worden samen opnieuw getoetst.

Reprints staan eerst, daarna passende herkende rollen. Beperkte, onbekende en andere rollen
hebben een zichtbaar label en verschillen. Andere kaarten worden alleen aangeboden voor mainboard-niet-landen,
met gedeeld type en zonder nieuwe kleuren. De rolmodule herkent alleen een beperkte set
volledige Engelse teksten voor counters, removal, draw en mana-versnelling. Sinds fase 8
zijn **volledige herkenning, onvolledige aanwijzingen en onbekend** apart zichtbaar.
Alle niet beoordeelde tekst blijft leesbaar; een aanwijzing is nooit een sterke match.
Sinds fase 9 zijn ook vaste gerichte schade en afzonderlijke modale opties zichtbaar.
Schade bewijst geen dodelijkheid; modale effecten worden niet als combinatie voorgesteld.
Beide zijn nooit een sterke rolmatch. De historische fase-9-meting is **52 volledig herkend,
300 onvolledig en 4.812 onbekend** op 5.164 Standard-legale kaartdefinities. De nieuwe
gereserveerde set geeft **1 volledig, 7 onvolledig en 22 onbekend**: 13 van 21 relevante
kaarten gemist, tegenover 18 met v0.8. De dekking blijft beperkt.
De score **3/10 → 10/10** betreft alleen de bestaande gekozen ontwikkelscenario's.
Geen brede kaartkennis of claim over synergie/winkans. Zie
[schade, modale opties en aparte evaluatie](docs/DAMAGE_AND_MODAL_ROLES.md).

De aparte mono-rode werkbank gebruikt expliciete beoordelingen voor één deckplan,
zonder de generieke parserrollen te veranderen. Op twintig vooraf vastgelegde
ontwikkelreferenties geeft zij 20 gewenste eerste keuzes tegenover vijf met generiek
advies. Dit meet functie-filtering én rangschikking met gesimuleerd bezit; geen
onafhankelijke of ongeziene praktijkscore. De echte actuele catalogusproef bevestigt
27 geschikte ondersteunde poolkaarten en drie geblokkeerde uitsluitingen. Zie
[kaartpool, gebruik en grenzen](docs/MONO_RED_POOL.md).
Resterende tekorten blijven expliciet staan. Bij gewijzigde gegevens is opnieuw berekenen
nodig. Zie [handleiding en grenzen](docs/DECK_ALTERNATIVES.md).

## Lokale opslag

De eerste start past de meegeleverde migraties automatisch toe op
`%LOCALAPPDATA%/MTGDeckbuilder/deckbuilder.db`. Daglogs staan in de submap `logs`;
maximaal 14 bestanden worden bewaard. Er worden geen accountgegevens gevraagd.

Vóór een schema-upgrade van een bestaande database maakt de app met de SQLite-back-up-API
een reservekopie in `backups`. Via Instellingen zijn ook handmatige back-ups en gecontroleerd
herstel beschikbaar. Dit is geen periodieke back-updienst; bewaar zelf een extra kopie
op een andere schijf. Zie docs/BACKUP_RESTORE.md voor de stappen en herstelgrenzen.
Catalogusdownloadcache en hervatbare catalogusimportbestanden staan in `catalog-cache`. De twee meest recente
voltooide bronbestanden blijven bewaard; bestanden van onvoltooide imports blijven staan
voor hervatten. Die kunnen schijfruimte blijven innemen.

Collectievoorbereidingen en wijzigingshistorie staan in de database. Het gekozen
bronbestand hoeft na voorbereiding niet te blijven staan. De registratie bevat de
bestandsnaam en SHA-256, niet het volledige bronpad. Er is nog geen opruimfunctie voor historie.

Voor een geïsoleerde ontwikkel- of diagnosestart kun je voor het starten
`MTGDECKBUILDER_DATA_DIR` op een andere map instellen. Laat deze variabele normaal leeg.
Gebruik voor een back-up tijdens gebruik de knop in Instellingen. Een losse handmatige
bestandskopie vereist een gesloten app; houd rekening met eventuele SQLite-sidecarbestanden.
Per gegevensmap is één desktopapp toegestaan om herstel vóór databasegebruik veilig te houden.

## Verificatie

```powershell
./scripts/Verify.ps1 -Screenshots
```

Dit voert restore, Release-build en alle xUnit-tests uit. Resultaten verschijnen in
`artifacts/build.log`, `artifacts/test-results/` en `artifacts/ui/`.
De tests gebruiken wegwerpdatabases in de tijdelijke gebruikersmap.
Dezelfde verificatie is opgenomen in de Windows GitHub Actions-workflow; deze workflow
is pas actief nadat het project in een GitHub-repository is geplaatst.

## Migraties ontwikkelen

De lokale toolversie staat in `dotnet-tools.json`:

```powershell
dotnet tool restore
dotnet ef migrations add NaamVanWijziging --project src/MTGDeckbuilder.Infrastructure
```

Controleer de gegenereerde migratie en test dat bestaande gegevens behouden blijven.
Start de app om de migratie op de lokale gebruikersdatabase toe te passen. Gebruik geen
`database drop`, `EnsureDeleted` of `EnsureCreated` op gebruikersdata.

## Documentatie en beperkingen

Installatie/upgrade: [RELEASE_GUIDE.md](docs/RELEASE_GUIDE.md).
Releasecontrole: [VALIDATION_PHASE13.md](docs/VALIDATION_PHASE13.md).

Zie PROJECT_PLAN.md, AGENTS.md, docs/ARCHITECTURE.md, docs/DECISIONS.md,
docs/VALIDATION_PHASE12.md, docs/BACKUP_RESTORE.md, docs/VALIDATION_PHASE11.md, docs/DECK_GENERATION.md, docs/VALIDATION_PHASE10.md, docs/MONO_RED_POOL.md, docs/DAMAGE_AND_MODAL_ROLES.md, docs/ROLE_COVERAGE.md, docs/CARD_ROLES.md, docs/DECK_ALTERNATIVES.md, docs/FORMAT_VALIDATION.md, docs/DECKS.md,
docs/COLLECTION_IMPORT.md en het complete bestandenoverzicht in docs/FILES.md.
docs/VALIDATION.md en docs/VALIDATION_PHASE2.md tot en met docs/VALIDATION_PHASE9.md bevatten de historische resultaten.
Er is nog geen automatische Arena-collectiebron, accountkoppeling, kaartafbeelding,
validatie van andere formaten, bewezen strategische optimalisatie, periodieke back-up of synchronisatie.
Decks mogen meer exemplaren bevatten
dan je bezit; deckplanning en werkelijk bezit zijn bewust gescheiden.

De invoervelden vereisen gehele aantallen; decimalen worden met een foutmelding
geweigerd. Deckregels tonen kaartnaam, set, collectornummer en zone, zodat verschillende
printings uit dezelfde set te onderscheiden zijn.

Op deze ontwikkelmachine heeft Windows App Control eerder gewijzigde ontwikkelbuilds
geblokkeerd. Bij de vervolgcontrole stond Smart App Control uit. Daarna is de bijgewerkte
fase-1-versie volledig getest en opnieuw gestart. Fase 2 is daarna met 55 tests geverifieerd.
Er zijn door de agent geen
beveiligingsinstellingen veranderd. De gebruiker heeft daarvoor wel expliciet toestemming
gegeven. Details staan in docs/VALIDATION.md.
