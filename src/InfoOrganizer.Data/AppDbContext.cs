using InfoOrganizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<StockMovement> Movements => Set<StockMovement>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<RawRecord> RawRecords => Set<RawRecord>();
    public DbSet<ReviewRow> ReviewRows => Set<ReviewRow>();
    public DbSet<SourceProfile> SourceProfiles => Set<SourceProfile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.Property(p => p.Name).IsRequired();
            e.HasIndex(p => p.Sku);
            e.HasIndex(p => p.Name);
        });

        b.Entity<StockMovement>(e =>
        {
            e.Property(m => m.ExtraAttributesJson).HasDefaultValue("{}");

            e.HasOne(m => m.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.ImportBatch)
                .WithMany(i => i.Movements)
                .HasForeignKey(m => m.ImportBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.RawRecord)
                .WithMany()
                .HasForeignKey(m => m.RawRecordId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(m => m.ReviewRow)
                .WithOne(r => r.Movement)
                .HasForeignKey<StockMovement>(m => m.ReviewRowId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(m => m.OccurredOn);
            e.HasIndex(m => m.LocationName);
        });

        b.Entity<RawRecord>(e =>
            e.HasOne(r => r.ImportBatch)
                .WithMany(i => i.RawRecords)
                .HasForeignKey(r => r.ImportBatchId)
                .OnDelete(DeleteBehavior.Cascade));

        b.Entity<ReviewRow>(e =>
        {
            e.Property(r => r.IssuesJson).HasDefaultValue("[]");
            e.Property(r => r.ExtraAttributesJson).HasDefaultValue("{}");

            e.HasOne(r => r.ImportBatch)
                .WithMany(i => i.ReviewRows)
                .HasForeignKey(r => r.ImportBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.RawRecord)
                .WithMany()
                .HasForeignKey(r => r.RawRecordId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.ImportBatchId);
        });

        b.Entity<SourceProfile>(e => e.HasIndex(s => s.Fingerprint).IsUnique());
    }
}
