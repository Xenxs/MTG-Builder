# Formaatcontrole — fase 5 / v0.5.0

## Gebruik

1. Ververs eerst de kaartcatalogus online via Scryfall. Bestaande catalogusgegevens uit
   fase 4 krijgen bij deze verversing legaliteitsmetadata, ook bij dezelfde bronversie.
2. Open Mijn decks, kies het deck en de tab Formaatcontrole.
3. Kies Arena Standard BO1 of BO3 en klik Profiel bewaren en controleren.
4. Los de getoonde fouten op en controleer daarna opnieuw. Iedere melding bevat een
   reden en herstelrichting. Decks met fouten blijven gewoon opgeslagen en bewerkbaar.

Het oorspronkelijke formaatlabel wordt behouden. Een bestaand Standard-deck begint met
Niet gekozen / ander formaat; er wordt geen wedstrijdvariant geraden. Je expliciete
keuze blijft na herstart bewaard. Nieuwe decks/imports met het expliciete label Arena
Standard BO1 of BO3 krijgen dat profiel direct. Een vrij label voor een ander formaat
geeft geen gevalideerde regels voor dat formaat.

## Controles en betekenis

- Minimaal 60 mainboardkaarten; maximaal 15 sideboardkaarten voor beide varianten.
- Geen commander-zone in Standard.
- Maximaal vier per Engelse kaartnaam over mainboard, sideboard en alle printings samen.
- Cataloguskaarten met het Basic Land-type mogen onbeperkt voorkomen.
- Exact herkende Oracle-zinnen voor any number en up to one t/m ten passen de kaartlimiet
  aan. Dat maakt de kaart niet automatisch legaal in Standard: kaarttoegang blijft apart.
- Scryfall legalities.standard controleert toegang/rotatie en bans. De gecontroleerde
  Wizards-banlijst vult dat aan, waaronder Leyline of Resonance alleen voor BO1.
- Een onzekere handmatige identiteit, ontbrekende metadata, niet-actuele Arena-printing,
  onbekende status of niet-ondersteunde deckbouwclausule maakt de controle onvolledig.

**Geslaagd** betekent geen fout of onzekerheid binnen deze controles en de getoonde
brondata. **Niet toegestaan volgens de controles** betekent dat er fouten gevonden zijn;
eventuele onzekerheden blijven ook zichtbaar. **Onvolledig gecontroleerd** betekent dat
geen volledig oordeel mogelijk is. Het is geen goedkeuring door een ingelogde Arena-client.

Bezitaantallen veranderen niet door een profielkeuze of controle. Kaarten die je niet
bezit kunnen een legaal deck vormen; tekorten staan onder Analyse. Ook een volledig
legaal deck zegt niets over strategische kwaliteit of winkans.

## Actualiteit en offline gebruik

De UI toont regelversie, bronversie, brondatum, importdatum en controletijd. De online
manifestdatum is de brondatum; de tijd waarop je een lokaal bestand kopieert of importeert
bewijst de bronleeftijd niet. Bestandsimports blijven werken, maar geven daarom een
onvolledige actualiteitscontrole. Ook metadata die ontbreekt in een oud hervat checkpoint
blijft onbekend; een nieuwe online import herstelt dat.

Brondata ouder dan 24 uur, een datum meer dan vijf minuten in de toekomst of een onbekende
brondatum blokkeren een geslaagd oordeel. De ingebouwde aanvullende regels zijn op
22 oktober 2026 gecontroleerd en worden na 14 dagen als verouderd gemeld. Verversen van
kaartdata vernieuwt deze vaste regelversie niet. Daarvoor moet een ontwikkelaar de primaire
regels opnieuw controleren en een bijgewerkte appversie opleveren. Dit zijn conservatieve
grenzen; binnen die termijn kunnen regels alsnog veranderen. De app is geen live monitor.

Een eerdere controle geldt voor het getoonde moment. Wijzigingen aan profiel/deck/inhoud,
vernieuwen en opnieuw openen van de pagina wissen de uitkomst. Wijzigingen door een
andere appinstantie worden bij opnieuw controleren gelezen, niet automatisch gepusht.

## Grenzen

Andere formaten, volledige Companion-/Commander-regels en alle bijzondere naamequivalenties
vormen geen volledige geïmplementeerde regelengine. De importer accepteert nog steeds geen
Companion-sectie. Bij meer dan 250 mainboardkaarten wordt de niet-bewezen Arena-clientlimiet
als onzekerheid getoond. Er is geen actuele Arena-client ingelogd en geen competitief
of platformonafhankelijk legaliteitscertificaat afgegeven. Geen wildcardbudget of deckadvies.

## Gecontroleerde bronnen

- [Wizards Standard](https://magic.wizards.com/en/formats/standard).
- [Wizards: BO1-sideboard terug naar 15 per 3 februari 2026](https://magic.wizards.com/en/news/mtg-arena/announcements-february-2-2026).
- [Wizards banned/restricted](https://magic.wizards.com/en/banned-restricted-list).
- [Comprehensive Rules per 25 september 2026](https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt): 100.2a, 100.4a en 101.1.
- [Scryfall Card Objects](https://scryfall.com/docs/api/cards): legalities-veld. De webtool
  kreeg HTTP 403; de eerder opgeslagen documentatie en de echte API/bulkdata zijn aanvullend
  geraadpleegd. De online praktijkproef bewijst de concrete velden en verwerking.

Resultaten en precieze bewijsbestanden: [VALIDATION_PHASE5.md](VALIDATION_PHASE5.md).
