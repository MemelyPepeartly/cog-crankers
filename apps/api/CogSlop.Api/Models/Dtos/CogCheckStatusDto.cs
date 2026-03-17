namespace CogSlop.Api.Models.Dtos;

public record CogCheckStatusDto(
    bool HasOpenSession,
    int? CogSessionId,
    bool RequiresCogCheck,
    int SpinsRequired,
    int SuccessfulCogChecks,
    int RequiredCogChecks,
    int WarningIntervalMinutes,
    DateTime? CogInAtUtc,
    DateTime? NextCogCheckAtUtc,
    DateTime? CogCheckDeadlineAtUtc,
    bool AutoCoggedOutNoPayout,
    DateTime? CogOutAtUtc,
    int? PayoutCogs);
