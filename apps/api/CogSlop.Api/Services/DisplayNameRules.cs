namespace CogSlop.Api.Services;

public static class DisplayNameRules
{
    public const int MaxLength = 120;

    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public static bool IsConfigured(string? value)
    {
        return Normalize(value) is not null;
    }
}
