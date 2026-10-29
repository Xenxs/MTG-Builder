# Werken vanuit GitHub

Repository: https://github.com/Xenxs/MTG-Builder. De gebruiker heeft op 29 oktober 2026
expliciet voor openbare plaatsing gekozen. De bestaande lokale werkmap blijft behouden.
Deze verhuizing verandert de applicatiefunctionaliteit en gewone collectie niet.

## Ophalen, bouwen en starten

Windows x64 met Git en de .NET 10 SDK:

~~~powershell
git clone https://github.com/Xenxs/MTG-Builder.git
cd MTG-Builder
dotnet run --project src/MTGDeckbuilder.Desktop
./scripts/Verify.ps1 -Screenshots
~~~

De solution bevat alle zes projecten, migraties, assets, ingebedde kaartprofielen,
kleine ontwikkel-/evaluatievoorbeelden, tests en documentatie. NuGet gebruikt alleen
de publieke nuget.org-bron. Er zijn geen applicatiesecrets nodig voor de bestaande
Scryfall-functionaliteit. Internet is nodig voor package restore en nieuwe kaartinformatie.

Een verse start krijgt de bestaande 202 aangenomen productkaarten, geen kopie van
iemands collectie. Gewone data staan onder de lokale gebruikersmap. Gebruik
MTGDECKBUILDER_DATA_DIR voor een afzonderlijke testmap; neem nooit een echte database
als testfixture. De optionele all-cards-UI-test vereist de afzonderlijk voorbereide,
gemarkeerde bulkfixture en meldt zonder die fixture expliciet een skip.

De bestaande GitHub Actions-workflow bouwt en test op Windows en bewaart de resultaten
als workflowartifact. Een lokale geslaagde controle bewijst nog geen geslaagde GitHub-run;
controleer daarvoor het tabblad Actions en de daadwerkelijke run.

## Wat lokaal blijft

- .local: grote Scryfall-downloads, geïsoleerde databases, installatieproeven en gereedschap.
- artifacts: historische meetrapporten, screenshots, installers, portable ZIP's en back-ups.
- bin/obj, gebruikersdatabases en logs, persoonlijke IDE-instellingen en sleutelbestanden.

De validatiedocumenten beschrijven werkelijk eerder uitgevoerde proeven. Hun grote
lokale bewijsbestanden worden niet automatisch via Git verspreid. De 33.727-kaartenproef
is geen meegeleverde standaardcollectie en kan vanuit een kale clone niet direct worden
gestart. De broncode van het meetgereedschap en de kleine A/B-referenties staan wel in Git.

## Windows-pakket opnieuw maken

Installeer Inno Setup 7.1 en voer uit:

~~~powershell
./scripts/Package.ps1 -IsccPath 'C:\pad\naar\Inno Setup\ISCC.exe'
~~~

Dit bouwt lokaal de self-contained Windows x64-pakketten met hashes onder artifacts/release.
Een bestaand releasepad wordt niet overschreven. De scriptuitvoer of een lokaal pakket
maakt nog geen GitHub Release; distributiebestanden horen als aparte release-assets.

## Verder ontwikkelen

De huidige functionele baseline is v1.4.0. Volg AGENTS.md, PROJECT_PLAN.md en
docs/ARCHITECTURE.md. Een nieuwe enginefase vereist een aparte opdracht; deze
repositorymigratie start die niet. Controleer wijzigingen lokaal vóór commit/push.
