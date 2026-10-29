# MTG Arena Smart Deckbuilder 1.0 — Windows x64

Deze beperkte v1.0 bevat lokale opslag, kaartcatalogus, collectie-/deckimport,
Standard BO1/BO3-controles, beperkte kaartalternatieven, één vast mono-rood BO1-plan
en databaseback-up/herstel. Geen automatische Arena-sync of bewezen speelsterkte.

## Installeren en starten

Windows 10 vanaf build 19041 of Windows 11, x64. Alleen deze ontwikkelmachine is
daadwerkelijk getest; andere Windows-builds/hardware zijn geen gecertificeerde matrix.
De .NET-runtime is inbegrepen. Een SDK of afzonderlijke .NET-installatie is niet nodig.
Dubbelklik het setup-bestand. De installatie is voor de huidige gebruiker en vereist
geen administratorrechten. Standaard: `%LOCALAPPDATA%\Programs\MTGDeckbuilder`.
Start via het Startmenu. De portable zip kan volledig worden uitgepakt in een eigen
programmamap; start daarna MTGDeckbuilder.Desktop.exe. Verplaats niet alleen de exe.

De distributie is **ongetekend**. Er is geen uitgeverscertificaat; Windows kan een
vertrouwensmelding tonen. Vergelijk de SHA-256 met SHA256SUMS.txt bij een vertrouwde
herkomst. De hash is geen digitale handtekening. Verlaag geen beveiligingsbeleid om
een onbekend bestand te starten.

## Eerste gebruik

Werk via Catalogus de kaartbron online bij. Registreer bezit of importeer een ondersteund
JSON-bestand; kaartimport maakt geen bezit aan. Daarna kun je decks beheren of het
vaste mono-rode voorstel bekijken. Een lege collectie of oude bron blokkeert voorstellen
uit bezit. Planning is expliciet en mag ontbrekende kaarten bevatten.

## Gegevens, upgrade en verwijderen

Database, back-ups en logs: `%LOCALAPPDATA%\MTGDeckbuilder`, onafhankelijk van de
programmamap. Portable betekent verplaatsbare code; de gegevens blijven per gebruiker.
Laat MTGDECKBUILDER_DATA_DIR normaal leeg. Eén app per gegevensmap.
Maak vóór een upgrade via Instellingen een back-up en kopieer die ook naar een andere
schijf. Sluit de app en voer de nieuwe installer uit. Hetzelfde installatie-ID en pad
worden hergebruikt. Schema-upgrades krijgen bovendien automatisch een veiligheidskopie.
Een oudere app terugplaatsen op een nieuw schema is niet ondersteund.

Verwijderen kan via Windows Geïnstalleerde apps of unins000.exe in de programmamap.
Gebruikersgegevens en back-ups blijven behouden. Herinstallatie gebruikt ze opnieuw.
De installer vraagt geen automatische verwijdering van die gegevens.
Bij herstel wordt de hele database teruggezet bij de volgende start na bevestiging;
de huidige database moet leesbaar zijn voor de veiligheidskopie. Logs/cache vallen
buiten de back-up. Bij een fout: bewaar de gegevensmap en log; verwijder geen WAL-bestanden.

## Grenzen

Geen code signing, automatische updater, echte Arena-accounttest, gameplaytest of
schone-machinecertificering. De regels zijn op 22 oktober 2026 gecontroleerd en blokkeren
na veertien dagen zonder onderhoud; kaartbron mag maximaal 24 uur oud zijn. v1.0 is dus
een afgebakende release, geen onbeperkt actueel adviesproduct. Runtimeupdates vereisen
een nieuwe publicatie met bijgewerkte runtime. Inbegrepen derdenbestanden/notices blijven
bij de distributie; er wordt geen commercieel rechtenadvies gegeven.
