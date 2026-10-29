# Validatie fase 14 — fysiek Magic / v1.1.0

27 oktober 2026. Opdracht: fysieke kaarten, telefoon-/ManaBox-CSV, Scryfall-afbeeldingen,
Conflux-testbezit, aanvullend Commander-generatie en de FINAL FANTASY Starter Kit.

## Build en automatische controles

`scripts/Verify.ps1 -Screenshots`: **build geslaagd, 0 waarschuwingen, 0 fouten**.
**422 tests geslaagd**, 36 Domain en 386 Application/SQLite/UI, geen overgeslagen tests.
Bewijs: artifacts/build.log en artifacts/test-results/phase14_net10.0_20261027231012.trx,
phase14_net10.0_20261027231053.trx. De aangescherpte afbeelding-identiteitstest is daarna
ook afzonderlijk geslaagd: phase14-final-image-identity-render.trx.

Getest:
- CSV: UTF-8/BOM, quotes, escaped quotes, meerregelige velden, drie scheidingstekens,
  ongeldige aantallen/IDs/headers en onbekend bindertype. Exacte identiteit, taal,
  finish/conditie/binder, proxy/list uitsluiting en expliciete conflictkeuze.
- Review wijzigt geen bezit; apply absoluut, idempotent en atomair. Gewijzigde-en-weer-
  teruggewijzigde voorraad blokkeert oude preview/undo. Werkelijke SQL-rollback na
  geïnjecteerde fout of annulering, persistent receipt en herstart/herstel.
- Conflux CSV 63 regels/82 exemplaren; gezamenlijke CSV **118 regels/202 exemplaren**.
  Eenmalige Starter Kit-toevoeging behoudt iedere oudere voorraadrij/versie en echte
  handmatige registratie. Seedmarkers kunnen niet handmatig als import worden hersteld.
- Decktekst/kopie/voorraad-/metadatafingerprints, idempotente nieuwe-deckopslag,
  geen bezitmutatie; fysieke collectornummers behouden hun originele case.
- Casual60-Conflux: 60 kaarten, 36 niet-landen/24 basics, exact uitgegeven voorraad,
  Oracle-kopielimiet, expliciete planning en gewijzigde referentiemetadata.
- Commander: exact 100/99+1, singleton over printings, kleuridentiteit, basic-landtypen,
  geen sideboard, legale/fresh bronnen, ban-/off-color-/stale uitsluiting, kleurloos/Wastes,
  basic/snow-basic onbeperkt. Commander-geschiktheid volgens actuele front-face regels,
  legendary Vehicle/Spacecraft/P-T en expliciete uitzondering; variabele kleurkeuze geweigerd.
- Commander-bezit/planning, gerichte Scryfall-aanvulling, ontbrekende kaarten nooit als
  bezit, volledige review vóór save, voorraad-/metadatawijziging en bronverval tussen
  review/save blokkeren. Opgeslagen Commander-profiel/100 kaarten overleven herstart.
- Scryfall exact ID/natuurlijke sleutel, game:paper, imagehost, bounded cache en aparte
  voor-/achterzijde; UI-bindingen en fout-/bevestigingsroutes.

## Echte netwerk- en opslagproef

`scripts/PhysicalProbe` uitgevoerd in `.local/phase14-commanders-delivery` met echte
Scryfall-metadata en uitdrukkelijk gesimuleerd bezit. Vier brondecks: 41 + 41 + 60 + 60.
**202 speelkaarten, 109 exacte uitgaven, 118 posities**. Vier Starter Kit-tokens en
Arena-codekaarten tellen niet als speelkaarten mee.

De volledige CSV werd zonder onopgeloste identiteit toegepast. Een bewerkbare kopie,
60-kaart bezitvoorstel en Commander-planning zijn als nieuwe decks opgeslagen. Daarna
back-up, latere voorraadwijziging, gecontroleerd herstel en import-undo uitgevoerd.
Na herstel: 202 kaarten en zeven decks; geen verlies van fysieke/advies-/importgegevens.
Bewijs: **artifacts/phase14-real-commander-delivery.json**.

Cloud-bezitmodus blokkeerde terecht: **34/62 geschikte verschillende niet-landen**.
Planning leverde **100 kaarten** met alleen bezitstekorten: **32 ontbrekende exemplaren
van 29 Oracle-kaarten**. De ontbrekende kaarten werden niet aan bezit toegevoegd.
Dit meet de concrete opgeslagen proef, geen generieke kwaliteit of winrate.

Echte afbeelding: **Adelbert Steiner, FIN 3, en**, 82.327 bytes JPEG,
SHA-256 `A3B1C3945E4BB10D761AB52F88BED7A7FF0421C19CA1D9E2C910D6E3A1EDCC9D`.
De UI-test koppelt de bestands-ID aan de exact geselecteerde printing en decodeert/toont
het beeld: artifacts/ui/paper-scryfall-image.png. Compact venster van 1050 px is ook
gecontroleerd (paper-compact.png). Alle gewijzigde paper-*.png-schermen zijn bekeken.
Zelfstandige offline beeldtest zonder netwerk is eerder afzonderlijk geslaagd.

## Bestaande gegevens en Windows-start

Initiële overgang: alle vijftien Arena-tabellen op beide bestaande databases ongewijzigd,
vijf fysieke tabellen erbij; migratie 7 → 8 met automatische reservekopie. Bewijs:
artifacts/phase14-data-preservation.json en phase14-{user,filled}-{before,after}.json.

Vóór de Starter Kit-toevoeging zijn consistente SQLite-reservekopieën gemaakt in
artifacts/phase14-final-{user,filled}-before.db. Daarna native v1.1.0 op beide bestaande
mappen gestart en normaal gesloten. Ieder proces reageerde en initialiseert zonder
UI-fout. Native logs: artifacts/phase14-{user,filled}-native.json.

De uitgebreidere audit vergelijkt **iedere oorspronkelijke rij en alle kolommen** via
de primaire sleutel: op beide databases bleven oorspronkelijke gegevens van **alle
20 applicatietabellen** behouden (38.204 respectievelijk 56.656 rijen). Alleen de nieuwe
productdata is toegevoegd: nu 118 fysieke posities en vier productdecks, nog acht
migraties. Bewijs: artifacts/phase14-final-{user,filled}-after.json en
artifacts/phase14-final-preservation.json. Beveiligingsbeleid niet gewijzigd.

De Windows-computer-use-helper weigerde het venster te koppelen met een fout over de
venstereigenaar, ook na één herselectie. Geen handmatige native klik-/screenshotcontrole
claimen. Echte processtart, netwerk/cache, gegevens en gerenderde Avalonia-schermen
zijn wel gecontroleerd. De installatiecontrole wordt afzonderlijk na het verpakken
vastgelegd onder artifacts/phase14-install-delivery-*; dit bronverslag wordt aangevuld.

## Grenzen

Geen echte telefoonexport of volledige scanherkenningsketen. Het ManaBox-schema is
getest met gegenereerde voorbeelden; willekeurige scanners kunnen expliciete omzetting
vereisen. Geen gameplay, onafhankelijke adviesbenchmark, bewezen synergie, optimaal
mana, casting-/winkans of bracket/cEDH-classificatie. Commander ondersteunt één geschikte
commander; geen partner/background-paren, variabele kleurkeuze of volledige uitzonderingsengine.

Vaste constructieplannen: Conflux-Casual60 en Commander 62 niet-landen/37 basics plus
commander. Andere gewenste landverhoudingen, Modern en algemene optimalisatie zijn nog
niet geleverd. Planning is expliciet en geen bezit. Andere decks reserveren geen kaarten.
Prijs-/aankoopoptimalisatie ontbreekt. Bronnen/regels verlopen; onderhoud blijft nodig.
Geen schone tweede pc, code signing, ARM of prestatiegarantie op zeer grote collecties.
Afbeeldingscache en logs vallen buiten databaseback-up. Oude bekende back-ups kunnen
bij een nieuwe startup de nog ontbrekende productdemo toegevoegd krijgen; echte voorraad
blijft een aparte actieve bron.

Modeladvies: GPT-6.1 Sol / High; geen ondersteunde instellingsbediening, niets gewijzigd.
Start circa 21:41 Europe/Amsterdam; Commander-uitbreiding circa 22:36. Aanvankelijke raming
2–4 uur plus 1–2 uur voor uitbreiding. Werkelijke eindtijd en pakketproef volgen in
PROJECT_PLAN.md na afronding. Geen nieuwe fase starten zonder opdracht.

## Definitieve pakketproef — 23:23 Europe/Amsterdam

Definitieve self-contained win-x64-installer en portable zip gebouwd en SHA-256 geverifieerd.
Werkelijke v1.0.0-installer → definitieve v1.1.0, uninstall, herinstallatie en tweede
uninstall op geïsoleerde paden geslaagd. Programma gebruikte zijn eigen coreclr.dll;
processen reageerden. De test wacht ook op de complete fysieke startwerkruimte.
Na upgrade/herinstallatie/verwijderen: 118 fysieke posities, vier productdecks en alle
bestaande gegevens behouden, acht migraties en één upgradeback-up. Snelkoppeling klopt
en wordt bij uninstall verwijderd. Authenticodesignature: NotSigned.
Bewijs: artifacts/phase14-install-delivery-installation.json en bijbehorende audits/logs.

Installer: 55.041.786 bytes,
SHA-256 `7CBA1A7A043B721B7DE2E428BFAC9ADD59A048E5F2492F0FE0C857D722622E0E`.
Portable zip: 79.477.926 bytes,
SHA-256 `9C3C80B37867A6402AB5EB76B8D45B68F629E84C6EE58DA7C5E91C6D91F192F1`.
Manifest: artifacts/release/1.1.0/release-manifest.json. Voorlopige pre-Commander-build
is apart gearchiveerd onder .local/phase14-package-preview; die is niet de oplevering.
De verpakte handleidingen zijn een momentopname vóór deze laatste installatieproef;
dit projectverslag en de afzonderlijke eindrapportage bevatten het latere bewijs.

Definitieve portable v1.1.0 om circa 23:24 zichtbaar geopend met de normale gegevensmap,
reagerend venster en meegeleverde runtime bevestigd; open gelaten. Procesbewijs:
artifacts/phase14-final-open.json. Bron-/bestandsinventaris: docs/FILES.md.
