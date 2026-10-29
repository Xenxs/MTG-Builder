# Verificatie fase 7 — 22 oktober 2026

Versie **0.7.0**, expliciet gestart met "Start fase 7" om circa 11:25 Europe/Amsterdam.
Scope: beperkte kaartrollen, uitlegbare rolrangschikking en een vastgelegde referentiemeting.
Zes projecten, bestaande pakketversies en zes bestaande migraties behouden.
Geen externe AI-API, accountgegevens of beveiligingsbeleidswijziging.

## Definitieve build en tests

Op Windows x64 met .NET SDK 10.0.401 daadwerkelijk uitgevoerd, inclusief NuGet-restore:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase7-delivery
```

**Release-build: 0 waarschuwingen, 0 fouten. 239 tests geslaagd, 0 mislukt,
0 overgeslagen.** Buildlog: artifacts/build.log.

| Testproject | Geslaagd | Exact resultaatbestand |
| --- | --- | --- |
| Domain.Tests | 28 | artifacts/test-results/phase7-delivery_net10.0_20261022114037.trx |
| Application.Tests inclusief SQLite/UI | 211 | artifacts/test-results/phase7-delivery_net10.0_20261022114055.trx |

Alle 197 eerdere tests blijven onderdeel van de controle. Toegevoegd zijn dertig
Oracle-referenties, zes volledige-teksttegenvoorbeelden, doel-/voorwaarde-/timing-/mana-
vergelijkingen, de rangschikkingsmeting, versieopslag, oude ontvangsten, gewijzigde
gedrukte kosten en echte UI-bindingen. Een compileerfout in een tijdelijk exporthulpmiddel
(dubbele lokale variabelenaam) is hersteld; dat hulpmiddel is geen solutionproject.

## Referentieset en kwaliteit

examples/role-reference-v1.json bevat **30 volledige, handmatig nagekeken Oracle-teksten**
uit de eerder geïmporteerde echte catalogus. Verwachtingen zijn vastgelegd vóór de
classificatie-/rangschikkingstests. Een dubbele removalreferentie is vóór de tests
vervangen door Quick Study om ook instant-draw te toetsen. **18 herkend, 12 bewust
onbekend**; alle dertig behouden de verwachte rol, doelgroep en beperking.

Daarnaast zijn tien benoemde rangschikkingsscenario's vooraf van verwachte uitkomsten
voorzien. Beide algoritmen zien dezelfde kandidaatpool. De oude vergelijking reproduceert
de vaste fase-6-sorteersleutels op die pool; er wordt geen oude binary of tweede productie-
engine gebruikt. Rolherkenning verandert de geschiktheidsfilters niet.

| Meting op deze tien scenario's | Fase 6 | Fase 7 |
| --- | --- | --- |
| Verwachte eerste keuze | 3/10 | 10/10 |
| Gemiddelde omgekeerde positie van die keuze | 0,583 | 1,000 |

Voorbeelden: Negate kiest Cancel boven Essence Scatter; Go for the Throat kiest Murder
boven Dark Ritual; een instant-draw zoekt een herkende draw-rol boven een onbekende
cantrip. De beperkingen van Quick Study tegenover Opportunity en Rampant Growth tegenover
Farseek blijven in de verwachte uitkomst expliciet Limited.

Bewijs: artifacts/phase7-role-evaluation.json. **Dit is een kleine gekozen ontwikkel-
en regressieset, geen onafhankelijke of representatieve evaluatie.** Sommige kaarten zijn
niet Standard-legaal. In deze scenario's worden bezit en legaliteit gesimuleerd om de
rangschikking te isoleren. De productieapp controleert de echte opgeslagen legaliteit.
Geen winstpercentage, algemeen kwaliteitspercentage of volledige kaartinterpretatie bewezen.

De webweergave van Scryfall-documentatie gaf HTTP 403 en de named-API was via de webtool
niet toegankelijk. Daarom zijn geen actuele webteksten geclaimd; de daadwerkelijk eerder
online geïmporteerde catalogus is de primaire bron. De exacte gebruikte teksten zijn
zelfstandig meegeleverd in de referentie-JSON; de export staat ook in
artifacts/phase7-oracle-reference-source.json.

## Gecontroleerde functionaliteit

- Gehele Engelse tekst tegen gesloten patronen; niet alleen sleutelwoorden. Multimodale,
  meervoudige, extra-kosten-, triggered/activated en niet ondersteunde teksten blijven onbekend.
- Vier effectfamilies met doelwitten, herkenbare betaal- en manawaardebeperkingen,
  draw-aantallen, landtypen en tijdelijke mana. Geen tijdelijke versnelling bewezen
  wanneer het bruto aantal mana niet boven de catalogus-manawaarde ligt.
- Creature-counter versus noncreature-counter, betaaluitweg, minder kaarten,
  instant-/sorcerytiming, tijdelijke mana versus land en verschillen in removal blijven zichtbaar.
- Dezelfde Oracle-kaart blijft eerst; daarna Matching, Limited, Unknown, Different;
  vervolgens de bestaande stabiele structurele criteria.
- Rolstatus verandert de bestaande voorraad-, kleur-/type-, kopie-, bronactualiteits-
  of formaatcontroles niet. Geen sterke rolclaim voor onbekende of beperkte matches.
- Nieuwe voorbereidingen bewaren engine-/classifierversie, keuzes en beoordeelde uitleg
  met herkenningsregel. Oude ongebruikte voorbereidingen blijven bewaard maar vereisen
  herberekenen; oude toegepaste ontvangsten blijven idempotent.
- Manakosten zijn onderdeel van de gegevensvingerafdruk. Wijzigingen blokkeren ook
  het oude voorbeeld wanneer de catalogus-manawaarde gelijk blijft.
- Bestaande fout-/annuleringsproeven na SQL-writes, onafhankelijke writer gates,
  voorraadconflicten, gegevensbehoud en migratietests blijven groen.
- Werkelijke UI-bindingen tonen Matching, Limited en Unknown, laten een beperkte
  match bewust kiezen en bewaren die waarschuwing opnieuw in het gezamenlijke voorbeeld.

## Schermen

Definitief gerenderd met Avalonia Headless en daadwerkelijk bekeken:

- artifacts/ui/deck-roles-matching.png
- artifacts/ui/deck-roles-limited.png
- artifacts/ui/deck-roles-unknown.png
- artifacts/ui/deck-roles-preview.png
- artifacts/ui/dashboard.png en artifacts/ui/settings.png

Dit zijn testfixtures; de teksten Missing/Spare/Better zijn geen geïmporteerde echte
kaartidentiteiten. De testmetadata zijn consistent met de getoonde blauwe manakosten.
Geen volledige DPI-, toegankelijkheids- of hardwarematrix en geen native screenshots geclaimd.

## Echte catalogus en praktijkdekking

Nieuwe geïsoleerde SQLite-kopie .local/phase7-smoke-data-final, gebaseerd op de fase-6-
proefdatabase, met **21.621 Arena-printings** en **2.003 gesimuleerde bezitsregels**.
Geen gebruikersdatabase als testfixture. De bron is de bestaande Scryfall-snapshot van
**21 oktober 2026 21:06:00.423 UTC**, binnen de 24-uursgrens tijdens de proef.
Geen nieuwe bulkdownload in deze fase geclaimd.

Van **5.164 Standard-legale kaartdefinities** worden er slechts **16 herkend**:
7 counterspells, 7 removal, 1 draw en 1 mana-versnelling; **5.148 onbekend**.
Binnen het typebereik van **1.022 kaarten met exact Instant/Sorcery** is dat circa 1,6%;
over alle Standard-legale kaarten circa 0,3%. Dit is een beperkte eerste rolmodule.
De hoge score op de kleine referentieset bewijst geen brede dekking.

In de echte Negate-proef staat na de reprint nu Cancel als rolpassende kandidaat.
Essence Scatter wordt uitdrukkelijk Limited wegens andere doelwitten. De proef kiest
bewust één reprint en één beperkte vervanger om uitleg en opslag te controleren,
niet om een competitief beter deck te claimen.

| Bewerking | Eenmalige lokale meting |
| --- | --- |
| Advies lezen | 0,193 s |
| 64 kandidaten zoeken | 0,168 s |
| Voorbeeld opslaan | 0,112 s |
| Nieuw deck toepassen | 0,111 s |
| Piekgeheugen gehele proef | circa 121 MiB |

Nieuwe deckcontrole geslaagd, 0 resterende tekorten; origineel en alle bezitsregels
gelijk. Herhaalde opslag geeft hetzelfde deck. Een uit fase 6 overgenomen toegepaste
ontvangst geeft eveneens dezelfde resultaat-ID. Bewijs:
artifacts/phase7-real-catalog-final.json. Eerdere tussenmeting staat in phase7-real-catalog.json;
de definitieve meting bevat ook de laatste tijdelijke-mana-controle.

## Native start en gegevensbehoud

Geen schemawijziging nodig: de bestaande ChoicesJson-kolom bewaart nu een versieerbaar
object. De model-/migratiecontrole vindt geen ontbrekende migratie. De zes bestaande
migraties blijven ongewijzigd; geen EnsureCreated, databaseverwijdering of tabelrebuild.

De definitieve Windows-executable is gestart en geïnitialiseerd:

- Gevulde fase-6-data: **11:42:05–11:42:07 Europe/Amsterdam**, PID **1428**, reageert,
  daarna normaal gesloten.
- Normale gebruikersdata: **11:42:07–11:42:09**, PID **32700**, reageert,
  titel MTG Arena Smart Deckbuilder, zichtbaar open gelaten.
- Nieuwe logs: geen WRN/ERR/FTL. Geen beveiligingsinstellingen gewijzigd.

Bewijs: artifacts/phase7-synthetic-native-start.json en phase7-user-native-start.json.
Geteste/native hashes:

| Bestand | SHA-256 |
| --- | --- |
| Desktop.dll | 220023D4C9B2A1A68ADF091DF6ABB1735F532F8CE02217EE317902C0743822EA |
| Infrastructure.dll | 28F9AAF30B006654B67B501BD869CC337F31492DB34994C2A3AFFD5D472537F2 |

Voor/na-hashes van **13 bestaande tabellen**, inclusief kaart-/legaliteitsdata,
alle imports, adviesontvangsten, bezit, decks en instellingen zijn gelijk. Beide databases
houden zes migraties. De normale bezit/decktabellen waren leeg; de gevulde native proef
behield **2.003 bezitsregels, 4 decks, 11 deckregels en 1 oude adviesontvangst**.
Bewijs: artifacts/phase7-user-before.json, phase7-user-after.json, phase7-user-preservation.json
en overeenkomstige phase7-synthetic-bestanden.

Er is geen migratie toegepast en daarom ook geen nieuwe upgradeback-up gemaakt:
bestaande aantallen blijven 5 normaal en 1 in de geïsoleerde bron. Eerdere back-ups
blijven bestaan. Dit is geen periodieke back-updienst.

## Oplevergrenzen

Gebruik en grenzen: docs/CARD_ROLES.md en docs/DECK_ALTERNATIVES.md. De beperkte dekking
moet leidend zijn in productclaims. Geen volledige deckgenerator, algemene semantische
kaartkennis, manabase-/synergie-engine, wildcardbudget, metagame, externe AI-service
of test in een ingelogde Arena-client. De eerdere bron-/regelvervalgrenzen blijven gelden.

Fase 7 wordt ter beoordeling opgeleverd. Fase 8 is niet gestart.

Werkelijke actieve uitvoering circa **20–25 minuten**, inclusief bronexport, implementatie, referentiemeting, build/tests, native start en documentatie. Advies bij start: GPT-6.1 Sol / High; geen chatinstellingen gewijzigd omdat ondersteunde bediening ontbreekt.
