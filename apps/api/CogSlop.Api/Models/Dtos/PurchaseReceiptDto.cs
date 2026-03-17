namespace CogSlop.Api.Models.Dtos;

public record PurchaseReceiptDto(
    string Message,
    int QuantityPurchased,
    int CogsSpent,
    int NewCogBalance,
    InventoryItemDto Gear);
