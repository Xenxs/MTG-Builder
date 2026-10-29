using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MTGDeckbuilder.Infrastructure;

var path = args.Length > 2 && args[2] == "database-file" ? Path.GetFullPath(args[1])
    : (args.Length > 1 ? new AppPaths(args[1]) : new AppPaths()).DatabasePath;
using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
await db.OpenAsync();
if (args.Length > 3 && args[2] == "snapshot")
{
    var snapshot = Path.GetFullPath(args[3]);
    if (File.Exists(snapshot)) throw new ArgumentException("Preserve the existing snapshot; choose a fresh path.");
    using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot, Pooling = false }.ToString());
    await target.OpenAsync(); db.BackupDatabase(target);
}
var queries = new Dictionary<string, string>
{
    ["PaperPrintings"] = "SELECT * FROM PaperPrintings ORDER BY Id",
    ["PaperHoldings"] = "SELECT * FROM PaperHoldings ORDER BY Id",
    ["PaperImports"] = "SELECT * FROM PaperImports ORDER BY Id",
    ["PaperDecks"] = "SELECT * FROM PaperDecks ORDER BY Id",
    ["PaperDeckCards"] = "SELECT * FROM PaperDeckCards ORDER BY DeckId, PrintingId, Zone",
    ["Collection"] = "SELECT * FROM CollectionEntries ORDER BY PrintingId",
    ["Decks"] = "SELECT * FROM Decks ORDER BY Id",
    ["DeckCards"] = "SELECT * FROM DeckCards ORDER BY DeckId, PrintingId, Zone",
    ["Settings"] = "SELECT * FROM Settings ORDER BY Id",
    ["CollectionImports"] = "SELECT * FROM CollectionImports ORDER BY Id",
    ["CollectionImportRows"] = "SELECT * FROM CollectionImportRows ORDER BY BatchId, Number",
    ["DeckAdvicePlans"] = "SELECT * FROM DeckAdvicePlans ORDER BY Id",
    ["DeckGenerationPlans"] = "SELECT * FROM DeckGenerationPlans ORDER BY Id",
    ["DeckImports"] = "SELECT * FROM DeckImports ORDER BY Id",
    ["DeckImportRows"] = "SELECT * FROM DeckImportRows ORDER BY BatchId, Number",
    ["Cards"] = "SELECT * FROM Cards ORDER BY Id",
    ["CardLegalities"] = "SELECT * FROM CardLegalities ORDER BY CardId",
    ["Printings"] = "SELECT * FROM Printings ORDER BY Id",
    ["CatalogImports"] = "SELECT * FROM CatalogImports ORDER BY Id",
    ["CatalogStaging"] = "SELECT * FROM CatalogStaging ORDER BY RunId,ScryfallId"
};
var results = new Dictionary<string, object>();
foreach (var query in queries)
{
    using var existence = db.CreateCommand();
    existence.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
    existence.Parameters.AddWithValue("$name", query.Key == "Collection" ? "CollectionEntries" : query.Key);
    if (Convert.ToInt32(await existence.ExecuteScalarAsync()) == 0)
    { results[query.Key] = new { Exists = false, Rows = 0, Sha256 = "" }; continue; }
    using var command = db.CreateCommand(); command.CommandText = query.Value;
    using var reader = await command.ExecuteReaderAsync();
    var rows = new List<object[]>();
    while (await reader.ReadAsync())
    {
        var values = new object[reader.FieldCount]; reader.GetValues(values); rows.Add(values);
    }
    var columnNames=Enumerable.Range(0,reader.FieldCount).Select(reader.GetName).ToArray();
    var retainedOrdinals=Enumerable.Range(0,columnNames.Length).Where(i=>query.Key!="Settings"||columnNames[i]!="ExamplesEnabled").ToArray();
    var retainedRows=rows.Select(row=>retainedOrdinals.Select(i=>row[i]).ToArray()).ToArray();
    results[query.Key] = new { Exists = true, Rows = rows.Count, Columns=columnNames, Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows)))), RetainedSha256=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(retainedRows))) };
}
using var migrationsCommand = db.CreateCommand(); migrationsCommand.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
var migrations = new List<string>();
using (var reader = await migrationsCommand.ExecuteReaderAsync()) while (await reader.ReadAsync()) migrations.Add(reader.GetString(0));
var retained = new Dictionary<string, long>();
if (args.Length > 3 && args[2] == "compare")
{
    using var old = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(args[3]), Mode = SqliteOpenMode.ReadOnly }.ToString());
    await old.OpenAsync();
    foreach (var query in queries)
    {
        var table = query.Key == "Collection" ? "CollectionEntries" : query.Key;
        using var exists = old.CreateCommand(); exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name"; exists.Parameters.AddWithValue("$name", table);
        if (Convert.ToInt32(await exists.ExecuteScalarAsync()) == 0) continue;
        string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        using var schema = old.CreateCommand(); schema.CommandText = "PRAGMA table_info(" + Quote(table) + ")";
        var keys = new List<string>();
        using (var columns = await schema.ExecuteReaderAsync()) while (await columns.ReadAsync()) if (columns.GetInt32(5) > 0) keys.Add(columns.GetString(1));
        if (keys.Count == 0) throw new Exception("Audit requires primary keys: " + table);
        using var select = old.CreateCommand(); select.CommandText = query.Value; using var rows = await select.ExecuteReaderAsync(); long count = 0;
        while (await rows.ReadAsync())
        {
            using var current = db.CreateCommand(); current.CommandText = "SELECT * FROM " + Quote(table) + " WHERE " + string.Join(" AND ", keys.Select((k,i) => Quote(k) + "=$p" + i));
            for (var i=0;i<keys.Count;i++) current.Parameters.AddWithValue("$p"+i, rows.GetValue(rows.GetOrdinal(keys[i])));
            using var actual = await current.ExecuteReaderAsync(); if (!await actual.ReadAsync()) throw new Exception("Original row lost in " + table);
            var before = new object[rows.FieldCount]; rows.GetValues(before); var after = Enumerable.Range(0,rows.FieldCount).Select(i=>actual.GetValue(actual.GetOrdinal(rows.GetName(i)))).ToArray();
            if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after)) throw new Exception("Original row changed in " + table); count++;
        }
        retained[table] = count;
    }
}
var output = new { CheckedAtUtc = DateTimeOffset.UtcNow, Database = path, ReadOnly = true, UserData = results, Migrations = migrations, OriginalRowsRetained = retained };
var text = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(args[0], text);
Console.WriteLine(text);
