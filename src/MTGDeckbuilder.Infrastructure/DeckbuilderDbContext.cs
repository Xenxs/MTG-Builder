using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class DeckbuilderDbContext(DbContextOptions<DeckbuilderDbContext> options) : DbContext(options)
{
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Printing> Printings => Set<Printing>();
    public DbSet<CollectionEntry> CollectionEntries => Set<CollectionEntry>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<DeckCard> DeckCards => Set<DeckCard>();
    public DbSet<UserSettings> Settings => Set<UserSettings>();
    public DbSet<CatalogImportRun> CatalogImports => Set<CatalogImportRun>();
    public DbSet<CatalogStagingEntry> CatalogStaging => Set<CatalogStagingEntry>();
    public DbSet<CollectionImportBatch> CollectionImports => Set<CollectionImportBatch>();
    public DbSet<CollectionImportRow> CollectionImportRows => Set<CollectionImportRow>();
    public DbSet<DeckImportBatch> DeckImports => Set<DeckImportBatch>();
    public DbSet<DeckImportRow> DeckImportRows => Set<DeckImportRow>();
    public DbSet<CardFormatLegality> CardLegalities => Set<CardFormatLegality>();
    public DbSet<DeckAdvicePlan> DeckAdvicePlans => Set<DeckAdvicePlan>();
    public DbSet<DeckGenerationPlan> DeckGenerationPlans => Set<DeckGenerationPlan>();
    public DbSet<PaperPrinting> PaperPrintings => Set<PaperPrinting>();
    public DbSet<PaperHolding> PaperHoldings => Set<PaperHolding>();
    public DbSet<PaperImportPlan> PaperImports => Set<PaperImportPlan>();
    public DbSet<PaperDeck> PaperDecks => Set<PaperDeck>();
    public DbSet<PaperDeckCard> PaperDeckCards => Set<PaperDeckCard>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PaperPrinting>(e => { e.HasKey(x => x.Id); e.Property(x => x.MetadataJson).IsRequired(); });
        model.Entity<PaperHolding>(e =>
        {
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.Key, x.IsDemo }).IsUnique(); e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<PaperPrinting>().WithMany().HasForeignKey(x => x.PrintingId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("PaperHoldings", t => t.HasCheckConstraint("CK_PaperHolding_Quantity", "Quantity >= 0 AND Quantity <= 9999"));
        });
        model.Entity<PaperImportPlan>(e => { e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken(); });
        model.Entity<PaperDeck>(e => { e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken(); });
        model.Entity<PaperDeckCard>(e =>
        {
            e.HasKey(x => new { x.DeckId, x.PrintingId, x.Zone });
            e.HasOne<PaperDeck>().WithMany().HasForeignKey(x => x.DeckId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PaperPrinting>().WithMany().HasForeignKey(x => x.PrintingId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("PaperDeckCards", t => { t.HasCheckConstraint("CK_PaperDeckCard_Quantity", "Quantity >= 1 AND Quantity <= 9999"); t.HasCheckConstraint("CK_PaperDeckCard_Zone", "Zone IN (0,1,2)"); });
        });
        model.Entity<DeckGenerationPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.ReviewedJson).IsRequired();
        });
        model.Entity<DeckAdvicePlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.ChoicesJson).IsRequired();
        });
        model.Entity<Card>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name);
            e.Property(x => x.SearchName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.SearchName);
            e.Property(x => x.ColorIdentity).HasMaxLength(5).IsRequired();
            e.HasIndex(x => x.OracleId).IsUnique();
        });
        model.Entity<Printing>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SetCode).HasMaxLength(12).IsRequired();
            e.Property(x => x.CollectorNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.Language).HasMaxLength(12).IsRequired();
            e.Property(x => x.SetName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Rarity).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.SetCode, x.CollectorNumber, x.Language }).IsUnique().HasFilter("ScryfallId IS NULL");
            e.HasIndex(x => new { x.SetCode, x.CollectorNumber, x.Language, x.IsInCatalog });
            e.HasIndex(x => x.ArenaId);
            e.HasIndex(x => x.ScryfallId).IsUnique();
            e.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CollectionEntry>(e =>
        {
            e.HasKey(x => x.PrintingId);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Printing>().WithMany().HasForeignKey(x => x.PrintingId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("CollectionEntries", t => t.HasCheckConstraint("CK_Collection_Quantity", "Quantity >= 0 AND Quantity <= 9999"));
        });
        model.Entity<Deck>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Format).HasMaxLength(40).IsRequired();
        });
        model.Entity<DeckCard>(e =>
        {
            e.HasKey(x => new { x.DeckId, x.PrintingId, x.Zone });
            e.HasOne<Deck>().WithMany().HasForeignKey(x => x.DeckId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Printing>().WithMany().HasForeignKey(x => x.PrintingId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("DeckCards", t =>
            {
                t.HasCheckConstraint("CK_DeckCard_Quantity", "Quantity >= 1 AND Quantity <= 9999");
                t.HasCheckConstraint("CK_DeckCard_Zone", "Zone IN (0, 1, 2)");
            });
        });
        model.Entity<UserSettings>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.PlayerName).HasMaxLength(80).IsRequired();
            e.Property(x => x.ExamplesEnabled).HasDefaultValue(true);
            e.ToTable("Settings", t => t.HasCheckConstraint("CK_Settings_Singleton", "Id = 1"));
        });
        model.Entity<CatalogImportRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(200).IsRequired();
            e.Property(x => x.SourceVersion).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.StartedUtc);
            e.ToTable("CatalogImports", t => t.HasCheckConstraint("CK_Import_Checkpoint",
                "ProcessedRecords >= 0 AND StagedRecords >= 0 AND StagedRecords <= ProcessedRecords"));
        });
        model.Entity<CatalogStagingEntry>(e =>
        {
            e.HasKey(x => new { x.RunId, x.ScryfallId });
            e.Property(x => x.Data).IsRequired();
            e.HasOne<CatalogImportRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CollectionImportBatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.CreatedUtc);
            e.ToTable("CollectionImports", t => t.HasCheckConstraint("CK_CollectionImport_Counts", "ChangedCount >= 0 AND SkippedCount >= 0"));
        });
        model.Entity<CollectionImportRow>(e =>
        {
            e.HasKey(x => new { x.BatchId, x.Number });
            e.HasIndex(x => new { x.BatchId, x.PrintingId });
            e.HasOne<CollectionImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Printing>().WithMany().HasForeignKey(x => x.PrintingId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("CollectionImportRows", t => t.HasCheckConstraint("CK_CollectionImportRow_Quantity", "Quantity >= 0 AND Quantity <= 9999 AND Number >= 1"));
        });
        model.Entity<DeckImportBatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.CreatedUtc);
            // DeckId is an audit reference; deleting a created deck must preserve its import receipt.
        });
        model.Entity<DeckImportRow>(e =>
        {
            e.HasKey(x => new { x.BatchId, x.Number });
            e.HasOne<DeckImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Printing>().WithMany().HasForeignKey(x => x.PrintingId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("DeckImportRows", t =>
            {
                t.HasCheckConstraint("CK_DeckImportRow_Quantity", "Quantity >= 1 AND Quantity <= 9999 AND Number >= 1");
                t.HasCheckConstraint("CK_DeckImportRow_Zone", "Zone IN (0, 1, 2)");
            });
        });
        model.Entity<CardFormatLegality>(e =>
        {
            e.HasKey(x => x.CardId);
            e.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CatalogImportRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("CardLegalities", t => t.HasCheckConstraint("CK_CardLegality_Standard", "Standard IN (0, 1, 2, 3, 4)"));
        });
    }
}

public sealed class CatalogStagingEntry
{
    public Guid RunId { get; set; }
    public string ScryfallId { get; set; } = "";
    public string Data { get; set; } = "";
}
