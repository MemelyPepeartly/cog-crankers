namespace CogSlop.Api.Models.Dtos;

public record StoreItemDto(
    int GearItemId,
    string Name,
    string? Description,
    string GearType,
    int CostInCogs,
    int? StockQuantity,
    bool IsActive,
    string? FlavorText);
