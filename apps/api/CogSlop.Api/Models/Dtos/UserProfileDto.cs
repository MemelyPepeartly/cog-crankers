namespace CogSlop.Api.Models.Dtos;

public record UserProfileDto(
    int UserAccountId,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    int CogBalance,
    IReadOnlyList<string> Roles,
    IReadOnlyList<InventoryItemDto> Inventory,
    bool IsAdmin);
