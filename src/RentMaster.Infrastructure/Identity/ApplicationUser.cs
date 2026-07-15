using Microsoft.AspNetCore.Identity;

namespace RentMaster.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public required string FullName { get; set; }
    public required string PublicProfileCode { get; set; }
    public bool IsActive { get; set; } = true;
}
