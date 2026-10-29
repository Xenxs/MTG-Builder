# Verificatie fase 3 — 22 oktober 2026

Versie **0.3.0**. Fase 3 is expliciet gestart op 22 oktober 2026. Dit document
beschrijft uitgevoerde controles, geen veronderstelde resultaten. Fase 4 is niet gestart.

## Build en automatische tests

Uitgevoerd met .NET SDK 10.0.401 op Windows x64:

```powershell
./scripts/Verify.ps1 -SkipRestore -Screenshots -ReportPrefix phase3-delivery
```

De bestaande, reeds herstelde NuGet-afhankelijkheden zijn behouden; fase 3 voegt geen
nieuwe NuGet-packages of solutionprojecten toe. De definitieve Release-build is geslaagd
met **0 waarschuwingen en 0 fouten**. **93 tests geslaagd, 0 mislukt, 0 overgeslagen**:

| Project | Geslaagd | Bewijsbestand |
| --- | --- | --- |
| Domain.Tests | 19 | artifacts/test-results/phase3-delivery_net10.0_20261022091312.trx |
| Application.Tests, inclusief SQLite en UI | 74 | artifacts/test-results/phase3-delivery_net10.0_20261022091320.trx |

De tests zijn buiten de sandbox uitgevoerd vanwege testhostcommunicatie op Windows.
Dit is geen wijziging van Windows-beveiligingsbeleid. Buildlog: `artifacts/build.log`.
De GitHub Actions-workflow is bijgewerkt via het gedeelde verificatiescript, maar is
niet op een externe repository uitgevoerd. Er is hier nog geen Git-repository ingericht.

## Wat werkelijk is getest

- Domeinstatussen: een import niet tweemaal toepassen, een verworpen voorbereiding niet
  alsnog toepassen, herstel niet herhalen, ongeldige tellers afwijzen.
- Bezitsversie wijzigt bij iedere expliciete schrijfactie, ook bij hetzelfde aantal.
- Voorbereiden zonder bezitwijziging; na bronbestandverwijdering en providerherstart
  dezelfde voorbereiding toepassen. Historie en herstel overleven opnieuw openen.
- Absolute aantallen, expliciete nul, behoud van niet genoemde kaarten, decks en instellingen.
- Identieke herimport geeft nul wijzigingen en raakt bezitsversies niet aan.
- Onbekende regels, naamverschillen, meerdere varianten, gedeelde Arena-ID's,
  taalonderscheid, expliciete handmatige doelkeuze en overslaan.
- Dubbele doelen blokkeren, ook bij gelijke aantallen. Conflicten op latere pagina's
  blokkeren de hele import; resultaten en historie worden per 50 opgehaald.
- Verouderde keuzeversie, later gewijzigde aantallen (inclusief wijzigen en terugzetten),
  relevante cataloguswijzigingen en twee onafhankelijke serviceproviders op dezelfde
  database laten geen stille overschrijving toe.
- Herstel wijzigt alleen importwijzigingen en wordt volledig geblokkeerd bij latere
  betrokken schrijfacties. Nieuwe bezitsrijen verdwijnen; printings/deckregels blijven.
- Fouten én annulering na echte SQL-writes maar vóór commit rollen bezit en auditstatus
  volledig terug, zowel bij toepassen als bij herstel. Een proceskill is niet uitgevoerd.
- Een echt fase-2-schema wordt met gevulde collectie en deckregels naar fase 3 gemigreerd;
  de upgradeback-up is teruggelezen en de oorspronkelijke waarden zijn gecontroleerd.
- Export en herimport met catalogus- en handmatige printings, UTF-8-BOM, Unicode-doelzoeken.
- Verkeerde schema's/types/identiteiten, onbekende en dubbele velden, ontbrekende of
  ongeldige aantallen, afgekapt JSON, meer dan 50.000 regels en meer dan 16 MiB brondata.
- Export weigert een lege collectie en niet-JSON-doelen; annulering/fouten behouden
  een reeds bestaand exportdoel.
- Alle zes navigatiepagina's renderen. De nieuwe UI koppelt een bronregel via echte
  zoek-/selectiebindingen aan een bestaande handmatige printing, past aantallen toe
  en herstelt ze. Annuleren blijft actief en een fout laat de busy-status niet vastlopen.

De eerste ronde vond een daadwerkelijk defect: de export serialiseerde een weergaveveld
`label` dat de strikte importer terecht weigerde. Het veld wordt nu uitgesloten en
de export/herimport-tests slagen. De bestaande migratietest vergeleek nog met een vast
aantal twee migraties; deze vergelijkt nu alle bekende en toegepaste migraties.
Een eerste obsolete Avalonia-eigenschap is vervangen; de definitieve build heeft geen waarschuwing.
De native controle vond daarna nog een door pooling opengehouden back-upbestemming.
Die verbinding gebruikt nu Pooling=false; een aanvullende assert opent de gemaakte
back-up direct met FileShare.None. Na deze verbetering is de volledige bovenstaande
verificatie opnieuw uitgevoerd en geslaagd.

## Praktijkmeting met echte catalogus, gesimuleerd bezit

Dezelfde DI-registraties, database, parser en usecases als de desktopapp zijn gebruikt
in een afzonderlijke lokale smoke-database. De gebruikerscollectie is niet als testdata gebruikt.
De kaartbron is de werkelijk in fase 2 opgehaalde Scryfall-snapshot van 21 oktober 2026;
de bezitaantallen in deze proef zijn **gesimuleerd**, geen uitgelezen Arena-account.

| Controle | Resultaat |
| --- | --- |
| Catalogus | 119.106 bronrecords; 21.621 Arena-printings; 16.399 kaartdefinities |
| Collectiebestand | 2.000 unieke Scryfall-identiteiten; alle gekoppeld |
| Voorbereiding | 2.000 wijzigingen; circa 0,317 seconde |
| Toepassen | circa 0,279 seconde |
| Herstel | circa 0,066 seconde |
| Vóór | 9 exemplaren, 2 unieke bezitsprintings, 1 deck |
| Na toepassen | 4.011 exemplaren, 1.602 unieke bezitsprintings, 1 deck |
| Na herstel | weer 9 exemplaren, 2 unieke bezitsprintings, 1 deck |
| Export/herimport | 2.001 bezitsrijen inclusief nul-aantallen; 0 nieuwe wijzigingen |
| Deckregel | bleef 4 exemplaren |
| Piekgeheugen | circa 275 MiB, inclusief catalogusimport in hetzelfde proces |

Het bronbestand is na voorbereiding verwijderd en toepassing werkte vanuit de database.
Bewijs: `artifacts/phase3-collection-smoke.json`. Dit zijn lokale metingen, geen
algemene prestatiegaranties. De grens van 50.000 geldige regels is niet als volledige
prestatieproef gemeten; de limietafwijzing boven die grens is automatisch getest.
De diagnostische helpers onder `.local` horen niet bij de zes solutionprojecten.

## Native Windows-start en normale database-upgrade

De definitieve executable is gestart op **22 oktober 2026 om circa 09:14 Europe/Amsterdam**:

- PID 17684; venstertitel `MTG Arena Smart Deckbuilder`; `Responding = true`.
- ProductVersion 0.3.0; normale gebruikersdatamap, geen testoverride.
- Desktop-DLL SHA-256: `6D2EA4D35FBB8DC3BA360037DA94BD5A00771BF1820B2F1E59891782FCE2CD86`.
- Applicatielog bevestigt start, automatische upgradeback-up en voltooide initialisatie.
- Migratie `20261022065339_AddCollectionTransfer` is toegevoegd aan de bestaande twee.
- De eerste native start om circa 09:09 (PID 11864) heeft de upgrade toegepast. Deze
  instantie is normaal afgesloten voor de back-upverbetering en definitieve rebuild.
- Vóór de upgrade is `pre-migration-20261022-070902-556-8318826914f3471fb4c2fd616985eacb.db`
  gemaakt in de normale back-upmap.
- De reservekopie is 33.325.056 bytes; SHA-256 staat in artifacts/phase3-backup.json.

Leescontroles vóór/na vergelijken aantallen en SHA-256 van bezit, decks, deckinhoud en
instellingen; alle komen overeen. De echte gebruikersdatabase had in deze vier tabellen
op dat moment nul rijen. Behoud van **gevulde** data is dus bewezen in de geïsoleerde
migratie-/importtests en smoke-proef, niet door te doen alsof de gebruiker al bezit had.
Er is geen collectie-import toegepast op de normale gebruikersdatabase.

Bewijs: `artifacts/phase3-native-start.json`, `phase3-user-before.json`,
`phase3-user-after.json`, `phase3-user-preservation.json`, `phase3-backup.json`,
`phase3-upgrade-native-start.json` en de applicatielogs.
Er is in fase 3 geen Windows-beveiligingsbeleid gewijzigd en geen certificaat gebruikt.

## Visuele controle en grenzen

De zes pagina's zijn gerenderd. De gewijzigde dashboard-/instellingenschermen en de
collectievoorbereiding/herstelweergave zijn daadwerkelijk visueel bekeken:
`artifacts/ui/dashboard.png`, `settings.png`, `transfer.png`, `transfer-preview.png`
en `transfer-history.png`. De schermafbeeldingen gebruiken testdata; native appstart
bewijst aanvullend dat de echte Windows-executable draait. Geen volledige handmatige
muis-, bestandspicker-, DPI- of toegankelijkheidsmatrix op verschillende computers.

Er is nog geen gevalideerde Arena-logadapter, officiële collectie-exportadapter, CSV,
accountkoppeling, live synchronisatie, samenvoegen van twee printings, legaliteitscontrole,
wildcardberekening of deckadvies. Handmatige doelkeuze ondersteunt bestaande printings,
maar verplaatst geen deckregels. Voorbereidingen en auditregels blijven bewaard; er is
nog geen opruimfunctie. Herstel is conservatief en heeft geen geforceerde overschrijvingsoptie.
Limieten en precieze betekenis staan in COLLECTION_IMPORT.md.
