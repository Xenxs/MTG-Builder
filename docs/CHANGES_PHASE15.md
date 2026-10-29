# Gemaakte en gewijzigde bestanden — fase 15

De complete broninventaris staat in FILES.md. Hieronder de relevante fasewijzigingen.

| Onderdeel | Bestanden / wijziging |
| --- | --- |
| Logo en Windows-icoon | Desktop/Assets/magic-logo.webp (origineel), magic-logo.png, magic-mark.png, magic.ico; scripts/BrandAssets voor reproduceerbare conversie |
| Vormgeving en navigatie | Desktop/App.axaml, MainWindow.axaml, App.axaml.cs, app.manifest, Desktop.csproj en ShellViewModel.cs |
| Collectiefilters | Application/CardThemes.cs, PaperCard in PaperContracts.cs; Desktop/ViewModels/PaperFilters.cs, PaperViewModel.cs en Views/PaperView.axaml |
| Deckwizard | Application/DeckWizardRequest.cs; Desktop/ViewModels/DeckWizardViewModel.cs en Views/DeckWizardView.axaml; ViewCodeBehind.cs geeft alleen UI-events door |
| Constructie en bronnen | PhysicalDeckPlanner.cs, CommanderDeckPlanner.cs, SqlitePaperWorkspace.cs, ScryfallPaperCatalog.cs en contracten IPaperWorkspace/IPaperCatalog |
| Veilig legen | Application/DatabaseResetContracts.cs, Infrastructure/SqliteDatabaseResetService.cs, SqliteBackupService.cs; Desktop/SettingsResetViewModel.cs en SettingsView.axaml |
| Instellingen en migratie | Domain/UserSettings in Models.cs; DeckbuilderDbContext.cs; 20261028062821_DisableExamplesOnReset.cs, designer en modelsnapshot |
| Historische UI | Verplaatst van Desktop naar tests/MTGDeckbuilder.Application.Tests/Legacy: oude Arena-viewmodels/views, LegacyShell en LegacyWindow; bestaande UI-regressies gebruiken die testkopieën |
| Automatische controles | DatabaseResetTests.cs, WizardAndFiltersTests.cs, WizardPlanningTests.cs; TestDatabase en eerdere UI-/migratietests aangepast aan scheiding en migratie negen |
| Praktijk- en distributiecontrole | scripts/WizardProbe; DatabaseAudit uitgebreid met oude-kolomvergelijking; Verify.ps1, Package.ps1, Test-Installation.ps1, Test-PhysicalStartup.ps1 en installer/MTGDeckbuilder.iss |
| Versie | Directory.Build.props: 1.2.0 |
| Documentatie | PROJECT_PLAN, AGENTS, README, ARCHITECTURE, DECISIONS (ADR-059–061), FILES, MAGIC_WIZARD_HANDLEIDING, VALIDATION_PHASE15, RELEASE_GUIDE, scanhandleiding en back-uphandleiding |

Desktop-paden zijn relatief aan src/MTGDeckbuilder.Desktop, Application aan
src/MTGDeckbuilder.Application en Infrastructure aan src/MTGDeckbuilder.Infrastructure.
De fase introduceert geen nieuw NuGet-pakket in de solution. De zes projecten blijven bestaan.
