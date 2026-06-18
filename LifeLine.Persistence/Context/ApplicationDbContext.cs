using LifeLine.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Persistence.Context;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<MedicalDocument> MedicalDocument { get; set; }
    public DbSet<MedicalUpdate> MedicalUpdate { get; set; }
    public DbSet<Donation> Donations { get; set; }
    public DbSet<SupportMessage> SupportMessages { get; set; }
    public DbSet<Payout> Payouts { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Campaign ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Slug).IsUnique();
            entity.Property(c => c.Title).IsRequired().HasMaxLength(150);
            entity.Property(c => c.PatientName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.MedicalCondition).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Story).IsRequired().HasMaxLength(5000);
            entity.Property(c => c.Slug).IsRequired().HasMaxLength(200);
            entity.Property(c => c.GoalAmount).HasColumnType("decimal(18,2)");
            entity.Property(c => c.AmountRaised).HasColumnType("decimal(18,2)");
            entity.Property(c => c.Status).HasConversion<string>();
            entity.Property(c => c.BankName).HasMaxLength(100);
            entity.Property(c => c.AccountNumber).HasMaxLength(10);
            entity.Property(c => c.AccountName).HasMaxLength(100);
            entity.Property(c => c.CoverImageUrl).HasMaxLength(500);
            entity.Property(c => c.CoverImagePublicId).HasMaxLength(300);
            entity.HasOne(c => c.Creator)
                  .WithMany(u => u.Campaigns)
                  .HasForeignKey(c => c.CreatorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── MedicalDocument ──────────────────────────────────────────────────
        modelBuilder.Entity<MedicalDocument>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.FileUrl).IsRequired().HasMaxLength(500);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(200);
            entity.Property(d => d.FileType).HasMaxLength(50);
            entity.HasOne(d => d.Campaign)
                  .WithMany(c => c.Documents)
                  .HasForeignKey(d => d.CampaignId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── MedicalUpdate ────────────────────────────────────────────────────
        modelBuilder.Entity<MedicalUpdate>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Title).IsRequired().HasMaxLength(150);
            entity.Property(u => u.Content).IsRequired().HasMaxLength(3000);
            entity.Property(u => u.ImageUrl).HasMaxLength(500);
            entity.HasOne(u => u.Campaign)
                  .WithMany(c => c.Updates)
                  .HasForeignKey(u => u.CampaignId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Donation ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Donation>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Amount).HasColumnType("decimal(18,2)");
            entity.Property(d => d.PaymentReference).IsRequired().HasMaxLength(100);
            entity.HasIndex(d => d.PaymentReference).IsUnique();
            entity.Property(d => d.DonorName).HasMaxLength(100);
            entity.Property(d => d.DonorEmail).HasMaxLength(150);
            entity.Property(d => d.Message).HasMaxLength(300);
            entity.HasOne(d => d.Campaign)
                  .WithMany(c => c.Donations)
                  .HasForeignKey(d => d.CampaignId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(d => d.Donor)
                  .WithMany(u => u.Donations)
                  .HasForeignKey(d => d.DonorId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── SupportMessage ───────────────────────────────────────────────────
        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Message).IsRequired().HasMaxLength(500);
            entity.HasOne(s => s.Campaign)
                  .WithMany(c => c.SupportMessages)
                  .HasForeignKey(s => s.CampaignId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Payout ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Payout>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            entity.Property(p => p.BankName).IsRequired().HasMaxLength(100);
            entity.Property(p => p.AccountNumber).IsRequired().HasMaxLength(10);
            entity.Property(p => p.AccountName).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Status).HasMaxLength(20);
            entity.Property(p => p.Notes).HasMaxLength(500);
            entity.Property(p => p.RejectionReason).HasMaxLength(500);
            entity.Property(p => p.RequestedById).IsRequired().HasMaxLength(450);
            entity.Property(p => p.ApprovedByAdminId).HasMaxLength(450);
            entity.HasOne(p => p.Campaign)
                  .WithMany()
                  .HasForeignKey(p => p.CampaignId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── AuditLog ─────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.PerformedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(a => a.TargetEntityId).HasMaxLength(450);
            entity.Property(a => a.TargetEntityType).HasMaxLength(100);
            entity.Property(a => a.Details).HasMaxLength(1000);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}