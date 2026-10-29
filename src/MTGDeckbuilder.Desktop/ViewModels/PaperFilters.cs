using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;
using Microsoft.Extensions.Logging;

namespace MTGDeckbuilder.Desktop.ViewModels;
public sealed partial class PaperViewModel
{
    public CardChoice[] ColorFilters=>CardThemes.Colors;
    public CardChoice[] ThemeFilters=>CardThemes.Choices;
    public CardChoice[] TypeFilters=>CardThemes.Types;
    public ObservableCollection<CardChoice> SetFilters {get;}=[new("any","Alle series")];
    [ObservableProperty] private CardChoice? colorFilter=CardThemes.Colors[0];
    [ObservableProperty] private CardChoice? themeFilter=CardThemes.Choices[0];
    [ObservableProperty] private CardChoice? typeFilter=CardThemes.Types[0];
    [ObservableProperty] private CardChoice? setFilter=new("any","Alle series");
    [ObservableProperty] private string filterSummary="";
    [ObservableProperty] private bool isFiltering;
    public Task? FilteringTask {get;private set;}
    private CancellationTokenSource? _filterCancel;
    private int _filterGeneration;
    private IReadOnlyList<PaperOwned> _allOwned=[];
    partial void OnColorFilterChanged(CardChoice? value)=>ApplyFilters();
    partial void OnThemeFilterChanged(CardChoice? value)=>ApplyFilters();
    partial void OnTypeFilterChanged(CardChoice? value)=>ApplyFilters();
    partial void OnSetFilterChanged(CardChoice? value)=>ApplyFilters();
    partial void OnSearchChanged(string value)=>ApplyFilters();
    private void LoadFilters(IReadOnlyList<PaperOwned> all)
    {
        _allOwned=all;var old=SetFilter?.Key;SetFilters.Clear();SetFilters.Add(new("any","Alle series"));
        foreach(var g in all.GroupBy(x=>x.Card.Set).OrderBy(g=>g.First().Card.SetName,StringComparer.Ordinal))
            SetFilters.Add(new(g.Key,$"{g.First().Card.SetName} ({g.Key.ToUpperInvariant()})"));
        SetFilter=SetFilters.FirstOrDefault(x=>x.Key==old)??SetFilters[0];ApplyFilters();
    }
    private void ApplyFilters()
    {
        _filterCancel?.Cancel();_filterCancel?.Dispose();_filterCancel=new();var ct=_filterCancel.Token;var generation=++_filterGeneration;
        var selected=SelectedOwned?.Card.Id;var source=_allOwned;var text=Search;var color=ColorFilter?.Key??"any";var theme=ThemeFilter?.Key??"any";var type=TypeFilter?.Key??"any";var set=SetFilter?.Key??"any";
        if(source.Count>500){IsFiltering=true;FilterSummary="Filters berekenen… vorige weergave blijft zichtbaar.";FilteringTask=FilterLargeAsync(source,text,color,theme,type,set,selected,generation,ct);return;}
        IsFiltering=false;CommitFilters(CardThemes.Filter(source,text,color,theme,type,set),selected);FilteringTask=Task.CompletedTask;
    }
    private async Task FilterLargeAsync(IReadOnlyList<PaperOwned> source,string text,string color,string theme,string type,string set,Guid? selected,int generation,CancellationToken ct)
    {
        try
        {
            var rows=await Task.Run(()=>{var result=new List<PaperOwned>();foreach(var chunk in source.Chunk(128)){ct.ThrowIfCancellationRequested();result.AddRange(CardThemes.Filter(chunk,text,color,theme,type,set));}ct.ThrowIfCancellationRequested();return result;},ct);
            if(generation==_filterGeneration&&!ct.IsCancellationRequested)CommitFilters(rows,selected);
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch(Exception ex){logger.LogError(ex,"Large collection filter failed");if(generation==_filterGeneration){operations.ErrorMessage="Het collectiefilter kon niet worden berekend. Probeer opnieuw; details staan in het logbestand.";operations.Status="Filter mislukt";}}
        finally{if(generation==_filterGeneration)IsFiltering=false;}
    }
    private void CommitFilters(IReadOnlyList<PaperOwned> rows,Guid? selected)
    {
        Collection.Clear();foreach(var row in rows)Collection.Add(row);SelectedOwned=Collection.FirstOrDefault(x=>x.Card.Id==selected)??Collection.FirstOrDefault();
        FilterSummary=$"{Collection.Count} van {_allOwned.Count} uitgaven · {rows.Sum(x=>x.Quantity)} getoonde exemplaren";
    }
    [RelayCommand] private void ClearFilters(){Search="";ColorFilter=ColorFilters[0];ThemeFilter=ThemeFilters[0];TypeFilter=TypeFilters[0];SetFilter=SetFilters[0];ApplyFilters();}
}
