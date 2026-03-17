namespace CogSlop.Api.Models.Dtos;

public record CogSessionDto(
    int CogSessionId,
    DateTime CogInAtUtc,
    DateTime? CogOutAtUtc,
    int? DurationMinutes,
    bool IsOpen,
    int WarningIntervalMinutesAtCogIn,
    int SuccessfulCogChecks,
    bool AutoCogOutNoPayout,
    int? PayoutCogs,
    string? CogInNote,
    string? CogOutNote);
