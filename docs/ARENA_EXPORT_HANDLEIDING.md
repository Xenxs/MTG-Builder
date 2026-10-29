# Je Arena-kaarten gebruiken in MTG Arena Smart Deckbuilder

Voor Windows en applicatieversie 1.0.0. Gecontroleerd op 23 oktober 2026.
Dit gaat over **MTG Arena**, niet over het andere spel Magic Online (MTGO).

## Eerst: een deck is geen collectie

| Wat wil je meenemen? | Waar gebruik je het? | Betekenis |
| --- | --- | --- |
| Een decklijst | Deck importeren | Kaarten die een deck vraagt; geen bewijs dat je ze bezit |
| Je bezit met aantallen per uitgave | Collectie-import | Werkelijke geregistreerde collectie; vereist ons collectie-JSON |

**V1.0 heeft geen rechtstreekse Arena-koppeling.** Een Arena-log, CSV of willekeurige
tracker-JSON kun je niet rechtstreeks als collectie importeren. Een kaartcatalogus
van Scryfall bevat kaartgegevens, niet jouw bezit.

## 1. Maak de applicatie klaar

1. Open MTG Arena Smart Deckbuilder.
2. Ga naar **Instellingen → Nu een back-up maken** als je al gegevens hebt.
3. Ga naar **Catalogus → Catalogus importeren / bijwerken → Bijwerken via Scryfall**.
4. Wacht tot de import voltooid is. Je kunt nu de juiste kaarten en uitgaven koppelen.

## 2. Je volledige collectie uit Arena halen

Een mogelijke externe route is **Tapps Tracker**. De projectdocumentatie beschrijft
collectie-export via het geheugen van de draaiende Arena-client. Het is software van
een derde partij, geen onderdeel van onze app. Ik heb deze route niet met jouw Arena-
account uitgevoerd en kan de volledigheid van jouw export nog niet bevestigen.
[Bron en downloadverwijzing](https://github.com/pattont/MTGA-Tapps#export-your-collection).

1. Download een Windows-versie via de Releases-link van dat project.
2. Start Arena en log in op het account waarvan je de collectie wilt gebruiken.
3. Open in Arena eenmaal **Decks**, zodat de collectie wordt geladen. Laat Arena open.
4. Open in het tracker-dashboard **Settings → collectie-export** en kies **JSON**.
5. Bewaar het bestand en controleer eventuele exportwaarschuwingen. Een melding over
   ontbrekende kaartgegevens of een onvolledige scan is reden om eerst te stoppen.

De precieze knoppen kunnen per trackerversie verschillen. Dit is een beschreven route,
geen geteste garantie voor iedere Arena-update. Gebruik de collectie-export; functies
voor het verplaatsen of verwijderen van Arena-decks zijn hiervoor niet nodig.

## 3. De tracker-JSON omzetten voor onze applicatie

De gecontroleerde Tapps-bron schrijft een ander formaat en kan uitgaven samenvoegen.
Het meegeleverde script zet uitsluitend regels met **één Arena-ID** om. Bij meerdere
ID's stopt het hele proces: een gecombineerd aantal eerlijk verdelen kan niet uit
dit bestand worden afgeleid. Er wordt niets stilzwijgend overgeslagen of opgeteld.
[Gecontroleerde exportcode](https://github.com/pattont/MTGA-Tapps/blob/main/src/mtga_tracker/collection_export.py).

Het script staat in de projectmap onder `scripts/Convert-TappsCollection.ps1` en zit
niet in de eerder gebouwde v1.0-installer. De applicatie zelf is niet gewijzigd.

1. Bewaar de originele export ongewijzigd.
2. Open PowerShell in de projectmap `C:\Temp\Magic-Applicatie`.
3. Voer het volgende uit; PowerShell vraagt daarna om twee volledige bestandspaden:

```powershell
.\scripts\Convert-TappsCollection.ps1
```

4. Vul bij **SourcePath** het pad naar de tracker-JSON in.
5. Vul bij **OutputPath** een nieuw pad in, bijvoorbeeld een bestand met de naam
   `mijn-collectie-import.json` in dezelfde map. Gebruik een nog niet bestaande naam.
   Bij deze invulvragen plak je de paden zonder omringende aanhalingstekens.
6. Bij succes meldt het script hoeveel regels zijn omgezet. Een bestaand bestand
   wordt nooit overschreven. De originele export en spelgegevens worden niet gewijzigd.

**Stopt het script bij meerdere ID's?** De export bevat dan geen betrouwbare verdeling
per uitgave. Ga niet alle exemplaren aan de eerste ID toekennen en knip de andere ID's
niet weg. Controleer die kaarten in Arena en gebruik de handmatige route hieronder,
of laat de bronexport eerst inhoudelijk beoordelen. Ook opnieuw gebalanceerde Alchemy-
kaarten kunnen naam-/identiteitsverschillen opleveren die je afzonderlijk moet controleren.

De omzetting controleert bestandsstructuur en aantallen. Zij bewijst niet dat de scanner
iedere kaart vond, het juiste account las of juiste aantallen uit Arena haalde.

## 4. De collectie daadwerkelijk importeren

1. Ga in onze app naar **Collectie-import → Collectiebestand kiezen**.
2. Kies het omgezette `mijn-collectie-import.json`, niet de originele tracker-JSON.
3. Bekijk de voorbeeldweergave en alle pagina's. Voorbereiden verandert nog geen bezit.
4. Controleer per regel kaartnaam, uitgave, aantal en de getoonde doelprinting.
5. Los ongekoppelde of dubbele regels op met **Geselecteerde regel koppelen of overslaan**.
   Eén Arena-ID kan bij meerdere printings horen; kies bewust de juiste uitgave.
6. Vergelijk enkele kaarten rechtstreeks met Arena, inclusief kaarten met meerdere
   uitgaven en verschillende aantallen. Vergelijk ook het totaal aantal exemplaren.
7. Vink de controle aan en kies **Wijzigingen toepassen**.
8. Open **Mijn collectie** en controleer het resultaat.

De aantallen vervangen het bestaande aantal voor dezelfde printing: 2 → 4 wordt 4,
geen 6. Kaarten die ontbreken in het bestand blijven ongewijzigd. Een nieuwe export
is dus geen automatische volledige synchronisatie. Importeer geen nul-aantallen als
de scanner die alleen uit ontbrekende gegevens heeft afgeleid.

## 5. Zonder tracker: bezit handmatig registreren

Dit is direct bruikbaar als je met een beperkt deel van je collectie wilt beginnen.

1. Zoek in Arena een kaart waarvan je het bezit en de exacte uitgave kunt controleren.
2. Zoek dezelfde kaart in onze **Catalogus** en kies dezelfde printing.
3. Vul je werkelijke bezitsaantal in en kies **Collectieaantal opslaan**.
4. Herhaal voor de andere kaarten. Begin bijvoorbeeld met kaarten uit je gewenste deck.

Je kunt ook zelf een collectie-JSON maken. Dit is alleen een formaatvoorbeeld; vul je
eigen kaartgegevens en aantallen in. Neem deze aantallen niet over als werkelijk bezit.

```json
{
  "schemaVersion": 1,
  "cards": [
    {
      "setCode": "FDN",
      "collectorNumber": "227",
      "language": "en",
      "name": "Llanowar Elves",
      "quantity": 4
    }
  ]
}
```

Hier is `en` de taal van de gekozen catalogusprinting, geen gok op basis van je
Arena-menutaal. Zie [COLLECTION_IMPORT.md](COLLECTION_IMPORT.md) voor andere identiteiten
en de volledige formaatregels.

## 6. Alleen een deck overzetten

Selecteer een deck in Arena's deckoverzicht en zoek een **Export/Exporteren**-actie.
Als jouw client die aanbiedt, plak de gekopieerde lijst eerst in Kladblok om te zien
wat is gekopieerd. De exacte plaats van deze knop is hier niet live gecontroleerd.
Voor een bij een tracker opgeslagen lijst beschrijft Tapps ook **Copy deck list**.
[Tracker-deckdocumentatie](https://github.com/pattont/MTGA-Tapps#deck-manager-beta).

1. Gebruik bij voorkeur Engelse kaartnamen en secties zoals `Deck` en `Sideboard`;
   onze app vertaalt de tekst niet automatisch.
2. Open in onze app **Deck importeren**, plak de tekst of laad een `.txt`.
3. Kies naam/formaat en klik **Voorbereiden**.
4. Controleer en koppel alle regels, bevestig en kies **Nieuw deck opslaan**.

Dit registreert een deckplan, **geen bezit**. Gebruik deckaantallen nooit als vervanging
van een collectie-export. Een lijst kan kaarten bevatten die je niet bezit. Een
`Companion`-sectie wordt door v1.0 geweigerd; laat die eerst inhoudelijk controleren.

## Problemen herkennen

| Melding/probleem | Wat je doet |
| --- | --- |
| Onbekende velden, bijvoorbeeld `export_date` of `count` | Je koos waarschijnlijk de originele tracker-JSON; gebruik eerst de omzetting |
| Meerdere Arena-ID's bij omzetting | Stop; verdeling per uitgave ontbreekt, niet raden |
| Kaart niet gevonden of meerdere kandidaten | Catalogus vernieuwen en uitgave/identiteit handmatig controleren |
| Geen voorstel uit bezit | Controleer bezit én bronstatus; een deckimport vult bezit niet in |
| Aantallen zijn onverwacht laag | Controleer account, exportwaarschuwingen en enkele kaarten in Arena; niet blind toepassen |
| Import heeft een fout aantal toegepast | Historie kan herstel bieden zolang de betrokken aantallen daarna niet gewijzigd zijn; zie collectiehandleiding |

## Wat logs wel en niet betekenen

Wizards documenteert **Options → View Account → Detailed Logs**, gevolgd door een
herstart van Arena, voor logging en communitytools. Dat is geen garantie voor een
volledige collectie-export en onze app leest `Player.log` niet als collectie.
[Officiële loghandleiding](https://mtgarena-support.wizards.com/hc/en-us/articles/360000726823-Creating-Log-Files-on-PC-Mac-Steam).

Deze handleiding is gebaseerd op de gecontroleerde appcode en gepubliceerde bronnen.
Het omzettingsscript is getest met nagebouwde exportvoorbeelden; de volledige route
Arena → tracker → jouw collectie is nog niet met een echte export gevalideerd.
