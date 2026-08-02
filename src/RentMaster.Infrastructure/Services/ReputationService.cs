using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentMaster.Application.Common;
using RentMaster.Application.Contracts;
using RentMaster.Application.Interfaces;
using RentMaster.Domain.Enums;
using RentMaster.Infrastructure.Identity;
using RentMaster.Infrastructure.Persistence;

namespace RentMaster.Infrastructure.Services;

public sealed class ReputationService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager)
    : IReputationService
{
    public async Task<ReputationProfileDto> GetByCodeAsync(
        string profileCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profileCode))
            throw new ValidationException("Profile code is required.");

        var normalizedCode = profileCode.Trim().ToUpperInvariant();
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.PublicProfileCode == normalizedCode && x.IsActive,
                cancellationToken)
            ?? throw new NotFoundException("Reputation profile was not found.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Reviews.AsNoTracking()
            .Include(x => x.Scores)
            .Include(x => x.Disputes)
            .Where(x => x.SubjectUserId == user.Id &&
                        (x.Status == ReviewStatus.Published ||
                         x.Status == ReviewStatus.Disputed))
            .OrderByDescending(x => x.ModeratedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        double? average = total == 0
            ? null
            : await query.AverageAsync(x => (double)x.OverallRating, cancellationToken);

        var entities = await query.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var reviews = entities.Select(x => new PublicReviewDto(
            x.Id,
            x.Direction,
            x.Direction == ReviewDirection.OwnerToTenant
                ? "Verified owner"
                : "Verified tenant",
            x.OverallRating,
            x.Scores.Select(s => new ReviewScoreDto(s.Category, s.Score)).ToArray(),
            x.Comment,
            x.ModeratedAtUtc ?? x.UpdatedAtUtc,
            x.Status == ReviewStatus.Disputed ||
                x.Disputes.Any(d => !d.IsResolved)))
            .ToArray();

        var roles = await userManager.GetRolesAsync(user);

        return new ReputationProfileDto(
            user.PublicProfileCode,
            CreateDisplayName(user.FullName),
            roles.ToArray(),
            average.HasValue ? Math.Round(average.Value, 2) : null,
            total,
            new PagedResult<PublicReviewDto>(reviews, page, pageSize, total));
    }

    private static string CreateDisplayName(string fullName)
    {
        var parts = fullName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return "Verified user";

        if (parts.Length == 1)
            return parts[0];

        return $"{parts[0]} {parts[^1][0]}.";
    }
}
