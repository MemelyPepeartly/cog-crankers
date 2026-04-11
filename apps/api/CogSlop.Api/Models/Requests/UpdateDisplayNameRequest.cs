using System.ComponentModel.DataAnnotations;
using CogSlop.Api.Services;

namespace CogSlop.Api.Models.Requests;

public class UpdateDisplayNameRequest
{
    [Required]
    [MaxLength(DisplayNameRules.MaxLength)]
    public string DisplayName { get; set; } = string.Empty;
}
