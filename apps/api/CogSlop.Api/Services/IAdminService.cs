using System.Security.Claims;
using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Requests;

namespace CogSlop.Api.Services;

public interface IAdminService
{
    Task<IReadOnlyList<AdminUserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StoreItemDto>> GetGearItemsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<AdminUserSummaryDto> GrantCogsAsync(ClaimsPrincipal adminPrincipal, GrantCogsRequest request, CancellationToken cancellationToken);

    Task<InventoryItemDto> GrantGearAsync(ClaimsPrincipal adminPrincipal, GrantGearRequest request, CancellationToken cancellationToken);

    Task<StoreItemDto> CreateGearItemAsync(UpsertGearItemRequest request, CancellationToken cancellationToken);

    Task<StoreItemDto> UpdateGearItemAsync(int gearItemId, UpsertGearItemRequest request, CancellationToken cancellationToken);

    Task<CogRuntimeSettingsDto> GetCogRuntimeSettingsAsync(CancellationToken cancellationToken);

    Task<CogRuntimeSettingsDto> UpdateWarningIntervalAsync(
        ClaimsPrincipal adminPrincipal,
        UpdateCogWarningIntervalRequest request,
        CancellationToken cancellationToken);
}
