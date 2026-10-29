# Kaartalternatieven — vanaf fase 6

Versie 0.10.0 helpt ontbrekende exacte exemplaren in een bestaand deck vervangen met
lokaal geregistreerd bezit. Ondersteund: Arena Standard BO1 en BO3. Het advies draait
lokaal, zonder AI-API of account. Sinds fase 8 staan volledige herkenning, onvolledige
rol-aanwijzingen en onbekende teksten apart. Geen voorspelling van synergie of winkans.
Fase 9 voegt vaste gerichte schade en modale opties toe: schade bewijst geen
dodelijkheid en opties zijn geen onvoorwaardelijke combinatie. Beide zijn nooit sterk.

Fase 10 voegt daarnaast een afzonderlijke [mono-rode kaartpool](MONO_RED_POOL.md) toe.
Deze gebruikt expliciete functies voor één BO1-deckplan en totale geregistreerde
voorraad. Zij verandert de onderstaande generieke adviezen, voorraadcontrole en
nieuwe-deckopslag niet. Poolopties zijn geen reeds gecontroleerde deckvervangingen.

## Gebruik

1. Werk de catalogus **online** bij en registreer je bezit via Catalogus of Collectie-import.
   Een oude fase-2-catalogus zonder legaliteitsdata is onvoldoende, ook na de app-upgrade.
2. Open **Mijn decks → Formaatcontrole**. Bewaar expliciet BO1 of BO3 en herstel alle
   fouten en onzekerheden. Het oorspronkelijke deck moet volledig slagen volgens de
   opgeslagen brondata en ondersteunde regels. Ontbrekend bezit is geen legaliteitsfout.
3. Open **Alternatieven → Advies opnieuw berekenen**. Deze stap leest het opgeslagen
   profiel, het volledige deck, de collectie en de brongegevens opnieuw.
4. Kies een tekort en klik **Alternatieven voor dit tekort zoeken**. Dezelfde Oracle-kaart
   uit een andere bezeten uitgave staat vooraan. Vergelijk het rollabel, doelen,
   voorwaarden, kosten, reden en beide kaartteksten.
5. Kies het aantal en klik **Vervanging kiezen**. Alle keuzes worden gezamenlijk getoetst.
   Herhaal dit voor andere tekorten, of selecteer een keuze om die te verwijderen.
6. Geef het nieuwe deck een naam en klik **Gezamenlijk voorbeeld controleren**. Het
   voorbeeld toont de gekozen wijzigingen, resterende tekorten, deckgrootte en controle.
7. Kies **Als nieuw deck opslaan**. De app controleert de gegevens nogmaals, slaat het
   nieuwe deck atomair op en selecteert dat deck. Het origineel en je bezit blijven staan.

Een gedeeltelijke oplossing mag: resterende tekorten blijven expliciet in het nieuwe
deck staan. Een geslaagde formaatcontrole betekent dus niet dat je alle kaarten bezit.
Na een gewijzigde naam of selectie moet het voorbeeld opnieuw worden gemaakt. Bij een
extern gewijzigd deck, profiel, catalogus of bezit wordt opslag geweigerd; bereken opnieuw.

## Voorraad en geschiktheid

- Alle zones delen één voorraad per exacte printing. Exacte sideboardexemplaren krijgen
  voorrang; daarna volgt het mainboard. Bezit wordt niet tussen verschillende decks gereserveerd.
- Alleen ontbrekende exemplaren kunnen worden vervangen. Bezit dat al een bestaande
  deckregel dekt, wordt niet voor een tweede plaats gebruikt.
- Voor alle ondersteunde zones zijn andere actuele, bezeten printings van dezelfde
  geverifieerde Oracle-kaart toegestaan. Een handmatig ingevoerde naam bewijst geen identiteit.
- Andere kaarten worden uitsluitend aangeboden voor niet-landen in het mainboard. Ze
  moeten een hoofdkaarttype delen en hun kleuridentiteit mag geen kleur toevoegen aan
  de oorspronkelijke kaart. Kleurloze kandidaten kunnen dus wel bij een gekleurde kaart passen.
- Iedere kaart met een landzijde is uitgesloten van vervanging door een andere kaart,
  inclusief modale dubbelzijdige kaarten. Sideboardstrategie en manabase worden niet afgeleid.
- Kandidaten moeten in de actuele Arena-catalogus bevestigd zijn, Standard legal zijn
  en de volledige profielcontrole doorstaan. BO1-bans, kopielimieten over printings/zones
  en ondersteunde uitzonderingen blijven gelden. Het getoonde maximum telt vrije voorraad,
  resterend tekort en de regels samen.
- Basisland-unlocks in Arena worden niet verondersteld: alleen lokaal geregistreerde
  exemplaren tellen mee. Zie ook [deckanalyse](DECKS.md).

## Rangschikking en grenzen

De vaste volgorde is: dezelfde Oracle-kaart, passende/beperkte/onbekende/andere rol,
volledige vergelijking vóór onvolledige aanwijzing binnen dezelfde categorie,
gelijke kleuridentiteit, kleinste verschil
in catalogus-manawaarde, kleinste verschil in hoofdkaarttypen, Engelse naam, set,
collectornummer en printing-ID. Hoofdtypen komen van de voorzijde. De kaarttekst staat
ter vergelijking in de UI. Alleen gesloten volledige tekstpatronen of expliciet
onvolledige zelfstandige passages worden geclassificeerd. Ondersteunde extra effecten,
permanent-abilities, mechanismen en kosten blijven zichtbaar; alle niet beoordeelde
tekst wordt bij een aanwijzing bewaard. Een aanwijzing levert nooit een sterke rolmatch op.

Concreet: bij Negate komt een beschikbare Cancel nu vóór structureel vergelijkbare
kaarten. Essence Scatter heeft andere doelwitten en krijgt een beperkte rolmatch;
Een hoge positie is **geen bewijs van strategische gelijkwaardigheid**. De huidige module
herkent 52 van 5.164 Standard-legale kaartdefinities volledig; 300 geven alleen een
onvolledige aanwijzing en 4.812 blijven onbekend. Daaronder zitten 50 modale kaarten
met afzonderlijke opties. De nieuwe aparte evaluatie mist 13 van 21 relevante kaarten,
tegenover 18 met de v0.8-baseline. Zie [schade, modale opties en meetgrenzen](DAMAGE_AND_MODAL_ROLES.md).
Controleer of een kandidaat bij je plan past.

De bestaande grenzen voor bronactualiteit blijven ongewijzigd: bron maximaal 24 uur oud,
ingebouwde regels maximaal 14 dagen na hun controledatum. Alleen de catalogus bijwerken
vernieuwt de ingebouwde regelcontrole niet. Zie [formaatcontrole](FORMAT_VALIDATION.md).

Geen volledige deckgeneratie, bewezen synergie, manabronanalyse, wildcardbudget,
metagamedata, andere formaten, live Arena-sync of test binnen een ingelogde Arena-client.

## Opslag en herstel

Een voorbeeld wordt als onveranderlijke voorbereiding in DeckAdvicePlans bewaard, met
bron-deck-ID, gegevensvingerafdruk, naam en keuzes. Sinds fase 7 bevat de JSON ook engine-/
classifierversie en de beoordeelde rollen, herkenningsregels en uitleg. Vanaf fase 8
ook herkende passage, niet beoordeelde resttekst, mechanisme en extra effecten.
Vanaf fase 9 ook optienummer, optietekst, optieanalyse en ondersteunde families.
Oude nog niet toegepaste voorbeelden vereisen herberekenen na een enginewijziging;
oude toegepaste ontvangsten blijven idempotent. Geen deck of bezit wijzigt bij voorbereiden.
De vingerafdruk omvat het deck, profiel, relevante kaartmetadata/legaliteit, brondatum,
voorraad inclusief wijzigingsversies en algoritme-/regelversie. Ook een bezitsaantal
wijzigen en terugzetten maakt het oude voorbeeld ongeldig.

Opslag maakt één nieuw deck en legt het resultaat in dezelfde transactie vast. Herhaald
opslaan van hetzelfde voorbeeld maakt geen tweede deck. Na verwijderen van het nieuwe
deck wordt dat door dezelfde ontvangst niet opnieuw aangemaakt. Een fout of annulering
na SQL-writes rolt zowel het deck als de ontvangst terug.

Voorbereidingen en ontvangsten blijven lokaal in de database. Er is nog geen historie-
of opruimscherm voor adviesvoorbereidingen; bereken na een UI-herstart het advies opnieuw.
Het opslagcontract zelf kan een bewaard voorbeeld na herstart veilig afhandelen.
