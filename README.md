# Magic · Smart Deckbuilder — fysieke kaarten

Windows-app met .NET 10, Avalonia/MVVM, EF Core/SQLite en Serilog. Versie **1.4.0**
gebruikt fysieke Magic-kaarten: telefoonscan → ManaBox CSV → controle → lokale collectie.
Scryfall levert exacte uitgaven, kaarttekst en afbeeldingen. Het aangeleverde Magic-logo,
de oranje/gouden vormgeving en het programmaicoon zijn verwerkt.

## Starten

Deze repository bevat broncode, tests, documentatie en kleine voorbeeldbestanden.
Installeer de .NET 10 SDK en start vanuit de projectmap:

~~~powershell
dotnet run --project src/MTGDeckbuilder.Desktop
~~~

De eerder gebouwde installer en portable staan uitsluitend in de oorspronkelijke
lokale werkmap onder artifacts/release/1.4.0; ze worden niet als Git-bestanden meegeleverd.
Die pakketten bevatten de runtime en zijn ongetekend. Zie [installatie](docs/RELEASE_GUIDE.md)
en [werken vanuit GitHub](docs/GITHUB.md).

## Functies

- **Mijn collectie:** combineer naam, kaartkleur, synergie-aanwijzing, kaarttype en
  serie/set. Selecteer een uitgave voor de afbeelding. Online zoeken blijft via Scryfall.
- **Kaarten importeren:** ManaBox CSV met controle vóór toepassen, afzonderlijke taal,
  finish, conditie en map; historie en gecontroleerd herstel.
- **Mijn decks / Deckwizard:** naam en formaat, toegestane kleuren, thema en speelstijl,
  bezit of expliciete planning, en een commander indien nodig. Bekijk de complete lijst
  en tekorten vóór opslag als nieuw deck. Bestaande decks en bezit worden behouden.
- **Instellingen:** spelersnaam, opslagpaden, volledige back-ups en gecontroleerd herstel.
  Database legen vraagt een actuele preview, exact WISSEN en eerst een verplichte
  reservekopie. Na legen komen voorbeeldkaarten ook na herstart niet terug.

Lees [filters, wizard en legen](docs/MAGIC_WIZARD_HANDLEIDING.md) en
[kaarten scannen en importeren](docs/FYSIEKE_KAARTEN_HANDLEIDING.md).

De actieve app bevat geen Arena-schermen of digitale werkruimte. Oude gegevens en
migraties blijven intern bewaard voor upgrades en volledige back-ups. De historische
UI staat uitsluitend in het regressietestproject en wordt niet met het programma gebouwd.

Een nieuwe gegevensmap begint met aangenomen testbezit: Conflux **Naya Domain** en
**Bant on the March** (elk 41 kaarten), plus **Cloud** en **Sephiroth** uit de FINAL
FANTASY Starter Kit (elk 60). Samen **202 speelkaarten, 109 uitgaven en vier decks**.
Dit zijn gesimuleerde productlijsten, geen echte telefoonscan. Een eigen registratie
activeert een afzonderlijke voorraad; demo en eigen bezit worden niet opgeteld.

De afzonderlijke [alle-kaartenproef](docs/SYNERGY_FULL_CATALOG.md) gebruikt 33.727 fysieke Oracle-kaarten als gesimuleerd bezit. Je gewone collectie wordt hierdoor niet vervangen. De generator ondersteunt nu ook expliciet herkende commander-creaturetypes/castrollen en achtergrondfilters voor grote collecties.

## Grenzen

Synergie gebruikt 87 gecontroleerde productprofielen en beperkte herkenning voor overige kaarten. Relaties, voorwaarden, conflicten en onbekende werking staan in deck en wizard. [Gebruik en grenzen](docs/SYNERGY_ENGINE.md). Aggro, control en gebalanceerd
sturen een eenvoudige rangschikking. Geen bewezen combo's, optimale mana of speelsterkte.
De vaste plannen gebruiken 36 spreuken/24 basics of commander + 62 singleton spreuken/
37 basics. Planning zoekt een begrensde Scryfall-pool, geen uitputtende kaartcatalogus.
Eén ondersteunde commander; geen partner/background-paren of variabele kleurkeuze.

## Ontwikkelen en controleren

De zes projecten blijven Desktop, Application, Domain, Infrastructure, Domain.Tests en
Application.Tests. Domain is onafhankelijk; Application verwijst alleen naar Domain.

~~~powershell
dotnet run --project src/MTGDeckbuilder.Desktop
./scripts/Verify.ps1 -Screenshots
~~~

Normale data: %LOCALAPPDATA%/MTGDeckbuilder. Tests gebruiken uitsluitend geïsoleerde
databases. Een eigen testmap kan via MTGDECKBUILDER_DATA_DIR. Gebruik migraties, verwijder
nooit een bestaande database om een upgrade te forceren.

[Architectuur](docs/ARCHITECTURE.md) · [Besluiten](docs/DECISIONS.md) ·
[Voortgang](PROJECT_PLAN.md) · [Bestanden](docs/FILES.md) ·
[Uitgevoerde validatie fase 17](docs/VALIDATION_PHASE17.md)

