# Kaartrollen en advieskwaliteit — fase 7

Dit document beschrijft de **historische fase-7-versie**. Vanaf v0.8.0 is de
classifier v2 actief, met aanvullende volledige patronen en expliciet onvolledige
aanwijzingen. Voor het huidige gebruik en de actuele meetgrenzen zie
[fase 8: praktische roldekking](ROLE_COVERAGE.md) en [controlebewijs](VALIDATION_PHASE8.md).
De 16 herkende kaarten hieronder zijn de fase-7-baseline, niet de huidige dekking.
De huidige versie is v0.9.0; zie [fase 9](DAMAGE_AND_MODAL_ROLES.md) voor schade en
afzonderlijke modale aanwijzingen.

Versie **0.7.0** voegt beperkte rolherkenning toe aan Mijn decks → Alternatieven.
Reprints van dezelfde geverifieerde kaart blijven bovenaan. Andere kandidaten worden
eerst op rolvergelijking gerangschikt, daarna op de bestaande structurele criteria.
Voorraad, kleur-/typefilters, formaatcontrole en veilige nieuwe-deckopslag blijven gelden.

## Betekenis van de labels

| Label | Betekenis |
| --- | --- |
| Dezelfde kaart | Dezelfde geverifieerde Oracle-ID; alleen de printing verandert |
| Rol past binnen ondersteunde herkenning | Dezelfde beperkte effectfamilie met passende doeldekking; geen strategische gelijkwaardigheid gegarandeerd |
| Beperkte rolmatch | Dezelfde familie, maar bijvoorbeeld andere doelwitten, betaaluitweg, minder kaarten of een ander soort mana-effect |
| Rol niet bewezen | Minstens één volledige tekst wordt niet ondersteund; alleen structurele overeenkomst |
| Andere herkende rol | Herkende effectfamilies verschillen; alleen structurele overeenkomst |

De laatste drie categorieën worden niet verborgen: je kunt ze bewust kiezen na het
lezen van de verschillen en de kaarttekst. Ze zijn geen sterke rolvervangers. Een
groene **formaatcontrole** zegt alleen iets over de bestaande deckregels, niet over
de kwaliteit van een gekozen vervanger. Een onbekende rol maakt een kaart niet illegaal.

Klik een kandidaat aan om doelen, beperkingen, timing, gedrukte manakosten en beide
kaartteksten te vergelijken. In het gezamenlijke voorbeeld staat de rolwaarschuwing
opnieuw; onder **Rol, verschillen en herkenningsversie** staat de volledige uitleg.

## Afgebakende herkenning

De fase-7-classifier heet **Oracle roles v1 (2026-10-22)**. Hij normaliseert witruimte en
controleert de **gehele** Engelse Oracle-tekst tegen een gesloten set patronen.
Een los woord zoals draw, destroy of counter geeft geen classificatie.
Alleen enkelzijdige kaarten met exact het type Instant of Sorcery vallen binnen deze versie.

| Familie | Ondersteunde tekstvormen en beperkingen |
| --- | --- |
| Counterspells | Enkel doel spell, creature, noncreature, instant, instant/sorcery of artifact/enchantment; herkenbare betaaluitweg; mana value N or greater (N 1–9) |
| Gerichte removal | Enkel destroy/exile met ondersteunde doelcategorie; nonartifact creature; creature/nonland permanent terug naar de hand |
| Kaarten trekken | Alleen Draw N cards of Target player draws N cards; N één t/m zeven |
| Mana-versnelling | Eén basic land of de exacte Farseek-landtypen naar battlefield; tijdelijke add-mana-tekst met 1–7 mana en bruto aantal groter dan de catalogus-manawaarde |

Niet ondersteund zijn onder meer meerdere effecten, modaliteiten, extra kosten, complexe
voorwaarden, herinneringstekst naast het effect, schade als removal, permanent-abilities
en meervoudige kaarten. Opt, Consider, Make Disappear, Cultivate en Llanowar Elves blijven
daarom onbekend in deze versie. Dat is geen uitspraak dat ze geen rol hebben; die rol
wordt door deze classifier niet bewezen.

De vergelijking bewaart verschillen. Een universele counter kan de doelgroep van
Negate dekken. Essence Scatter kan dat niet: creature en noncreature zijn verschillende
doelgroepen. Spell Pierce heeft een betaaluitweg. Destroy, exile en bounce hebben
verschillende gevolgen; bounce tegenover definitieve removal wordt als beperkt gemeld.
Basic lands tegenover Farseek-landtypen gelden niet automatisch als gelijke dekking.
Gedrukte kosten worden getoond, maar castbaarheid, alternatieve kosten, netto mana,
synergie, speltoestand en winkans worden niet berekend.

## Gemeten resultaten en hun grenzen

De vaste bronset [role-reference-v1.json](../examples/role-reference-v1.json) bevat
**30 nagekeken Oracle-teksten** uit de eerder online opgehaalde Scryfall-snapshot.
Verwacht: **18 herkend en 12 bewust onbekend**. Alle dertig voldoen in de definitieve tests
aan de verwachte rol, doelgroep en beperking.

Tien aanvullende rangschikkingsscenario's gebruiken deze teksten met gesimuleerd bezit
en legaliteit. Dezelfde kandidaten worden vergeleken met de vastgelegde sortering uit
fase 6. De verwachte eerste keuze stijgt van **3/10 naar 10/10**. De gemiddelde omgekeerde
positie van die keuze stijgt van **0,583 naar 1,000**. Dit is een kleine, bewust gekozen
ontwikkel-/regressieset, geen onafhankelijke of representatieve praktijkbeoordeling.
Enkele kaarten in de referentieset zijn niet Standard-legaal; de scenario's meten alleen
de rangschikking. De echte applicatie blijft de werkelijke opgeslagen legaliteit eisen.

De praktijkdekking is veel kleiner dan de referentiescore suggereert. In de echte
catalogussnapshot worden **16 van 5.164 Standard-legale kaartdefinities** herkend:
7 counterspells, 7 removal, 1 draw en 1 mana-versnelling. Van deze 5.164 kaarten vallen
1.022 kaarten met exact Instant/Sorcery binnen het typebereik; 16 daarvan is circa **1,6%**.
Over de hele Standard-legale kaartset is het circa **0,3%**. Alle andere teksten blijven
onbekend. Dit is een beperkte eerste rolmodule, geen brede semantische kaartkennis.

De echte Negate-proef toont na een reprint nu Cancel als passende rolkandidaat. Essence
Scatter blijft kiesbaar met een expliciete waarschuwing voor andere doelwitten. Zie
[controlebewijs](VALIDATION_PHASE7.md) voor meetbestanden en exacte omstandigheden.

## Versies en gegevensbehoud

De voorbereiding bewaart een versieerbaar JSON-object met keuzes, engineversie,
classifierversie en de beoordeelde verschillen inclusief herkenningsregel en uitleg.
Dit past in het bestaande DeckAdvicePlans.ChoicesJson-veld; er is **geen nieuwe tabel
of migratie nodig**. De bestaande zes migraties blijven staan.

De gegevensvingerafdruk bevat nu ook de gedrukte manakosten en de classificatieversie
via de engineversie. Oude nog niet toegepaste voorbeelden moeten opnieuw worden berekend;
ze worden niet gewist. Al toegepaste ontvangsten blijven hetzelfde resultaat opleveren.
Het origineel en bezit worden nooit door het advies overschreven.

Voorbereidingen hebben nog geen UI-historie/opruimfunctie. De bestaande 24-uursgrens
voor brondata en 14-dagentermijn voor de ingebouwde formaatregels blijven gelden.
