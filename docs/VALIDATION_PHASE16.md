# Validatie fase 16 — v1.3.0

28 oktober 2026. Opdracht: de aanwezige testkaarten inhoudelijk gebruiken om de
synergie-engine te onderzoeken en verbeteren. Advies vóór start GPT-6.1 Sol / High,
raming 4–8 actieve uren inclusief onderzoek, implementatie, evaluatie, tests en
documentatie. Geen ondersteunde modelbediening gevonden; niets gewijzigd.

## Bevinding en gemaakte wijziging

De v1.2-logica gebruikte woordtreffers en grotendeels statische rangschikking. Een
structureel geldig deck was daardoor onvoldoende bewijs van samenwerking. De
ongewijzigde baseline is bewaard in artifacts/phase16/baseline-v1.2.json en de
bijbehorende codevingerafdruk. Die baseline gaf al 24/24 structureel geldige
thema-/stijlvoorstellen: dit wordt niet achteraf als een kwaliteitswinst gepresenteerd.

Nu zijn feiten, eigen/vijandelijke acties, producer/payoff, relaties, voorwaarden,
conflicten en onbekende werking gescheiden. 87 Oracle-productkaarten hebben handmatig
beoordeelde profielen voor ondersteunde interacties met exacte gameplayvingerafdruk.
Dat dekt 109 uitgaven / 202 aangenomen exemplaren, geen volledige kennis van alle
effecten op die kaarten. Algemene tekstherkenning blijft beperkt en conservatief.
Relaties veranderen de keuze na iedere pick; kopieën/basiscompatibiliteit worden
begrensd. Castingkleuren en manaoutput zijn apart; Domain telt echte basislandtypen.
Deck/wizard tonen de relaties en voorwaarden. Zie SYNERGY_ENGINE.md en ADR-062–066.

Vooraf vastgelegd: docs/SYNERGY_ACCEPTANCE_PHASE16.md. Geen onafhankelijke reviewer;
dezelfde ontwikkelaar heeft geïmplementeerd, geannoteerd en geëvalueerd.

## Productreferenties en tegengevallen

Onder meer Cloud/Equipment, Adelbert/Equipmenttelling, Beatrix/aanhechten,
Barret/uitgeruste creatures, Job select, shroud/protection, eigen/vijandelijke
sterftriggers, artifact versus creature, tokens versus echte kaarten, Xande,
cycling, manaoutput versus kosten, Domain/landtypen, echte groene/rode permanents,
Paleoloth/power, exalted/soloaanval, modaliteit, gerichte zoeknamen en conflicterende
graveyard-exile zijn afzonderlijk gecontroleerd. Voorwaarden worden niet als reeds
gerealiseerde speltoestand geteld. Tekstwijzigingen verliezen gecontroleerd vertrouwen.

Belangrijke correcties:

- Gwafa's draw voor de tegenstander is geen eigen draw-engine.
- Sephiroth vraagt vijandelijke creature-dood, geen eigen sacrifice.
- Kaleidostone levert mana, geen basislandtypen; gewone Forest/Mountain zijn normaal
  geen groene/rode permanents voor Rhox Meditant/Ember Weaver.
- Creatuurtokens zijn geen blijvende creaturekaarten voor recursion/Xande.
- Deft Duelist is geen equip-target, maar kan wel een exalted-bonus krijgen.
- De huidige productcollectie bevat geen vastgestelde vrij herhaalbare creature-outlet;
  artifact-/Treasure-routes worden als beperkter beschreven.
- De live-planning vond Abyssal Persecutor en Archfiend of the Dross zonder beoordeelde
  uitweg. Herkende eigen verlies-/winblokkades zijn daarom uitgesloten van automatisch
  kiezen. Dit is geen volledige detectie van ieder kaartnadeel.

## Aparte evaluaties — fouten en grenzen

Iedere set: 21 kaarten, drie per zeven vooraf gekozen themafamilies. Selectie: eerste
naamgesorteerde Scryfall-pagina, daaruit lexicografische Oracle-ID-volgorde, met uitsluiting
van de productpool en eerder gebruikte kaarten. Dit is selectief, niet representatief.
Nieuw thema opponent-death telt niet mee in deze vooraf gekozen zeven-labelmeting.
Een positief token-thema kan ook een team-payoff betekenen, niet alleen tokenproductie.

Per eerste proef zijn reservering, bron-JSON, codehash en voorspellingen vóór Oracle-
inspectie bewaard. De gouden labels kwamen pas bij inspectie, door dezelfde ontwikkelaar.
Themalabels meten geen complete relaties, strategische bruikbaarheid of regelscorrectheid.

| Eerste proef | Relevante labels | Herkend | Onterecht toegekend | Gemist |
| --- | ---: | ---: | ---: | ---: |
| Set 1 / engine v1 | 26 | 9 | 0 | 17 |
| Set 2 / engine v2 | 28 | 21 | 0 | 7 |
| Set 3 / engine v3 | 23 | 14 | 0 | 9 |

Set 1 leidde tot scheiding van kosten, trigger en effectbegunstigde. Daarna is deze
set ontwikkelmateriaal, geen ongeziene benchmark. Set 2 vond bovenop de labelmissers
twee onveilige samengevoegde layouts: Adventure en split. Die worden nu niet automatisch
gekozen; effecten/kosten van kanten worden niet samengevoegd. Eigen enchantment-/onbekende
tokens werden bovendien onderscheiden van creature-/artifacttokens. Sequence-komma's
werden niet langer zomaar triggers. Een regressie bij Domain als ability word is met
een specifieke voorwaardetest hersteld; search → put blijft een gewone effectreeks.

Set 3 bleef inhoudelijk ongebruikt voor verbeteringen aan themadekking. Missers:
Artifist Acumen (eerste strike-teambonus), Arwen's Gift (scry gevolgd door draw),
Argivian Find (beperkte artifact/enchantment-recursion), Anowon (symmetrische sacrifice),
Balduvian Atrocity (beperkte recursion plus uitgestelde sacrifice), Arcum (controller-
afhankelijke sacrifice), Ajani Resolute (geneste token/abilitytekst) en Ajani Fells the
Godsire (Saga-hoofdstuk). Balduvian mist twee labels; samen negen. Dit zijn bekende
grenzen, geen negatieve beoordeling van de kaarten zelf.

Na de derde evaluatie zijn nog gerichte eigen-verliesguards en de live-poolvolgorde
verbeterd. Eindversie is dus engine v4; geen nieuwe ongeziene kwaliteitsclaim daarvoor.
Hergebruikte eindmetingen staan onder artifacts/phase16/final, expliciet regressie:
set 1 26/26, set 2 24/28 (drie bewust geweigerde layoutlabels en Angrath-controllerwissel),
set 3 14/23. De eerste uitkomsten zijn niet overschreven. Nul gevonden onterechte labels
in deze kleine sets is geen algemene precisieclaim. Geen onafhankelijke paar-/gameplaymeting.

Reproduceerbare bronnen: examples/synergy-holdout-cards-v1/v2/v3.json en bijbehorende
truth-bestanden, scripts/SynergyHoldout, scripts/Test-SynergyEvaluation.ps1. Eerste
voorspellingen/freeze/reserveringen staan onder artifacts/phase16; bevroren binaries
onder .local/phase16-engine-v1/v2/v3. Finalecodevingerafdruk: engine-final-delivery.json. De eerdere freeze blijft bewaard.

## Build, regressie en UI

Uitgevoerd: scripts/Verify.ps1 -SkipRestore -Screenshots -ReportPrefix phase16-delivery.
Eerder deze fase ook restore met volledige Verify uitgevoerd. Release-build geslaagd,
nul fouten en nul waarschuwingen. **538 tests geslaagd: 36 Domain en 502 Application/
echte SQLite/Avalonia; niets overgeslagen.** Dit zijn 95 extra gevallen ten opzichte
van v1.2.0. De inhoudelijke/metingsgrenzen hierboven blijven ondanks het testtotaal bestaan.

TRX: artifacts/test-results/phase16-delivery_net10.0_20261028132254.trx en
phase16-delivery_net10.0_20261028132401.trx. Buildlog artifacts/build.log.
Vier nieuwe testklassen: PhysicalSynergyTests, SynergyClauseTests,
SynergySafetyAndCoverageTests en SynergySelectionAndUiTests. Bestaande voorraad-,
legaliteits-, import-, reset-, back-up-, herstel-, GUI- en historische regressies behouden.
Na de laatste codewijziging is de volledige controle herhaald.

Gerenderde schermen onder artifacts/ui/phase16 bekeken: phase16-deck-synergy.png,
phase16-wizard-synergy.png en phase16-synergy-minimum.png, daarnaast bestaande fysieke
schermen opnieuw vastgelegd. UI-test opent het echte Cloud-deck, bereidt de echte wizard
voor, opent relaties en controleert een venster van 1050×740. Geen native muiscontrole
van elke afzonderlijke knop claimen. De daadwerkelijke Scryfall-afbeelding van Adelbert
is gecachet en in de schermcontrole gebruikt.

## Praktijk: complete voorstellen en opslag

Eindproef artifacts/phase16/final-owned.json: **27/27** thema-/stijlcombinaties,
allemaal 60 kaarten, nul voorraadproblemen. Themafamilies inclusief any en het nieuwe
vijandelijke-sterfthema. Referentie gebalanceerd: any WB, Equipment WR, tokens WB,
graveyard UB, eigen sacrifice WB, vijandelijke dood UB, Domain WBG, exalted WU, draw WU.
Domain heeft vijf verschillende basislandtypen. Geen castingkans of spelsterkte bewezen.
Vier productdecks opnieuw geanalyseerd; de historische Conflux-decks blijven 41 kaarten.

Live Scryfall en verse geïsoleerde database: artifacts/phase16/live-final.json.

| Verzoek | Resultaat |
| --- | --- |
| WR Equipment, bezit | 60 kaarten, nul tekorten |
| UB graveyard, bezit | 60 kaarten, nul tekorten |
| WB vijandelijke dood, bezit | 60 kaarten, nul tekorten |
| Cloud Commander, bezit | terecht geblokkeerd: 34/62 verschillende passende spreuken |
| Sephiroth Commander, bezit | terecht geblokkeerd: 23/62 verschillende passende spreuken |
| WR Standard, planning | 60 kaarten; 25 ontbrekende exemplaren van 14 Oracle-kaarten |
| Cloud Commander, planning | 100 kaarten; 39 ontbrekende exemplaren van 37 Oracle-kaarten |
| Sephiroth Commander, planning | 100 kaarten; 68 ontbrekende exemplaren van 41 Oracle-kaarten |

Alle zes gemaakte decks opgeslagen en na herstart teruggelezen; voorraad bleef 202,
decks gingen in deze geïsoleerde proef van vier naar tien. Commander gecontroleerd op
actuele legaliteit, kleuridentiteit, singleton en 100 kaarten; automatische selectie
bevat geen afgewezen layouts of herkende verliesblokkades. Geen echte telefoonscan of
fysieke speltest. Voorbereiding in deze proef circa 0,08–0,20 seconde voor bezit en
2,0–4,4 seconden voor planning inclusief bronverzoeken; geen algemene prestatienorm.

Eerdere live-proef is eveneens bewaard en toont de alfabetische bias en onveilige
verlieskaarten. De pool is nu begrensd op Scryfall-populariteit geordend. Dit is een
praktische bronkeuze, geen eigen optimaliteitsmeting; vaste 62/37- en 36/24-plannen blijven.

## Gegevensbehoud en echte appstart

Normale gebruikersdatabase eerst alleen-lezen onderzocht, zonder initializer/seeder/
netwerk: 202 exemplaren, 109 printings, 87 unieke Oracles en vier decks. Alle profielen
op deze werkelijke bron beoordeeld. Zie current-user-readonly.json en
current-user-final-readonly.json. Voor alle schrijf-/generatieproeven aparte databases.

Consistente voorafsnapshots en hashes over alle kolommen van twintig tabellen voor de
normale database en de bestaande gevulde smoke-database. Na echte appstart **alle hashes
gelijk** en alle originele rijen per sleutel/kolom behouden: respectievelijk 38.372 en
56.824 rijen. Negen migraties gebleven; geen nieuw schema of NuGet-pakket. Rapporten:
user/filled-before.json en user/filled-after-native.json, snapshots en vergelijking
onder artifacts/phase16. Geen gebruikersdatabase geleegd. Geen beveiligingsbeleid aangepast.

Native bronprogramma gestart met beide bestaande gegevensmappen, respons/venster/log
gecontroleerd en normaal afgesloten. Exacte laatste bronbuild: artifacts/
phase16-delivery-source-user-native.json en phase16-delivery-source-filled-native.json.
Een leeg argument aan de audithelper werd aanvankelijk geweigerd vóór openen; de
vergelijking is daarna met het expliciete door Windows opgegeven gegevenspad uitgevoerd.

## Definitieve distributiecontrole

Een eerste v1.3.0-pakket doorliep de v1.2.0-upgradeproef al. Bij de eindcontrole bleek dat profielbewijs na vijf fragmenten werd afgekapt; dat is hersteld, zodat ook latere beperkingen in de relatie blijven staan. Een nieuwe gerichte test bewaakt dit. De eerste pakketkandidaat is apart bewaard onder artifacts/phase16/release-candidate-v1; het definitieve pakket wordt opnieuw gebouwd en getest.

Deze ingesloten kopie beschrijft controles van vóór pakketbouw. Installer/portable
worden daarna met eigen runtime gecontroleerd. Het definitieve aanvullende resultaat
en hashes staan naast de release in artifacts/release/1.3.0/VALIDATIE-NA-PAKKET.md;
claim die installatiecontrole pas wanneer dat verslag daadwerkelijk bestaat.

Het definitieve verslag bestaat inmiddels: v1.2.0 → v1.3.0-installatie/upgrade/
uninstall/herinstallatie en laatste uninstall op geïsoleerde paden geslaagd. Alle twintig
tabellen exact gelijk; schema negen bleef negen, nul nieuwe migratieback-ups. Eigen
runtime en Startmenu-doel gecontroleerd. Definitieve portable start/respons/log/normale
afsluiting met beide bestaande gegevensmappen bevestigd. Laatste vergelijkingen:
user/filled-after-package.json, alle twintig hashes en alle originele rijen/kolommen gelijk.
Alle 266 ZIP-items matchen de portable inhoud; installer/ZIP-hashes matchen het manifest.
Installer/programma NotSigned. Open zichtbare v1.3.0 op normale data bevestigd in
artifacts/phase16/final-visible-start.json. Geen latere codewijziging na de 538 tests.

Geen signing-certificaat, schone tweede machine, ARM-, gameplay-, winrate- of
stroomuitvaltest. Geen onafhankelijke brede kaart-/deckkwaliteit bewezen. Stop na fase 16
voor gebruikersbeoordeling; geen volgende fase gestart.
