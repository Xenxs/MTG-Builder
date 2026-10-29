# Controleverslag fase 10

Uitgevoerd op **23 oktober 2026**, Windows x64, .NET SDK **10.0.401**, **v0.10.0**.
Expliciete startopdracht: "start fase 10". Scope: gecontroleerde kaartpool en meetbare
advieskwaliteit voor mono-rood aggro in Arena Standard BO1. Complete deckgeneratie
is fase 11 en is niet gestart. Gebruik: [MONO_RED_POOL.md](MONO_RED_POOL.md).

## Definitieve build en automatische controles

Werkelijk uitgevoerd:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase10-delivery
```

Restore geslaagd; Release-build **0 waarschuwingen, 0 fouten**.
**303 tests geslaagd**, 0 mislukt/overgeslagen:

| Suite | Geslaagd | TRX onder artifacts/test-results |
| --- | ---: | --- |
| Domain | 28 | phase10-delivery_net10.0_20261023094627.trx |
| Application, SQLite-integratie, Avalonia UI | 275 | phase10-delivery_net10.0_20261023094657.trx |

`artifacts/build.log` bewaart het buildresultaat. De nieuwe controles omvatten:
alle velden van de metadata-afdruk, geen naam-only koppeling, conflicterende reprints,
actuele Arena-status, onbekende/verboden/niet-legale kaarten, oude/toekomstige/missende
bronnen, verlopen regels, werkelijk bezit, nulvoorraad, bewuste uitsluitingen, landen,
herstart, annulering en gegevensbehoud. De twintig inhoudelijke referenties worden
apart gerapporteerd en tellen niet als twintig afzonderlijke xUnit-tests.

Gebonden UI-controles bedienen de nieuwe navigatie, zoekfilter, bezitfilter,
uitsluitingen, bronvernieuwing en uitklapbare voorwaarden. Een extra regressie voorkomt
dat na externe voorraadwijziging opties voor de oude kaart worden getoond terwijl
het bezitfilter inmiddels een andere kaart selecteert: opnieuw kiezen is dan vereist.
Alle automatische databasetests gebruiken wegwerpdatabases, geen echte gebruikersdata.

Ontwikkelcontroles vonden testsyntaxis-/annuleringstokenproblemen en daarna twee
onjuiste testverwachtingen: de bestaande banmelding gebruikt "Wizards-banlijst" en
ingeklapte details zijn nog niet in de visuele boom aanwezig. Gecorrigeerd zonder
waarschuwingen uit te schakelen, sleeps of retries. Een volledige reviewrun met 303
tests slaagde; na de genoemde filterregressie is de definitieve volledige suite opnieuw geslaagd.

## Werkelijke online bron

De officiële Scryfall-bulkroute is werkelijk online gebruikt in een **geïsoleerde**
map onder `.local/phase10-source-data`. Actuele bronversie:

`e2ef41e3-5778-4bc2-af3f-78eca4dd9c23:2026-10-22T21:05:50.0170000+00:00`.

Definitieve import gecontroleerd om **09:25:33 Europe/Amsterdam**:
119.155 bronrecords verwerkt, 21.647 Arena-printings, 16.415 kaartdefinities;
een tweede online controle retourneert AlreadyCurrent=true. MetadataVersion=1 en
alle zes migraties aanwezig. Bezit-/decktellingen vóór/na import blijven nul.
De geëxporteerde Standard-subset bevat 5.164 kaartdefinities.

Bronbestand SHA-256:
`3036724CC4685C09FC1236E44ADE053626637E969057CF0D1B86B9EFC0318F80`.
Bewijs: `artifacts/phase10-online-import.json`, consolelog en
`phase10-standard-source.json`. De eerste poging gebruikte per abuis een oudere
fase-2-diagnosebinary zonder legaliteitstabellen. De tool is opnieuw gebouwd; de
bestaande migraties en herimport brachten uitsluitend deze geïsoleerde proefdatabase
op de huidige structuur/metadata. Gebruikersdata zijn daarvoor niet gebruikt.

De actuele [Wizards-banlijst](https://magic.wizards.com/en/banned-restricted-list)
is opnieuw gelezen; de bestaande BO1-uitzondering voor Leyline of Resonance blijft
toegepast. De generieke regels en hun controledatum zijn niet gewijzigd op grond van
een onbewezen extra regel. Scryfall-bronstatus en de bestaande gecontroleerde aanvullende
banlijst worden samen gebruikt, met dezelfde 24-uurs-/14-daggrenzen.

## Vooraf vastgelegde pool en referenties

Pool: **30 kaarten**, waarvan **26 ondersteunde niet-landen**, Mountain en **3
bewuste uitsluitingen**. De beoordeelde tekst, functie, voorkeur en voorwaarden zijn
meegeleverd als embedded resource. Deze expliciete beoordeling door dezelfde
ontwikkelaar (Codex) is geen onafhankelijke menselijke review.

Vóór implementatie van de poolrangschikking zijn op **07:30:57 UTC** de pool en twintig
inhoudelijke referentiegevallen vastgelegd. Beide zijn sindsdien bytegewijs gelijk:

| Bestand | SHA-256 |
| --- | --- |
| src/MTGDeckbuilder.Application/Resources/mono-red-aggro-v1.json | 407B61C6C8144027A7F57313178419668D77205F5CA7B098D96FE065047CC486 |
| examples/mono-red-advice-reference-v1.json | 944E1E3DC93F0EC3AA56C51B4A2DB64A32F3EEEA7208BACBCB34D22274E12030 |

Bewijs: `artifacts/phase10-pool-reference-freeze.json`. De definitieve engine-/storehashes
staan in `phase10-pool-engine-final.json`; de eindcontrole verifieert alle bestanden opnieuw.

`artifacts/phase10-advice-reference-evaluation.json` rapporteert **5/20** gewenste
eerste keuzes met het bestaande generieke advies tegenover **20/20** met de pool.
Het zijn dezelfde aangeboden bron-/kandidaatsets, maar andere geschiktheidsfilters:
de pool vereist dezelfde beoordeelde functie en kan daarbij bewust verschillende
hoofdkaarttypen vergelijken. Dit meet **functie-filtering plus rangschikking**, niet
alleen sortering op identieke eindresultaten. Bezit/printings/legaliteit zijn voor
deze regressies gesimuleerd. De gevallen zijn gekozen door dezelfde ontwikkelaar,
geen ongeziene of representatieve praktijktest. Geen algemene precisie- of win-rateclaim.

De generieke Oracle-classifier en DeckAdviceEngine zijn inhoudelijk niet aangepast.
De kaartpool maakt hun onbekende rollen niet stilzwijgend volledig. Voorkeuren zijn
normatieve keuzes voor dit ene plan, geen bewezen algemene kaartsterkte.

## Echte catalogusproef en herstart

`artifacts/phase10-real-pool-final.json`, **07:47:48 UTC**, gebruikt de definitieve
Infrastructure-assembly in een nieuwe kopie onder `.local/phase10-smoke-data-final`.
Bron is de actuele geïsoleerde catalogus; geen gebruikersdatabase als testfixture.

- **27/27 ondersteunde poolkaarten** zijn actueel geschikt volgens de opgeslagen
  bronmetadata en gecontroleerde BO1-regels; drie uitsluitingen blijven geblokkeerd.
- Slickshot Show-Off krijgt **Emberheart Challenger**, daarna **Fugitive Codebreaker**
  als beoordeelde opties. Firebrand Archer en Coruscation Mage zijn aanwezig in het
  gesimuleerde bezit maar worden als andere functie niet aangeboden.
- Eerste poollezing circa **0,114 seconde**, piekwerkgeheugen staat in het JSON-verslag.
  Dit is één lokale meting, geen prestatiegarantie.
- Een technisch bestaand deck bevat 60 Mountain en 2 Slickshot Show-Off. Bezit is
  gesimuleerd: vier exemplaren van enkele payoffs, 60 Mountain, nul van de bronkaart.
  Dit is geen competitief deck of volledige deckgeneratie.
- Na provider-/verbindingherstart geeft de pool dezelfde labels en opties.
  Collectie en bestaand deck zijn ongewijzigd; geen extra deck of reservering gemaakt.

De actuele controle bewijst bronverwerking, exacte metadata en het bedoelde beperkte
advies, geen prestaties in matches. Zelfstandige power/toughness, speltoestand,
mana-/castbaarheidsanalyse en volledige synergie worden niet gemodelleerd. De afdruk
dekt de opgeslagen velden uit de handleiding, niet alle mogelijke Scryfall-velden.

## Scherminspectie en echte Windows-start

De definitieve Avalonia-headlessbeelden onder `artifacts/ui` zijn daadwerkelijk bekeken:
`pool-owned-options.png`, `pool-excluded.png`, `pool-metadata-changed.png` en
`pool-no-source.png`. Ze tonen navigatie, voorwaarden, beoordeelde opties, BO1-ban,
metadata-blokkade en ontbrekende bron. Dit is renderbewijs, geen volledige handmatige
bediening van iedere flow in het native venster.

De echte Windows-executable is daarnaast gestart:

| Database | Proces bij controle | Resultaat |
| --- | ---: | --- |
| Bestaande gevulde fase-9-proef | 3464 | Reagerend venster, v0.10.0 en migratie-initialisatie gelogd; normaal gesloten |
| Normale lokale gebruikersdatabase | 26996 | Reagerend venster, v0.10.0 en migratie-initialisatie gelogd; open gelaten |

Definitieve normale start **09:48:23 Europe/Amsterdam**, initialisatie bevestigd om
09:48:25. `phase10-synthetic-native-start.json` en `phase10-user-native-start.json`
bewaren nieuwe logregels, tijdstippen en hashes. Geen WRN/ERR/FTL in deze startregels.

| Assembly | SHA-256 |
| --- | --- |
| Desktop | 9D5BE64FB4A1D030C65170FC7DEB4731687984B414D692643FE89469A4814580 |
| Infrastructure | 1E7CD4B07F01233C75733457986721AA611D532A0D10055E2D93BE88341C3223 |

De Infrastructure-hash stemt overeen met de echte poolproef. Het laatste proces-/
hashresultaat staat in `artifacts/phase10-final-process-check.json`.

## Gegevensbehoud

Vóór en na beide native starts zijn **alle kolommen van 13 bestaande tabellen**
alleen-lezen canoniek gesorteerd en gehasht, nu ook de collectieversies. **Alle 13
hashes en rijtellingen zijn per database gelijk**. De normale database heeft geen
bezit of decks; daarom ook de gevulde proef: **2.005 bezitsregels, 12 decks,
31 deckregels en 5 adviesontvangsten** blijven behouden.

Beide databases behouden zes migraties, laatste `20261022090051_AddDeckAdvicePlans`.
Back-upbestandslijsten vóór/na zijn gelijk. Geen schemawijziging of extra upgradeback-up.
Bewijs onder artifacts: `phase10-user-before/after.json`,
`phase10-synthetic-before/after.json`, `phase10-data-preservation.json` en
`phase10-backups-before/after.json`. Catalogusimport en gesimuleerde proefwrites vonden
uitsluitend in de afzonderlijke fase-10-proefmappen plaats. Poollezing schrijft niets.

## Oplevergrens

Geen nieuwe NuGet-pakketten, secrets, externe AI-API of beveiligingswijzigingen.
Een tijdelijke helper had eerst geen netwerktoegang tot NuGet-auditgegevens; de
normale restore met toegestane netwerktoegang slaagde, zonder audit uit te schakelen.
Geen Git-repository/remote, commit, PR of uitgevoerde CI-run geclaimd.

Modeladvies [GPT-6.1 Sol](https://developers.openai.com/api/docs/models/gpt-6.1-sol) /
High; geen ondersteunde directe chatbediening beschikbaar, instellingen niet gewijzigd.
Oorspronkelijke raming 2–4 actieve uren; tijdens controle bijgesteld naar nog 30–60
minuten. Werkelijke uitvoertijd staat in PROJECT_PLAN.md. Fase 10 wacht op beoordeling;
fase 11 is niet gestart.

Nieuwe bronbestanden: ArchetypePool.cs, de poolresource, SqliteArchetypePoolStore.cs,
ArchetypePoolViewModel.cs en ArchetypePoolView.axaml. Verder drie nieuwe testbestanden,
de vaste adviesreferentie, handleiding en dit verslag. Bestaande DI, navigatie,
versies en documentatie zijn bijgewerkt. Volledig overzicht: [FILES.md](FILES.md).
Tijdelijke helpers/cache/meetbestanden blijven buiten de solution onder genegeerde
`.local` en `artifacts`; de poolresource en referenties/tests worden wel meegeleverd.
