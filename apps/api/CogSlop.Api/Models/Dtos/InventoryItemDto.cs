namespace CogSlop.Api.Models.Dtos;

public record InventoryItemDto(
    int GearItemId,
    string Name,
    string GearType,
    int Quantity,
    string? FlavorText,
    int CostInCogs);
