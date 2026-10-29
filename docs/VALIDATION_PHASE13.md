# Controleverslag fase 13 — beperkte v1.0

23 oktober 2026, Windows x64, .NET SDK 10.0.401. Fase 12 geaccepteerd met
"fase 12 akkoord start fase 13". Oplevering: **1.0.0**, per-user installer en
portable distributie met .NET-runtime **10.0.12**. Handleiding: RELEASE_GUIDE.md.

## Uitgevoerde build en tests

`./scripts/Verify.ps1 -Screenshots -ReportPrefix phase13-delivery`:
restore geslaagd, Release-build **0 waarschuwingen, 0 fouten**;
**362 tests geslaagd**, geen mislukt of overgeslagen.

| Suite | Geslaagd | TRX onder artifacts/test-results |
| --- | ---: | --- |
| Domain | 33 | phase13-delivery_net10.0_20261023114048.trx |
| Application / SQLite / Avalonia | 329 | phase13-delivery_net10.0_20261023114135.trx |

Geen nieuwe unit-tests voor de kleine versie-/verpakkingswijziging. Daarnaast is de
echte installatietest scripts/Test-Installation.ps1 uitgevoerd, met asserts voor
processen, runtime, data, migratie, shortcut en uninstall. Deze externe controles
zijn geen extra xUnit-tests. Scherm settings.png werkelijk bekeken: v1.0.0 / Fase 13.

## Verpakking

`scripts/Package.ps1 -IsccPath …/ISCC.exe` publiceert win-x64 self-contained zonder
trimming/single-file en compileert installer/MTGDeckbuilder.iss. De runtime en native
afhankelijkheden zijn inbegrepen; START-HIER.md wordt meegeleverd. Geen extra app-
NuGet-pakket, nieuwe database-migratie, wijziging van algoritmes of beveiligingsbeleid.

Inno Setup **7.1.0 x64** is opgehaald via de officiële GitHub-release van jrsoftware/issrc.
Downloadhash kwam overeen met de officiële release-digest; Authenticode-status Valid,
ondertekenaar Pyrsys B.V. De compiler is uitsluitend bouwgereedschap. De gegenereerde
installer heeft Authenticode-status **NotSigned**. Geen certificaat of ondertekening
van onze app geclaimd. Officiële bronnen: [Inno Setup](https://jrsoftware.org/isinfo.php),
[release](https://github.com/jrsoftware/issrc/releases/tag/is-7_1_0),
[self-contained .NET](https://learn.microsoft.com/dotnet/core/deploying/).

Pakketten onder artifacts/release/1.0.0:

| Bestand | Bytes | SHA-256 |
| --- | ---: | --- |
| MTGDeckbuilder-1.0.0-win-x64-setup.exe | 54.815.358 | 6159FB4A9F36E3C2E60F788630C58ECBA79156D7C9D40D7D7338230483149E4B |
| MTGDeckbuilder-1.0.0-win-x64-portable.zip | 79.138.316 | 5696AE01381F6B048333A5F4DA54705BB704F6FD255A1EB648B8F6C42A1CEBAE |

release-manifest.json en SHA256SUMS.txt horen bij deze bestanden. Geen externe upload,
storepublicatie of automatische updater. Het pakket is lokaal beschikbaar; hashes
zijn integriteitsinformatie, geen herkomsthandtekening.

De zip is echt uitgepakt naar een nieuwe map: alle **249 bestanden** kwamen bytegewijs
overeen. De portable app startte op een lege geïsoleerde gegevensmap, initialiseert
de database, reageerde en werd normaal gesloten. coreclr.dll werd geladen uit de
uitgepakte map. Bewijs: artifacts/phase13-portable.json. Het feit dat deze machine een
SDK heeft, is dus geen veronderstelling dat de app de globale runtime gebruikte.

## Werkelijke installatie-/upgradeproef

Scripts/Test-Installation.ps1 gebruikt uitsluitend verse mappen onder .local en weigert
een bestaande installatie buiten de testworkspace te vervangen. Een expliciete fixture-
database wordt meegegeven. De bron was een gesloten pre-migration-back-up met leeg WAL,
zes migraties, 2.005 bezitsregels, twaalf decks, 31 deckregels en vijf adviesontvangsten.

Een installer met versie **0.99.0** is uit de huidige bron opgebouwd als testfixture.
Dat is **geen authentieke eerdere release**. De installerupdate 0.99.0 → 1.0.0 bewijst
versievervanging met dezelfde AppId; de oude schemafixture bewijst werkelijk zes → zeven
migraties met automatische upgradeback-up. De zevende tabel komt leeg erbij.

Getest: stille installatie, eerste start, upgrade, start, uninstall, herinstallatie,
start, Startmenu-snelkoppeling/target en afsluitende uninstall. Alle geïnstalleerde
processen reageerden en laadden coreclr.dll uit de installatiemap. Alle bestaande
tabelinhoud bleef gelijk; uninstall verwijderde programmacode en shortcut maar behield
gegevens en de nieuwe migratieback-up. De testinstallatie is na afloop verwijderd,
zodat een latere gebruikersinstallatie niet naar een testpad wordt doorgestuurd.
Bewijs: artifacts/phase13-installation.json, phase13-install-*.json en installerlogs.

De installatie gebruikt PrivilegesRequired=lowest en HKCU, geen administratorplicht.
De testtools draaiden met proces-/registertoegang op deze ontwikkelmachine; een aparte
standaardgebruiker zonder adminrechten is niet getest. Windows 10/11 x64 is de bedoelde
doelgroep, geen bewezen matrix van alle versies of hardware. ARM niet apart getest.

## Bestaande gegevens en normale start

De v1.0.0-ontwikkelapp startte rond **11:45 Europe/Amsterdam** op de bestaande gevulde
proef (proces 30016, normaal gesloten) en normale gebruikersdatabase (proces 36004,
reagerend open gelaten). Verse logs bevatten versie en migratie-initialisatie zonder
waarschuwingen/fouten. Alle kolommen van vijftien applicatietabellen en de zeven
migraties zijn vóór/na gelijk. Bewijs: phase13-user-before/after.json,
phase13-filled-before/after.json, phase13-data-preservation.json en beide native-start.json.
De installatie-/verwijderproeven gebruikten deze normale gegevens niet.

## Reproduceren en grenzen

Voor bouwen: .NET 10 SDK en Inno Setup 7.1, daarna Verify en Package.ps1. Het script
weigert een bestaande releasemap te overschrijven. Dezelfde bron/runtime/configuratie
is reproduceerbaar te bouwen; byte-identieke installeruitvoer wordt niet beloofd.
Voor installatietesten: twee pakketten, een gesloten testdatabase, een nieuwe TestName
en expliciet -FixtureDatabase. De meegeleverde read-only audithelper onder
scripts/DatabaseAudit wordt automatisch gebouwd en is geen onderdeel van de distributie.
De zes solutionprojecten blijven behouden; de helper is een afzonderlijk diagnosetool.

Beperkingen: ongetekend; geen schone tweede machine, echte Arena-export/account/gameplay,
SmartScreen-reputatie of brede Windows-certificering. Het beperkte mono-rode plan en
de herstelgrenzen uit fase 12 blijven gelden. Bron vervalt na 24 uur, regelcontrole
na veertien dagen vanaf 22 oktober 2026; nieuwe bron-/regel-/runtimeversies vereisen
onderhoud. Een v1.0-label maakt deze gegevens niet onbeperkt actueel.

Advies GPT-6.1 Sol / High, instellingen niet gewijzigd wegens ontbrekende ondersteunde
bediening. Start circa 11:37 Europe/Amsterdam; raming 1–3 actieve uren. De uitvoeringstijd
is circa 15 minuten, van 11:37 tot circa 11:52 Europe/Amsterdam; de raming was ruim
door hergebruik. Laatste controle om 11:50:33 bevestigt proces 36004 reagerend,
installer NotSigned en de tijdelijke installatieregistratie verwijderd (phase13-final-check.json).
Stop na fase 13; verdere ontwikkeling vereist een nieuwe opdracht.

De gebruiker heeft deze v1.0.0-oplevering op 23 oktober 2026 geaccepteerd met
"goedgekeurd". Deze administratieve vastlegging wijzigt geen releasebestanden of hashes.
