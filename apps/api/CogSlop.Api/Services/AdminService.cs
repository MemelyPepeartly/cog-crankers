using System.Security.Claims;
using CogSlop.Api.Data;
using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Entities;
using CogSlop.Api.Models.Requests;
using Microsoft.EntityFrameworkCore;

namespace CogSlop.Api.Services;

public class AdminService(
    CogSlopDbContext dbContext,
    ICurrentUserService currentUserService) : IAdminService
{
    public async Task<IReadOnlyList<AdminUserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var roleLookup = await dbContext.UserRoles
            .AsNoTracking()
            .GroupBy(x => x.UserAccountId)
            .Select(g => new
            {
                UserAccountId = g.Key,
                Roles = g.Select(x => x.Role.Name).OrderBy(x => x).ToList(),
            })
            .ToDictionaryAsync(x => x.UserAccountId, x => (IReadOnlyList<string>)x.Roles, cancellationToken);

        var balanceLookup = await dbContext.CogTransactions
            .AsNoTracking()
            .GroupBy(x => x.UserAccountId)
            .Select(g => new
            {
                UserAccountId = g.Key,
                Balance = g.Sum(x => x.Amount),
            })
            .ToDictionaryAsync(x => x.UserAccountId, x => x.Balance, cancellationToken);

        return users
            .Select(x => new AdminUserSummaryDto(
                x.UserAccountId,
                x.DisplayName,
                x.Email,
                balanceLookup.GetValueOrDefault(x.UserAccountId),
                roleLookup.GetValueOrDefault(x.UserAccountId, Array.Empty<string>())))
            .ToList();
    }

    public async Task<IReadOnlyList<StoreItemDto>> GetGearItemsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = dbContext.GearItems
            .AsNoTracking()
            .Where(x => !x.IsPlayerCrafted);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new StoreItemDto(
                x.GearItemId,
                x.Name,
                x.Description,
                x.GearType,
                x.CostInCogs,
                x.StockQuantity,
                x.IsActive,
                x.FlavorText))
            .ToListAsync(cancellationToken);
    }

    public async Task<CogRuntimeSettingsDto> GetCogRuntimeSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateRuntimeSettingsAsync(cancellationToken);
        return await BuildRuntimeSettingsDtoAsync(settings, cancellationToken);
    }

    public async Task<AdminUserSummaryDto> GrantCogsAsync(
        ClaimsPrincipal adminPrincipal,
        GrantCogsRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await currentUserService.EnsureUserAsync(adminPrincipal, cancellationToken);
        var targetUser = await dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.UserAccountId == request.UserAccountId, cancellationToken);

        if (targetUser is null)
        {
            throw new KeyNotFoundException("Target user not found.");
        }

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = targetUser.UserAccountId,
            Amount = request.Amount,
            TransactionType = CogTransactionTypes.Grant,
            Description = request.Note,
            GrantedByUserAccountId = admin.UserAccountId,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await currentUserService.GetRoleNamesAsync(targetUser.UserAccountId, cancellationToken);
        var newBalance = await currentUserService.GetCogBalanceAsync(targetUser.UserAccountId, cancellationToken);

        return new AdminUserSummaryDto(
            targetUser.UserAccountId,
            targetUser.DisplayName,
            targetUser.Email,
            newBalance,
            roles);
    }

    public async Task<InventoryItemDto> GrantGearAsync(
        ClaimsPrincipal adminPrincipal,
        GrantGearRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await currentUserService.EnsureUserAsync(adminPrincipal, cancellationToken);

        var targetUser = await dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.UserAccountId == request.UserAccountId, cancellationToken);
        if (targetUser is null)
        {
            throw new KeyNotFoundException("Target user not found.");
        }

        var gear = await dbContext.GearItems
            .FirstOrDefaultAsync(x => x.GearItemId == request.GearItemId, cancellationToken);
        if (gear is null)
        {
            throw new KeyNotFoundException("Gear item not found.");
        }

        if (gear.StockQuantity.HasValue && gear.StockQuantity.Value < request.Quantity)
        {
            throw new InvalidOperationException("Not enough stock available for this grant.");
        }

        if (gear.StockQuantity.HasValue)
        {
            gear.StockQuantity -= request.Quantity;
            gear.UpdatedAtUtc = DateTime.UtcNow;
        }

        var inventory = await dbContext.UserInventories
            .FirstOrDefaultAsync(
                x => x.UserAccountId == request.UserAccountId && x.GearItemId == request.GearItemId,
                cancellationToken);

        if (inventory is null)
        {
            inventory = new UserInventory
            {
                UserAccountId = request.UserAccountId,
                GearItemId = request.GearItemId,
                Quantity = request.Quantity,
                LastGrantedAtUtc = DateTime.UtcNow,
            };
            dbContext.UserInventories.Add(inventory);
        }
        else
        {
            inventory.Quantity += request.Quantity;
            inventory.LastGrantedAtUtc = DateTime.UtcNow;
        }

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = request.UserAccountId,
            Amount = 0,
            TransactionType = CogTransactionTypes.GearGrant,
            Description = request.Note,
            GearItemId = request.GearItemId,
            GrantedByUserAccountId = admin.UserAccountId,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InventoryItemDto(
            gear.GearItemId,
            gear.Name,
            gear.GearType,
            inventory.Quantity,
            gear.FlavorText,
            gear.CostInCogs);
    }

    public async Task<StoreItemDto> CreateGearItemAsync(UpsertGearItemRequest request, CancellationToken cancellationToken)
    {
        var gear = new GearItem
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            GearType = request.GearType.Trim(),
            CostInCogs = request.CostInCogs,
            StockQuantity = request.StockQuantity,
            IsActive = request.IsActive,
            FlavorText = request.FlavorText?.Trim(),
            CraftedByUserAccountId = null,
            IsPlayerCrafted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        dbContext.GearItems.Add(gear);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new StoreItemDto(
            gear.GearItemId,
            gear.Name,
            gear.Description,
            gear.GearType,
            gear.CostInCogs,
            gear.StockQuantity,
            gear.IsActive,
            gear.FlavorText);
    }

    public async Task<StoreItemDto> UpdateGearItemAsync(int gearItemId, UpsertGearItemRequest request, CancellationToken cancellationToken)
    {
        var gear = await dbContext.GearItems
            .FirstOrDefaultAsync(x => x.GearItemId == gearItemId && !x.IsPlayerCrafted, cancellationToken);

        if (gear is null)
        {
            throw new KeyNotFoundException("Gear item not found.");
        }

        gear.Name = request.Name.Trim();
        gear.Description = request.Description?.Trim();
        gear.GearType = request.GearType.Trim();
        gear.CostInCogs = request.CostInCogs;
        gear.StockQuantity = request.StockQuantity;
        gear.IsActive = request.IsActive;
        gear.FlavorText = request.FlavorText?.Trim();
        gear.IsPlayerCrafted = false;
        gear.CraftedByUserAccountId = null;
        gear.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new StoreItemDto(
            gear.GearItemId,
            gear.Name,
            gear.Description,
            gear.GearType,
            gear.CostInCogs,
            gear.StockQuantity,
            gear.IsActive,
            gear.FlavorText);
    }

    public async Task<CogRuntimeSettingsDto> UpdateWarningIntervalAsync(
        ClaimsPrincipal adminPrincipal,
        UpdateCogWarningIntervalRequest request,
        CancellationToken cancellationToken)
    {
        var admin = await currentUserService.EnsureUserAsync(adminPrincipal, cancellationToken);

        if (request.WarningIntervalMinutes < CogCheckRules.MinimumWarningIntervalMinutes
            || request.WarningIntervalMinutes > CogCheckRules.MaximumWarningIntervalMinutes)
        {
            throw new InvalidOperationException(
                $"Warning interval must be between {CogCheckRules.MinimumWarningIntervalMinutes} and {CogCheckRules.MaximumWarningIntervalMinutes} minutes.");
        }

        var settings = await GetOrCreateRuntimeSettingsAsync(cancellationToken);
        settings.WarningIntervalMinutes = request.WarningIntervalMinutes;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        settings.UpdatedByUserAccountId = admin.UserAccountId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildRuntimeSettingsDtoAsync(settings, cancellationToken);
    }

    private async Task<CogRuntimeSetting> GetOrCreateRuntimeSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.CogRuntimeSettings
            .OrderBy(x => x.CogRuntimeSettingId)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new CogRuntimeSetting
        {
            WarningIntervalMinutes = 60,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserAccountId = null,
        };

        dbContext.CogRuntimeSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<CogRuntimeSettingsDto> BuildRuntimeSettingsDtoAsync(
        CogRuntimeSetting settings,
        CancellationToken cancellationToken)
    {
        string? updatedByDisplayName = null;
        if (settings.UpdatedByUserAccountId.HasValue)
        {
            updatedByDisplayName = await dbContext.UserAccounts
                .Where(x => x.UserAccountId == settings.UpdatedByUserAccountId.Value)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new CogRuntimeSettingsDto(
            settings.WarningIntervalMinutes,
            settings.UpdatedAtUtc,
            settings.UpdatedByUserAccountId,
            updatedByDisplayName);
    }
}
