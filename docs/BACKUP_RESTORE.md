# Back-up en herstel — fase 12

Vooraf vastgelegd op 23 oktober 2026. Een handmatige back-up is een consistente kopie
van de volledige SQLite-database via de SQLite-backup-API. Dit werkt ook bij WAL;
los alleen het .db-bestand kopiëren is geen veilige online back-up.

Herstel wordt eerst gecontroleerd en uitsluitend na expliciete UI-bevestiging gepland
voor de volgende appstart. Vóór die start zijn geen databaseacties toegestaan. Een
veiligheidskopie bewaart de laatste toestand, waarna de SQLite-backup-API de gecontroleerde
kopie transactioneel terugzet. Een klein hersteljournaal houdt de voortgang bij.
Een tweede app voor dezelfde gegevensmap wordt geweigerd. Geen hernoemen van geopende
databases, geen verwijderen van losse WAL/journal-bestanden.

De back-up bevat kaarten, bezit, decks, instellingen en historie. Downloadcache, logs en
losse exportbestanden vallen erbuiten. Herstel maakt een oude bron niet weer actueel.
Dit is geen cloud-/periodieke back-up en geen garantie tegen verlies van de hele schijf.
Bewaar zelf een kopie buiten deze gegevensmap. Er wordt niets automatisch opgeruimd.

Bronnen: [Microsoft.Data.Sqlite online backup](https://learn.microsoft.com/dotnet/standard/data/sqlite/backup),
[SQLite Backup API](https://www.sqlite.org/backup.html) en [WAL-bestanden](https://www.sqlite.org/wal.html).

## Een back-up maken

Open **Instellingen → Back-up en herstel → Nu een back-up maken**. De app kopieert via
SQLite naar een tijdelijk bestand en controleert integriteit, verwijzingen en schema.
Pas daarna verschijnt de nieuwe `manual-…db` in de lijst. Het volledige pad is selecteerbaar.
Je kunt deze gesloten back-upkopie met Verkenner naar een andere schijf kopiëren. De actieve
`deckbuilder.db` kan nog WAL-wijzigingen hebben; kopieer die niet los terwijl de app draait.

De lijst toont ook bestaande `pre-migration-…db` en `pre-restore-…db`. Hun aanwezigheid
is geen bewijs dat een later gewijzigd bestand nog goed is: vóór herstel worden ze opnieuw
gecontroleerd. Er is geen opruimfunctie, overschrijving van bestaande back-ups of planning.
De app ondersteunt bestanden tot **512 MiB**. De synchrone SQLite-kopie draait buiten
de UI-thread; annulering is tijdens die kopie niet direct beschikbaar.

## Terugzetten

1. Kies een back-up uit de lijst en klik **Gekozen back-up controleren**, of gebruik
   **Ander back-upbestand kiezen…** voor een `.db` op een andere locatie.
2. Controleer bestandsnaam, aantallen en waarschuwing. Er is nog niets teruggezet.
   De app bewaart een afzonderlijke gecontroleerde kopie onder `recovery`; een latere
   wijziging van het oorspronkelijke bestand verandert deze voorbereiding niet.
3. Vink expliciet aan dat je de **volledige database** wilt terugzetten en klik
   **Herstel plannen voor volgende appstart**. Dit is geen samenvoeging. Latere
   wijzigingen aan kaarten, bezit, decks, instellingen en historie verdwijnen uit de
   actieve database. Het huidige plan blijft vóór toepassen annuleerbaar.
4. Sluit de app normaal en start opnieuw. Sluit ook andere programma's die de database
   openen. Slechts één desktopapp mag dezelfde gegevensmap gebruiken.
5. Vóór database-initialisatie controleert de app de kopie opnieuw. De nieuwste toestand
   komt in een nieuwe `pre-restore-…db`. Daarna volgt de transactionele terugzetactie,
   de herstelontvangst en zo nodig migratie van een oudere ondersteunde versie.
6. Instellingen toont **Herstel voltooid bij appstart** en de veiligheidskopie. Controleer
   bezit/decks. Om terug te keren naar de latere toestand kun je die veiligheidskopie
   op dezelfde manier controleren, bevestigen en bij een volgende start terugzetten.

Een nieuwe pc kan eerst de app normaal starten met een lege database; kies daarna je
externe back-up in Instellingen. De bron wordt niet als actieve gebruikersdatabase geopend.
Ververs na herstel de catalogus online. Importhistorie kan verwijzen naar downloadcache
die niet in de back-up zit; ontbreekt die, start de catalogusimport opnieuw.

## Fouten en onderbreking

Een beschadigde bron, ongeldige verwijzingen, onbekende migratie of afwijkend schema
(ook extra triggers/indexen) wordt geweigerd. De vergelijking gebruikt een tijdelijk
schema dat alleen uit meegeleverde migraties is opgebouwd. Dit is een integriteits-
en compatibiliteitscontrole; SHA-256 is geen handtekening of bewijs van afzender.

Bij een gewijzigde/ontbrekende herstelkopie vóór toepassen blijft de normale start
geblokkeerd met een foutmelding. **Gepland herstel annuleren en opnieuw proberen**
kan een nog niet begonnen terugzetactie annuleren zonder databasewijziging.

Zodra de veiligheidskopie en status `Applying` zijn vastgelegd, wordt annuleren geweigerd.
Een nieuwe start herhaalt veilig dezelfde kopie voordat de app bruikbaar wordt. De eerste
veiligheidskopie blijft behouden en wordt op hash gecontroleerd. Een fout na terugzetten
maar vóór het vastleggen van `Applied` geeft dus geen vrijgave voor nieuwe gebruikersacties.
Na `Applied` wordt die terugzetactie niet opnieuw uitgevoerd.

Bij een beschadigd/onbekend hersteljournaal of gewijzigde veiligheidskopie: sluit de app,
bewaar de hele gegevensmap inclusief database, eventuele WAL/journal-bestanden, `backups`
en `recovery`, en laat de toestand onderzoeken. Verwijder geen losse WAL-bestanden en
zet een onderbroken `Applying`-actie niet handmatig op voltooid.

**Grens:** deze functie vereist dat de huidige database nog als geldige veiligheidskopie
kan worden gelezen. Bij een fysiek beschadigde of ontoegankelijke actieve database stopt
de app veilig; er is nog geen automatische reparatie of begeleide noodherstelmodus.
Externe SQLite-programma's moeten gesloten zijn; zij volgen het appdatamapslot niet.
Er zijn lock-/writefouten getest, geen echte stroomuitval, schijfdefect of volgeschreven schijf.

Uitvoering en controlebewijs: [VALIDATION_PHASE12.md](VALIDATION_PHASE12.md).

## Actueel in v1.2.0

De back-up omvat alle twintig applicatietabellen, inclusief historische gegevens.
Migratie negen voegt Settings.ExamplesEnabled toe. Herstel accepteert alleen exact
herkende schema's; een bekende oudere versie wordt bij appstart via migraties bijgewerkt.

De lijst toont ook pre-reset-reservekopieën. Die ontstaan uitsluitend nadat de gebruiker
in Instellingen een actuele wispreview heeft gecontroleerd en exact WISSEN heeft getypt.
Na succesvol legen blijft één standaard instellingenrij met voorbeelden uitgeschakeld.
Back-ups, logs en afbeeldingen worden niet gewist. Herstel van een pre-reset-kopie zet
ook de eerdere voorbeeldinstelling terug. Zie [legen en terugzetten](MAGIC_WIZARD_HANDLEIDING.md).

