# Controleverslag fase 11

Uitgevoerd **23 oktober 2026**, Windows x64, .NET SDK **10.0.401**, **v0.11.0**.
Startopdracht: "fase 10 akkoord start fase 11". Fase 10 is daarmee geaccepteerd.
Scope: complete begrensde mono-rode BO1-voorstellen met bezit, expliciete planning,
mana/curve, tekorten, voorwaarden en veilige opslag. Gebruik: [DECK_GENERATION.md](DECK_GENERATION.md).

## Definitieve build en automatische tests

Werkelijk uitgevoerd, na de verduidelijking uit de eerste scherminspectie:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase11-delivery-final
```

Restore geslaagd; Release-build **0 waarschuwingen, 0 fouten**.
**343 tests geslaagd, 0 mislukt, 0 overgeslagen**:

| Suite | Geslaagd | TRX onder artifacts/test-results |
| --- | ---: | --- |
| Domain | 33 | phase11-delivery-final_net10.0_20261023105731.trx |
| Application / SQLite / Avalonia | 310 | phase11-delivery-final_net10.0_20261023105800.trx |

Er zijn **40 nieuwe tests**: 5 Domain en 35 Application/integratie/UI. Bestaande tests
blijven behouden; de historische upgradecontrole verwacht nu zeven migraties.
Een eerste build kon bestanden niet vervangen door de nog geopende v0.10.0-app.
Die projectinstantie is normaal gesloten; daarna bouwden alle controles zonder fouten.
Bij de eerste nieuwe UI-testcompilatie is een onjuiste testproperty Items naar Decks
hersteld. Geen productiebug of test weggelaten om de build groen te maken.

Vooraf vastgelegde criteria staan in PROJECT_PLAN.md en DECK_GENERATION.md, geschreven
vóór de generatorimplementatie. De controles omvatten:

- Exact 60 mainboard, 22 Mountains, 20 creatures, 18 overige spreuken en alle functiedoelen.
- Gedrukte curve: minimaal twaalf 1-drops, gemiddelde niet-land-MV maximaal twee.
- Geen verzonnen bezit, expliciete planning met zestig ontbrekende kaarten inclusief landen.
- Ontoereikende landen, creatures, verkeerde functies en ontbrekende goedkope kaarten.
- Herdrukken met één Oracle-kopiegrens en exacte toewijzing aan werkelijk bezeten printings.
- Bezit vóór voorkeur, voorwaarden zichtbaar, determinisme bij omgekeerde invoervolgorde.
- Metadatawijziging, ban, inactieve printing, ontbrekende/oude bron en verlopen regels.
- Gewijzigd bezit en terugzetten, nieuwe catalogusversie, verouderde berekening/preview.
- Bron-/regelverval zonder datamutatie; gewijzigde engine-/poolversie of previewregels.
- Voorbereiden maakt geen deck; opslag als nieuw BO1-deck; bestaande data behouden.
- Herstart, dubbele toepassing, afzonderlijke serviceproviders, verwijderd resultaat en
  oude toegepaste ontvangsten zonder hercreatie.
- Fout én annulering na SQL-writes rollen deck en ontvangst volledig terug; veilige herhaling.
- Upgrade van gevuld fase-10-schema, migratieback-up, eerdere vervangingsontvangst behouden.
- Gebonden UI: navigatie, modewissel, kaartvoorwaarden, naamwijziging, opnieuw controleren,
  nieuwe-deckopslag, zichtbaarheid bij Mijn decks en herstel na een centrale stale-foutmelding.

## Implementatie en afbakening

Nieuwe bestanden: Application/DeckGeneration.cs; Domain/DeckGenerationPlan.cs;
Infrastructure/SqliteDeckGenerationStore.cs; de migratie plus designer;
Desktop/ViewModels/DeckGenerationViewModel.cs en Views/DeckGenerationView.axaml;
vier nieuwe testbestanden; gebruiksdocument en dit verslag. Bijgewerkt: gedeelde
poollezer, model-snapshot, DI, shell/navigatie, eenvoudige viewconstructor, versies,
historische upgradetest, UI-test-DI en projectdocumentatie. [FILES.md](FILES.md) is het
complete actuele overzicht. Zes projecten, geen nieuw NuGet-pakket of externe AI-API.

De vaste template is 22 landen, 12 vroege creatures, 4 spell-aanvallers, 4 pingers/
enters-burn-creatures, 8 burn, 2 creature-interactie, 6 combat en 2 handverversers.
Bezit gaat vóór poolvoorkeur, gedrukte MV, naam en identiteit. Curveherstel wisselt
alleen binnen dezelfde functie. Geen uitputtend optimaal-deckonderzoek of gameplaymodel.
De fase-10-pool en de twintig eerdere adviesreferenties zijn bytegewijs ongewijzigd:

| Bestand | SHA-256 |
| --- | --- |
| mono-red-aggro-v1.json | 407B61C6C8144027A7F57313178419668D77205F5CA7B098D96FE065047CC486 |
| mono-red-advice-reference-v1.json | 944E1E3DC93F0EC3AA56C51B4A2DB64A32F3EEEA7208BACBCB34D22274E12030 |

Poolcontrole en generator gebruiken dezelfde transactionele lezer en StandardDeckValidator.
De algemene classifier en generieke vervangingsengine zijn inhoudelijk ongewijzigd.
Voorbereidingen staan in DeckGenerationPlans, zonder nep-oorspronkelijk deck. De eigen
migratie `20261023084552_AddDeckGenerationPlans` voegt alleen die tabel toe. De naam,
controlecode, versieerbare aanvraag, exacte regels en volledige uitleg blijven bewaard.
Voorraad wordt niet verbruikt, gereserveerd of bij andere decks afgetrokken.

## Echte catalogusproef met gesimuleerd bezit

Bewijs: `artifacts/phase11-real-generation-final.json`, bijbehorende log en
`artifacts/phase11-generated-deck.txt`. De lokale diagnosetool `.local/GenerationProbe`
is met de definitieve assemblies gebouwd en gebruikt een nieuwe geïsoleerde kopie
`.local/phase11-smoke-data-final`. Geen echte gebruikerscollectie als testfixture.

Twee online Scryfall-controles bevestigden AlreadyCurrent=true voor bron
**2026-10-22T21:05:50.017Z**, run `dd538490-84e7-4b91-b772-2e01de1cac33`.
De bestaande echte catalogus bevat 21.647 Arena-printings. Er was geen nieuwe bulkdownload
nodig. Alle 27 ondersteunde poolkaarten bleven geschikt. Bezit is bewust gesimuleerd:
22 Mountains en vier per ondersteund niet-land; geen uitgelezen Arena-account.

| Controle | Resultaat |
| --- | --- |
| Mainboard / sideboard | 60 / 0 |
| Mountains / creatures / overige spreuken | 22 / 20 / 18 |
| Ontbrekend bezit | 0 |
| 1 mana / 2 mana / 3+ mana | 24 / 14 / 0 niet-landen |
| Gemiddelde gedrukte niet-land-MV | 1,3684 |
| Berekening | circa 0,045 seconde |
| Piekgeheugen gehele proef | circa 112,5 MiB |
| BO1-controle na opslag | Geslaagd volgens opgeslagen brondata/ondersteunde regels |
| Herstart vóór toepassen | Exact dezelfde regels |
| Dubbel toepassen | Dezelfde deck-ID, één nieuw deck |
| Gewijzigd bezit na preview | Oude preview geweigerd |
| Bestaand technisch deck | Ongewijzigd |
| Bezit inclusief Version-velden bij opslag | Ongewijzigd |
| Arena-tekstexport | Gemaakt via bestaande exportfunctie |

De export is een technisch gegenereerd voorstel, **geen bewezen competitieve decklijst**.
Er is geen Arena-game gespeeld en de export is niet in een echte Arena-client geïmporteerd.
De tijd/geheugenwaarden zijn één lokale proef, geen representatieve prestatiebenchmark.

## Gerenderde schermcontrole

Werkelijk bekeken onder artifacts/ui:

- generation-owned-conditions.png: expliciete kaartvoorwaarden en volledige tekst.
- generation-owned-preview.png: naam, bezitmodus, zestig kaarten en actieve opslagknop.
- generation-planning-preview.png: alle tekorten, planninglabel en planning-opslagknop.
- generation-saved.png: ontvangst zichtbaar, opslagknop uitgeschakeld.
- generation-full-list-standard-window.png: complete lijst bij normale hoogte 850.
- generation-insufficient-stock.png: functieblokkades en expliciet onvolledig plan.
- generation-no-source.png: ontbrekende bron blokkeert ook planning.
- pool-owned-options.png: bestaande pool met bijgewerkte verwijzing naar Deckvoorstel.

De eerste inspectie wees een verwarrende tekst aan: een lege deellijst meldde geen
kaarttekorten terwijl het vaste plan niet haalbaar was. Deze tekst verduidelijkt nu
dat het plan onvolledig is en functieblokkades eerst opgelost moeten worden. Daarna
de volledige verificatie opnieuw uitgevoerd. UI-captures scrollen naar preview/save
en ontvangst om die werkelijk zichtbaar te controleren. Inhoud is verticaal scrollbaar.
Dit zijn Avalonia-headless renders met echte bindings, geen native schermopnamen.

## Native start en gegevensbehoud

De definitieve Windows-app is daadwerkelijk gestart:

| Database | Proces | Tijd Europe/Amsterdam | Resultaat |
| --- | ---: | --- | --- |
| Bestaande gevulde fase-9-proef | 3544 | 10:58:06–10:58:08 | Reagerend venster, v0.11.0/initialisatie gelogd, normaal gesloten |
| Normale gebruikersdatabase | 13000 | vanaf 10:58:08 | Reagerend venster, v0.11.0/initialisatie gelogd, open gelaten |

Bewijs: phase11-synthetic-native-start.json en phase11-user-native-start.json onder
artifacts. Alleen nieuwe logregels beoordeeld; geen waarschuwing/fout/fatale melding.
De native start bewijst proces/venster/initialisatie; de UI-bediening is afzonderlijk
met headless bindings getest. Native computerbediening is hier niet beschikbaar.
Laatste procescontrole om **11:04:08 Europe/Amsterdam** bevestigt proces 13000 nog
reagerend met dezelfde assemblyhashes; bewijs: phase11-final-process-check.json.

Vóór en na de upgrade zijn **alle kolommen van dertien bestaande tabellen** per database
gesorteerd en gehasht. Inclusief Collection.Version, kaarten, printings, catalogus, legaliteit,
decks, instellingen en import-/advieshistorie. Alles gelijk. De nieuwe tabel is leeg.
De gevulde proef behoudt 2.005 bezitsregels, 12 decks, 31 deckregels en 5 eerdere
adviesontvangsten. De normale gebruikersdatabase had geen collectie/decks en nog geen
legaliteitsmetadata; een upgrade maakt daar geen bezit of actuele kaartbron bij.

Zes eerdere migraties zijn behouden en de zevende is toegevoegd. Beide databases kregen
precies één nieuwe pre-migration-back-up; eerdere back-ups zijn bytegewijs behouden.
De nieuwe back-ups zijn read-only geopend: zes migraties, geen nieuwe tabel, alle
dertien bestaande tabelhashes gelijk aan de vóórtoestand. Herstelbaarheid van deze
kopie is inhoudelijk gelezen; er is geen productfunctie voor gebruikersherstel gebouwd.

Bewijsbestanden onder artifacts: phase11-user-before/after.json, phase11-filled-before/
after.json, phase11-backups-before.json, phase11-user-backup-check.json,
phase11-filled-backup-check.json en phase11-data-preservation.json. De audittool schrijft
uitsluitend rapporten en opent de databases/back-ups read-only.

Definitieve assembly SHA-256:

- Desktop: B46C444C368F46E110569FC0AEA2C7A323AD9760CDFEC19BD922EB9087B0F64E
- Infrastructure: 91C6AC40F2163ADC516CFFAD14CE892B199025301D2E79DD653EBE590927F83F

## Model, uitvoering en grenzen

Advies **GPT-6.1 Sol / High** voor samenhangende selectie-/voorraad-/opslagcontroles.
De [OpenAI modeldocumentatie](https://developers.openai.com/api/docs/models) bevestigt
de beschikbaarheid van het model en high-reasoning; de keuze voor dit project is een
inhoudelijke ontwikkelinschatting. Geen ondersteunde directe instellingenbediening
beschikbaar, chatinstellingen niet gewijzigd. Geen modelwissel geclaimd.

Start circa 10:34 Europe/Amsterdam. Startraming 3–6 actieve uren; na implementatie en
eerste praktijkproef bijgesteld naar 20–40 minuten resterend. Hergebruik van bestaande
bron-/voorraad-/regels- en UI-infrastructuur beperkt de uitvoeringstijd. Werkelijke
actieve uitvoering circa **30–35 minuten**, van 10:34 tot circa 11:05 Europe/Amsterdam.
De oorspronkelijke raming was te ruim voor de vaste template en bestaande infrastructuur.

Beperkingen: één kleur, één vaste template, geen andere formaten/decktypen, geen
strategie-/win-ratebewijs, geen castingkansen of volledige power/toughness-/spelsimulatie,
geen wildcardoptimalisatie, geen automatische Arena-bron, geen reservering voor andere
decks. Voorbereidingshistorie groeit zonder opruim-UI. Bronverval na 24 uur en regelverval
na veertien dagen blokkeren advies; ververs bron en gebruik bijgewerkte regels.
Geen extra pakket, secrets, externe AI-API of beveiligingsbeleidswijziging nodig.

Stop na fase 11; fase 12 is niet gestart en vereist een afzonderlijke startopdracht.
