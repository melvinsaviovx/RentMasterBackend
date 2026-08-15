using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentMaster.Domain.Common;
using RentMaster.Domain.Entities;
using RentMaster.Infrastructure.Identity;

namespace RentMaster.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<OwnerProfile> OwnerProfiles => Set<OwnerProfile>();
    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
    public DbSet<IdentityDocument> IdentityDocuments => Set<IdentityDocument>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<RentalApplication> RentalApplications => Set<RentalApplication>();
    public DbSet<Tenancy> Tenancies => Set<Tenancy>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewScore> ReviewScores => Set<ReviewScore>();
    public DbSet<ReviewDispute> ReviewDisputes => Set<ReviewDispute>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PropertyPhoto> PropertyPhotos => Set<PropertyPhoto>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.PublicProfileCode).HasMaxLength(20);
            entity.HasIndex(x => x.PublicProfileCode).IsUnique();
        });

        ConfigureBaseEntity<OwnerProfile>(builder);
        ConfigureBaseEntity<TenantProfile>(builder);
        ConfigureBaseEntity<IdentityDocument>(builder);
        ConfigureBaseEntity<Property>(builder);
        ConfigureBaseEntity<RentalApplication>(builder);
        ConfigureBaseEntity<Tenancy>(builder);
        ConfigureBaseEntity<Review>(builder);
        ConfigureBaseEntity<ReviewScore>(builder);
        ConfigureBaseEntity<ReviewDispute>(builder);
        ConfigureBaseEntity<RefreshToken>(builder);
        ConfigureBaseEntity<ChatConversation>(builder);
        ConfigureBaseEntity<ChatMessage>(builder);
        ConfigureBaseEntity<PropertyPhoto>(builder);
        ConfigureBaseEntity<SupportTicket>(builder);
        ConfigureBaseEntity<MaintenanceRequest>(builder);

        builder.Entity<OwnerProfile>(entity =>
        {
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.BusinessName).HasMaxLength(200);
            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<OwnerProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TenantProfile>(entity =>
        {
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<TenantProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentityDocument>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.DocumentType }).IsUnique();
            entity.HasIndex(x => new { x.DocumentType, x.NumberHash })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] IN (1, 2)");
            entity.Property(x => x.DocumentNumber).HasMaxLength(2048);
            entity.Property(x => x.NumberLast4).HasMaxLength(4);
            entity.Property(x => x.NumberHash).HasMaxLength(128);
            entity.Property(x => x.StorageObjectName).HasMaxLength(500);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.ConsentVersion).HasMaxLength(50);
            entity.Property(x => x.ConsentIpAddress).HasMaxLength(64);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Property>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.AddressLine1).HasMaxLength(250);
            entity.Property(x => x.Locality).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.State).HasMaxLength(120);
            entity.Property(x => x.PostalCode).HasMaxLength(12);
            entity.Property(x => x.MonthlyRent).HasPrecision(18, 2);
            entity.Property(x => x.SecurityDeposit).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.City, x.Locality, x.Status });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<PropertyPhoto>(entity =>
        {
            entity.Property(x => x.StorageObjectName).HasMaxLength(500);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.HasIndex(x => new { x.PropertyId, x.SortOrder });
            entity.HasOne(x => x.Property)
                .WithMany(x => x.Photos)
                .HasForeignKey(x => x.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SupportTicket>(entity =>
        {
            entity.Property(x => x.Category).HasMaxLength(80);
            entity.Property(x => x.Subject).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(3000);
            entity.Property(x => x.AdminReply).HasMaxLength(2000);
            entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        builder.Entity<MaintenanceRequest>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(3000);
            entity.Property(x => x.MaintenanceNote).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenancyId, x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.AssignedToUserId, x.Status, x.CreatedAtUtc });
            entity.HasOne(x => x.Tenancy)
                .WithMany()
                .HasForeignKey(x => x.TenancyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Property)
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RentalApplication>(entity =>
        {
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.DecisionReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.PropertyId, x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.TenantUserId, x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.PropertyId, x.TenantUserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] IN (1, 2, 3)");
            entity.HasOne(x => x.Property)
                .WithMany(x => x.RentalApplications)
                .HasForeignKey(x => x.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.TenantUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenancy)
                .WithMany()
                .HasForeignKey(x => x.TenancyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Tenancy>(entity =>
        {
            entity.Property(x => x.AgreedMonthlyRent).HasPrecision(18, 2);
            entity.Property(x => x.AgreedSecurityDeposit).HasPrecision(18, 2);
            entity.Property(x => x.EndRequestReason).HasMaxLength(500);
            entity.Property(x => x.EndRequestedByUserId).HasMaxLength(450);
            entity.Property(x => x.EndApprovedByUserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.PropertyId, x.Status });
            entity.HasIndex(x => x.PropertyId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] IN (1, 2, 3, 6, 7)");
            entity.HasOne(x => x.Property)
                .WithMany(x => x.Tenancies)
                .HasForeignKey(x => x.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.TenantUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Review>(entity =>
        {
            entity.HasIndex(x => new { x.TenancyId, x.ReviewerUserId }).IsUnique();
            entity.Property(x => x.Comment).HasMaxLength(1500);
            entity.Property(x => x.ModerationReason).HasMaxLength(500);
            entity.HasOne(x => x.Tenancy)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.TenancyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.ReviewerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.SubjectUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReviewScore>(entity =>
        {
            entity.HasIndex(x => new { x.ReviewId, x.Category }).IsUnique();
            entity.Property(x => x.Category).HasMaxLength(80);
            entity.HasOne(x => x.Review)
                .WithMany(x => x.Scores)
                .HasForeignKey(x => x.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReviewDispute>(entity =>
        {
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.Resolution).HasMaxLength(1000);
            entity.HasOne(x => x.Review)
                .WithMany(x => x.Disputes)
                .HasForeignKey(x => x.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            entity.Property(x => x.CreatedByIp).HasMaxLength(64);
            entity.Property(x => x.RevokedByIp).HasMaxLength(64);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        builder.Entity<ChatConversation>(entity =>
        {
            entity.HasIndex(x => new { x.PropertyId, x.OwnerUserId, x.TenantUserId }).IsUnique();
            entity.HasIndex(x => new { x.OwnerUserId, x.LastMessageAtUtc });
            entity.HasIndex(x => new { x.TenantUserId, x.LastMessageAtUtc });
            entity.HasIndex(x => x.RentalApplicationId)
                .IsUnique()
                .HasFilter("[RentalApplicationId] IS NOT NULL");
            entity.HasIndex(x => x.TenancyId)
                .IsUnique()
                .HasFilter("[TenancyId] IS NOT NULL");
            entity.HasOne(x => x.Property)
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.TenantUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RentalApplication)
                .WithMany()
                .HasForeignKey(x => x.RentalApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Tenancy)
                .WithMany()
                .HasForeignKey(x => x.TenancyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChatMessage>(entity =>
        {
            entity.Property(x => x.Content).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ConversationId, x.DeliveredAtUtc });
            entity.HasIndex(x => new { x.ConversationId, x.ReadAtUtc });
            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HttpMethod).HasMaxLength(10);
            entity.Property(x => x.Path).HasMaxLength(500);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.Property(x => x.TraceId).HasMaxLength(100);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.UserId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static void ConfigureBaseEntity<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
