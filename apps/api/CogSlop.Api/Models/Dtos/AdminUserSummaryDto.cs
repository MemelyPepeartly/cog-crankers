namespace CogSlop.Api.Models.Dtos;

public record AdminUserSummaryDto(
    int UserAccountId,
    string DisplayName,
    string Email,
    int CogBalance,
    IReadOnlyList<string> Roles);
