# Validatie fase 15 — v1.2.0

28 oktober 2026. Expliciete scope: Arena-interface verwijderen; aangeleverd Magic-logo,
thema en icoon; filters kleur/synergie/type/serie; database legen; deckwizard.
Advies vóór start: GPT-6.1 Sol / High, 2–4 actieve uren. Geen ondersteunde bediening
voor modelinstellingen beschikbaar; geen wijziging geclaimd.

## Bouwen en automatische controles

Uitgevoerd: scripts/Verify.ps1 -Screenshots -ReportPrefix phase15-delivery.
NuGet restore geslaagd; volledige Release-build met nul waarschuwingen/fouten.
**443 tests geslaagd: 36 Domain en 407 Application/SQLite/Avalonia.** Geen overgeslagen tests.
TRX onder artifacts/test-results/phase15-delivery_*.trx; buildlog artifacts/build.log.

Eenentwintig nieuwe gevallen naast de eerdere 422: gecombineerde filters, kleurloos,
onvoldoende voorraad/Standard-pool, wizardkeuzes, UI-stappen/bevestiging/opslag/navigation,
Standard-/Commander-planning met kleur-/legaliteitsgrenzen, bronverval vóór opslag,
netwerkfout, aantoonbaar verschillende creature/instantmix per speelstijl;
wispreview, verkeerde bevestiging, volledige reset/herstart/herstel, gewijzigde data,
idempotentie, onmaakbare back-up, gepland herstel, SQL-fout en annulering na delete-SQL
met complete rollback; onbekend schema en ongeldige verwijzingen blokkeren vóór legen. Alle resetproeven gebruiken uitsluitend geïsoleerde databases.

De oudere Arena-UI staat nu alleen in Tests/Legacy met LegacyShell/LegacyWindow.
Een nieuwe UI-test controleert afwezigheid van oude routes en viewmodel in de echte
desktopassembly. De productiecode bevat geen Arena-interface of archiefmenu.

## Beelden en programma

Gerenderde Avalonia-schermen onder artifacts/ui bekeken: phase15-collection,
phase15-wizard-choices/review/save/minimum, phase15-settings-reset-review,
paper-scryfall-image en paper-compact. Werkelijk gedownloade Adelbert Steiner-afbeelding
is gekoppeld aan exact de geselecteerde Scryfall-ID. Normaal venster 1280×880,
minimum 1050×740; lange pagina's/lijsten/toelichtingen kunnen scrollen.
Het logo is het ongewijzigde aangeleverde WebP; PNG en ICO zijn technische conversies.
Windows-icoon bevat zeven resoluties met het oranje merksymbool uit hetzelfde logo.

Native Windows-start op normale en bestaande gevulde gegevensmap geslaagd: responderend
venster met titel Magic · Smart Deckbuilder, v1.2.0, migraties klaar, geen ERR/FTL en
normale afsluiting. Bewijs artifacts/phase15-user-native.json en phase15-filled-native.json.
Geen handmatige native kliktest claimen; UI-interactie is via Avalonia-tests gecontroleerd.

## Gegevensbehoud

Vóór upgrade consistente SQLite-voorafkopieën gemaakt. Na migratie negen zijn alle
oorspronkelijke kolommen per primaire sleutel vergeleken: **20 tabellen, 38.372 rijen**
normale database; **20 tabellen, 56.824 rijen** gevulde database. Alle oorspronkelijke
waarden behouden. De enige toevoeging is Settings.ExamplesEnabled, default true.
Rapporten/voorafkopieën: artifacts/phase15/user-before/after en filled-before/after.
Normale databases zijn niet geleegd. Upgrade maakt tevens een automatische reservekopie.

## Werkelijke Scryfall-proef

scripts/WizardProbe gebruikt een nieuwe afzonderlijke gegevensmap. Echte Scryfall-
metadata/afbeelding, expliciet gesimuleerd productbezit van 202 kaarten, geen telefoonscan.
Wit + Equipment + Creature + Final Fantasy geeft drie uitgaven.

| Verzoek | Uitkomst |
| --- | --- |
| Casual 60, WR, Equipment, aggro, bezit | 60 kaarten, nul tekorten of andere problemen |
| Standard 60, WR, Equipment, aggro, planning | 60 kaarten, 11 tekortregels, geen overige problemen |
| Cloud Commander, WR, Equipment, gebalanceerd, planning | 100 kaarten, 57 tekortregels, geen overige problemen |

Kleuridentiteit op alle regels gecontroleerd. Opslag/herstart van drie nieuwe decks
bevestigd; voorraad blijft 202. Daarna uitsluitend die testdatabase geleegd: na echte
providerherstart nul kaarten/decks en demo uit. Pre-reset-kopie via de normale gecontroleerde
herstelroute teruggezet: weer 202 kaarten en zeven decks. Bewijs:
artifacts/phase15/wizard-practice.json, met volledige lijsten en afbeeldingshash.
Deze proef bewijst constructie en opslag, geen gameplay, positieve synergie of speelsterkte.

## Distributiecontrole

Definitief pakket gecontroleerd tegen de authentieke v1.1.0-installer op geïsoleerde
paden. Installatie v1.1.0, upgrade naar v1.2.0, uninstall, herinstallatie en tweede
uninstall geslaagd. Eigen coreclr-runtime geladen uit installatiemap, Startmenu-doel
gecontroleerd, programma/snelkoppeling verwijderd en gebruikersgegevens/back-up behouden.
Twintig tabelinhouden vergeleken; alleen de nieuwe instellingenkolom is toegevoegd.
Schema acht naar negen, één migratieback-up. Bewijs: artifacts/phase15-install-installation.json.
Automatische upgradeback-ups van normale, gevulde en installatietestdatabase zijn ook
inhoudelijk exact gelijk aan hun vooraf vastgelegde twintig tabellen (schema acht).

Definitieve portable opnieuw native gestart op beide bestaande gegevensmappen; vensters
reageren, normale afsluiting, geen ERR/FTL. Bewijs: artifacts/phase15-delivery-*-native.json.
Executable- en installericoon na de uiteindelijke build uitgelezen en bekeken: oranje
Magic-merksymbool. Installer NotSigned, beide pakket-SHA-256-hashes opnieuw gecontroleerd.

| Bestand | Bytes | SHA-256 |
| --- | --- | --- |
| MTGDeckbuilder-1.2.0-win-x64-portable.zip | 79.422.825 | 587ECC31271773E52D28FE4391BBDED952C448CEEE33EE2E6583CD7FF3C37C04 |
| MTGDeckbuilder-1.2.0-win-x64-setup.exe | 54.979.623 | 47789E5014EB662656819A4EEEC122654A7DC901DD67BEA8A6F742BDF5093D64 |

De ingesloten validatiekopie is van vóór de installatieproef. De aanvullende, daadwerkelijk
uitgevoerde pakketcontrole staat ook als VALIDATIE-NA-PAKKET.md naast de releasebestanden.
Geen schone tweede-machine- of ondertekende release claimen.

## Beperkingen

Synergie is beperkte tekst-/typeherkenning, geen volledige combo-engine. Thema/stijl
zijn voorkeuren; kleuren, voorraad en ondersteunde regels zijn harde grenzen. Vaste
landen-/spreukenplannen en beperkte Scryfall-pool; geen optimale manabase, castingkans,
powerlevel, winstpercentage of uitputtende zoekclaim. Geen partner/background-paren of
variabele commander-kleurkeuze. Regels hebben een onderhoudsdatum en kunnen verlopen.

Legen wist alle databasegebruikersdata inclusief oudere gegevens, maar behoudt schema,
back-ups, logs en kaartafbeeldingen. De verplichte SQLite-kopie is niet onmiddellijk
annuleerbaar. Herstel vereist leesbare huidige data voor de veiligheidskopie en een
exact herkend schema. Geen stroomuitval-/fysieke-corruptietest of automatische reparatie.
Geen echte telefoonexportketen, schone tweede machine, ARM, signing of gameplaytest.



### Aanvullende zichtbare start

Eén extra launcherstart, direct na expliciet verwijderen van de gegevensmapvariabele
in de startshell, eindigde vóór applicatielogging. De exacte oorzaak daarvan is niet
vastgesteld. Dezelfde definitieve portable startte vervolgens zonder die bewerking op
de normale gegevensmap, en daarna ook zichtbaar. De zichtbare app bleef reageren zonder
ERR/FTL en staat voor de gebruiker open; bewijs artifacts/phase15/open-for-user.json.
Dit is geen claim dat de eerste fout is gereproduceerd of gerepareerd.
