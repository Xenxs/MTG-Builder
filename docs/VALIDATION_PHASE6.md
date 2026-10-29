# Verificatie fase 6 — 22 oktober 2026

Versie **0.6.0**. Expliciete startopdracht: "start fase 6", daarna "ga verder met fase 6".
Scope: uitlegbare alternatieven uit eigen collectie, gezamenlijke voorraad-/formaatcontrole
en opslag als nieuw deck. De zes projecten en bestaande pakketten zijn behouden.
Geen beveiligingsbeleid of chatinstellingen gewijzigd.

## Definitieve build en tests

Windows x64, .NET SDK 10.0.401. Uitgevoerd, inclusief succesvolle NuGet-restore:

```powershell
./scripts/Verify.ps1 -Screenshots -ReportPrefix phase6-delivery
```

**Release-build: 0 waarschuwingen, 0 fouten. 197 tests geslaagd, 0 mislukt,
0 overgeslagen.** Buildlog: artifacts/build.log.

| Testproject | Geslaagd | Resultaatbestand |
| --- | --- | --- |
| Domain.Tests | 28 | artifacts/test-results/phase6-delivery_net10.0_20261022111350.trx |
| Application.Tests inclusief SQLite/UI | 169 | artifacts/test-results/phase6-delivery_net10.0_20261022111408.trx |

De 168 bestaande tests blijven onderdeel van de controle. De eerste uitbreiding vond
een fout in de testopbouw: aantallen werden gewijzigd zonder de testcollectieprinting
eerst toe te voegen. Dit is met de bestaande catalogus-collectiefunctie hersteld.
De xUnit-analyzer vereiste ook doorgeven van de test-annuleringstoken. Geen controles
onderdrukt. De definitieve run heeft alle correcties en schermverbeteringen meegenomen.

## Getoetst gedrag

- Reprints eerst; stabiele rangschikking op kleur, manawaarde, hoofdtype en identiteit.
- Andere kaarten alleen voor mainboard-niet-landen; geen nieuwe kleuren; een landzijde
  sluit strategische vervanging uit. Reprints kunnen wel voor land/sideboard.
- Alle zones en uitgaven delen voorraad en kopielimieten. Twee tekorten kunnen niet
  dezelfde vrije exemplaren uitgeven. Exact sideboardbezit krijgt voorrang.
- Alleen ontbrekende exemplaren vervangen. Geen handmatige identiteit raden, onbekende
  legaliteit of inactieve printings accepteren. BO1-ban en onbekende kaartuitzonderingen
  blokkeren ook kandidaten. Ondersteunde copy-exceptions worden hergebruikt.
- Het oorspronkelijke deck moet de bestaande actuele regelcontrole doorstaan. Verouderde
  bron of regels blokkeren het advies/opslag, ook als de database verder onveranderd blijft.
- Veranderde voorraad, teruggezet bezitsaantal, bron, profiel, kaartmetadata, deckinhoud
  of verwijderd origineel blokkeren het oude voorbeeld. Geen nieuw deck bij een conflict.
- Voorbereiden wijzigt geen deck/bezit. Voorbereiding overleeft herstart op opslagniveau.
  Gelijktijdig opslaan vanuit onafhankelijke writer gates maakt één deck via SQLite.
- Een fout en annulering **na de werkelijke SQL-writes** rollen deck en ontvangst terug;
  daarna kan hetzelfde voorbeeld geldig opnieuw worden toegepast.
- Origineel en voorraad blijven gelijk; profiel en zones worden bewaard. Een gedeeltelijke
  oplossing houdt resterende tekorten expliciet. Replay maakt ook na deckverwijdering geen duplicaat.
- Gevulde fase-5-upgrade behoudt bezit, deckregels en profiel en maakt een upgradeback-up.
- Werkelijke Avalonia-selectie-, aantal-, naam- en commandobindingen kiezen, beoordelen
  en bewaren een nieuw deck. Na naamwijziging vervalt het voorbeeld; voorraadconflict
  toont de centrale foutmelding. Onbekend profiel toont herstel zonder keuzestappen.

## Schermen

Gerenderd met Avalonia Headless en daadwerkelijk visueel bekeken:

- artifacts/ui/deck-advice-candidates.png
- artifacts/ui/deck-advice-preview.png
- artifacts/ui/deck-advice-conflict.png
- artifacts/ui/deck-advice-blocked.png
- artifacts/ui/dashboard.png en artifacts/ui/settings.png

Na de eerste visuele controle zijn niet-bruikbare keuzevelden bij geblokkeerd advies
verborgen en staan de daadwerkelijke wijzigingen ook in het gezamenlijke voorbeeld.
De schermdata zijn testfixtures. Geen volledige DPI-, toegankelijkheids- of hardwarematrix.

## Proef met echte catalogus en gesimuleerd bezit

Een nieuwe SQLite-kopie in .local/phase6-smoke-data gebruikt de eerder online opgehaalde
Scryfall-catalogus: **21.621 Arena-printings**, bron **21 oktober 2026 21:06:00.423 UTC**.
Er is in deze fase geen nieuwe bulkdownload geclaimd. Op de controledatum was de bron
binnen de bestaande 24-uursgrens. De gebruikersdatabase is geen testfixture.

**2.003 gesimuleerde bezitsregels**, waaronder 2.000 catalogusprintings, een Forest-voorraad,
Negate-reprint en expliciete exacte bronprinting met aantal 0. Een technisch deck met
60 Forest en 2 Negate is gebruikt; dit is geen competitief deck.

- Advies lezen: **0,185 s**; **64 kandidaten** zoeken: **0,163 s**.
- Voorbeeld maken: **0,137 s**; nieuw deck opslaan: **0,152 s**.
- Gekozen: 1 Negate (DMU) 58 → Negate (FDN) 710 en 1 → Essence Scatter (AKR) 60.
- Resterende exacte tekorten: **0**; nieuwe deckcontrole geslaagd.
- Oorspronkelijke deckregels en alle bezitsregels gelijk; herhaalde opslag retourneert hetzelfde deck.
- Piekgeheugen van de gehele proef inclusief voorbereiding: **circa 118 MiB**.

Bewijs: artifacts/phase6-real-catalog.json. Tijdmetingen zijn één lokale meting, geen
prestatiegarantie. De echte resultaten tonen ook een inhoudelijke grens: Consult the Star
Charts kan op structurele overeenkomst hoog eindigen voor Negate, hoewel het geen counter
is. Kaartrol/werking wordt niet bewezen door deze rangschikking.

## Migratie, native Windows-start en gegevensbehoud

Zesde migratie **20261022090051_AddDeckAdvicePlans** is gegenereerd en het SQL-script
bekeken: artifacts/phase6-migration.sql. Binnen één transactie wordt alleen de nieuwe
DeckAdvicePlans-tabel toegevoegd, gevolgd door migratieregistratie. Geen bestaande tabel
verwijderd/opnieuw gebouwd; geen foreign-key-uitschakeling of EnsureCreated.

De definitieve executable is native gestart:

- Gevulde geïsoleerde fase-5-database: **11:14:56–11:14:58 Europe/Amsterdam**, PID **30372**,
  reagerend, database geïnitialiseerd, normaal gesloten na de controle.
- Normale gebruikersdatabase: **11:14:58–11:15:00**, PID **26732**, reagerend,
  titel MTG Arena Smart Deckbuilder, database geïnitialiseerd. App zichtbaar open gelaten.
- Nieuwe startlogs bevatten geen WRN/ERR/FTL. Geen beveiligingsbeleidswijziging.

Bewijs: artifacts/phase6-synthetic-native-start.json en phase6-user-native-start.json.
Geteste/native assemblyhashes:

| Bestand | SHA-256 |
| --- | --- |
| Desktop.dll | FDF0582A8A2D1174B991649F34B8FCF1998EFAF9BCF52ACFCB5AF973B404CAC5 |
| Infrastructure.dll | E5F1D2F5C21B565044D55C6E19B027022244C651FA6E0148A81E14FBBF9CBE01 |

Voor/na alleen-lezen tabelhashes van kaarten, printings, legaliteiten, collectie, decks,
deckregels, instellingen en beide importhistorieën zijn gelijk; migraties **5 → 6**.
De normale bezit/decktabellen waren leeg. De gevulde native controle behield **2 bezitsregels,
2 decks, 22 deckregels, 2 deckimportontvangsten en 22 auditregels**.

Bewijs: artifacts/phase6-user-before.json, phase6-user-after.json, phase6-user-preservation.json
en overeenkomstige phase6-synthetic-*.json. Beide starts maakten vooraf een upgradeback-up.
De normale back-up heeft **33.325.056 bytes**, SHA-256
**F00CAA3C9B6DA8D7995D8279E4F4E5BC6B9290C68E51EACC7020A457786B317F**.
Exacte dynamische locaties staan in de artifacts; geen gebruikerspaden in productcode.

## Oplevergrenzen

Dit is lokale, uitlegbare structurele matching met gecontroleerde voorraad en opslag.
Geen volledige deckgenerator, semantische rol-/synergieanalyse, bewezen winkansverbetering,
manabase-/sideboardstrategie, wildcardbudget, externe AI-service of live Arena-clienttest.
Voorbereidingen hebben nog geen UI-historie/opruimfunctie; na UI-herstart advies opnieuw berekenen.
De bestaande bron-/regelvervalgrenzen blijven gelden, zie docs/FORMAT_VALIDATION.md.

Fase 6 is opgeleverd ter beoordeling. Fase 7 is niet gestart.

Actieve uitvoering circa **25–30 minuten** (circa 10:53–11:20 Europe/Amsterdam), inclusief implementatie, herstel van testopbouw, definitieve controles en documentatie. Het modeladvies was GPT-6.1 Sol / High; geen bevestigde instellingswijziging.
