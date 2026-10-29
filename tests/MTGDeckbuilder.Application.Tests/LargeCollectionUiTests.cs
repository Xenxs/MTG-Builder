using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class LargeCollectionUiTests
{
    [AvaloniaFact]
    public async Task LargeFiltersRunAsynchronouslyAndOnlyLatestResultIsDisplayed()
    {
        var ct=TestContext.Current.CancellationToken;await using var t=new TestDatabase();await t.InitializeAsync();
        await using(var db=await t.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(ct))
        {
            var setting=await db.Settings.SingleOrDefaultAsync(ct);if(setting is null){setting=new UserSettings();db.Settings.Add(setting);}setting.DisableExamples();
            for(var i=0;i<600;i++)
            {
                var raw=PaperWorkspaceTests.Example()["Cards"]![0]!.DeepClone().AsObject();var id=Guid.NewGuid();raw["id"]=id;raw["oracle_id"]=Guid.NewGuid();raw["name"]=$"Corpus {i:D4}";raw["type_line"]="Creature — Human";raw["oracle_text"]=i%2==0?"Draw a card.":"";
                db.PaperPrintings.Add(new(id,raw.ToJsonString(),DateTime.UtcNow));db.PaperHoldings.Add(new(PaperIdentity.Key(id,"en","normal","unknown","UI fixture"),id,"en","normal","unknown","UI fixture",4,true));
            }
            await db.SaveChangesAsync(ct);
        }
        var shell=t.Provider.GetRequiredService<ShellViewModel>();var window=new MainWindow{DataContext=shell};window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null);var vm=Assert.IsType<PaperViewModel>(shell.CurrentPage);if(vm.FilteringTask is {} initial)await initial;
            Assert.Equal(600,vm.Collection.Count);vm.ThemeFilter=CardThemes.Choices.Single(x=>x.Key=="draw");Assert.True(vm.IsFiltering);
            var tick=false;Dispatcher.UIThread.Post(()=>tick=true);Dispatcher.UIThread.RunJobs();Assert.True(tick);
            vm.Search="Corpus 00";vm.Search="Corpus 05";await vm.FilteringTask!;Dispatcher.UIThread.RunJobs();
            Assert.False(vm.IsFiltering);Assert.Equal(50,vm.Collection.Count);Assert.All(vm.Collection,x=>Assert.StartsWith("Corpus 05",x.Card.Name));Assert.All(vm.Collection,x=>Assert.Equal("Draw a card.",x.Card.OracleText));Assert.False(shell.Operations.HasError);
        }
        finally{window.Close();}
    }
}
