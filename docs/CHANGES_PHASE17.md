# Wijzigingen fase 17 — v1.4.0

- Application: SynergyInteractionIndex vervangt de volledige voorafgaande paargraph;
  SynergyProfile cachet begrensd feiten met behoud van caller-identiteit/freshness.
- SynergyTextRules, SynergyTargets en SynergyEngine: cycling/typecycling, ETB-/sterf-
  subjecten, nontoken, eigen sacrifice versus delayed/symmetrisch en echte recursiedoelen
  met vaste type-/mana-valuegrenzen. Geen algemene Oracle-taalinterpreter.
- CommanderBuildPlan + 370-bronvocabulaire: expliciete type-/castafhankelijkheden;
  SynergySelection ondersteunt die en begrenst overmatige graveyard-setup.
  PaperDeckRules beschrijft de herkende bouwfocus en ondersteuning.
- PaperContracts/PaperCatalog: afwijkende regels/layouts uit bestaande bron-JSON;
  duidelijker simulatie-label. Geen migratie of wijziging van echte voorraad.
- SqlitePaperWorkspace: actualiteit batchgewijs lezen, alleen werkelijk verouderde
  bronnen vernieuwen; ontbrekende themapool vroeg uitleggen.
- Desktop PaperFilters/PaperViewModel/PaperView: achtergrondfilters met annulering en
  nieuwste-resultaatcontrole, algemene simulatiebanner; versie/fasestatus 1.4.0/fase17.
- Tests: SynergyFullPoolTests, CommanderBuildPlanTests, PaperSourceBatchTests,
  LargeCollectionUiTests en optionele FullCatalogUiTests met echte geïsoleerde bulkfixture.
- Meetgereedschap: FullCatalogProbe, FullCatalogPractice, Measure-SynergyScaling,
  Start-AllCardsTest; bestaande SynergyHoldout en Test-SynergyEvaluation ondersteunen
  ontwikkelregressies en de vooraf vastgelegde acht families. A/B-bronnen/waarheid onder examples.
- Distributieversie en actuele gidsen bijgewerkt; oude v1.3.0-release/nulmetingen bewaard.

Voor bewijs, afwijkingen, onvolledige herkenning en testresultaten:
[VALIDATION_PHASE17.md](VALIDATION_PHASE17.md). Artifacts en .local zijn onderzoeksbewijs,
geen meegeleverde echte gebruikerscollectie.
