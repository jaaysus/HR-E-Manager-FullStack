using Microsoft.AspNetCore.Identity;

namespace HrETracker.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool NotifyLowStock { get; set; } = true;
    public bool NotifyClothingActivity { get; set; } = true;
}
