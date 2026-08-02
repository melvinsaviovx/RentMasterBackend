using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RentMaster.Application.Interfaces;
using RentMaster.Infrastructure.Configuration;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Persistence;
using RentMaster.Infrastructure.Security;
using RentMaster.Infrastructure.Services;
using RentMaster.Infrastructure.Storage;

namespace RentMaster.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "JWT issuer is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "JWT audience is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.SigningKey) && x.SigningKey.Length >= 64,
                "JWT signing key must contain at least 64 characters.")
            .Validate(x => x.AccessTokenMinutes is >= 1 and <= 1440,
                "JWT access-token lifetime must be between 1 and 1440 minutes.")
            .Validate(x => x.RefreshTokenDays is >= 1 and <= 365,
                "JWT refresh-token lifetime must be between 1 and 365 days.")
            .ValidateOnStart();

        services.AddOptions<VerificationOptions>()
            .Bind(configuration.GetSection(VerificationOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.NumberHashPepper) &&
                           x.NumberHashPepper.Length >= 32,
                "Verification number hash pepper must contain at least 32 characters.")
            .Validate(x => x.MaximumFileSizeBytes is > 0 and <= 20 * 1024 * 1024,
                "Verification maximum file size must be between 1 byte and 20 MB.")
            .Validate(x => x.RequiredDocuments is { Length: > 0 } &&
                           x.RequiredDocuments.All(value => Enum.IsDefined(value)),
                "At least one valid verification document type is required.")
            .Validate(x => x.RequiredDocuments.Distinct().Count() == x.RequiredDocuments.Length,
                "Verification document types must not contain duplicates.")
            .Validate(x => x.MinimumVerifiedDocuments > 0 &&
                           x.MinimumVerifiedDocuments <= x.RequiredDocuments.Distinct().Count(),
                "Verification minimum approved-document count must be between 1 and the number of accepted document types.")
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        // services.AddDbContext<AppDbContext>(options =>
        //     options.UseSqlServer(connectionString, sql =>
        //         sql.EnableRetryOnFailure(5)));
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));


        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false; // Enable in production after email delivery exists.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT settings are missing.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = System.Security.Claims.ClaimTypes.Name,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIdentityVerificationService, IdentityVerificationService>();
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IRentalApplicationService, RentalApplicationService>();
        services.AddScoped<ITenancyService, TenancyService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReputationService, ReputationService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddSingleton<IndiaDateProvider>();
        services.AddScoped<IDocumentStorage, DevelopmentDocumentStorage>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<DevelopmentDataSeeder>();

        return services;
    }
}
