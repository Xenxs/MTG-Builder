using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CatalogSearchTests
{
    [Fact]
    public async Task CombinesNameTypeSetRarityColorManaAndOwnershipFilters()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var path = await CatalogFixtures.WriteAsync(test,
        [CatalogFixtures.Card("Negate", collector: "1", colors: ["U"], mana: 2, type: "Instant", rarity: "uncommon"),
         CatalogFixtures.Card(collector: "2"),
         CatalogFixtures.Card("Gold Spell", set: "alt", collector: "3", colors: ["U", "R"], mana: 3, type: "Sorcery", rarity: "rare"),
         CatalogFixtures.Card("Colorless", set: "alt", collector: "4", colors: [], mana: 4, type: "Artifact")], "jsonl", ct);
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(path, ct: ct);
        var catalog = test.Provider.GetRequiredService<CatalogService>();
        Assert.Equal("Negate", Assert.Single((await catalog.SearchAsync(new(Search: " neGa "), ct)).Items).Name);
        Assert.Equal("Gold Spell", Assert.Single((await catalog.SearchAsync(new(SetCode: "alt", Rarity: "RARE", Color: "U", Type: "sorcery", MaxManaValue: 3), ct)).Items).Name);
        Assert.Equal("Colorless", Assert.Single((await catalog.SearchAsync(new(Color: "C"), ct)).Items).Name);
        Assert.Equal("Gold Spell", Assert.Single((await catalog.SearchAsync(new(Color: "M"), ct)).Items).Name);
        Assert.Equal("Llanowar Elves", Assert.Single((await catalog.SearchAsync(new(MaxManaValue: 1), ct)).Items).Name);
        Assert.Empty((await catalog.SearchAsync(new(Color: "W"), ct)).Items);
        Assert.Empty((await catalog.SearchAsync(new(Search: "' OR 1=1--"), ct)).Items);
        var gold = Assert.Single((await catalog.SearchAsync(new(Search: "Gold"), ct)).Items);
        await catalog.SetOwnedQuantityAsync(gold.PrintingId, 3, ct);
        Assert.Equal(gold.PrintingId, Assert.Single((await catalog.SearchAsync(new(OwnedOnly: true), ct)).Items).PrintingId);
        Assert.Equal(3, Assert.Single(await test.Service.GetCollectionAsync(ct)).Quantity);
        Assert.Equal(2, (await catalog.GetSetsAsync(ct)).Count);
    }

    [Fact]
    public async Task PaginatesAndNormalizesUnicodeNamesWithoutLoadingWholeCatalogIntoUi()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var path = await CatalogFixtures.WriteAsync(test, Enumerable.Range(0, 65)
            .Select(i => CatalogFixtures.Card($"Éther {i:000}", collector: i.ToString())), "json", ct);
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(path, ct: ct);
        var catalog = test.Provider.GetRequiredService<CatalogService>();
        var first = await catalog.SearchAsync(new(), ct);
        var second = await catalog.SearchAsync(new(Page: 2), ct);
        Assert.Equal(65, first.Total);
        Assert.Equal(50, first.Items.Count);
        Assert.Equal(15, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.PrintingId).Intersect(second.Items.Select(x => x.PrintingId)));
        Assert.Equal(2, (await catalog.SearchAsync(new(Page: 999), ct)).Page);
        Assert.Equal(10, (await catalog.SearchAsync(new(Search: "éther 05"), ct)).Total);
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SearchAsync(new(Page: 0), ct));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SearchAsync(new(PageSize: 1000), ct));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SearchAsync(new(Color: "Q"), ct));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.SearchAsync(new(MaxManaValue: double.NaN), ct));
    }
}
