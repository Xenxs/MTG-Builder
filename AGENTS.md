# Werkinstructies voor MTG Arena Smart Deckbuilder

## Samenwerking

Wees direct en feitelijk over fouten, beperkingen en risico's. Onderbouw conclusies
met uitgevoerde controles. Claim nooit een geslaagde build, test of appstart zonder
bijbehorend resultaat. Vraag alleen bij belangrijke onomkeerbare of inhoudelijk
onduidelijke keuzes om toelichting. Gebruik voor routinekeuzes professionele standaarden.

Vermeld bij elk fasevoorstel en vóór de start van iedere fase het aanbevolen model
en reasoningniveau, met een korte inhoudelijke reden. Maak onderscheid tussen advies
en de werkelijk ingestelde configuratie; claim geen modelwissel zonder bevestigde wijziging.

De gebruiker geeft toestemming om de chatinstellingen automatisch aan te passen aan
het aanbevolen model en reasoningniveau. Vraag hiervoor niet opnieuw toestemming.
Gebruik hiervoor beschikbare, ondersteunde instellingsbediening en controleer het
resultaat. Als directe bediening ontbreekt, meld dat er niets is gewijzigd. Deze
toestemming geeft geen opdracht om een nieuwe chat of een volgende projectfase te starten.

Geef bij iedere fase ook een indicatie van de actieve uitvoertijd, inclusief bouwen,
testen en documenteren. Gebruik een realistische bandbreedte, benoem de bijbehorende
scope en belangrijkste onzekerheden, en presenteer de schatting niet als gegarandeerde
eindtijd of vaste draaitimer. Werk de indicatie bij zodra voortgang of blokkades daartoe
aanleiding geven. Vermeld vóór de start samen: fase, model, reasoning en tijdsindicatie.

## Architectuur

- Domain is onafhankelijk van Avalonia, EF Core, Application en Infrastructure.
- Application verwijst alleen naar Domain en definieert opslagcontracten.
- Infrastructure implementeert die contracten met EF Core en SQLite.
- Desktop is het composition root en gebruikt MVVM met CommunityToolkit.Mvvm.
- Geen databasecode in views; code-behind mag alleen UI-gebeurtenissen doorgeven.
- Gebruik korte DbContext-levensduren via IDbContextFactory. Deel nooit een context tussen threads.
- SQLite I/O draait via de infrastructure-decorator buiten de UI-thread.

## Data en kwaliteit

- Gebruik EF-migraties; vervang deze niet door EnsureCreated of databaseverwijdering.
- Bestaande gebruikersdata behouden. Geen echte gebruikersdatabase gebruiken in tests.
- Paden via Environment.SpecialFolder.LocalApplicationData; geen gebruikersnamen of secrets vastleggen.
- Centrale foutmelding in de UI en technische foutdetails via Serilog.
- Schrijf tests voor domeinregels, usecases en werkelijk databasegedrag.
- Controleer na wijzigingen met `./scripts/Verify.ps1 -Screenshots`.
- Bekijk gewijzigde schermen via de gerenderde afbeeldingen onder artifacts/ui.
- Documenteer ontwerpkeuzes in docs/DECISIONS.md en voortgang in PROJECT_PLAN.md.

## Scope

Fase 1 en fase 2 zijn geaccepteerd. De gebruiker heeft fase 2 op 21 oktober 2026
expliciet goedgekeurd: kaartcatalogus, herhaalbare catalogusimport en zoeken/filters,
inclusief tests en documentatie. Arena-collectie-import, legaliteitsregels en
aanbevelingsalgoritmen vielen buiten fase 2. Begin niet aan fase 3 zonder expliciete
startopdracht van de gebruiker. Die startopdracht is op 22 oktober 2026 ontvangen.
Fase 3 omvat bestandsimport van collectieaantallen met voorbeeldweergave, expliciete
koppelingen, conflictcontrole, historie en herstel. Een Arena-logkoppeling wordt pas
als werkend opgeleverd na controle met een geschikte echte bron. Begin niet aan fase 4
zonder afzonderlijke startopdracht.
Fase 3 is op 22 oktober 2026 als v0.3.0 opgeleverd en door de gebruiker geaccepteerd
met "goed ga door". Daarmee is fase 4 gestart: Arena-decktekst import/export,
ontbrekende kaarten en beschrijvende deckanalyse. Geen formaatlegaliteit of slimme
aanbevelingen; begin niet aan fase 5 zonder afzonderlijke startopdracht.
Bewijs en grenzen staan in docs/VALIDATION_PHASE3.md en docs/COLLECTION_IMPORT.md.
Fase 4 is op 22 oktober 2026 als v0.4.0 opgeleverd en door de gebruiker geaccepteerd
met "fase 4 is goed". Na het voorstel met "begin nog niet" heeft de gebruiker fase 5
op 22 oktober 2026 expliciet gestart met "start fase 5". Fase 5 levert v0.5.0:
uitlegbare Arena Standard BO1-/BO3-controle met bronactualiteit en gegevensbehoud.
Zie docs/FORMAT_VALIDATION.md en docs/VALIDATION_PHASE5.md. De gebruiker heeft fase 5
op 22 oktober 2026 geaccepteerd met "fase 5 is goed". Voor fase 6 is alleen een voorstel
gevraagd. De afzonderlijke startopdracht "start fase 6" is op 22 oktober 2026 ontvangen.
Fase 6: uitlegbare lokale kaartalternatieven, met gezamenlijke voorraadcontrole en opslag
als nieuw deck. Opgeleverd als v0.6.0 met 197 geslaagde tests; op 22 oktober 2026
geaccepteerd door de gebruiker met "Fase 6 ok".
Zie docs/DECK_ALTERNATIVES.md en docs/VALIDATION_PHASE6.md.
Voor fase 7 is informatie gevraagd: kaartrollen en meetbare advieskwaliteit.
De afzonderlijke startopdracht "Start fase 7" is op 22 oktober 2026 ontvangen.
Fase 7 omvat beperkte rolherkenning, uitlegbare rangschikking en een referentiemeting.
Opgeleverd als v0.7.0 met 239 geslaagde tests; op 22 oktober 2026 geaccepteerd
met "Goed ga naar fase 8". Daarmee is fase 8 expliciet gestart.
Zie docs/CARD_ROLES.md en docs/VALIDATION_PHASE7.md. De fase-7-praktijkdekking was slechts
16 van 5.164 Standard-legale kaartdefinities; claim geen brede kaartkennis.
Fase 8 verbetert praktische roldekking en meet die met een aparte evaluatieset.
Maak onderscheid tussen volledig herkende teksten, onvolledige aanwijzingen en onbekend.
Op 22 oktober 2026 opgeleverd als v0.8.0: 255 geslaagde tests; geaccepteerd met
"Fase 8 goed start 9". Daarmee is fase 9 expliciet gestart.
Praktijkmeting: 43 volledig, 207 onvolledig, 4.914 onbekend op dezelfde 5.164 kaarten.
De vooraf gereserveerde evaluatie geeft 0 volledig, 4 onvolledig en 26 onbekend;
15 van 19 relevante kaarten gemist. Geen brede nauwkeurigheid of kaartkennis claimen.
Parser/vergelijking zijn vóór tekstinspectie bevroren; geen onafhankelijke reviewer.
Gebruik en bewijs: docs/ROLE_COVERAGE.md en docs/VALIDATION_PHASE8.md.
Fase 9: begrensde gerichte schade en expliciete modale rol-aanwijzingen, met
doelwitten, hoeveelheid, keuzeregels en resttekst; geen sterke schade-/modale match.
Reserveer vóór implementatie nieuwe evaluatiegevallen; bevries vóór tekstinspectie.
Behoud eerdere veiligheidscontroles. Begin niet aan fase 10 zonder startopdracht.
Fase 9 is op 22 oktober 2026 opgeleverd als v0.9.0 met 278 geslaagde tests;
op 23 oktober 2026 geaccepteerd met "fase 9 goed gekeurd". Op 23 oktober 2026
is fase 10 expliciet gestart met "start fase 10".
Praktijkdekking bij oplevering: 52 volledig, 300 onvolledig en 4.812 onbekend.
Daaronder 50 modale kaarten; tel hun overlappende families niet als extra kaarten.
Nieuwe evaluatie: 1 volledig, 7 onvolledig, 22 onbekend; 13/21 relevante kaarten gemist
tegenover 18/21 met de vooraf vastgelegde v0.8-baseline. Geen algemene precisieclaim.
Parser, vergelijking, rangschikking en labels zijn na evaluatie ongewijzigd gebleven.
Gebruik en bewijs: docs/DAMAGE_AND_MODAL_ROLES.md en docs/VALIDATION_PHASE9.md.
Fase 10: gecontroleerde kaartpool voor mono-rood aggro in Arena Standard BO1 en een
vastgelegde adviesmeting. Bewaar exacte Oracle-identiteit en metadata-afdruk, rol binnen
dit decktype, voorwaarden en bron. Geen brede parserkennis of onafhankelijke review claimen.
Onzekere/gewijzigde kaartmetadata, oude bron of ongeldige legaliteit blokkeren pooladvies.
Complete decks genereren is fase 11; begin daaraan niet zonder afzonderlijke startopdracht.
Fase 10 is op 23 oktober 2026 opgeleverd als v0.10.0 met 303 geslaagde tests;
geaccepteerd met "fase 10 akkoord start fase 11". Daarmee is fase 11 expliciet gestart.
Pool: 26 ondersteunde niet-landen, Mountain en drie uitsluitingen.
De actuele catalogusproef bevestigt 27 geschikte ondersteunde kaarten. Twintig vooraf
vastgelegde ontwikkelreferenties geven 5/20 generiek tegenover 20/20 met pooladvies;
dit meet filtering plus rangschikking, geen onafhankelijke/ongeziene benchmark.
De werkbank leest alleen, reserveert geen voorraad en maakt geen complete decks.
De algemene classifier en generieke adviesopslag blijven inhoudelijk gelijk.
Gebruik en bewijs: docs/MONO_RED_POOL.md en docs/VALIDATION_PHASE10.md.
Fase 11: begrensde complete mono-rode BO1-deckvoorstellen, expliciete bezit-/planningsmodus,
mana en curve, tekorten, hercontrole en opslag als nieuw deck. Leg vóór implementatie
acceptatiecriteria vast. Behoud de pool- en broncontroles; geen bewezen speelsterkte claimen.
Begin niet aan fase 12 zonder afzonderlijke startopdracht.
Fase 11 is op 23 oktober 2026 opgeleverd als v0.11.0 met 343 geslaagde tests;
geaccepteerd met "goedgekeurd start fase 12". Fase 12 is daarmee expliciet gestart.
Eén vast mono-rood BO1-plan: 22 Mountains, 20 creatures en
18 overige spreuken; bezitmodus standaard, planning expliciet met alle tekorten.
Geen bewezen speelsterkte, castingkans of optimaal deck claimen. Pool en algemene
classifier blijven inhoudelijk gelijk. Eigen voorbeeldontvangst en zevende migratie,
hercontrole en idempotente nieuwe-deckopslag; geen bezit-/deckoverschrijving.
Echte catalogusproef met gesimuleerd bezit: 60 kaarten, nul tekorten, herstart en opslag
bevestigd. Beide bestaande databases behouden alle kolommen van dertien tabellen,
de nieuwe tabel is leeg en één upgradeback-up per database inhoudelijk gecontroleerd.
Gebruik en bewijs: docs/DECK_GENERATION.md en docs/VALIDATION_PHASE11.md.
Fase 12: betrouwbaarheid en praktijkcontrole met echte catalogus, expliciet gesimuleerd
bezit, handmatige back-ups en gecontroleerd herstel bij herstart. De gebruiker heeft
bevestigd dat er geen echte export beschikbaar is; claim geen controle met echte collectie.
Herstelproeven uitsluitend in geïsoleerde databases. Maak vóór werkelijk herstel een
veiligheidskopie; weiger beschadigde/onbekende schema's. Stop vóór fase 13.
Fase 12 is op 23 oktober 2026 opgeleverd als v0.12.0 met 362 geslaagde tests;
geaccepteerd met "fase 12 akkoord start fase 13". Fase 13 is expliciet gestart.
Handmatige SQLite-back-ups, exacte schemacontrole, expliciet
bevestigd herstel bij appstart, veiligheidskopie en herhaalbaar journaal. Eén desktopapp
per gegevensmap. Geen nieuwe migratie/pakket. Echte catalogus van 23 oktober, gesimuleerd
bezit/decks, volledig herstel inclusief gevulde import-/advieshistorie en native startup
bevestigd. Vijftien tabellen, migraties en back-ups op beide bestaande databases gelijk.
Geen automatische reparatie van fysieke corruptie: huidige database moet leesbaar zijn
voor de veiligheidskopie. Geen echte Arena-export, gameplay- of stroomuitvaltest claimen.
Gebruik en bewijs: docs/BACKUP_RESTORE.md en docs/VALIDATION_PHASE12.md.
Fase 13: beperkte v1.0 Windows x64-distributie met gebundelde runtime, installatie,
upgrade-/verwijdercontrole met gegevensbehoud, hashes en startinstructies. Geen
code signing-certificaat beschikbaar; geen ondertekende of extern gecertificeerde
release claimen. Test installatie/herinstallatie/verwijdering op geïsoleerde paden.
Normale gebruikersdata alleen auditen, nooit verwijderen voor installatietests.
Fase 13 is op 23 oktober 2026 opgeleverd als beperkte v1.0.0: 362 tests geslaagd,
self-contained win-x64 zip en ongetekende per-user installer, SHA-256-manifest.
Werkelijke installatie/versie-upgrade/uninstall/herinstallatie met gevulde schemafixture,
eigen runtime en gegevensbehoud bevestigd. 0.99.0 is een opnieuw gebouwde versiefixture,
geen authentieke eerdere release. Geen schone tweede machine of signing claimen.
Normale en gevulde bestaande databases behouden vijftien tabellen en zeven migraties.
Geen nieuwe appfunctionaliteit of fase starten zonder opdracht. Gebruik en bewijs:
docs/RELEASE_GUIDE.md en docs/VALIDATION_PHASE13.md. De gebruiker heeft de v1.0-oplevering
op 23 oktober 2026 expliciet geaccepteerd met "goedgekeurd". Fase 13 is afgesloten;
verdere ontwikkeling en onderhoud vereisen een afzonderlijke opdracht.
Op 27 oktober 2026 is de omschakeling naar fysiek Magic expliciet opgedragen: telefoon-
scan/CSV (ManaBox), fysieke Scryfall-printings en afbeeldingen; aangenomen testbezit van
Conflux Naya Domain en Bant on the March. Nieuwe fase 14 is gestart. Oude Arena-data
behouden en niet als fysiek bezit herlabelen. Beide introdecks zijn historische lijsten,
geen bewijs van actuele Standard-legaliteit of een compleet 60-kaartdeck. AI-optimizer
blijft geparkeerd; lokale logica, uitleg en bestaande kwaliteits-/back-upeisen blijven.
Gebruik en bewijs staan in docs/DECKS.md en docs/VALIDATION_PHASE4.md.

## Toestemming voor beveiligingsbeleid

De gebruiker heeft expliciet toestemming gegeven om voor dit project noodzakelijke
beveiligingsbeleidsaanpassingen uit te voeren. Vraag die toestemming niet opnieuw voor
dezelfde scope. Kies de kleinste passende wijziging en documenteer de toestand vóór en
na de ingreep en de herstelroute. Deze toestemming geeft geen goedkeuring voor fase 2.

## Fase 14 — fysieke uitbreiding (actuele scope)

De gebruiker heeft op 27 oktober 2026 aanvullend Commander-generatie en de FINAL FANTASY
Starter Kit opgedragen. Deze startopdracht vervangt de eerdere beperking tot alleen
Casual60. Vier productdecks/202 aangenomen speelkaarten/109 exacte uitgaven/118 posities;
geen echte telefoonscan claimen. Starter Kit heeft een eigen seedmarker en twee foil-
hoofdkaarten. Tokens en Arena-codes zijn geen geregistreerde deckkaarten.

Commander: exact 100 inclusief één ondersteunde commander, singleton, kleuridentiteit,
basislandtypen, bronactualiteit en papieren legaliteit. Actuele CR 903.3 bevat ook legendary
Vehicles en geschikte Spacecraft; niet op verouderde alleen-creature-aanname bouwen.
Vast constructieplan: 62 niet-landen/37 basics plus commander. Bezitmodus standaard;
planning expliciet met gerichte Scryfall-aanvulling en zichtbare tekorten. Geen fictief
bezit, partner/background-paren, variabele kleurkeuze, bracket/cEDH- of winstclaim.
Zie docs/COMMANDER.md, docs/PHYSICAL_COLLECTION.md en docs/VALIDATION_PHASE14.md.
Stop na fase 14 voor beoordeling; begin niet zelfstandig een volgende fase.

Fase 14 is op 27 oktober 2026 als v1.1.0 opgeleverd ter beoordeling: 422 geslaagde tests,
echte Scryfall-proef, Commander-planning en veilige bezitblokkade, 202 aangenomen kaarten,
gegevensbehoud en definitieve installatie/upgrade/uninstall/herinstallatie bevestigd.
Nog geen gebruikersacceptatie claimen. Geen volgende fase starten zonder opdracht.

## Fase 15 — huidige opdracht van 28 oktober 2026

De gebruiker heeft expliciet opgedragen Arena uit de app te verwijderen, het aangeleverde
Magic-logo/thema/icoon toe te passen, collectiefilters, Database legen en een deckwizard
te bouwen. Dit is de nieuwe geautoriseerde scope na fase 14, geen algemene toestemming
voor verdere fases. Modeladvies GPT-6.1 Sol / High; raming 2–4 actieve uren. Geen
ondersteunde instellingenbediening beschikbaar; model/reasoning niet aangepast.

Geen actieve Arena-UI of route in de desktopassembly; oude UI alleen Tests/Legacy voor
regressiecontrole. Historische tabellen/migraties blijven bij gewone upgrades behouden.
Het aangeleverde logo blijft ongewijzigd beschikbaar als WebP; PNG/ICO zijn conversies.
Filterkleur is geprinte kleur, wizardkleur is kleuridentiteit. Synergie/stijl zijn
begrensde tekst-/type-/MV-voorkeuren, geen engine of bewezen speelsterkte.

Database legen is een expliciete gebruikersoptie met preview, exact WISSEN, verplichte
consistente voorafkopie, toestandcontrole en één transactie. Nooit de normale database
legen als test. ExamplesEnabled voorkomt herbevolking na legen en herstart. Nieuwe
migratie negen behoudt bestaande instellingen/data. Bronactualiteit ook bij wizard-save.
Zie docs/MAGIC_WIZARD_HANDLEIDING.md en docs/VALIDATION_PHASE15.md.
Stop na fase 15 voor beoordeling; begin niet zelfstandig een volgende fase.

Fase 15 is op 28 oktober 2026 als v1.2.0 gereed voor beoordeling: 443 geslaagde tests,
volledige build zonder fouten/waarschuwingen, echte Scryfall-wizardproef en uitsluitend
geïsoleerde reset/herstelproeven. Gegevensbehoud van twintig tabellen in twee bestaande
databases, definitieve native start en v1.1.0→v1.2.0-installatie/upgrade/uninstall/
herinstallatie met eigen runtime en gegevensbehoud bevestigd. Geen gebruikersacceptatie
of volgende fase claimen. Zie docs/VALIDATION_PHASE15.md en docs/CHANGES_PHASE15.md.

## Fase 16 — synergieonderzoek en verbetering

Op 28 oktober 2026 heeft de gebruiker expliciet vervolgwerk opgedragen: onderzoeken of
de synergie-engine goed werkt met de aanwezige testkaarten en aanpassen waar nodig.
Neem inhoudelijke beoordeling als hoofdzaak; geslaagde structuur-/opslagtests bewijzen
geen strategisch goede decks. Acceptatie vooraf: docs/SYNERGY_ACCEPTANCE_PHASE16.md.
Bewaar v1.2-baseline; onderscheid eigen/opponent, producer/payoff, voorwaarden, onbekend
of gewijzigde metadata, en relaties tussen kaarten. Behoud alle voorraad-/legaliteits-
/identiteitscontroles. Geen normale database schrijven/resetten als proef. Geen
onafhankelijke of ongeziene benchmark claimen als dezelfde ontwikkelaar voorbeelden
heeft gebruikt. Stop na fase 16 voor beoordeling; geen volgende fase zonder opdracht.

Fase 16 is op 28 oktober 2026 als v1.3.0 gereed voor beoordeling: 538 tests geslaagd,
build zonder fouten/waarschuwingen, 87 gameplay-afgedekte productprofielen en relaties
met voorwaarden/conflicten/ontbrekende ondersteuning. Geen brede kaartkennis claimen.
Eerste aparte metingen 9/26, 21/28 en 14/23; eerste twee sets later ontwikkelmateriaal.
Eindregressies 26/26, 24/28 en 14/23, niet onafhankelijk. Derde set negen labels gemist.
Nieuwe beperkingen op automatische selectie voor meerdere kanten/Adventure/split en
herkende eigen verlies-/winblokkades. Populariteitspool is geen deckkwaliteitsbewijs.
Alle bewijsfragmenten bewaren, ook beperkingen na de vijfde ability; niet inkorten
in de onderliggende relatie om een scherm korter te maken.

27/27 bezitplannen, live Standard-/twee Commander-planningen en terecht geblokkeerde
bezit-Commander (34/62 en 23/62) bevestigd. Harde voorraad-/identiteits-/legaliteits-
controles behouden. Twintig tabellen in twee bestaande databases exact behouden,
negen migraties, geen nieuwe migratie/pakketten. Definitieve v1.2.0→v1.3.0-installatie/
upgrade/uninstall/herinstallatie en portable startup met eigen runtime bevestigd.
Geen signing, schone tweede pc, echte scan of spelsterkteclaim. Geen beveiligingsbeleid
gewijzigd. Advies GPT-6.1 Sol / High; geen instellingenwijziging. Bewijs en grenzen:
docs/SYNERGY_ENGINE.md, docs/VALIDATION_PHASE16.md en aanvullende pakketvalidatie.
Geen gebruikersacceptatie claimen en geen volgende fase starten zonder nieuwe opdracht.

## Fase 17 — alle huidige papieren kaarten als aangenomen bezit

De gebruiker heeft op 28 oktober 2026 expliciet nieuw onderzoek opgedragen met alle
huidige kaarten als aangenomen bezit. Scope vooraf: docs/SYNERGY_ACCEPTANCE_PHASE17.md.
Volledige actuele fysieke Oracle-pool uit Default Cards, oudere sets inbegrepen,
uitgegeven uiterlijk vandaag; geen tokens/digitale-only/niet-deckobjecten. Vier kopieën
per niet-basic en 250 per basic, uitsluitend afzonderlijke gesimuleerde testdatabase.
Nooit gewone voorraad vervangen. Bewaar v1.3.0-nulmeting en reserveer evaluatie vóór
tekstinspectie. Kwadratische selectie niet onbeperkt laten draaien. Meet echte
inhoudelijke fouten naast schaalbaarheid; geen brede nauwkeurigheids-/speelsterkteclaim.
Advies GPT-6.1 Sol / High, 4–8 actieve uren; geen modelbediening, niets gewijzigd.
Stop na fase 17 voor beoordeling, geen volgende fase zonder opdracht.

Fase 17 is als v1.4.0 gereed voor beoordeling, niet door gebruiker geaccepteerd.
33.727 actuele fysieke Oracle-kaarten / 138.106 aangenomen exemplaren staan uitsluitend
in .local/phase17-all-cards, nooit in gewone voorraad. Simulatielauncher weigert verlopen
24u-snapshot om duizenden losse verversingen te voorkomen; geen automatische bulkimport
claimen. Nulmeting/binaries v1.3.0 en alle oorspronkelijke evaluaties bewaren.

583 tests geslaagd (36+547), build nul fouten/waarschuwingen, screenshots bekeken.
Eerste aparte A15/52 (geen winst), B22/57 tegenover 19/57; 35 B-labels gemist. Na inspectie
ontwikkelmateriaal: eindregressie A20/52, B22/57. Waarheidsfout Bloodlord transparant
gecorrigeerd met origineel bewaard. Zelfde ontwikkelaar beoordeelt; geen onafhankelijke
of algemene precisie-/speelsterkteclaim. 87 gecontroleerde productprofielen, 13.716
beperkt, 19.924 type-only/onbekend. 1.531 automatisch uitgesloten; leveler/acorn/silver
en complexe vormen niet platmaken. Veel tribal/proliferate/combo blijft onbekend.

Nieuwe groepindex/feitcache vervangt volledige paargraph; relatiepredicatewijzigingen
vereisen overeenkomstige key/upper-bound en differentiële tests. Finale volledige
31.037-kandidatenselector circa 40 s / 308 MiB; complete meerdeckmeetprogramma's circa 1,9 GiB.
Geldige structuur bewijst geen samenhang: Giada/Talrand/Krenko moesten inhoudelijk
gecorrigeerd worden met expliciete commander-type-/castfocus; Meren-setup afgeremd.
61/64 volledige bezitvoorstellen geslaagd, drie Standard/Exalted zonder themapool
terecht geblokkeerd. Alle 16 betrokken Commander/graveyard-aanvragen opnieuw geslaagd,
idempotent opgeslagen en heropend; voorraad gelijk. Geen decks overschrijven.

Authentieke v1.3.0→v1.4.0-upgrade/installatie/uninstall/herinstallatie op geïsoleerde
paden bevestigd; eigen runtime, hashes, 269 ZIP-items, normale native afsluiting en
Startmenu-doel gecontroleerd. Bestaande databases behouden alle 20 tabellen, 9 migraties
en 38.372/56.824 originele rijen exact. Geen nieuwe migratie/pakketten/beveiligingsbeleid.
Finale v1.4.0 zichtbaar geopend op de aparte alle-kaartensimulatie. Geen signing,
schone tweede pc, echte scan, universele kaartkennis of gameplayclaim. Bewijs/grenzen:
docs/SYNERGY_FULL_CATALOG.md, docs/VALIDATION_PHASE17.md en pakketnacontrole.
Geen volgende fase starten zonder opdracht. Advies GPT-6.1 Sol / High; geen modelwissel.

## GitHub-repository — 29 oktober 2026

De gebruiker heeft de repositorymigratie opgedragen en expliciet openbare plaatsing
in https://github.com/Xenxs/MTG-Builder goedgekeurd. Dit is onderhoud, geen start van
een volgende enginefase. De lokale werkmap blijft bestaan en main volgt origin/main.
Gebruik voor broncode/tests/docs/assets/kleine voorbeelden de repository. .local,
artifacts, databases, logs, persoonlijke IDE-instellingen en sleutels niet committen.
Grote bulk-/gebruikersdata en bestaande distributieproeven blijven uitsluitend lokaal.
Een schone bronkopie is werkelijk gebouwd/getest: nul buildfouten/waarschuwingen,
582 geslaagde tests, één expliciete skip voor de ontbrekende all-cards-UI-fixture.
Verwar die skip niet met de volledige fase-17-proef met 583 geslaagde tests.
GitHub Actions-resultaten alleen claimen op basis van de werkelijk afgeronde run.
Zie docs/GITHUB.md. Geen chatinstellingen, appfunctionaliteit of gebruikersdata gewijzigd.

