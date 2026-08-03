using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using RentMaster.Application.Common;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Security;

namespace RentMaster.Infrastructure.Persistence;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    public async Task SeedAsync()
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Unable to create role {role}: {string.Join(", ", result.Errors.Select(x => x.Description))}");
                }
            }
        }

        if (!configuration.GetValue<bool>("AdminSeed:Enabled"))
        {
            return;
        }

        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("AdminSeed is enabled, but email/password are not configured.");
        }

        email = email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Rent Master Administrator",
                LockoutEnabled = true,
                PublicProfileCode = TokenUtilities.CreatePublicProfileCode()
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to seed admin: {string.Join(", ", createResult.Errors.Select(x => x.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to assign the admin role: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
            }
        }
    }
}
