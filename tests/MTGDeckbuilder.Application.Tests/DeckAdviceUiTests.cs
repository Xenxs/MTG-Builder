using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckAdviceUiTests
{
    [AvaloniaFact]
    public Task ModalOptionsStaySeparateThroughPreviewAndDatabaseStorage() => NewRoleFlow(modal: true);
    [AvaloniaFact]
    public Task DamageAmountAndLethalityWarningSurvivePreviewAndStorage() => NewRoleFlow(modal: false);
    private static async Task NewRoleFlow(bool modal)
    {
        var handler = DeckAdvicePersistenceTests.Handler();
        handler.Cards[1]["oracle_text"] = modal ? "Counter target noncreature spell." : "Missing deals 3 damage to target creature.";
        handler.Cards[2]["oracle_text"] = modal ? "Choose one —\n• Counter target spell.\n• Draw two cards." : "Spare deals 2 damage to target creature.";
        foreach (var card in handler.Cards.Skip(1)) { card["color_identity"] = new[] { modal ? "U" : "R" }; card["mana_cost"] = modal ? "{1}{U}" : "{1}{R}"; card["cmc"] = 2; }
        await using var test = DeckValidationPersistenceTests.Database(handler); await DeckAdvicePersistenceTests.Seed(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("decks");
        var vm = Assert.IsType<DecksViewModel>(shell.CurrentPage); var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        var name = modal ? "deck-roles-modal" : "deck-roles-damage";
        try
        {
            window.Show(); Layout(window); Find<TabControl>(window, "DeckWorkspaceTabs").SelectedIndex = 4; Layout(window);
            await Execute(window, "CalculateAdvice"); await Execute(window, "FindAdviceCandidates");
            var role = Assert.Single(vm.AdviceCandidates).Role!; Assert.Equal(RoleFit.Limited, role.Fit);
            var visible = Find<TextBlock>(window, "AdviceRoleDetails").Text!;
            Assert.Contains(modal ? "Optie 2: Draw two cards." : "Toughness", visible);
            if (modal) { Assert.True(role.Candidate.IsModal); Assert.False(role.Candidate.Complete); }
            else { Assert.Equal(2, role.Candidate.Amount); Assert.True(role.Candidate.FullyRecognized); }
            Capture(window, name);
            await Execute(window, "AddAdviceChoice"); await Execute(window, "PreviewAdvice");
            Assert.Equal(RoleFit.Limited, Assert.Single(vm.AdvicePreview!.Advice.Changes).Role!.Fit);
            Capture(window, name + "-preview", bottom: true);
            await using (var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync())
            {
                var json = await db.DeckAdvicePlans.Select(x => x.ChoicesJson).SingleAsync();
                using var payload = System.Text.Json.JsonDocument.Parse(json);
                var saved = payload.RootElement.GetProperty("ReviewedChanges")[0].GetProperty("Role").GetProperty("Candidate");
                Assert.Equal(CardRoleClassifier.Version, saved.GetProperty("Version").GetString());
                if (modal) Assert.Equal(2, saved.GetProperty("Options").GetArrayLength());
                else Assert.Equal(2, saved.GetProperty("Amount").GetInt32());
            }
            await Execute(window, "ApplyAdvice"); Assert.Equal(2, vm.Decks.Count); Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task PartialEvidenceAndUnparsedTextStayVisibleThroughPreviewAndStorage()
    {
        var handler = DeckAdvicePersistenceTests.Handler();
        handler.Cards[1]["oracle_text"] = "Counter target noncreature spell.";
        handler.Cards[2]["oracle_text"] = "Counter target spell. Create a 1/1 Bird creature token.";
        foreach (var card in handler.Cards.Skip(1)) { card["color_identity"] = new[] { "U" }; card["mana_cost"] = "{1}{U}"; card["cmc"] = 2; }
        await using var test = DeckValidationPersistenceTests.Database(handler); await DeckAdvicePersistenceTests.Seed(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("decks");
        var vm = Assert.IsType<DecksViewModel>(shell.CurrentPage); var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window); Find<TabControl>(window, "DeckWorkspaceTabs").SelectedIndex = 4; Layout(window);
            await Execute(window, "CalculateAdvice"); await Execute(window, "FindAdviceCandidates");
            var role = Assert.Single(vm.AdviceCandidates).Role!;
            Assert.False(role.Candidate.FullyRecognized); Assert.Equal(RoleFit.Limited, role.Fit);
            Assert.Contains("Onvolledige rol-aanwijzing", Find<TextBlock>(window, "AdviceRoleDetails").Text);
            Assert.Contains("Create a 1/1 Bird", Find<TextBlock>(window, "AdviceRoleDetails").Text);
            Capture(window, "deck-roles-partial");
            await Execute(window, "AddAdviceChoice"); await Execute(window, "PreviewAdvice");
            Assert.False(Assert.Single(vm.AdvicePreview!.Advice.Changes).Role!.Candidate.Complete);
            Capture(window, "deck-roles-partial-preview", bottom: true);
            await Execute(window, "ApplyAdvice"); Assert.Equal(2, vm.Decks.Count); Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task RoleDetailsSeparateMatchingLimitedAndUnknownChoicesInBoundControls()
    {
        var handler = DeckAdvicePersistenceTests.Handler();
        handler.Cards[1]["oracle_text"] = "Counter target noncreature spell.";
        handler.Cards[2]["oracle_text"] = "Counter target creature spell.";
        handler.Cards[1]["color_identity"] = new[] { "U" }; handler.Cards[2]["color_identity"] = new[] { "U" };
        handler.Cards[1]["mana_cost"] = "{1}{U}"; handler.Cards[1]["cmc"] = 2;
        handler.Cards[2]["mana_cost"] = "{1}{U}"; handler.Cards[2]["cmc"] = 2;
        var better = DeckValidationPersistenceTests.LegalCard("Better", "4");
        better["oracle_text"] = "Counter target spell."; better["color_identity"] = new[] { "U" }; better["cmc"] = 3; better["mana_cost"] = "{1}{U}{U}";
        var unknown = DeckValidationPersistenceTests.LegalCard("Uncertain", "5");
        unknown["oracle_text"] = "Choose one —\n• Create a token.\n• Gain 2 life."; unknown["color_identity"] = new[] { "U" };
        unknown["mana_cost"] = "{1}{U}"; unknown["cmc"] = 2;
        handler.Cards = [.. handler.Cards, better, unknown];
        await using var test = DeckValidationPersistenceTests.Database(handler); await DeckAdvicePersistenceTests.Seed(test);
        var catalog = test.Provider.GetRequiredService<CatalogService>(); var entries = await catalog.SearchAsync(new(), TestContext.Current.CancellationToken);
        foreach (var name in new[] { "Better", "Uncertain" })
            await catalog.SetOwnedQuantityAsync(entries.Items.Single(x => x.Name == name).PrintingId, 2, TestContext.Current.CancellationToken);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("decks");
        var vm = Assert.IsType<DecksViewModel>(shell.CurrentPage); var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window); Find<TabControl>(window, "DeckWorkspaceTabs").SelectedIndex = 4; Layout(window);
            await Execute(window, "CalculateAdvice"); await Execute(window, "FindAdviceCandidates");
            Assert.Equal(new[] { RoleFit.Matching, RoleFit.Limited, RoleFit.Unknown }, vm.AdviceCandidates.Select(x => x.Role!.Fit));
            Assert.Contains("counter.target.v1", Find<TextBlock>(window, "AdviceRoleDetails").Text);
            Capture(window, "deck-roles-matching");
            Find<ComboBox>(window, "AdviceCandidatePicker").SelectedItem = vm.AdviceCandidates[1]; Layout(window);
            Assert.Contains("Beperkte rolmatch", Find<TextBlock>(window, "AdviceRoleDetails").Text);
            Capture(window, "deck-roles-limited");
            Find<ComboBox>(window, "AdviceCandidatePicker").SelectedItem = vm.AdviceCandidates[2]; Layout(window);
            Assert.Contains("Rol niet bewezen", Find<TextBlock>(window, "AdviceRoleDetails").Text);
            Capture(window, "deck-roles-unknown");
            Find<ComboBox>(window, "AdviceCandidatePicker").SelectedItem = vm.AdviceCandidates[1]; Layout(window);
            await Execute(window, "AddAdviceChoice"); await Execute(window, "PreviewAdvice");
            Assert.Equal(RoleFit.Limited, Assert.Single(vm.AdvicePreview!.Advice.Changes).Role!.Fit);
            Capture(window, "deck-roles-preview", bottom: true);
            Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task BoundControlsChooseReviewInvalidateAndSaveAsNewDeck()
    {
        await using var test = DeckValidationPersistenceTests.Database(DeckAdvicePersistenceTests.Handler());
        var seed = await DeckAdvicePersistenceTests.Seed(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("decks"); var vm = Assert.IsType<DecksViewModel>(shell.CurrentPage);
        var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window); Find<TabControl>(window, "DeckWorkspaceTabs").SelectedIndex = 4; Layout(window);
            await Execute(window, "CalculateAdvice"); Assert.True(vm.Advice!.Baseline.Passed);
            Assert.Equal(1, Find<ComboBox>(window, "AdviceNeedPicker").ItemCount);
            await Execute(window, "FindAdviceCandidates"); Assert.Single(vm.AdviceCandidates);
            Assert.Contains("geen gelijkwaardig", Find<TextBlock>(window, "AdviceReason").Text);
            Capture(window, "deck-advice-candidates");
            Find<NumericUpDown>(window, "AdviceQuantity").Value = 2; Layout(window);
            await Execute(window, "AddAdviceChoice"); Assert.Empty(vm.AdviceNeeds); Assert.Single(vm.AdviceChanges);
            Find<TextBox>(window, "AdviceDeckName").Text = "Reviewed new deck"; Layout(window);
            await Execute(window, "PreviewAdvice"); Assert.True(vm.HasAdvicePreview); Assert.Single(await test.Service.GetDecksAsync(TestContext.Current.CancellationToken));
            Find<TextBox>(window, "AdviceDeckName").Text = "Renamed review"; Layout(window); Assert.False(vm.HasAdvicePreview);
            await Execute(window, "PreviewAdvice"); Capture(window, "deck-advice-preview", bottom: true);
            await test.Service.SetCollectionQuantityAsync(seed.Spare, 1, TestContext.Current.CancellationToken);
            await Execute(window, "ApplyAdvice"); Assert.False(vm.HasAdvicePreview); Assert.Contains("gewijzigd", vm.Operations.ErrorMessage);
            Assert.Single(await test.Service.GetDecksAsync(TestContext.Current.CancellationToken));
            Capture(window, "deck-advice-conflict");
            await test.Service.SetCollectionQuantityAsync(seed.Spare, 2, TestContext.Current.CancellationToken);
            await Execute(window, "CalculateAdvice"); await Execute(window, "FindAdviceCandidates");
            Find<NumericUpDown>(window, "AdviceQuantity").Value = 2; Layout(window);
            await Execute(window, "AddAdviceChoice"); await Execute(window, "PreviewAdvice"); await Execute(window, "ApplyAdvice");
            Assert.Equal(2, vm.Decks.Count); Assert.NotEqual(seed.Deck, vm.SelectedDeck!.Id); Assert.Null(vm.Advice);
            Assert.Contains(await test.Service.GetDeckCardsAsync(seed.Deck, TestContext.Current.CancellationToken), x => x.PrintingId == seed.Missing);
            Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task UnsupportedProfileShowsRepairInstructionsAndNoChoices()
    {
        await using var test = DeckValidationPersistenceTests.Database(DeckAdvicePersistenceTests.Handler());
        var seed = await DeckAdvicePersistenceTests.Seed(test);
        await DeckValidationPersistenceTests.Store(test).SetProfileAsync(seed.Deck, DeckRuleProfile.Unspecified, TestContext.Current.CancellationToken);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("decks");
        var vm = Assert.IsType<DecksViewModel>(shell.CurrentPage); var window = new LegacyWindow { DataContext = shell };
        try
        {
            window.Show(); Layout(window); Find<TabControl>(window, "DeckWorkspaceTabs").SelectedIndex = 4; Layout(window);
            await Execute(window, "CalculateAdvice"); Assert.False(vm.CanReviewAdvice); Assert.Empty(vm.AdviceNeeds);
            Assert.Contains("Eerst de formaatcontrole", Find<TextBlock>(window, "AdviceSummary").Text);
            Capture(window, "deck-advice-blocked");
            vm.SelectedValidationProfile = StandardDeckValidator.Profiles[1]; Assert.Null(vm.Advice);
        }
        finally { window.Close(); }
    }
    private static async Task Execute(Window window, string name)
    {
        var command = Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, name).Command);
        await command.ExecuteAsync(null); Layout(window);
    }
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Capture(Window window, string name, bool bottom = false)
    {
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS"); if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory); Layout(window);
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(x => x.Content is ContentControl);
        if (bottom) scroll?.ScrollToEnd(); else scroll?.ScrollToHome(); Layout(window);
        using var bitmap = window.CaptureRenderedFrame(); Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
