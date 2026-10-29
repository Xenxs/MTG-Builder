using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteDatabaseResetService(IDbContextFactory<DeckbuilderDbContext> factory, DatabaseWriteLock writes,
    AppPaths paths, SqliteBackupService backups, ILogger<SqliteDatabaseResetService> logger) : IDatabaseResetService
{
    private DatabaseResetReview? _review;
    private DatabaseResetResult? _result;
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string[] Tables(DeckbuilderDbContext db) => db.Model.GetEntityTypes().Select(x=>x.GetTableName()!).Distinct().Order(StringComparer.Ordinal).ToArray();
    private static async Task<(string Hash, Dictionary<string,long> Counts)> ReadAsync(DeckbuilderDbContext db, CancellationToken ct)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var counts=new Dictionary<string,long>();
        foreach(var table in Tables(db))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(table));await using var cmd=db.Database.GetDbConnection().CreateCommand();cmd.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText="SELECT * FROM "+Quote(table)+" ORDER BY rowid";await using var reader=await cmd.ExecuteReaderAsync(ct);long count=0;
            while(await reader.ReadAsync(ct)){var values=new object[reader.FieldCount];reader.GetValues(values);hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(values));count++;}counts[table]=count;
        }
        return(Convert.ToHexString(hash.GetHashAndReset()),counts);
    }
    public Task<DatabaseResetReview> ReviewAsync(CancellationToken ct=default)=>Task.Run(async()=>
    {
        if((await backups.StatusAsync(ct)).Scheduled)throw new ArgumentException("Annuleer eerst gepland herstel voordat je de database leegt.");
        await using var db=await factory.CreateDbContextAsync(ct);await db.Database.OpenConnectionAsync(ct);await using var tx=await db.Database.BeginTransactionAsync(ct);
        var state=await ReadAsync(db,ct);return _review=new(Guid.NewGuid(),state.Hash,state.Counts);
    },ct);
    public async Task<DatabaseResetResult> ClearAsync(Guid reviewId,string confirmation,CancellationToken ct=default)
    {
        if(confirmation!="WISSEN")throw new ArgumentException("Typ exact WISSEN om deze actie expliciet te bevestigen.");
        if((await backups.StatusAsync(ct)).Scheduled)throw new ArgumentException("Er staat herstel gepland; legen is geblokkeerd.");
        return await Task.Run(async()=>
        {
            DatabaseResetResult? result=null;
            await writes.RunAsync(async()=>
            {
                if(_result?.ReviewId==reviewId){result=_result;return;}
                if(await backups.HasPendingRestoreUnderWriteLockAsync(ct))throw new ArgumentException("Er staat herstel gepland; legen is geblokkeerd.");
                var review=_review;if(review is null||review.Id!=reviewId)throw new ArgumentException("Open eerst een actueel wisvoorbeeld.");
                await using var db=await factory.CreateDbContextAsync(ct);await db.Database.OpenConnectionAsync(ct);
                await using(var check=await db.Database.BeginTransactionAsync(ct))
                {if((await ReadAsync(db,ct)).Hash!=review.Fingerprint)throw new ArgumentException("Database gewijzigd sinds het voorbeeld; controleer opnieuw.");await check.CommitAsync(ct);}
                Directory.CreateDirectory(paths.BackupsDirectory);var file=Path.Combine(paths.BackupsDirectory,$"pre-reset-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
                using(var destination=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=file,Pooling=false}.ToString()))
                {await destination.OpenAsync(ct);((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(destination);using var check=destination.CreateCommand();check.CommandText="PRAGMA quick_check";if(!Equals(await check.ExecuteScalarAsync(ct),"ok"))throw new IOException("Reservekopie kon niet worden gecontroleerd; er is niets gewist.");}
                if(new FileInfo(file).Length>SqliteBackupService.MaxBytes)throw new IOException("Reservekopie te groot voor gecontroleerd herstel; er is niets gewist.");
                await backups.ValidateResetBackupUnderWriteLockAsync(file,ct);
                await using var tx=await db.Database.BeginTransactionAsync(ct);
                if((await ReadAsync(db,ct)).Hash!=review.Fingerprint)throw new ArgumentException("Database tijdens reserveren gewijzigd; er is niets gewist. Controleer opnieuw.");
                var types=db.Model.GetEntityTypes().ToArray();var remaining=Tables(db).ToHashSet(StringComparer.Ordinal);
                while(remaining.Count>0)
                {
                    var next=remaining.Order(StringComparer.Ordinal).FirstOrDefault(parent=>!types.Where(t=>remaining.Contains(t.GetTableName()!)&&t.GetTableName()!=parent).Any(t=>t.GetForeignKeys().Any(f=>f.PrincipalEntityType.GetTableName()==parent)))
                        ??throw new InvalidOperationException("Cyclic schema cannot be cleared safely.");
                    await using var cmd=db.Database.GetDbConnection().CreateCommand();cmd.Transaction=tx.GetDbTransaction();cmd.CommandText="DELETE FROM "+Quote(next);await cmd.ExecuteNonQueryAsync(ct);remaining.Remove(next);
                }
                var defaults=new UserSettings();defaults.DisableExamples();db.Settings.Add(defaults);await db.SaveChangesAsync(ct);ct.ThrowIfCancellationRequested();await tx.CommitAsync(ct);
                result=_result=new(reviewId,file);_review=null;logger.LogInformation("Database explicitly cleared; safety backup {BackupPath}",file);
            },ct);return result!;
        },ct);
    }
}
