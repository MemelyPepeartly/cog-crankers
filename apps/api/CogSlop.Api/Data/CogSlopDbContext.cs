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

    public DbSet<CogSession> CogSessions => Set<CogSession>();

    public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();

    public DbSet<CogRuntimeSetting> CogRuntimeSettings => Set<CogRuntimeSetting>();

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
            entity.Property(x => x.IsPlayerCrafted).HasDefaultValue(false);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.CraftedByUserAccount)
                .WithMany(x => x.CraftedGearItems)
                .HasForeignKey(x => x.CraftedByUserAccountId)
                .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<CogSession>(entity =>
        {
            entity.ToTable("CogSessions");
            entity.HasKey(x => x.CogSessionId);
            entity.Property(x => x.CogInAtUtc).IsRequired();
            entity.Property(x => x.WarningIntervalMinutesAtCogIn).IsRequired();
            entity.Property(x => x.SuccessfulCogChecks).HasDefaultValue(0).IsRequired();
            entity.Property(x => x.AutoCogOutNoPayout).HasDefaultValue(false).IsRequired();
            entity.Property(x => x.CogInNote).HasMaxLength(300);
            entity.Property(x => x.CogOutNote).HasMaxLength(300);
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.CogSessions)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MarketplaceListing>(entity =>
        {
            entity.ToTable("MarketplaceListings");
            entity.HasKey(x => x.MarketplaceListingId);
            entity.Property(x => x.Quantity).IsRequired();
            entity.Property(x => x.PriceInCogs).IsRequired();
            entity.Property(x => x.ListingStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.SellerNote).HasMaxLength(300);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(x => x.SellerUserAccount)
                .WithMany(x => x.MarketplaceListingsAsSeller)
                .HasForeignKey(x => x.SellerUserAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.BuyerUserAccount)
                .WithMany(x => x.MarketplaceListingsAsBuyer)
                .HasForeignKey(x => x.BuyerUserAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.GearItem)
                .WithMany(x => x.MarketplaceListings)
                .HasForeignKey(x => x.GearItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CogRuntimeSetting>(entity =>
        {
            entity.ToTable("CogRuntimeSettings");
            entity.HasKey(x => x.CogRuntimeSettingId);
            entity.Property(x => x.WarningIntervalMinutes).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(x => x.UpdatedByUserAccount)
                .WithMany(x => x.UpdatedCogRuntimeSettings)
                .HasForeignKey(x => x.UpdatedByUserAccountId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserInventory>()
            .ToTable(t => t.HasCheckConstraint("CK_UserInventories_Quantity", "[Quantity] > 0"));

        modelBuilder.Entity<GearItem>()
            .ToTable(t => t.HasCheckConstraint("CK_GearItems_Cost", "[CostInCogs] >= 0"));

        modelBuilder.Entity<CogSession>()
            .ToTable(t => t.HasCheckConstraint("CK_CogSessions_Order", "[CogOutAtUtc] IS NULL OR [CogOutAtUtc] >= [CogInAtUtc]"));

        modelBuilder.Entity<CogSession>()
            .ToTable(t => t.HasCheckConstraint("CK_CogSessions_WarningInterval", "[WarningIntervalMinutesAtCogIn] >= 5"));

        modelBuilder.Entity<CogSession>()
            .ToTable(t => t.HasCheckConstraint("CK_CogSessions_SuccessfulCogChecks", "[SuccessfulCogChecks] >= 0"));

        modelBuilder.Entity<CogSession>()
            .ToTable(t => t.HasCheckConstraint("CK_CogSessions_PayoutCogs", "[PayoutCogs] IS NULL OR [PayoutCogs] >= 0"));

        modelBuilder.Entity<MarketplaceListing>()
            .ToTable(t => t.HasCheckConstraint("CK_MarketplaceListings_Quantity", "[Quantity] > 0"));

        modelBuilder.Entity<MarketplaceListing>()
            .ToTable(t => t.HasCheckConstraint("CK_MarketplaceListings_PriceInCogs", "[PriceInCogs] > 0"));

        modelBuilder.Entity<MarketplaceListing>()
            .ToTable(t => t.HasCheckConstraint("CK_MarketplaceListings_Status", "[ListingStatus] IN (N'Open', N'Sold', N'Cancelled')"));

        modelBuilder.Entity<MarketplaceListing>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_MarketplaceListings_SoldAt",
                "([ListingStatus] <> N'Sold' AND [SoldAtUtc] IS NULL) OR ([ListingStatus] = N'Sold' AND [SoldAtUtc] IS NOT NULL)"));

        modelBuilder.Entity<CogRuntimeSetting>()
            .ToTable(t => t.HasCheckConstraint("CK_CogRuntimeSettings_WarningInterval", "[WarningIntervalMinutes] >= 5"));
    }
}
