using System.Security.Claims;
using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Requests;

namespace CogSlop.Api.Services;

public interface IEconomyService
{
    Task<DashboardDto> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<PurchaseReceiptDto> BuyGearAsync(ClaimsPrincipal principal, int gearItemId, int quantity, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoreItemDto>> GetStoreItemsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<CraftGearReceiptDto> CraftGearAsync(ClaimsPrincipal principal, CraftGearRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketplaceListingDto>> GetMarketplaceListingsAsync(CancellationToken cancellationToken);

    Task<MarketplaceListingDto> CreateMarketplaceListingAsync(
        ClaimsPrincipal principal,
        CreateMarketplaceListingRequest request,
        CancellationToken cancellationToken);

    Task<MarketplacePurchaseReceiptDto> BuyMarketplaceListingAsync(
        ClaimsPrincipal principal,
        int marketplaceListingId,
        CancellationToken cancellationToken);

    Task<CogSessionDto> CogInAsync(ClaimsPrincipal principal, CogInRequest request, CancellationToken cancellationToken);

    Task<CogSessionDto> CogOutAsync(ClaimsPrincipal principal, CogOutRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CogSessionDto>> GetCogSessionHistoryAsync(
        ClaimsPrincipal principal,
        int take,
        CancellationToken cancellationToken);

    Task<CogCheckStatusDto> GetCogCheckStatusAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<CogCheckStatusDto> CompleteCogCheckAsync(
        ClaimsPrincipal principal,
        CompleteCogCheckRequest request,
        CancellationToken cancellationToken);
}
