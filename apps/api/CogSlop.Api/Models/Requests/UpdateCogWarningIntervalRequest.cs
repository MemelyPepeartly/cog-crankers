using System.ComponentModel.DataAnnotations;

namespace CogSlop.Api.Models.Requests;

public class UpdateCogWarningIntervalRequest
{
    [Range(5, 720)]
    public int WarningIntervalMinutes { get; set; } = 60;
}
