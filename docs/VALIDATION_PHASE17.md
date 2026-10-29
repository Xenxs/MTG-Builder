# Validatie fase 17 — v1.4.0

28 oktober 2026. Opdracht: alle huidige kaarten als aangenomen bezit gebruiken om de
engine opnieuw te onderzoeken en waar nodig te verbeteren. Advies vooraf GPT-6.1 Sol /
High, 4–8 actieve uren voor bronselectie, nulmeting, inhoudelijke beoordeling,
implementatie, tests, opslag-/Windowscontrole en documentatie. Geen ondersteunde
modelinstellingenbediening gevonden; niets gewijzigd. Criteria vóór Oracle-inspectie:
SYNERGY_ACCEPTANCE_PHASE17.md. Geen onafhankelijke beoordelaar of echte collectie.

## Bron en afbakening

Default Cards-descriptor: updated_at 2026-10-28T09:05:49Z. Werkelijk gedownload JSONL.gz:
80.216.061 bytes; descriptor en downloadhash in artifacts/phase17. Oracle Cards-bulk
was bewust niet de eindbron: de gekozen herkenbare printing kan digitaal zijn.

119.509 bronregels zijn gededupliceerd tot 33.727 fysieke, tot en met 28 oktober
uitgebrachte Oracle-identiteiten. Selectie prefereert Engels, gewone border en stabiele
printingkeuze. 68.683 dubbele printings, 9.174 niet-fysiek, 847 nog niet uitgebracht,
83 zonder identiteit en 6.995 niet-libraryobjecten/meldresultaten vielen af. Meldhelften
blijven behouden; het eerste extract met ten onrechte alle meldkaarten uitgesloten
werd vóór tekstinspectie vervangen. Acorn/silver/playtest mogen bezit zijn, maar zijn
geen automatisch ondersteunde spelregels. De papieren MB2-playtest Rusko is geen
digitale kaart alleen omdat de tekst ongebruikelijk is.

Canonieke invoer: `.local/phase17-catalog/paper-oracles-v2.jsonl`, SHA-256
`0A6290082E24142DD5D59A9457C64C7D0182DB36CDB92FDE355D8C83901BD262`.
Manifest: artifacts/phase17/catalog-manifest.json. Creaturetypen-vocabulaire: 370
termen uit de actuele Scryfall-catalogus; embedded met bronbewijs. Officiële regels
van 25 september 2026 geraadpleegd via de Wizards-regelpagina: onder meer tokenbezit
(111.2), cycling/typecycling (702.29), levelers (711) en afwijkende kaarten (100.7).

Alleen de afzonderlijke `.local/phase17-all-cards` is gevuld: 33.727 printings,
138.106 gesimuleerde exemplaren, vier per niet-basic en 250 per basic (13 basics).
Holdings zijn IsDemo=true, map/spelersnaam/marker benoemen SIMULATIE. Automatische
productvulling is daar uitgeschakeld. Database bij seed circa 196 MiB. Geen echte
gebruikersdatabase gebruikt voor import-/opslagproeven.

## Nulmeting en schaalbaarheid

Ongewijzigde v1.3.0-binaries (engine v4) en bronhashes bewaard in .local/phase17-baseline
en artifacts/phase17/baseline-code-hashes.json. Geen onbeperkte 33k×33k-baselineproef.
De afzonderlijke 10.000-proef werd na 60,07 seconden gestopt; waargenomen piek circa
676 MiB, nog geen voltooid resultaat. De 60s/1GiB-limiet is werkelijk gehandhaafd.

| Invoer / versie | Constructor | Eerste keuze extra | Piek |
| --- | ---: | ---: | ---: |
| 5.000 / v1.3.0 | 43,54 s | 5,18 s | 707 MiB |
| 5.000 / v1.4.0 | 7,25 s | 1,13 s | 155 MiB |
| Alle 31.037 geschikte niet-landen / v1.4.0 | 30,76 s | 6,74 s | 308 MiB |

Hele laatste selectorproces inclusief lezen circa 40 s, 5.924 semantische groepen.
5.000-invoer gebruikt dezelfde vaste SHA-volgorde/filterprocedure; inhoudelijke guards
zijn intussen bewust uitgebreid. Het is geen geïsoleerde causaliteitsmeting van alleen
de index. Bij de eerste indexwijziging waren 8.640 scorevergelijkingen op 80 bevroren
kandidaten exact gelijk aan baseline (maxdelta 0); latere semantische/selectiewijzigingen
zijn bedoeld en vallen niet onder die gelijkheidsclaim. Bewijs: index-equivalence.json,
final-baseline-sequential en final-v7-sequential. Eenmalige tijden op deze pc, geen SLA.

Feiten worden begrensd gecachet op gameplayvingerafdruk en afwijkende-layoutflag;
printing/freshness blijven van de aanroeper. De nieuwe index bewaart alleen lazy
numerieke relaties per semantische groep en actuele context. Gedetailleerde relaties
worden met echte kaarten berekend. Key en bovengrens moeten bij iedere nieuwe relationele
predicate worden aangepast; metamorfische tests vergelijken de echte relaties in alle
thema's. Geen eerste-N-truncatie van de bezitpool.

## Vooraf gereserveerde steekproeven

A en B hebben ieder 62 kaarten: 48 SHA-geordende algemene gevallen plus 14 uit de
layout-/lengtestrata; niet alle acht beoogde strata waren gevuld. Vooraf was maximaal
64 beoogd. Geen overlap met de 87 productkaarten, drie eerdere sets of tussen A/B.
Selectief en met opzettelijk veel complexe kaarten; niet representatief. Reserveringen,
bron-JSON, voorspellingen en bevroren codehashes blijven bewaard. Waarheidslabels zijn
na tekstinspectie toegekend door dezelfde ontwikkelaar. Acht vooraf benoemde themafamilies;
labels zijn geen volledige regels- of deckkwaliteitsmeting.

| Eerste beoordeling | Waarheidslabels | Baseline herkend | Tussenversie herkend | Onterecht | Gemist tussenversie |
| --- | ---: | ---: | ---: | ---: | ---: |
| A / engine v5 | 52 | 15 | 15 | 0 | 37 |
| B / engine v6, vóór B-tekstinspectie | 57 | 19 | 22 | 0 | 35 |

A leverde aanvankelijk dus geen themametriekwinst op. Na inspectie werd A expliciet
ontwikkelmateriaal. B was vóór de volgende iteratie gereserveerd en de v6-voorspelling
is vóór B-inspectie vastgelegd. Bij B vallen 13 relevante labels onder 17 bewust
afgewezen complexe/afwijkende kaarten; daarnaast worden 22 labels op overige kaarten
gemist. Nul positieve fouten hier bewijst geen brede precisie.

Eerste B-annotatie gaf Bloodlord of Vaasgoth ten onrechte een tokenrol. Zijn payoff
vraagt een daadwerkelijk gecaste Vampire-spell; een gemaakt token triggert dit niet.
Dit waarheidslabel is gecorrigeerd zonder parseraanpassing; oorspronkelijke waarheid
en 58-labelmeting zijn bewaard als truth-b-initial-review.json en eerste evaluatie.
De finale eerste meting gebruikt 57 labels. Dino DNA's creature-identiteitshergebruik
is als graveyardrol geannoteerd; dat is een expliciete inhoudelijke afbakening.
Garland's controlled-but-not-owned-voorwaarde is geen gewone eigen-tokenpayoff.

Laatste engine v7 is na A/B-inspectie verder veranderd voor Commanderbouwplannen en
graveyardbalans. Eindregressie: A 20/52, nul onterecht, 32 gemist; B 22/57, nul onterecht,
35 gemist. Dit zijn ontwikkelregressies, geen nieuw ongezien bewijs. Alle fouten blijven
in de evaluatiebestanden staan; er is niet voor ieder gemist label een regel toegevoegd.

## Volledige dekking is geen volledige kennis

v1.3.0: 87 gecontroleerde productprofielen, 12.743 beperkte aanwijzingen en 20.897
type-only/onbekend; 903 automatisch uitgesloten. v1.4.0: 87 gecontroleerd, 13.716 beperkt,
19.924 type-only/onbekend; 1.531 automatisch uitgesloten. Formaatmetadata: 5.164 Standard-
legale en 32.110 Commander-legale kaarten. Dit zijn afzonderlijke tellingen; niet optellen
als extra kaarten. Alle 33.727 geanalyseerd in 40,32 s, piek circa 149 MiB.

Veel onbekende werking blijft: counters/proliferate, veel tribal/combo, variabele
voorwaarden, Saga, complexe wijzen/kanten en negatieve effecten. Onbekend is geen
slechte kaart; automatisch weglaten van complexe kaarten beperkt de generator bewust.

## Complete decks en inhoudelijke herproef

Productieroute IPaperWorkspace met echte EF/SQLite in de all-cards-database, bezitmodus,
geen netwerk toegestaan. 64 aanvragen: Casual/Standard × negen thema's × drie stijlen,
plus Krenko, Giada, Talrand, Meren, Cloud, Sephiroth, Edgar, Atraxa, Kenrith en Kozilek.
Dit omvat mono-, twee-, drie-, vier-, vijfkleurig en kleurloos Commander.

Engine v5 én v6: 61/64 geldig, opgeslagen, nogmaals idempotent opgeslagen, opnieuw
geopend via een nieuwe serviceprovider. Drie Standard/Exalted-combinaties geblokkeerd
omdat er nul beschikbare ondersteunde themakaarten zijn. Exact 60/100, eigen voorraad,
Oracle-kopielimiet, singleton, kleuridentiteit, formaat/freshness, geen verboden shapes
gecontroleerd. Bij iedere run exact dezelfde 138.106 exemplaren. Netwerkverzoeken: nul.
v6 hele meetprogramma 577 s, piek 1.913.585.664 bytes inclusief bewaarde rapporten en
JSON-serialisatie. Dit hoge totaal niet verwarren met 308 MiB van de selectorproef.

Handmatige inspectie vond ondanks structureel geslaagde decks slechte commanderfocus:
Giada één andere Angel, Talrand twee instants/sorceries, Krenko dertien andere Goblins;
Meren 56 setupkaarten. v7 geeft respectievelijk 47 Angels, 46 instants/sorceries,
43 Goblins en 15 setupkaarten (40 hergebruikrollen tegenover 12). Metingen tellen
gedrukte typen/ondersteunde rollen; overlapping mogelijk. Commander zelf niet bij
Angel/Goblin. Dit zijn gebruikte ontwikkelgevallen, geen onafhankelijke kwaliteitswinst.

Alle 16 betrokken aanvragen opnieuw uitgevoerd met v7: zes graveyard + tien Commander,
allemaal geldig/opgeslagen/idempotent/heropend. Totale run 210 s, piek 2.007.601.152 bytes
inclusief rapporten, nul netwerk, voorraad gelijk. Nu 138 opgeslagen proefdecks over de
drie runs; namen kunnen herhaald zijn. Alleen nieuwe voorstellen worden anders gebouwd;
bestaande decks worden niet herschreven. Bewijs: full-practice-v5/v6/v7-focused.json,
compacte samenvattingen en compositietellingen in artifacts/phase17.

## Build, interface en bestaande data

`./scripts/Verify.ps1 -Screenshots` werkelijk uitgevoerd: restore/build geslaagd,
**nul waarschuwingen/fouten, 583 tests geslaagd** (36 Domain, 547 Application), nul
overgeslagen. De extra all-cards-interfaceproef gebruikte een SQLite-back-up van de
gemarkeerde testdatabase, geen echte gebruikersdatabase. Zonder die expliciete fixture
meldt deze test een skip; reguliere ontwikkelaars moeten dat als onuitgevoerd lezen.
33.727 kaarten zichtbaar, graveyardfilter op achtergrond, dispatcher reageert,
opgeslagen Giada opnieuw geladen. PNG's onder artifacts/ui/phase17 bekeken. Overige
tests omvatten bronactualiteit (nul calls bij verse cache, één bij oud/toekomstig),
cache-identiteit, targets/subjecten, negatie, typed sacrifice, levelers, nontoken,
Commanderfocus en indexrelaties/bovengrenzen. Aantallen tests zijn geen accuratesse.

Native bron-v1.4.0 werkelijk gestart en normaal afgesloten op gewone én gevulde
bestaande gegevensmap: respons/logs gecontroleerd. Vóór/na alle kolommen en originele
rijen exact behouden (38.372 respectievelijk 56.824). Twintig tabellen en negen
migraties gelijk; geen nieuw schema of NuGet-pakketten. Bewijs user-/filled-before.db,
after-source-audits en artifacts/phase17-source-*-native.json.

## Distributie

Op dit moment zijn de bron- en data-/testcontroles hierboven voltooid. Definitieve
installer/portable worden hierna gebouwd. Pakketcontrole, authentieke v1.3.0→v1.4.0-
upgrade, uninstall/herinstallatie, gebundelde runtime, hashes en uiteindelijke native
start worden uitsluitend na uitvoering toegevoegd. Ingesloten validatie is een
momentopname; eindbewijs staat naast de release en in dit bronverslag.

Geen signingcertificaat, schone tweede pc, echte scan, partij-/winrateproef of algemene
regelscorrectheid claimen. Geen beveiligingsbeleid aangepast. Geen volgende fase starten.

## Definitief pakketbewijs — na bouwen toegevoegd

De definitieve v1.4.0-installer en self-contained win-x64 portable zijn werkelijk
gebouwd. Alle **269 ZIP-bestanden** komen inhoudelijk overeen met de portable map;
manifest-/ZIP-/installer-SHA-256 gecontroleerd, geen gebruikersdatabase of log in het
pakket. Installer en programma zijn NotSigned. Geen runtime uit de machine-installatie
gebruikt: geladen coreclr.dll komt telkens uit de eigen installatiemap.

Authentieke v1.3.0-release geïnstalleerd op geïsoleerd pad, werkelijk naar v1.4.0
geüpgraded, verwijderd, hergeïnstalleerd en voor de laatste keer verwijderd. Tijdens
iedere start respons en logs gecontroleerd, normaal afgesloten. Startmenu-doel correct;
laatste uninstall verwijdert programma/shortcut en behoudt data. Alle twintig tabellen
exact gelijk, schema negen, nul nieuwe migratieback-ups. Bewijs:
artifacts/phase17-install-installation.json en de bijbehorende audit-/installatielogs.

Definitieve portable bovendien met beide bestaande gegevensmappen gestart en normaal
afgesloten. Opnieuw alle kolommen/rijen en twintig tabelhashes exact gelijk, 38.372 en
56.824 originele rijen, negen migraties. Bewijs: artifacts/phase17/retention-summary.json,
user-/filled-after-package.json en phase17-package-*-native.json.

De aparte alle-kaartenlauncher werkelijk uitgevoerd: v1.4.0 draait zichtbaar op de
gesimuleerde map, reageert en laadt eigen gebundelde runtime. Native startpiek circa
779 MiB, circa 690 MiB werkgeheugen na laden; dit is één startmeting, geen bovengrens
bij genereren. Proces/log-/padbewijs: artifacts/phase17/all-cards-native.json.
Normale gebruikersdata blijven apart. De finale UI-screenshotpositie is nog met één
gerichte geslaagde all-cards-test hercontroleerd; geen runtimecode na de 583-testsuite gewijzigd.

Naast de release staat VALIDATIE-NA-PAKKET.md; de ingesloten validatie is de eerlijke
momentopname vóór deze pakketproeven. Het pakket zelf is daarna niet aangepast.
Werkvenster circa 14:08–17:25 Europe/Amsterdam, ongeveer 3¼ actieve uren. De raming
4–8 uur is ruim gebleken doordat bestaande opslag-/installatiecontroles herbruikbaar
waren. Geen gebruikersacceptatie of volgende fase claimen; fase 17 stopt voor beoordeling.
