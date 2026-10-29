# Verificatie fase 5 — 22 oktober 2026

Versie **0.5.0**, expliciet gestart met "start fase 5". Scope: uitlegbare controle van
Arena Standard BO1/BO3 en bijbehorende brondata. De zes projecten blijven behouden;
geen nieuwe NuGet-pakketten of beveiligingsbeleidswijzigingen.

## Definitieve build en tests

Windows x64, .NET SDK 10.0.401, bestaande vaste pakketversies. NuGet-restore is succesvol
uitgevoerd met bereikbare broncontrole. Daarna:

```powershell
./scripts/Verify.ps1 -SkipRestore -Screenshots -ReportPrefix phase5-delivery
```

**Release-build geslaagd: 0 waarschuwingen, 0 fouten. 168 tests geslaagd, 0 mislukt,
0 overgeslagen.** Buildlog: artifacts/build.log.

| Testproject | Geslaagd | Exact resultaatbestand |
| --- | --- | --- |
| Domain.Tests | 27 | artifacts/test-results/phase5-delivery_net10.0_20261022103601.trx |
| Application.Tests inclusief SQLite/UI | 141 | artifacts/test-results/phase5-delivery_net10.0_20261022103616.trx |

Een eerste sandboxrun is gestopt wegens ontbrekende Windows-testhostcommunicatie.
De geslaagde volledige runs draaiden buiten de sandbox. De eerste compilatie vond een
await in een LINQ-predicaat; dit is naar de voorafgaande async leesstap verplaatst.
Een migratie-generatiepoging kreeg een NuGet-vulnerability-bronfout in de sandbox;
restore met netwerktoegang slaagde. Geen waarschuwingen onderdrukt of beleid aangepast.

## Gecontroleerde eigenschappen

- Alleen expliciete BO1-/BO3-labels selecteren direct een profiel; oude Standard-labels
  blijven Unspecified. Profielkeuze bewaart het oorspronkelijke label en blijft na herstart.
- Minimum 60 mainboard, maximaal 15 sideboard voor beide varianten, verboden commanderzone.
- Vier exemplaren over printings/zones, Basic Land, exacte any-number/up-to-uitzonderingen,
  onbekende clausules, handmatige identiteiten en tegenstrijdige identiteiten.
- Standard legal/not_legal/banned, onbekende toekomstige waarden en Restricted; aanvullende
  Wizards-banlijst en het onderscheid Leyline of Resonance tussen BO1 en BO3.
- Onbekende, te oude en toekomstige brondatums; oude metadata-versie en verlopen regelversie.
  Een vaste testklok houdt de regeltests onafhankelijk van de kalender van een latere run.
- Online metadata wordt opgeslagen en na herstart gebruikt. Een gelijk bronmanifest met
  oude metadata-versie wordt opnieuw verwerkt; daarna wordt een ongewijzigde bron overgeslagen.
- Catalogusverversing wijzigt legaliteit zonder deckregels/bezit te wijzigen. Een lokaal
  bestand krijgt geen fictieve brondatum en ontbrekende velden wissen vorige bekende statussen.
- Malformed legalities en tegenstrijdige printings weigeren de gehele verversing en
  behouden de vorige zichtbare catalogus/legaliteit. Onbekende stringwaarden blijven Unknown.
- Fout en annulering **na echte SQL-writes** van de nieuwe legaliteiten rollen catalogus,
  legaliteit en ontvangst terug. Een eerdere voltooide snapshot blijft bruikbaar.
- Een gevulde fase-4-database migreert met oude sleutels, aantallen, zones en labels;
  een upgradeback-up wordt aangemaakt en herhaald initialiseren is idempotent.
- Echte UI-selectie-/commandobindingen bewaren een profiel, tonen geslaagd/fouten/onvolledig,
  wissen het oordeel bij gewijzigde keuze/inhoud en lezen na verversing actuele gegevens.
- Alle eerdere regressietests uit fase 1–4 blijven onderdeel van de 168 tests.

Bij visuele controle bleek dat SQLite DateTimeKind niet bewaart: ToUniversalTime zou
de UTC-brondatum onbedoeld als lokale tijd behandelen. De DTO-projectie herstelt nu UTC;
de integratietest controleert exacte bronwaarde én Kind. De definitieve schermafbeelding
toont 08:00 UTC bij een bronversie met 08:00+00:00. De klok/dataset in dat scherm is testdata.

## Echte catalogusproef, geïsoleerde database

Online import in .local/phase5-smoke-data, niet de gebruikersdatabase:

- Bron: Scryfall Default Cards, manifestdatum **21 oktober 2026 21:06:00.423 UTC**.
- **119.115 records → 21.621 Arena-printings → 16.399 kaartidentiteiten.**
- Standard-statussen: **5.164 Legal, 11.222 NotLegal, 13 Banned**; geen Unknown in deze bron.
- Technisch deck met 60 Forest en 1 Leyline of Resonance in sideboard: BO1 geeft de
  aanvullende ban; BO3 slaagt. Deze lijst test regels, geen competitieve kwaliteit.
- Vijftien sideboard-basislanden slagen; zestien geven side-max. Drie Negates mainboard
  plus twee van een andere printing in sideboard geven copies. Bezit blijft leeg en gelijk.
- Eerste volledige download/import: circa **8,34 seconden**, piek circa **270 MiB** inclusief
  catalogusverwerking. Definitieve controle circa **0,04 seconde**; de tweede online check
  hergebruikt de ongewijzigde snapshot en is geen tweede volledige downloadmeting.

Bewijs: artifacts/phase5-scryfall-manifest.json, artifacts/phase5-real-catalog.json
(eerste proef) en artifacts/phase5-real-catalog-final.json (definitieve assemblies).
De laatste Infrastructure-SHA-256 komt overeen met de native desktopstart.
De Scryfall-documentatiepagina gaf HTTP 403 aan de webtool; de echte API en bulkbron
leverden de velden succesvol en zijn daadwerkelijk verwerkt.

## Native upgrade en Windows-start

Eerst is een verborgen helper met de definitieve executable gestart op de gevulde
geïsoleerde fase-4-database. PID 3172, reagerend, migraties geïnitialiseerd; daarna
normaal gesloten. Vervolgens is de normale gebruikersapp zichtbaar gestart:

- **22 oktober 2026 circa 10:37 Europe/Amsterdam, PID 32436**, titel MTG Arena Smart Deckbuilder.
- Reagerend, v0.5.0 / Fase 5; migraties geïnitialiseerd. Geen WRN/ERR/FTL in de nieuwe startregels.
- Desktop-SHA-256: E020E56C7F771A1FE44C89E61F16D9D12C9AC6DCAB1D3D88CA4F8D6812A8EF1F.
- Infrastructure-SHA-256: 87E56644BCFB1FC6E0DACC10A9EB760BADA812272936BB34C6D51DA049B2CB9B.
- Database van vier naar vijf migraties via 20261022082737_AddStandardValidation.
  SQL in artifacts/phase5-migration.sql: alleen kolommen/tabel/index toevoegen binnen een
  transactie; geen bestaande data/tabel verwijderen en foreign keys blijven aan.
- Normale upgradeback-up: **33.325.056 bytes**, SHA-256
  EBB83F93E6B73FC5597B6F77F68E034F467CC4C3FA3A1EF19B9266FE8A2879CF.
  Dynamisch pad en bestandsnaam staan in artifacts/phase5-backup.json.

Voor/na zijn de oorspronkelijke velden van kaarten, printings, bezit, decks, deckregels,
instellingen en collectie-/deckimporthistorie uitsluitend lezend met SHA-256 vergeleken.
Voor de uitgebreidere vergelijking is de daadwerkelijke upgradeback-up als toestand vóór
gebruikt. Alle hashes gelijk; de normale bezit/decktabellen waren leeg. Gevuld behoud is
apart bewezen met 2 bezitsregels, 2 decks, 22 deckregels, 2 deckimportontvangsten en
22 auditregels, naast de complete catalogus van 16.399 kaarten / 21.621 printings.

Bewijsbestanden onder artifacts/:

- phase5-native-start.json en phase5-synthetic-native-upgrade.json.
- phase5-user-before/after.json en phase5-synthetic-before/after.json: oorspronkelijke vergelijking.
- phase5-user-before/after-full.json en phase5-synthetic-before/after-full.json: uitgebreid,
  inclusief kaartcatalogus en deckimporthistorie.
- phase5-user-preservation-full.json en phase5-synthetic-preservation-full.json: gelijke hashes,
  vier migraties in de back-up en vijf na de upgrade.

De normale app is open gelaten. Geen echte gebruikersgegevens gebruikt als testfixture;
de normale start past alleen de bedoelde migratie toe. De catalogusproef en nieuwe
testdecks zijn uitsluitend in geïsoleerde databases uitgevoerd.

## Visuele controle en grenzen

Definitief daadwerkelijk bekeken onder artifacts/ui: deck-validation-passed.png,
deck-validation-errors.png, deck-validation-incomplete.png, dashboard.png en settings.png.
Dit zijn Avalonia-renderingen met testdata, geen screenshots van een ingelogde Arena-client.
Geen volledige DPI-/toegankelijkheids-/hardwarematrix uitgevoerd. Alle zeven navigatiepagina's
zijn door de bestaande UI-tests gegaan.

Een geslaagd oordeel blijft beperkt tot ondersteunde regels en de getoonde brondata.
Brondata ouder dan 24 uur, onbekende brondatum en aanvullende regels ouder dan 14 dagen
geven onvolledig; online kaartverversing verlengt de vaste regelversie niet. Andere
formaten, volledige Companion-/Commander-/naamequivalentieregels, live Arena-acceptatie,
wildcardbudgetten en aanbevelingen zijn niet opgeleverd. Geen installer, remote CI of PR.

Actieve uitvoering circa **20–25 minuten**, korter dan de oorspronkelijke 2–4 uur door
de Standard-afbakening en hergebruik van bestaande infrastructuur. Modeladvies was
GPT-6.1 Sol / High; geen instellingswijziging bevestigd. Fase 5 wacht op acceptatie;
fase 6 is niet gestart.
