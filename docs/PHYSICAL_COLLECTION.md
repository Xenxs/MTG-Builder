# Fysieke collectie — contract en grenzen

Actueel: v1.3.0 gebruikt hetzelfde fysieke opslagmodel en migraties tot en met negen. De onderstaande fase-14-beschrijving is de oorspronkelijke contractspecificatie. Actuele filters/wizard: [handleiding](MAGIC_WIZARD_HANDLEIDING.md); kaartrelaties en grenzen: [synergie](SYNERGY_ENGINE.md).

Versie 1.1.0, fase 14. Advies GPT-6.1 Sol / High; geen ondersteunde modelsetter beschikbaar,
dus geen configuratiewijziging geclaimd. Oorspronkelijke raming 2–4 actieve uren.

## Opslag en migratie

Migration `20261027205729_AddPhysicalWorkspace` voegt vijf tabellen toe:
PaperPrintings (exacte Scryfall-printing, bron-JSON, ophaaltijd), PaperHoldings (positie,
taal/finish/conditie/binder/aantal/demo/versie), PaperImports (bronhash, voorbeeld,
koppelingen en before/after-ontvangst), PaperDecks en PaperDeckCards (printing+zone).
Alle vijftien bestaande Arena-tabellen blijven inhoudelijk bestaan. Arena-ID en
Arena-voorraad worden niet omgezet naar fysiek bezit. Databasepad en installatie-AppId
blijven behouden, zodat upgrade en back-up beide gegevenssoorten meenemen.

Fysieke gegevens zijn onafhankelijk van Arena-beschikbaarheid. Domain blijft zonder
UI/EF-referenties; Application definieert contracten en regels; Infrastructure doet
alle SQLite/Scryfall-I/O buiten de UI-thread met korte contexts. Writes lopen via de
bestaande process-wide lock en expliciete SQLite-transactie; voorraadversies bewaken
wijzigingen tussen review en commit. De bestaande gegevensmap-lease blijft vereist.

De vijf-tabellen-uitbreiding houdt printingmetadata als ruwe Scryfall-JSON om onbekende
velden niet te verliezen. Dit vervangt geen algemene wereldwijd genormaliseerde catalogus.
De actieve werkruimte leest haar lokale metadata voor analyses; zeer grote collecties
en lang gebruikte caches hebben nog geen gemeten prestatiegarantie.

## CSV

UTF-8, optionele BOM; komma, puntkomma of tab; quoted delimiters, escaped quotes en
meerregelige velden. Maximaal 16 MiB, 10.000 regels en 0–9.999 exemplaren per positie.
Quantity verplicht. Exacte koppeling via Scryfall ID of setcode+collectornummer+taal;
geen uitgave kiezen op naam alleen. Conflicten tussen ID/naam/uitgave vragen expliciete
koppeling. Ontbrekende taal bij natuurlijke sleutel blijft onopgelost; bij ID komt de
taal van de exacte printing. Een expliciete afwijkende taal vraagt dezelfde set/nummer/
taal bij Scryfall, met controle op dezelfde Oracle-identiteit.

Normal/nonfoil/false worden normal; foil/true en etched blijven afzonderlijk.
Ontbrekende finish/conditie wordt unknown. Binder Type deck/list en Proxy true worden
zichtbaar overgeslagen. Een onbekend Binder Type wordt geweigerd. Kolomnamen zijn
case-insensitive; geen algemene aanpasbare kolommapping. Bekende taalnamen worden naar
Scryfall-codes genormaliseerd. Alle originele kolommen, inclusief prijs/altered/misprint/
opmerkingen, blijven in de bronregel van de importontvangst bewaard. Dat zijn nog geen
afzonderlijk doorzoekbare bezits- of waarderingsvelden.

Identiteit = SHA-256 van exacte printing, taal, finish, conditie en binder. Dubbele
identieke regels worden opgeteld. De som is absoluut, geen delta. Omitted posities
blijven bestaan; nulregels zijn nodig voor verwijderde/verplaatste oude registraties.
Een binderhernoeming of dubbel geregistreerde fysieke exemplaren kan dubbeltellen:
de app kan niet bewijzen welke twee regels hetzelfde fysieke kaartobject voorstellen.

Prepared import bewaart alle regels, bronhash en de fingerprint van de gehele voorraad.
Apply controleert versie, fingerprint en huidige printing/finish/taal opnieuw. Voorraad
en ontvangst worden samen gecommit. Dubbel toepassen van hetzelfde plan is idempotent.
Undo vergelijkt de exacte betrokken after-versies en zet geen latere wijzigingen terug.
Een import zonder getalswijziging verandert de voorraadversie niet onnodig. Historie
toont de recentste 100 imports; oudere ontvangstgegevens blijven opgeslagen.

Zodra enige niet-demo-positie bestaat, tellen uitsluitend niet-demo-posities mee.
Dit geldt ook bij aantal nul: een lege echte collectie wordt niet door demo aangevuld.
Na volledig herstel van de eerste import kan demo opnieuw actief worden. Decks reserveren
geen bezit tegenover andere decks. Handmatige registratie heeft een eigen binder.

## Scryfall en afbeeldingen

On-demand `cards/id`, `cards/set/number/language` en zoeken met `game:paper`, maximaal
40 resultaten per zoekopdracht. Elke kaart wordt na ontvangst zelf op `games:paper`
gecontroleerd, ook als een zoekfilter anders wordt geïnterpreteerd. ID/natuurlijke sleutel
worden tegen het antwoord gecontroleerd. Metadata-cache maximaal één dag voor ID-opzoeking;
Standard weigert ontbrekende/verouderde bronnen. Een actuele lokale cache maakt niet
alle nieuwe zoekopdrachten offline mogelijk.

Eigen User-Agent en bestaande HTTP-client zonder redirects. API-aanvragen worden 120 ms
uit elkaar gestart; bounded antwoorden (2 MiB), timeout 30 sec. Netwerkfouten leiden
tot zichtbare melding/onopgeloste regel, geen stil bezitverlies. Bij API-429 is er nog
geen automatische Retry-After-resume: opnieuw proberen kan nodig zijn.

Beelden uitsluitend HTTPS op cards.scryfall.io, defaultpoort, zonder credentials;
maximaal 5 MiB, 25 sec timeout, JPEG/PNG-signatuur. Cache per exact ID en voor-/achterzijde,
schrijven via tijdelijk bestand en atomische move. UI decodeert buiten de UI-thread,
cancelt verouderde selecties en vervangt het beeld alleen bij de nog actuele selectie.
De CDN-afbeelding toont geen werkelijk gescande foil/conditie. Cache kan door een gebruiker
worden verwijderd wanneer de app gesloten is om een afbeelding opnieuw op te halen.

## Conflux en decks

Embedded `conflux-paper-v1.json`: op 27 oktober 2026 opgehaalde echte Scryfall-JSON voor
54 exacte printings, met de twee MTGJSON-productlijsten. 41 kaarten per introdeck,
82 exemplaren/63 bronregels; foil-conditie volgens productdata, geen willekeurige booster.
Een toegepaste marker voorkomt opnieuw aanvullen/hermaken bij iedere start.
Demo-productlijsten kunnen niet rechtstreeks op kaartniveau gewijzigd worden; maak een
kopie. Een expliciet verwijderd productdeck wordt niet bij elke start teruggezet.

Casual60 is uitsluitend een structurele controle, geen tournamentprofiel. Standard
gebruikt Scryfall `legalities.standard` voor papier, minimum 60 mainboard, maximaal 15
sideboard, normaal vier per Oracle-identiteit over zones/printings. Basic en Basic Snow
lands zijn onbeperkt; kaarttekst met deckbouwuitzondering geeft onzekerheid, geen pass.
Regelcontroledatum 27 oktober 2026 verloopt na 14 dagen; metadata na 24 uur. Er is geen
automatische volledige Comprehensive Rules-engine of formatdatabank voor Commander/Modern.

Analyse: gedrukte MV/curve, aanwezige basislandtypen en tekstvermeldingen domain/exalted.
Geen castingkans of speltoestand. De bestaande beperkte rolclassifier wordt hergebruikt;
alleen volledige beperkte matches of dezelfde Oracle-kaart worden als lokale opties getoond.
Onbekende kaarttekst is geen lage speelsterkte. Geen LLM/API-integratie of generieke
synergie-optimizer. De oude complete mono-rode Arena-generator blijft uitsluitend archief.

Decktekst deelt de bestaande count/name/(set)/number-syntax, maar haalt fysieke uitgaven
op en eist expliciete taal en exacte naam/uitgave. Geen bezitwijziging. Review van import,
kopie of vervanging is tijdelijk; opslag maakt idempotent een nieuw deck met vooraf
vastgelegd ID. Voorraad én lokale kaartmetadata worden bij opslaan hercontroleerd.
Opgeslagen decks overleven herstart; voorbereide CSV-imports zijn wel persistent.

## Bronnen (geraadpleegd 27 oktober 2026)

- https://manabox.app/guides/collection/import-export/
- https://manabox.app/guides/scanner/getting-started/
- https://mtgjson.com/api/v5/decks/NayaDomain_CON.json
- https://mtgjson.com/api/v5/decks/BantOnTheMarch_CON.json
- https://mtgjson.com/data-models/deck/
- https://magic.wizards.com/en/formats/standard
- https://magic.wizards.com/en/news/feature/sideboard-2015-08-10
- https://scryfall.com/docs/api en https://scryfall.com/docs/api/images

Scryfall-documentatie gaf in deze omgeving HTTP 403; de werkelijke API-/CDN-proef is
wel uitgevoerd en vastgelegd. De MTGJSON-bron verwijst naar een oude Wizards-productpagina
die hier niet bereikbaar was; de MTGJSON-lijsten zijn als productdata gebruikt. Geen
handmatig gecontroleerde echte telefoonexport of gameplaybenchmark claimen.

## Fysiek Conflux-voorstel

PhysicalDeckPlanner levert één lokaal, reproduceerbaar Casual60-constructieplan: 36
niet-landen en 24 basislanden. De pool is begrensd tot Oracle-identiteiten uit de twee
gepubliceerde productdecks. Naam, manakosten/MV, type, Oracle-tekst en kleuridentiteit
moeten exact de bevroren referentie matchen; een afwijkende bekende printing sluit de
hele Oracle-kaart uit. Prijzen/illustraties/ophaaltijd horen niet bij die structuurhash.

Selectie sorteert niet-landen op MV en naam, tot maximaal vier per Oracle-kaart. Bezit
wordt gezamenlijk en per exacte printing uitgegeven; een bronkopie wordt niet tweemaal
besteed. Basislanden: één van ieder beschikbaar basislandtype, daarna het grootste
quotiënt (enkelkleurige gedrukte manasymbolen + 1)/(gekozen landen van dat type + 1).
Dit is een vast uitlegbaar rekenvoorbeeld, geen geoptimaliseerde castingkans of synergie.

Bezitmodus is standaard en weigert elk tekort/structureel probleem. Planning is expliciet
met volledige tekorten, zonder bezitmutatie. Volledige lijst, bron-/voorraadfingerprints,
bevestiging en idempotente nieuwe-deckopslag gebruiken hetzelfde PaperDeckDraft-contract.
De modus is onderdeel van het tijdelijke voorbeeld; na opslag is het een regulier
fysiek deck met actuele tekortcontrole, geen persistent optimalisatie- of evaluatierapport.
Geen algemene generator voor nieuw gescande sets of Standard, geen winrateclaim.

## Actuele uitbreiding: Starter Kit en Commander

`final-fantasy-starter-v1.json` bevat echte Scryfall-metadata voor 55 uitgaven en
55 bronregels/120 speelkaarten volgens MTGJSON en de officiële Wizards-decklijsten.
Een eigen marker voegt uitsluitend de nieuwe productdata toe. Samen met Conflux:
202 speelkaarten, 109 exacte printings, 118 bezitsposities en vier decks. Geen extra
schemawijziging; nog steeds twintig applicatietabellen en acht migraties. Beide
seedmarkers zijn uitgesloten van collectie-importgeschiedenis en handmatige undo.

PaperCard leest nu Commander-legaliteit en P/T-velden uit bestaande bron-JSON; geen
Arena-beschikbaarheid vereist. CommanderDeckPlanner werkt met alle passende actieve
fysieke voorraad, niet alleen Conflux. Planning vraagt maximaal 50 Oracle-kaarten per
MV-band (<=3, 4–5, 6–8), plus maximaal 10 basislanden: `game:paper legal:commander id<=...`,
`unique=cards`. Antwoorden worden ook lokaal op legaliteit/papier/kleur gecontroleerd.
De eerste API-pagina begrenst de aanvulling; geen volledige wereldcatalogus of willekeurige
online decklijst wordt geïmporteerd. Bezitmodus vraagt geen extra niet-bezitskaartpool op.

Vast Commander-plan: één ondersteunde commander + 62 singleton niet-landen + 37 basics.
Selectie geeft bezit voorrang, daarna lichte tekstaanwijzingen (bijvoorbeeld equip en
Equipment) en een vaste MV-doelreeks. Dit bewijst geen functionele of strategische
synergie. Mana gebruikt enkelkleurige gedrukte pips; geen castingkanssimulatie. Singleton
wordt over Oracle-printings geteld, de commander kan niet nogmaals in de library;
basic/snow-basic lands onbeperkt. Kleuridentiteit inclusief achterzijden komt van
Scryfall; basic-landtypen krijgen daarnaast een defensieve kleurcontrole. Sideboard nul.

Regels CR 903.3/903.4/903.5, geldig 25 september 2026, gelezen 27 oktober: legendary
creatures, legendary Vehicles, Spacecraft met P/T en expliciete 'can be your commander'
worden ondersteund. Front-face type wordt gebruikt voor commander-keuze. Partner/background-
paren, variabele kleurkeuze en overige niet-herkende kaartuitzonderingen blijven geblokkeerd
of onzeker; geen pass daarvoor. Brackets, Game Changers en cEDH zijn geen geleverde classificatie.

Alle gebruikte Commander-kaarten moeten papieren Commander-legaliteit legal hebben,
metadata maximaal 24 uur oud en geen onredelijke toekomsttijd; regelversie maximaal
14 dagen oud. SaveDeckDraft hercontroleert naast voorraad-/metadatafingerprints ook
Commander-regels en tijdsactualiteit. Planning mag uitsluitend bezitstekorten hebben;
andere problemen blokkeren opslag. Decktekst met Commander-sectie krijgt automatisch
het Commander-profiel; fysieke collectornummers behouden hun oorspronkelijke hoofdletters.

Bronnen:
- https://magic.wizards.com/en/news/announcements/final-fantasy-starter-kit-decklists
- https://mtgjson.com/api/v5/decks/BlueBlack_FIN.json
- https://mtgjson.com/api/v5/decks/RedWhite_FIN.json
- https://magic.wizards.com/en/formats/commander
- https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt (903.3–903.5)
