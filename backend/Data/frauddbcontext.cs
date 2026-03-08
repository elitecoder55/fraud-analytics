using FraudAnalytics.Models;
using Microsoft.EntityFrameworkCore;

namespace FraudAnalytics.Data;

public class FraudDbContext : DbContext
{
    public FraudDbContext(DbContextOptions<FraudDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>();
    public DbSet<MerchantProfile> MerchantProfiles => Set<MerchantProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.TransactionId);
            e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            e.Property(t => t.RiskScore).HasColumnType("decimal(5,4)");
            e.HasIndex(t => new { t.CardNumber, t.Timestamp })
             .HasDatabaseName("IX_Transactions_Card_Time");
            e.HasIndex(t => t.RiskScore)
             .HasFilter("[RiskScore] >= 0.7")
             .HasDatabaseName("IX_Transactions_HighRisk");
            e.HasIndex(t => t.MerchantId)
             .HasDatabaseName("IX_Transactions_Merchant");
        });

        modelBuilder.Entity<FraudAlert>(e =>
        {
            e.HasKey(a => a.AlertId);
            e.HasOne(a => a.Transaction)
             .WithMany()
             .HasForeignKey(a => a.TransactionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MerchantProfile>(e =>
        {
            e.HasKey(m => m.MerchantId);
            e.HasIndex(m => m.MerchantName).HasDatabaseName("IX_Merchant_Name");
        });

        base.OnModelCreating(modelBuilder);
    }
}