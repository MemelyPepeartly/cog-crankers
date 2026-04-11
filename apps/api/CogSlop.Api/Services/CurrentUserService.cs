using System.Security.Claims;
using CogSlop.Api.Data;
using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CogSlop.Api.Services;

public class CurrentUserService(
    CogSlopDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor) : ICurrentUserService
{
    public async Task<UserAccount> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var identity = currentUserAccessor.GetRequiredIdentity(principal);

        var user = await dbContext.UserAccounts
            .FirstOrDefaultAsync(
                x => x.GoogleSubject == identity.GoogleSubject || x.Email == identity.Email,
                cancellationToken);

        var now = DateTime.UtcNow;
        var saveRequired = false;

        if (user is null)
        {
            user = new UserAccount
            {
                GoogleSubject = identity.GoogleSubject,
                Email = identity.Email,
                DisplayName = string.Empty,
                AvatarUrl = identity.AvatarUrl,
                CreatedAtUtc = now,
                LastLoginAtUtc = now,
            };

            dbContext.UserAccounts.Add(user);
            saveRequired = true;
        }
        else
        {
            if (!string.Equals(user.GoogleSubject, identity.GoogleSubject, StringComparison.Ordinal))
            {
                user.GoogleSubject = identity.GoogleSubject;
                saveRequired = true;
            }

            if (!string.Equals(user.Email, identity.Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = identity.Email;
                saveRequired = true;
            }

            if (!string.Equals(user.AvatarUrl, identity.AvatarUrl, StringComparison.Ordinal))
            {
                user.AvatarUrl = identity.AvatarUrl;
                saveRequired = true;
            }

            user.LastLoginAtUtc = now;
            saveRequired = true;
        }

        if (saveRequired)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureDefaultRoleAsync(user.UserAccountId, cancellationToken);
        return user;
    }

    public async Task<UserAccount> GetExistingUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var identity = currentUserAccessor.GetRequiredIdentity(principal);
        UserAccount? user = null;

        var userAccountIdClaim = principal.FindFirst(CogClaimTypes.UserAccountId)?.Value;
        if (int.TryParse(userAccountIdClaim, out var userAccountId))
        {
            user = await dbContext.UserAccounts
                .FirstOrDefaultAsync(x => x.UserAccountId == userAccountId, cancellationToken);
        }

        user ??= await dbContext.UserAccounts
            .FirstOrDefaultAsync(
                x => x.GoogleSubject == identity.GoogleSubject || x.Email == identity.Email,
                cancellationToken);

        if (user is null)
        {
            return await EnsureUserAsync(principal, cancellationToken);
        }

        var saveRequired = false;
        if (!string.Equals(user.GoogleSubject, identity.GoogleSubject, StringComparison.Ordinal))
        {
            user.GoogleSubject = identity.GoogleSubject;
            saveRequired = true;
        }

        if (!string.Equals(user.Email, identity.Email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = identity.Email;
            saveRequired = true;
        }

        if (!string.Equals(user.AvatarUrl, identity.AvatarUrl, StringComparison.Ordinal))
        {
            user.AvatarUrl = identity.AvatarUrl;
            saveRequired = true;
        }

        if (saveRequired)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<UserProfileDto> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await GetExistingUserAsync(principal, cancellationToken);
        return await BuildProfileAsync(user, cancellationToken);
    }

    public async Task<UserProfileDto> UpdateDisplayNameAsync(
        ClaimsPrincipal principal,
        string displayName,
        CancellationToken cancellationToken)
    {
        var normalizedDisplayName = DisplayNameRules.Normalize(displayName);
        if (normalizedDisplayName is null)
        {
            throw new InvalidOperationException("Display name is required before entering the cog economy.");
        }

        var user = await GetExistingUserAsync(principal, cancellationToken);
        if (!string.Equals(user.DisplayName, normalizedDisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = normalizedDisplayName;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await BuildProfileAsync(user, cancellationToken);
    }

    public async Task<UserProfileDto> BuildProfileAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var roles = await GetRoleNamesAsync(user.UserAccountId, cancellationToken);
        var balance = await GetCogBalanceAsync(user.UserAccountId, cancellationToken);

        var inventory = await dbContext.UserInventories
            .AsNoTracking()
            .Where(x => x.UserAccountId == user.UserAccountId)
            .Include(x => x.GearItem)
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.GearItem.Name)
            .Select(x => new InventoryItemDto(
                x.GearItemId,
                x.GearItem.Name,
                x.GearItem.GearType,
                x.Quantity,
                x.GearItem.FlavorText,
                x.GearItem.CostInCogs))
            .ToListAsync(cancellationToken);

        var isAdmin = roles.Contains(RoleNames.CogAdmin, StringComparer.OrdinalIgnoreCase);

        return new UserProfileDto(
            user.UserAccountId,
            user.DisplayName,
            user.Email,
            user.AvatarUrl,
            balance,
            roles,
            inventory,
            isAdmin);
    }

    public async Task<int> GetCogBalanceAsync(int userAccountId, CancellationToken cancellationToken)
    {
        var balance = await dbContext.CogTransactions
            .AsNoTracking()
            .Where(x => x.UserAccountId == userAccountId)
            .SumAsync(x => (int?)x.Amount, cancellationToken);

        return balance ?? 0;
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(int userAccountId, CancellationToken cancellationToken)
    {
        return await dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.UserAccountId == userAccountId)
            .Select(x => x.Role.Name)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureDefaultRoleAsync(int userAccountId, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Name == RoleNames.CogUser, cancellationToken);

        if (role is null)
        {
            role = new Role { Name = RoleNames.CogUser };
            dbContext.Roles.Add(role);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var hasRole = await dbContext.UserRoles
            .AnyAsync(
                x => x.UserAccountId == userAccountId && x.RoleId == role.RoleId,
                cancellationToken);

        if (!hasRole)
        {
            dbContext.UserRoles.Add(new UserRole
            {
                UserAccountId = userAccountId,
                RoleId = role.RoleId,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
