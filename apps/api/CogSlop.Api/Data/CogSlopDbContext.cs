using CogSlop.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CogSlop.Api.Data;

public class CogSlopDbContext(DbContextOptions<CogSlopDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<GearItem> GearItems => Set<GearItem>();

    public DbSet<UserInventory> UserInventories => Set<UserInventory>();

    public DbSet<CogTransaction> CogTransactions => Set<CogTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.HasKey(x => x.UserAccountId);
            entity.HasIndex(x => x.GoogleSubject).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.GoogleSubject).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.AvatarUrl).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.LastLoginAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.RoleId);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(x => new { x.UserAccountId, x.RoleId });
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Role)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GearItem>(entity =>
        {
            entity.ToTable("GearItems");
            entity.HasKey(x => x.GearItemId);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(600);
            entity.Property(x => x.GearType).HasMaxLength(60).IsRequired();
            entity.Property(x => x.CostInCogs).IsRequired();
            entity.Property(x => x.FlavorText).HasMaxLength(300);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<UserInventory>(entity =>
        {
            entity.ToTable("UserInventories");
            entity.HasKey(x => x.UserInventoryId);
            entity.HasIndex(x => new { x.UserAccountId, x.GearItemId }).IsUnique();
            entity.Property(x => x.Quantity).IsRequired();
            entity.Property(x => x.LastGrantedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.UserInventories)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.GearItem)
                .WithMany(x => x.UserInventories)
                .HasForeignKey(x => x.GearItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CogTransaction>(entity =>
        {
            entity.ToTable("CogTransactions");
            entity.HasKey(x => x.CogTransactionId);
            entity.Property(x => x.TransactionType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.CogTransactions)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.GearItem)
                .WithMany(x => x.CogTransactions)
                .HasForeignKey(x => x.GearItemId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.GrantedByUserAccount)
                .WithMany(x => x.GrantedCogTransactions)
                .HasForeignKey(x => x.GrantedByUserAccountId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserInventory>()
            .ToTable(t => t.HasCheckConstraint("CK_UserInventories_Quantity", "[Quantity] > 0"));

        modelBuilder.Entity<GearItem>()
            .ToTable(t => t.HasCheckConstraint("CK_GearItems_Cost", "[CostInCogs] >= 0"));
    }
}
