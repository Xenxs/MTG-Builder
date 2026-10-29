# Controleverslag fase 9

**22 oktober 2026**, Windows x64, .NET SDK 10.0.401, **v0.9.0**. Fase 8 is geaccepteerd
en fase 9 gestart met "Fase 8 goed start 9". Scope: vaste gerichte schade en expliciete
modale aanwijzingen, zonder sterke schade-/modale rolmatch. Gebruik en grenzen:
[DAMAGE_AND_MODAL_ROLES.md](DAMAGE_AND_MODAL_ROLES.md).

## Definitieve build en tests

Werkelijk uitgevoerde opdracht:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase9-delivery
```

Restore geslaagd, Release-build **0 waarschuwingen, 0 fouten**.
**278 tests geslaagd**, 0 mislukt/overgeslagen:

| Suite | Geslaagd | TRX onder artifacts/test-results |
| --- | ---: | --- |
| Domain | 28 | phase9-delivery_net10.0_20261022131702.trx |
| Application/SQLite-integratie/Avalonia UI | 250 | phase9-delivery_net10.0_20261022131728.trx |

`artifacts/build.log` bevat het buildresultaat. Nieuwe tests controleren vaste
hoeveelheid/doelen, voorwaarden, letterlijke kaartnamen, onbekende variabele/massale/
speler-schade, behoud van extra kosten, aparte modale opties, onbekende opties,
JSON-roundtrip en dat schade/modaal nooit Matching wordt. Gebonden UI-tests kiezen
kandidaten, maken een voorbeeld, controleren de opgeslagen JSON en bewaren als nieuw
deck. Bestaande voorraad-, versie-, conflict-, rollback- en actualiteitstests blijven slagen.
Alle automatische databasetests gebruiken wegwerpdatabases, geen gebruikersdata.

De eerste ontwikkelrun slaagde met 277 tests. De evaluatierun vond daarna één race
in de bestaande DeckValidationUiTests: een directe RefreshAsync buiten UiOperations
liet een asynchrone bindingselectie lopen terwijl de test validatie vroeg. Die opdracht
werd wegens IsBusy overgeslagen. De test gebruikt nu dezelfde NavigateCommand als de
interface, zodat vernieuwen binnen de UI-bewerkingsgrens afloopt. Geen parserwijziging,
productie-workaround, retry of extra wachttijd. De definitieve volledige suite slaagt.

## Gereserveerde evaluatie en bevriezing

Bron: de werkelijk eerder geïmporteerde Scryfall-snapshot
**2026-10-21T21:06:00.423Z**, met 5.164 Standard-legale kaartdefinities. Geen nieuwe
bulkdownload in fase 9. De v0.8-classifier is vóór implementatie op dezelfde bron
uitgevoerd en per kaart vastgelegd; resultaat 43 volledig, 207 onvolledig, 4.914 onbekend.

`artifacts/phase9-evaluation-selection.json` reserveert op **11:07:55 UTC** dertig kaarten.
Tien laagste SHA-256-waarden van `phase9-eval-v1/OracleId` per trefwoordstratum:
Damage (`deals? .*damage`), anders Modal (regel begint met Choose one/two/three/
one or more/two or more), anders Other. Damage krijgt bij overlap voorrang. Oude
referentie- en fase-8-evaluatienamen en Consult the Star Charts, Dazzling Denial en
Desculpting Blast zijn uitgesloten. Trefwoorden definiëren strata, niet de juiste rol.
Deze selectie is niet representatief voor alle kaarten of decks.

Parser, vergelijking en rangschikking zijn op **11:12:54 UTC** bevroren vóór het
bekijken van de nieuwe gereserveerde teksten. Eindcontrole bevestigt dezelfde hashes:

| Bestand onder src/MTGDeckbuilder.Application | SHA-256 |
| --- | --- |
| CardRoles.cs | B3FD76043CA1F78E2F2A5F3520874264EFF5BFA077B571DAEFF0121B5CF9262F |
| RoleTextInterpreter.cs | 7477AF26C6AEF3ACCC7C482D47A6D0A24B8B2A2E1BFDF666DD7EEEBDE1CDF96C |
| ModalRoleInterpreter.cs | 122F8C52908FA9428FEAB9441D821A999006182D3232E6D0DD422F566981672D |
| DeckAdviceEngine.cs | D106145D340FF6EB2913C6A7B538E519DC8A5A4282EE3B93C3F29E1F10E12D77 |

Dezelfde ontwikkelaar labelde na bevriezing de verwachte families en voorwaarden,
vóór baseline- of huidige voorspellingen per kaart werden bekeken. Geen onafhankelijke
reviewer. `examples/role-evaluation-v2.json` is op **11:13:55 UTC** gelabeld en daarna
inhoudelijk én bytegewijs gelijk gebleven. Vastgelegde SHA-256:
`72D25BE6256DDE8D4DA94BD25E6860F4FDD73839B6157E431517C5C1016CBD5D`.
De oude voorspellingen op deze dertig kaarten staan afzonderlijk in
`examples/role-evaluation-v2-baseline.json`; de parser is niet op de uitkomst aangepast.

Definitief `artifacts/phase9-reserved-evaluation.json`:

| Maat | v0.8-baseline | v0.9 |
| --- | ---: | ---: |
| Volledig herkend | 0 | 1 |
| Onvolledige aanwijzing | 3 | 7 |
| Onbekend | 27 | 22 |
| Geheel gemiste relevante kaarten | 18 van 21 | 13 van 21 |
| Enige relevante familie herkend | 14,3% | 38,1% |
| Verkeerde families vastgesteld | 0 | 0 |

Er zijn 21 positieve en 9 negatieve gevallen. De telling is per kaart met minstens
één herkende familie, geen volledige meerlabel-recall. Alle vereiste voorwaarden
blijven aanwezig in de uitleg bij gevonden gevallen. Eén volledige voorspelling en
zeven aanwijzingen zijn te weinig voor een betrouwbare algemene precisieclaim.
Dertien relevante kaarten blijven gemist. Dit is een beperkte verbetering op een
kleine gestratificeerde set, geen bewijs van brede kaartkennis of betere winkans.

De oude fase-8-set is nu regressiemateriaal: `phase9-previous-evaluation-regression.json`
geeft 0 volledig, 9 onvolledig, 21 onbekend en geen verkeerde families. Geen nieuwe
ongeziene meting. `phase9-role-development-evaluation.json` geeft voor de huidige
ontwikkelset 26 volledig en 4 onvolledig; de bestaande tien rangschikkingsgevallen
blijven 10/10, tegenover fase-6-baseline 3/10 (MRR 1,000 tegenover 0,583). De historische
veldnamen Phase7Top1/Phase7Mrr duiden de huidige rolrangschikking aan.

## Werkelijke catalogus en opslagproef

`artifacts/phase9-real-catalog-final.json`, gemaakt op **11:16:01 UTC**, gebruikt
de definitieve Infrastructure-assembly en een nieuwe geïsoleerde database onder
`.local/phase9-smoke-data-final`. Bronkopie: de eerdere gevulde fase-8-proef, geen
gebruikersdatabase. 21.621 catalogusprintings; 2.003 gesimuleerde bezitsregels bij de
Negate-proef. Legaliteit is die uit de opgeslagen snapshot, geen nieuwe online
kaartcontrole. Van 5.164 Standard-legale kaartdefinities:

| Maat | Aantal |
| --- | ---: |
| Volledig herkend | **52** |
| Onvolledige aanwijzing | **300** |
| Onbekend | **4.812** |
| Modale kaarten binnen aanwijzingen | 50 |
| Volledig herkende vaste schade | 9 |
| Niet-modale schade-aanwijzingen | 45 |

Volledig per familie: 11 counter, 25 removal, 11 draw, 5 mana. Onvolledige families:
155 removal, 28 counter, 78 draw, 42 mana; deze overlappen door modale opties en
mogen niet tot het kaarttotaal worden opgeteld. De 52 volledige kaarten zijn circa
1,0% van de set. Dit meet patroonuitkomsten, geen juistheid van ieder gevonden geval.

De echte Negate-proef vindt 64 kandidaten in **0,248 seconde**; voorbeeld 0,118 seconde,
opslag 0,112 seconde, piekwerkgeheugen circa 142,4 MiB. Reprint plus gewaarschuwde
Essence Scatter zijn als nieuw deck opgeslagen, zonder wijziging van origineel/bezit;
herhalen is idempotent. Drie oudere toegepaste ontvangsten geven hetzelfde resultaat.

Een aanvullende echte **Shock → Lightning Strike/Abrade**-proef vindt 59 kandidaten.
Lightning Strike blijft Limited ondanks meer schade; Abrade is een onvolledige
modale aanwijzing met aparte damage- en artifact-destroyopties. Beide keuzes zijn
gezamenlijk als nieuw deck opgeslagen, met nul restanttekort. Origineel/bezit gelijk;
herhaald toepassen geeft hetzelfde deck. De technische decks bestaan uit 60 Forest
en twee bronkaarten; ze zijn geen competitieve plannen of castbaarheidsbewijs.
Alle nieuwe writes gebeuren uitsluitend in deze geïsoleerde proef.

## Beelden en native start

De vier definitieve gerenderde afbeeldingen zijn daadwerkelijk bekeken:
`artifacts/ui/deck-roles-damage.png`, `deck-roles-damage-preview.png`,
`deck-roles-modal.png` en `deck-roles-modal-preview.png`. Kandidaten tonen hoeveelheid,
onbekende dodelijkheid respectievelijk aparte opties en geen sterke match. Het
gezamenlijke voorbeeld herhaalt Limited/onvolledig; volledige uitleg is uitklapbaar.
Dit is Avalonia-headlessrenderbewijs, geen volledige handmatige native bediening.

Daarnaast is de echte Windows-executable gestart:

| Database | Proces bij controle | Resultaat |
| --- | ---: | --- |
| Bestaande gevulde fase-8-proef | 26512 | Reagerend venster, v0.9.0 en migratie-initialisatie gelogd; normaal gesloten |
| Normale gebruikersdatabase | 33324 | Reagerend venster, v0.9.0 en migratie-initialisatie gelogd; open gelaten |

Definitieve normale start: **13:18:13 Europe/Amsterdam**, initialisatie bevestigd om
13:18:15. `phase9-synthetic-native-start.json` en `phase9-user-native-start.json`
bewaren nieuwe logregels en hashes. Geen WRN/ERR/FTL in die startregels.

| Assembly | SHA-256 |
| --- | --- |
| Desktop | 0236A104B70C323927C8E1F734B34B08161461DC559A8B16BD1E2EB1732330D4 |
| Infrastructure | F996EC316A00C571F1D62B7BE182709E216BE08C23A3C5C50CA2DE5FF8526757 |

De Infrastructure-hash is gelijk aan de catalogusproef. De laatste proces-/hashcontrole
staat in `artifacts/phase9-final-process-check.json`.

## Gegevensbehoud

Vóór en na de native starts zijn 13 bestaande tabellen alleen-lezen gehasht, inclusief
collectie, decks, deckregels, instellingen, importhistorie/-regels, adviesplannen,
kaarten, legaliteiten, printings en catalogusimports. **Alle 13 hashes en rijtellingen
zijn per database gelijk**. De normale database bevat geen bezit/decks; daarom ook
de bestaande gevulde proef: **2.003 bezitsregels, 8 decks, 21 deckregels en 3 ontvangsten**
behouden. Beide databases behouden zes migraties, laatste
`20261022090051_AddDeckAdvicePlans`. Back-upbestandslijsten vóór/na zijn gelijk.

Bewijs onder artifacts: `phase9-user-before/after.json`,
`phase9-synthetic-before/after.json`, `phase9-data-preservation.json` en
`phase9-backups-before/after.json`. Geen schemawijziging, nieuwe upgradeback-up of
gebruikersdatabase als testfixture. De bestaande JSON-envelop draagt nu ook opties.
Oude ongebruikte voorbeelden vereisen herberekenen; toegepaste ontvangsten blijven idempotent.

## Oplevergrens

Geen nieuwe NuGet-pakketten, externe AI-API, secrets of beveiligingswijzigingen.
Geen volledige spelregels, synergie, winkans, automatische deckgeneratie of Arena-sync.
Modeladvies: [GPT-6.1 Sol](https://developers.openai.com/api/docs/models/gpt-6.1-sol) /
High; geen ondersteunde directe chatbediening beschikbaar, niets gewijzigd.
Raming was 45–90 actieve minuten, met onzekerheid bij complexe tekstvarianten.
Werkelijke uitvoertijd staat in PROJECT_PLAN.md. Fase 9 wacht op beoordeling;
fase 10 is niet gestart.

Nieuwe bronbestanden: ModalRoleInterpreter.cs, DamageAndModalRoleTests.cs,
PhaseNineEvaluationTests.cs; nieuwe voorbeelden: role-reference-v3.json,
role-evaluation-v2.json en de afzonderlijke baseline; nieuwe handleiding en dit verslag.
Verder zijn rolmodel/interpreter/engine, UI-uitleg, versie, tests en projectdocumentatie
bijgewerkt. Compleet overzicht: [FILES.md](FILES.md). Tijdelijke helpers en lokale
meetbestanden blijven buiten de solution onder genegeerde `.local`/`artifacts`.
Geen Git-repository/remote, commit, PR of uitgevoerde CI-run geclaimd.
