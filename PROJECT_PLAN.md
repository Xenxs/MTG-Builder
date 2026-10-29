# Projectplan — MTG Arena Smart Deckbuilder

## Doel en afbakening

Fase 1 levert een compileerbare Windows-desktopbasis met betrouwbare lokale opslag,
werkende navigatie, handmatige collectie- en deckbewerkingen en automatische tests.
Een dashboard alleen zou de opslag niet bewijzen: daarom zijn echte invoer- en
bewerkingsstromen onderdeel van deze fase. Slimme aanbevelingen vallen erbuiten.

## Fase 1

- [x] Ontwikkelomgeving controleren: Windows x64, .NET SDK 10.0.401, runtime 10.0.12.
- [x] Solution met vier productielagen en twee testprojecten aanmaken.
- [x] NuGet-afhankelijkheden met vaste versies herstellen.
- [x] Dark-mode MVVM-shell en vier functionerende schermen implementeren.
- [x] Handmatige collectie, deckinhoud en instellingen opslaan.
- [x] SQLite-schema, databasebeperkingen en initiële EF-migratie genereren.
- [x] Dependency injection, logging, invoervalidatie en centrale foutafhandeling toevoegen.
- [x] Domein-, applicatie-, SQLite-integratie- en headless UI-tests uitvoeren.
- [x] Gerenderde schermen visueel inspecteren.
- [x] Definitieve controle en native Windows-appstart vastleggen in docs/VALIDATION.md.
- [x] Architectuur, beslissingen en ontwikkelinstructies documenteren.

Opleverresultaat op 21 oktober 2026: Release-build zonder waarschuwingen/fouten;
27 tests geslaagd (13 Domain, 14 Application/integratie/UI). De definitieve native
Windows-app is gestart en reageert. Decimale aantallen worden geweigerd; deckregels
tonen het collectornummer. Smart App Control stond bij de vervolgcontrole uit; er is
geen beveiligingsbeleid gewijzigd. Resultaten en eerdere beleidsblokkades staan in
docs/VALIDATION.md. Fase 1 is opgeleverd;
fase 2 is op 21 oktober 2026 expliciet gestart door de gebruiker.

## Fase 2 — geaccepteerd

Gestart: 21 oktober 2026 om circa 21:00 (Europe/Amsterdam).
Goedgekeurde scope: kaartcatalogus, herhaalbare catalogusimport, zoeken en filters,
met behoud van bestaande collectie en decks. De onderstaande tijdsindicatie blijft gelden.

Aanbevolen configuratie (advies op 21 oktober 2026): **GPT-6.1 Sol — High reasoning**.
Deze fase combineert kaart-/printingidentiteiten, schemawijzigingen en een herhaalbare
import die bestaande collectiegegevens moet behouden. High is hiervoor een passend
startpunt; Extra high/Max is pas zinvol bij aantoonbaar moeilijke problemen.
De gebruiker heeft vervolgens toestemming gegeven om model en reasoning automatisch
aan het advies aan te passen. In deze sessie is geen directe instellingsbediening
beschikbaar; een daadwerkelijke wijziging is daarom niet uitgevoerd of bevestigd.
Bij volgende fases mag ondersteunde bediening zonder hernieuwde toestemming worden
gebruikt, met controle en vermelding van de werkelijk ingestelde configuratie.

Voorlopige tijdsindicatie: **60–120 minuten actieve uitvoering**, inclusief build,
tests en documentatie, voor kaartcatalogus, herhaalbare catalogusimport en zoeken/filters.
Dit is een planningsinschatting, geen gemeten doorlooptijd of gegarandeerde eindtijd.
Bij problemen met brondata, kaart-/printingmapping of de ontwikkelomgeving kan dit
oplopen tot circa **2–3 uur**. De inschatting wordt vóór de start aan de definitieve
scope getoetst en bij relevante nieuwe bevindingen bijgewerkt. Volledige Arena-
collectie-import, legaliteitsregels en deckadvies zijn niet in deze tijdsindicatie opgenomen.

Bronnen: [OpenAI model selection](https://developers.openai.com/api/docs/guides/model-selection)
en [OpenAI reasoning guidance](https://developers.openai.com/api/docs/guides/reasoning).

1. Definieer eerst een betrouwbare catalogus en mapping tussen kaart, printing,
   Scryfall-ID en Arena-ID. Naam alleen is geen betrouwbare importidentiteit.
2. Bouw een herhaalbare, hervatbare kaartimport met bronversie en importregistratie.
   Houd collectiebezit volledig gescheiden van catalogusverversingen.
3. Voeg zoeken, filters en het expliciet opslaan van aantallen voor een geselecteerde
   catalogusprinting toe. Importeren van catalogusdata verandert geen bezit.

Buiten deze fase blijven: collectie-import met voorbeeldweergave/conflictcontrole,
formaatlegaliteit, ontbrekende kaarten/wildcards en slimme aanbevelingen. Betrouwbare
kaartidentiteiten en brondata komen eerst; aanbevelingen moeten later meetbaar worden getoetst.

### Uitvoering fase 2

- [x] Expliciete startopdracht ontvangen; model/reasoning en tijdsindicatie vermeld.
- [x] Bestaande architectuur en actuele primaire kaartbron onderzocht.
- [x] Catalogusidentiteiten en een databehoudende migratie implementeren.
- [x] Streamingimport met bronversie, importregistratie, annuleren en hervatten implementeren.
- [x] Catalogus doorzoeken/filteren en geselecteerde printing aan collectie toevoegen.
- [x] Migratie-, import-, fout- en schermtests uitvoeren en fouten oplossen.
- [x] Volledige build, visuele controle en native appstart uitvoeren.
- [x] Oplevering en beperkingen documenteren; stoppen vóór fase 3.

Resultaat: versie 0.2.0; 55 automatische tests geslaagd, Release-build zonder
waarschuwingen/fouten. Een echte Scryfall-import verwerkte 119.106 bronrecords tot
21.621 Arena-printings en 16.399 kaartdefinities. Ook de gebruikersdatabase is met een
voorafgaande upgradeback-up bijgewerkt; bezit/decktotalen bleven gelijk. Zie
docs/VALIDATION_PHASE2.md voor de meetresultaten en exacte grenzen. De bronimport zelf
duurde hier circa 9–18 seconden; dit is iets anders dan de ontwikkeltijd van de fase.
Verstreken uitvoering van fase 2: circa 70–75 minuten, binnen de vooraf geraamde 60–120 minuten.

De gebruiker heeft fase 2 op 21 oktober 2026 expliciet goedgekeurd.

## Fase 3 — geaccepteerd

Expliciet gestart door de gebruiker op 22 oktober 2026, circa 08:46 Europe/Amsterdam.
Advies: **GPT-6.1 Sol / High reasoning**, vanwege identiteitskoppelingen en
gegevensbehoud. Geen modelwissel uitgevoerd: directe instellingsbediening ontbreekt.
Raming: **2–4 uur actieve uitvoering** inclusief build, tests en documentatie;
**4–6 uur** bij lastige bronformaten. Dit is geen gegarandeerde eindtijd.

- [x] Startopdracht, modeladvies en tijdsindicatie vastleggen.
- [x] Bronhaalbaarheid controleren en één expliciet versieerbaar bestandsformaat vastleggen.
- [x] Import voorbereiden zonder bezit te wijzigen; onbekende/ambigue regels tonen.
- [x] Koppelingen en overslaan expliciet laten kiezen, met paginering.
- [x] Absolute aantallen atomair toepassen; ontbrekende regels ongemoeid laten.
- [x] Stale previews blokkeren en latere handmatige wijzigingen bij herstel beschermen.
- [x] Persistente historie, gecontroleerd herstel en collectie-export toevoegen.
- [x] Domein-, database-, migratie- en UI-tests uitvoeren; schermen inspecteren.
- [x] Volledige build, native start en documentatie afronden; stoppen vóór fase 4.

Geen Arena Player.log gevonden op de standaardlocatie in deze ontwikkelomgeving.
De officiële logdocumentatie garandeert geen complete collectie-snapshot. Daarom
wordt een gedocumenteerde JSON-bestandsimport gebouwd; automatische Arena-sync wordt
niet als bewezen functie gepresenteerd. Een echte Arena-bron blijft apart te valideren.

Resultaat: **v0.3.0**, Release-build met 0 waarschuwingen/fouten, **93 tests geslaagd**
(19 Domain, 74 Application/integratie/UI). Definitieve native Windows-start op
22 oktober 2026 om circa 09:14 Europe/Amsterdam; applicatie reageert. De normale
database is met upgradeback-up naar drie migraties gebracht; voor/na-controlegetallen
voor bezit, decks en instellingen zijn gelijk. Die gebruikerstabellen waren leeg;
gevuld gegevensbehoud is aanvullend met geïsoleerde tests bewezen.

Een praktijkproef gebruikte de echte kaartcatalogus (21.621 printings) en 2.000
gesimuleerde bezitsregels. Voorbereiden, toepassen, export/herimport en herstel zijn
geslaagd; herimport had 0 wijzigingen en herstel behield de oorspronkelijke aantallen
en het deck. De back-upverbinding blijft na afloop niet meer in de connection pool;
de migratietest controleert dat het back-upbestand direct exclusief te openen is.

De oorspronkelijke raming van 2–4 uur was ruim: door de afgebakende JSON-scope en
hergebruik van de fase-2-architectuur duurde de uitvoering **circa 30 minuten**.
Tijdens het werk is de resterende raming bijgesteld naar 15–30 minuten. Werkelijke
metingen en beperkingen staan in docs/VALIDATION_PHASE3.md; formaat en gebruik in
docs/COLLECTION_IMPORT.md; alle 86 bron-/configuratie-/documentatiebestanden in docs/FILES.md.

Fase 3 is op 22 oktober 2026 door de gebruiker geaccepteerd met "goed ga door".

## Fase 4 — geaccepteerd

Gestart op 22 oktober 2026, circa 09:17 Europe/Amsterdam. Advies: **GPT-6.1 Sol /
High reasoning**, wegens kaartidentiteiten, veilige deckimport en correcte
bezitsverdeling tussen zones/reprints. Geen modelwissel uitgevoerd: bediening ontbreekt.
Raming: **60–120 minuten actieve uitvoering**, inclusief tests/documentatie;
**2–3 uur** bij lastige varianten. Geen gegarandeerde eindtijd.

Scope: Arena-decktekst importeren met voorbereiding en expliciete probleemoplossing,
exporteren, plannen met kaarten buiten bezit, mainboard/sideboard/commander, ontbrekende
exemplaren per kaartidentiteit en per exacte printing, mana-/type-/kleurstatistieken.
Import maakt een nieuw deck; bestaande decks en bezit worden niet vervangen.
Companion-/maybeboard-notatie, formele legaliteit, wildcardkosten en aanbevelingen
blijven buiten deze fase. Niet-ondersteunde secties worden expliciet geweigerd.

- [x] Scope, modeladvies, raming en officiële Arena-importdocumentatie controleren.
- [x] Domein- en applicatiecontracten voor deckvoorbereiding en analyse bouwen.
- [x] Persistente, atomaire nieuwe-deckimport en tekstexport implementeren.
- [x] Commander-zone via migratie toevoegen zonder bestaande deckregels te wijzigen.
- [x] Bezit over reprints en zones correct verdelen; beschrijvende statistieken tonen.
- [x] Deckschermen met cataloguskeuze, import, analyse en export implementeren.
- [x] Parser-, database-, migratie-, conflict- en UI-tests uitvoeren; schermen bekijken.
- [x] Build, native start, documentatie en gegevensbehoud afronden; stoppen vóór fase 5.

Oplevering op 22 oktober 2026: **v0.4.0**, build met 0 waarschuwingen/fouten en
**134 tests geslaagd** (21 Domain, 113 Application/integratie/UI). De definitieve native
app is om circa 09:50 Europe/Amsterdam gestart en reageert. Uitvoering circa **35–40
minuten**, korter dan de vooraf geraamde 60–120 minuten door hergebruik en afgebakende scope.

Een praktijkproef met de echte catalogus importeerde en exporteerde een technische lijst
met 60 mainboardkaarten en 2 sideboardkaarten; herimport behield kaarten, aantallen en zones.
De reprintproef onderscheidt kaarttekort 0 van exact tekort 2 bij vier exemplaren verdeeld
over twee uitgaven. Een definitieve native migratie van gevulde fase-3-testdata behield
bezit, deck en 4.001 collectie-auditregels. De tabelomzetting is transactioneel en met
foutinjectie gecontroleerd. De normale gebruikersgegevens zijn bij de upgrade behouden;
deze tabellen waren leeg, dus gevuld behoud is afzonderlijk bewezen.

Zie docs/DECKS.md voor gebruik, docs/VALIDATION_PHASE4.md voor exacte bewijsbestanden en
grenzen, docs/DECISIONS.md voor keuzes en docs/FILES.md voor het complete bestandenoverzicht.
Geen test in een ingelogde Arena-client, legaliteit of slimme aanbevelingen geclaimd.
Bij oplevering van fase 4 was fase 5 nog niet gestart en vereiste die een afzonderlijke
startopdracht met scope, modeladvies en tijdsindicatie; die is later ontvangen, zie hieronder.

De gebruiker heeft fase 4 op 22 oktober 2026 expliciet geaccepteerd met "fase 4 is goed"
en voor fase 5 alleen informatie gevraagd: "begin nog niet".

## Fase 5 — geaccepteerd

Voorgestelde scope: **formaatcontrole en uitlegbare deckvalidatie**, eerst voor
Arena Standard Best-of-One en Best-of-Three. Dit vormt de basis voor latere
aanbevelingen: een advies over een deck moet rekening houden met de toegestane kaarten
en deckregels. Alle formaten tegelijk ondersteunen is voor deze fase te breed.

Advies: **GPT-6.1 Sol / High reasoning**, wegens samenhang tussen kaartidentiteiten,
regeluitzonderingen, versieerbare brondata en migraties met gegevensbehoud. Dit is een
projectadvies, geen letterlijk voorgeschreven reasoningniveau uit de modeldocumentatie.
Geen chatinstellingen gewijzigd: directe ondersteunde instellingsbediening ontbreekt.
Bron: [OpenAI model selection](https://developers.openai.com/api/docs/guides/model-selection).

Tijdsindicatie: **2–4 uur actieve uitvoering**, inclusief broncontrole, implementatie,
database-upgrade, bouwen, testen, visuele controle en documentatie. Bij moeilijk
verifieerbare Arena-afwijkingen of kaartuitzonderingen kan dit **4–6 uur** worden.
Geen vaste timer of gegarandeerde eindtijd; bij de start opnieuw toetsen aan de scope.

Voorgestelde oplevering:

1. Expliciete formaat-/wedstrijdprofielen; bestaande vrije formaatlabels zorgvuldig
   omzetten of als niet-ondersteund behouden, zonder zelf een profiel te raden.
2. Legaliteitsgegevens met bron, ophaaldatum en geldigheid naast de bestaande catalogus
   opslaan en herhaalbaar verversen. Geen bezit of deckinhoud wijzigen.
3. Minimumdeckgrootte, sideboardgrenzen, aantallen over zones en verschillende printings,
   toegestane kaarten, bans en bekende kaartuitzonderingen controleren. Arena-specifieke
   BO1-/BO3-regels bij uitvoering aan actuele primaire bronnen toetsen.
4. Per probleem kaart, reden en herstelrichting tonen. Status onderscheidt fouten,
   geslaagde controles en onvolledige controle. Onbekende kaarten, niet-ondersteunde
   uitzonderingen of verouderde brondata leveren geen ongekwalificeerd groen oordeel op.
5. Migratie-, regel-, import- en UI-tests, visuele controle, native start en
   controleerbare documentatie opleveren. Ook bestaande collectie en decks behouden.

Buiten dit voorstel: alle andere formaten, volledige Companion-/Commander-regels,
wildcardbudgetten, automatische Arena-sync, strategische kaartaanbevelingen en claims
over winkans. Ontbrekend bezit blijft onderscheiden van formaatlegaliteit.

Primaire regelbron bij voorbereiding:
[Wizards Standard](https://magic.wizards.com/en/formats/standard).
De Scryfall-documentatie gaf bij deze raadpleging HTTP 403; de concrete dekking en
actualiteit van legaliteitsvelden moeten vóór implementatie alsnog worden bewezen.
Dit was de toestand bij het voorstel; uitvoering is daarna expliciet toegestaan.

### Uitvoering fase 5

Expliciet gestart op 22 oktober 2026, circa 10:21 Europe/Amsterdam. Advies vóór start:
**GPT-6.1 Sol / High reasoning**, wegens regeluitzonderingen, kaartidentiteiten en
veilige migraties. Geen model-/reasoninginstelling gewijzigd: directe bediening ontbreekt.
De raming van 2–4 uur is tijdens uitvoering bijgesteld naar 20–40 minuten resterend.
De afgebakende Standard-scope en hergebruik van import-/testinfrastructuur verkortten
de uitvoering tot **circa 20–25 minuten**, inclusief bouwen, testen en documenteren.

- [x] Expliciete start, modeladvies en tijdsindicatie vastleggen.
- [x] Actuele primaire bronnen controleren: BO1 heeft sinds februari 2026 ook maximaal 15 sideboardkaarten.
- [x] Expliciete BO1-/BO3-profielen toevoegen zonder bestaande labels te raden of te vervangen.
- [x] Standard-legaliteit met bronversie, brondatum, importdatum en metadata-versie opslaan.
- [x] Catalogus en legaliteiten herhaalbaar/atomair verversen, met rollback bij fouten/annulering.
- [x] Deckgrootte, sideboard, commander, aantallen over printings/zones, bans en ondersteunde uitzonderingen controleren.
- [x] Onbekende metadata/verouderde bronnen zichtbaar laten blokkeren op een volledig geslaagd oordeel.
- [x] Formaatcontrole in Mijn decks toevoegen, met meldingen, herstelrichtingen en brondata.
- [x] Domein-, regel-, database-, migratie- en echte UI-bindingtests uitvoeren; schermen bekijken.
- [x] Echte catalogusproef, native upgrade/start, gegevensbehoud en documentatie afronden.

Resultaat: **v0.5.0**, Release-build met **0 waarschuwingen/fouten** en **168 tests
geslaagd** (27 Domain, 141 Application/integratie/UI), 0 mislukt/overgeslagen.
Native app om circa **10:37 Europe/Amsterdam** gestart en reagerend. Normale database
met upgradeback-up naar vijf migraties gebracht. Controlehashes van oorspronkelijke
kaarten, printings, bezit, decks, instellingen en importhistorie bleven gelijk.
De normale bezit/decktabellen waren leeg; een afzonderlijke native upgrade van gevulde
fase-4-data behield 2 bezitsregels, 2 decks, 22 deckregels en 22 deckimport-auditregels.

Echte Scryfall-bron: 119.115 records, 21.621 Arena-printings, 16.399 kaartidentiteiten;
5.164 Standard legal, 11.222 not_legal, 13 banned. De technische testlijst met Leyline
of Resonance krijgt een BO1-ban en slaagt in BO3. Vijftien sideboardkaarten slagen,
zestien geven een fout; drie plus twee Negates over verschillende printings/zones
geven gezamenlijk een vier-exemplarenfout. Bezit blijft ongewijzigd.

Brondata ouder dan 24 uur en de na 14 dagen verouderde ingebouwde aanvullende regels
maken een controle onvolledig. Bestandsimport bewijst geen brondatum. Geen volledige
regelengine, alle formaten, Companion-keuze, wildcardbudget, aanbevelingen of live
Arena-acceptatie geclaimd. Gebruik: docs/FORMAT_VALIDATION.md; bewijs:
docs/VALIDATION_PHASE5.md. De gebruiker heeft fase 5 op 22 oktober 2026 expliciet
geaccepteerd met "fase 5 is goed". De latere fase-6-start staat hieronder.

## Fase 6 — geaccepteerd: uitlegbare kaartalternatieven uit eigen collectie

Expliciet gestart op 22 oktober 2026, circa 10:53 Europe/Amsterdam. Doel: ontbrekende exemplaren in een
bestaand Arena Standard BO1-/BO3-deck helpen vervangen met lokaal geregistreerde kaarten.
Een overeenkomst in type/kleur/manawaarde bewijst geen gelijkwaardige spelwerking of
hogere winkans. De eerste aanbevelingen krijgen daarom expliciete redenen en grenzen.

Modeladvies: **GPT-6.1 Sol / High reasoning**, voor samenhang tussen voorraadverdeling,
kaartidentiteiten, rangschikking, legaliteitscontrole en veilige toepassing. High is
een projectkeuze; de officiële modeldocumentatie beschrijft de ondersteunde instellingen:
[OpenAI Docs](https://developers.openai.com/api/docs/models/gpt-6.1-sol).
Geen chatinstellingen gewijzigd: directe ondersteunde bediening ontbreekt.

Raming voor deze afgebakende scope: **60–120 minuten actieve uitvoering**, inclusief
implementatie, bouwen, tests, schermcontrole en documentatie. **2–3 uur** bij lastige
voorraad-/regelcombinaties. De vorige fases waren aanzienlijk korter dan geraamd; deze
bandbreedte houdt rekening met hergebruik, maar is geen vaste timer of gegarandeerde eindtijd.

Voorgestelde scope:

1. Begin met een bestaand deck dat de fase-5-controle met actuele gegevens doorstaat.
   Bij fouten, verlopen regelversie of onvolledige data toont de app eerst wat nodig is
   om betrouwbaar te adviseren. De bestaande vervalgrenzen worden niet versoepeld.
2. Bied eerst andere bezeten printings van dezelfde Oracle-kaart aan. Voor ontbrekende
   niet-landen in het mainboard kunnen daarna alternatieve kaarten worden gezocht.
   Automatische manabase- of sideboardstrategie valt buiten deze eerste versie.
3. Filter op ondersteund profiel, kaartlimieten en werkelijk beschikbaar lokaal bezit.
   Alle zones delen dezelfde voorraad; een gezamenlijk voorstel gebruikt geen exemplaar
   dubbel. Een onbewezen handmatige identiteit krijgt geen automatische equivalentie.
4. Rangschik geschikte kandidaten met vaste, controleerbare criteria, zoals kleuridentiteit,
   kaarttype en verschil in manawaarde. Toon beschikbare aantallen en redenen/verschillen.
   Deze overeenkomst is geen bewijs dat effecten of strategische rollen gelijkwaardig zijn.
5. Laat de gebruiker voorstellen kiezen en bekijk de gezamenlijke wijzigingen vóór opslag.
   Controleer de volledige uitkomst opnieuw en bewaar die als nieuw deck. Originele deck
   en bezit blijven behouden. Bij gewijzigde bron, deck of voorraad moet het voorstel
   opnieuw worden berekend; geen verouderde voorbereiding stilzwijgend toepassen.
6. Test rangschikking, voorraadverdeling, limieten, actualiteit, conflicten en gegevensbehoud;
   controleer de schermen en documenteer bekende beperkingen en de werkelijke resultaten.

Buiten scope: volledige automatische deckgeneratie, bewezen synergie-/winkansoptimalisatie,
metagamedata, wildcardbudget, externe AI-API, nieuwe formaten en live Arena-sync. De eerste
aanbevelingen draaien lokaal en gebruiken de bestaande catalogus, collectie en regelcontrole.
Startopdracht ontvangen: "start fase 6". Stop na oplevering vóór fase 7.

- [x] Scope, modeladvies en tijdsindicatie gecontroleerd; chatinstellingen ongewijzigd.
- [x] Deterministische adviesregels en voorraadverdeling geïmplementeerd.
- [x] Persistente voorbereiding, wijzigingscontrole en atomaire opslag geïmplementeerd.
- [x] Advieskeuze en gezamenlijke voorbeeldweergave in Mijn decks toegevoegd.
- [x] Regel-, opslag-, migratie-, conflict- en UI-tests uitvoeren.
- [x] Build, visuele controle, native start en documentatie afronden.

Resultaat: **v0.6.0**, definitieve Release-build met **0 waarschuwingen/fouten**,
**197 tests geslaagd** (28 Domain, 169 Application/integratie/UI), 0 mislukt/overgeslagen.
Native Windows-app op 22 oktober 2026 om **11:15 Europe/Amsterdam** geïnitialiseerd en
reagerend open gelaten. Zesde migratie voegt alleen DeckAdvicePlans toe. Back-ups vooraf
gemaakt; alle oorspronkelijke controlehashes gelijk. Gevulde native upgrade behield
2 bezitsregels, 2 decks, 22 deckregels en 22 deckimport-auditregels.

Een geïsoleerde kopie van de echte catalogus met **21.621 printings** en **2.003 gesimuleerde
bezitsregels** leverde 64 kandidaten in **0,163 s**. Een gekozen Negate-reprint plus
Essence Scatter werd als nieuw deck opgeslagen, met 0 resterende tekorten. Origineel en
voorraad gelijk; herhaalde opslag maakt geen tweede deck. De rangschikking herkent geen
kaartrollen: bijvoorbeeld een kaarttrek-instant kan structureel hoog bij een counter eindigen.

Werkelijke uitvoering **circa 25–30 minuten**, korter dan de aanvankelijke 60–120 minuten
door hergebruik van voorraad-, migratie- en regelinfrastructuur. Tijdens uitvoering is
na de eerste testopbouwfout een resterende 30–60 minuten genoemd; er volgde geen verdere
blokkade. Dit zijn ontwikkeltijden; de genoemde seconden zijn losse bewerkingstijden.

Gebruik: docs/DECK_ALTERNATIVES.md. Bewijs en grenzen: docs/VALIDATION_PHASE6.md.
Architectuur, ADR-028/029/030 en bestandenlijst bijgewerkt. De normale gebruikerscatalogus
heeft nog geen legaliteitsgegevens; vóór advies moet die online worden ververst en
moet het bron-deck de formaatcontrole doorstaan. Geen testkaarten aan gebruikersbezit toegevoegd.
De gebruiker heeft fase 6 op 22 oktober 2026 geaccepteerd met "Fase 6 ok".
Fase 7 is later expliciet gestart, zie hieronder.

## Fase 7 — geaccepteerd: kaartrollen en meetbare advieskwaliteit

Doel: het zwakste onderdeel van fase 6 verbeteren. De structurele rangschikking kan
een kaarttrek-instant hoog bij een counter laten eindigen. Voorraad- en regeltests
bewijzen veilige verwerking, maar bewijzen niet dat het vervangingsadvies inhoudelijk
bruikbaar is. Voeg daarom beperkte rolherkenning en een afzonderlijke evaluatieset toe.

Modeladvies: **GPT-6.1 Sol / High reasoning**, vanwege ambiguïteit in kaarttekst,
doelbeperkingen en het voorkomen van regressies in bestaande advies-/opslagregels.
Dit is een projectadvies; ondersteunde reasoningniveaus staan in
[OpenAI Docs](https://developers.openai.com/api/docs/models/gpt-6.1-sol).
Geen chatinstellingen gewijzigd: directe ondersteunde instellingsbediening ontbreekt.

Tijdsindicatie: **60–120 minuten actieve uitvoering**, inclusief ontwerp, implementatie,
een afgebakende referentieset, bouwen, tests, schermcontrole en documentatie. Bij lastige
voorwaardelijke of meervoudige effecten **2–3 uur**. De eerdere fases waren korter door
hergebruik; hier zit de onzekerheid vooral in interpretatie en controle van kaarttekst.
Dit is geen gegarandeerde eindtijd. Bij de start de scope en raming opnieuw toetsen.

Voorgestelde scope:

1. Beperk de eerste rolherkenning tot vier effectfamilies: counterspells, gerichte
   removal, kaarten trekken en mana-versnelling. Bewaar classificatieregel/versie en
   uitleg. Onbekende of niet ondersteunde tekst blijft expliciet onbekend; geen algemene
   claim dat de volledige Oracle-tekst wordt begrepen.
2. Onderscheid doelwitten en relevante beperkingen. Creature-only counter is geen
   gelijkwaardige vervanging voor noncreature-only counter. Maak verschillen in voorwaarden,
   timing en ondersteunde kosten zichtbaar; ga bij complexe modaliteiten/triggered of
   activated effecten niet af op het voorkomen van losse woorden alleen.
3. Houd dezelfde Oracle-kaart als eerste voorkeur. Rangschik daarachter rolpassende
   kandidaten vóór losse structurele overeenkomsten. Toon waarom een kandidaat past,
   welke verschillen bekend zijn en wat niet beoordeeld is. Laat zwakke matches niet
   stilzwijgend als sterke rolvervangers doorgaan.
4. Maak circa 20–30 vastgelegde, handmatig nagekeken referentiegevallen met passende,
   ongeschikte en onbekende gevallen. Meet de volgorde van de eerste resultaten ten
   opzichte van fase 6, inclusief tegenvoorbeelden. Een groene technische test alleen
   is geen kwaliteitsmeting; publiceer de feitelijke uitkomst en de grenzen van deze set.
5. Behoud alle bestaande voorraad-, profiel-, bronactualiteits-, conflict- en
   nieuwe-deckopslagcontroles. Test de nieuwe classificatie/rangschikking en voer de
   volledige regressie- en visuele controle uit.

Oplevercriterium: de referentieset toont aantoonbaar betere rolrelevantie dan de
fase-6-rangschikking zonder de bestaande veiligheidscontroles te verzwakken. Onzekerheid
blijft zichtbaar; een hogere referentiescore bewijst geen hogere winkans in echte matches.

Buiten scope: volledige automatische deckgeneratie, volledige spelregel-/synergie-engine,
metagame-optimalisatie, wildcardbudget, nieuwe formaten, live Arena-sync en externe AI-API.
Expliciet gestart met "Start fase 7" op 22 oktober 2026, circa 11:25 Europe/Amsterdam.
Advies en raming vóór de start bevestigd; geen chatinstellingen gewijzigd.

- [x] Startopdracht, afbakening en acceptatiecriteria vastleggen.
- [x] Referentiegevallen met verwachte rollen en tegenvoorbeelden vastleggen.
- [x] Beperkte classificatie, rolvergelijking en rangschikking implementeren.
- [x] Uitleg, onzekerheid en versie in UI en voorbereiding vastleggen.
- [x] Classificatie, meetresultaten, opslag en volledige regressies toetsen.
- [x] Schermen bekijken, native app starten en documentatie afronden.

Stop na fase 7; fase 8 vereist een afzonderlijke startopdracht.

Oplevering: **v0.7.0**, Release-build met **0 waarschuwingen/fouten** en **239 geslaagde
tests** (28 Domain, 211 Application/integratie/UI), 0 mislukt/overgeslagen. De vaste set
van 30 Oracle-teksten behoudt alle verwachte labels (18 herkend, 12 bewust onbekend).
De eerste-keuzescore op tien gekozen rangschikkingsscenario's stijgt **3/10 → 10/10**;
gemiddelde omgekeerde positie **0,583 → 1,000**. Dit is geen onafhankelijke of representatieve
kwaliteitsmeting; bezit en legaliteit zijn voor die scenario's gesimuleerd.

Praktijkdekking is expliciet beperkt: **16 van 5.164 Standard-legale kaartdefinities**
herkend; 5.148 onbekend. Binnen het typebereik is dit 16 van 1.022 Instant/Sorcery-kaarten.
Deze eerste module mag niet als brede kaartkennis of hogere winkans worden gepresenteerd.
De echte Negate-proef plaatst Cancel nu als rolpassend na de reprint en waarschuwt bij
Essence Scatter voor andere doelwitten. 64 kandidaten zoeken duurde circa 0,168 seconde.

Nieuwe voorbereidingen bewaren versies, regels en uitleg in de bestaande JSON-kolom;
geen nieuwe migratie. Oude ongebruikte voorbeelden blijven bewaard maar moeten worden
herberekend; oude toegepaste ontvangsten blijven idempotent. Native start op de normale
gebruikersdatabase om circa **11:42 Europe/Amsterdam**, reagerend open gelaten. Alle hashes
van 13 bestaande tabellen gelijk; een gevulde native fase-6-proef behield 2.003 bezitsregels,
4 decks, 11 deckregels en een oude adviesontvangst. Zes migraties blijven staan.

Uitvoering **circa 20–25 minuten** (circa 11:25–11:47), korter dan de raming door hergebruik
en strikt afgebakende tekstpatronen. Er is geen brede semantische engine gebouwd om die
tijd te verkorten of als volwaardig te claimen: de geringe dekking is gemeten en vastgelegd.
Handleiding: docs/CARD_ROLES.md; exact bewijs: docs/VALIDATION_PHASE7.md; complete bestandenlijst:
docs/FILES.md. Fase 7 is geaccepteerd met "Goed ga naar fase 8"; daarmee is fase 8 expliciet gestart.
## Fase 8 — opgeleverd en geaccepteerd

Gestart op 22 oktober 2026, circa 12:29 Europe/Amsterdam. Advies: GPT-6.1 Sol / High
voor zorgvuldige tekstinterpretatie en het voorkomen van onterechte rolclaims.
Geen ondersteunde directe modelbediening gevonden; chatinstellingen niet gewijzigd.
Raming: **45–90 minuten actieve uitvoering**, inclusief analyse, implementatie, build,
tests, schermcontrole en documentatie; **90–150 minuten** bij lastige tekstvarianten.
Dit is geen vaste timer. De eerdere beperkte dekking (16/5.164) is de verbeterprioriteit.

Scope: de vier bestaande effectfamilies gerichter herkennen in de echte catalogus,
volledige herkenning scheiden van onvolledige aanwijzingen, beperkingen zichtbaar houden,
en een apart gereserveerde evaluatieset meten. De bron, selectie en beperkingen van die
set worden vastgelegd; geen onafhankelijke reviewer of algemene kwaliteit claimen zonder
bewijs. Voorraad-, formaat-, actualiteits- en opslagcontroles blijven behouden.
Geen volledige deckgeneratie, winkans-/synergie-engine, externe AI-API of nieuwe formaten.

- [x] Start, acceptatie fase 7, scope, modeladvies en raming vastleggen.
- [x] Werkelijke onbekende teksten analyseren en evaluatiegevallen vooraf reserveren.
- [x] Herkenning en uitleg uitbreiden zonder gedeeltelijke tekst als volledig te presenteren.
- [x] Aparte evaluatie, praktijkdekking en regressies meten.
- [x] Gewijzigde schermen, native start, gegevensbehoud en documentatie afronden.

Opgeleverd als **v0.8.0**. Definitieve restore/Release-build: **0 waarschuwingen/fouten**;
**255 geslaagde tests** (28 Domain, 227 Application/integratie/UI), 0 mislukt/overgeslagen.
Classifier v2 en engine v3 bewaren herkende passage, niet beoordeelde resttekst,
mechanisme en extra effecten. Een onvolledige aanwijzing wordt nooit een sterke match;
bij gelijke vergelijkingscategorie hebben volledige vergelijkingen voorrang.

Dezelfde echte snapshot van 5.164 Standard-legale kaarten: **43 volledig herkend**
(fase 7: 16), **207 onvolledige aanwijzingen**, **4.914 onbekend**. Volledige dekking
blijft slechts circa 0,83%. De ontwikkelset telt 24 volledig, 4 gedeeltelijk en 2 onbekend;
de bestaande tien rangschikkingsscenario's blijven 10/10 tegen fase-6-baseline 3/10.
Dit is een regressiescore, geen algemeen kwaliteitsbewijs.

Dertig vooraf gereserveerde andere kaarten zijn na parserbevriezing gelabeld door
dezelfde ontwikkelaar, vóór de voorspellingen per kaart werden bekeken. Uitkomst:
**0 volledig, 4 onvolledig, 26 onbekend; 15 van 19 relevante kaarten gemist**.
Geen verkeerde families bij vier aanwijzingen en alle vereiste voorwaarden zichtbaar,
maar dit bewijst geen brede precisie. Geen onafhankelijke reviewer of representatieve
steekproef; de parser is niet achteraf op die uitkomsten aangepast. Deze zwakke
dekking is expliciet gepubliceerd, niet verborgen achter de groene tests.

De echte catalogusproef zoekt 64 Negate-kandidaten in circa 0,222 seconde en bewaart
reprint plus gewaarschuwde Essence Scatter als nieuw deck; origineel/bezit gelijk,
herhaling idempotent en twee oudere ontvangsten blijven bruikbaar. Native v0.8.0
start met de normale database om circa **12:50 Europe/Amsterdam**, reagerend open
gelaten. Ook de bestaande gevulde proef is native gestart en normaal gesloten.
Bij beide databases zijn alle hashes van 13 tabellen gelijk; de gevulde proef behoudt
2.003 bezitsregels, 6 decks, 16 deckregels en 2 ontvangsten. Zes migraties blijven staan;
geen extra upgradeback-up, pakket, secrets of beveiligingswijziging.

Actieve uitvoering **circa 25–30 minuten** (12:29–circa 12:55), korter dan geraamd
door hergebruik van de catalogus, opslagproeven en bestaande UI. De scope blijft een
begrensde patroonuitbreiding; er is geen algemene kaartinterpretatie gebouwd of bewezen.
Modeladvies bleef GPT-6.1 Sol / High; geen directe instellingsbediening beschikbaar,
dus niets gewijzigd. Handleiding: docs/ROLE_COVERAGE.md; controlebewijs:
docs/VALIDATION_PHASE8.md; compleet bestandenoverzicht: docs/FILES.md.

Stop na fase 8; fase 9 vereist een afzonderlijke startopdracht.

Fase 8 is geaccepteerd met "Fase 8 goed start 9"; daarmee is fase 9 gestart.

## Fase 9 — opgeleverd en geaccepteerd

Gestart op 22 oktober 2026 circa 13:06 Europe/Amsterdam. Advies: **GPT-6.1 Sol / High**
voor tekstinterpretatie en controle op onjuiste rolclaims; geen ondersteunde directe
chatinstellingsbediening gevonden, niets gewijzigd. Raming **45–90 actieve minuten**,
inclusief ontwerpen, bouwen, testen, schermcontrole en documenteren; bij complexe
tekstvarianten mogelijk 90–150. Geen vaste timer of gegarandeerde eindtijd.

Prioriteit: de gemeten geringe dekking verbeteren binnen een afgebakend bereik.
Ondersteun vaste, gerichte schade op creatures/planeswalkers en expliciete modale
keuzes zonder die als onvoorwaardelijke combinatie te presenteren. Bewaar hoeveelheid,
doelgroep, keuzeregels, herkende opties en niet beoordeelde tekst. Schade bewijst geen
dodelijkheid zonder speltoestand; modale aanwijzingen bewijzen geen gelijkwaardige
kaart. Beide leveren daarom nooit een sterke rolmatch op.

Acceptatie: meer praktische dekking dan de fase-8-baseline, geteste tegenvoorbeelden,
zichtbare waarschuwingen tot en met voorbeeld/opslag, en een vooraf gereserveerde
aparte evaluatie met eerlijk gepubliceerde uitkomsten. Parser en vergelijking worden
vóór inspectie van gereserveerde teksten bevroren. Dezelfde ontwikkelaar labelt,
geen onafhankelijke reviewer of representativiteitsclaim. De oude evaluatie wordt
regressiemateriaal; niet als nieuwe ongeziene meting presenteren.

Geen volledige spelregels, automatische deckgeneratie, synergie/winkans, externe
AI-API, nieuw formaat of Arena-accountkoppeling. Bestaande voorraad-, bron-, formaat-,
conflict- en idempotentiecontroles blijven gelden; geen schemawijziging voorzien.

- [x] Acceptatie fase 8, start fase 9, scope, modeladvies en raming vastleggen.
- [x] Nieuwe evaluatiegevallen vóór implementatie reserveren.
- [x] Herkenning, vergelijking, uitleg en versiebeheer uitbreiden.
- [x] Parser bevriezen, nieuwe evaluatie en praktijkdekking meten.
- [x] Volledige build/tests, schermen, native start en gegevensbehoud controleren.
- [x] Documentatie en oplevering afronden.

Opgeleverd als **v0.9.0**. Definitieve restore/Release-build met **0 waarschuwingen,
0 fouten** en **278 geslaagde tests** (28 Domain, 250 Application/integratie/UI),
0 mislukt/overgeslagen. Vaste gerichte schade bewaart hoeveelheid/doelen en krijgt
nooit een sterke match. Modale kaarten bewaren afzonderlijke opties, ook onbekende,
en de volledige keuze-/resttekst; zij blijven onvolledige aanwijzingen.

Dezelfde echte snapshot van 5.164 Standard-legale kaarten: **52 volledig**, **300
onvolledig**, **4.812 onbekend**, tegenover 43/207/4.914 in fase 8. De volledige gevallen
omvatten 9 schade-spells; binnen de aanwijzingen zitten 50 modale kaarten en 45
niet-modale schadegevallen. Volledige dekking is nog slechts circa 1,0%.
Families kunnen bij modale kaarten overlappen en mogen niet tot kaarttotalen worden opgeteld.

Nieuwe gereserveerde evaluatie: **1 volledig, 7 onvolledig, 22 onbekend**, tegenover
0/3/27 met de vóór implementatie vastgelegde v0.8-baseline. **13 van 21 relevante
kaarten gemist**, tegenover 18; enige familie herkend 14,3% → 38,1%. Geen verkeerde
families vastgesteld bij acht gevonden gevallen; voorwaarden zichtbaar behouden.
Geen onafhankelijke reviewer of representatieve steekproef, geen algemene precisie-
of meerlabel-recallclaim. Parser/vergelijking/rangschikking zijn vóór tekstinspectie
bevroren; labels vóór voorspellingen vastgelegd. Beide zijn achteraf ongewijzigd.

De oude fase-8-evaluatie is voortaan regressiemateriaal (0/9/21). De gekozen
ontwikkelset geeft 26 volledig en 4 onvolledig; de bestaande tien rangschikkingsscenario's
blijven 10/10 tegenover fase-6-baseline 3/10. Geen nieuwe ongeziene kwaliteitsmeting.

De echte Negate-proef vindt 64 kandidaten in circa 0,248 seconde. Een extra
Shock-proef kiest Lightning Strike en Abrade met expliciete schade-/modale waarschuwingen
en bewaart een nieuw deck zonder origineel of bezit te wijzigen. Herhaling is
idempotent; drie oudere toegepaste ontvangsten blijven bruikbaar. Vier nieuwe
schermbeelden zijn bekeken. Een bestaande UI-testrace is hersteld door via dezelfde
navigatieopdracht als de interface te vernieuwen, zonder productiewijziging/retry.

Native normale start **13:18 Europe/Amsterdam**, reagerend open gelaten. De bestaande
gevulde fase-8-proef is eveneens native gestart en normaal gesloten. Alle 13 tabellen
per database gelijk; de gevulde proef behoudt 2.003 bezitsregels, 8 decks, 21 deckregels
en 3 adviesontvangsten. Zes migraties en back-uplijsten ongewijzigd. Geen nieuw pakket,
secrets, externe AI-API, schema- of beveiligingswijziging.

Actieve uitvoering **circa 18–20 minuten** (13:06–circa 13:24), korter dan de raming
door hergebruik van catalogus en opslagcontroles en de beperkte tekstscope. Dit is
een uitbreiding van patronen, geen algemene semantische engine. Modeladvies bleef
GPT-6.1 Sol / High; geen ondersteunde chatbediening beschikbaar, instellingen niet gewijzigd.
Handleiding: docs/DAMAGE_AND_MODAL_ROLES.md; exact bewijs: docs/VALIDATION_PHASE9.md;
compleet bestandenoverzicht: docs/FILES.md.

Stop na fase 9; fase 10 vereist een afzonderlijke startopdracht.

Fase 9 is op 23 oktober 2026 geaccepteerd met "fase 9 goed gekeurd".
Er was tot dit moment geen totaal aantal vervolgfasen of vast v1.0-eindpunt afgesproken.
De gebruiker vraagt hoeveel fasen resteren; dit is geen startopdracht voor fase 10.

## Voorgesteld eindpunt v1.0 — nog niet gestart of goedgekeurd

Advies op 23 oktober 2026: nog vier afgebakende fasen, 10–13, voor een beperkte maar
bruikbare v1.0. Deze planning is een voorstel, geen al afgesproken totaal of garantie.
De oorspronkelijke scope definieerde geen eindpunt na fase 9. Voorkom eindeloos
uitbreiden van tekstpatronen zonder aantoonbaar gebruikersresultaat.

| Fase | Afgebakend resultaat | Model / reasoning | Actieve uitvoertijd |
| --- | --- | --- | --- |
| 10 | Gecontroleerde kaartpool voor één gekozen Standard-decktype, met vastgelegde verwachtingen en kwaliteitsgrenzen; geen volledige catalogusinterpretatie | GPT-6.1 Sol / High | 2–4 uur |
| 11 | Vanuit geregistreerd bezit complete decks voorstellen binnen dat decktype, inclusief mana, curve, aantallen, tekorten en uitlegbare keuzes | GPT-6.1 Sol / High | 3–6 uur |
| 12 | Praktijkcontrole met echte collectie/deckvoorbeelden, correcties, herstart-/gegevensbehoud, back-up/herstel en prestatietests | GPT-6.1 Sol / High | 2–4 uur |
| 13 | Versie 1.0 verpakken: Windows-installatie, upgradecontrole, startinstructies en eindcontrole | GPT-6.1 Sol / High | 1–3 uur |

Totaal indicatief **8–17 actieve uren**, inclusief bouwen, testen en documenteren,
exclusief wachttijd op gebruikersfeedback, een eventuele tweede beoordelaar of een
geschikte testmachine. Sol / High is advies voor algoritmes, gegevensbehoud en
zorgvuldige verificatie; geen wijziging van de ingestelde chatconfiguratie.
Bron modelmogelijkheden: https://developers.openai.com/api/docs/models/gpt-6.1-sol.

De voornaamste onzekerheden zijn de gekozen kaartpool, gewenste advieskwaliteit,
beschikbaarheid van echte voorbeelden en het verifiëren van installatie/upgrades.
Als kwaliteitsgrenzen niet worden gehaald wordt eerst de oorzaak gerapporteerd;
geen v1.0-label alleen omdat de vier nummers zijn afgewerkt.

Voorgesteld v1.0-eindcriterium: installeerbare lokale app die collectie bewaart,
Standard-decks importeert/controleert en binnen de expliciete kaartpool complete,
uitlegbare voorstellen maakt. Geen claim over competitieve winkans of brede kaartkennis.
Live Arena-sync, metagamedata, alle archetypen en extra formaten vallen buiten dit
eindpunt en zijn eventueel later afzonderlijke uitbreidingen. Fase 10 is hieronder gestart;
de volgende fasen vereisen elk een afzonderlijke startopdracht.

## Fase 10 — opgeleverd, beoordeling afwachten

Expliciet gestart op 23 oktober 2026 met "start fase 10", circa 09:20 Europe/Amsterdam.
Modeladvies **GPT-6.1 Sol / High**, voor zorgvuldige kaartbeoordeling, versiecontrole
en toetsbare advieskwaliteit. Geen ondersteunde directe chatbediening gevonden;
instellingen niet gewijzigd. Raming **2–4 actieve uren**, inclusief broncontrole,
implementatie, bouwen, testen, schermcontrole en documenteren. Grootste onzekerheden:
actuele kaarten/bronbeschikbaarheid en fouten in de inhoudelijke adviesgevallen.

Decktype: **mono-rood aggro, Arena Standard BO1**. Eén kleur beperkt de mana-onzekerheid.
Een gecontroleerde pool met minimaal twintig ondersteunde niet-landen en Mountain
beschrijft concrete deckfuncties, voorwaarden en beperkingen. Geen nieuwe algemene
Oracle-parser of claim dat alle Standard-kaarten worden begrepen. Controleer identiteit
en metadata exact; een wijziging verplicht tot nieuwe beoordeling. Gebruik de bestaande
bron-, ban- en formaatcontrole, geen afzonderlijk optimistisch legaliteitsvlaggetje.

De UI maakt de pool, bronstatus, eigendom, uitleg en beperkte alternatieven zichtbaar.
Dit is een lees-/advieswerkbank; voorraad is geregistreerd bezit, geen vrijgave of
reservering voor een compleet deck. De eerdere generieke adviezen blijven afzonderlijk.
Complete decks genereren is fase 11 en valt buiten deze startopdracht.

Acceptatiecriteria vóór implementatie:

- Minstens twintig ondersteunde niet-landen plus Mountain, met exacte bronmetadata,
  deckfunctie en expliciete voorwaarden; bewuste tegenvoorbeelden/uitsluitingen.
- Onbekende identiteit, gewijzigde metadata, geen actuele Arena-printing, niet-legaal
  of verlopen bron geeft geen poolaanbeveling. Geen koppeling uitsluitend op naam.
- Alleen geschikte, werkelijk bezeten kaarten uit dezelfde beoordeelde functie mogen
  als alternatief verschijnen; met verschillen en zonder gelijkwaardigheids-/winkansclaim.
- Vóór rangschikkingsimplementatie vaste inhoudelijke vergelijkingsgevallen vastleggen.
  Vergelijk op dezelfde kandidaatpool met de eerdere generieke volgorde; rapporteer
  de uitkomst en eventuele missers. Dezelfde ontwikkelaar beoordeelt: geen onafhankelijke
  of representatieve benchmark claimen en geen algemene precisiepercentages.
- Geen verlies van gebruikersdata, geen nieuwe schemawijziging voorzien; volledige
  build/tests, scherminspectie, echte catalogusproef en native start controleren.

- [x] Start, model, raming, decktype en acceptatiecriteria vastleggen.
- [x] Actuele bron en concrete kaartpool met adviesgevallen vastleggen.
- [x] Poolcontrole, beperkte rangschikking en zichtbare uitleg implementeren.
- [x] Inhoudelijke meting en veiligheids-/integratie-/UI-tests uitvoeren.
- [x] Native start, gegevensbehoud, documentatie en oplevering afronden.

Opgeleverd als **v0.10.0**. Definitieve restore/Release-build: **0 waarschuwingen,
0 fouten**; **303 geslaagde tests** (28 Domain, 275 Application/integratie/UI),
0 mislukt/overgeslagen. De nieuwe navigatiepagina toont poolfunctie, bronstatus,
voorraad, voorwaarden en beoordeelde alternatieven. De werkbank leest opnieuw vóór
zoeken, blokkeert bij gewijzigde/ontbrekende metadata of bronproblemen en voorkomt
advies bij een bronkaart die door vernieuwen uit het bezitfilter verdwijnt.

Pool **mono-red-bo1-v1/2026-10-23**: **26 ondersteunde niet-landen, Mountain en
3 bewuste uitsluitingen**. Exacte opgeslagen metadata, functie en voorwaarden;
geen algemene parseruitbreiding. Power/toughness zijn geen zelfstandige velden
en speltoestand/volledige mana/synergie worden niet gesimuleerd. Dezelfde ontwikkelaar
(Codex) beoordeelt, geen onafhankelijke menselijke review.

Actuele Scryfall-bron online verwerkt: **2026-10-22T21:05:50.017Z**, 21.647
Arena-printings en 16.415 definities; tweede online controle AlreadyCurrent=true.
De geïsoleerde echte proef bevestigt **27/27 geschikte ondersteunde poolkaarten** en
drie geblokkeerde uitsluitingen, met BO1-bancontrole. Slickshot Show-Off krijgt
Emberheart Challenger en Fugitive Codebreaker uit gesimuleerd bezit, andere functies
niet. Lezen circa 0,114 seconde, piekgeheugen circa 95,2 MiB, resultaat gelijk na
provider-/verbindingherstart; bestaand deck en bezit onveranderd.

Pool en twintig inhoudelijke referentiegevallen zijn vóór rangschikkingsimplementatie
vastgelegd en daarna bytegewijs gelijk gebleven. Gewenste eerste keuze **5/20 generiek
→ 20/20 pooladvies**. Dit meet filtering op functie én rangschikking, met gesimuleerd
bezit/legaliteit en bewust verschillende geschiktheidsfilters. Geen onafhankelijke,
ongeziene of representatieve praktijkscore; geen algemene precisie of win-rateclaim.
De algemene classifier en generieke nieuwe-deckopslag blijven inhoudelijk gelijk.

Vier definitieve schermbeelden zijn bekeken. Native v0.10.0 start op de normale
database om **09:48 Europe/Amsterdam**, reagerend open gelaten; de bestaande gevulde
fase-9-proef is eveneens gestart en normaal gesloten. Alle kolommen van 13 tabellen
per database gelijk. De gevulde proef behoudt **2.005 bezitsregels, 12 decks,
31 deckregels en 5 ontvangsten**. Zes migraties en back-uplijsten ongewijzigd;
geen schemawijziging, nieuw pakket, secrets, externe AI-API of beveiligingswijziging.

Actieve uitvoering **circa 35–40 minuten** (09:20–circa 09:58), korter dan de
oorspronkelijke 2–4 uur door hergebruik van bestaande bron-/regel-/opslagcontroles
en de vaste pool van dertig kaarten. Bij de voortgangscontrole is de resterende
raming bijgesteld naar 30–60 minuten; ook die was ruim. Er is geen brede semantische
engine, gameplaytest of complete deckgenerator gebouwd om de tijd korter te maken.
Modeladvies bleef GPT-6.1 Sol / High; geen ondersteunde directe chatbediening,
instellingen niet gewijzigd. Gebruik: docs/MONO_RED_POOL.md; controlebewijs:
docs/VALIDATION_PHASE10.md; volledig bestandenoverzicht: docs/FILES.md.

Fase 10 is geaccepteerd op 23 oktober 2026 met "fase 10 akkoord start fase 11".

## Fase 11 — complete begrensde mono-rode BO1-voorstellen (geaccepteerd)

Start 23 oktober 2026, 10:34 Europe/Amsterdam. Advies GPT-6.1 Sol / High: selectie,
voorraadverdeling en transactionele opslag vragen zorgvuldige samenhang. Geen ondersteunde
modelbediening beschikbaar; chatinstellingen niet gewijzigd. Raming 3–6 actieve uren,
inclusief bouwen, testen, documenteren. Onzekerheid: bruikbaarheid bij onvolledige collectie.

Acceptatiecriteria vastgelegd vóór implementatie (zie docs/DECK_GENERATION.md):

- Exact 60 mainboard, geen sideboard/commander, BO1-controle geslaagd. Vast ontwikkelplan:
  22 Mountains, 12 vroege creatures, 4 spell-aanvallers, 4 pingers/enters-burn-creatures,
  8 burn, 2 creature-interactie, 6 combat-spreuken en 2 handverversers.
- Minimaal 12 niet-landen met gedrukte manawaarde 1; gemiddelde niet-land-MV maximaal 2.
  Gedrukte kosten, geen simulatie van kicker, plot, discard of spelsterkte. Alle voorwaarden tonen.
- Standaard alleen geregistreerd actueel bezit. Geen fictieve onbeperkte Mountains.
  Aparte expliciete planningsmodus mag tekorten bevatten, herkenbaar vóór opslag.
- Maximaal 4 per Oracle-ID over printings; exacte printingtoewijzing uit bezit eerst,
  daarna ontbrekende exemplaren. Voorraad wordt niet verbruikt of voor andere decks gereserveerd.
- Ontbrekende bron, veroudering, gewijzigde metadata en illegale kaarten blokkeren zoals fase 10.
  Geen opvulling met willekeurige kaarten als het vaste plan niet haalbaar is.
- Deterministisch; tekort per functie én per voorgestelde printing zichtbaar, mana/curve/uitleg.
  Eén voorstel per berekening; geen optimaliteits-, meta- of win-rateclaim.
- Voorbeeld vóór opslag, eigen migratie/ontvangst, hercontrole op bron/tijd/voorraadversies,
  idempotent en atomair een nieuw deck, geen bestaande decks/bezit wijzigen.
- Vooraf gekozen controles: volledige voorraad; geen Mountains; te weinig creatures;
  alleen verkeerde functies; verdeeld bezit over herdrukken; curve met weinig 1-drops;
  expliciete planning met tekorten; gewijzigde metadata/bron; verlopen voorbeeld;
  dubbele toepassing, herstart, annulering en SQL-fout na schrijven met rollback.
- Volledige Verify met screenshots, beelden bekijken, echte catalogus met gesimuleerd bezit,
  native start en vóór/na gebruikersgegevens inclusief alle kolommen en migratieback-up.

- [x] Start, scope, modeladvies en acceptatiecriteria vastleggen.
- [x] Generator, opslag, migratie en UI implementeren.
- [x] Automatische tests en echte catalogusproef uitvoeren.
- [x] Schermen, native start, gegevensbehoud en documentatie afronden.

Opgeleverd als **v0.11.0**: restore/Release-build met **0 waarschuwingen, 0 fouten**;
**343 tests geslaagd** (33 Domain, 310 Application/integratie/UI), nul mislukt of
overgeslagen. Veertig nieuwe tests controleren template, voorraad/printings, curve,
expliciete planning, actualiteit, versies, herstart, dubbele toepassing, rollback,
migratie en daadwerkelijke schermbindingen. Definitief uitgevoerd:
`./scripts/Verify.ps1 -Screenshots -ReportPrefix phase11-delivery-final`.

De generator gebruikt de ongewijzigde fase-10-pool met een vast plan van 60 kaarten.
Bezitsmodus verzint geen exemplaren; planning is een expliciete keuze en toont tekorten.
Voorkeur: bezit, poolvoorkeur, gedrukte MV, naam/identiteit; curveherstel blijft binnen
de functie. Exacte printingallocatie en één vierkopieënlimiet per Oracle-ID. De eigen
DeckGenerationPlans-ontvangst en zevende migratie bewaren een gecontroleerde voorbereiding.
Opslaan leest opnieuw, maakt atomair één nieuw BO1-deck en behoudt bestaande data.
Geen voorraadverbruik of reservering voor andere decks; geen algemene parseruitbreiding.

De echte Scryfall-catalogus van **2026-10-22T21:05:50.017Z** is tweemaal online als
actueel bevestigd. Met gesimuleerd bezit in een afzonderlijke database ontstaat een
BO1-gecontroleerd voorstel met **22 Mountains, 20 creatures en 18 overige spreuken**,
**nul tekorten**, 24 gedrukte 1-drops en gemiddelde niet-land-MV 1,3684. Berekenen circa
**0,045 seconde**, piekgeheugen gehele proef circa **112,5 MiB**. Herstart gelijk,
oude preview na voorraadwijziging geweigerd, dubbel toepassen dezelfde deck-ID en
export gemaakt. Geen Arena-games of import in de echte Arena-client getest.

Gewijzigde schermen zijn gerenderd en bekeken, inclusief zichtbare preview-/opslagknop,
ontvangst en volledige lijst op de normale vensterhoogte. Een verwarrende tekst bij
een lege deellijst is vóór de definitieve volledige verificatie verduidelijkt.
Native start om **10:58 Europe/Amsterdam**: reagerende v0.11.0 op de normale database
open gelaten; de bestaande gevulde proef normaal gesloten. Alle kolommen van dertien
bestaande tabellen per database gelijk; gevulde proef behoudt 2.005 bezitsregels,
12 decks, 31 deckregels en vijf oude adviesontvangsten. Nieuwe tabel leeg; zes oude
migraties plus de zevende. Precies één nieuwe upgradeback-up per database, inhoudelijk
gelijk aan de vóórtoestand; eerdere back-ups ongewijzigd.

Startraming 3–6 actieve uren; na eerste succesvolle praktijkproef bijgesteld naar
20–40 minuten resterend. Werkelijke actieve uitvoering circa **30–35 minuten**,
10:34–circa 11:05 Europe/Amsterdam. De oorspronkelijke raming was te ruim voor deze
vaste template met bestaande bron-/voorraad-/regel-/UI-infrastructuur. Geen gameplay-
of brede combinatorische optimalisatie gebouwd. Modeladvies bleef GPT-6.1 Sol / High;
geen ondersteunde directe bediening, chatinstellingen niet gewijzigd. Geen nieuw
NuGet-pakket, secrets, externe AI-API of beveiligingswijziging.

Gebruik: docs/DECK_GENERATION.md; controlebewijs: docs/VALIDATION_PHASE11.md;
bestandenoverzicht: docs/FILES.md. Belangrijkste grens: het plan is technisch compleet
en controleerbaar, maar strategische kwaliteit is nog niet gemeten. Eén kleur en vaste
functieverdeling sluiten veel andere mogelijke decks uit.

Stop na fase 11 en vraag beoordeling; fase 12 is niet gestart.

Fase 11 is geaccepteerd met "goedgekeurd start fase 12" op 23 oktober 2026.

## Fase 12 — betrouwbaarheid, praktijkcontrole en back-up/herstel (opgeleverd, beoordeling afwachten)

Start circa 11:05 Europe/Amsterdam, 23 oktober 2026. Advies GPT-6.1 Sol / High voor
gegevensbehoud, herstarts en herstel na fouten. Geen ondersteunde instellingsbediening;
chatinstellingen niet gewijzigd. Raming 1–3 actieve uren inclusief bouwen/tests/documentatie,
bijgesteld ten opzichte van het oude globale voorstel op basis van eerdere uitvoertijden.
Grootste onzekerheid: veilig herstel bij onderbreking en schema-afwijkingen.
De gebruiker bevestigt: geen echte export; gebruik gesimuleerde voorbeelden.

Acceptatiecriteria vóór implementatie:

- Handmatig een consistente SQLite-back-up maken tijdens normaal gebruik, onafhankelijk
  van een schema-upgrade. Alleen voltooide gecontroleerde kopieën zichtbaar; geen stille
  overschrijving of automatische verwijdering van oudere back-ups.
- Herstelbron controleren op integriteit, referentiële integriteit en exact bekend schema/
  migratieprefix. Onbekend/toekomstig schema, vreemde triggers/indexen, beschadigde of te
  grote bestanden weigeren. Brondata niet als gebruikersdatabase openen voor wijzigingen.
- Herstelvoorbeeld met aantallen en expliciete waarschuwing dat alle lokale databasegegevens
  worden teruggezet. Alleen na afzonderlijke UI-bevestiging plannen voor de volgende start.
- Herstel gebruikt de gecontroleerde, gehashte kopie. Bij appstart vóór DI/gebruikersacties:
  exclusieve appdatamap, nieuwe veiligheidsback-up, transactionele SQLite-backup-API naar
  de bestaande database. Geen rename van een geopende database of losse WAL-verwijdering.
- Bij onderbreking/fout geen half gekopieerde database, geen stil overslaan van herstel;
  herhaalbare startstatus, veiligheidskopie behouden. Oud bekend schema daarna migreren.
- UI: back-uplijst, bron kiezen, voorbeeld, expliciete herstelbevestiging, annuleren van
  gepland herstel en duidelijke status na herstart. Geen herstel van echte gebruikersdata
  tijdens ontwikkeling; echte normale start en vóór/na audit wel uitvoeren.
- Tests: volledig gegevensrondje, WAL, oudere migratie, vreemde/corrupte bron, TOCTOU,
  annulering, fout/onderbreking rond herstel, dubbele start en volle historie behouden.
- Praktijkproef met actuele echte catalogus plus gesimuleerd bezit/decks, herstart/herstel,
  herhaalde selectie en prestatiemetingen; geen gameplay- of echte accountclaim.
- Volledige Verify -Screenshots, renders bekijken, native start en alle bestaande tabellen
  vóór/na hashen. Geen nieuwe pakketten of schemawijziging voorzien.

- [x] Start, scope en acceptatiecriteria vastleggen.
- [x] Back-up/herstel en UI implementeren.
- [x] Geïsoleerde automatische en praktijkcontroles uitvoeren.
- [x] Native start, gegevensbehoud, documentatie en oplevering afronden.

Opgeleverd als **v0.12.0**. Definitieve restore/Release-build: **0 waarschuwingen,
0 fouten**. **362 tests geslaagd**: 33 Domain, 329 Application/integratie/UI;
19 nieuwe controles, geen mislukt of overgeslagen. Uitgevoerd:
`./scripts/Verify.ps1 -Screenshots -ReportPrefix phase12-delivery`.

Instellingen biedt nu een handmatige consistente SQLite-back-up, bestandskeuze en
gecontroleerd herstelvoorbeeld, afzonderlijke bevestiging, planning bij volgende
appstart en annulering vóór toepassen. Integriteit, foreign keys en exact schema uit
bekende migraties worden gecontroleerd. Eigen reviewkopie met hash; eerste veiligheids-
kopie en hash blijven bij retry behouden. Scheduled/Applying/Applied beheersen de
startvolgorde vóór normaal databasegebruik. Eén desktopapp per gegevensmap; geen
hernoemen van actieve databases of verwijderen van losse WAL-bestanden.

Praktijkproef met echte Scryfall-bron **2026-10-23T09:05:40.788Z**, 21.673 Arena-
printings na 26 nieuwe uitgaven, en gesimuleerd bezit/decks. Back-up 35,3 MiB;
maken circa **0,68 s**, inspecteren **0,66 s**, herstellen inclusief initialiseren
**1,49 s**. Vijftien herhaalde berekeningen: mediaan **7,35 ms**, bereik **6,72–25,07 ms**.
Piekgeheugen gehele proef inclusief online import circa **318,2 MiB**. Na herstel alle
vijftien applicatietabellen plus migratiehistorie gelijk, veiligheidskopie bevat latere
wijzigingen en hetzelfde volledige voorstel komt terug. Een aparte gevulde kopie met
2.005 importregels en oude adviesontvangsten is eveneens volledig teruggezet.

Werkelijke native startup past geïsoleerd herstel toe vóór initialisatie, behoudt
alle vijftien tabellen en blokkeert zichtbaar een tweede desktopinstantie. Beide
proefvensters normaal gesloten. Normale v0.12.0 start rond **11:25 Europe/Amsterdam**,
reagerend open gelaten. De bestaande gevulde proef is apart zonder herstel gestart
en gesloten. Alle kolommen van vijftien tabellen op beide bestaande databases gelijk;
zeven migraties en bestaande back-ups ongewijzigd. Gewijzigde schermen bekeken;
startfouten tonen geen ongeïnitialiseerde dashboardnullen meer.

Werkelijke uitvoering circa **30 actieve minuten**, 11:05–circa 11:35
Europe/Amsterdam. Startraming 1–3 uur was ruim door hergebruik van bestaande opslag,
bron-/regelcontroles en SQLite-backup-API. Modeladvies GPT-6.1 Sol / High; geen
ondersteunde directe bediening, instellingen niet gewijzigd. Geen nieuwe migratie,
NuGet-pakket, secrets, externe AI-API of beveiligingswijziging.

Beperkingen: geen echte collectie-export beschikbaar (door gebruiker bevestigd), geen
Arena-games of algemene prestatieclaim; geen echte stroomuitval/schijfdefecttest.
De huidige database moet leesbaar zijn voor een geldige veiligheidskopie; geen
automatische reparatie van fysieke corruptie. Geen periodieke/cloudback-up, encryptie
of opschoning; cache/logs vallen buiten databaseherstel. Gebruik: docs/BACKUP_RESTORE.md;
bewijs: docs/VALIDATION_PHASE12.md; volledig overzicht: docs/FILES.md.

Stop na fase 12; fase 13 vereist een afzonderlijke startopdracht.

Fase 12 geaccepteerd op 23 oktober 2026 met "fase 12 akkoord start fase 13".

## Fase 13 — beperkte Windows v1.0-distributie (geaccepteerd en afgesloten)

Start 23 oktober 2026 circa 11:37 Europe/Amsterdam. Advies GPT-6.1 Sol / High voor
installatie-/upgradegedrag en gegevensbehoud. Instellingen niet gewijzigd: geen
ondersteunde bediening. Raming 1–3 actieve uren inclusief bouwen, installeren, testen
en documenteren. Onzekerheden: verpakkingsgereedschap en ongetekende distributie.

Vooraf vastgelegde acceptatiecriteria:
- Self-contained Windows x64-publicatie zonder trimming/single-file-extractie, runtime
  inbegrepen. Portable zip én per-user installer met vaste app-identiteit en uninstall.
- Reproduceerbaar bouwscript, SHA-256-manifest, expliciete ongetekende status en grenzen.
- Installatie zonder administratorplicht; programmacode buiten de databasegegevensmap.
- Installer wist geen gebruikersdata; upgrade/herinstallatie en verwijderen behouden
  database, back-ups en instellingen. Geen automatische appstart bij stille installatie.
- Werkelijk installatie, reageren van geïnstalleerde app, herinstallatie, uninstall,
  herinstallatie en opnieuw starten controleren. Gevulde oude database geïsoleerd toetsen.
- Volledige Verify -Screenshots, gewijzigde renders bekijken en normale data auditen.
- Geen brede strategische kwaliteit of schone-machinecompatibiliteit claimen zonder bewijs.
  Geen extern publiceren, geen beveiligingsbeleid wijzigen zonder concrete noodzaak.
Stop na fase 13; aanvullende ontwikkeling vereist een nieuwe opdracht.

Opgeleverd als **v1.0.0**. Restore/Release-build: **0 waarschuwingen, 0 fouten**;
**362 tests geslaagd** (33 Domain, 329 Application/integratie/UI), geen overgeslagen
of mislukt. Het gewijzigde versiescherm is gerenderd en bekeken. Geen schema- of
algoritmewijziging. Zes solutionprojecten behouden, aparte read-only audittool bij de
installatietest. Geen nieuw apppakket, secrets, externe publicatie of beleidswijziging.

Zelfstandige win-x64-publicatie met runtime **10.0.12**, zonder trimming/single-file.
Per-user Inno Setup-installer **54.815.358 bytes** en portable zip **79.138.316 bytes**
onder artifacts/release/1.0.0, met SHA256SUMS.txt en release-manifest.json. Setup is
ongetekend. Inno Setup 7.1.0 is van de officiële bron opgehaald, digest gecontroleerd
en met geldige Authenticode-handtekening aangetroffen; de compilerhandtekening maakt
onze installer niet ondertekend. Geen certificaat beschikbaar, instellingen niet verlaagd.

Portable zip echt uitgepakt: alle 249 bestanden gelijk, reagerende app, geïnitialiseerde
lege testdatabase en coreclr.dll geladen uit eigen map. Werkelijke installerproef:
0.99.0-versiefixture → 1.0.0, verwijderen, herinstalleren, starten, Startmenu-target en
afsluitend verwijderen. Gebruikersdata en upgradeback-up behouden. De fixture is uit
huidige bron herbouwd en geen historische release; de gevulde database is wel werkelijk
van zes naar zeven migraties gegaan met behoud van bestaande kolommen/IDs/hoeveelheden.
De testregistratie/shortcut wordt verwijderd; normale gebruikersdata blijft buiten de proef.

Normale v1.0.0-app rond **11:45 Europe/Amsterdam** reagerend open gelaten. Bestaande
gevulde proef gestart en normaal gesloten. Vijftien tabellen en zeven migraties op
beide bestaande databases ongewijzigd. De installatietest is zelfstandig herhaalbaar
met expliciete fixture en nieuwe testmap; de audittool wordt uit meegeleverde bron gebouwd.

Advies GPT-6.1 Sol / High; geen ondersteunde directe instellingsbediening, niets gewijzigd.
Actieve uitvoering circa **15 minuten**, 11:37–circa 11:52. De raming 1–3 uur was
ruim door bestaande verificatie-/opslaginfrastructuur en direct bruikbare installercompiler.
Geen schone tweede pc, standaardgebruikeraccount, Windows/ARM-matrix, echte Arena-export
of gameplaytest. Bron-/regelverval en ontbrekende fysieke databasereparatie blijven
bekende grenzen. Gebruik: docs/RELEASE_GUIDE.md; bewijs: docs/VALIDATION_PHASE13.md;
volledig overzicht: docs/FILES.md. De vier voorgestelde vervolgfases 10–13 zijn uitgevoerd;
dit is geen opdracht voor een nieuwe fase of blijvend onderhoud.

De gebruiker heeft de v1.0.0-oplevering op **23 oktober 2026** expliciet goedgekeurd
met "goedgekeurd". Fase 13 en de voorgestelde oplevering zijn daarmee afgesloten.
Geen volgende fase gestart; bekende beperkingen en onderhoudsgrenzen blijven gelden.

## Aanvulling na acceptatie — Arena-exporthandleiding

Gebruikersopdracht: "maak een handleiding hoe ik de kaarten kan exporteren van het
online spel zodat ik dit in de applicatie kan gebruiken". Geen nieuwe projectfase.
docs/ARENA_EXPORT_HANDLEIDING.md beschrijft tracker-export, collectie tegenover deck,
handmatige route en importcontrole. De actuele trackerbron is gelezen: exportregels
kunnen meerdere Arena-ID's samenvoegen; die aantallen mogen niet worden verdeeld op gok.
scripts/Convert-TappsCollection.ps1 zet alleen ondubbelzinnige JSON-regels om en weigert
meerdere/dubbele ID's, ongeldige aantallen/totaaltellers en bestaande uitvoerbestanden.
De echte Arena-exportketen is niet uitgevoerd; voorbeelden voor de converter zijn synthetisch.
Geen applicatiebinary of goedgekeurd releasepakket gewijzigd.

## Fase 14 — fysiek Magic, scan-CSV en afbeeldingen (gestart)

Expliciete opdracht 27 oktober 2026. Advies GPT-6.1 Sol / High; raming 2–4 actieve uren
voor broncontrole, model/migratie, UI, testen, documenteren en verpakken. Geen ondersteunde
modelbediening beschikbaar; instellingen niet gewijzigd. Grootste onzekerheden: exacte
Conflux-lijsten/printings en veilig behoud van bestaande Arena-data. Start circa 21:41
Europe/Amsterdam. Productfocus wordt fysiek Magic; de eerdere app blijft historisch bewaard.

Acceptatiecriteria vóór implementatie:
- Fysieke kaarten/bezit/decks in eigen tabellen, nooit bestaand Arena-bezit herlabelen.
- ManaBox CSV met quotes/BOM/regels, exacte Scryfall-ID of set+nummer+taal; geen gokken
  op naam. Review vóór toepassen, idempotentie, voorraadversies, historie/herstel en atomair
  schrijven. Binder/list/deck-rijen onderscheiden; plannen tellen niet als fysiek bezit.
  Finish, taal en conditie behouden; geen proxy als echt bezit meetellen.
- Scryfall paper-printings automatisch ophalen voor import en online zoeken; broncache,
  aanvraagbegrenzing en veilige gecachete voor-/achterzijde-afbeeldingen met foutstatus.
- Reproduceerbare Conflux-demo uit gepubliceerde productlijsten, eenmaal als aangenomen
  bezit en twee voorbeelddecks; géén verzonnen boosterinhoud of extra kopieën tot 60.
- Papierprofielen: beschrijvend casual 60 en Standard met bron-/regelactualiteit; geen
  Arena-bans of Arena-printingeis op fysieke kaarten. Historische 41-kaartlijsten tonen
  hun omvang eerlijk. Mechanieken/synergie beschrijvend, geen speelsterkteclaim.
- Primaire schermen fysiek: collectie/catalogus, CSV-review, decks/analyse/alternatieven,
  instellingen/back-up. Oude Arena-werkruimte herkenbaar als archief, niet als standaard.
- Migratie/back-up behoudt alle bestaande gegevens; volledige Verify -Screenshots,
  gewijzigde beelden bekijken, echte Scryfall-proef met 82 aangenomen productkaarten,
  herstart, gegevensaudit, bijgewerkte installer/portable en handleiding.
Stop na fase 14 en vraag beoordeling.

Aanvullende acceptatie vóór implementatie van de fysieke voorstelroute (22:25):
"Overige eisen blijven" vraagt ook een bruikbare fysieke vervanger voor de eerdere
begrensde deckgenerator. Lever uitsluitend een reproduceerbaar Casual60-Conflux-plan:
36 niet-landen + 24 basislanden, exact gecontroleerde Oracle-pool/metadata, voorraad
gegroepeerd over uitgaven en maximaal vier niet-basics. Bezitmodus standaard; expliciete
planning mag tekorten tonen. Geen Standard-, gameplay-, algemeen synergie- of optimaal
manabasisoordeel. Voorbeeld eerst controleren, fingerprints en opslag als nieuw deck.
Bij onvoldoende geschikte voorraad volledig blokkeren; geen kaarten verzinnen of als
bezit opslaan. Test totalen, Oracle-limieten, voorraad, planning en gewijzigde metadata.
Laatste extra indicatie 20–40 actieve minuten; modeladvies ongewijzigd.

Uitbreiding expliciet opgedragen op 27 oktober, circa 22:36: Commander-generator en
FINAL FANTASY Starter Kit toevoegen aan het aangenomen testbezit. Advies GPT-6.1 Sol /
High, 1–2 extra actieve uren voor broncontrole, opslag/UI, tests, netwerkproef en nieuw
pakket. Geen ondersteunde modelbediening; niets gewijzigd.

Acceptatie vóór implementatie:
- Twee Starter Kit-decks volgens Wizards/MTGJSON, elk 60 kaarten met juiste uitgaven/
  foil. Een eigen seedmarker voegt alleen deze 120 kaarten toe aan bestaande demo;
  Conflux/echte CSV en gewijzigde gegevens nooit opnieuw aanmaken/overschrijven.
- Commander: precies 100 inclusief één ondersteunde commander, 99 library, singleton
  over Oracle/printings (basic lands onbeperkt), kleuridentiteit inclusief dubbelzijdig,
  basic-landtypen, papieren Commander-legaliteit en actuele bronnen; geen sideboard.
  Regels CR 903.3/903.4/903.5, geldig 25 september 2026, gelezen 27 oktober. Ook
  legendary Vehicle en Spacecraft met P/T kunnen volgens actuele regels commander zijn.
  Geen partner/background-paren, variabele kleurkeuze, brackets/cEDH- of gameplayclaim.
- Eén vaste generatorconstructie: commander + 62 verschillende niet-landen + 37
  basislanden. Bezitmodus standaard, volledig blokkeren bij onvoldoende geschikte
  voorraad. Planning expliciet, gerichte Scryfall-aanvulling met papieren/Oracle-/kleur- en
  broncontrole. Geen nieuwe kaarten aan bezit toevoegen. Volledige lijst en tekorten
  vóór bevestiging, hercontrole vóór opslag als nieuw deck, herstart/back-up behouden.
- Starter Kit is geen Commander-product. Meet wat werkelijk uit 202 aangenomen kaarten
  mogelijk is; claim nooit een bezit-Commander wanneer legale singletonvoorraad ontbreekt.
- Automatische regels-, voorraad-, generator-, CSV-/UI-tests en echte Scryfall-proef.
  Complete build/Verify -Screenshots, beelden bekijken, definitief pakket opnieuw testen.

## Fase 14 — opgeleverd ter beoordeling, 27 oktober 2026

v1.1.0 levert fysieke opslag/collectie/decks, ManaBox-CSV-review met herstel, on-demand
Scryfall en afbeeldingen, aparte bewaarde Arena-data, Conflux- en FINAL FANTASY Starter
Kit-testbezit. 202 speelkaarten, 109 uitgaven, 118 posities, vier productdecks; tokens
apart vermeld. Twee fysieke generatorroutes: begrensd Conflux-Casual60 en Commander
(één commander, 62 singleton niet-landen, 37 basics), bezitmodus standaard/planning expliciet.
Geen werkelijke telefoonscan of bewezen speelsterkte/algemene synergie claimen.

422 tests geslaagd (36 Domain, 386 Application/SQLite/UI), volledige build zonder fouten/
waarschuwingen en gerenderde screenshots bekeken. Aangescherpte exacte image-ID-test
ook los geslaagd. Echte Scryfall-proef: vier productdecks/202 kaarten, 60-kaart bezitvoorstel,
100-kaart Cloud-planning met 32 ontbrekende exemplaren van 29 kaarten. Cloud-bezitmodus
blokkeert terecht bij 34/62 geschikte unieke niet-landen. Opslag, herstart, back-up/herstel
van zeven decks en import-undo bevestigd zonder bezit te verzinnen.

Native appstart op normale en gevulde bestaande gegevensmap geslaagd. Eerste migratie
behield vijftien Arena-tabellen; Starter Kit-toevoeging behield iedere oorspronkelijke
rij/kolom van alle twintig applicatietabellen (38.204 en 56.656 rijen). Nu acht migraties;
geen extra migratie voor de latere Commander/Starter Kit-uitbreiding nodig. Consistente
voorafkopieën/audits bewaard. Geen beveiligingsbeleid gewijzigd. Windows-helper kon het
venster niet koppelen; geen handmatige native klik-/screenshotcontrole claimen.

Definitief pakket met eigen runtime, handleidingen en drie voorbeeld-CSV's. Werkelijke
v1.0.0 → v1.1.0-installatie/upgrade/uninstall/herinstallatie op geïsoleerde paden geslaagd,
gegevens en upgradeback-up behouden, complete fysieke werkruimte gecontroleerd. Hashes
geverifieerd, installer NotSigned. Bewijs: docs/VALIDATION_PHASE14.md en artifacts/phase14-*
rapporten. Geen schone tweede pc, ARM, signing of echte telefoonexportketen uitgevoerd.

Werkvenster circa 21:41–23:25 Europe/Amsterdam met een korte gebruikersonderbreking;
actieve uitvoering ongeveer 1 uur 40 minuten. Oorspronkelijke raming 2–4 uur en later
1–2 extra uur was ruimer door hergebruik van bestaande opslag-/test-/distributiebasis.
Modeladvies GPT-6.1 Sol / High; geen ondersteunde instellingsbediening, niets gewijzigd.
Fase 14 is gereed voor gebruikersbeoordeling; geen volgende fase gestart.

## Fase 15 — Magic-thema, filters, wizard en veilig legen (gestart)

28 oktober 2026, expliciete opdracht. Advies GPT-6.1 Sol / High; 2–4 actieve uren voor
logo-/icoonverwerking, UI, filters, wizard, opslagveiligheid, bouwen/tests/documentatie
zoals gebruikelijk. Geen ondersteunde modelinstellingsbediening; niets gewijzigd.

Acceptatie vóór implementatie:
- Geen Arena-navigatie/schermen/opdrachten in de actieve app. Historische tabellen en
  migraties behouden voor veilige upgrades/back-ups; geen stille gegevensverwijdering.
- Aangeleverd logo exact als merkasset, oranje/goud/zwart Magic-thema, herkenbaar app-
  en Windows-programmaicoon. Technische PNG/ICO-conversie, geen generatieve logohertekening.
- Combineerbare collectiefilters op geprinte kaartkleur, type, set/serie en expliciet
  begrensde synergie-aanwijzingen; duidelijke aantallen en geen verzonnen combo-kennis.
- Wizard met stappen voor formaat, kleuridentiteit, thema, speelstijl, bezit/planning,
  commander indien nodig, review en bevestigde opslag als nieuw deck. Kleur/voorraad/
  formaat harde grenzen; thema/stijl uitlegbare voorkeuren, geen winrate-/optimaliteitsclaim.
- Database legen is alleen een aangeboden gebruikersactie; niet uitvoeren op normale
  gebruikersdata. Preview, expliciet WISSEN, verplichte consistente reservekopie vóór
  delete, versie-/vingerafdrukcontrole en één transactie. Alle data inclusief oudere
  gegevens legen, schema/migraties en back-ups behouden. Voorbeelden blijven na legen
  ook na herstart uitgeschakeld. Fouten/annulering mogen geen halflege database opleveren.
- Uitgevoerde unit-/SQLite-/UI-tests, Verify -Screenshots, afbeeldingen bekijken,
  echte netwerkproef waar passend, native start en distributie v1.2.0 met upgradebewijs.
Stop na fase 15 voor beoordeling; geen volgende fase starten.

### Fase 15 — gecontroleerde implementatie

Volledige Verify -Screenshots geslaagd: 443 tests (36 Domain, 407 Application/SQLite/UI),
geen bouwfouten/waarschuwingen. Aangeleverd logo exact bewaard (SHA-256 identiek), Magic-
layout en icoon; actieve Arena-desktopcode verhuisd naar historische regressietests.
Collectie combineert geprinte kleur, zeven thema-aanwijzingen, kaarttype en setnaam/code.
Wizard biedt vijf stappen: formaat, kleuridentiteit, thema/stijl, bezit/planning en
commander indien van toepassing, review en nieuwe-deckopslag met hercontrole.

Werkelijke Scryfall-proef in geïsoleerde gegevensmap: WR Equipment uit bezit 60 kaarten
zonder tekorten, Standard-planning 60 met elf tekortregels, Cloud Commander-planning 100
met 57 tekortregels; geen overige ondersteunde regelproblemen. Drie nieuwe decks na
herstart beschikbaar, bezit blijft 202. Alleen de testdatabase geleegd; na herstart
leeg en demo uit, daarna volledig hersteld naar 202 kaarten en zeven decks.

Reset heeft actuele preview/exact WISSEN, verplichte consistente kopie met integriteits-,
FK- en exacte schemacontrole, twee data-afdrukcontroles en één delete-transactie.
Fout/annulering na delete-SQL rolt alles terug. Bronafwijking blokkeert vóór verwijderen.
Nieuw: migratie negen met ExamplesEnabled, default true en na reset false. Beide bestaande
normale/gevulde databases: alle originele waarden van twintig tabellen behouden (38.372/
56.824 rijen). Native start/respons/normale afsluiting bevestigd, screenshots bekeken.

Documentatie: MAGIC_WIZARD_HANDLEIDING, VALIDATION_PHASE15, README, installatiegids,
architectuur, ADR-059/060/061, AGENTS en bestandenlijst bijgewerkt. Definitieve distributie-
controle volgt in de afsluitende vermelding hieronder; geen succes vooruitlopend claimen.

### Fase 15 — definitieve oplevering

28 oktober 2026, v1.2.0. Definitieve self-contained Windows x64-installer en portable ZIP
gebouwd. Authentieke v1.1.0 → v1.2.0-installatie/upgrade/uninstall/herinstallatie op
geïsoleerde paden geslaagd; eigen runtime geladen, Startmenu-doel gecontroleerd en alle
gegevens plus migratieback-up behouden. Drie upgradeback-ups exact tegen alle twintig
vooraf vastgelegde tabellen gecontroleerd. Definitieve portable start/respons/afsluiting
op beide bestaande gegevensmappen geslaagd. Exe-/installericoon uitgelezen en bekeken;
SHA-256 opnieuw gecontroleerd; ongetekend. Geen beveiligingsbeleid gewijzigd.

Bronvalidatie en aanvullend pakketbewijs bijgewerkt. Ingesloten validatiekopie dateert
van vóór installatieproef; VALIDATIE-NA-PAKKET naast de release vermeldt het eindresultaat.
Bestandswijzigingen staan in docs/CHANGES_PHASE15.md. Geen normale gebruikersdatabase
geleegd, geen echte telefoonscan/gameplay/schone tweede machine/ARM/signing claimen.

Werkvenster circa 07:14–08:56 Europe/Amsterdam, ongeveer 1 uur 45 minuten actieve uitvoering.
De eerdere raming 2–4 uur was ruimer; bestaande fysieke opslag en distributie konden
worden hergebruikt. Advies GPT-6.1 Sol / High; geen ondersteunde modelbediening en geen
wijziging geclaimd. Fase 15 is gereed voor gebruikersbeoordeling; geen volgende fase gestart.



## Fase 16 — inhoudelijke synergiecontrole (gestart 28 oktober 2026)

Expliciete gebruikersopdracht: de aanwezige testkaarten gebruiken om de synergie-engine
te onderzoeken, verbeteren waar nodig, en hier ruim tijd voor nemen. Advies GPT-6.1
Sol / High; raming 4–8 actieve uren inclusief bronnenonderzoek, vooraf vastgelegde
referenties, implementatie, evaluatie, UI/regressie-/praktijkcontrole en documentatie.
Geen ondersteunde modelbediening gevonden; instellingen niet gewijzigd.

Vooraf vastgelegde scope/criteria: docs/SYNERGY_ACCEPTANCE_PHASE16.md. Eerst v1.2-baseline
bewaren, positieve/negatieve relaties en ontbrekende voorwaarden onderzoeken, daarna
semantische kenmerken en contextafhankelijke selectie implementeren. Eigen testcollectie
is aangenomen productbezit, geen echte scan. Geen gameplay-/winrate-/brede kaartkennisclaim.
Geen normale data veranderen voor evaluatie. Nieuwe fase stopt bij oplevering ter beoordeling.

### Fase 16 — inhoudelijke controle en implementatie

28 oktober 2026, v1.3.0. Nieuwe voorwaardelijke engine met 87 gameplay-afgedekte
productprofielen, algemene beperkte feitenherkenning, kaartrelaties/conflicten,
contextselectie, expliciet onbekend en conditionele thematische alternatieven.
Geen algemeen sterke decks of volledige regelskennis claimen. Eigen/vijandelijke
sterftriggers, tokens/kaarten, echte landtypen, doelwitbescherming en kosten gescheiden.

Vooraf protocol en v1.2-baseline bewaard. Eerste ongeïnspecteerde proeven: 9/26,
21/28 en 14/23 relevante labels. Tweede proef vond bovendien twee onveilige layouts.
De eerste twee sets zijn na aanpassingen ontwikkelmateriaal. Derde set blijft negen
missers tonen; geen onafhankelijke of algemene precisieclaim. Eindregressies 26/26,
24/28 en 14/23. Live praktijk vond eigen verlieskaarten en alfabetische poolbias;
guards en populariteitsvolgorde toegevoegd. Multi-face/Adventure/split niet auto kiezen.
Ook beperkingen na vijf bewijsfragmenten blijven nu volledig in de relatie staan.

Volledige build nul fouten/waarschuwingen; 538 tests geslaagd (36 + 502), 95 extra
gevallen. Gewijzigde UI en klein venster gerenderd/bekeken. 27/27 thema-/stijlplannen
uit bezit hebben 60 kaarten zonder tekorten. Cloud/Sephiroth bezit-Commander blokkeert
terecht bij 34/62 en 23/62; live planning maakt beide 100 kaarten. Zes voorstellen
opgeslagen en heropend in verse geïsoleerde data; bezit bleef 202. Geen echte scan/speltest.

Normale en bestaande gevulde database behouden alle kolommen/rijen van twintig tabellen
(38.372 en 56.824), negen migraties. Geen nieuwe migratie/pakketten. Native bronstart/
respons/normaal afsluiten bevestigd. Eerste installatiekandidaat succesvol getest en
apart bewaard; na laatste bewijsweergavefix opnieuw gebouwd. Definitief pakketbewijs
volgt naast de release. Zie docs/SYNERGY_ENGINE.md en docs/VALIDATION_PHASE16.md.

### Fase 16 — definitieve oplevering

Definitieve v1.3.0-installer/portable opnieuw gebouwd na de laatste bewijsweergavefix.
Authentieke v1.2.0→v1.3.0-installatie, upgrade, uninstall, herinstallatie en laatste
uninstall op geïsoleerde paden geslaagd; eigen runtime en Startmenu-doel gecontroleerd.
Alle twintig tabellen exact behouden, schema negen gebleven, nul nieuwe migratieback-ups.
Definitieve portable start met beide bestaande gegevensmappen en normale afsluiting
bevestigd. Daarna alle kolommen en 38.372/56.824 originele rijen behouden. Alle 266 ZIP-
items matchen portable; manifest/installer/ZIP-hashes gecontroleerd. Installer/programma
NotSigned. Zichtbare v1.3.0 op normale data open gelaten voor beoordeling.

Eindbewijs naast de release: artifacts/release/1.3.0/VALIDATIE-NA-PAKKET.md. Ingesloten
validatie is een eerdere momentopname; bronverslag bevat het volledige eindresultaat.
Geen codewijzigingen na de 538 geslaagde tests. Geen signing, schone tweede pc, echte
scan of spelsterkte bewezen. Geen beveiligingsbeleid aangepast.

Werkvenster circa 10:36–13:40 Europe/Amsterdam, ongeveer drie actieve uren. De vooraf
ruime raming 4–8 uur is tijdens de uitvoering bijgesteld; bestaande opslag-/distributie-
controles konden worden hergebruikt. Modeladvies GPT-6.1 Sol / High; geen ondersteunde
instellingenbediening gevonden en niets gewijzigd. Fase 16 stopt hier voor beoordeling;
geen gebruikersacceptatie of volgende fase claimen.

## Fase 17 — gestart op 28 oktober 2026

Nieuwe opdracht: alle huidige kaarten als bezit aannemen en engine opnieuw onderzoeken.
Vooraf scope/acceptatie vastgelegd in docs/SYNERGY_ACCEPTANCE_PHASE17.md. Afzonderlijke
all-cards-fixture, geen aanpassing van echte collectie. Eerst bron/unieke papieren kaarten,
bevroren nulmeting, gereserveerde evaluatie en schaalproef; vervolgens gerichte correcties,
volledige deck-/regressie-/opslagcontrole en eerlijk rapport van onbekende werking.
Advies GPT-6.1 Sol / High; 4–8 actieve uren; geen modelinstellingen gewijzigd.

### Fase 17 — onderzoek en bronvalidatie voltooid

v1.4.0 / engine v7: Default Cards van 28 oktober geeft 33.727 fysieke Oracle-kaarten;
afzonderlijke IsDemo-fixture met 138.106 exemplaren. Nulmeting v1.3.0 bewaard, 10k-proef
na 60s gestopt. Groepindex/feitcache vervangt volledige paargraph; finale 31.037 geschikte
niet-landen in circa 40s / 308MiB selectorpiek. Complete meetruns met bewaarde grote
rapporten gebruiken circa 1,9GiB: geen lage-geheugengarantie voor de hele app.

Afzonderlijke eerste A15/52 (geen winst tegenover baseline), B22/57 tegenover 19/57,
nul positieve fouten in die beperkte metingen. Na inspectie ontwikkelmateriaal:
eindregressie A20/52,B22/57. Zelfde beoordelaar, selectief, geen algemene precisie.
13.716 beperkt herkend, 19.924 type-only/onbekend en 87 gecontroleerde productprofielen;
1.531 automatische uitsluitingen. Geen algemene kaart-/combo-/proliferatekennis claimen.

61/64 complete bezitplannen geslaagd/opgeslagen/idempotent/heropend; drie Standard/
Exalted terecht geen themapool. Slechte Giada/Krenko/Talrand-focus en overmatige Meren-
setup gecorrigeerd via expliciete commanderbouwfocus en setupbalans; alle 16 getroffen
aanvragen opnieuw geslaagd. 583 tests (36+547), build nul waarschuwingen/fouten,
grote collectie/filter/Commander UI-afbeeldingen bekeken. Native bronstart/normale
afsluiting op beide bestaande data bevestigd; alle 20 tabellen/9 migraties en 38.372/56.824
originele rijen behouden. Geen schema/pakket-/beveiligingswijziging. Pakketvalidatie volgt.

### Fase 17 — definitieve oplevering

v1.4.0 self-contained Windows x64 portable/installer gebouwd. Alle 269 ZIP-bestanden
matchen portable; hashes gecontroleerd, geen gebruikersdata meegeleverd. Installer en
programma ongetekend. Authentieke v1.3.0→v1.4.0-installatie/upgrade/uninstall/herinstallatie
met gevulde geïsoleerde fixture geslaagd, eigen runtime en Startmenu-doel bevestigd.
Laatste testuninstall laat data intact. Negen migraties, nul nieuwe migratieback-ups.

Definitieve portable met normale en gevulde bestaande data gestart, respons/logs en
normaal afsluiten bevestigd; alle twintig tabelhashes en 38.372/56.824 originele rijen
exact behouden. Daarna v1.4.0 zichtbaar op afzonderlijke alle-kaartensimulatie geopend.
Startpiek circa 779 MiB; complete-generatie-/rapportproeven eerder circa 1,9 GiB.
Ingesloten validatie is een momentopname; eindbewijs staat naast de release en in
docs/VALIDATION_PHASE17.md. Geen runtimecode gewijzigd na 583 geslaagde tests.
Eén gerichte screenshot-/all-cards-UI-hercontrole geslaagd. Scripts syntactisch gecontroleerd.

Werkvenster circa 14:08–17:25 Europe/Amsterdam, ongeveer 3¼ actieve uren; eerdere raming
4–8 uur ruim door hergebruik van bestaande controles. Modeladvies GPT-6.1 Sol / High,
geen ondersteunde instellingenwijziging. Fase 17 stopt voor gebruikersbeoordeling;
geen acceptatie of nieuwe fase claimen.

## GitHub-migratie — 29 oktober 2026

De gebruiker heeft verhuizen naar GitHub opgedragen en na controle van de bestaande
repository expliciet voor openbare plaatsing in Xenxs/MTG-Builder gekozen. Dit is
repositoryonderhoud, geen nieuwe enginefase. Advies GPT-6.1 Sol / Medium, 20–45 minuten
actief voor bestands-/secretscontrole, Git-inrichting, schone-clonecontrole en upload.
Geen chatinstellingen gewijzigd. Lokale werkmap en data blijven behouden.

Broncode/tests/docs/kleine voorbeelden/assets en de bestaande Windows-workflow worden
overgezet. .local, artifacts, databases/logs, IDE-gebruikersbestanden en sleutels worden
uitgesloten. README en docs/GITHUB.md beschrijven starten vanaf bron en het onderscheid
tussen repository en lokale distributie-/bulkproeven. Uitvoering/resultaat worden na
de daadwerkelijke upload en verificatie vastgelegd; geen succes vooruit claimen.


