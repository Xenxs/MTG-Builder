# Ontwerpbeslissingen

## ADR-001 — Modulaire monoliet met vier lagen

Eén desktopproces is voldoende voor lokale opslag. Geen server, microservices of generiek
repositoryframework. Domain blijft onafhankelijk; Application definieert de benodigde
opslagcontracten. Gevolg: later is een importmodule toe te voegen zonder UI in het domein.

## ADR-002 — .NET 10, Avalonia 12 en xUnit v3

De beschikbare SDK is 10.0.401. global.json staat .NET 10 featureband-rollforward toe.
Directe NuGet-versies zijn vastgezet: Avalonia 12.1.4, CommunityToolkit.Mvvm 8.4.2,
EF Core en Microsoft DI 10.0.13, Serilog.Extensions.Logging 10.0.0, file sink 7.0.0,
xUnit v3 3.2.2, runner 4.0.1 en Test SDK 18.11.0.

Avalonia.Headless.XUnit 12.1.4 gebruikt xUnit v3; de testprojecten gebruiken daarom
dezelfde generatie. Een aanvankelijke combinatie met xUnit v2 leidde aantoonbaar tot
dubbele testtypen en is vervangen. SDK, toolmanifest en pakketversies zijn vastgelegd.
Bron: lokale NuGet-package-manifesten en [Avalonia-documentatie](https://docs.avaloniaui.net/docs/get-started/install-avalonia).

## ADR-003 — MigrateAsync en versioneerbare migraties

Geen EnsureCreated. De eerste migratie en modelsnapshot zijn gegenereerd met de
projectlokale dotnet-ef 10.0.13-tool en opgenomen in Infrastructure/Migrations.
De app past migraties bij de start toe; fouten blokkeren bewerken en worden gemeld.
Dit is passend voor een lokale toepassing. Vanaf fase 2 wordt vóór een schema-upgrade
automatisch een consistente SQLite-back-up gemaakt. Herstellen is nog een handmatige
handeling met gesloten app; er is geen periodieke back-updienst.
Bron: [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/).

## ADR-004 — Kaart, printing en bezit apart

Een kaart heeft verschillende printings; bezit en deckregels verwijzen naar een printing.
Zo kan catalogusdata later onafhankelijk van gebruikersaantallen worden ververst.
Handmatige fase-1-invoer hergebruikt kaartnamen zonder onderscheid in hoofdletters.
Dit is een tijdelijke invoerregel, geen importidentiteit. Vanaf fase 2 bepaalt Oracle-ID
de kaartidentiteit en Scryfall-ID de printingidentiteit. De oude unieke sleutel is uitgebreid
met taal en geldt alleen voor handmatige printings. Arena-ID is een optionele lookup,
geen uniek opgelegde sleutel. Zie ADR-011 voor koppeling van bestaande gebruikersdata.

## ADR-005 — Geplande decks zijn onafhankelijk van bezit

De gebruiker mag kaarten plannen die hij niet of onvoldoende bezit. Deze fase controleert
geen formaatregels, vierkopieënlimiet, sideboardlimiet of Arena-beschikbaarheid. Aantallen
zijn technisch begrensd om onzinnige invoer te weigeren. De UI maakt de ontbrekende
legaliteitscontrole duidelijk. Formaat is metadata en geen gevalideerde regelset.

## ADR-006 — Async-grens rond SQLite

Alle opslagcontracten ondersteunen async en cancellation. Microsoft.Data.Sqlite voert
asynchrone I/O-aanroepen intern synchroon uit. Een infrastructure-decorator voert de
bewerkingen daarom op workerthreads uit met een eigen DbContext per bewerking. De UI
blijft zo tijdens lokale opslag en migraties beschikbaar. Dit is geen claim van native
asynchrone SQLite I/O.
Bron: [Microsoft.Data.Sqlite async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async).

## ADR-007 — Echte database en echte views in tests

Geen EF InMemory-provider voor integratietests: die zou SQL-checks, foreign keys en
migraties onvoldoende toetsen. Elke test krijgt een tijdelijk SQLite-bestand. Tests
openen services en verbindingen opnieuw, controleren migratiehistorie, mutaties en
databasebeperkingen. UI-tests laden echte XAML en renderen via Avalonia Headless/Skia.

## ADR-008 — Centrale log- en foutpresentatie

Serilog schrijft daglogs met 14-bestandenretentie. EF SQL wordt niet standaard gelogd.
De UI toont validatieproblemen concreet en onverwachte fouten zonder stacktrace.
Instellingen toont de loglocatie. Daglogs zijn lokaal en worden niet verzonden.
Er zijn geen secrets, externe API-sleutels of accounts nodig.

## ADR-009 — Beleidsstatus controleren en alleen gevalideerde wijzigingen opleveren

De machine blokkeerde eerst gewijzigde ontwikkelbuilds via Windows App Control.
Aanvankelijk is de eerder gevalideerde versie teruggezet. De gebruiker gaf daarna
expliciet toestemming voor noodzakelijke beveiligingsbeleidsaanpassingen voor dit project.
Bij controle stond Smart App Control al uit; de agent heeft geen beleid aangepast.
De resterende fase-1-verbeteringen voor aantallen en decklabels zijn alsnog doorgevoerd.
Die fase-1-versie bouwde zonder waarschuwingen, doorstond alle 27 tests en is als native
Windows-app gestart. Zie VALIDATION.md voor bewijs en eerdere blokkades. Fase 2 is daarna
expliciet gestart, gebouwd en getest; actuele resultaten staan in VALIDATION_PHASE2.md.

## ADR-010 — Scryfall Default Cards en streaminginvoer

Een volledige Arena-catalogus vraagt om een bulkbron, niet om losse aanvragen per kaart.
Het actuele Scryfall-manifest levert jsonl_download_uri en compressed_size. Deze velden
zijn op 21 oktober 2026 daadwerkelijk via de primaire API gecontroleerd; oudere voorbeelden
met alleen download_uri zijn niet als actuele waarheid gebruikt. De client ondersteunt
ook dat oudere veld en de parser ondersteunt zowel JSONL als JSON-arrays, met of zonder gzip.
Alleen records waarvan games de waarde arena bevat worden opgenomen. Geen account of
API-sleutel nodig. De app noemt Scryfall en Wizards of the Coast als bron/rechthebbende.
Bronnen: [bulk data](https://scryfall.com/docs/api/bulk-data),
[API-voorwaarden en headers](https://scryfall.com/docs/api),
[kaartobjecten](https://scryfall.com/docs/api/cards).

## ADR-011 — Identiteiten en handmatige gegevens behouden

Scryfall-ID is de unieke bronidentiteit per printing; Oracle-ID groepeert reprints.
Printings met verschillende Scryfall-ID's mogen dezelfde set/collector/taal hebben.
Daarom is de natuurlijke sleutel alleen uniek voor niet-gekoppelde handmatige regels.
Een bronlookup op alleen Arena-ID hoeft geen unieke match af te dwingen.

Handmatige printings worden alleen gekoppeld als de natuurlijke identificatie en naam
ondubbelzinnig overeenkomen. Het bestaande interne printing-ID, collectieaantal en alle
deckreferenties blijven behouden. Bij onzekerheid blijft de handmatige invoer apart en
wordt dit gerapporteerd. Er wordt geen naamgebaseerde, blinde samenvoeging uitgevoerd.
Afwezige catalogusprintings blijven in de database voor bestaande gebruikersreferenties.

## ADR-012 — Voorbereiden met checkpoints, atomair toepassen

Het zichtbare catalogusresultaat mag niet half worden bijgewerkt. Download en parsing
schrijven daarom eerst een afzonderlijke importregistratie en stagingrecords. Elke 500
bronrecords vormen een duurzaam checkpoint. De volledige toepasstap is één transactie;
annuleren of een fout behoudt de vorige catalogus en alle gebruikersaantallen.

Hervatten gebruikt de volledige broncache met SHA-256-controle en slaat reeds gecommitte
bronrecords over. Afgebroken downloads worden opnieuw gedaan: HTTP-rangehervatting is
geen onderdeel van deze fase. Succesvolle staging wordt verwijderd; de twee nieuwste
voltooide broncaches blijven staan. Onvoltooide caches blijven voor hervatten bewaard.
Een bestandslot verhindert twee gelijktijdige imports; een processchrijfslot begrenst
interferentie met normale collectie-/deckbewerkingen.

## ADR-013 — Zoeken blijft lokaal en geeft geen bezit cadeau

Zoekresultaten worden in SQL gefilterd en gepagineerd. De UI haalt 50 resultaten op en
toont naam, type, regeltekst, mana, set, collector, taal, zeldzaamheid en bezit.
Filters zijn eenvoudig en expliciet, zonder volledige Scryfall-querytaal. Een catalogusimport
schrijft nooit naar CollectionEntries of DeckCards; alleen een gebruikersactie op een
geselecteerde printing mag het absolute collectieaantal veranderen.

## ADR-014 — Upgradeback-up vóór migreren

Vóór een bestaande database met openstaande migraties wordt gewijzigd, maakt de app
een consistente kopie via SQLite.BackupDatabase. Een back-upfout blokkeert de upgrade.
Een integratietest bouwt daadwerkelijk het fase-1-schema, vult bezit/decks/instellingen,
voert de upgrade uit en leest de reservekopie terug. Periodieke back-ups en een herstel-UI
vallen buiten fase 2.
Vanaf fase 3 gebruikt de back-upbestemming Pooling=false: de afgesloten verbinding
houdt het back-upbestand niet langer via de pool open. De migratietest opent de
back-up vóór verder gebruik exclusief om die vrijgave te controleren.

## ADR-015 — Eerst een bewijsbaar collectieformaat

Fase 3 gebruikt eigen, versieerbaar collectie JSON v1. De actuele Wizards-logdocumentatie
beschrijft Detailed Logs maar garandeert geen complete bezitslijst; een Player.log was
op de standaardlocatie in deze omgeving afwezig. Er wordt geen fictieve Arena-adapter
gebouwd op aannames. Een toekomstige adapter kan hetzelfde ICollectionTransfer-concept
gebruiken na bronvalidatie. CSV, logs, accountkoppeling en live volgen zijn geen onderdeel
van deze oplevering. Limieten en identiteitsregels staan in COLLECTION_IMPORT.md.
Bron: [Wizards-logdocumentatie](https://mtgarena-support.wizards.com/hc/en-us/articles/360000726823-Creating-Log-Files-on-PC-Mac-Steam),
gecontroleerd op 22 oktober 2026.

## ADR-016 — Expliciete voorbereiding en absolute aantallen

Voorbereiden schrijft uitsluitend audit-/voorbereidingsrijen; bezit verandert pas na
expliciet controleren en toepassen. De voorbereiding blijft na herstart beschikbaar,
ook als het bronbestand is verwijderd. Alleen de bestandsnaam, hash en gevalideerde
kaartregels blijven bewaard, geen volledig bronpad of willekeurige loginhoud.

Een bronregel verwijst naar één bestaande printing via Scryfall-ID, Arena-ID of een
volledige set/nummer/taal-identiteit. Naam alleen is onvoldoende. Meerdere kandidaten
vereisen een expliciete keuze. Naamverschillen mogen niet stilzwijgend worden genegeerd.
Een handmatige printing is een geldig doel; een expliciete koppeling verplaatst of
combineert geen bestaande printings, bezit of deckregels. Dat blijft een aparte toekomstige usecase.

Importaantallen zijn absoluut. Ontbrekende regels blijven staan, expliciete nullen worden
toegepast, dubbele doelen blokkeren totdat de gebruiker ze oplost. Daarmee is herimport
zonder extra bezit mogelijk, ook bij herhaald klikken of opnieuw openen van dezelfde bron.
Een nieuwe voorbereiding is een nieuwe bewuste snapshot van huidige doelkeuzes en aantallen.

## ADR-017 — Versies beschermen voorbereid toepassen en herstel

Ieder expliciet schrijven aan een bezitaantal vervangt CollectionEntry.Version door
een nieuw GUID; EF gebruikt het ook als concurrency-token. Alleen aantallen vergelijken
zou een wijziging-en-terugzetting missen. Collectievoorbereidingen hebben daarnaast een
eigen versie om keuzes vanuit twee geopende clients niet stilzwijgend te laten winnen.
Een doelidentiteitshash detecteert relevante catalogus-/naamwijzigingen na voorbereiding.

Toepassen en herstel zijn afzonderlijke, volledige SQLite-transacties, met validatie
vóór wijzigingen. Bezit en auditstatus committen samen. Annulering wordt zowel vóór
SaveChanges als vóór commit gecontroleerd. Integratietests veroorzaken fouten en
annulering na echte SQL-writes om rollback te controleren.

Herstel is strikt en raakt alleen werkelijk gewijzigde regels. Eén latere wijziging
blokkeert de hele herstelactie. Een volledige database terugzetten zou ook latere decks
en andere collectieaanpassingen verwijderen; daarom wordt hier met wijzigingsregels
gewerkt. Geen geforceerd herstel en geen slimme automatische herstelketens.

## ADR-018 — Export is overdracht, geen volledige back-up

Export schrijft bestaande bezitregels, inclusief nul-aantallen, naar hetzelfde JSON v1-formaat.
Scryfall-ID heeft voorrang; handmatige printings krijgen de volledige natuurlijke identiteit.
Een leeg bezitbestand wordt niet als bruikbare import voorgesteld. Export accepteert alleen
een `.json`-doel en schrijft eerst naast het doel naar een tijdelijk bestand. Een fout of
annulering behoudt het oorspronkelijke doelbestand. Decks, instellingen en historie vallen
niet onder export; upgradeback-ups blijven een afzonderlijk mechanisme.

## ADR-019 — Eerst deckplanning, daarna regels en aanbevelingen

De gebruiker accepteerde fase 3 met "goed ga door" en startte daarmee fase 4. De
volgende afgebakende stap is decktekst import/export en beschrijvende analyse. Hiermee
kan werkelijk worden getoond wat ontbreekt op basis van lokaal bezit. Geen vermeende
decksterkte of legaliteit zonder onderbouwd regels-/evaluatiemodel. Companion wordt
expliciet geweigerd omdat het geen onvoorwaardelijke extra sideboardkaart is; commander
krijgt wel een eigen zone zonder regels over geschiktheid op te leggen.

## ADR-020 — Nieuwe decks uit duurzame voorbereidingen

Import schrijft eerst alleen voorbereiding en bronregels. Onbekende/ambigue identiteiten
moeten expliciet worden opgelost; niets verdwijnt stilzwijgend. Eén voorbereiding kan
precies één nieuw deck aanmaken. Bestaande decks blijven staan, ook bij dezelfde naam.
Bij een later verwijderde doeldeck blijft de ontvangstregistratie behouden. Een tweede
bewuste kopie vraagt een nieuwe voorbereiding. Geen bezitwijzigingen door deckimport.

Dubbele regels voor dezelfde printing/zone tellen op: ze beschrijven deelhoeveelheden
van één deck. Dit wijkt bewust af van collectie-import met absolute bezitsopdrachten.
Een hoeveelheidconflict wordt voor alle pagina's berekend en blokkeert toepassen.
Identiteitshashes bewaken cataloguswijzigingen; bezitsversies zijn geen importblokkade,
omdat deckplanning nooit bezit overschrijft. SQLite-transacties omvatten deck én ontvangst.

## ADR-021 — Tekstidentiteit is soms een kaart, soms een printing

Set/nummer plus naam moeten uniek koppelen of een expliciete keuze krijgen. Bij alleen
naam mag de app een uitgave kiezen als alle kandidaten dezelfde geverifieerde Oracle-ID
hebben: meest bezit, dan Engels, actief, set/nummer en stabiele ID. De reden is zichtbaar.
Ongeverifieerde handmatige namen worden niet automatisch met catalogusidentiteiten gemengd.
Een voorzijdenaam is een expliciet ondersteunde alias voor dubbelzijdige namen.

Export ondersteunt de gangbare Engelse sectie-/printingnotatie; taal en artvariant passen
niet afzonderlijk in die tekst. Er wordt geen exact GUID-uitwisselformaat geclaimd.
De eigen parser controleert export voordat die wordt aangeboden. Er zijn geen claims
over alle Arena-versies zonder test in de actuele Arena-client.
Bronnen: [Wizards import](https://mtgarena-support.wizards.com/hc/en-us/articles/360049857771-Importing-a-Deck),
[Wizards uitgavekeuze](https://mtgarena-support.wizards.com/hc/en-us/articles/32275576862484-Patch-Notes-2024-43-00)
en [decktool-documentatie](https://magicalpiles.com/docs/), gecontroleerd op 22 oktober 2026.

## ADR-022 — Vraag over zones en reprints combineren

Voorraad is per lokale printing vastgelegd. Exact tekort combineert eerst vraag over
mainboard, sideboard en commander, zodat bezit niet per zone dubbel wordt gebruikt.
Kaarttekort combineert daarna alle geverifieerde printings met dezelfde Oracle-ID.
Beide getallen blijven zichtbaar. Twee plus twee van verschillende uitgaven kan dus
een kaartvraag van vier dekken terwijl nog twee exacte printings ontbreken.

Handmatige identiteiten blijven per printing gescheiden. Tussen decks worden geen
exemplaren gereserveerd. Basisland-unlocks, speciale Arena-eigendomsregels en wildcard-
kosten worden niet geraden; de uitkomst heet expliciet lokaal geregistreerd bezit.

## ADR-023 — Beschrijvende statistieken met zichtbare grenzen

De pure analyzer telt mainboardmetadata. Curve en gewogen gemiddelde sluiten landen
en ontbrekende metadata uit. Meervoudige kaarten gebruiken voorzijdetype en catalogus-
manawaarde; variabele kosten en speelkeuzes worden niet gesimuleerd. Meerdere typen en
kleuridentiteiten kunnen overlappen. Kleuridentiteit is geen analyse van manabronnen.
Een ontbrekende metadatawaarde wordt niet als nul-mana-kleurloos voorgesteld.

## ADR-024 — Commander-migratie geheel transactioneel

De eerste ontwikkelstart gaf een EF-waarschuwing over foreign_keys uitschakelen buiten
de transactie bij een generieke SQLite-rebuild. DeckCards heeft geen inkomende foreign
keys. De definitieve vierde migratie bouwt daarom expliciet binnen de migratietransactie
een vervangende tabel, kopieert de rijen, vervangt de oude tabel en maakt de index opnieuw.
Foreign keys blijven aan. De schema-uitkomst is gelijk; oudere migraties veranderen niet.
Een fout na de SQL-copy/drop/rename rolt aantoonbaar alles terug. Een nieuwe native upgrade
op gevulde geïsoleerde fase-3-data slaagt zonder waarschuwingen en behoudt alle controlehashes.

## ADR-025 — Standard BO1/BO3 expliciet kiezen

Fase 5 controleert alleen Arena Standard BO1 en BO3. Bestaande formaatlabels zijn vrije
metadata; Standard wordt niet naar een wedstrijdvariant geraden. Een aparte domeinwaarde
legt de expliciete keuze vast zonder het oude label te wijzigen. Nieuwe expliciete
BO1-/BO3-labels kunnen die waarde direct zetten. Andere formaten blijven bewerkbaar,
maar krijgen geen impliciet Standard-oordeel. Commander-regels zijn ongeldig in dit profiel;
een companion kiezen/importeren blijft buiten de scope.

De actuele primaire bron corrigeert de oude BO1-grens van zeven: beide varianten hebben
sinds 3 februari 2026 maximaal vijftien sideboardkaarten. BO1 heeft aanvullend de ban
op Leyline of Resonance. Bronnen gecontroleerd op 22 oktober 2026:
[Wizards Arena](https://magic.wizards.com/en/news/mtg-arena/announcements-february-2-2026),
[banlijst](https://magic.wizards.com/en/banned-restricted-list) en
[Comprehensive Rules](https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt).

## ADR-026 — Legaliteitsdata met herkomst en atomair vervangen

CardLegalities koppelt Standard-status aan de kaart en de toegepaste import. Het online
bulkmanifest levert de bewezen brondatum; bij een lokaal bestand is die onbekend, ook
als het net geïmporteerd is. Een hash bewijst identiteit, geen ouderdom. MetadataVersion
zorgt dat fase-2/3/4-imports opnieuw worden verwerkt bij online bijwerken. Oude incomplete
checkpoints kunnen onbekende velden bevatten; ze leveren dan geen volledig oordeel op.

De importer valideert legalities.standard, bewaart onbekende toekomstige stringwaarden
als Unknown en weigert verkeerde JSON-typen en tegenstrijdige statussen bij dezelfde
Oracle-ID. De zichtbare catalogus en legaliteiten wisselen samen met de ontvangst in
één transactie. Tests onderbreken na de daadwerkelijke writes, ook met annulering.
Ontbrekende velden wissen de vorige bekende status in plaats van die te recyclen.

## ADR-027 — Geen ongekwalificeerd groen oordeel

De validator combineert minimum 60 mainboard, maximum 15 sideboard, verboden commanderzone,
vier exemplaren per Engelse kaartnaam over zones/printings, Basic Land en exact herkende
Engelse any-number/up-to-one-through-ten-uitzonderingen. Andere deckbouwclausules en
expliciete interchangeable-tekst blijven onbekend. Handmatige namen zijn geen bewijs
voor basislandstatus. Ontbrekend bezit is geen legaliteitsfout.

Scryfall Standard-status bepaalt rotatie/kaarttoegang; een op 22 oktober 2026 gecontroleerde
Wizards-banlijst vangt bekende bronafwijkingen en BO1 op. Die lijst wordt niet live gescrapet.
Een maximumbronleeftijd van 24 uur en hercontroletermijn van 14 dagen voor de ingebouwde
regels zijn conservatieve productkeuzes, geen garantie dat tussentijds niets verandert.
Bij overschrijding volgt Unknown. Een actuele catalogusverversing vernieuwt de ingebouwde
regelcontrole niet; daarvoor is een opnieuw gecontroleerde appversie nodig.
Voor mainboards boven 250 wordt de niet-bewezen Arena-clientlimiet expliciet als onbekend
gemeld. Het product claimt geen volledige spelregelengine of live Arena-acceptatie.

Een oordeel bevat controle-uur, bronversie, importdatum, brondatum, fouten en onzekerheden.
Profiel-/deck-/inhoudswijzigingen wissen het getoonde oordeel. Validatie gebruikt een
consistente leestransactie en een injecteerbare klok. DateTimeKind wordt bij het lezen
van UTC-datums uit SQLite hersteld om lokale tijdzoneconversie als brondatum te voorkomen.

## ADR-028 — Structurele alternatieven met zichtbare grenzen

Fase 6 gebruikt een vaste lokale rangschikking: reprints, kleuridentiteit, manawaarde,
hoofdkaarttype en stabiele identiteitsvelden. Een externe AI-dienst voegt zonder
betrouwbare evaluatie geen bewezen kwaliteit toe. Kaarttekst staat ter vergelijking
in de UI. De engine claimt geen semantisch gelijke effecten, synergie of hogere winkans.
De echte proef laat dit expliciet zien: Consult the Star Charts kan structureel bij
Negate passen maar is geen counter. Rolherkenning vereist een aparte meetbare vervolgstap.

Alleen ontbrekende exemplaren worden vervangen; andere kaarten alleen in het mainboard,
zonder nieuwe kleuren of landzijden. Sideboard en manabase krijgen alleen geverifieerde
reprints. Kandidaten en de volledige uitkomst gebruiken de bestaande profielvalidator,
inclusief onzekerheden, bans, kopie-uitzonderingen en bron-/regelvervalgrenzen.

## ADR-029 — Eén gezamenlijke voorraad en bewuste gedeeltelijke oplossingen

Exacte sideboardvoorraad krijgt eerst toewijzing, vervolgens het mainboard. Daardoor
kan de ruimere mainboardkeuze het tekort oplossen zonder exacte sideboardkaarten op
te maken. Doelvoorraad is alleen het werkelijk vrije lokale bezit; alle zones en keuzes
delen die voorraad. Reprints tellen alleen bij dezelfde geverifieerde Oracle-ID.

Een gedeeltelijke oplossing mag als nieuw deck worden opgeslagen. Overgebleven tekorten
blijven zichtbaar in het voorbeeld en de analyse. De gebruiker kan zo ook een deel van
het deck oplossen als bijvoorbeeld basislanden niet lokaal geregistreerd zijn. Geen
impliciete Arena-unlocks, wildcardberekening of reservering tussen verschillende decks.

## ADR-030 — Onveranderlijk voorbeeld en atomaire, idempotente nieuwe-deckopslag

Een voorbereiding bewaart naam, keuzes en SHA-256 van deck/profiel, relevante metadata,
bron, voorraad inclusief wijzigingsversies en engine-/regelversie. Anders dan een teller
alleen detecteert de voorraadversie ook veranderen en terugzetten. Bij verschil moet
opnieuw worden berekend. Ook zonder verschil worden tijdgebonden regels opnieuw getoetst.

Opslag voegt één nieuw deck toe en markeert dezelfde voorbereiding in één transactie.
Het originele deck en bezit worden niet overschreven. Een herhaalde aanvraag retourneert
hetzelfde resultaat; ook na verwijdering wordt geen tweede deck aangemaakt. Bron- en
resultaat-ID's zijn auditwaarden zonder FK-cascade. Onafhankelijke writer gates zijn
tegelijk getest tegen SQLite; fout/annulering na SQL-writes bewijst volledige rollback.
Er is nog geen UI-historie of opruimfunctie voor de bewaarde adviesvoorbereidingen.

## ADR-031 — Volledige tekstpatronen vóór brede rolclaims

De eerste rolmodule ondersteunt vier effectfamilies met een gesloten set volledige
Oracle-teksten. Witruimte wordt genormaliseerd; extra regels, herinneringstekst, modale
keuzes en niet ondersteunde permanente abilities blijven onbekend. Classificatie draagt
versie, regel-ID en uitleg. Dit kiest conservatieve dekking boven onzichtbare foutpositieven.
Negate/Essence Scatter zijn dezelfde familie maar verschillende doelgroepen; een betaal-
uitweg of andere relevante beperking blijft zichtbaar in een beperkte match.

Kosten en timing worden getoond, geen complete castbaarheids-/spelsimulatie. Rollen
wijzigen de bestaande geschiktheidsfilters niet. Onbekende of andere rollen blijven
bewust selecteerbare structurele kandidaten, met een waarschuwing bij keuze en voorbeeld.
Reprints houden de hoogste prioriteit. Daarna Matching, Limited, Unknown en Different,
gevolgd door de bestaande structurele sortering. Een rolmatch garandeert geen synergie.

## ADR-032 — Referentiescore scheiden van praktijkdekking

Dertig nagekeken echte Oracle-teksten vormen de vaste regressieset; tien aanvullende
rangschikkingsgevallen hebben verwachte uitkomsten. De oude sortering wordt op dezelfde
kandidaatpool gereproduceerd. De gemeten eerste-keuzescore stijgt 3/10 → 10/10, maar de
voorbeelden zijn gekozen tijdens ontwikkeling, met gesimuleerd bezit en legaliteit.
Geen onafhankelijke testset, algemeen kwaliteitspercentage of win-rateclaim.

Een aparte proef meet alle 5.164 Standard-legale kaartdefinities uit de echte snapshot.
Slechts 16 worden herkend (7 counters, 7 removal, 1 draw, 1 mana); 5.148 blijven onbekend.
Binnen het typebereik zijn 16 van 1.022 herkend. Deze beperking staat expliciet in README,
handleiding en oplevering. De ontwikkelscore mag deze geringe praktijkdekking niet verhullen.

## ADR-033 — Versioneerbare uitlegenvelop zonder schemawijziging

De bestaande ChoicesJson-kolom bewaart nu een object met engine-/classifierversies,
keuzes en de beoordeelde rolvergelijkingen, inclusief regel-ID's en uitleg. Geen nieuwe
tabel nodig. De maximale JSON-lengte in het domein is vergroot naar 2.000.000 tekens om gezamenlijke
uitleg te kunnen bewaren. De andere invoer-/keuzegrenzen blijven staan.

ManaCost en classifier-/engineversie horen bij de vingerafdruk. Oude ongebruikte
voorbereidingen worden niet gewist; de afwijkende versie verplicht tot herberekenen.
Oude toegepaste ontvangsten blijven hetzelfde resultaat retourneren. De envelop is
controleerbare auditinformatie; opslag berekent de actuele uitkomst opnieuw in plaats
van alleen het opgeslagen oordeel te vertrouwen. Alle eerdere rollback-/conflicttests
blijven van toepassing. Geen nieuwe migratie betekent ook geen extra upgradeback-up.

## ADR-034 — Onvolledige rol-aanwijzingen zijn geen volledige kaartkennis

De praktijkmeting van fase 7 (16/5.164) maakt alleen meer ontwikkelvoorbeelden scoren
onvoldoende. Fase 8 voegt gesloten samengestelde patronen en enkele permanente
abilities toe, maar scheidt volledige herkenning van gedeeltelijke aanwijzingen.
Evidence bewaart de exacte herkende passage; UnparsedText de gehele overige tekst.
Een aanwijzing krijgt nooit Matching, ook niet tegenover dezelfde aanwijzing op
een andere kaart. Mechanisme en bijkomende effecten blijven in de vergelijking.

Deze keuze geeft eerder nuttige leesaanwijzingen zonder te claimen dat voorwaarden
of kosten volledig zijn beoordeeld. Same-Oracle blijft eerst; bij gelijke Fit komt
volledige herkenning vóór gedeeltelijke herkenning. De versie-update naar engine v3
en classifier v2 maakt oude ongebruikte voorbeelden ongeldig zonder ze te verwijderen.
Toegepaste ontvangsten blijven idempotent. Geen schemawijziging.

## ADR-035 — Gereserveerde evaluatie vóór het bekijken van parseruitkomsten

Dertig andere Standard-kaarten zijn deterministisch per trefwoordstratum gereserveerd
vóór parserontwikkeling, met oude referentienamen en al bekeken praktijkkaarten
uitgesloten. Parser en vergelijking zijn vóór tekstinspectie gehasht en bevroren.
Daarna zijn verwachte families en voorwaarden door dezelfde ontwikkelaar gelabeld,
vóór de voorspellingen per kaart werden bekeken. Dit is geen onafhankelijke review
en de strata zijn niet representatief. De bron/keuze/hash staan in VALIDATION_PHASE8.md.

De uitkomst (0 volledig, 4 aanwijzingen, 26 onbekend; 15/19 relevante kaarten gemist)
wordt gepubliceerd zonder de parser achteraf op die kaarten te tunen. Nul verkeerde
families bij vier aanwijzingen bewijst geen algemene nauwkeurigheid. De praktijkmeting
van 43 volledig en 207 gedeeltelijk is dekking, geen juistheidsmeting van ieder geval.
Een volgende uitbreiding moet nieuwe ongeziene gevallen reserveren en bij voorkeur
een tweede beoordelaar gebruiken. Groene regressietests vervangen die kwaliteitsvraag niet.

## ADR-036 — Asynchrone UI-opdrachten afronden vóór testopruiming

De deckselectie kan een lopende LoadDeckCommand achterlaten na de laatste UI-assertie.
Een eerdere fase-8-run faalde bij verwijderen van de nog geopende SQLite-testdatabase.
De gedeelde testfixture wacht deze opdracht af vóór container-disposal en poolopruiming.
De opgeloste absolute opruimmap moet onder de tijdelijke MTGDeckbuilder.Tests-map
liggen. Geen retrylus die een echte fout verbergt, geen wijziging aan productiedata.
De daaropvolgende volledige runs slagen; de definitieve suite bevat 255 tests.

## ADR-037 — Schade herkennen zonder dodelijkheid te suggereren

Vaste gerichte schade is een ontbrekende praktische familie binnen removal. De
parser ondersteunt alleen exacte spellbron, vaste hoeveelheid en gerichte doelen;
overige kosten/effecten blijven als onvolledige resttekst staan. Variabele power,
fight, massale schade en speler-only schade worden niet als dit patroon geclaimd.
Hoeveelheid en doelgroep worden getoond, maar iedere damagevergelijking is Limited,
ook bij meer schade of gelijke teksten. Volledig geparseerd betekent niet dodelijk.

## ADR-038 — Modale opties afzonderlijk bewaren, geen onvoorwaardelijke combinatie

Een gesloten keuzestructuur met header vooraan en 2–8 bullets geeft afzonderlijke
CardRoleOption-objecten, inclusief onbekende opties. De parent blijft incomplete en
bewaart de gehele originele tekst. Costs en keuzeregels zijn niet volledig bewezen.
Roles verzamelt families; de enkelvoudige Role blijft Unknown voor deze parent.
Overlap levert hoogstens Limited, nooit Matching. Per-familietellingen kunnen
daardoor overlappen; rapporteer unieke kaarttotalen apart.

De opties passen binnen de bestaande JSON-envelop. Classifier v3/engine v4 voorkomen
dat oude ongebruikte voorbeelden zonder herberekenen worden toegepast. Oude toegepaste
ontvangsten blijven werken; schema en voorraad-/formaatregels blijven gelijk.

## ADR-039 — Nieuwe evaluatie met vóór implementatie vastgelegde baseline

De vorige gereserveerde set is door kennis van de resultaten voortaan regressiemateriaal.
Voor fase 9 zijn dertig nieuwe kaarten op ID-hash gereserveerd en v0.8-uitkomsten vooraf
vastgelegd. Parser, vergelijking en rangschikking zijn vóór tekstinspectie bevroren;
verwachte families/voorwaarden zijn door dezelfde ontwikkelaar vóór voorspellingen
gelabeld. Geen onafhankelijke reviewer; strata zijn niet representatief.

De nieuwe set stijgt van 3 naar 8 kaarten met enige relevante herkenning; 13/21 blijven
gemist. Eén volledig en zeven gedeeltelijk, geen verkeerde familie vastgesteld.
Dit ondersteunt een beperkte verbetering op deze set, geen algemene precisie,
volledige meerlabel-recall of hogere winkans. De parser en labels zijn na de uitkomst
ongewijzigd; hashes en details staan in VALIDATION_PHASE9.md.

De evaluatierun vond daarnaast een bestaande UI-test die RefreshAsync rechtstreeks
aanriep buiten de UI-bewerkingsgrens. De bindingselectie kon de daarna gevraagde
validatie wegens IsBusy verdringen. De test vernieuwt nu via NavigateCommand, dezelfde
route als de interface. Geen productiewijziging of sleep/retry om de fout te verbergen.

## ADR-040 — Eén expliciet beoordeelde kaartpool vóór verdere algemene tekstuitbreiding

De fase-9-dekking bleef beperkt. Fase 10 richt daarom één eindige kaartpool in voor
mono-rood aggro in Arena Standard BO1: één kleur, vroege druk, aanvallende spell-payoffs
en burn. Dertig kaarten, waarvan 26 niet-landen plus Mountain ondersteund en drie
bewust uitgesloten. Iedere kaart draagt exacte opgeslagen metadata, functie,
voorkeur, doel en voorwaarden. Dezelfde ontwikkelaar (Codex) beoordeelt; geen claim
van onafhankelijke menselijke review, algemene Oracle-kennis of bewezen kaartsterkte.

De pool is een embedded versieerbare resource in Application. Oracle-ID en hash over
de opgeslagen velden zijn vereist; geen naam-only/fuzzy match. Wijzigingen verplichten
tot nieuwe beoordeling. De afdruk omvat geen zelfstandige power/toughness omdat die
nog niet worden opgeslagen; de pool is geen volledige simulatie van kaartwerking.

## ADR-041 — Bron-/legaliteitscontrole hergebruiken in een afzonderlijke leeswerkbank

De pool gebruikt dezelfde StandardDeckValidator voor bron-/regelactualiteit, metadata,
Arena-beschikbaarheid en BO1-ban-/legaliteit. Bij één kaart wordt alleen main-min
genegeerd, omdat de werkbank geen compleet deck beoordeelt. Overige fouten/onzekerheden
blokkeren. Een paper-Standard-legale Leyline of Resonance blijft hierdoor in BO1 geblokkeerd.

De workbench leest opnieuw vóór zoeken, schrijft geen decks en reserveert geen bezit.
Totale geregistreerde en actueel bruikbare aantallen staan apart. Adviezen vereisen
dezelfde beoordeelde functie en sorteren op vaste voorkeur/manaverschil/naam/ID.
Hoofdkaarttypen kunnen hierbij verschillen; waarschuwingen tonen timing/kosten en
andere voorwaarden. De generieke deckalternatieven en hun strengere voorraad-/
formaatcontrole bij opslag blijven afzonderlijk. Geen schemawijziging of extra pakket.

## ADR-042 — Referentiewensen vooraf vastleggen en meetgrenzen benoemen

Pool en twintig inhoudelijke referentiegevallen zijn vóór rangschikkingsimplementatie
vastgelegd en sindsdien bytegewijs gelijk. Dezelfde aangeboden kaartsets geven vijf
gewenste eerste keuzes generiek tegenover twintig met de pool. Dit meet filtering
op deckfunctie én rangschikking: de geschiktheidsfilters verschillen. De beoordeling
en gevallen zijn van dezelfde ontwikkelaar, met gesimuleerd bezit/legaliteit.
Geen onafhankelijke, ongeziene of representatieve benchmark of win-rateclaim.

Een afzonderlijke echte online catalogusproef bevestigt alle 27 ondersteunde kaarten
en drie uitsluitingen, plus uitlegbare opties en behoud na herstart. Dit bewijst
bronverwerking en de bedoelde afbakening; gameplay/volledige mana of alle Standard-
alternatieven worden niet bewezen. Exacte bestanden, hashes en controleomstandigheden
staan in VALIDATION_PHASE10.md.

## ADR-043 — Filters mogen na herlezing geen advies aan de verkeerde bronkaart koppelen

Als tijdens het bezitfilter extern de voorraad van de gekozen bronkaart naar nul
gaat, kan herlezing een andere zichtbare kaart selecteren. De oorspronkelijke
adviesaanvraag mag dan geen opties tonen naast die nieuwe selectie. De viewmodel
breekt af met opnieuw kiezen; een gebonden UI-regressie oefent dit scenario uit.
Geen oude cache of vertraagd resultaat als actuele keuze presenteren.

## ADR-044 — Een vaste decktemplate met harde grens tussen bezit en planning

Fase 11 levert één deterministisch mono-rood BO1-voorstel per berekening: 22 Mountains,
20 creatures en 18 niet-creature-spreuken, met exacte functietellingen, minstens twaalf
1-drops en gemiddelde niet-land-MV maximaal twee. Dit beperkt de combinatoriek en maakt
mislukking uitlegbaar. Het is een ontwikkelheuristiek, geen bewezen sterk of optimaal deck.
Ontoereikende functies worden niet met willekeurige legale kaarten opgevuld.

Standaard bestaat iedere gekozen kopie in het actuele geregistreerde bezit. Planning
vereist een afzonderlijke zichtbare keuze en mag alle zestig kaarten missen. Basislanden
zijn niet impliciet gratis/onbeperkt. Dezelfde Oracle-ID deelt één vierkopieënlimiet over
printings; daarna exacte voorraadallocatie. Andere decks reserveren geen voorraad.
Bezit gaat vóór poolvoorkeur/MV/naam; curveherstel blijft binnen dezelfde functies.
Gedrukte mana en getoonde voorwaarden bewijzen geen castingkans of gameplay-synergie.

## ADR-045 — Generatorvoorbeelden krijgen een eigen migratie en atomair opslagbewijs

DeckGenerationPlan beschrijft een nieuw voorstel zonder oorspronkelijk deck. Daarom een
eigen tabel via AddDeckGenerationPlans en geen misbruik van DeckAdvicePlan.SourceDeckId.
De controlecode omvat bron, printings, relevante collectieversies, poolmetadata en engine/
regelversies. Dezelfde bronlezer wordt met de poolwerkbank gedeeld. Hercontrole op tijd
staat los van de hash, zodat veroudering zonder mutatie opslag alsnog blokkeert.

Voorbereiden schrijft alleen een versieerbare aanvraag met exacte regels en uitleg.
Toepassen bouwt opnieuw, vergelijkt en maakt het deck plus ontvangst in één transactie.
Fout/annulering na writes moet alles terugrollen. Een dubbele toepassing, ook vanuit
afzonderlijke providers, retourneert dezelfde ID. Het resultaat verwijderen behoudt
de historische ontvangst en veroorzaakt geen hercreatie. Geen voorraad- of deckoverschrijving.

## ADR-046 — Technisch complete voorstellen zijn geen bewijs van strategische kwaliteit

Acceptatiecriteria en grensgevallen zijn vóór implementatie in PROJECT_PLAN.md en
DECK_GENERATION.md vastgelegd. De automatische tests beoordelen technische grenzen en
de vaste template. Een echte actuele catalogusproef gebruikt gesimuleerd bezit en
controleert bron, selectie, herstart, stale preview, exacte opslag en export.
Geen ongeziene benchmark, onafhankelijke reviewer of gespeelde wedstrijden. De beperkte
fase-10-pool en alle voorwaarden blijven zichtbaar; de algemene parser wordt niet uitgebreid.

## ADR-047 — SQLite-snapshots en exact bekende schema's voor herstel

Een online back-up moet ook committed WAL-data meenemen. Daarom de SQLite-backup-API,
geen losse bestandskopie van de actieve database. Nieuwe handmatige kopieën verschijnen
pas na integriteits-/foreign-key- en schemacontrole. Historische/externe bestanden worden
vóór herstel opnieuw gecontroleerd en in een eigen onveranderlijk bedoelde reviewkopie
vastgelegd. Hashcontrole bij plannen en toepassen vangt wijzigingen op.

Een migratienaam alleen bewijst compatibiliteit niet. Het schema wordt exact vergeleken
met een tijdelijke database opgebouwd uit de meegeleverde migraties tot dezelfde prefix.
Onbekende versies, vreemde triggers/indexen of andere schemawijzigingen blokkeren. Dit
is bewust conservatief en geen database-authenticatie of cryptografische handtekening.

## ADR-048 — Herstel bij startup, met veiligheidskopie en herhaalbaar journaal

Terugzetten vervangt alle databasegegevens en vereist daarom een zichtbaar herstelvoorbeeld
plus afzonderlijke bevestiging in de UI. Het wordt uitgevoerd bij de volgende appstart,
vóór databasegebruik. Een exclusief datamapslot houdt andere desktopinstanties buiten.
De eerste veiligheidskopie en hash blijven bewaard bij herhaling. Scheduled/Applying/
Applied in een atomair geschreven journaal bewaken de startvolgorde. Een fout na commit
maar vóór ontvangst laat geen nieuwe gebruikersacties toe; de volgende start herhaalt
dezelfde kopie. Een voltooide ontvangst wordt niet opnieuw toegepast.

De actieve database wordt niet hernoemd en sidecars worden niet los verwijderd. SQLite
beheert de terugzettransactie. Dit vermijdt een eigen meerbestanden-rollbackprotocol.
Daartegenover staat een expliciete grens: de huidige database moet nog leesbaar zijn
voor een geldige veiligheidskopie. Fysieke corruptie vereist nog handmatige noodhulp;
er is geen automatische reparatie. Cache/logs vallen niet in de databaseback-up.

## ADR-049 — Praktijkbewijs met echte bron en eerlijk gesimuleerde collectie

De gebruiker heeft bevestigd dat er geen echte export beschikbaar is. Fase 12 gebruikt
daarom een actuele echte Scryfall-catalogus met gesimuleerd bezit/decks en bestaande
historie in een nieuwe geïsoleerde database. Metingen vergelijken alle applicatietabellen
plus migratiehistorie, niet alleen zichtbare aantallen. Herhaalde berekeningen en werkelijk
native startup-herstel leveren technische betrouwbaarheidsevidence. Geen echte account-
of gameplaytest, onafhankelijke kwaliteitsreview of algemene snelheidsclaim.

## ADR-050 — Zelfstandige x64-publicatie en per-user installatie

De beperkte v1.0 krijgt een meegeleverde .NET-runtime: de gebruiker hoeft geen SDK te
installeren. Geen trimming/single-file/AOT, omdat die extra reflectie-/extractierisico's
toevoegen zonder bewezen voordeel voor deze desktopapp. Een portable zip en Inno Setup-
installer gebruiken dezelfde publicatie. Per-user installatie vermijdt administratorplicht.
Een vaste AppId en standaardpad ondersteunen upgrades; de aparte gegevensmap blijft behouden.

## ADR-051 — Ongetekende lokale release met concrete testgrenzen

Er is geen vertrouwd signing-certificaat. Daarom geen uitgeversvertrouwen claimen,
geen beveiligingsbeleid verlagen en geen release automatisch online publiceren. SHA-256
controleert bestandsintegriteit, niet herkomst. De compilerdownload is gecontroleerd
tegen de officiële releasehash en heeft een geldige Authenticode-handtekening.
De gegenereerde appinstaller heeft bewust geen handtekening.

Installerproeven controleren echte processen, runtimepad, gegevens en verwijdergedrag.
Een opgebouwde 0.99.0-versiefixture test het updatepad; dit wordt niet voorgesteld als
een authentieke eerdere release. De gevulde oude databaseschemafixture test wel de
werkelijke migratie van zes naar zeven migraties. Geen schone-machine-, ARM- of brede
Windows-certificering. Runtime- en regelupdates blijven onderhoudswerk na deze fase.

## ADR-052 — Fysiek Magic als productfocus, Arena als bewaard archief

27 oktober 2026, expliciete gebruikersopdracht. Geen conversie van digitale aantallen
naar fysiek bezit: vijf eigen tabellen en een nieuwe standaardwerkruimte. Dat voorkomt
verzonnen eigendom en bewaart alle bestaande gegevens en geaccepteerde oude functies.
De naam wordt MTG Smart Deckbuilder; technische solution-/projectnamen, installatie-AppId
én gegevensmap blijven gelijk voor compatibele upgrade. Mono-rode Arena-deckgeneratie
blijft archieffunctionaliteit, geen fysieke Conflux-generator.

## ADR-053 — Exacte scanidentiteit en absolute aantallen per positie

CSV via framework-parser, geen regex-/kommasplit. Koppeling op exacte Scryfall-ID of
set+nummer+taal; bij ambiguïteit expliciete keuze, geen gok op naam. Finish, conditie,
taal en binder onderscheiden fysieke posities. Dubbele identieke regels worden zichtbaar
opgeteld, vervolgens absoluut toegepast. Omitted posities blijven behouden; binderhernoeming
is geen automatische verhuizing. Bronvelden blijven in ontvangst bewaard. Preview/version/
stock-fingerprint en before/after-ontvangst beschermen commit en herstel. Proxy en deck/list
zijn geen echt bezit. Demo en eigen voorraad worden niet bij elkaar opgeteld.

## ADR-054 — Bevroren productvoorbeeld en on-demand Scryfall

MTGJSON levert twee Conflux-introproductlijsten; Scryfall levert echte metadata voor
54 exacte uitgaven. Elk productdeck heeft 41 vaste kaarten. Geen boosterkaarten of
extra kopieën verzinnen om 60 te bereiken. Embedded JSON laat de demo offline starten;
nieuwe CSV-identiteiten en zoeken gebruiken Scryfall. Afbeeldingen zijn begrensd en
gecachet, op geselecteerde printing/zijde gecontroleerd en buiten de UI-thread gedecodeerd.
Geen claim dat een productvoorbeeld een echte telefoonexport of gameplaytest is.

## ADR-055 — Papieren controle en begrensd advies

Casual60 controleert structuur/bezit, Standard gebruikt papieren Scryfall-legaliteit
zonder Arena-bans of Arena-printingeis. Identiteit/copy limits over Oracle-uitgaven,
onbeperkte basic/snow basic lands en onzekerheid bij kaartgebonden uitzonderingen.
Bronactualiteit (24 uur) en regelonderhoud (14 dagen) blijven expliciet. Domain/exalted,
mana en curve zijn beschrijvend; geen algemene synergie- of winkansclaim. Bestaande
beperkte rolherkenning kan lokale opties tonen. Tekstimport/kopie/vervanging krijgen een
review en een nieuwe deckidentiteit met voorraad-/metadatahercontrole; geen bronoverschrijving.

## ADR-056 — Fysieke vervanger voor de begrensde Arena-generator

Om de bestaande voorstelroute bij de fysieke omschakeling bruikbaar te houden krijgt
fysiek een eigen bevroren Conflux-pool. Eén Casual60-plan (36 niet-landen/24 basislanden),
bezitmodus standaard, planning expliciet, maximaal vier over Oracle-printings en
vingerafdrukcontrole van metadata/voorraad. MV-/naamselectie en een eenvoudige pip/land-
verhouding zijn zichtbaar; ze bewijzen geen speelsterkte of optimale manabase. Nieuwe
sets en algemene synergie vallen buiten dit gecontroleerde model. Geen fictief bezit,
geen Arena-legaliteit op het papieren voorstel en geen brondeckoverschrijving.

## ADR-057 — Commander naast Casual60, met expliciete planning

De gebruiker breidt fase 14 uit met Commander en de FINAL FANTASY Starter Kit. De actuele
gelezen CR 903.3–903.5 (25 september 2026) laat ook legendary Vehicles en geschikte
Spacecraft als commander toe. Eén ondersteunde commander, 99 librarykaarten, singleton,
kleuridentiteit/basislandtypen, geen sideboard en actuele papieren Commander-legaliteit.
Twee commanders, variabele kleurkeuze, brackets en niet-herkende uitzonderingen niet
stilzwijgend toestaan. Dit is een controle van ondersteunde deckbouwregels, geen spelengine.

Een vast plan (commander + 62 singleton niet-landen + 37 basics) bewaakt voorraad per
Oracle én exacte printing. Bezitmodus standaard. Planning mag gericht maximaal 150
verschillende niet-landen plus tien basislanden bij Scryfall opvragen, zonder bezit te
wijzigen. Beperkte tekstaanwijzingen en gedrukte MV vormen een uitlegbare heuristiek,
geen bewezen synergie/sterkte. Alle tekorten en kaartregels vóór opslag zichtbaar.
Save controleert ook nieuw bronverval tussen review en commit. Fysieke decktekst houdt
collectornummer-case intact; oude Arena-parsing behoudt bestaand gedrag.

## ADR-058 — Starter Kit als afzonderlijke idempotente producttoevoeging

MTGJSON BlueBlack_FIN/RedWhite_FIN met de officiële Wizards-decklijsten vergeleken:
55 exacte uitgaven, 55 bronregels en 120 speelkaarten, inclusief twee foil-hoofdkaarten.
Een eigen marker voegt uitsluitend ontbrekende Starter Kit-demo toe; bestaande Conflux-
voorraadversies en echte CSV-/handmatige invoer blijven behouden. Demo blijft apart van
echte voorraad. Totale productdemo: 202 speelkaarten, 109 uitgaven, 118 posities en vier
decks. Vier tokens en Arena-codes apart vermelden, niet als deckkaart/bezitaantal meetellen.
Twintig tabellen/acht migraties blijven gelijk. Back-up en rijvergelijking vóór/na de
toevoeging bewijzen gegevensbehoud op de twee reeds bestaande databases.

## ADR-059 — Arena-interface verwijderen met behoud van upgradegegevens

De gebruiker vraagt het online Arena-gedeelte te verwijderen. Alle actieve navigatie,
views, viewmodels en desktopregistraties voor die werkruimte zijn verwijderd uit het
productieproject. Historische UI-regressies gebruiken kopieën onder Tests/Legacy; geen
verborgen archiefroutes in het programma. Oude opslagmodellen/migraties blijven voor
back-up, herstel en gegevensbehoud. Zij worden alleen bij bewust Database legen gewist.
Release bevat alleen de actuele fysieke gebruikershandleidingen; oude Arena-handleidingen
blijven als projecthistorie in de bronmap. Aangeleverd logo exact bewaard en naar PNG/ICO
geconverteerd; Windows-icoon gebruikt het herkenbare oranje symbool.

## ADR-060 — Filters en wizard onderscheiden aanwijzingen van harde regels

Collectie filtert geprinte kleur; deckbouw begrenst kleuridentiteit. Alle collectiefilters
worden gecombineerd, serie-dropdown komt uit actieve voorraad. Synergie is zeven beperkte
tekst-/typefamilies en geen positieve combo- of volledigheidsclaim.

Wizard: formaat, kleuren, thema/stijl, bezit/planning en optionele commander, volledige
review/bevestigde nieuwe-deckopslag. Beperkte rangschikking blijft gescheiden van harde
voorraad-, Oracle-, kleur-, formaat- en broncontroles. Planning zoekt maximaal drie
MV-banden van vijftig, tien basislanden en optioneel vijftig themaresultaten; geen
uitputtende pool of globale optimalisatie. Casual-aanvulling gebruikt Commander-legale
papieren kaarten als afgebakende zoekpool; eigen Casual-kaarten zijn niet zo beperkt.
Standard/Commander controleren actualiteit opnieuw bij opslag, ook als de preview eerst
wel geldig was. Geen speelsterkte-, combo-, managarantie of automatische aankoop.

## ADR-061 — Expliciet legen met reservekopie en blijvend uitgeschakelde demo

Geen bestandverwijdering. Snapshotpreview over alle modeltabellen, exact WISSEN, twee
vingerafdrukcontroles, bestaande schrijfvergrendeling, verplichte consistente SQLite-kopie
met integriteits-, FK- en exacte schemacontrole en ondersteunde herstelgrootte vóór iedere delete. Eén transactie leegt
in FK-volgorde alle twintig tabellen, vervolgens standaardinstellingen met demo uit.
Bij fouten/annulering vóór commit rollback van alle SQL. Herhaald succesvol verzoek is
idempotent en mag later toegevoegde data niet nogmaals wissen. Gepland herstel blokkeert.
Back-ups/cache/logs en schema/migraties blijven bestaan. Migratie negen voegt één bool
ExamplesEnabled toe, default true zodat een gewone upgrade bestaande werking bewaart.
De gewone gebruikersdatabase wordt hiervoor nooit als test gebruikt.

## ADR-062 — Voorwaardelijke kaartrelaties in plaats van woordoverlap

Fase 16 gebruikt afzonderlijke feiten voor eigen/vijandelijke acties, producer/payoff,
typen tokens, kosten, triggers, verwijdering en targetbeperkingen. SynergyEngine bouwt
gerichte relaties en conflicten tussen verschillende Oracle-kaarten; kopieën vergroten
dit aantal niet. Gewicht één is basiscompatibiliteit, geen bijzondere strategische
synergie. SynergySelection gebruikt actuele gekozen kaarten, begrenst contextbeloning
en bewaakt creature-/Equipment-verhouding. Harde voorraad-/identiteits-/formaatcontroles
blijven afzonderlijk. Geen algemene spelsimulatie, winrate of combo-/optimaliteitsclaim.

## ADR-063 — Gecontroleerde productfeiten met gameplayvingerafdruk

Voor de 87 unieke Oracle-kaarten uit de vier productlijsten zijn ondersteunde kenmerken
en voorwaarden handmatig vastgelegd. SHA-256 over identiteit, tekst, typen, kosten,
kleuren en power/toughness voorkomt stilzwijgend gebruik bij gewijzigde gameplaymetadata.
Gewijzigd betekent typefeiten en herbeoordeling, geen automatische equivalentie.
Overige kaarten krijgen beperkte patroonherkenning met bewijsclausules en expliciet
onbekende restwerking. Optionele stats komen uit bestaande JSON, dus geen migratie.

## ADR-064 — Evaluatiesets worden na inspectie ontwikkelmateriaal

Voor elke eerste proef bronidentiteiten reserveren, implementatie bevriezen en voorspellen
vóór tekstinspectie. Eerste metingen waren respectievelijk 9/26, 21/28 en 14/23 relevante
labels; de tweede vond bovendien twee onveilige samengevoegde kaartvormen. Nul onterecht
toegekende labels in deze kleine sets bewijst geen algemene precisie. Dezelfde ontwikkelaar
implementeerde en beoordeelde; selectieve samples zijn niet representatief. Na verbeteringen
hergebruikte scores expliciet regressie/ontwikkeling noemen. Themametriek alleen mist
regel-, mana- en strategiefouten: daarom adversariële én live complete-deckproeven.

## ADR-065 — Onvoldoende gemodelleerde kaartvormen en verliesvoorwaarden begrenzen

Multi-face, Adventure en split niet samenvoegen tot één kosten-/effectmodel; tijdelijk
geen automatische generatie of thematisch alternatief, handmatig gebruik blijft.
Expliciete eigen verlies-/winblokkades ook uitsluiten van automatische selectie, omdat
geen veilige uitweg wordt beoordeeld. Bij handmatige decks waarschuwen. Deze gerichte
guard is geen volledige detectie van nadelen. De live-proef vond onder meer Abyssal
Persecutor en Archfiend of the Dross; structuur/legaal alleen was dus onvoldoende.

## ADR-066 — Beperkte aanvulpool op populariteit, kosten los van manaoutput

Scryfall-aanvulling behoudt bestaande grenzen (themapool en drie MV-banden van vijftig,
tien basics) maar gebruikt EDHREC-populariteit zoals door Scryfall aangeboden, in plaats
van de eerste alfabetische namen. Populariteit is geen eigen kwaliteitsmeting of
geschiktheidsbewijs voor Commander of Standard. Selectie beoordeelt daarna lokaal.
Geen kleurvoorkeur zoekt beschikbare spell-/landcapaciteit binnen minder castingkleuren;
Domain mag breder. Gedrukte en geactiveerde gekleurde kosten tellen mee, manaoutput niet.
Geen castingkans-/landoptimaliteitsclaim of automatische aankoop.

## ADR-067 — Schaalbare selectie via feitgroepen en actuele context

Geen complete Oracle-paargraph voor 33.727 kaarten. Bounded immutable gameplayfeiten
delen op fingerprint; caller behoudt printing, beeld en bronactualiteit. Lazy numerieke
relaties delen alleen traits die Between werkelijk gebruikt: kenmerken, targets,
gedrukte kleuren, type, power-/MV-grenzen, namen en afwijkende kaartvormen. De echte
relatie-uitleg blijft kaartgebonden. Zelf-Oraclebijdragen apart aftrekken; kopieën tellen
niet als nieuwe paren. Bovengrenzen mogen zoeken uitsluitend stoppen bij een bewezen
maximum. Nieuwe predicate vereist key/upper-bound en differentiële tests. Geen first-N
truncatie. Ook semantische groepen kunnen groot worden; dit is geen constante resourcegarantie.

## ADR-068 — Aangenomen allesbezit en eerlijke evaluatie

Default Cards fysieke printings dedupliceren; een voorkeursprinting uit Oracle Cards
is niet gegarandeerd papier. Bevroren datum 28 oktober 2026, één printing per Oracle,
vier niet-basics/250 basics uitsluitend in afzonderlijke gemarkeerde SQLite-fixture.
Geen wijziging van echte collectie. A/B eerst reserveren/voorspellen, dan annoteren;
ieder 62, omdat twee strata leeg waren. Eerste resultaten A15/52 en B22/57; na inspectie
ontwikkelmateriaal, geen onafhankelijke algemene precisie. Bloodlord-waarheidsfout met
bewaarde oorspronkelijke annotatie corrigeren; geen stille scoreverandering. Complete
deckstructuur bewijst geen goede samenhang: afzonderlijk samenstelling/voorwaarden inspecteren.

## ADR-069 — Begrensde expliciete commanderfocus en graveyardbalans

Een generieke tokenbeloning selecteerde nauwelijks Angels voor Giada of spells voor
Talrand. Herken letterlijke eigen creaturetype-/castafhankelijkheden met actuele
Scryfall-typevocabulaire; kaartnamen en artifactsubtypen zijn geen creaturetypes.
Tokenproductie telt alleen waar passende eigen tokens volstaan, nooit als een cast.
Stuur een haalbaar supportdoel tot de helft van niet-landslots, met begrensde verdere
voorkeur; benoem werkelijk herkende ondersteuning. Graveyard-setup boven circa een
kwart slots progressief afremmen. Dit zijn constructieheuristieken, geen optimale
verhoudingen, counters/proliferate-model of bewezen commander-archetypekennis.

## ADR-070 — Grote collecties en verse brondata batchgewijs

UI-filterwerk boven 500 uitgaven op workerthread, annulering per zoekgeneratie en
alleen nieuwste resultaat committen. Vorige weergave blijft zichtbaar met status.
Voor Standard/Commander eerst alle actieve bezitstimestamps in één korte context lezen;
uitsluitend oude of onwaarschijnlijk toekomstige bronnen afzonderlijk verversen. Geen
33k contexten/networkcalls voor al verse bulkdata. De tijdelijke proeflauncher weigert
verouderde snapshots; dit is geen nieuw automatisch bulkimport-/updatesysteem.
Bronchecks blijven hard. Geen migratie, nieuw pakket of verkapte verhoging van bezit.

## ADR-071 — Bronrepository los van lokale gegevens en onderzoeksartefacten

Op 29 oktober 2026 heeft de gebruiker de GitHub-migratie en openbare plaatsing in de
bestaande Xenxs/MTG-Builder-repository expliciet opgedragen. Alle bronprojecten,
migraties, tests, kleine referenties, assets en documentatie opnemen; .local/artifacts,
gebruikersdata, logs en sleutelbestanden uitsluiten. Lokale werkmap niet verwijderen.
Geen nieuwe applicatielicentie of nieuwe enginefase impliceren. Bestaande Windows-CI
behouden, brongebruik in README uitleggen en een schone kopie werkelijk controleren.
Een historische lokale testpass is geen bewijs van GitHub Actions-succes. Onderzoeks-
en distributieartefacten blijven lokaal; eventuele GitHub Release-assets zijn afzonderlijk.

