using System.ComponentModel.DataAnnotations;

namespace CogSlop.Api.Models.Requests;

public class CompleteCogCheckRequest
{
    [Range(15, 200)]
    public int SpinsCompleted { get; set; } = 15;
}
