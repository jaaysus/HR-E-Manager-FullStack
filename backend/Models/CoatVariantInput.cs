using System.ComponentModel.DataAnnotations;
namespace HrETracker.Models;
public class CoatVariantInput
{
    [Required, StringLength(150)] public string Name { get; init; } = "";
    [Required, StringLength(50)] public string Color { get; init; } = "";
    [Required, StringLength(30)] public string Size { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public string? RowVersion { get; init; }
}
