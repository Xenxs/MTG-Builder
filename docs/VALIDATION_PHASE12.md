# Controleverslag fase 12

Uitgevoerd op **23 oktober 2026**, Windows x64, .NET SDK **10.0.401**, **v0.12.0**.
De gebruiker accepteerde fase 11 met "goedgekeurd start fase 12". Dezelfde gebruiker
bevestigde dat er geen echte export beschikbaar is: gebruik gesimuleerde voorbeelden.
Deze fase levert back-up/herstel en technische praktijkcontrole, geen echte account-
of gameplaytest. Gebruik: [BACKUP_RESTORE.md](BACKUP_RESTORE.md).

## Build en automatische controles

Werkelijk uitgevoerd na de laatste bronwijzigingen:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase12-delivery
```

Restore geslaagd, Release-build **0 waarschuwingen, 0 fouten**.
**362 tests geslaagd, 0 mislukt, 0 overgeslagen**:

| Suite | Geslaagd | TRX onder artifacts/test-results |
| --- | ---: | --- |
| Domain | 33 | phase12-delivery_net10.0_20261023112323.trx |
| Application / SQLite / Avalonia | 329 | phase12-delivery_net10.0_20261023112404.trx |

**19 nieuwe tests**: zestien herstel-/databasegevallen en drie UI-gevallen. De gerichte
filter Backup voerde ook bestaande back-upgerelateerde controles uit. Alle eerdere
tests zijn behouden. Het eerste bouwen vereiste een expliciet generiek returntype
voor RecoveryStatus. Twee xUnit-analyzerfouten in de nieuwe testopbouw zijn hersteld.
De eerste uitvoering vond vervolgens twee onjuiste verwachtingen: de fixture registreert
dertig kaarten inclusief uitsluitingen, en Windows geeft UnauthorizedAccessException
bij de gebruikte journaalvergrendeling. De aantallen en platformgebonden toegangsfout
zijn gecorrigeerd; de veiligheidsuitkomsten blijven volledig getoetst.

De nieuwe controles bewijzen onder meer:

- Database-rondje inclusief kaarten, bezit/Version, decks, instellingen, historie en
  generatorontvangsten; tabelinhoud gelijk na herstel en herstart.
- Online back-up met committed WAL-data; de broninhoud blijft ongewijzigd.
- Alleen volledige gecontroleerde handmatige bestanden publiceren; annulering vóór
  uitvoering maakt geen back-up of herstelverzoek.
- Expliciete bevestiging vereist; gepland herstel wijzigt de database nog niet;
  annuleren vóór toepassen laat de nieuwste gegevens staan.
- Onbekende migratie, vreemde trigger/index, foreign-keyfout en corrupt bestand geweigerd.
- Extern bestand mag na inspectie veranderen; de reviewkopie blijft de geselecteerde bron.
- Gewijzigde reviewkopie blokkeert plannen/start; gewijzigde veiligheidskopie blokkeert retry.
- Oude bekende migratieprefix terugzetten en daarna naar de zeven huidige migraties gaan.
- Vergrendeld doel vóór kopiëren: eerste veiligheidskopie en Applying-status blijven behouden.
- Geblokkeerde journaalpublicatie ná commit: geen normale vrijgave, veilige retry met dezelfde
  veiligheidskopie; een Applied-ontvangst wordt niet opnieuw toegepast.
- Exclusief appdatamapslot, ongeldige/verlopen lease geweigerd, corrupt/onbekend journaal
  geeft geen stilzwijgende start zonder herstel.
- Werkelijke bindings voor instellingen, lijst, review, bevestiging, planning/annuleren,
  bronkeuzewijziging en centrale fouten; startblokkade annuleerbaar vóór toepassen.

Er is geen werkelijke stroomuitval, fysiek defect, volle schijf of geïnjecteerde fout
midden in de native SQLite-backup-call uitgevoerd. De transactiegarantie daarvan komt
van SQLite; de uitgevoerde fouten liggen vóór kopiëren en na commit in het journaalvenster.
Annulering tijdens de synchrone backup-call is niet onmiddellijk mogelijk.

## Architectuur en bestanden

Nieuwe Application-contracten in BackupContracts.cs, infrastructuur in SqliteBackupService,
DataDirectoryLease en RecoveringDatabaseInitializer; MVVM-coördinatie in BackupViewModel.cs;
BackupRecoveryTests.cs en BackupUiTests.cs; handleiding en dit verslag. Bijgewerkt: DI,
desktopcomposition, shell/startweergave, instellingenview/bestandselectie, versies en
documentatie. [FILES.md](FILES.md) geeft het volledige actuele overzicht.

Geen nieuw NuGet-pakket, tabel of migratie: de zeven migraties uit v0.11.0 blijven gelijk.
De pool, generieke classifier en generator zijn inhoudelijk ongewijzigd. De poolresource
behoudt SHA-256 `407B61C6C8144027A7F57313178419668D77205F5CA7B098D96FE065047CC486`.
Geen externe AI-API, secrets of beveiligingsbeleidswijziging.

Een herstelvoorbereiding is een afzonderlijke snapshot plus SHA-256, aantallen en
migratieversie. sqlite_master wordt vergeleken met een tijdelijke database uit de
meegeleverde migraties. Startup-herstel gebeurt vóór normaal databasegebruik, onder
een levensduurslot voor de desktopapp en een bewerkingsslot. De eerste veiligheidskopie
en hash blijven behouden. Het journaal is Scheduled → Applying → Applied, atomair
gepubliceerd na flush. Na herstel volgt de bestaande migratie-/importinitialisatie.
Er worden geen actieve databasebestanden hernoemd of losse WAL-bestanden verwijderd.

## Praktijkproef met echte catalogus en gesimuleerde collectie

Een nieuwe geïsoleerde database `.local/phase12-recovery-data` is gekopieerd uit de
fase-11-proef, niet uit de normale gebruikersdatabase. De actuele online Scryfall-
import leverde bron **2026-10-23T09:05:40.788Z**, run
`e33eda92-dfa2-418c-a7a2-48574909a648`: **26 nieuwe Arena-printings**, nul gewijzigde,
**21.673 Arena-printings** uit **119.158** verwerkte bronrecords. De exacte bron is
vastgelegd in `artifacts/phase12-real-recovery.json`.

Het bestaande volledige voorstel bleef geldig. Vijftien herhaalde berekeningen
gaven dezelfde regels. Daarna: back-up, broninspectie, spelersnaam veranderen,
Mountains naar nul, bezitmodus geblokkeerd, planning met 22 tekorten als nieuw deck
opslaan, herstel plannen, provider/verbindingen sluiten, herstellen en herinitialiseren.

| Gemeten controle | Resultaat |
| --- | --- |
| Back-upgrootte | 36.999.168 bytes, circa 35,3 MiB |
| Back-up inclusief validatie | 0,683 seconde |
| Inspectie en eigen reviewkopie | 0,659 seconde |
| Herstel plus initialisatie | 1,491 seconde |
| Vijftien deckberekeningen, minimum/mediaan/maximum | 6,72 / 7,35 / 25,07 ms |
| Piekgeheugen hele proef, inclusief online import | circa 318,2 MiB |
| Alle vijftien applicatietabellen plus migratiehistorie na herstel | Gelijk aan back-up |
| Veiligheidskopie | Gelijk aan de latere toestand vóór terugzetten |
| Na back-up gemaakt planningdeck | Uit actieve database verdwenen, in veiligheidskopie bewaard |
| Herstart / opnieuw voorstellen | Dezelfde volledige lijst |

De data zijn echte kaartmetadata met **gesimuleerd bezit/decks**. Er is geen echte
Arena-export/account, Arena-import, wedstrijd, onafhankelijke reviewer of speelsterkte-
meting gebruikt. Dit is één lokale prestatieproef, geen algemene hardwarebenchmark.

Aanvullend is een nieuwe kopie van de oudere gevulde proef gebruikt. Die had vijf
adviesontvangsten maar nog geen collectie-/deckimporthistorie. Via de echte export-/
importfuncties zijn daarom expliciete gesimuleerde importontvangsten toegevoegd.
Het volledige back-up/herstelrondje vergeleek daarna alle vijftien tabellen plus
migratiehistorie, inclusief 2.005 bezitsregels, één collectie-import met 2.005 regels,
een toegepaste deckimport en de vijf oude adviesontvangsten. Alle inhoud gelijk na
herstel. Bewijs: phase12-history-roundtrip-final.json en phase12-history-restored-final.json.

## Native Windows-herstel en tweede instantie

Na de geautomatiseerde praktijkproef is in die geïsoleerde map nogmaals herstel gepland,
een latere spelersnaam opgeslagen en de echte desktop gestart. Bewijs:
`artifacts/phase12-native-restore.json`, native-restore-before/after.json en
native-restore-expected.json. Proces **22836**, rond **11:25:32 Europe/Amsterdam**.
Het log meldt herstel toegepast om **11:25:34.339**, vóór database-initialisatie om
**11:25:34.594**. Alle vijftien applicatietabellen zijn gelijk aan de gekozen back-up.
Het venster reageerde; de app is normaal gesloten.

Een tweede desktopproces voor dezelfde map werd daadwerkelijk gestart en toonde
**MTG Deckbuilder — al geopend**. Ook dit venster reageerde en is normaal gesloten.
De melding en verwachte waarschuwing horen bij deze bewuste blokkade, niet bij een
onverwachte fout van de primaire app.

## Bestaande gegevens en normale start

De bestaande gevulde fase-9-proef startte met v0.12.0 als proces **8392** rond
**11:25:36**, reagerend en normaal gesloten. De normale gebruikersdatabase startte
als proces **30296** rond **11:25:38**, reagerend open gelaten. Beide starters hadden
nieuwe versie-/initialisatielogs zonder waarschuwingen of fouten. Op deze twee bestaande
databases is **geen herstel toegepast**.
Laatste controle om **11:34:56 Europe/Amsterdam** bevestigt proces 30296 nog reagerend
met dezelfde assemblyhashes; bewijs: phase12-final-process-check.json.

Vóór/na zijn alle kolommen van **vijftien applicatietabellen** gesorteerd/gehasht,
inclusief CatalogStaging, Collection.Version en beide typen adviesontvangsten. Alles
gelijk, zeven migraties gelijk en alle bestaande back-ups bytegewijs gelijk. De gevulde
proef behoudt 2.005 bezitsregels, twaalf decks, 31 deckregels en vijf eerdere
vervangingsontvangsten. De normale database bevat nog geen bezit/decks en heeft nog
een oude catalogus zonder legaliteit; een app-upgrade maakt geen collectie of actuele bron.

Bewijs onder artifacts: phase12-user-before/after.json, phase12-filled-before/after.json,
phase12-backups-before.json, phase12-data-preservation.json, phase12-synthetic-native-start.json
en phase12-user-native-start.json. De audit opent databases uitsluitend read-only.
De standaardstart maakt alleen nieuwe slot-/recoverymappen, logs en zo nodig een lockbestand;
geen gebruikersdatabasewijziging of nieuwe schema-upgrade.

Definitieve assembly SHA-256:

- Desktop: `27490964817D127BC7A7F4514846EC9B0BFC35867A0B0D2D0F6615F9AC824170`
- Infrastructure: `EDE47594AD05E2C8C42F4C02DE7FA705D9C455FE3BAE5A96374EB4C447820546`

## Schermcontrole

Werkelijk bekeken onder artifacts/ui: backup-reviewed-confirmation.png,
backup-restore-scheduled.png en backup-startup-blocked.png. De eerste toont aantallen,
de volledige vervangingswaarschuwing en de aanvankelijk uitgeschakelde planningsknop;
de tweede planning en annuleren; de derde een geblokkeerde start met herstelactie.
Na inspectie is pagina-inhoud tijdens initialisatiefouten verborgen, zodat niet-geladen
dashboardnullen geen gegevensverlies suggereren. Daarna is de volledige Verify uitgevoerd
en zijn de bijgewerkte beelden opnieuw bekeken. De gewone settings-render wordt ook
door de bestaande navigatietest gegenereerd.

Dit zijn Avalonia-headless renders met echte bindings. Het native bewijs controleert
proces/venster/logs en inhoud na startup-herstel. De native bestandsdialoog is niet
automatisch bediend; het doorgegeven bestandspad is apart via de gebonden VM getest.

## Grenzen en uitvoering

Geen periodieke/cloudback-up, opschoning, encryptie, automatische database-reparatie of
noodherstel van een fysiek beschadigde actieve database. De huidige database moet nog
leesbaar zijn voor de veiligheidskopie. Logs/cache vallen buiten de back-up; ontbrekende
cataloguscache vereist opnieuw importeren. Externe SQLite-tools volgen het desktopslot
niet en moeten gesloten zijn. SHA-256 is geen afzenderhandtekening. Bron-/regelverval
en de beperkte strategische dekking uit eerdere fasen blijven gelden.

Start circa **11:05 Europe/Amsterdam**. Advies **GPT-6.1 Sol / High** voor betrouwbare
herstelvolgorde en gegevenscontrole; [officiële modeldocumentatie](https://developers.openai.com/api/docs/models/gpt-6.1-sol).
Geen ondersteunde directe modelbediening beschikbaar; chatinstellingen niet gewijzigd.
Startraming **1–3 actieve uren**, inclusief bouwen/tests/docs. Werkelijke uitvoering
circa **30 minuten**, van 11:05 tot circa 11:35 Europe/Amsterdam. De raming was ruim
door hergebruik van bestaande infrastructuur. Geen installer gebouwd; fase 13 is niet gestart.
