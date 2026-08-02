using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RentMaster.Application.Common;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Entities;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Identity;

namespace RentMaster.Infrastructure.Persistence;

public sealed class DevelopmentDataSeeder(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext,
    IDocumentStorage documentStorage,
    IConfiguration configuration)
{
    private static readonly byte[] PlaceholderPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl8nWQAAAAASUVORK5CYII=");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("DemoSeed:Enabled"))
            return;

        var password = configuration["DemoSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("DemoSeed password is required when demo seeding is enabled.");

        var owner = await EnsureUserAsync(
            configuration["DemoSeed:OwnerEmail"] ?? "owner@rentmaster.local",
            "Demo Property Owner",
            "+919900000001",
            AppRoles.Owner,
            "RM-OWNER1",
            password,
            cancellationToken);

        var tenant = await EnsureUserAsync(
            configuration["DemoSeed:TenantEmail"] ?? "tenant@rentmaster.local",
            "Demo Tenant",
            "+919900000002",
            AppRoles.Tenant,
            "RM-TENANT1",
            password,
            cancellationToken);

        await EnsureVerifiedDocumentAsync(owner.Id, "111122223333", cancellationToken);
        await EnsureVerifiedDocumentAsync(tenant.Id, "444455556666", cancellationToken);

        var hasDemoProperty = await dbContext.Properties
            .AnyAsync(x => x.OwnerUserId == owner.Id && x.Title == "Demo 2 BHK in Thoraipakkam", cancellationToken);

        if (!hasDemoProperty)
        {
            dbContext.Properties.Add(new Property
            {
                OwnerUserId = owner.Id,
                Title = "Demo 2 BHK in Thoraipakkam",
                AddressLine1 = "Demo Street, Near IT Corridor",
                Locality = "Thoraipakkam",
                City = "Chennai",
                State = "Tamil Nadu",
                PostalCode = "600097",
                MonthlyRent = 22000,
                SecurityDeposit = 66000,
                Bedrooms = 2,
                Bathrooms = 2,
                Status = PropertyStatus.Published
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        string email,
        string fullName,
        string phone,
        string role,
        string profileCode,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                EmailConfirmed = true,
                PhoneNumber = phone,
                PhoneNumberConfirmed = true,
                FullName = fullName,
                PublicProfileCode = profileCode
            };

            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
                throw new InvalidOperationException($"Unable to create demo user {email}: {string.Join(", ", create.Errors.Select(x => x.Description))}");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addRole = await userManager.AddToRoleAsync(user, role);
            if (!addRole.Succeeded)
                throw new InvalidOperationException($"Unable to add demo role {role}: {string.Join(", ", addRole.Errors.Select(x => x.Description))}");
        }

        if (role == AppRoles.Owner && !await dbContext.OwnerProfiles.AnyAsync(x => x.UserId == user.Id, cancellationToken))
            dbContext.OwnerProfiles.Add(new OwnerProfile { UserId = user.Id });
        if (role == AppRoles.Tenant && !await dbContext.TenantProfiles.AnyAsync(x => x.UserId == user.Id, cancellationToken))
            dbContext.TenantProfiles.Add(new TenantProfile { UserId = user.Id });

        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task EnsureVerifiedDocumentAsync(
        string userId,
        string documentNumber,
        CancellationToken cancellationToken)
    {
        if (await dbContext.IdentityDocuments.AnyAsync(
                x => x.UserId == userId && x.DocumentType == IdentityDocumentType.Aadhaar,
                cancellationToken))
        {
            return;
        }

        var pepper = configuration["Verification:NumberHashPepper"]
            ?? throw new InvalidOperationException("Verification:NumberHashPepper is required for demo seeding.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        var numberHash = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"{IdentityDocumentType.Aadhaar}:{documentNumber}")));

        await using var stream = new MemoryStream(PlaceholderPng, writable: false);
        var objectName = await documentStorage.SaveAsync(
            userId,
            "demo-aadhaar.png",
            "image/png",
            stream,
            cancellationToken);

        dbContext.IdentityDocuments.Add(new IdentityDocument
        {
            UserId = userId,
            DocumentType = IdentityDocumentType.Aadhaar,
            NumberLast4 = documentNumber[^4..],
            NumberHash = numberHash,
            StorageObjectName = objectName,
            OriginalFileName = "demo-aadhaar.png",
            ContentType = "image/png",
            SizeBytes = PlaceholderPng.Length,
            ConsentVersion = "LOCAL-UAT-1.0",
            ConsentAcceptedAtUtc = DateTimeOffset.UtcNow,
            ConsentIpAddress = "local-seed",
            Status = VerificationStatus.Verified,
            ReviewedByUserId = "local-seed",
            ReviewedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
