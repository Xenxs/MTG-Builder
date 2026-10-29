# Vooraf vastgelegde brede proef — fase 17

28 oktober 2026. Opdracht: neem alle huidige kaarten als bezit aan en onderzoek/verbeter
de engine opnieuw. Scope vóór tekstinspectie van de nieuwe catalogus/evaluatie vastgelegd.
Advies GPT-6.1 Sol / High, 4–8 actieve uren. Geen ondersteunde modelbediening gevonden,
niets gewijzigd. Geen subagents of nieuwe chats. Stop na fase 17 voor beoordeling.

## Bronomvang en bezit

Alle inmiddels uitgegeven papieren speelkaarten uit de actuele Scryfall Default Cards-
bulk, release_date uiterlijk 2026-10-28. Eén fysieke printing per Oracle-ID, bij voorkeur
Engels en recent. Geen digitale-only kaarten, tokens/emblems/art-cards, meld-resultaten,
Plane/Scheme/Vanguard/Conspiracy/Dungeon/Phenomenon of andere niet-deck-speelobjecten.
Meerdere zijden blijven in de catalogus, maar bestaande automatische-selectiebeperkingen
blijven gelden totdat correct per kant modelleren mogelijk is. Leg alle uitsluitingen
met aantallen vast; catalogus omvat oudere sets, niet alleen Standard.

Vier aangenomen exemplaren per niet-basic Oracle-kaart en 250 per basic Oracle-kaart.
Dit maakt geen uitzonderingen op vierlimiet/singleton/legaliteit. Geen normale voorraad
overschrijven. Verse afzonderlijke SQLite-fixture met grote testmapnaam en kopie van
bronmanifest/hash, label "gesimuleerd bezit"; geen echte scan claimen.

Default Cards boven Oracle Cards: laatstgenoemde kiest één herkenbare printing, die
niet gegarandeerd papier is. Alle fysieke uitgaven eerst dedupliceren. Bulkdescriptor,
timestamp, downloadbestand en SHA-256 bewaren. Niet voor iedere kaart losse API-calls.

## Beoordeling vóór aanpassen

- Ongewijzigde v1.3.0-code/binaries bewaren. Alle geselecteerde Oracle-kaarten eenmaal
  analyseren: gecontroleerd/beperkt/type-only/gewijzigd/afgewezen, rollen, formaten,
  layout en tijd/geheugen. Herkenning is geen volledigheid of accuratesse.
- Meet oude selectie op groeiende, vaste invoer (100/250/500/1000). Een begrensde
  subprocessproef mag grotere invoer maximaal 60 s / 1 GiB geven; rapporteer begrenzing
  eerlijk. Geen miljard paren laten aanmaken op de gewone desktop om een vastloop te bewijzen.
- Reserveer vóór menselijke Oracle-inspectie een aparte evaluatieset: 48 willekeurig
  ogende gevallen via vaste SHA-256-volgorde van Oracle-ID (seed phase17-holdout-v1),
  buiten productpool en de drie eerdere sets. Daarnaast 16 gereserveerde gevallen
  verdeeld over minder gebruikelijke layouts/tekstlengten. IDs/hashregel opslaan.
  Voorspel met bevroren code vóór inspectie, leg waarheid/voorwaarden/fouten daarna vast.
  Dezelfde ontwikkelaar beoordeelt; geen onafhankelijke of representatieve benchmarkclaim.
- Gebruik eerder gevonden negen gemiste labels plus afzonderlijke adversariële gevallen
  als expliciet ontwikkelmateriaal. Nieuwe gebruikte gevallen verliezen hun ongeziene status.

## Acceptatie

- Geen voorafgaande volledig verbonden kaartgrafiek. Geen verborgen eerste-50 alfabetische
  beperking om "alle kaarten" te claimen. Alle geschikte invoer moet bij selectie kunnen
  meedingen; geef eventuele begrenzingen en tijd/geheugen eerlijk op.
- Meet relaties/voorwaarden juistheid apart van kaartthemalabels en deckstructuur.
  Negatie, begunstigde, kaarttypen, kosten, targetrestricties, voorgeschreven getallen,
  geneste abilities en onbekende resttekst mogen niet door woordtreffers worden gladgestreken.
- Nieuwe kennis breidt algemene patronen uit met bewijs en grenzen; geen handmatig
  positief label voor een benchmarkkaart om de score op te krikken.
- Vergelijk kleine-poolrangschikking vóór/na schaalwijziging; verander inhoudelijke
  rangschikking bewust en met een reden, niet stilzwijgend door een cache/index.
- Grote-poolproeven: Casual en Standard voor relevante thema's/stijlen en kleuren;
  Commander mono-, twee-, drie- en vijfkleurig plus kleurloos waar ondersteund.
  Bezitmodus, juiste kleuren, legale actuele kaarten, exact 60/100, singleton/vierlimiet,
  geen tekorten of uitgesloten layouts/verliesblokkades. Geen speelsterkteclaim uit
  structuurcontrole of een hoger aantal kaartrelaties.
- Opslag/herstart via echte SQLite in de afzonderlijke all-cards-fixture. Gewone database
  alleen snapshot/auditen en normale appstart; alle twintig tabellen/negen migraties behouden.
- Volledige Verify.ps1 -Screenshots, beelden bekijken, relevante native-/pakketcontrole
  bij codewijzigingen. Rapportage met nulmeting, ontwikkel- versus ongeziene resultaten,
  bekende missers en feitelijke tijd/geheugen. Geen volgende fase zonder opdracht.
