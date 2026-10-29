# Mono-rood BO1 — deckvoorstellen (fase 11)

Vooraf vastgelegd op 23 oktober 2026, vóór implementatie. Dit is een beperkt
ontwikkelplan binnen de beoordeelde fase-10-pool, geen geteste competitieve decklijst.

Het plan heeft 60 mainboardkaarten: 22 Mountains; 12 EarlyThreat, 4 SpellAttacker,
4 SpellPinger/ReachCreature, 8 BurnReach, 2 CreatureInteraction, 6 CombatSupport en
2 CardFlow. De functies tellen samen 20 creatures en 18 niet-creature-spreuken.
Minimaal 12 spreuken kosten gedrukt 1 mana; gemiddeld niet-land-MV maximaal 2.
22 onvoorwaardelijke rode bronnen is een vaste heuristiek, geen mulligan- of
kansberekening. Aanvullende kosten/voorwaarden blijven expliciet zichtbaar.

Alleen bezit is de standaard. Planning moet expliciet gekozen worden en toont alle
ontbrekende exemplaren, inclusief Mountains. Er wordt geen voorraad verbruikt,
geen wildcard besteed en geen vrij bezit over meerdere decks berekend.

Voorkeuren: werkelijk bezit vóór ontbrekende exemplaren, daarna poolvoorkeur,
gedrukte manawaarde en stabiele naam/identiteit. Begrensde functiekeuze met zo nodig
vervanging door 1-drops voor de curve; geen uitputtende strategische optimalisatie.
Maximaal vier per Oracle-ID, Mountains uitgezonderd. Bestaande poolgeschiktheid,
metadata, catalogusactualiteit en Standard BO1-regels blijven verplicht.

Vooraf vastgelegde acceptatiegevallen staan in PROJECT_PLAN.md. Ze zijn
ontwikkelcontroles; geen onafhankelijke of ongeziene benchmark van speelsterkte.

## Gebruik

1. Werk de catalogus online bij via Catalogus en registreer werkelijk bezit met de
   bestaande collectievelden of collectie-import. Een catalogusimport voegt geen bezit toe.
2. Open **Deckvoorstel**. De pagina berekent standaard alleen met actueel geregistreerd bezit.
   Bij onvoldoende kaarten staat per functie hoeveel ontbreken; er wordt geen compleet
   deck geclaimd. Gewijzigde poolmetadata wordt uitgesloten, niet stilzwijgend opnieuw beoordeeld.
3. Kies eventueel expliciet **Planningsmodus** en bereken opnieuw. Deze mag ontbrekende
   exemplaren opnemen. Alle tekorten staan per exacte printing in beeld, ook basislanden.
4. Bekijk de volledige lijst, functieverdeling, broncontrole, curve en de voorwaarden per
   gekozen kaart. Bezit gaat vóór poolvoorkeur, daarna gedrukte MV, naam en identiteit.
   De keuze is deterministisch en geeft één voorstel per berekening.
5. Geef een decknaam en kies **Voorbeeld opnieuw controleren**. Dit bewaart alleen de
   gecontroleerde voorbereiding; er is nog geen deck gemaakt. Wijziging van naam/modus
   of opnieuw berekenen verwijdert de actieve UI-voorbereiding.
6. Kies **Nieuw deck uit bezit opslaan** of **Nieuw planningdeck opslaan**. Alle relevante
   gegevens en tijdgrenzen worden opnieuw gecontroleerd. Een gewijzigde bezitsversie
   blokkeert ook wanneer het aantal heen en terug is gewijzigd. Bereken dan opnieuw.
7. Het nieuwe deck staat bij **Mijn decks**, met het expliciete BO1-profiel. Bekijk daar
   de formaatcontrole en exporteer de tekst. Bestaande decks en collectie blijven behouden.

De lijst gebruikt het totale geregistreerde bezit, niet bezit na reserveringen voor andere
decks. Eén kaart kan dus in meerdere opgeslagen decks voorkomen. Planning koopt/craft niets.
Inactieve printings tellen niet als bruikbaar bezit; geschikte herdrukken worden samengeteld
per Oracle-ID en vervolgens exact toegewezen. Meerdere actieve uitgaven verhogen de limiet
van vier niet. Mountains worden alleen bij expliciete planning zonder bezit aangevuld.

## Opslag en hercontrole

`DeckGenerationPlans` is een aparte tabel via migratie `20261023084552_AddDeckGenerationPlans`.
Een voorbereiding bewaart de naam, SHA-256 van bron/printings/voorraadversies/pool/engine/regels,
versieerbare aanvraag, beoordeelde regels en volledige uitleg. De nieuwe-deckopslag leest
opnieuw binnen één transactie, vergelijkt de exacte uitkomst en past dezelfde BO1-regels toe.
Wijziging/veroudering geeft geen half deck. Een fout of annulering na SQL-writes rolt deck
en ontvangst terug. Dubbele toepassing retourneert dezelfde deck-ID, ook na herstart of
latere verwijdering van dat deck; de ontvangst blijft historisch en hermaakt niets.

Er is geen herstelknop voor generatorvoorbereidingen en nog geen historielijst/opruimfunctie.
Een niet-opgeslagen voorbereiding blijft in de database voor controle. De app maakt vóór
de schema-upgrade een SQLite-back-up in de bestaande back-upmap.

## Bewijsgrenzen

Het plan sluit veel mogelijke legale decks bewust uit. Een andere landverhouding of
functiemix kan beter zijn; deze generator zoekt dat niet uit. De 1-mana-grens wordt binnen
functies hersteld waar mogelijk, zonder uitputtend combinaties of strategische kwaliteit
te optimaliseren. Kaartvoorwaarden worden getoond, niet met een speltoestand opgelost.
Ancestral Anger rekent bijvoorbeeld niet op een specifieke graveyard, Might of the Meek
garandeert geen Mouse en discard/offspring/kicker worden niet als gratis voordelen geteld.
Gedrukte MV is geen werkelijke gemiddelde betaalde mana; geen power/toughness- of
castingkanssimulatie. 20 creatures tellen is geen bewijs van genoeg aanvallende schade.

De tests en de proef met een echte catalogus en gesimuleerd bezit toetsen technische
eigenschappen en de vastgelegde template. Geen blind deckkwaliteitsexperiment, menselijke
review, games, tegenstanders, winstpercentage of wildcardwaarde is gemeten.
Exacte uitvoering en beperkingen: [VALIDATION_PHASE11.md](VALIDATION_PHASE11.md).
