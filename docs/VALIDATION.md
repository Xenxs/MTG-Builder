# Validatie fase 1

Historisch rapport voor versie 0.1.0. De actuele versie 0.2.0 en fase-2-resultaten staan
in [VALIDATION_PHASE2.md](VALIDATION_PHASE2.md). Bestanden met generieke namen zoals
artifacts/native-start.json en artifacts/build.log worden bij volgende runs bijgewerkt;
de gedateerde fase-1-testrapporten en logregels blijven de historische bron.

Datum: 21 oktober 2026 (Europe/Amsterdam).

## Definitieve oplevering

| Controle | Werkelijk resultaat |
| --- | --- |
| SDK | .NET SDK 10.0.401, Windows x64, .NET runtime 10.0.12 |
| NuGet | Alle zes projecten hersteld vanaf nuget.org |
| Release-build | Geslaagd, 0 waarschuwingen, 0 fouten |
| Domeintests | 13 geslaagd, 0 mislukt, 0 overgeslagen |
| Applicatie-, opslag- en UI-tests | 14 geslaagd, 0 mislukt, 0 overgeslagen |
| Totaal | **27 geslaagd** |
| Windows-appstart | 21 oktober 2026, 20:51:01 +02:00: venster beschikbaar en proces reageert |
| Venstertitel | MTG Arena Smart Deckbuilder |
| Lokale opslag | Automatische migratie op de standaarddatabase in de lokale gebruikersmap |
| Visuele controle | Dashboard, Mijn collectie, Mijn decks en Instellingen gerenderd en bekeken |

De geteste versie is dezelfde bronversie als de definitieve build en native appstart.
De app is na de laatste start opengelaten zodat de gebruiker de schermen kan bekijken.

## Reproduceerbare opdrachten

```powershell
./scripts/Verify.ps1 -Screenshots
```

De definitieve build en test zijn ook afzonderlijk uitgevoerd:

```powershell
dotnet build MTGDeckbuilder.sln --no-restore -c Release -m:1 -p:UseSharedCompilation=false
dotnet test MTGDeckbuilder.sln --no-build --no-restore -c Release -m:1 --logger "trx;LogFilePrefix=phase1-final" --results-directory artifacts/test-results
```

De oorspronkelijke sandbox-testrun kon geen verbinding maken met de testhost en is
afgebroken. De testruns buiten die procesbeperking hebben de tests wel uitgevoerd.
De informatie-opdracht `dotnet --info` meldde in de sandbox onvoldoende toegang tot
Service Control Manager; de daadwerkelijke SDK-builds en restore functioneerden.

## Bewijsbestanden

- `artifacts/build.log`: definitieve geslaagde Release-build.
- `artifacts/test-results/phase1-final_net10.0_20261021205020.trx`: 13 domeintests.
- `artifacts/test-results/phase1-final_net10.0_20261021205025.trx`: 14 applicatie-/integratie-/UI-tests.
- `artifacts/native-start.json`: proces- en venstercontrole van de definitieve appstart.
- `artifacts/security-status.json`: Smart App Control Off, registerstatus 0, geen beleidswijziging door de agent.
- `artifacts/ui/dashboard.png`, `collection.png`, `decks.png`, `settings.png` en
  `dashboard-populated.png`: gerenderde echte Avalonia-schermen.

De artifacts-map wordt bewust niet als broncode behandeld. Er staan ook eerdere
afgebroken/mislukte runs in; de twee hierboven exact genoemde TRX-bestanden zijn
de definitieve testrapporten. De GitHub Actions-workflow is toegevoegd, maar er is geen
remote CI-run uitgevoerd en er is geen repository aangemaakt of gepubliceerd.

## Testdekking

- **Domain (13):** namen, identifiernormalisatie, lege sleutels, geldige aantallen,
  nulbezit, positieve deckaantallen, zones en naamlimieten.
- **Application (2):** invoernormalisatie en afwijzen vóór opslag.
- **SQLite (7):** echte migratiehistorie en modelconsistentie, idempotente initialisatie,
  persistentie na heropenen, upserts, conflicten, gedeelde kaartdefinities, nulbezit,
  mainboard/sideboard, cascadeverwijdering, foreign keys, SQL-checkconstraints en cancellation.
- **Avalonia (5):** navigatiebindingen en viewrendering voor vier schermen, volledige
  invoerstroom via viewmodelcommando's, dashboardverversing en herstel na invoerfouten.
  Aanvullend: herstel na een initialisatiefout, en weigeren van decimalen bij collectie-invoer,
  wijzigen van collectieaantallen en opslaan van deckaantallen. De persistentietest controleert
  ook dat een deckregel het collectornummer toont.

## Omgevingsbeperking: Windows App Control

Bij een aanvullende gewijzigde ontwikkelbuild blokkeerde Windows het laden van de
desktop-DLL met HRESULT **0x800711C7**. Event 3077 in
`Microsoft-Windows-CodeIntegrity/Operational` bevestigde het beleid
**VerifiedAndReputableDesktop**, policy-GUID `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`.
Een schone herbouw nam die blokkade niet weg. Latere testwijzigingen kregen eveneens
een laadblokkade. Deze runs tellen niet als geslaagde uitvoering.

De aanvullende wijzigingen zijn aanvankelijk teruggezet naar de eerder volledig geteste
fase-1-versie; toen slaagden alle 25 tests opnieuw. De gebruiker gaf daarna expliciet
toestemming voor beveiligingsbeleidsaanpassingen voor dit project.

Bij de daaropvolgende controle meldde Get-MpComputerStatus **SmartAppControlState Off**;
VerifiedAndReputablePolicyState was **0**. Dit was een waarneming vóór eventuele
wijzigingen: de agent heeft geen beveiligingsbeleid, uitzondering of vertrouwensregel
aangepast. Het precieze moment of de oorzaak van die statusverandering is niet vastgesteld.
De beheertool CiTool gaf bij het uitlezen toegang geweigerd; de beschikbare shell had
geen verhoogd Windows-administratortoken. Dat blokkeerde de build of tests niet.

De verbeteringen voor gehele aantallen en zichtbare collectornummers zijn vervolgens
alsnog doorgevoerd. Daarna slaagden alle **27 tests**, de Release-build had nul
waarschuwingen/fouten en de native app is gestart. De eerdere laadblokkade trad bij deze
uitvoering niet op. Er is geen code signing-traject uitgevoerd; eventueel beleid bij
latere ontwikkelbuilds moet op basis van de dan actieve toestand worden beoordeeld.
Zie de [Microsoft App Control-diagnosehandleiding](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/operations/appcontrol-debugging-and-troubleshooting).

## Bekende functionele grenzen

Geen volledige kaartcatalogus, kaartimport, afbeeldingen, Arena-logparser, legaliteitsregels,
slim deckadvies, cloudsync, automatische back-up of installer. Handmatige kaartnamen zijn
geen betrouwbare toekomstige importidentiteit. Meertalige uitgaven/varianten vragen een
uitgebreide printingsleutel. Gelijktijdig wijzigen vanuit meerdere appinstanties heeft
geen optimistische conflictcontrole. Headless schermtests vervangen geen volledige
Windows-testmatrix voor verschillende DPI-instellingen en toegankelijkheid.

De eerder genoemde beperkingen rond het afkappen van decimalen en ontbrekende
collectornummers in deckregels zijn in de definitieve vervolgcontrole opgelost.
