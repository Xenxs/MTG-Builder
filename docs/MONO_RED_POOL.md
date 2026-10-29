# Gecontroleerde mono-rode kaartpool — fase 10

Versie **0.10.0** voegt **Mono-rode kaartpool** toe aan het navigatiemenu. Het deckplan
is **mono-rood aggro voor Arena Standard BO1**: vroege druk, aanvallende spell-payoffs
en burn. Eén kleur houdt de eerste afbakening beheersbaar. Dit is geen claim dat
mono-rood het beste huidige metagame-deck is.

De pool bevat **dertig expliciet beoordeelde kaarten**: 26 ondersteunde niet-landen,
Mountain en drie bewuste uitsluitingen. Het zijn beoordelingen door dezelfde
ontwikkelaar (Codex), geen onafhankelijke menselijke review. De pool staat los van
de generieke Oracle-parser; de beperkte algemene kaartkennis uit fase 9 blijft beperkt.

## Gebruik

1. Werk de catalogus **online** bij via Catalogus en registreer bezit via de bestaande
   collectie-invoer/-import. Een oude catalogus zonder legaliteit of een verlopen
   snapshot blokkeert advies. De app-upgrade vernieuwt de bron niet vanzelf.
2. Open **Mono-rode kaartpool**. Bekijk aantallen en **Poolversie en broncontrole**.
   **Bronstatus en bezit vernieuwen** leest de lokale gegevens opnieuw; dit downloadt niet.
3. Zoek een kaartnaam, toon alleen geregistreerd bezit of schakel bewuste uitsluitingen in.
4. Selecteer een poolkaart. Lees functie, status, voorwaarden en de volledige kaarttekst.
   Ook een niet-bezeten bronkaart mag als vergelijkingspunt worden gekozen.
5. Kies **Opties met dezelfde deckfunctie zoeken**. De app leest bron, metadata en bezit
   opnieuw en toont alleen geschikte andere poolkaarten met dezelfde beoordeelde functie.
6. Open **Origineel, alternatief en alle voorwaarden** om beide teksten en beperkingen
   te vergelijken. Verschillende kosten, timing, triggers en extra effecten blijven relevant.

De werkbank is **alleen lezen**: zij verandert geen collectie, deck, profiel of
adviesvoorbereiding. Aantallen zijn totale geregistreerde voorraad, geen vrij berekende
voorraad na andere deckkeuzes. Bezit wordt niet gereserveerd. Vanaf v0.11.0 staan
complete begrensde voorstellen op de afzonderlijke pagina **Deckvoorstel**;
zie [DECK_GENERATION.md](DECK_GENERATION.md). Deze poolwerkbank zelf blijft read-only.

## De afgebakende functies

| Functie | Ondersteunde kaarten |
| --- | --- |
| Basismana | Mountain |
| Vroege aanvaller | Clockwork Percussionist, Fanatical Firebrand, Ghitu Lavarunner, Hired Claw, Charming Scoundrel, Frenzied Goblin |
| Aanvallende spell-payoff | Slickshot Show-Off, Emberheart Challenger, Fugitive Codebreaker |
| Noncreature-spell-ping | Firebrand Archer, Coruscation Mage |
| Creature met enters-burn | Viashino Pyromancer |
| Burn naar tegenstander | Shock, Lightning Strike, Burst Lightning, Boltwave |
| Creature-interactie | Abrade, Torch the Tower, Scorching Shot |
| Combat-ondersteuning | Ancestral Anger, Might of the Meek, Crash Through, Turn Inside Out, Blazing Crescendo |
| Hand verversen | Thrill of Possibility, Grab the Prize |

**Bewust uitgesloten:** Courageous Goblin (power-4-plan niet bewezen), Manifold Mouse
(specifiek Mouse-plan niet als algemene payoff veronderstellen), Leyline of Resonance
(verboden in Arena Standard BO1). De actuele Wizards-lijst vermeldt de BO1-uitzondering
voor Leyline. [Officiële banlijst](https://magic.wizards.com/en/banned-restricted-list).
Een uitsluiting betekent niet dat de kaart in ieder ander deck slecht is.

Belangrijke verschillen blijven expliciet: Burst Lightning geeft alleen vier schade
bij extra kicker {4}; Torch the Tower vereist opofferen voor bargain; Abrade kiest
schade **of** artifact-destroy; Boltwave raakt geen creature/planeswalker. Ghitu Lavarunner
vereist graveyardkaarten voor de bonus; Emberheart heeft valiant-voorwaarden; offspring,
plot en disguise worden niet gratis verondersteld. Discard als extra kosten verdwijnt
niet uit het advies. Schade of een combatboost bewijst geen dodelijkheid.

## Geschiktheid en rangschikking

Elke poolkaart is gekoppeld aan een **exacte Oracle-ID** en een SHA-256 over de
beoordeelde opgeslagen velden: naam, Oracle-ID, manakosten, type, Oracle-tekst,
kleuridentiteit en manawaarde. Alleen een naam matchen is onvoldoende. Gewijzigde
of tegenstrijdige metadata blokkeert de kaart tot een nieuwe poolbeoordeling in een
appversie; geen knop die de wijziging stilzwijgend goedkeurt.

Daarnaast gelden de bestaande bron-/regelactualiteit en BO1-ban-/legaliteitscontrole.
De enige genegeerde uitkomst bij de afzonderlijke kaarttoets is het verwachte minimum
van zestig kaarten: de werkbank beoordeelt één kaart, geen compleet deck. Overige
problemen blokkeren. Bron maximaal 24 uur oud; ingebouwde regels maximaal 14 dagen
na hun controledatum. Bezit in niet-actuele printings blijft bewaard, maar telt niet
mee als actueel bruikbaar bezit. Verschillende definities voor dezelfde Oracle-ID
worden conservatief als conflict behandeld.

Opties vereisen een geschikte bronkaart én geschikte andere kaarten uit dezelfde
functie met positief bruikbaar bezit. Landen krijgen geen andere kaart als vervanging.
Daarna sorteert de werkbank op vooraf vastgelegde poolvoorkeur (eerste/voorwaardelijk/
smal), verschil in manawaarde, ordinale naam en Oracle-ID. De voorkeuren zijn expliciete
ontwikkelbeoordelingen voor dit plan; geen objectieve kracht- of winkansscores.

Dezelfde functie kan een ander hoofdkaarttype of timing hebben, bijvoorbeeld Thrill
of Possibility → Grab the Prize. Dit wijkt bewust af van de generieke alternatieven
onder Mijn decks. Het verschil staat in de kaartteksten/voorwaarden; beide adviezen
zijn geen garantie van strategische gelijkwaardigheid. De werkbank bewaart geen
poolkeuze als deckvervanging en omzeilt de bestaande voorraad-/deckcontrole niet.

## Gemeten kwaliteit en grenzen

De [twintig vaste referentiegevallen](../examples/mono-red-advice-reference-v1.json)
zijn vastgelegd **vóór de rangschikkingsimplementatie**. Poolmetadata en verwachtingen
zijn sindsdien ongewijzigd. Op dezelfde aangeboden kaartsets:

| Verwachte eerste keuze volgens deze referentie | Generiek v0.9-advies | Pooladvies |
| --- | ---: | ---: |
| Aantal geslaagde gevallen | **5/20** | **20/20** |

Dit meet **functie-filtering én rangschikking**. De geschiktheidsfilters verschillen;
het is geen vergelijking van alleen de sorteervolgorde op identieke eindresultaten.
Bezit, printings en legaliteit zijn voor deze regressies gesimuleerd. Dezelfde
ontwikkelaar koos en beoordeelde de gevallen; geen onafhankelijke, ongeziene of
representatieve benchmark. Twintig goede referenties bewijzen geen algemene
adviesprecisie of verbetering van winkans. Alle gevallen vallen binnen deze vaste pool.

Een aparte echte catalogusproef gebruikte de actuele Scryfall-snapshot van
**22 oktober 2026 21:05:50.017 UTC**, online geïmporteerd op 23 oktober. Daarin zijn
**27 van 27 ondersteunde poolkaarten** actueel geschikt; drie uitsluitingen blijven
geblokkeerd. Slickshot Show-Off krijgt Emberheart Challenger en Fugitive Codebreaker
als beoordeelde opties uit gesimuleerd bezit. Dit bewijst identiteit, broncontrole,
gegevensverwerking en de bedoelde functie-indeling, geen prestaties in echte matches.

Niet gemodelleerd: power/toughness als zelfstandige velden, speltoestand, volledige
mana-/castbaarheid, algehele synergie, matchups, sideboardstrategie, metagame of alle
mogelijke alternatieven. De metadata-afdruk betreft de hierboven opgesomde opgeslagen
velden en is geen volledige fingerprint van ieder veld van het Scryfall-kaartobject.
Geen claim dat dertig kaarten of dit ene deckplan de Standard-catalogus dekken.

## Bron, versie en onderhoud

Pool **mono-red-bo1-v1/2026-10-23**, engine **Mono-red pool advice v1**. De beoordeelde
definities en voorwaarden staan als embedded resource in
[mono-red-aggro-v1.json](../src/MTGDeckbuilder.Application/Resources/mono-red-aggro-v1.json).
Een nieuw catalogusbestand is toegestaan zolang de kaartmetadata gelijk blijft en
de actuele bron-/regeltoets slaagt. De oorspronkelijke bronversie/hash blijft als
provenance vastgelegd; een hele-catalogus-hashwijziging alleen blokkeert niet alles.

Fase 10 voegde geen tabel, migratie of NuGet-pakket toe; de zes toenmalige migraties bleven staan.
Fase 11 voegt een zevende migratie toe voor generatorvoorbeelden; de pooldefinitie blijft gelijk.
De generieke engine/classifier en bestaande ontvangsten worden door deze afzonderlijke
werkbank niet gewijzigd. Tests controleren zowel metadata-/bronblokkades als
voorraadverandering, herstart en daadwerkelijke UI-bindingen.
Exacte uitvoering en bewijsbestanden: [VALIDATION_PHASE10.md](VALIDATION_PHASE10.md).
