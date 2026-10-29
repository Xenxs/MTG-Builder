# Magic Smart Deckbuilder 1.4.0 — Windows installeren

Fysieke kaarten, ManaBox CSV, Scryfall, Magic-thema, collectiefilters en een deckwizard.
Geen Arena-werkruimte in het programma. Historische databasegegevens blijven bij een
gewone upgrade behouden.

## Installeren of upgraden

Sluit de vorige app en start MTGDeckbuilder-1.4.0-win-x64-setup.exe uit de releasemap.
De installatie is per gebruiker, voor Windows 10 2004 of nieuwer en Windows 11 x64.
De runtime is inbegrepen. Open daarna **MTG Smart Deckbuilder** in het Startmenu.

Installatie-identiteit en gegevensmap blijven gelijk. Deze versie verandert het schema niet: twintig tabellen en negen migraties. Een upgrade vanaf v1.3.0 vraagt geen nieuwe migratie. Bij een oudere database worden de bestaande migraties met voorafkopie toegepast. Bestaande collectie/decks worden behouden.

Installer en programma zijn **ongetekend**. De SHA-256-bestanden controleren integriteit,
geen uitgeversvertrouwen. Er is geen test op een schone tweede pc of ARM-machine.
Dit is een lokaal gevalideerde Windows x64-distributie.

## Portable starten

Pak MTGDeckbuilder-1.4.0-win-x64-portable.zip volledig uit en start
MTGDeckbuilder.Desktop.exe. De hele map is nodig. Portable en installer gebruiken
standaard dezelfde gegevensmap; start nooit twee versies tegelijk op dezelfde data.

## Gebruik en opslag

Zie [handleiding filters en wizard](MAGIC_WIZARD_HANDLEIDING.md),
[telefoonscan/CSV](FYSIEKE_KAARTEN_HANDLEIDING.md) en [Commander](COMMANDER.md).
Een verse gegevensmap krijgt vier duidelijk gemarkeerde productdecks als testbezit:
202 kaarten. Na database legen is deze automatische voorbeeldvulling uitgeschakeld.
Internet is nodig voor nieuwe kaartinformatie en niet-gecachete afbeeldingen.

Data: %LOCALAPPDATA%/MTGDeckbuilder; programma:
%LOCALAPPDATA%/Programs/MTGDeckbuilder. Instellingen toont de werkelijke paden.
Database, back-ups, afbeeldingen en logs staan buiten de installatiebestanden.
Bewaar ook een kopie van back-ups buiten deze pc.

Verwijderen via Windows verwijdert het programma en de snelkoppeling. De gegevensmap
blijft behouden; herinstallatie gebruikt dezelfde data. **Database legen is een andere,
expliciete actie in Instellingen**, inclusief voorafkopie en bevestiging.

Een afzonderlijke testmap kies je vóór starten:

~~~powershell
$env:MTGDECKBUILDER_DATA_DIR = Join-Path $env:TEMP 'MTG-fysieke-proef'
& '.\MTGDeckbuilder.Desktop.exe'
~~~

Deze keuze geldt voor dat proces. Een gewone Startmenu-start gebruikt de normale map.
Teruggaan naar oude programmaversies na een migratie is niet ondersteund. Herstel
eventueel een geschikte oudere back-up met de bijbehorende oude app in een aparte map.
De app accepteert alleen herkende, exact gecontroleerde schema's. Een herkende oudere back-up wordt na terugzetten via de gewone migraties bijgewerkt.

## Onderhoud

Standard- en Commander-bronnen en regels kunnen verlopen. Vernieuw kaartgegevens en
onderhoud de regelversie wanneer de app dat vraagt. De wizard bouwt begrensde voorstellen;
thema en stijl bewijzen geen combo's, speelsterkte of optimale manabase.
Zie [bewijs en beperkingen](VALIDATION_PHASE17.md).



De ontwikkelwerkmap bevat daarnaast een afzonderlijke, tijdelijke [alle-kaartenproef](SYNERGY_FULL_CATALOG.md). Deze simuleert bezit en zit niet als gebruikerscollectie in de installer.

