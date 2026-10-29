# Synergie in de fysieke deckbuilder — v1.4.0

De engine zoekt concrete, voorwaardelijke relaties tussen verschillende kaarten.
Hij simuleert geen partij en bewijst geen speelsterkte of optimale decklijst.
Een lijst met veel relaties is niet noodzakelijk beter: een drager naast Equipment
is een basiscombinatie, geen bijzondere combo.

## Zo gebruik je de controle

Open een deck onder **Mijn decks**. De synergiecontrole staat boven de bewerkvelden.
De wizard toont dezelfde controle bij het voorstel: aantallen gecontroleerde,
beperkt herkende, onbekende en gewijzigde profielen, aandachtspunten en de volledige
lijst relaties. Lees bij iedere relatie de voorwaarde. Bezit/planning en de tekorten
blijven afzonderlijke controles.

Bij **Mijn collectie** is een synergie-filter een themarol van één kaart. Dat kan
een producer, enabler of payoff zijn. Het filter bevestigt niet dat de rest van je
deck de noodzakelijke ondersteuning bevat. Alle overige filters blijven gecombineerd.

| Thema | Wat het betekent |
| --- | --- |
| Equipment | Equipment, een ondersteunde uitgeruste-creature-payoff of hulp bij betalen/aanhechten. Een gewone creature is nog geen themapayoff. |
| Tokens | Eigen tokens of ondersteunde ondersteuning, zoals een teambonus of convoke. Niet ieder token is een creature of artifact. |
| Graveyard | Geselecteerde eigen setup-, hergebruik- en payoffrollen. Alleen het woord graveyard is onvoldoende. |
| Eigen sacrifice | Eigen opofferroutes of eigen sterfpayoffs. Een kaart die alleen zichzelf opoffert is geen vrije outlet voor andere creatures. |
| Removal & vijandelijke sterftriggers | Ondersteunde verwijdering of vijandelijke sterfpayoff; belangrijk voor Sephiroth. Eigen creatures opofferen activeert zijn vijandelijke sterfvoorwaarde niet. |
| Domain | Payoff voor de vijf basislandtypen onder je eigen landen. Vijf kleuren mana of vijf Forests zijn geen vijf typen. |
| Exalted | Eén als aanvaller verklaarde creature. Evasion kan helpen; meerdere aanvallers krijgen de solo-bonus niet. |
| Draw | Werkelijk eigen kaarten trekken of een ondersteunde draw-payoff. Reveal/exile-toegang is geen draw. |

## Voorbeelden uit de testcollectie

- **Cloud + Warrior's Sword:** uitrusten kan de aanvaller ondersteunen, maar vraagt
  een legaal target, equipkosten, de kaarten op het slagveld en het juiste aanvalsmoment.
- **Job select:** levert bij binnenkomst een eigen Hero-token en Equipment. Verplaatsen
  naar Cloud kost later nog een geschikte equip-/aanhechtactie.
- **Barret:** telt uitgeruste creatures. Drie Equipment op één creature tellen als één
  uitgeruste creature.
- **Deft Duelist:** shroud blokkeert eigen gerichte equip-acties. Exalted target niet.
- **Kaleidostone:** kan kleuren mana leveren, maar is geen land en voegt geen Domain-type toe.
- **G'raha Tia / Al Bhed Salvagers:** eigen relevante creatures/artifacts moeten werkelijk
  sterven; lees ook beperkingen zoals andere permanents en eenmaal per beurt.
- **Sephiroth:** vraagt sterven van creatures van tegenstanders. Exile en bounce zijn
  geen sterven. Een eigen sacrifice-route alleen is geen Sephiroth-engine.
- **Xande / recursion:** verdwenen tokens zijn geen kaarten om later uit de graveyard
  terug te halen. Creaturekaarten en noncreature/nonlandkaarten worden apart behandeld.

De 202 aangenomen exemplaren bevatten 109 uitgaven van 87 verschillende Oracle-kaarten.
Voor die 87 kaarten bestaan gecontroleerde profielen voor de ondersteunde relaties.
Dit zijn geen volledige regelsmodellen van alle effecten op die kaarten. Er is in deze
collectie geen aangetoonde vrij herhaalbare creature-sacrifice-outlet. Artifact- en
Treasure-routes zijn beperkter en worden zo gemeld.

## Hoe selectie werkt

Na iedere gekozen kaart kijkt de generator opnieuw naar passende ondersteuning,
creature-/Equipment-verhouding, thema, stijl, curve en kopieën. Het aantal relaties
wordt begrensd in de rangschikking; extra kopieën tellen niet als nieuwe kaartparen.
Bij geen kleurvoorkeur zoekt hij een haalbaar plan met minder castingkleuren.
Domain mag breder gaan. Geactiveerde gekleurde kosten tellen mee; geproduceerde mana
wordt niet als castingkosten gelezen. Dit is geen berekening van castingkansen.

De vaste constructieplannen blijven 36 spreuken + 24 basics, of één commander + 62
verschillende spreuken + 37 basics. Bij Commander zijn kleuridentiteit, legaliteit,
actualiteit en singleton harde grenzen. Bezitmodus verzint geen voorraad. Planning
kan bij Scryfall aanvullen en blijft expliciet tekorten tonen.

De aanvullende zoekpool is begrensd en op Commander-populariteit geordend, in plaats
van de eerste alfabetische kaarten te bevoordelen. Dit is een bronkeuze, geen bewijs
dat een kaart bij jouw strategie of bij Standard past. De lokale selectie en regels
worden daarna toegepast; de pool is niet de volledige Magic-catalogus.

## Grenzen die je serieus moet nemen

- Buiten de 87 gecontroleerde kaarten wordt slechts een deel van Oracle-tekst herkend.
  Onder meer Saga-hoofdstukken, geneste aangeleerde abilities, controllerwisselingen,
  timing, counters en beperkingen in doelwitteksten kunnen ontbreken.
- Multi-face, Adventure en split worden niet samengevoegd tot één effect/kostmodel.
  De generator kiest zulke kaarten voorlopig niet automatisch. Handmatig gebruiken
  en bekijken blijft mogelijk.
- Expliciete eigen verliesvoorwaarden of winblokkades worden uitgesloten van
  automatische selectie. De engine bewijst geen veilige uitweg. Deze gerichte
  beveiliging detecteert niet ieder mogelijk nadeel van iedere Magic-kaart.
- Een gewijzigd gecontroleerd profiel valt terug op typefeiten. Laat de gameplaytekst
  opnieuw beoordelen; een bekende kaartnaam maakt gewijzigde metadata niet vertrouwd.
- Relaties beschrijven mogelijke samenwerking, geen gelijktijdige aanwezigheid,
  geldige oneindige lus, voldoende mana, bewezen winstplan of algemene nauwkeurigheid.

## Onderhoud en bewijs

Application bevat `SynergyProfile`, `SynergyTextRules`, `SynergyEngine`,
`SynergySelection`, `SynergyReplacement` en `DeckManaHeuristics`. De gecontroleerde
bronannotaties staan in `examples/synergy-product-annotations-v1.json`; de resource
wordt reproduceerbaar gemaakt met `scripts/SynergyProfiles`. SHA-256 over Oracle-ID,
naam, tekst, typen, kleuren, kosten en power/toughness bewaakt gewijzigde metadata.
Bij nieuwe kaarten eerst feiten/voorwaarden beoordelen, dan referenties toevoegen.
Alleen het toevoegen van een positief themalabel is onvoldoende.

De eerste aparte evaluatieset gaf 9/26 relevante labels; na verwerking werd die set
ontwikkelmateriaal. De tweede eerste proef gaf 21/28 en vond bovendien twee onveilige
samengevoegde kaartvormen. De derde, vóór inspectie bevroren proef gaf 14/23 met negen
gemiste labels en nul onterecht toegekende labels in die kleine set. De selectie is
daarna nog beveiligd voor de in de live-proef gevonden eigen verliesvoorwaarden.
Dit zijn kleine, selectieve sets met dezelfde ontwikkelaar als beoordelaar; geen
onafhankelijke precisieclaim of aangetoonde speelsterkte.

De bovenstaande kleine evaluaties horen bij fase 16. Zie [uitgevoerde controles fase 16](VALIDATION_PHASE16.md) en
[vooraf vastgelegde criteria](SYNERGY_ACCEPTANCE_PHASE16.md).
Regelbron: [officiële Magic-regels](https://magic.wizards.com/en/rules), geraadpleegd
28 oktober 2026; de daar gekoppelde Comprehensive Rules zijn van 25 september 2026.
Kaarttekst en afbeeldingen: [Scryfall](https://scryfall.com/).

## Uitbreiding fase 17 — alle papieren kaarten

De afzonderlijke proef gebruikt 33.727 fysieke Oracle-kaarten en 138.106 aangenomen
exemplaren, los van je gewone collectie. Selectie deelt berekeningen zonder alle
kaartparen vooraf te maken. Grote collectiefilters rekenen op de achtergrond.

Cycling/draw, typed recursion, ETB-/sterfsubjecten, nontoken en beperkte sacrifice-
voorwaarden zijn aangescherpt. Levelers/acorn/silver en andere onvoldoende ondersteunde
kaartvormen worden automatisch geweigerd. De commanderselectie gebruikt herkende
letterlijke creaturetype-/castafhankelijkheden; bijvoorbeeld echte Angels voor Giada
en spells voor Talrand. Tokens tellen niet als gecaste spells. Het verslag benoemt
de herkende bouwfocus. Graveyard-setup wordt afgeremd wanneer het deck er te veel van krijgt.

Dit is nog geen universele strategie-engine: de eerste aparte B-proef herkende 22/57
relevante themalabels, met 35 missers. Nul onterechte labels in die selectieve set is
geen algemene nauwkeurigheid. Slechts 87 kaarten hebben handmatig gecontroleerde
productprofielen; 19.924 kaarten blijven type-only/onbekend. Proliferate, veel tribal,
variabele thresholds en algemene combo's blijven beperkingen. Zie
[gebruik alle-kaartenproef](SYNERGY_FULL_CATALOG.md) en
[volledig bewijs fase 17](VALIDATION_PHASE17.md), inclusief tegenvallende resultaten.

