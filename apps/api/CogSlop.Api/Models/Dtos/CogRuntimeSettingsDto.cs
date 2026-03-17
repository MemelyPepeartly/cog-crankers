namespace CogSlop.Api.Models.Dtos;

public record CogRuntimeSettingsDto(
    int WarningIntervalMinutes,
    DateTime UpdatedAtUtc,
    int? UpdatedByUserAccountId,
    string? UpdatedByDisplayName);
