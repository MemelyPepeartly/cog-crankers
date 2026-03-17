namespace CogSlop.Api.Models.Dtos;

public record DashboardDto(
    UserProfileDto Pilot,
    IReadOnlyList<StoreItemDto> StoreFront);
