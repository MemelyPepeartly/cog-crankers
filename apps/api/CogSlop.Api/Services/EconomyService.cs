using System.Security.Claims;
using CogSlop.Api.Data;
using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Entities;
using CogSlop.Api.Models.Requests;
using Microsoft.EntityFrameworkCore;

namespace CogSlop.Api.Services;

public class EconomyService(
    CogSlopDbContext dbContext,
    ICurrentUserService currentUserService) : IEconomyService
{
    public async Task<DashboardDto> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);
        var profile = await currentUserService.BuildProfileAsync(user, cancellationToken);
        var storeItems = await GetStoreItemsAsync(includeInactive: false, cancellationToken);

        return new DashboardDto(profile, storeItems);
    }

    public async Task<IReadOnlyList<StoreItemDto>> GetStoreItemsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = dbContext.GearItems
            .AsNoTracking()
            .Where(x => !x.IsPlayerCrafted);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.CostInCogs)
            .ThenBy(x => x.Name)
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

    public async Task<PurchaseReceiptDto> BuyGearAsync(
        ClaimsPrincipal principal,
        int gearItemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity < 1 || quantity > 20)
        {
            throw new InvalidOperationException("You can buy between 1 and 20 gears at a time.");
        }

        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var gear = await dbContext.GearItems
            .FirstOrDefaultAsync(x => x.GearItemId == gearItemId && x.IsActive && !x.IsPlayerCrafted, cancellationToken);

        if (gear is null)
        {
            throw new KeyNotFoundException("That gear is not in this workshop.");
        }

        if (gear.StockQuantity.HasValue && gear.StockQuantity.Value < quantity)
        {
            throw new InvalidOperationException("That gear shelf is running low. Not enough stock.");
        }

        var currentBalance = await currentUserService.GetCogBalanceAsync(user.UserAccountId, cancellationToken);
        var totalCost = checked(gear.CostInCogs * quantity);

        if (currentBalance < totalCost)
        {
            throw new InvalidOperationException("Not enough cogs in your pocket to spin this purchase.");
        }

        if (gear.StockQuantity.HasValue)
        {
            gear.StockQuantity -= quantity;
            gear.UpdatedAtUtc = DateTime.UtcNow;
        }

        var inventory = await dbContext.UserInventories
            .FirstOrDefaultAsync(
                x => x.UserAccountId == user.UserAccountId && x.GearItemId == gear.GearItemId,
                cancellationToken);

        if (inventory is null)
        {
            inventory = new UserInventory
            {
                UserAccountId = user.UserAccountId,
                GearItemId = gear.GearItemId,
                Quantity = quantity,
                LastGrantedAtUtc = DateTime.UtcNow,
            };

            dbContext.UserInventories.Add(inventory);
        }
        else
        {
            inventory.Quantity += quantity;
            inventory.LastGrantedAtUtc = DateTime.UtcNow;
        }

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = user.UserAccountId,
            Amount = -totalCost,
            TransactionType = CogTransactionTypes.Purchase,
            Description = $"Bought {quantity} x {gear.Name}",
            GearItemId = gear.GearItemId,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var newBalance = currentBalance - totalCost;

        return new PurchaseReceiptDto(
            $"{quantity} {gear.Name} added to your cog locker.",
            quantity,
            totalCost,
            newBalance,
            new InventoryItemDto(
                gear.GearItemId,
                gear.Name,
                gear.GearType,
                inventory.Quantity,
                gear.FlavorText,
                gear.CostInCogs));
    }

    public async Task<CraftGearReceiptDto> CraftGearAsync(
        ClaimsPrincipal principal,
        CraftGearRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);
        var name = request.Name.Trim();
        var gearType = request.GearType.Trim();
        var description = Normalize(request.Description);
        var flavorText = Normalize(request.FlavorText);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(gearType))
        {
            throw new InvalidOperationException("Crafting requires a valid gear name and type.");
        }

        var craftingCost = request.CraftingCostInCogs;
        var currentBalance = await currentUserService.GetCogBalanceAsync(user.UserAccountId, cancellationToken);
        if (currentBalance < craftingCost)
        {
            throw new InvalidOperationException("Not enough cogs in your pocket to craft this custom gear.");
        }

        var now = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var craftedGear = new GearItem
        {
            Name = name,
            Description = description,
            GearType = gearType,
            CostInCogs = craftingCost,
            StockQuantity = null,
            IsActive = false,
            FlavorText = flavorText,
            CraftedByUserAccountId = user.UserAccountId,
            IsPlayerCrafted = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var inventory = new UserInventory
        {
            UserAccountId = user.UserAccountId,
            GearItem = craftedGear,
            Quantity = 1,
            LastGrantedAtUtc = now,
        };

        dbContext.UserInventories.Add(inventory);
        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = user.UserAccountId,
            Amount = -craftingCost,
            TransactionType = CogTransactionTypes.Crafting,
            Description = $"Crafted custom gear: {craftedGear.Name}",
            CreatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CraftGearReceiptDto(
            $"Your custom cog-creation \"{craftedGear.Name}\" has been forged and added to your locker.",
            craftingCost,
            currentBalance - craftingCost,
            new InventoryItemDto(
                craftedGear.GearItemId,
                craftedGear.Name,
                craftedGear.GearType,
                inventory.Quantity,
                craftedGear.FlavorText,
                craftedGear.CostInCogs));
    }

    public async Task<IReadOnlyList<MarketplaceListingDto>> GetMarketplaceListingsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.MarketplaceListings
            .AsNoTracking()
            .Where(x => x.ListingStatus == MarketplaceListingStatuses.Open)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new MarketplaceListingDto(
                x.MarketplaceListingId,
                x.GearItemId,
                x.GearItem.Name,
                x.GearItem.GearType,
                x.GearItem.FlavorText,
                x.Quantity,
                x.PriceInCogs,
                x.SellerUserAccountId,
                x.SellerUserAccount.DisplayName,
                x.ListingStatus,
                x.SellerNote,
                x.CreatedAtUtc,
                x.SoldAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<MarketplaceListingDto> CreateMarketplaceListingAsync(
        ClaimsPrincipal principal,
        CreateMarketplaceListingRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);

        var inventory = await dbContext.UserInventories
            .Include(x => x.GearItem)
            .FirstOrDefaultAsync(
                x => x.UserAccountId == user.UserAccountId && x.GearItemId == request.GearItemId,
                cancellationToken);

        if (inventory is null)
        {
            throw new KeyNotFoundException("That gear is not in your locker.");
        }

        if (request.Quantity > inventory.Quantity)
        {
            throw new InvalidOperationException("You do not have enough quantity to post that listing.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        inventory.Quantity -= request.Quantity;
        if (inventory.Quantity == 0)
        {
            dbContext.UserInventories.Remove(inventory);
        }

        var listing = new MarketplaceListing
        {
            SellerUserAccountId = user.UserAccountId,
            GearItemId = inventory.GearItemId,
            Quantity = request.Quantity,
            PriceInCogs = request.PriceInCogs,
            ListingStatus = MarketplaceListingStatuses.Open,
            SellerNote = Normalize(request.SellerNote),
            CreatedAtUtc = now,
        };

        dbContext.MarketplaceListings.Add(listing);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MarketplaceListingDto(
            listing.MarketplaceListingId,
            listing.GearItemId,
            inventory.GearItem.Name,
            inventory.GearItem.GearType,
            inventory.GearItem.FlavorText,
            listing.Quantity,
            listing.PriceInCogs,
            listing.SellerUserAccountId,
            user.DisplayName,
            listing.ListingStatus,
            listing.SellerNote,
            listing.CreatedAtUtc,
            listing.SoldAtUtc);
    }

    public async Task<MarketplacePurchaseReceiptDto> BuyMarketplaceListingAsync(
        ClaimsPrincipal principal,
        int marketplaceListingId,
        CancellationToken cancellationToken)
    {
        var buyer = await currentUserService.EnsureUserAsync(principal, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var listing = await dbContext.MarketplaceListings
            .Include(x => x.GearItem)
            .Include(x => x.SellerUserAccount)
            .FirstOrDefaultAsync(x => x.MarketplaceListingId == marketplaceListingId, cancellationToken);

        if (listing is null)
        {
            throw new KeyNotFoundException("This listing no longer exists.");
        }

        if (!string.Equals(listing.ListingStatus, MarketplaceListingStatuses.Open, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("This listing is no longer available.");
        }

        if (listing.SellerUserAccountId == buyer.UserAccountId)
        {
            throw new InvalidOperationException("You cannot buy your own listing.");
        }

        var buyerBalance = await currentUserService.GetCogBalanceAsync(buyer.UserAccountId, cancellationToken);
        if (buyerBalance < listing.PriceInCogs)
        {
            throw new InvalidOperationException("Not enough cogs available for this marketplace purchase.");
        }

        var buyerInventory = await dbContext.UserInventories
            .FirstOrDefaultAsync(
                x => x.UserAccountId == buyer.UserAccountId && x.GearItemId == listing.GearItemId,
                cancellationToken);

        if (buyerInventory is null)
        {
            buyerInventory = new UserInventory
            {
                UserAccountId = buyer.UserAccountId,
                GearItemId = listing.GearItemId,
                Quantity = listing.Quantity,
                LastGrantedAtUtc = DateTime.UtcNow,
            };
            dbContext.UserInventories.Add(buyerInventory);
        }
        else
        {
            buyerInventory.Quantity += listing.Quantity;
            buyerInventory.LastGrantedAtUtc = DateTime.UtcNow;
        }

        listing.ListingStatus = MarketplaceListingStatuses.Sold;
        listing.BuyerUserAccountId = buyer.UserAccountId;
        listing.SoldAtUtc = DateTime.UtcNow;

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = buyer.UserAccountId,
            Amount = -listing.PriceInCogs,
            TransactionType = CogTransactionTypes.MarketplacePurchase,
            Description = $"Bought {listing.Quantity} x {listing.GearItem.Name} from {listing.SellerUserAccount.DisplayName}",
            GearItemId = listing.GearItemId,
            CreatedAtUtc = DateTime.UtcNow,
        });

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = listing.SellerUserAccountId,
            Amount = listing.PriceInCogs,
            TransactionType = CogTransactionTypes.MarketplaceSale,
            Description = $"Sold {listing.Quantity} x {listing.GearItem.Name} to {buyer.DisplayName}",
            GearItemId = listing.GearItemId,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MarketplacePurchaseReceiptDto(
            $"Marketplace deal closed: {listing.Quantity} x {listing.GearItem.Name} transferred to your locker.",
            listing.PriceInCogs,
            buyerBalance - listing.PriceInCogs,
            new InventoryItemDto(
                listing.GearItemId,
                listing.GearItem.Name,
                listing.GearItem.GearType,
                buyerInventory.Quantity,
                listing.GearItem.FlavorText,
                listing.GearItem.CostInCogs));
    }

    public async Task<CogSessionDto> CogInAsync(
        ClaimsPrincipal principal,
        CogInRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);
        var now = DateTime.UtcNow;

        var existingOpenSession = await GetOpenCogSessionAsync(user.UserAccountId, cancellationToken);
        if (existingOpenSession is not null)
        {
            var existingState = EvaluateCogCheck(existingOpenSession, now);
            if (existingState.DeadlineMissed)
            {
                await AutoCogOutNoPayoutAsync(
                    existingOpenSession,
                    now,
                    "Auto-cogged out: cog check timed out.",
                    cancellationToken);
            }
            else
            {
                throw new InvalidOperationException("You are already cogged in. Cog out before starting a new shift.");
            }
        }

        var warningIntervalMinutes = await GetWarningIntervalMinutesAsync(cancellationToken);

        var session = new CogSession
        {
            UserAccountId = user.UserAccountId,
            CogInAtUtc = now,
            WarningIntervalMinutesAtCogIn = warningIntervalMinutes,
            SuccessfulCogChecks = 0,
            AutoCogOutNoPayout = false,
            PayoutCogs = null,
            CogInNote = Normalize(request.Note),
        };

        dbContext.CogSessions.Add(session);
        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = user.UserAccountId,
            Amount = 0,
            TransactionType = CogTransactionTypes.CogIn,
            Description = "Cogged in for a new shift.",
            CreatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToCogSessionDto(session);
    }

    public async Task<CogSessionDto> CogOutAsync(
        ClaimsPrincipal principal,
        CogOutRequest request,
        CancellationToken cancellationToken)
    {
        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);
        var now = DateTime.UtcNow;

        var session = await GetOpenCogSessionAsync(user.UserAccountId, cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("No active shift found. Cog in first.");
        }

        var cogCheckState = EvaluateCogCheck(session, now);
        if (cogCheckState.DeadlineMissed)
        {
            await AutoCogOutNoPayoutAsync(
                session,
                now,
                "Auto-cogged out: cog check timed out before manual cog-out.",
                cancellationToken);

            return ToCogSessionDto(session);
        }

        if (cogCheckState.RequiresCheck)
        {
            throw new InvalidOperationException("Complete the current cog check before cogging out to receive payout.");
        }

        var durationMinutes = CalculateDurationMinutes(session.CogInAtUtc, now);
        var payoutCogs = CalculatePayoutCogs(session.CogInAtUtc, now);

        session.CogOutAtUtc = now;
        session.CogOutNote = Normalize(request.Note);
        session.AutoCogOutNoPayout = false;
        session.PayoutCogs = payoutCogs;

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = user.UserAccountId,
            Amount = payoutCogs,
            TransactionType = CogTransactionTypes.CogOut,
            Description = $"Cogged out after {durationMinutes} minute(s). Payout: {payoutCogs} cogs.",
            CreatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToCogSessionDto(session);
    }

    public async Task<IReadOnlyList<CogSessionDto>> GetCogSessionHistoryAsync(
        ClaimsPrincipal principal,
        int take,
        CancellationToken cancellationToken)
    {
        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);
        await AutoCloseExpiredSessionIfNeededAsync(user.UserAccountId, DateTime.UtcNow, cancellationToken);

        var sessions = await dbContext.CogSessions
            .AsNoTracking()
            .Where(x => x.UserAccountId == user.UserAccountId)
            .OrderByDescending(x => x.CogInAtUtc)
            .Take(take)
            .Select(x => new
            {
                x.CogSessionId,
                x.CogInAtUtc,
                x.CogOutAtUtc,
                x.WarningIntervalMinutesAtCogIn,
                x.SuccessfulCogChecks,
                x.AutoCogOutNoPayout,
                x.PayoutCogs,
                x.CogInNote,
                x.CogOutNote,
            })
            .ToListAsync(cancellationToken);

        return sessions
            .Select(x =>
            {
                var cogInAtUtc = AsUtc(x.CogInAtUtc);
                var cogOutAtUtc = x.CogOutAtUtc.HasValue ? AsUtc(x.CogOutAtUtc.Value) : (DateTime?)null;

                return new CogSessionDto(
                    x.CogSessionId,
                    cogInAtUtc,
                    cogOutAtUtc,
                    cogOutAtUtc == null ? null : CalculateDurationMinutes(cogInAtUtc, cogOutAtUtc.Value),
                    cogOutAtUtc == null,
                    NormalizeWarningInterval(x.WarningIntervalMinutesAtCogIn),
                    Math.Max(0, x.SuccessfulCogChecks),
                    x.AutoCogOutNoPayout,
                    x.PayoutCogs,
                    x.CogInNote,
                    x.CogOutNote);
            })
            .ToList();
    }

    public async Task<CogCheckStatusDto> GetCogCheckStatusAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);
        var now = DateTime.UtcNow;

        var openSession = await GetOpenCogSessionAsync(user.UserAccountId, cancellationToken);
        if (openSession is not null)
        {
            var checkState = EvaluateCogCheck(openSession, now);
            if (checkState.DeadlineMissed)
            {
                await AutoCogOutNoPayoutAsync(
                    openSession,
                    now,
                    "Auto-cogged out: cog check timed out.",
                    cancellationToken);
            }
            else
            {
                return BuildOpenCogCheckStatus(openSession, checkState);
            }
        }

        var latestSession = await dbContext.CogSessions
            .AsNoTracking()
            .Where(x => x.UserAccountId == user.UserAccountId)
            .OrderByDescending(x => x.CogInAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return BuildClosedCogCheckStatus(latestSession);
    }

    public async Task<CogCheckStatusDto> CompleteCogCheckAsync(
        ClaimsPrincipal principal,
        CompleteCogCheckRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SpinsCompleted < CogCheckRules.SpinsRequired)
        {
            throw new InvalidOperationException($"Spin the handle at least {CogCheckRules.SpinsRequired} times to pass cog check.");
        }

        var user = await currentUserService.EnsureUserAsync(principal, cancellationToken);
        var now = DateTime.UtcNow;

        var session = await GetOpenCogSessionAsync(user.UserAccountId, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException("No active shift found. Cog in first.");
        }

        var checkState = EvaluateCogCheck(session, now);
        if (checkState.DeadlineMissed)
        {
            await AutoCogOutNoPayoutAsync(
                session,
                now,
                "Auto-cogged out: cog check timed out.",
                cancellationToken);

            return BuildClosedCogCheckStatus(session);
        }

        if (!checkState.RequiresCheck)
        {
            throw new InvalidOperationException("No cog check is currently required.");
        }

        session.SuccessfulCogChecks += 1;

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = user.UserAccountId,
            Amount = 0,
            TransactionType = CogTransactionTypes.CogCheckCompleted,
            Description = $"Cog check completed with {request.SpinsCompleted} handle spins.",
            CreatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var postCheckState = EvaluateCogCheck(session, now);
        return BuildOpenCogCheckStatus(session, postCheckState);
    }

    private static CogSessionDto ToCogSessionDto(CogSession session)
    {
        var cogInAtUtc = AsUtc(session.CogInAtUtc);
        var cogOutAtUtc = session.CogOutAtUtc.HasValue ? AsUtc(session.CogOutAtUtc.Value) : (DateTime?)null;

        return new CogSessionDto(
            session.CogSessionId,
            cogInAtUtc,
            cogOutAtUtc,
            cogOutAtUtc == null ? null : CalculateDurationMinutes(cogInAtUtc, cogOutAtUtc.Value),
            cogOutAtUtc == null,
            NormalizeWarningInterval(session.WarningIntervalMinutesAtCogIn),
            Math.Max(0, session.SuccessfulCogChecks),
            session.AutoCogOutNoPayout,
            session.PayoutCogs,
            session.CogInNote,
            session.CogOutNote);
    }

    private static int CalculateDurationMinutes(DateTime startedAtUtc, DateTime endedAtUtc)
    {
        return Math.Max(0, (int)Math.Floor((endedAtUtc - startedAtUtc).TotalMinutes));
    }

    private static int CalculatePayoutCogs(DateTime startedAtUtc, DateTime endedAtUtc)
    {
        return Math.Max(0, (int)Math.Floor((endedAtUtc - startedAtUtc).TotalHours));
    }

    private async Task<int> GetWarningIntervalMinutesAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.CogRuntimeSettings
            .OrderBy(x => x.CogRuntimeSettingId)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new CogRuntimeSetting
            {
                WarningIntervalMinutes = 60,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserAccountId = null,
            };

            dbContext.CogRuntimeSettings.Add(settings);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return NormalizeWarningInterval(settings.WarningIntervalMinutes);
    }

    private async Task<CogSession?> GetOpenCogSessionAsync(int userAccountId, CancellationToken cancellationToken)
    {
        return await dbContext.CogSessions
            .Where(x => x.UserAccountId == userAccountId && x.CogOutAtUtc == null)
            .OrderByDescending(x => x.CogInAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task AutoCloseExpiredSessionIfNeededAsync(int userAccountId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var session = await GetOpenCogSessionAsync(userAccountId, cancellationToken);
        if (session is null)
        {
            return;
        }

        var checkState = EvaluateCogCheck(session, nowUtc);
        if (!checkState.DeadlineMissed)
        {
            return;
        }

        await AutoCogOutNoPayoutAsync(
            session,
            nowUtc,
            "Auto-cogged out: cog check timed out.",
            cancellationToken);
    }

    private async Task AutoCogOutNoPayoutAsync(
        CogSession session,
        DateTime nowUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        if (session.CogOutAtUtc.HasValue)
        {
            return;
        }

        session.CogOutAtUtc = nowUtc;
        session.AutoCogOutNoPayout = true;
        session.PayoutCogs = 0;
        session.CogOutNote = string.IsNullOrWhiteSpace(session.CogOutNote) ? reason : session.CogOutNote;

        dbContext.CogTransactions.Add(new CogTransaction
        {
            UserAccountId = session.UserAccountId,
            Amount = 0,
            TransactionType = CogTransactionTypes.CogOutNoPayout,
            Description = reason,
            CreatedAtUtc = nowUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CogCheckEvaluation EvaluateCogCheck(CogSession session, DateTime nowUtc)
    {
        var cogInAtUtc = AsUtc(session.CogInAtUtc);
        var warningIntervalMinutes = NormalizeWarningInterval(session.WarningIntervalMinutesAtCogIn);
        var successfulChecks = Math.Max(0, session.SuccessfulCogChecks);
        var elapsedMinutes = CalculateDurationMinutes(cogInAtUtc, nowUtc);
        var requiredChecks = elapsedMinutes / warningIntervalMinutes;

        var nextRequiredCheckNumber = successfulChecks + 1;
        var nextCheckAtUtc = AsUtc(cogInAtUtc.AddMinutes(nextRequiredCheckNumber * warningIntervalMinutes));
        var checkDeadlineAtUtc = AsUtc(nextCheckAtUtc.AddMinutes(CogCheckRules.CogCheckDeadlineMinutes));

        var requiresCheck = requiredChecks > successfulChecks && nowUtc >= nextCheckAtUtc && nowUtc <= checkDeadlineAtUtc;
        var deadlineMissed = requiredChecks > successfulChecks && nowUtc > checkDeadlineAtUtc;

        return new CogCheckEvaluation(
            requiredChecks,
            successfulChecks,
            warningIntervalMinutes,
            nextCheckAtUtc,
            checkDeadlineAtUtc,
            requiresCheck,
            deadlineMissed);
    }

    private static CogCheckStatusDto BuildOpenCogCheckStatus(CogSession session, CogCheckEvaluation checkState)
    {
        return new CogCheckStatusDto(
            true,
            session.CogSessionId,
            checkState.RequiresCheck,
            CogCheckRules.SpinsRequired,
            checkState.SuccessfulChecks,
            checkState.RequiredChecks,
            checkState.WarningIntervalMinutes,
            AsUtc(session.CogInAtUtc),
            checkState.NextCheckAtUtc,
            checkState.CheckDeadlineAtUtc,
            false,
            null,
            null);
    }

    private static CogCheckStatusDto BuildClosedCogCheckStatus(CogSession? latestSession)
    {
        if (latestSession is null)
        {
            return new CogCheckStatusDto(
                false,
                null,
                false,
                CogCheckRules.SpinsRequired,
                0,
                0,
                60,
                null,
                null,
                null,
                false,
                null,
                null);
        }

        return new CogCheckStatusDto(
            false,
            latestSession.CogSessionId,
            false,
            CogCheckRules.SpinsRequired,
            Math.Max(0, latestSession.SuccessfulCogChecks),
            Math.Max(0, latestSession.SuccessfulCogChecks),
            NormalizeWarningInterval(latestSession.WarningIntervalMinutesAtCogIn),
            AsUtc(latestSession.CogInAtUtc),
            null,
            null,
            latestSession.AutoCogOutNoPayout,
            latestSession.CogOutAtUtc.HasValue ? AsUtc(latestSession.CogOutAtUtc.Value) : (DateTime?)null,
            latestSession.PayoutCogs);
    }

    private static int NormalizeWarningInterval(int warningIntervalMinutes)
    {
        if (warningIntervalMinutes < CogCheckRules.MinimumWarningIntervalMinutes)
        {
            return 60;
        }

        return Math.Min(warningIntervalMinutes, CogCheckRules.MaximumWarningIntervalMinutes);
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record CogCheckEvaluation(
        int RequiredChecks,
        int SuccessfulChecks,
        int WarningIntervalMinutes,
        DateTime NextCheckAtUtc,
        DateTime CheckDeadlineAtUtc,
        bool RequiresCheck,
        bool DeadlineMissed);
}
