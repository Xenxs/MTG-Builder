# Collectiefilters, deckwizard en database legen

Geldt voor **v1.3.0**, fase 16. De app gebruikt fysieke kaarten. Scryfall blijft de bron
voor kaarttekst, exacte uitgaven en afbeeldingen; een online zoekresultaat is geen bezit.

## Je collectie filteren

Open **Mijn collectie**. De filters werken meteen en gelden samen:

| Filter | Betekenis |
| --- | --- |
| Naam | Zoekt in kaartnamen in je actieve collectie. |
| Kleur | Wit, blauw, zwart, rood, groen, kleurloos of meerkleurig; een kleur toont ook meerkleurige kaarten die die kleur bevatten. |
| Synergie / themarol | Equipment, tokens, graveyard, eigen sacrifice, removal/vijandelijke sterftriggers, domain, exalted of kaarten trekken. |
| Kaarttype | Creature, artifact, enchantment, instant, sorcery, land, planeswalker, battle of kindred. |
| Serie / set | De series die in je actieve collectie voorkomen, met naam en setcode. |

Bijvoorbeeld **Wit + Equipment + Creature + Final Fantasy** toont drie uitgaven in de
voorbeeldcollectie. Een basisland kan een kleurloze kaart zijn, ook als het gekleurde
mana maakt. De wizard kijkt naar **kleuridentiteit**, die ook regelsymbolen en andere
kaartzijden meetelt. Dat is bewust een andere eigenschap.

Het aantal onder de filters toont uitgaven en exemplaren. **Filters wissen** herstelt
alle filters. Selecteer onder **Mijn actieve fysieke collectie** een kaart voor details
en afbeelding. **Zoeken bij Scryfall** gebruikt het zoekveld voor een aparte online
zoekopdracht; de vier collectie-dropdowns filteren de eigen collectie.

Synergiefilters gebruiken ondersteunde eigen rollen. De 87 productkaarten zijn afzonderlijk beoordeeld; buiten die pool is de herkenning beperkt. Bij een deck en wizardvoorstel zie je concrete relaties, voorwaarden en ontbrekende ondersteuning. Zie [synergie gebruiken en begrijpen](SYNERGY_ENGINE.md).

## Een deck met de wizard bouwen

Open **Deckwizard**, of **Mijn decks → Bouw een nieuw deck met de wizard**.

1. **Naam en formaat:** Casual 60, Standard 60 of Commander 100. Casual controleert de
   ondersteunde deckstructuur. Standard en Commander controleren ook actuele papieren
   legaliteit en bronactualiteit.
2. **Kleuren:** geen voorkeur, alleen gekozen kleuren, of bewust kleurloos. Kies bij
   de middelste optie één of meer kleuren. Kaarten moeten binnen die kleuridentiteit
   passen; elke gekozen kleur hoeft niet daadwerkelijk in het voorstel terug te komen.
3. **Thema en speelstijl:** kies een synergie-aanwijzing en aggro, control of gebalanceerd.
   Passende thema's krijgen voorrang. Aggro geeft creatures en lage gedrukte manawaarde
   voorrang; control geeft instants/sorceries en hogere gedrukte manawaarde voorrang.
   Dit zijn beperkte voorkeuren; geen garantie dat een strategisch sterk deck ontstaat.
4. **Bezit of planning:** standaard alleen actieve eigen voorraad. Bij een tekort stopt
   de wizard. Schakel Planning alleen bewust in om ontbrekende kaarten via een begrensde
   Scryfall-zoekpool toe te staan. Bij Commander kies je hier één geschikte commander.
   Diens kleuridentiteit kan de gekozen kleuren verder beperken.
5. **Controleren en opslaan:** lees de analyse, bekijk de volledige kaartlijst en alle
   tekorten. De toelichting en kaartlijst kunnen apart scrollen. Bevestig de checkbox en
   kies **Nieuw deck opslaan**. Met **Open mijn nieuwe deck** ga je naar je opgeslagen lijst.

De wizard schrijft pas bij die laatste bevestigde opslag. Andere decks en collectieaantallen
blijven behouden. Opslaan controleert opnieuw of voorraad/kaartgegevens zijn veranderd en
of Standard-/Commander-bronnen nog geldig zijn. Zo nodig moet je opnieuw voorbereiden.
Andere decks reserveren geen kaarten: dezelfde voorraad kan in verschillende deckplannen
voorkomen. Planning koopt geen kaarten en voegt geen fictief bezit toe.

De vaste plannen zijn 36 niet-landen en 24 basics bij 60 kaarten, of één commander,
62 verschillende niet-landen en 37 basics. De zoekpool is begrensd en niet uitputtend.
Geen automatisch partner/background-paar, variabele commander-kleurkeuze, combo-engine,
castingkansberekening of bewezen speelsterkte. Een thema vraagt minstens één passende
niet-landkaart, niet een volledig bewezen synergetisch deck.

## De huidige database legen

Dit wist **alle** kaart-, collectie-, deck-, import- en profielgegevens, inclusief
oudere intern bewaarde gegevens. Het wist geen back-ups, afbeeldingcache of logbestanden.

1. Open **Instellingen → Database legen → Bekijk wat wordt verwijderd**.
2. Controleer de aantallen en de omschrijving. Typ exact **WISSEN**.
3. Kies **Reservekopie maken en database legen**.

De app controleert dat de data sinds het voorbeeld niet is gewijzigd, maakt eerst een
consistente reservekopie, controleert integriteit, verwijzingen en het exacte schema,
en voert het legen in één transactie uit.
Bij een fout vóór de commit blijft de database behouden. Zonder maakbare reservekopie
gaat de actie niet door. Gepland herstel blokkeert legen totdat je dit hebt geannuleerd.
De SQLite-kopie zelf is synchroon op een achtergrondthread; annulering is tijdens die
korte kopie niet onmiddellijk beschikbaar.

Na afloop is de collectie leeg, zijn de decks weg en is het profiel gereset. Het schema
en migraties blijven bestaan. **De 202 voorbeeldkaarten komen na herstart niet terug.**
Je kunt een eigen scan-CSV importeren of handmatig kaarten registreren. Het resultaat
toont het pad van `pre-reset-…db`; dezelfde reservekopie staat in Back-up en herstel.

Om terug te keren: selecteer die reservekopie, laat haar controleren, bevestig herstel
en herstart de app. Zie [back-up en herstel](BACKUP_RESTORE.md). Herstel zet de volledige
toestand van die kopie terug, inclusief voorbeeldinstelling en alle toenmalige data.
